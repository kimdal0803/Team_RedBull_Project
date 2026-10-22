#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ReversalTower.Integration;

namespace ReversalTower.PlayerSetup.Editor
{
    /// <summary>
    /// 씬에 있는 플레이어의 수치를 전투방식설계서 v0.4 기준으로 한 번에 맞춘다.
    ///
    /// 왜 이게 필요한가:
    ///   스크립트의 `= 5f` 같은 기본값은 '새로 만든 컴포넌트'에만 적용된다.
    ///   씬에 이미 올라가 있는 플레이어는 예전 값이 저장돼 있어서, 코드를 고쳐도 안 바뀐다.
    ///   그래서 SerializedObject로 직접 덮어쓴다.
    ///
    /// 메뉴: 역전의 탑 ▸ 플레이어 수치를 설계서 기준으로 맞추기
    ///
    /// 되돌리기: Ctrl+Z 한 번으로 전부 되돌아간다 (Undo에 묶어 뒀다).
    /// </summary>
    public static class PlayerSpecStatsApplier
    {
        // ── 전투방식설계서 v0.4  2.1 기본 스탯 ──────────────────────────
        const int   MaxHealth          = 100;    // 보스 일반 공격 5대면 사망
        const int   BaseAttackDamage   = 10;     // 모든 스킬 계수의 기준
        const float MoveSpeed          = 7f;     // 보스방 20m를 약 2.9초에 횡단
        const float JumpForce          = 10.8f;  // 점프 높이 1.8m, 정점 0.33s
        const float GravityScale       = 3.3028f;// -9.81 × 3.3028 ≈ -32.4
        const float DashSpeed          = 20f;    // 20 × 0.2s = 4m
        const float DashDuration       = 0.2f;
        const float ParryWindow        = 0.15f;  // 설계서 4.1 판정 0.15s
        const float ParryCooldown      = 0.35f;  // 설계서 4.1 실패 후딜 0.35s
        const float HitStunDuration    = 0.15f;  // 설계서 2.1 피격 경직 0.15s
        const int   MaxPotionCount     = 3;
        const int   PotionHealAmount   = 40;
        const float PotionUseDuration  = 0.9f;

        // ── 설계서 2.2 / 2.3 (새로 추가된 항목) ─────────────────────────
        const float CoyoteTime            = 0.1f;
        const float JumpBufferTime        = 0.12f;
        const float VariableJumpCut       = 0.5f;
        const int   DashMaxStacks         = 2;
        const float DashStackRecharge     = 2.5f;
        const float DashMinInterval       = 0.3f;
        const float DashInvincibleTime    = 0.15f;

        // ── 어댑터 ──────────────────────────────────────────────────
        // 체력이 100이 됐으므로 보스 피해를 깎을 이유가 없다. 1:1로 전달한다.
        const float AdapterDamageScale = 1f;

        [MenuItem("역전의 탑/플레이어 수치를 설계서 기준으로 맞추기", false, 20)]
        public static void Apply()
        {
            var player = Object.FindAnyObjectByType<PlayerMovement>();

            if (player == null)
            {
                EditorUtility.DisplayDialog(
                    "플레이어를 찾지 못했습니다",
                    "열려 있는 씬에 PlayerMovement가 붙은 오브젝트가 없습니다.\n" +
                    "플레이어가 있는 씬을 연 뒤 다시 실행해 주세요.",
                    "확인");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "플레이어 수치 설계서 정렬");

            var so = new SerializedObject(player);
            int changed = 0;

            changed += SetInt  (so, "maxHealth",                 MaxHealth);
            changed += SetInt  (so, "baseAttackDamage",          BaseAttackDamage);
            changed += SetFloat(so, "moveSpeed",                 MoveSpeed);
            changed += SetFloat(so, "jumpForce",                 JumpForce);
            changed += SetFloat(so, "dashSpeed",                 DashSpeed);
            changed += SetFloat(so, "dashDuration",              DashDuration);
            changed += SetFloat(so, "parryWindow",               ParryWindow);
            changed += SetFloat(so, "parryCooldown",             ParryCooldown);
            changed += SetFloat(so, "hitStunDuration",           HitStunDuration);
            changed += SetInt  (so, "maxPotionCount",            MaxPotionCount);
            changed += SetInt  (so, "potionHealAmount",          PotionHealAmount);
            changed += SetFloat(so, "potionUseDuration",         PotionUseDuration);

            changed += SetFloat(so, "coyoteTime",                CoyoteTime);
            changed += SetFloat(so, "jumpBufferTime",            JumpBufferTime);
            changed += SetFloat(so, "variableJumpCutMultiplier", VariableJumpCut);
            changed += SetInt  (so, "dashMaxStacks",             DashMaxStacks);
            changed += SetFloat(so, "dashStackRechargeTime",     DashStackRecharge);
            changed += SetFloat(so, "dashMinInterval",           DashMinInterval);
            changed += SetFloat(so, "dashInvincibleTime",        DashInvincibleTime);

            so.ApplyModifiedProperties();

            // ── 중력 ──
            // 점프 높이 1.8m를 맞추려면 중력이 -32.4여야 한다.
            // Unity 기본 중력이 -9.81이므로 gravityScale로 환산한다.
            var rb = player.GetComponent<Rigidbody2D>();
            string gravityNote;

            if (rb != null)
            {
                Undo.RecordObject(rb, "중력 정렬");
                rb.gravityScale = GravityScale;
                EditorUtility.SetDirty(rb);
                gravityNote = $"Rigidbody2D.gravityScale = {GravityScale:0.####} (중력 ≈ -32.4)";
            }
            else
            {
                gravityNote = "⚠ Rigidbody2D를 찾지 못했습니다 — 중력은 직접 맞춰 주세요";
            }

            // ── 피격 어댑터 ──
            var adapter = Object.FindAnyObjectByType<PlayerBossHitAdapter>();
            string adapterNote;

            if (adapter != null)
            {
                Undo.RecordObject(adapter, "어댑터 피해 환산 정렬");
                adapter.damageScale = AdapterDamageScale;
                adapter.minDamage = 1;
                EditorUtility.SetDirty(adapter);
                adapterNote = $"PlayerBossHitAdapter.damageScale = {AdapterDamageScale} (보스 피해를 그대로 전달)";
            }
            else
            {
                adapterNote = "· 피격 어댑터는 씬에 없습니다 (아라쿠스 테스트 씬에서만 필요)";
            }

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);

            Debug.Log(
                $"[설계서 정렬] {player.name} — 필드 {changed}개 적용\n" +
                $"  체력 {MaxHealth} / 공격력 {BaseAttackDamage} / 이동 {MoveSpeed}m\\s\n" +
                $"  대시 {DashSpeed}×{DashDuration}s = 4m, 스택 {DashMaxStacks} / 충전 {DashStackRecharge}s / 무적 {DashInvincibleTime}s\n" +
                $"  패링 판정 {ParryWindow}s / 실패 후딜 {ParryCooldown}s\n" +
                $"  포션 {MaxPotionCount}개 / 회복 {PotionHealAmount}\n" +
                $"  {gravityNote}\n" +
                $"  {adapterNote}",
                player);

            EditorUtility.DisplayDialog(
                "설계서 기준으로 맞췄습니다",
                $"{player.name}\n\n" +
                $"필드 {changed}개를 바꿨습니다.\n" +
                $"{gravityNote}\n{adapterNote}\n\n" +
                "되돌리려면 Ctrl+Z 를 누르세요.\n" +
                "씬 저장(Ctrl+S)을 잊지 마세요.",
                "확인");
        }

        // ── SerializedProperty 헬퍼 ──
        // 필드 이름이 바뀌었거나 없으면 조용히 건너뛴다 (0을 돌려준다).

        static int SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning($"[설계서 정렬] 필드를 찾지 못했습니다: {path}");
                return 0;
            }
            p.floatValue = value;
            return 1;
        }

        static int SetInt(SerializedObject so, string path, int value)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning($"[설계서 정렬] 필드를 찾지 못했습니다: {path}");
                return 0;
            }
            p.intValue = value;
            return 1;
        }
    }
}
#endif
