using System.IO;
using ReversalTower.Boss;
using ReversalTower.Boss.EditorTools;
using ReversalTower.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReversalTower.Integration.EditorTools
{
    /// <summary>
    /// 아라쿠스 시험용 씬을 통째로 만들어 준다. 손으로 하던 20여 단계를 메뉴 한 번으로 끝낸다.
    ///
    ///   메뉴 ▸ 역전의 탑 ▸ ★ 아라쿠스 테스트 씬 만들기 (전자동)
    ///
    /// 하는 일
    ///   1. BossTestScene을 복제해 ArakusTestScene을 만든다 (원본은 건드리지 않는다)
    ///   2. Player·Wall 레이어가 없으면 만든다
    ///   3. 아라쿠스 패턴 에셋 11개를 만든다
    ///   4. 기존 DummyBoss를 '지우지 않고' 비활성화한다
    ///   5. 그 자리에 BossController 보스를 세우고 값을 맞춘다
    ///   6. 플레이어에 피격 어댑터·이벤트 릴레이를 붙인다
    ///   7. 전투 데이터 수집기를 놓고 보스를 연결한다
    ///   8. 돌진 기믹 기둥을 보스와 플레이어 사이에 놓는다
    ///   9. 씬을 저장한다
    ///
    /// 다시 실행해도 안전하다. 이미 있는 것은 건너뛰고 값만 다시 맞춘다.
    /// </summary>
    public static class ArakusSceneSetup
    {
        const string SourceScene = "Assets/Scenes/BossTestScene.unity";
        const string TargetScene = "Assets/Scenes/ArakusTestScene.unity";
        const string BossObjectName = "Arakus";
        const string CollectorObjectName = "CombatDataCollector";

        // 정훈 프로토타입의 수치에 맞춘 값 (플레이어 최대 체력 5, 공격력 1~3 기준)
        const float BossMaxHp = 60f;
        const float BossAttackPower = 20f;
        const int RewardHp = 50;

        [MenuItem("역전의 탑/★ 아라쿠스 테스트 씬 만들기 (전자동)", false, -100)]
        public static void SetUpEverything()
        {
            if (!File.Exists(SourceScene))
            {
                EditorUtility.DisplayDialog("역전의 탑",
                    $"원본 씬을 찾지 못했습니다:\n{SourceScene}\n\n" +
                    "이 메뉴는 정훈 프로토타입(BossTestScene이 있는 프로젝트)에서 실행해야 합니다.", "확인");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[씬 세팅] 취소했습니다.");
                return;
            }

            // ── 1. 씬 복제 ──
            if (!File.Exists(TargetScene))
            {
                AssetDatabase.CopyAsset(SourceScene, TargetScene);
                AssetDatabase.Refresh();
                Debug.Log($"[씬 세팅] {SourceScene} → {TargetScene} 복제");
            }
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

            // ── 2. 레이어 ──
            int playerLayer = ArakusSamplePatterns.EnsureLayer("Player");
            int wallLayer = ArakusSamplePatterns.EnsureLayer("Wall");

            // ── 3. 패턴 에셋 ──
            ArakusSamplePatterns.CreateAll();

            // ── 4. 기존 더미 보스 비활성화 ──
            Vector3 bossPosition = Vector3.zero;
            bool foundDummy = false;
            var dummy = Object.FindAnyObjectByType<DummyBoss>(FindObjectsInactive.Include);
            if (dummy != null)
            {
                bossPosition = dummy.transform.position;
                foundDummy = true;
                if (dummy.gameObject.activeSelf)
                {
                    Undo.RecordObject(dummy.gameObject, "더미 보스 끄기");
                    dummy.gameObject.SetActive(false);
                    Debug.Log($"[씬 세팅] 기존 '{dummy.gameObject.name}'을(를) 껐습니다 (지우지 않았습니다).", dummy.gameObject);
                }
            }
            else
            {
                Debug.LogWarning("[씬 세팅] DummyBoss를 찾지 못해 보스를 원점에 세웁니다. 위치를 직접 옮겨 주세요.");
            }

            // ── 5. 아라쿠스 세우기 ──
            var bossGo = GameObject.Find(BossObjectName);
            if (bossGo == null)
            {
                bossGo = new GameObject(BossObjectName);
                Undo.RegisterCreatedObjectUndo(bossGo, "아라쿠스 만들기");
            }
            bossGo.transform.position = bossPosition;

            var boss = GetOrAdd<BossController>(bossGo);
            boss.bossId = "Arakus";
            boss.maxHp = BossMaxHp;
            boss.attackPower = BossAttackPower;
            if (playerLayer >= 0) boss.targetMask = 1 << playerLayer;
            if (wallLayer >= 0) boss.wallMask = 1 << wallLayer;
            boss.logToConsole = true;
            boss.showDebugOverlay = true;

            var col = GetOrAdd<BoxCollider2D>(bossGo);
            col.size = new Vector2(0.5f, 2.4f);
            col.offset = new Vector2(0f, 1.2f);
            col.isTrigger = false;

            var uiBridge = GetOrAdd<BossUIBridge>(bossGo);
            uiBridge.displayName = "아라쿠스";
            uiBridge.rewardHpOverride = RewardHp;

            // 패턴 9개 + 페이즈 전환 3개
            ArakusSamplePatterns.Assign(boss);
            EditorUtility.SetDirty(bossGo);

            // ── 6. 플레이어에 다리 붙이기 ──
            var player = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (player != null)
            {
                var adapter = GetOrAdd<PlayerBossHitAdapter>(player.gameObject);
                adapter.player = player;

                var relay = GetOrAdd<PlayerCombatEventRelay>(player.gameObject);
                relay.player = player;

                if (playerLayer >= 0 && player.gameObject.layer != playerLayer)
                {
                    Undo.RecordObject(player.gameObject, "플레이어 레이어 맞추기");
                    player.gameObject.layer = playerLayer;
                    Debug.Log("[씬 세팅] 플레이어 레이어를 'Player'로 바꿨습니다.", player.gameObject);
                }
                EditorUtility.SetDirty(player.gameObject);
            }
            else
            {
                Debug.LogWarning("[씬 세팅] PlayerMovement를 찾지 못했습니다. 플레이어에 " +
                                 "'Player Boss Hit Adapter'와 'Player Combat Event Relay'를 직접 붙여 주세요.");
            }

            // ── 7. 수집기 ──
            var collectorGo = GameObject.Find(CollectorObjectName);
            if (collectorGo == null)
            {
                collectorGo = new GameObject(CollectorObjectName);
                Undo.RegisterCreatedObjectUndo(collectorGo, "수집기 만들기");
            }
            var collector = GetOrAdd<CombatDataCollector>(collectorGo);
            collector.boss = boss;
            collector.autoStartOnPlay = true;
            collector.keepEventLog = true;
            collector.logToConsole = true;
            EditorUtility.SetDirty(collectorGo);

            // ── 8. 돌진 기믹 기둥 ──
            var pillar = Object.FindAnyObjectByType<ChargeStopper>(FindObjectsInactive.Include);
            if (pillar == null)
            {
                var created = ArakusSamplePatterns.CreateChargeGimmickObject(boss);
                if (created != null && player != null)
                {
                    // 보스와 플레이어 사이에 놓는다
                    float mid = (boss.transform.position.x + player.transform.position.x) * 0.5f;
                    created.transform.position = new Vector3(mid, boss.transform.position.y, 0f);
                }
            }

            // ── 9. 저장 ──
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string summary =
                "아라쿠스 테스트 씬 세팅 완료\n\n" +
                $"씬          {TargetScene}\n" +
                $"보스        {BossObjectName}  (체력 {BossMaxHp:0}, 공격력 {BossAttackPower:0})\n" +
                $"패턴        {boss.patterns.Count}개 + 페이즈 전환 {(boss.phaseTriggers != null ? boss.phaseTriggers.Length : 0)}개\n" +
                $"기존 더미   {(foundDummy ? "껐습니다 (지우지 않음)" : "찾지 못함")}\n" +
                $"플레이어    {(player != null ? "어댑터·릴레이 연결됨" : "직접 붙여야 함")}\n" +
                $"수집기      {CollectorObjectName}\n\n" +
                "이제 Play를 누르고 Game 창을 클릭한 뒤 보스에게 다가가 보세요.";

            Debug.Log("[씬 세팅] " + summary.Replace("\n\n", "\n"));
            EditorUtility.DisplayDialog("역전의 탑", summary, "좋아요");

            Selection.activeGameObject = bossGo;
            EditorGUIUtility.PingObject(bossGo);
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null)
            {
                c = Undo.AddComponent<T>(go);
            }
            return c;
        }
    }
}
