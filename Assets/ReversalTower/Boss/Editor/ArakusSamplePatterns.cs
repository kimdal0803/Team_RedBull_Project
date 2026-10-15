using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReversalTower.Boss.EditorTools
{
    /// <summary>
    /// 아라쿠스 기획서 타임라인으로 샘플 패턴 에셋을 만든다.
    ///   메뉴 ▸ 역전의 탑 ▸ 아라쿠스 샘플 패턴 만들기
    ///   메뉴 ▸ 역전의 탑 ▸ 선택한 보스에 아라쿠스 샘플 연결
    ///   메뉴 ▸ 역전의 탑 ▸ 돌진 기믹 기둥 만들기        (레이어 생성 + 돌진 패턴 연결까지 한 번에)
    ///
    /// 시간 값은 기획서 타임라인 그대로이고, 데미지 계수·판정 높이·쿨타임처럼 기획서에 없는 값은 임시값이다.
    /// 각 에셋의 designNote에 근거와 임시값을 적어 두었다. 이미 있는 에셋은 덮어쓰지 않는다.
    /// </summary>
    public static class ArakusSamplePatterns
    {
        const string Folder = "Assets/Data/Boss/Arakus";
        const string GimmickLayerName = "GimmickObject";

        /// <summary>무작위 선택 대상이 되는 일반 패턴 (강제 패턴인 Charge·Rage는 제외)</summary>
        static readonly string[] NormalPatternIds =
        {
            "Arakus_NormalAttack_R",
            "Arakus_NormalAttack_L",
            "Arakus_NormalAttack_F",
            "Arakus_SwordSwinging",
            "Arakus_ShieldDash",
            "Arakus_QuickStep",
            "Arakus_ShieldThrowDash",
            "Arakus_ShieldThrowSlam",
            "Arakus_SweepingStrike",
        };

        [MenuItem("역전의 탑/아라쿠스 샘플 패턴 만들기")]
        public static void CreateAll()
        {
            EnsureFolder(Folder);

            Make("Arakus_NormalAttack_R", "일반 공격 1 (거검 횡베기)", p =>
            {
                p.telegraphTime = 1.05f; p.recoveryTime = 1.05f;
                p.maxDistance = 1.5f; p.weight = 3f;
                p.hits = new[] { Hit(width: 1.5f, height: 2f, coef: 1.0f, kb: KnockbackLevel.Light) };
                p.fxKey = "Arakus_SwordAttack_FX"; p.sfxKey = "SFX_Arakus_Sword_Swing";
                p.designNote = "근거: 아라쿠스 기획서 4.1 — Hit 1.05s / 총 2.1s, 범위 1.5m.\n임시: 계수 100%, 판정 높이 2m, 패링 불가.";
            });

            Make("Arakus_NormalAttack_L", "일반 공격 2 (방패 내려찍기)", p =>
            {
                p.telegraphTime = 0.85f; p.recoveryTime = 1.05f;
                p.maxDistance = 1f; p.weight = 2f;
                p.hits = new[] { Hit(width: 1f, height: 2f, coef: 0.8f, kb: KnockbackLevel.Light) };
                p.fxKey = "Arakus_ShieldAttack_FX"; p.sfxKey = "SFX_Arakus_Shield_Slam";
                p.designNote = "근거: 아라쿠스 기획서 4.1 — Hit 0.85s / 총 1.9s, 범위 1m.\n임시: 계수 80% (일반 공격 1 > 2 ≥ 3), 판정 높이 2m.";
            });

            Make("Arakus_NormalAttack_F", "일반 공격 3 (발차기)", p =>
            {
                p.telegraphTime = 0.8f; p.recoveryTime = 0.4f;
                p.maxDistance = 0.5f; p.weight = 2f;
                p.hits = new[] { Hit(width: 0.5f, height: 1.2f, coef: 0.7f, kb: KnockbackLevel.Light) };
                p.sfxKey = "SFX_Arakus_Kick";
                p.designNote = "근거: 아라쿠스 기획서 4.1 — Hit 0.8s / 총 1.2s, 범위 0.5m.\n임시: 계수 70%, 판정 높이 1.2m. 적중 시 추가 연계 공격은 기획서에 내용이 없어 넣지 않음.";
            });

            Make("Arakus_SwordSwinging", "거검 휘두르기", p =>
            {
                p.telegraphTime = 0.85f; p.recoveryTime = 0.75f;
                p.maxDistance = 2f; p.cooldown = 6f; p.weight = 1.5f;
                var h = Hit(width: 2f, height: 2f, coef: 1.5f, kb: KnockbackLevel.Medium);
                h.parryable = true; h.parryStaggerTime = 1f;
                p.hits = new[] { h };
                p.fxKey = "Arakus_SwordSwinging_FX"; p.sfxKey = "SFX_Arakus_GreatSword_Swing";
                p.designNote = "근거: 아라쿠스 기획서 4.2 — Hit 0.85s / 총 1.6s, 보스 전방 2m, 피격 시 1m 날아감(넉백 중), 패링으로 상쇄 가능.\n임시: 계수 150%, 판정 높이 2m, 패링 경직 1초, 쿨타임 6초.";
            });

            Make("Arakus_ShieldDash", "방패 돌진", p =>
            {
                p.kind = PatternKind.Dash;
                p.telegraphTime = 0.5f; p.recoveryTime = 0.35f;
                p.minDistance = 1f; p.maxDistance = 5f; p.cooldown = 8f; p.weight = 1f;
                var h = Hit(width: 1f, height: 2f, coef: 0.3f, kb: KnockbackLevel.None);
                h.repeatCount = 8; h.repeatInterval = 0.25f;
                p.hits = new[] { h };
                p.dash.distance = 5f; p.dash.duration = 1.75f; p.dash.stopAtWall = true;
                p.fxKey = "Arakus_ShieldDash_FX"; p.sfxKey = "SFX_Arakus_Shield_Dash";
                p.designNote = "근거: 아라쿠스 기획서 4.2 — 0.5s부터 0.25s 간격 8타, 총 2.6s, 직선 5m.\n주의: 간격 0.25s < 피격 무적 0.35s라 실제로는 4타만 들어감.\n임시: 타당 계수 30%, 넉백 없음(다단히트 중간 타).";
            });

            Make("Arakus_QuickStep", "퀵스텝", p =>
            {
                p.kind = PatternKind.Dash;
                p.conditionKey = "TargetBehind"; p.priority = 10;
                p.telegraphTime = 0.1f; p.recoveryTime = 0.35f;
                p.minDistance = 0f; p.maxDistance = 99f; p.cooldown = 3f; p.weight = 1f;
                p.hits = new HitData[0];
                p.dash.distance = 0.75f; p.dash.duration = 0.3f; p.dash.backward = true;
                p.fxKey = "Arakus_Dirt_FX"; p.sfxKey = "SFX_Arakus_Quickstep";
                p.designNote = "근거: 아라쿠스 기획서 4.2 — 총 0.75s, 플레이어가 안 보이면 돌아서며 0.75m 물러남.\n임시: '안 보임' = 등 뒤 1.5초(BossController.behindConditionTime), 선딜 0.1s, 쿨타임 3초.";
            });

            // ── 여기부터 새로 추가한 3개 ────────────────────────────────────────

            Make("Arakus_ShieldThrowDash", "방패 투척 후 대쉬", p =>
            {
                p.kind = PatternKind.Projectile;
                p.telegraphTime = 0.6f; p.recoveryTime = 0.5f;
                p.minDistance = 3f; p.maxDistance = 7f; p.cooldown = 9f; p.weight = 1f;

                // 0.6s에 방패를 던지고(투사체), 0.8s에 대쉬를 시작해 끝에서 베기
                p.projectile.spawnTime = 0f;      // 공격 시작 = 기획서 0.6s 시점
                p.projectile.speed = 12f;
                p.projectile.maxDistance = 7f;
                p.projectile.returns = true;

                var thrown = Hit(width: 0.8f, height: 0.8f, coef: 0.6f, kb: KnockbackLevel.Medium);
                thrown.carrier = HitCarrier.Projectile;

                var slash = Hit(width: 1f, height: 2f, coef: 1.2f, kb: KnockbackLevel.Medium);
                slash.time = 0.8f;               // 대쉬(0.2s 시작 + 0.6s 이동)가 끝나는 순간
                slash.parryable = true; slash.parryStaggerTime = 0.8f;

                p.hits = new[] { thrown, slash };
                // 대쉬(0.2 + 0.6)가 끝날 때까지 공격 단계를 유지한다.
                // PatternRun은 kind가 Dash일 때만 대쉬 길이를 자동 반영하므로 여기서 직접 적어 둔다.
                p.attackDuration = 0.8f;
                p.dash.startTime = 0.2f; p.dash.distance = 6f; p.dash.duration = 0.6f; p.dash.stopAtWall = true;
                p.fxKey = "Arakus_ShieldThrow_FX"; p.sfxKey = "SFX_Arakus_Shield_Throw";
                p.designNote = "근거: 아라쿠스 기획서 4.2 — 투척 0.6s / 대쉬 0.8s, 사거리 3~7m, 대쉬 6m를 0.6초(10m/s), 끝에 베기 1m.\n" +
                               "임시: 방패 계수 60%, 베기 계수 120%, 쿨타임 9초.\n" +
                               "임시(기획 확인 필요): 베기를 '패링 가능'으로 둠. 내려찍기(패링 불가)와의 이지선다가 성립하려면 한쪽은 패링, 한쪽은 회피여야 해서.";
            });

            Make("Arakus_ShieldThrowSlam", "방패 던지고 내려찍기", p =>
            {
                p.kind = PatternKind.Projectile;
                p.telegraphTime = 0.6f; p.recoveryTime = 0.45f;
                p.minDistance = 3f; p.maxDistance = 7f; p.cooldown = 9f; p.weight = 1f;

                p.projectile.spawnTime = 0f;
                p.projectile.speed = 12f;
                p.projectile.maxDistance = 7f;
                p.projectile.returns = true;

                var thrown = Hit(width: 0.8f, height: 0.8f, coef: 0.6f, kb: KnockbackLevel.Medium);
                thrown.carrier = HitCarrier.Projectile;

                // 착지 판정 반경 1.5m → 보스 몸 중심 기준 지름 3m
                var slam = Hit(width: 3f, height: 2.2f, coef: 1.6f, kb: KnockbackLevel.Heavy);
                slam.time = 0.75f;               // 기획서 내려찍기 1.35s − 투척 0.6s
                slam.box.anchor = HitboxAnchor.BossCenter;
                slam.parryable = false;

                p.hits = new[] { thrown, slam };
                p.attackDuration = 0.75f; // 내려찍기 판정까지 공격 단계 유지 (위와 같은 이유)
                p.dash.startTime = 0.15f; p.dash.distance = 5f; p.dash.duration = 0.5f; p.dash.stopAtWall = true;
                p.fxKey = "Arakus_ShieldSlam_FX"; p.sfxKey = "SFX_Arakus_Shield_Slam_Heavy";
                p.designNote = "근거: 아라쿠스 기획서 4.2 — 투척 0.6s / 내려찍기 1.35s / 총 1.8s, 7m, 착지 판정 반경 1.5m, 패링 불가.\n" +
                               "투척 후 대쉬와 시작 모션이 같아 이지선다를 이룬다 (기획서).\n" +
                               "임시: 방패 계수 60%, 내려찍기 계수 160%, 쿨타임 9초.\n" +
                               "임시: '7m까지 뛰어서 내려찍는' 이동을 대쉬 5m / 0.5초로 흉내 냈음. 점프 연출이 필요하면 전용 실행기를 따로 만들어야 함.";
            });

            Make("Arakus_SweepingStrike", "일대 휩쓸기", p =>
            {
                p.telegraphTime = 1f; p.recoveryTime = 0.8f;
                p.minDistance = 0f; p.maxDistance = 6f; p.cooldown = 12f; p.weight = 0.8f;

                // 횡베기는 낮게, 종베기 2타는 높게 (기획서)
                var sweep = Hit(width: 6f, height: 1f, coef: 1.5f, kb: KnockbackLevel.Medium);
                sweep.time = 0f; sweep.ignoresDodge = true;

                var chop1 = Hit(width: 6f, height: 2.5f, coef: 1.5f, kb: KnockbackLevel.Medium);
                chop1.time = 1f; chop1.ignoresDodge = true;

                var chop2 = Hit(width: 6f, height: 2.5f, coef: 1.5f, kb: KnockbackLevel.Heavy);
                chop2.time = 2f; chop2.ignoresDodge = true;

                p.hits = new[] { sweep, chop1, chop2 };
                p.fxKey = "Arakus_Aggressive_Attack_FX"; p.sfxKey = "SFX_Arakus_Sweep";
                p.designNote = "근거: 아라쿠스 기획서 4.2 — 횡베기 1s / 종베기 2s / 3s, 6m, 회피불가(구르기 무적 무시), 횡베기는 낮게·종베기는 높게.\n" +
                               "임시: 타당 계수 150%, 횡베기 판정 높이 1m(점프로 넘김) / 종베기 2.5m(점프로 못 넘김), 쿨타임 12초.\n" +
                               "확인 필요: '회피불가'를 전 타에 걸었음. 3타 모두 회피불가면 매우 아플 수 있으니 플레이테스트로 조정할 것.";
            });

            // ── 강제 패턴 (PhaseTrigger 전용) ──────────────────────────────────

            Make("Arakus_Charge", "돌진 (2페이즈 기믹)", p =>
            {
                p.kind = PatternKind.Dash;
                p.minPhase = 2; p.weight = 0f; // 강제 패턴 전용 (PhaseTrigger)
                p.telegraphTime = 1.1f; p.recoveryTime = 0f;
                p.maxDistance = 99f;
                var h = Hit(width: 1.2f, height: 2.2f, coef: 1.5f, kb: KnockbackLevel.Heavy);
                h.activeTime = 0.4f;
                p.hits = new[] { h };
                p.dash.distance = 20f; p.dash.duration = 0.4f; p.dash.stopAtWall = true; p.dash.groggyTime = 3f;
                p.fxKey = "Arakus_Charge_FX"; p.sfxKey = "SFX_Arakus_Charge_Dash";
                p.designNote = "근거: 아라쿠스 기획서 4.3 — Hit 1.1s / 총 1.5s, 맵 끝까지, 오브젝트에 막히면 그로기.\n" +
                               "확인 필요: 타임라인대로면 0.4초에 20m를 달리는 매우 빠른 돌진이 됨.\n" +
                               "임시: 계수 150%, 그로기 3초. dash.groggyObjectMask에 저지 오브젝트 레이어를 지정해야 그로기가 걸린다.\n" +
                               "메뉴 '돌진 기믹 기둥 만들기'를 쓰면 레이어 지정까지 자동으로 된다.";
            });

            Make("Arakus_Rage", "아라쿠스의 분노 (30% 버프)", p =>
            {
                p.weight = 0f; // 강제 패턴 전용 (PhaseTrigger)
                p.telegraphTime = 2.5f; p.recoveryTime = 2.5f;
                p.maxDistance = 99f;
                p.hits = new HitData[0];
                p.damageTakenMultiplier = 0.5f;
                p.fxKey = "Arakus_Of_Wrath_FX"; p.sfxKey = "SFX_Arakus_Rage_Charge";
                p.designNote = "근거: 아라쿠스 기획서 4.3 — Buff 2.5s / 총 5s, 시전 중 받는 피해 감소, 이후 모든 속도 1.5배.\n" +
                               "임시: 받는 피해 50%. 속도 1.5배는 PhaseTrigger에서 적용되므로 이 연출도 1.5배 빠르게 재생됨.";
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[역전의 탑] 아라쿠스 샘플 패턴을 {Folder}에 만들었습니다. (일반 {NormalPatternIds.Length}개 + 강제 2개)");
        }

        [MenuItem("역전의 탑/선택한 보스에 아라쿠스 샘플 연결")]
        public static void AssignToSelectedBoss()
        {
            var go = Selection.activeGameObject;
            var boss = go != null ? go.GetComponent<BossController>() : null;
            if (boss == null)
            {
                EditorUtility.DisplayDialog("역전의 탑", "BossController가 붙은 오브젝트를 먼저 선택하세요.", "확인");
                return;
            }
            Assign(boss);
        }

        /// <summary>패턴 9개와 페이즈 전환 3개를 이 보스에 연결한다. 씬 세팅 스크립트도 이걸 쓴다.</summary>
        public static void Assign(BossController boss)
        {
            if (boss == null) return;
            if (Load("Arakus_NormalAttack_R") == null || Load("Arakus_SweepingStrike") == null) CreateAll();

            Undo.RecordObject(boss, "아라쿠스 샘플 연결");
            boss.bossId = "Arakus";
            boss.patterns.Clear();
            foreach (var id in NormalPatternIds)
            {
                var p = Load(id);
                if (p != null) boss.patterns.Add(p);
            }
            boss.phaseTriggers = new[]
            {
                // 60%: 2페이즈 진입 + 돌진 기믹 (기획서: 2페이즈 동안 총 2번 — 두 번째 시점은 미정)
                new PhaseTrigger { hpRatio = 0.6f, setPhase = 2, setSpeedMultiplier = 0f, forcedPatterns = new[] { Load("Arakus_Charge") } },
                // 40%: 돌진 기믹 두 번째 (기획서 '2페이즈 동안 총 2번' 해석 — 임시)
                new PhaseTrigger { hpRatio = 0.4f, setPhase = 0, setSpeedMultiplier = 0f, forcedPatterns = new[] { Load("Arakus_Charge") } },
                // 30%: 분노 (모든 속도 1.5배)
                new PhaseTrigger { hpRatio = 0.3f, setPhase = 0, setSpeedMultiplier = 1.5f, forcedPatterns = new[] { Load("Arakus_Rage") } },
            };
            boss.previewPattern = Load("Arakus_SwordSwinging");
            EditorUtility.SetDirty(boss);
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
            Debug.Log($"[역전의 탑] 아라쿠스 샘플 패턴 {boss.patterns.Count}개와 페이즈 전환 3개를 연결했습니다.", boss);
        }

        /// <summary>
        /// 돌진 기믹에 필요한 것을 한 번에 만든다.
        ///   1) GimmickObject 레이어 (없으면 빈 칸에 추가)
        ///   2) 씬에 ChargeStopper 기둥
        ///   3) Arakus_Charge 에셋의 groggyObjectMask를 그 레이어로 지정
        /// </summary>
        [MenuItem("역전의 탑/돌진 기믹 기둥 만들기")]
        public static void CreateChargeGimmick() { CreateChargeGimmickObject(null); }

        /// <summary>기둥을 만들고 돌진 패턴에 레이어를 연결한다. 만든 기둥을 돌려준다 (실패하면 null).</summary>
        public static GameObject CreateChargeGimmickObject(BossController bossHint)
        {
            int layer = EnsureLayer(GimmickLayerName);
            if (layer < 0)
            {
                EditorUtility.DisplayDialog("역전의 탑",
                    $"'{GimmickLayerName}' 레이어를 만들지 못했습니다.\n" +
                    "Project Settings ▸ Tags and Layers에서 빈 칸에 직접 추가한 뒤 다시 실행하세요.", "확인");
                return null;
            }

            var go = new GameObject("ChargeStopper_Pillar");
            var stopper = go.AddComponent<ChargeStopper>();
            go.layer = layer;

            // 선택된 보스가 있으면 그 오른쪽 3m에 놓는다
            var selected = Selection.activeGameObject;
            var boss = bossHint != null
                ? bossHint
                : (selected != null ? selected.GetComponent<BossController>() : UnityEngine.Object.FindAnyObjectByType<BossController>());
            go.transform.position = boss != null
                ? boss.transform.position + new Vector3(3f, 0f, 0f)
                : Vector3.zero;

            Undo.RegisterCreatedObjectUndo(go, "돌진 기믹 기둥 만들기");

            // 돌진 패턴이 이 레이어를 보도록 연결
            var charge = Load("Arakus_Charge");
            if (charge != null)
            {
                Undo.RecordObject(charge, "돌진 기믹 레이어 연결");
                charge.dash.groggyObjectMask = 1 << layer;
                if (charge.dash.groggyTime <= 0f) charge.dash.groggyTime = 3f;
                EditorUtility.SetDirty(charge);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning("[역전의 탑] Arakus_Charge 에셋이 없어 레이어 연결을 건너뛰었습니다. 먼저 '아라쿠스 샘플 패턴 만들기'를 실행하세요.");
            }

            Debug.Log($"[역전의 탑] 돌진 기믹 기둥을 만들었습니다. 레이어 '{GimmickLayerName}'({layer}), 크기 {stopper.width}×{stopper.height}m.\n" +
                      "보스와 플레이어 사이에 오도록 위치를 옮긴 뒤, 2페이즈 돌진이 기둥에 부딪히는지 보세요.", go);
            return go;
        }

        // ── 도우미 ──

        static HitData Hit(float width, float height, float coef, KnockbackLevel kb)
        {
            return new HitData
            {
                box = new HitboxShape { anchor = HitboxAnchor.FrontEdge, forwardStart = 0f, width = width, bottom = 0f, height = height },
                damageCoefficient = coef,
                knockback = kb,
            };
        }

        static BossPatternData Load(string id) => AssetDatabase.LoadAssetAtPath<BossPatternData>($"{Folder}/{id}.asset");

        static void Make(string id, string displayName, Action<BossPatternData> setup)
        {
            string path = $"{Folder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<BossPatternData>(path) != null)
            {
                Debug.Log($"이미 있어서 건너뜀: {path}");
                return;
            }
            var p = ScriptableObject.CreateInstance<BossPatternData>();
            p.patternId = id;
            p.displayName = displayName;
            setup(p);
            AssetDatabase.CreateAsset(p, path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>레이어가 없으면 8번 이후 빈 칸에 만든다. 실패하면 -1.</summary>
        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return -1;

            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null) return -1;

            for (int i = 8; i < layers.arraySize; i++) // 0~7은 유니티 예약
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;
                element.stringValue = layerName;
                so.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log($"[역전의 탑] 레이어 '{layerName}'을(를) {i}번에 만들었습니다.");
                return i;
            }
            return -1;
        }
    }
}
