using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // 보스 그림: Scale3x로 해상도를 올린 Royal Knight (검 + 방패 — 아라쿠스 설정과 맞다)
        const string KnightIdleSheet = "Assets/MyAssets/RoyalKnight_x3/Combat_Ready_Idle.png";
        const string EffectPrefabFolder = "Assets/Data/Effects";

        const string BossObjectName = "Arakus";
        const string CollectorObjectName = "CombatDataCollector";
        const string VisualObjectName = "Visual";

        // 정훈 프로토타입의 수치에 맞춘 값 (플레이어 최대 체력 5, 공격력 1~3 기준)
        const float BossMaxHp = 60f;
        const float BossAttackPower = 20f;
        const int RewardHp = 50;

        /// <summary>
        /// 패턴 ID → 애니메이터 트리거 (ArtImportSetup이 만드는 Arakus.controller 기준).
        /// 패턴마다 다른 모션이 나오도록 일부러 흩어 놓았다 — 11개가 똑같아 보이면
        /// 플레이어가 패턴을 구분하지 못하고 DDA 데이터도 의미가 없어진다.
        /// </summary>
        static readonly Dictionary<string, string> AttackTriggers = new Dictionary<string, string>
        {
            { "Arakus_NormalAttack_R",  "Attack" },        // 공격 1
            { "Arakus_NormalAttack_L",  "Attack2" },       // 공격 2
            { "Arakus_NormalAttack_F",  "Attack3" },       // 공격 3
            { "Arakus_SwordSwinging",   "Attack2" },       // 거검 — 큰 동작
            { "Arakus_SweepingStrike",  "Attack3" },       // 일대 휩쓸기
            { "Arakus_ShieldDash",      "ShieldCharge" },  // 방패 들고 전진
            { "Arakus_Charge",          "ShieldCharge" },  // 돌진 기믹
            { "Arakus_ShieldThrowDash", "BeamAttack" },    // 방패 투척 — 검 치켜들기
            { "Arakus_ShieldThrowSlam", "BeamAttack" },
            { "Arakus_Rage",            "Channel" },       // 분노 — 기 모으기
            { "Arakus_QuickStep",       "BackStep" },      // 구르기
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

            // 3. 플레이어를 먼저 찾아 둔다 (다리 연결·정렬 레이어에 쓴다)
            var player = Object.FindAnyObjectByType<PlayerMovement>(FindObjectsInactive.Include);

            // 4. 기존 더미 보스 끄기 (지우지 않는다)
            //    BossController의 피벗은 '발밑'인데 DummyBoss는 피벗이 몸 가운데라
            //    위치를 그대로 쓰면 보스가 공중에 뜬다. 바닥 Y를 따로 구한다.
            Vector3 bossPosition = Vector3.zero;
            bool foundDummy = false;
            var dummy = Object.FindAnyObjectByType<DummyBoss>(FindObjectsInactive.Include);
            if (dummy != null)
            {
                bossPosition = dummy.transform.position;
                bossPosition.y = BottomOf(dummy.gameObject, bossPosition.y);
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

            // 더미가 없으면 그 자리에서 아래로 쏘아 바닥을 찾는다.
            // 플레이어가 선 바닥을 기준으로 삼으면 안 된다 — 맵이 여러 층이면
            // 언덕 위에 있어야 할 보스가 1층으로 내려와 버린다.
            if (!foundDummy)
                bossPosition.y = GroundBelow(bossPosition, bossPosition.y);

            // 5. 아라쿠스 본체
            var bossGo = GameObject.Find(BossObjectName);
            bool bossIsNew = bossGo == null;
            if (bossIsNew)
            {
                bossGo = new GameObject(BossObjectName);
                Undo.RegisterCreatedObjectUndo(bossGo, "아라쿠스 만들기");
            }
            // 더미가 있으면 그 자리가 기획된 보스 자리다 — X·Y 모두 그 자리로 되돌린다.
            // (직접 옮겨 두었다면 이 메뉴를 다시 누를 때 원래 자리로 돌아간다)
            Undo.RecordObject(bossGo.transform, "아라쿠스 위치");
            if (foundDummy || bossIsNew)
                bossGo.transform.position = bossPosition;
            Debug.Log($"[씬 세팅] 아라쿠스 위치 {bossGo.transform.position} " +
                      $"({(foundDummy ? "DummyBoss가 있던 자리의 발밑" : "아래로 찾은 바닥")})");

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

            // 바닥 붙기(중력) — 언덕을 오르내리게 한다. 플레이어와 같은 Ground 마스크를 쓴다.
            var snap = GetOrAdd<BossGroundSnap>(bossGo);
            snap.groundMask = ReadPlayerGroundMask(player);

            // 6. 그림 (Royal Knight)
            bool visualOk = AttachVisual(boss);

            // 7. 패턴 연결
            ArakusSamplePatterns.Assign(boss);
            EditorUtility.SetDirty(bossGo);

            // 8. 플레이어에 다리 붙이기
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

            // 7-2. 보스가 배경에 가려지지 않게 정렬 레이어 맞추기
            FixSorting(boss, player);

            // 7-3. 패턴별 이펙트 연결
            int fxCount = AssignEffects(boss);

            // 7-4. 소리 + 상태 연출 (패링·그로기·페이즈 전환·사망)
            GetOrAdd<BossStateFx>(bossGo);
            int sfxCount = AssignAudio(boss);

            // 7-5. 연출 확인용 치트키 (1~5). 발표·빌드 전에 꺼야 한다.
            GetOrAdd<BossFxTester>(bossGo);

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
                $"이펙트      패턴 {fxCount}개에 연결됨\n" +
                $"소리        패턴 {sfxCount}개 + 패링·그로기·페이즈·사망\n" +
                $"패턴 표시   공격 범위가 게임 화면에 색으로 보입니다\n\n" +
                "Play를 누르고 Game 창을 클릭한 뒤 보스에게 다가가 보세요.";

            Debug.Log("[씬 세팅] " + summary.Replace("\n\n", "\n"));
            EditorUtility.DisplayDialog("역전의 탑", summary, "좋아요");

            Selection.activeGameObject = bossGo;
            EditorGUIUtility.PingObject(bossGo);
        }

        // ─────────────────────── 그림 붙이기 ───────────────────────

        /// <summary>
        /// 보스의 그림을 만든다. SpriteRenderer + Animator 하나짜리 단순 구조라
        /// ArtImportSetup이 만든 애니메이션 클립이 그대로 붙는다.
        /// 이미 있으면 다시 연결만 한다.
        /// </summary>
        static bool AttachVisual(BossController boss)
        {
            var idle = AssetDatabase.LoadAllAssetsAtPath(KnightIdleSheet).OfType<Sprite>().FirstOrDefault();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ArtImportSetup.ControllerPath);

            if (idle == null)
            {
                Debug.LogWarning($"[씬 세팅] 보스 스프라이트를 찾지 못했습니다: {KnightIdleSheet}\n" +
                                 "먼저 메뉴 '역전의 탑 ▸ 아트 ▸ ★ 스프라이트 슬라이스 · 애니메이션 만들기'를 실행하세요.");
                return false;
            }

            var existingT = boss.transform.Find(VisualObjectName);
            GameObject inst;
            if (existingT != null)
            {
                inst = existingT.gameObject;
            }
            else
            {
                inst = new GameObject(VisualObjectName);
                inst.transform.SetParent(boss.transform, false);
                Undo.RegisterCreatedObjectUndo(inst, "보스 그림 붙이기");
            }

            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            var sr = inst.GetComponent<SpriteRenderer>();
            if (sr == null) sr = inst.AddComponent<SpriteRenderer>();
            sr.sprite = idle;

            var anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            if (controller == null)
                Debug.LogWarning("[씬 세팅] Arakus.controller가 없어 애니메이션은 비워 둡니다. " +
                                 "'아트 ▸ 스프라이트 슬라이스' 메뉴를 먼저 실행하세요.");

            FitToBossSize(boss, inst);
            WireVisual(boss, inst);
            EditorUtility.SetDirty(inst);
            return true;
        }

        /// <summary>보스 그림이 배경에 가려지지 않도록 플레이어와 같은 정렬 레이어에 둔다.</summary>
        static void FixSorting(BossController boss, PlayerMovement player)
        {
            var bossSr = boss.GetComponentInChildren<SpriteRenderer>();
            if (bossSr == null) return;

            var playerSr = player != null ? player.GetComponentInChildren<SpriteRenderer>() : null;
            if (playerSr != null)
            {
                bossSr.sortingLayerID = playerSr.sortingLayerID;
                bossSr.sortingOrder = playerSr.sortingOrder - 1;   // 플레이어 바로 뒤
            }
            else
            {
                bossSr.sortingOrder = 10;
            }
            EditorUtility.SetDirty(bossSr);
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

        // ─────────────────────── 이펙트 연결 ───────────────────────

        /// <summary>
        /// Assets/Data/Effects의 프리팹을 패턴마다 하나씩 돌려가며 배정한다.
        /// 어느 이펙트가 어느 패턴에 어울리는지는 코드가 알 수 없으니, 일단 전부 다르게 깔아 두고
        /// 플레이해 보면서 Inspector에서 바꾸면 된다. 목적은 '패턴이 서로 달라 보이게' 하는 것이다.
        /// </summary>
        static int AssignEffects(BossController boss)
        {
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { EffectPrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null)
                .ToList();

            if (prefabs.Count == 0)
            {
                Debug.LogWarning("[씬 세팅] 이펙트 프리팹이 없습니다. " +
                                 "'역전의 탑 ▸ 아트 ▸ ★ 스프라이트 슬라이스 · 애니메이션 만들기'를 먼저 실행하세요.");
                return 0;
            }

            var player = GetOrAdd<BossEffectPlayer>(boss.gameObject);

            // 일반 패턴 + 강제 패턴(페이즈 전환)을 모두 모은다
            var all = new List<BossPatternData>(boss.patterns);
            if (boss.phaseTriggers != null)
                foreach (var t in boss.phaseTriggers)
                    if (t?.forcedPatterns != null)
                        foreach (var p in t.forcedPatterns)
                            if (p != null && !all.Contains(p)) all.Add(p);

            player.patternEffects.Clear();
            int i = 0;
            foreach (var p in all)
            {
                if (p == null || string.IsNullOrEmpty(p.patternId)) continue;
                player.patternEffects.Add(new BossEffectPlayer.PatternEffect
                {
                    patternId = p.patternId,
                    prefab = prefabs[i % prefabs.Count],
                    // 판정 박스 한가운데쯤에 뜨도록 대충 맞춘다
                    forward = Mathf.Max(0.4f, p.hits != null && p.hits.Length > 0 ? p.hits[0].box.width * 0.5f : 1f),
                    height = 1.1f,
                    scale = 1f,
                });
                i++;
            }

            // 타격·패링은 공통으로 쓴다 (목록의 뒤쪽에서 하나씩)
            player.hitEffect = prefabs[prefabs.Count - 1];
            player.parryEffect = prefabs[Mathf.Max(0, prefabs.Count - 2)];
            player.commonScale = 0.7f;

            EditorUtility.SetDirty(player);
            return player.patternEffects.Count;
        }

        // ─────────────────────── 소리 연결 ───────────────────────

        const string SoundFolder = "Assets/MyAssets/Dark Knight/Sounds";

        /// <summary>
        /// 패턴 ID → (효과음 파일, 피치).
        /// 지금 쓸 수 있는 클립이 6개뿐이라 피치로 패턴을 구분한다.
        /// 낮을수록 묵직하고 큰 공격처럼, 높을수록 가볍고 빠른 동작처럼 들린다.
        /// 진짜 효과음이 생기면 Inspector에서 클립만 갈아끼우면 된다.
        /// </summary>
        static readonly (string pattern, string clip, float pitch)[] PatternSounds =
        {
            ("Arakus_NormalAttack_R",  "sword",      1.00f),  // 일반 공격 1
            ("Arakus_NormalAttack_L",  "sword",      1.15f),  // 일반 공격 2 — 조금 가볍게
            ("Arakus_NormalAttack_F",  "sword",      1.30f),  // 발차기 — 가장 가볍게
            ("Arakus_SwordSwinging",   "sword",      0.70f),  // 거검 — 묵직
            ("Arakus_SweepingStrike",  "sword",      0.58f),  // 일대 휩쓸기 — 가장 묵직
            ("Arakus_ShieldDash",      "footstep",   0.80f),  // 방패 돌진 — 둔탁한 발
            ("Arakus_Charge",          "beam",       0.65f),  // 돌진 기믹 — 크고 낮게
            ("Arakus_ShieldThrowDash", "beam",       1.25f),  // 방패 투척 — 날카롭게
            ("Arakus_ShieldThrowSlam", "beam",       0.90f),  // 내려찍기
            ("Arakus_Rage",            "power_load", 1.00f),  // 분노 — 기 모으기
            ("Arakus_QuickStep",       "footstep",   1.45f),  // 퀵스텝 — 가벼운 스텝
        };

        static int AssignAudio(BossController boss)
        {
            var audio = GetOrAdd<BossAudio>(boss.gameObject);

            audio.patternSounds.Clear();
            int count = 0;
            foreach (var (pattern, clip, pitch) in PatternSounds)
            {
                var c = LoadClip(clip);
                if (c == null) continue;
                audio.patternSounds.Add(new BossAudio.PatternSound
                {
                    patternId = pattern,
                    clip = c,
                    pitch = pitch,
                    volume = 1f,
                });
                count++;
            }

            // 상태 소리 — 화면 연출과 짝이 되도록
            audio.parrySuccess = new BossAudio.Cue(LoadClip("sword"), 1.85f, 1f);        // 높은 쇳소리
            audio.groggy = new BossAudio.Cue(LoadClip("pain"), 0.55f, 1f);               // 둔탁하게
            audio.phaseChange = new BossAudio.Cue(LoadClip("power_load"), 0.80f, 1f);
            audio.death = new BossAudio.Cue(LoadClip("DeathExplosion"), 1.00f, 1f);
            audio.bossHurt = new BossAudio.Cue(LoadClip("pain"), 1.05f, 0.5f);

            if (count == 0)
                Debug.LogWarning($"[씬 세팅] 효과음을 찾지 못했습니다: {SoundFolder}\n" +
                                 "소리 없이 진행합니다. 클립이 생기면 보스의 Boss Audio에 직접 넣어 주세요.");

            EditorUtility.SetDirty(audio);
            return count;
        }

        static AudioClip LoadClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundFolder}/{name}.wav");
        }

        // ─────────────────── 애니메이터 트리거 ───────────────────

        /// <summary>
        /// 패턴 에셋의 공격 트리거 칸을 채운다.
        /// 보스 그림이 바뀌면 트리거 이름도 바뀌므로 기존 값을 덮어쓴다 (생성된 데이터라 안전하다).
        /// </summary>
        static void ApplyAnimTriggers()
        {
            int changed = 0;
            foreach (var kv in AttackTriggers)
            {
                string path = $"Assets/Data/Boss/Arakus/{kv.Key}.asset";
                var p = AssetDatabase.LoadAssetAtPath<BossPatternData>(path);
                if (p == null || p.attackAnimTrigger == kv.Value) continue;
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

        /// <summary>
        /// 오브젝트의 '발밑' 월드 Y를 구한다 (콜라이더 → 스프라이트 순서로 본다).
        /// BossController는 피벗이 발밑이라, 피벗이 몸 가운데인 오브젝트의 위치를
        /// 그대로 쓰면 보스가 공중에 뜬다.
        /// </summary>
        static float BottomOf(GameObject go, float fallback)
        {
            var col = go.GetComponentInChildren<Collider2D>();
            if (col != null && col.bounds.size.y > 0.01f) return col.bounds.min.y;

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null && sr.bounds.size.y > 0.01f) return sr.bounds.min.y;

            return fallback;
        }

        /// <summary>
        /// 플레이어가 바닥으로 쓰는 레이어 마스크를 그대로 가져온다.
        /// PlayerMovement.groundLayer는 private이라 SerializedObject로 읽는다.
        /// 둘이 같은 바닥을 봐야 보스와 플레이어가 같은 지형에 선다.
        /// </summary>
        static LayerMask ReadPlayerGroundMask(PlayerMovement player)
        {
            if (player != null)
            {
                var so = new SerializedObject(player);
                var prop = so.FindProperty("groundLayer");
                if (prop != null && prop.intValue != 0) return prop.intValue;
            }

            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) return 1 << ground;

            Debug.LogWarning("[씬 세팅] 바닥 레이어를 찾지 못했습니다. " +
                             "보스의 Boss Ground Snap ▸ Ground Mask를 직접 지정해 주세요.");
            return 0;
        }

        /// <summary>
        /// 그 자리에서 아래로 쏘아 가장 가까운 바닥의 윗면 Y를 찾는다.
        /// 맵이 여러 층이어도 '그 위치의 바닥'을 찾으므로 언덕 위 보스도 맞게 선다.
        /// 더미 보스가 없을 때만 쓰는 보조 수단이다.
        /// </summary>
        static float GroundBelow(Vector3 from, float fallback)
        {
            var hits = Physics2D.RaycastAll(new Vector2(from.x, from.y + 3f), Vector2.down, 12f);
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;
                if (h.collider.GetComponentInParent<BossController>() != null) continue;
                return h.point.y;
            }
            return fallback;
        }
    }
}
