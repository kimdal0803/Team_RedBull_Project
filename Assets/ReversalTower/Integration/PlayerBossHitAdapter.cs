using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// FSM 보스(BossController)의 공격을 정훈의 PlayerMovement에 전달하는 어댑터.
    ///
    /// PlayerMovement를 한 줄도 고치지 않는다. 공개된 것만 쓴다.
    ///   IsParrying      (패링 중인지)
    ///   CurrentHealth   (현재 체력)
    ///   TakeDamage(int, Vector2)
    ///
    /// 결과 판정은 '체력이 실제로 줄었는가'로 한다. 이게 가장 정확하다.
    ///   체력이 줄었다        → Hit
    ///   안 줄었고 패링 중이었다 → Parried  (보스가 경직에 걸린다)
    ///   안 줄었고 살아 있다     → Missed   (= 회피. PlayerMovement에서 피해를 막는 것은 패링과 대시 무적뿐이다)
    ///   그 밖 (사망 후, 회피불가 공격을 대시로 흘림, 패링 불가 공격을 패링으로 막음) → Ignored
    ///
    /// 대시로 피한 타를 Ignored로 두면 DDA의 대응 성공률((패링 + 회피) ÷ (날아온 타 − 무시))에서
    /// 분모 · 분자가 같이 빠져, 대시를 잘 쓰는 플레이어일수록 성공률이 낮게 잡힌다 (DDA 설계서 1 · 7장).
    ///
    /// 붙이는 곳: 플레이어 오브젝트 (PlayerMovement와 같은 오브젝트 또는 그 자식)
    /// </summary>
    [AddComponentMenu("역전의 탑/플레이어 피격 어댑터 (FSM 보스 연결)")]
    public class PlayerBossHitAdapter : MonoBehaviour, IBossAttackReceiver
    {
        [Header("연결")]
        [Tooltip("비워 두면 부모·자신에서 찾고, 그래도 없으면 씬에서 찾는다")]
        public PlayerMovement player;

        [Header("피해 환산")]
        [Tooltip("FSM 보스의 피해(실수, 일반 공격 ≈ 20)를 플레이어 체력 단위(정수, 최대 5)로 줄이는 비율.\n" +
                 "0.05면 보스 20 피해 → 1. 플레이어 최대 체력을 올렸으면 이 값도 같이 올린다.")]
        [Min(0.001f)] public float damageScale = 0.05f;
        [Tooltip("환산 결과가 0이 되지 않도록 하는 최소 피해")]
        [Min(0)] public int minDamage = 1;

        [Header("기록")]
        [Tooltip("대시 무적으로 흘린 타를 회피(Missed)로 센다. 끄면 예전처럼 무시(Ignored)로 센다.")]
        public bool countDashAsDodge = true;

        [Header("넉백")]
        [Tooltip("PlayerMovement.TakeDamage에 넘길 넉백 방향의 위쪽 성분")]
        public float knockbackUp = 0.5f;

        [Header("디버그")]
        public bool logToConsole = true;

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerMovement>();
            if (player == null) player = GetComponentInChildren<PlayerMovement>();
            if (player == null) player = FindAnyObjectByType<PlayerMovement>();
            if (player == null)
                Debug.LogWarning("[피격 어댑터] PlayerMovement를 찾지 못했습니다. Player 칸에 직접 넣어 주세요.", this);
        }

        public HitResult ReceiveBossHit(in BossHitInfo info)
        {
            if (player == null) return HitResult.Ignored;

            int damage = Mathf.Max(minDamage, Mathf.RoundToInt(info.Damage * damageScale));
            bool wasParrying = player.IsParrying;
            int before = player.CurrentHealth;

            var knockback = new Vector2(info.Direction, knockbackUp).normalized;
            player.TakeDamage(damage, knockback);

            int after = player.CurrentHealth;
            HitResult result;

            if (after < before) result = HitResult.Hit;
            else if (wasParrying && info.Parryable) result = HitResult.Parried;
            else if (countDashAsDodge && damage > 0 && !wasParrying && !info.IgnoresDodge && before > 0) result = HitResult.Missed;
            else result = HitResult.Ignored;

            if (logToConsole)
            {
                string note = "";
                // 기획서상 회피불가 공격은 무적을 뚫어야 하지만, PlayerMovement를 고치지 않는 한
                // 대시·피격 무적이 그대로 막는다. 기록에 남도록 표시만 해 둔다.
                if (info.IgnoresDodge && result == HitResult.Ignored)
                    note = "  ※ 회피불가 공격인데 무적으로 막혔습니다 (PlayerMovement 보강 필요)";
                else if (wasParrying && !info.Parryable && result != HitResult.Hit)
                    note = "  ※ 패링 불가 공격인데 패링으로 막혔습니다 (PlayerMovement 보강 필요)";

                Debug.Log($"[피격 어댑터] {info.PatternId} {info.HitIndex + 1}타 → {result} " +
                          $"(보스 {info.Damage:0.#} → 플레이어 {damage}){note}", this);
            }

            return result;
        }
    }
}
