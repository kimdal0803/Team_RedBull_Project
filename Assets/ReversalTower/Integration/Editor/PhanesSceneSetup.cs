using System.Collections.Generic;
using System.IO;
using ReversalTower.Boss;
using ReversalTower.Boss.EditorTools;
using ReversalTower.Boss.Phanes;
using ReversalTower.Boss.Phanes.EditorTools;
using ReversalTower.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReversalTower.Integration.EditorTools
{
    /// <summary>
    /// 파네스(2보스)를 시험할 씬을 메뉴 한 번으로 만든다.
    ///
    ///   역전의 탑 ▸ ★ 3. 파네스 테스트 씬 만들기 (안전, 복제본)
    ///       BossTestScene을 복제해 PhanesTestScene을 만들고 거기에만 파네스를 세운다. 원본은 건드리지 않는다.
    ///       더미 보스·아라쿠스·돌진 기둥은 지우지 않고 끄기만 한다.
    ///
    /// 다시 눌러도 안전하다. 이미 있는 것은 건너뛰고 값만 다시 맞춘다.
    /// 게임 흐름(로비 → 1보스 → 2보스) 연결은 씬 이동 담당과 정한 뒤 따로 한다.
    /// </summary>
    public static class PhanesSceneSetup
    {
        const string SourceScene = "Assets/Scenes/BossTestScene.unity";
        const string TestScene = "Assets/Scenes/PhanesTestScene.unity";
        const string BossObjectName = "Phanes";
        const string CollectorObjectName = "CombatDataCollector";

        // 정훈 프로토타입 수치 기준 (플레이어 체력 5, 공격력 1~3). 아라쿠스 60보다 조금 높게 — 임시
        const float BossMaxHp = 70f;
        const float BossAttackPower = 20f;
        const int RewardHp = 60;

        [MenuItem("역전의 탑/★ 3. 파네스 테스트 씬 만들기 (안전, 복제본)", false, -98)]
        public static void SetUpTestScene()
        {
            if (!File.Exists(SourceScene))
            {
                EditorUtility.DisplayDialog("역전의 탑", $"원본 씬을 찾지 못했습니다:\n{SourceScene}", "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists(TestScene))
            {
                AssetDatabase.CopyAsset(SourceScene, TestScene);
                AssetDatabase.Refresh();
                Debug.Log($"[파네스 씬 세팅] {SourceScene} → {TestScene} 복제");
            }
            AddSceneToBuildSettings(TestScene);
            RunSetup(TestScene);
        }

        static void RunSetup(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. 레이어
            int playerLayer = ArakusSamplePatterns.EnsureLayer("Player");
            int groundLayer = LayerMask.NameToLayer("Ground");
            int wallLayer = LayerMask.NameToLayer("Wall");

            // 2. 패턴 에셋
            PhanesSamplePatterns.CreateAll();

            // 3. 다른 보스는 끈다 (지우지 않는다). 위치는 더미 보스 자리를 쓴다.
            Vector3 spawn = Vector3.zero;
            bool foundSpot = false;
            var dummy = Object.FindAnyObjectByType<DummyBoss>(FindObjectsInactive.Include);
            if (dummy != null)
            {
                spawn = dummy.transform.position;
                foundSpot = true;
                Disable(dummy.gameObject, "더미 보스 끄기");
            }
            foreach (var other in Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other.name == BossObjectName) continue;
                if (!foundSpot) { spawn = other.transform.position; foundSpot = true; }
                Disable(other.gameObject, "다른 보스 끄기");
            }
            foreach (var pillar in Object.FindObjectsByType<ChargeStopper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Disable(pillar.gameObject, "아라쿠스 돌진 기둥 끄기");
            if (!foundSpot) Debug.LogWarning("[파네스 씬 세팅] 더미 보스를 찾지 못해 원점에 세웁니다. 위치를 옮겨 주세요.");

            // 4. 파네스 본체
            var bossGo = GameObject.Find(BossObjectName);
            if (bossGo == null)
            {
                bossGo = new GameObject(BossObjectName);
                Undo.RegisterCreatedObjectUndo(bossGo, "파네스 만들기");
            }
            bossGo.transform.position = spawn;
            bossGo.layer = 0;

            var boss = GetOrAdd<BossController>(bossGo);
            boss.bossId = "Phanes";
            boss.maxHp = BossMaxHp;
            boss.attackPower = BossAttackPower;
            boss.bodyWidth = 0.8f;
            boss.bodyHeight = 2.0f;
            boss.moveSpeed = 0f;        // 상시 비행 — 걷지 않는다
            boss.idleTime = 0.5f;
            if (playerLayer >= 0) boss.targetMask = 1 << playerLayer;
            boss.logToConsole = true;
            boss.showDebugOverlay = true;

            // 공중에 떠 있으므로 트리거 — 플레이어가 보스 위에 올라서지 않게
            var col = GetOrAdd<BoxCollider2D>(bossGo);
            col.isTrigger = true;
            col.size = new Vector2(boss.bodyWidth, boss.bodyHeight);
            col.offset = new Vector2(0f, boss.bodyHeight * 0.5f);

            // 그림 자리 (비어 있으면 Play 때 PhanesBrain이 임시 마법사 실루엣을 만든다)
            var visual = bossGo.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(bossGo.transform, false);
                Undo.RegisterCreatedObjectUndo(visual.gameObject, "파네스 그림 자리");
            }
            boss.visualRoot = visual;

            var arena = GetOrAdd<PhanesArena>(bossGo);
            arena.autoFit = true;
            if (groundLayer >= 0) arena.groundMask = 1 << groundLayer;
            int wallMask = 1; // Default
            if (groundLayer >= 0) wallMask |= 1 << groundLayer;
            if (wallLayer >= 0) wallMask |= 1 << wallLayer;
            arena.wallMask = wallMask;

            GetOrAdd<PhanesMotor>(bossGo);
            var brain = GetOrAdd<PhanesBrain>(bossGo);
            brain.buildPlaceholderVisual = true;

            var ui = GetOrAdd<BossUIBridge>(bossGo);
            ui.displayName = "파네스";
            ui.rewardHpOverride = RewardHp;

            GetOrAdd<BossHitboxVisualizer>(bossGo); // 파네스 판정은 스킬이 직접 그리지만, 왼쪽 위 패턴 이름 표시는 쓸모 있다

            // 5. 패턴 + 페이즈 전환
            PhanesSamplePatterns.Assign(boss, includeExtended: true);
            EditorUtility.SetDirty(bossGo);

            // 6. 플레이어 쪽 연결 (아라쿠스와 같은 어댑터)
            var player = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (player != null)
            {
                GetOrAdd<PlayerBossHitAdapter>(player.gameObject).player = player;
                GetOrAdd<PlayerCombatEventRelay>(player.gameObject).player = player;
                if (playerLayer >= 0 && player.gameObject.layer != playerLayer)
                {
                    Undo.RecordObject(player.gameObject, "플레이어 레이어 맞추기");
                    player.gameObject.layer = playerLayer;
                }
                EditorUtility.SetDirty(player.gameObject);
            }
            else
            {
                Debug.LogWarning("[파네스 씬 세팅] PlayerMovement를 찾지 못했습니다. 플레이어에 " +
                                 "'Player Boss Hit Adapter'와 'Player Combat Event Relay'를 직접 붙여 주세요.");
            }

            // 7. 전투 데이터 수집기 → 파네스
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

            // 8. 저장
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string summary =
                "파네스 세팅 완료\n\n" +
                $"씬          {scenePath}\n" +
                $"보스        {BossObjectName}  (체력 {BossMaxHp:0}, 공격력 {BossAttackPower:0})\n" +
                $"패턴        {boss.patterns.Count}개 + 50% 2페이즈 전환(단절의 결계)\n" +
                $"그림        임시 마법사 실루엣 (Play 때 생성)\n" +
                $"플레이어    {(player != null ? "어댑터·릴레이 연결됨" : "직접 붙여야 함")}\n\n" +
                "Play를 누르면 전투 구역(바닥·벽)을 자동으로 잽니다.\n" +
                "왼쪽 위에 보스 상태와 그로기 게이지가 보입니다.";
            Debug.Log("[파네스 씬 세팅] " + summary.Replace("\n\n", "\n"));
            EditorUtility.DisplayDialog("역전의 탑", summary, "좋아요");

            Selection.activeGameObject = bossGo;
            EditorGUIUtility.PingObject(bossGo);
        }

        static void Disable(GameObject go, string undoName)
        {
            if (go == null || !go.activeSelf) return;
            Undo.RecordObject(go, undoName);
            go.SetActive(false);
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == scenePath) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[파네스 씬 세팅] Build Settings에 {scenePath}를 추가했습니다.");
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }
    }
}
