using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReversalTower.Boss.Phanes.EditorTools
{
    /// <summary>
    /// 파네스 기획서 수치로 패턴 에셋을 만든다.
    ///   메뉴 ▸ 역전의 탑 ▸ 파네스 ▸ 샘플 패턴 만들기
    ///   메뉴 ▸ 역전의 탑 ▸ 파네스 ▸ 선택한 보스에 파네스 연결 (전체 11종)
    ///   메뉴 ▸ 역전의 탑 ▸ 파네스 ▸ 선택한 보스에 파네스 연결 (핵심 6종만)
    ///
    /// 기획서에 적힌 숫자는 그대로, 없는 숫자(데미지 계수, 속도, 쿨타임 등)는 임시값이다.
    /// 각 에셋의 designNote에 근거(기획서 쪽 번호)와 임시값을 적어 두었다. 이미 있는 에셋은 덮어쓰지 않는다.
    /// 수치를 다시 받고 싶으면 Assets/Data/Boss/Phanes 폴더의 에셋을 지우고 메뉴를 다시 누른다.
    /// </summary>
    public static class PhanesSamplePatterns
    {
        public const string Folder = "Assets/Data/Boss/Phanes";
        public const string BarrierId = "Phanes_SeveranceBarrier";

        /// <summary>기획서 2.2 스킬 리스트·3.2 행동 트리에 있는 패턴 (핵심)</summary>
        static readonly string[] CoreIds =
        {
            "Phanes_Teleport",
            "Phanes_HomingOrb_P1", "Phanes_HomingOrb_P2",
            "Phanes_FanVolley_P1", "Phanes_FanVolley_P2",
            "Phanes_RefractionOrb",
            "Phanes_JudgmentPillar",
            "Phanes_NebulaShards",
        };

        /// <summary>기획서 4.2·4.3 상세 페이지에만 있는 패턴 (추가)</summary>
        static readonly string[] ExtendedIds =
        {
            "Phanes_PunishmentLine_P1", "Phanes_PunishmentLine_P2",
            "Phanes_HeavenlyEcho_P1", "Phanes_HeavenlyEcho_P2",
            "Phanes_Hellfire",
            "Phanes_GravityDistortion",
            "Phanes_PhantomClones",
        };

        [MenuItem("역전의 탑/파네스/샘플 패턴 만들기", false, 20)]
        public static void CreateAll()
        {
            EnsureFolder(Folder);

            // ── 3.1 이동 ──────────────────────────────────────────────

            Make("Phanes_Teleport", "순간이동", PhanesSkill.Teleport, p =>
            {
                p.conditionKey = PhanesBrain.TeleportCondition; p.priority = 10; p.weight = 1f;
                p.telegraphTime = 0.17f; p.attackDuration = 0.17f; p.recoveryTime = 0.05f;
                p.hits = new HitData[0];
                p.sfxKey = "SFX_Phanes_Teleport";
                p.designNote = "근거: 기획서 3.1 이동(Teleport) '패턴 시전 전 잦은 빈도', 3.2 행동 트리 '랜덤 순간이동', 4.3 플로우 '플레이어 근접 시 순간이동'.\n" +
                               "언제: 스킬이 끝날 때 PhanesBrain.randomTeleportChance(40%) 확률, 또는 플레이어가 2.2m 안에 0.5초 붙어 있을 때.\n" +
                               "선딜 = 흐려짐, 공격 = 나타남. 흐려진 동안은 맞지 않는다. 판정 없음(DDA 대상 아님).";
            });

            // ── 4.1 일반 공격 ─────────────────────────────────────────

            Make("Phanes_HomingOrb_P1", "일반 공격 1 · 유도 광탄", PhanesSkill.HomingOrb, p =>
            {
                p.isBasicAttack = true; p.maxPhase = 1; p.weight = 3f;
                p.telegraphTime = 0.55f; p.recoveryTime = 0.65f;
                p.hits = new[] { Orb(0.5f, 0.8f, KnockbackLevel.Light) };
                p.fxKey = "Phanes_HomingOrb_FX"; p.sfxKey = "SFX_Phanes_Orb";
                p.designNote = "근거: 기획서 4.1 — 1페이즈 1발, 직경 0.5m 원형, 1.5초 뒤 유도 상실 후 직선.\n" +
                               "임시: 속도 6.5m/s, 꺾임 200°/s, 계수 80%, 선딜 0.55s.\n" +
                               "임시: 패링하면 광탄이 사라진다 (되돌아가는 건 굴절의 광탄만).";
            });

            Make("Phanes_HomingOrb_P2", "일반 공격 1 · 유도 광탄 (2페이즈)", PhanesSkill.HomingOrb, p =>
            {
                p.isBasicAttack = true; p.minPhase = 2; p.weight = 3f;
                p.telegraphTime = 0.5f; p.recoveryTime = 0.6f;
                var h = Orb(0.5f, 0.8f, KnockbackLevel.Light);
                h.repeatCount = 3; h.repeatInterval = 0.2f;
                p.hits = new[] { h };
                p.fxKey = "Phanes_HomingOrb_FX"; p.sfxKey = "SFX_Phanes_Orb";
                p.designNote = "근거: 기획서 4.1 — 2페이즈 0.2초 간격 3발 연속.\n" +
                               "확인 필요: 2.2 스킬 리스트에는 '2번 연속 발사'로 적혀 있음. 4.1(상세)을 따라 3발로 둠 — repeatCount로 바꾼다.";
            });

            Make("Phanes_FanVolley_P1", "일반 공격 2 · 부채꼴 산탄", PhanesSkill.FanVolley, p =>
            {
                p.isBasicAttack = true; p.maxPhase = 1; p.weight = 2.5f;
                p.telegraphTime = 0.6f; p.recoveryTime = 0.7f;
                p.hits = new[] { Needle(0.8f) };
                p.fan.count = 3;
                p.fxKey = "Phanes_FanVolley_FX"; p.sfxKey = "SFX_Phanes_Needle";
                p.designNote = "근거: 기획서 4.1 — 시전 시점 플레이어 위치로 고정, 보스 전방 60° 부채꼴, 1페이즈 3갈래(간격 넓음), 투사체 폭 0.4m.\n" +
                               "임시: 바늘 길이 0.9m, 속도 9m/s, 계수 80%. 같은 방출의 바늘에는 한 번만 맞는다.";
            });

            Make("Phanes_FanVolley_P2", "일반 공격 2 · 부채꼴 산탄 (2페이즈)", PhanesSkill.FanVolley, p =>
            {
                p.isBasicAttack = true; p.minPhase = 2; p.weight = 2.5f;
                p.telegraphTime = 0.55f; p.recoveryTime = 0.65f;
                p.hits = new[] { Needle(0.8f) };
                p.fan.count = 5;
                p.fxKey = "Phanes_FanVolley_FX"; p.sfxKey = "SFX_Phanes_Needle";
                p.designNote = "근거: 기획서 4.1 — 2페이즈 5갈래(같은 60°라 간격 좁음).";
            });

            // ── 4.2 1페이즈 스킬 (2페이즈에서도 사용) ─────────────────

            Make("Phanes_RefractionOrb", "굴절의 광탄", PhanesSkill.RefractionOrb, p =>
            {
                p.weight = 1.5f; p.cooldown = 8f;
                p.teleportBeforeChance = 1f;
                p.telegraphTime = 1.0f; p.recoveryTime = 0.8f;
                var h = Orb(1.2f, 1.2f, KnockbackLevel.Medium);
                p.hits = new[] { h };
                p.fxKey = "Phanes_RefractionOrb_FX"; p.sfxKey = "SFX_Phanes_RefractionOrb";
                p.designNote = "근거: 기획서 4.2 — 순간이동 후 저속 대형 유도 구체(직경 1.2m). 대시 회피 가능, 저스트 패링 시 보스에게 역반사(피해 + 높은 그로기). " +
                               "피격 시 넉백 + 체력 감소, 반사 시 푸른빛/백금빛.\n" +
                               "임시: 속도 3.2m/s, 꺾임 70°/s, 유도 4초, 반사 피해 = 보스 최대 체력 8%, 그로기 +50 (게이지 100 → 두 번 반사하면 그로기 2.5초), 계수 120%, 쿨타임 8초.";
            });

            Make("Phanes_JudgmentPillar", "단죄의 빛 기둥", PhanesSkill.JudgmentPillar, p =>
            {
                p.weight = 1.5f; p.cooldown = 7f;
                p.telegraphTime = 1.0f; p.recoveryTime = 0.8f;
                var h = World(0f, 0.6f, KnockbackLevel.Light);
                h.repeatCount = 3; h.repeatInterval = 0.4f; h.activeTime = 0.4f;
                p.hits = new[] { h };
                p.fxKey = "Phanes_JudgmentPillar_FX"; p.sfxKey = "SFX_Phanes_Pillar";
                p.designNote = "근거: 기획서 4.2 — 지팡이 내리찍기 → 플레이어 발밑 경고 마법진 → 바닥~천장 빛 기둥(폭 1.6m, Y축 무한) 다단 히트. " +
                               "점프로 회피 불가, X축 이탈 또는 무적 대시로만 회피.\n" +
                               "임시: 경고(선딜) 1.0초, 0.4초 간격 3타(피격 무적 0.35초보다 길게), 타당 계수 60%, 쿨타임 7초.";
            });

            Make("Phanes_PunishmentLine_P1", "신벌의 징벌선", PhanesSkill.PunishmentLine, p =>
            {
                p.maxPhase = 1; p.weight = 1.2f; p.cooldown = 9f;
                p.telegraphTime = 1.0f; p.recoveryTime = 0.6f;
                p.returnHomeAfter = true;
                p.hits = new[] { World(0f, 1.0f, KnockbackLevel.Medium) };
                p.fxKey = "Phanes_PunishmentLine_FX"; p.sfxKey = "SFX_Phanes_GroundLaser";
                p.designNote = "근거: 기획서 4.2 — 지팡이를 지면에 꽂고 바닥을 따라 고속 수평 레이저. 바닥 기준 높이 0.8m, 전방 맵 끝까지. 1페이즈 단일 방향(보스 전방).\n" +
                               "임시: 레이저 속도 22m/s, 끝까지 뻗은 뒤 0.3초 유지, 계수 100%, 쿨타임 9초.\n" +
                               "구현: 발판 위에 있으면 가까운 바닥으로 순간이동한 뒤 쏜다. 끝나면 원래 체공 위치로 돌아간다.";
            });

            Make("Phanes_PunishmentLine_P2", "신벌의 징벌선 (2페이즈)", PhanesSkill.PunishmentLine, p =>
            {
                p.minPhase = 2; p.weight = 1.2f; p.cooldown = 9f;
                p.telegraphTime = 0.9f; p.recoveryTime = 0.6f;
                p.returnHomeAfter = true;
                var fire = World(0f, 0.3f, KnockbackLevel.None);
                fire.repeatCount = 3; fire.repeatInterval = 0.6f; fire.activeTime = 0.6f;
                p.hits = new[] { World(0f, 1.0f, KnockbackLevel.Medium), fire };
                p.line.bothDirections = true;
                p.line.residualFire = false;
                p.fxKey = "Phanes_PunishmentLine_FX"; p.sfxKey = "SFX_Phanes_GroundLaser";
                p.designNote = "근거: 기획서 4.2 — 2페이즈 '양방향(좌·우) 동시 방사 또는 레이저 통과 후 지면 잔류 불꽃'.\n" +
                               "확인 필요: 기획서가 '또는'이라 둘 다 만들어 두었다. 지금은 양방향만 켬. 잔류 불꽃은 line.residualFire를 켜면 된다 (hits[1]이 불꽃 틱: 0.6초 간격 3틱, 계수 30%, 임시).";
            });

            Make("Phanes_HeavenlyEcho_P1", "천벌의 잔향", PhanesSkill.HeavenlyEcho, p =>
            {
                p.maxPhase = 1; p.weight = 1.2f; p.cooldown = 10f;
                p.telegraphTime = 0.6f; p.recoveryTime = 0.3f;
                var h = World(0.8f, 0.8f, KnockbackLevel.Light);
                h.repeatCount = 3; h.repeatInterval = 1.0f; h.activeTime = 0.25f;
                p.hits = new[] { h };
                p.fxKey = "Phanes_HeavenlyEcho_FX"; p.sfxKey = "SFX_Phanes_Spike";
                p.designNote = "근거: 기획서 4.2 — 1.0초 간격으로 플레이어 발밑에 붉은 표식 3~4회, 각 표식 0.8초 뒤 폭발(폭 1.0m × 높이 1.5m). 다른 공격과 동시 시전.\n" +
                               "해석: 1페이즈 3회 / 2페이즈 4회로 나눔 (기획서 3~4회). echo.runInBackground가 켜져 있어 표식이 이어지는 동안 보스는 다음 패턴을 쓴다.\n" +
                               "임시: 폭발 판정 유지 0.25초, 계수 80%, 쿨타임 10초.";
            });

            Make("Phanes_HeavenlyEcho_P2", "천벌의 잔향 (2페이즈)", PhanesSkill.HeavenlyEcho, p =>
            {
                p.minPhase = 2; p.weight = 1.2f; p.cooldown = 10f;
                p.telegraphTime = 0.5f; p.recoveryTime = 0.3f;
                var h = World(0.8f, 0.8f, KnockbackLevel.Light);
                h.repeatCount = 4; h.repeatInterval = 1.0f; h.activeTime = 0.25f;
                p.hits = new[] { h };
                p.fxKey = "Phanes_HeavenlyEcho_FX"; p.sfxKey = "SFX_Phanes_Spike";
                p.designNote = "근거: 기획서 4.2 — '총 3~4회' 중 4회를 2페이즈 강화판으로 씀 (기획서 '강화된 1페이즈 패턴').";
            });

            Make("Phanes_Hellfire", "영벌의 업화", PhanesSkill.Hellfire, p =>
            {
                p.weight = 1f; p.cooldown = 16f;
                p.telegraphTime = 1.2f; p.recoveryTime = 0.5f;
                p.returnHomeAfter = true;
                var h = World(0f, 0.4f, KnockbackLevel.None);
                h.repeatCount = 5; h.repeatInterval = 1.0f; h.activeTime = 1.0f;
                p.hits = new[] { h };
                p.fxKey = "Phanes_Hellfire_FX"; p.sfxKey = "SFX_Phanes_Hellfire";
                p.designNote = "근거: 기획서 4.2 — 공중 부유 후 1.2초 전조, 최하단 바닥 전체가 5초간 화염 장판(바닥 기준 높이 1.2m). 공중 발판은 안전지대.\n" +
                               "구현: 1초마다 화상 1틱 × 5 (피격 무적 0.35초보다 긴 간격). 장판이 타는 동안 보스는 다음 패턴을 쓴다 ('타 투사체 패턴과의 복합 회피 압박').\n" +
                               "임시: 틱 계수 40%, 떠오르는 높이 5.5m, 쿨타임 16초.\n" +
                               "주의: 맵에 발판이 없으면 피할 곳이 없다. BossTestScene에는 가운데 발판이 있다.";
            });

            // ── 4.3 2페이즈 스킬 ──────────────────────────────────────

            Make(BarrierId, "단절의 결계 (2페이즈 진입 기믹)", PhanesSkill.SeveranceBarrier, p =>
            {
                p.minPhase = 2; p.weight = 0f; // 강제 패턴 전용 (PhaseTrigger 50%)
                p.telegraphTime = 1.2f; p.recoveryTime = 0.5f;
                p.faceTargetOnStart = true;
                var blast = World(10f, 50f, KnockbackLevel.Heavy);
                blast.ignoresDodge = true;
                blast.repeatCount = 6; blast.repeatInterval = 0.1f; blast.activeTime = 0.1f;
                p.hits = new[] { blast };
                p.fxKey = "Phanes_Barrier_FX"; p.sfxKey = "SFX_Phanes_Barrier";
                p.designNote = "근거: 기획서 4.3 — 맵 중앙 결계벽(가로 2.0m, 세로 천장 관통, 통과 불가), 보스는 결계 너머로 순간이동. " +
                               "제한 시간 내 파괴 실패 시 맵 전체 즉사급 폭발. 2페이즈 진입 시 확정 1회. 성공 시 보스 행동 불능 3초, 맵 전체 공격 취소(상세 플로우 분기 A).\n" +
                               "임시(기획서에 없음): 제한 시간 10초(= hits[0].time), 결계 체력 12(기본 공격력 1 기준 12대), 폭발 계수 5000%(플레이어 체력 5 기준 즉사).\n" +
                               "구현: 폭발은 0.1초 간격 6번 판정 + 회피불가 — 대시 무적(0.18초)으로 빠져나가지 못하게.\n" +
                               "확인 필요: 2.2는 '파괴 후 보스에게 피해를 입히는 데 실패하면'이라 적혀 있음. barrier.requireBossHitAfterBreak를 켜면 그 규칙이 된다 (지금은 행동 트리·4.3 플로우대로 벽 파괴만으로 성공).";
            });

            Make("Phanes_NebulaShards", "성운의 파편", PhanesSkill.NebulaShards, p =>
            {
                p.minPhase = 2; p.weight = 1f; p.cooldown = 14f;
                p.telegraphTime = 1.2f; p.recoveryTime = 0.8f;
                p.returnHomeAfter = true;
                var sphere = World(0f, 1.0f, KnockbackLevel.Medium);
                var wave = Needle(0.8f);
                wave.time = 0.6f; wave.repeatCount = 3; wave.repeatInterval = 1.3f;
                p.hits = new[] { sphere, wave };
                p.fxKey = "Phanes_Nebula_FX"; p.sfxKey = "SFX_Phanes_Nebula";
                p.designNote = "근거: 기획서 4.3 — 중앙 상공으로 떠올라 거대 성운 구체(반경 1.5m 원형 판정) 소환, 사방(별 모양)으로 번개·레이저 줄기 방사. 상세 플로우 그림 12갈래 + 줄기마다 파편.\n" +
                               "구현: 경고선 0.6초 → 번개 줄기 번쩍 + 파편 탄막. 탄막 3번, 매번 반 칸씩 엇갈려 안전지대가 바뀐다.\n" +
                               "임시: 파편 속도 8.5m/s, 줄기당 2개, 탄막 간격 1.3초, 계수 80%, 쿨타임 14초.\n" +
                               "확인 필요: 3.2 행동 트리에는 1페이즈 루프에도 있음. 2.2·4.3을 따라 2페이즈 전용(minPhase 2)으로 둠.";
            });

            Make("Phanes_GravityDistortion", "인력의 왜곡", PhanesSkill.GravityDistortion, p =>
            {
                p.minPhase = 2; p.weight = 1f; p.cooldown = 14f;
                p.telegraphTime = 0.8f; p.recoveryTime = 0.7f;
                var blast = World(3f, 1.5f, KnockbackLevel.Heavy);
                blast.activeTime = 0.15f;
                p.hits = new[] { blast };
                p.fxKey = "Phanes_Gravity_FX"; p.sfxKey = "SFX_Phanes_Gravity";
                p.designNote = "근거: 기획서 4.3 — 맵 중앙 상공 특이점, 3.0초간 반경 4.5m 흡인(가까울수록 강함), 종료 시 반경 2.0m 척력 폭발.\n" +
                               "임시: 특이점 높이 바닥+1.8m(폭발이 바닥에 닿게), 흡인 속도 1.2~4.2m/s(플레이어 이동 5.5보다 느려서 반대로 달리면 버틸 수 있음), 폭발 계수 150%.";
            });

            Make("Phanes_PhantomClones", "환영 분신", PhanesSkill.PhantomClones, p =>
            {
                p.minPhase = 2; p.weight = 0.8f; p.cooldown = 25f;
                p.telegraphTime = 0.8f; p.recoveryTime = 0.3f;
                p.hits = new[] { World(0f, 1.0f, KnockbackLevel.Medium) };
                p.fxKey = "Phanes_Phantom_FX"; p.sfxKey = "SFX_Phanes_Phantom";
                p.designNote = "근거: 기획서 4.3 — 좌우 3.0m에 분신 2체(체력 1, 지팡이 보석 색·마법진 회전 방향이 다름). " +
                               "정답 타격: 분신 소멸 + 보스 경직 / 오답: 자폭(반경 2.0m) + 암흑·침묵 / 시간 초과: 분신이 남아 일반 공격 복제.\n" +
                               "임시: 고를 시간 5초, 경직 1.5초, 암흑·침묵 4초, 복제 8초, 자폭 계수 100%.\n" +
                               "구분 단서(임시): 진짜 = 보석 금색 + 마법진 시계 방향, 가짜 = 보석 하늘색 + 반시계. clones 칸에서 바꾼다.\n" +
                               "주의: 침묵은 PlayerMovement가 PhanesStatusEffects.IsSilenced를 확인해야 실제로 스킬이 막힌다.";
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[역전의 탑] 파네스 패턴을 {Folder}에 만들었습니다. (핵심 {CoreIds.Length}개 + 추가 {ExtendedIds.Length}개 + 결계 1개)");
        }

        [MenuItem("역전의 탑/파네스/선택한 보스에 파네스 연결 (전체 11종)", false, 21)]
        public static void AssignAllToSelected() => AssignToSelected(includeExtended: true);

        [MenuItem("역전의 탑/파네스/선택한 보스에 파네스 연결 (핵심 6종만)", false, 22)]
        public static void AssignCoreToSelected() => AssignToSelected(includeExtended: false);

        static void AssignToSelected(bool includeExtended)
        {
            var go = Selection.activeGameObject;
            var boss = go != null ? go.GetComponent<BossController>() : null;
            if (boss == null)
            {
                EditorUtility.DisplayDialog("역전의 탑", "BossController가 붙은 오브젝트를 먼저 선택하세요.", "확인");
                return;
            }
            Assign(boss, includeExtended);
        }

        /// <summary>
        /// 패턴과 페이즈 전환(50% → 2페이즈 + 단절의 결계)을 이 보스에 연결한다. 씬 세팅 스크립트도 이걸 쓴다.
        /// includeExtended가 false면 기획서 2.2·3.2에 있는 핵심 스킬만 (일반 공격 2종, 굴절의 광탄, 빛 기둥, 결계, 성운의 파편).
        /// </summary>
        public static void Assign(BossController boss, bool includeExtended = true)
        {
            if (boss == null) return;
            if (Load("Phanes_Teleport") == null || Load(BarrierId) == null || Load("Phanes_PhantomClones") == null) CreateAll();

            Undo.RecordObject(boss, "파네스 패턴 연결");
            boss.bossId = "Phanes";
            boss.moveSpeed = 0f; // 걷지 않는다 (상시 비행)
            boss.patterns.Clear();
            var ids = new List<string>(CoreIds);
            if (includeExtended) ids.AddRange(ExtendedIds);
            foreach (var id in ids)
            {
                var p = Load(id);
                if (p != null) boss.patterns.Add(p);
            }
            boss.phaseTriggers = new[]
            {
                // 기획서 2.2·3.2: HP 50% 미만 → 2페이즈 + 단절의 결계 확정 1회
                new PhaseTrigger { hpRatio = 0.5f, setPhase = 2, setSpeedMultiplier = 0f, forcedPatterns = new BossPatternData[] { Load(BarrierId) } },
            };
            boss.previewPattern = null;
            EditorUtility.SetDirty(boss);
            PrefabUtility.RecordPrefabInstancePropertyModifications(boss);
            Debug.Log($"[역전의 탑] 파네스 패턴 {boss.patterns.Count}개({(includeExtended ? "전체" : "핵심만")})와 페이즈 전환(50%: 2페이즈 + 단절의 결계)을 연결했습니다.", boss);
        }

        // ── 도우미 ──

        /// <summary>투사체가 드는 광탄 판정 (패링 신호를 받고, 패링하면 사라지거나 되돌아간다)</summary>
        static HitData Orb(float diameter, float coef, KnockbackLevel kb)
        {
            return new HitData
            {
                carrier = HitCarrier.Projectile,
                box = new HitboxShape { anchor = HitboxAnchor.BossCenter, width = diameter, height = diameter },
                damageCoefficient = coef,
                knockback = kb,
                parryable = true,
                parryStaggerTime = 0f,          // 파네스는 패링으로 바로 경직되지 않는다 (굴절의 광탄은 반사로 그로기)
                parryInterruptsPattern = false,
            };
        }

        static HitData Needle(float coef)
        {
            var h = Orb(0.4f, coef, KnockbackLevel.Light);
            h.box.width = 0.9f;
            return h;
        }

        /// <summary>월드 좌표 판정 (빛 기둥, 장판, 폭발). 위치·크기는 스킬 실행기가 정한다.</summary>
        static HitData World(float time, float coef, KnockbackLevel kb)
        {
            return new HitData
            {
                time = time,
                carrier = HitCarrier.World,
                box = new HitboxShape { anchor = HitboxAnchor.BossCenter, width = 1f, height = 1f },
                damageCoefficient = coef,
                knockback = kb,
                parryable = false,
            };
        }

        static PhanesPatternData Load(string id) => AssetDatabase.LoadAssetAtPath<PhanesPatternData>($"{Folder}/{id}.asset");

        static void Make(string id, string displayName, PhanesSkill skill, Action<PhanesPatternData> setup)
        {
            string path = $"{Folder}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<PhanesPatternData>(path) != null)
            {
                Debug.Log($"이미 있어서 건너뜀: {path}");
                return;
            }
            var p = ScriptableObject.CreateInstance<PhanesPatternData>();
            p.patternId = id;
            p.displayName = displayName;
            p.kind = PatternKind.Scripted;
            p.skill = skill;
            p.minDistance = 0f;
            p.maxDistance = 99f;
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
    }
}
