using System.Collections.Generic;
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
    /// 아라쿠스를 씬에 세우는 일을 통째로 자동으로 한다. 손으로 하던 20여 단계를 메뉴 한 번으로 끝낸다.
    ///
    ///   역전의 탑 ▸ ★ 1. 아라쿠스 테스트 씬 만들기 (안전, 복제본)
    ///       BossTestScene을 복제해 ArakusTestScene을 만들고 거기에만 아라쿠스를 세운다.
    ///       원본은 건드리지 않는다. 혼자 시험할 때 쓴다.
    ///
    ///   역전의 탑 ▸ ★ 2. 게임 흐름에 아라쿠스 넣기 (BossTestScene 교체)
    ///       BossTestScene 자체를 고친다. 타이틀 → 로비 → 포탈로 들어가면 아라쿠스가 나온다.
    ///       기존 DummyBoss는 지우지 않고 끄기만 한다. 깃으로 되돌릴 수 있다.
    ///
    /// 두 메뉴 모두 다시 눌러도 안전하다. 이미 있는 것은 건너뛰고 값만 다시 맞춘다.
    /// </summary>
    public static class ArakusSceneSetup
    {
        const string SourceScene = "Assets/Scenes/BossTestScene.unity";
        const string DuplicateScene = "Assets/Scenes/ArakusTestScene.unity";
        const string DarkKnightPrefab = "Assets/MyAssets/Dark Knight/Prefabs/Dark Knight.prefab";

        const string BossObjectName = "Arakus";
        const string CollectorObjectName = "CombatDataCollector";
        const string VisualObjectName = "Visual";

        // 정훈 프로토타입의 수치에 맞춘 값 (플레이어 최대 체력 5, 공격력 1~3 기준)
        const float BossMaxHp = 60f;
        const float BossAttackPower = 20f;
        const int RewardHp = 50;

        /// <summary>패턴 ID → 애니메이터 트리거 (Dark Knight의 Knight Pivot 컨트롤러 기준)</summary>
        static readonly Dictionary<string, string> AttackTriggers = new Dictionary<string, string>
        {
            { "Arakus_NormalAttack_R", "Attack" },
            { "Arakus_NormalAttack_L", "Attack" },
            { "Arakus_NormalAttack_F", "Attack" },
            { "Arakus_SwordSwinging",  "Attack" },
            { "Arakus_SweepingStrike", "Attack" },
            { "Arakus_ShieldDash",     "Attack" },
            { "Arakus_Charge",         "Attack" },
            { "Arakus_ShieldThrowDash", "BeamAttack" },
            { "Arakus_ShieldThrowSlam", "BeamAttack" },
            { "Arakus_Rage",            "BeamAttack" },
            { "Arakus_QuickStep",       "BackStep" },
        };

        // ─────────────────────────── 메뉴 ───────────────────────────

        [MenuItem("역전의 탑/★ 1. 아라쿠스 테스트 씬 만들기 (안전, 복제본)", false, -100)]
        public static void SetUpDuplicateScene()
        {
            if (!CheckSourceScene()) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (!File.Exists(DuplicateScene))
            {
                AssetDatabase.CopyAsset(SourceScene, DuplicateScene);
                AssetDatabase.Refresh();
                Debug.Log($"[씬 세팅] {SourceScene} → {DuplicateScene} 복제");
            }
            AddSceneToBuildSettings(DuplicateScene);
            RunSetup(DuplicateScene, "테스트 씬 (복제본)");
        }

        [MenuItem("역전의 탑/★ 2. 게임 흐름에 아라쿠스 넣기 (BossTestScene 교체)", false, -99)]
        public static void SetUpRealScene()
        {
            if (!CheckSourceScene()) return;

            bool ok = EditorUtility.DisplayDialog("역전의 탑",
                "BossTestScene을 직접 고칩니다.\n\n" +
                "타이틀 → 로비 → 포탈로 들어가면 더미 보스 대신 아라쿠스가 나옵니다.\n" +
                "기존 DummyBoss는 지우지 않고 끄기만 합니다.\n\n" +
                "되돌리려면 깃에서 BossTestScene.unity를 revert 하면 됩니다.\n\n" +
                "진행할까요?", "진행", "취소");
            if (!ok) return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RunSetup(SourceScene, "게임 흐름 (BossTestScene)");
        }

        static bool CheckSourceScene()
        {
            if (File.Exists(SourceScene)) return true;
            EditorUtility.DisplayDialog("역전의 탑",
                $"원본 씬을 찾지 못했습니다:\n{SourceScene}\n\n" +
                "이 메뉴는 팀 프로젝트(BossTestScene이 있는 곳)에서 실행해야 합니다.", "확인");
            return false;
        }

        // ─────────────────────────── 본체 ───────────────────────────

        static void RunSetup(string scenePath, string label)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. 레이어
            int playerLayer = ArakusSamplePatterns.EnsureLayer("Player");
            int wallLayer = ArakusSamplePatterns.EnsureLayer("Wall");

            // 2. 패턴 에셋 + 애니메이터 트리거
            ArakusSamplePatterns.CreateAll();
            ApplyAnimTriggers();

            // 3. 기존 더미 보스 끄기 (지우지 않는다)
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
                }
            }
            else
            {
                Debug.LogWarning("[씬 세팅] DummyBoss를 찾지 못해 보스를 원점에 세웁니다. 위치를 직접 옮겨 주세요.");
            }

            // 4. 아라쿠스 본체
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
            boss.bodyWidth = 0.9f;   // 아래 콜라이더와 같은 폭 — 판정 박스가 몸 앞쪽부터 잡힌다
            if (playerLayer >= 0) boss.targetMask = 1 << playerLayer;
            if (wallLayer >= 0) boss.wallMask = 1 << wallLayer;
            boss.logToConsole = true;
            boss.showDebugOverlay = true;

            var col = GetOrAdd<BoxCollider2D>(bossGo);
            col.size = new Vector2(0.9f, 2.4f);
            col.offset = new Vector2(0f, 1.2f);
            col.isTrigger = false;

            var uiBridge = GetOrAdd<BossUIBridge>(bossGo);
            uiBridge.displayName = "아라쿠스";
            uiBridge.rewardHpOverride = RewardHp;

            GetOrAdd<BossHitboxVisualizer>(bossGo);

            // 5. 그림 (Dark Knight 에셋)
            bool visualOk = AttachVisual(boss);

            // 6. 패턴 연결
            ArakusSamplePatterns.Assign(boss);
            EditorUtility.SetDirty(bossGo);

            // 7. 플레이어에 다리 붙이기
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
                Debug.LogWarning("[씬 세팅] PlayerMovement를 찾지 못했습니다. 플레이어에 " +
                                 "'Player Boss Hit Adapter'와 'Player Combat Event Relay'를 직접 붙여 주세요.");
            }

            // 8. 수집기
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

            // 9. 돌진 기믹 기둥
            var pillar = Object.FindAnyObjectByType<ChargeStopper>(FindObjectsInactive.Include);
            if (pillar == null)
            {
                var created = ArakusSamplePatterns.CreateChargeGimmickObject(boss);
                if (created != null && player != null)
                {
                    float mid = (boss.transform.position.x + player.transform.position.x) * 0.5f;
                    created.transform.position = new Vector3(mid, boss.transform.position.y, 0f);
                }
            }

            // 10. 저장
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string summary =
                $"아라쿠스 세팅 완료 — {label}\n\n" +
                $"씬          {scenePath}\n" +
                $"보스        {BossObjectName}  (체력 {BossMaxHp:0}, 공격력 {BossAttackPower:0})\n" +
                $"그림        {(visualOk ? "Dark Knight 적용됨" : "없음 — 보이지 않습니다")}\n" +
                $"패턴        {boss.patterns.Count}개 + 페이즈 전환 {(boss.phaseTriggers != null ? boss.phaseTriggers.Length : 0)}개\n" +
                $"기존 더미   {(foundDummy ? "껐습니다 (지우지 않음)" : "찾지 못함")}\n" +
                $"플레이어    {(player != null ? "어댑터·릴레이 연결됨" : "직접 붙여야 함")}\n" +
                $"패턴 표시   공격 범위가 게임 화면에 색으로 보입니다\n\n" +
                "Play를 누르고 Game 창을 클릭한 뒤 보스에게 다가가 보세요.";

            Debug.Log("[씬 세팅] " + summary.Replace("\n\n", "\n"));
            EditorUtility.DisplayDialog("역전의 탑", summary, "좋아요");

            Selection.activeGameObject = bossGo;
            EditorGUIUtility.PingObject(bossGo);
        }

        // ─────────────────────── 그림 붙이기 ───────────────────────

        /// <summary>Dark Knight 프리팹을 보스의 그림으로 붙인다. 이미 있으면 다시 연결만 한다.</summary>
        static bool AttachVisual(BossController boss)
        {
            var existing = boss.transform.Find(VisualObjectName);
            if (existing != null)
            {
                WireVisual(boss, existing.gameObject);
                return true;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DarkKnightPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[씬 세팅] 보스 그림을 찾지 못했습니다: {DarkKnightPrefab}\n" +
                                 "보스가 보이지 않습니다. 원하는 스프라이트를 보스 자식으로 넣고 " +
                                 "BossController의 Visual Root에 연결해 주세요.");
                return false;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boss.transform);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = VisualObjectName;
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            // 그림만 남기고 원본 에셋의 AI·물리는 떼어낸다 (순서 중요: 스크립트 → 콜라이더 → 리지드바디)
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Object.DestroyImmediate(mb, true);
            foreach (var c in inst.GetComponentsInChildren<Collider2D>(true))
                if (c != null) Object.DestroyImmediate(c, true);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody2D>(true))
                if (rb != null) Object.DestroyImmediate(rb, true);

            FitToBossSize(boss, inst);
            WireVisual(boss, inst);
            Undo.RegisterCreatedObjectUndo(inst, "보스 그림 붙이기");
            return true;
        }

        /// <summary>보스 키(bodyHeight)에 맞춰 크기를 조정하고, 발밑이 보스 피벗에 오도록 맞춘다.</summary>
        static void FitToBossSize(BossController boss, GameObject visual)
        {
            if (!TryGetRendererBounds(visual, out var bounds)) return;
            if (bounds.size.y < 0.01f) return;

            float scale = boss.bodyHeight / bounds.size.y;
            visual.transform.localScale = Vector3.one * scale;

            if (!TryGetRendererBounds(visual, out bounds)) return;
            Vector3 feet = boss.transform.position;
            visual.transform.position += new Vector3(feet.x - bounds.center.x, feet.y - bounds.min.y, 0f);
        }

        static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds();
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            bool first = true;
            foreach (var r in renderers)
            {
                if (r == null || r.sprite == null) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return !first;
        }

        static void WireVisual(BossController boss, GameObject visual)
        {
            boss.visualRoot = visual.transform;
            var animator = visual.GetComponentInChildren<Animator>();
            boss.animator = animator;

            var binder = GetOrAdd<BossVisualBinder>(boss.gameObject);
            binder.animator = animator;
        }

        // ─────────────────── 애니메이터 트리거 ───────────────────

        /// <summary>패턴 에셋의 공격 트리거 칸만 채운다. 이미 적혀 있으면 건드리지 않는다.</summary>
        static void ApplyAnimTriggers()
        {
            int changed = 0;
            foreach (var kv in AttackTriggers)
            {
                string path = $"Assets/Data/Boss/Arakus/{kv.Key}.asset";
                var p = AssetDatabase.LoadAssetAtPath<BossPatternData>(path);
                if (p == null || !string.IsNullOrEmpty(p.attackAnimTrigger)) continue;
                p.attackAnimTrigger = kv.Value;
                EditorUtility.SetDirty(p);
                changed++;
            }
            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[씬 세팅] 패턴 {changed}개에 공격 애니메이션 트리거를 넣었습니다.");
            }
        }

        // ─────────────────────── 도우미 ───────────────────────

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == scenePath) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[씬 세팅] Build Settings에 {scenePath}를 추가했습니다.");
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = Undo.AddComponent<T>(go);
            return c;
        }
    }
}
