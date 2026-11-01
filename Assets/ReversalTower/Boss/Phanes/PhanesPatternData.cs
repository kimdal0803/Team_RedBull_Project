using System;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>파네스 기획서의 스킬 목록. 값 하나 = 실행 방식 하나 (PhanesCasts에 같은 이름의 클래스가 있다).</summary>
    public enum PhanesSkill
    {
        Teleport,           // 3.1 이동(Teleport) — 패턴 사이 순간이동
        HomingOrb,          // 4.1 일반 공격 1 · 유도 광탄
        FanVolley,          // 4.1 일반 공격 2 · 부채꼴 산탄
        RefractionOrb,      // 4.2 굴절의 광탄 (패링하면 보스에게 되돌아감)
        JudgmentPillar,     // 4.2 단죄의 빛 기둥
        PunishmentLine,     // 4.2 신벌의 징벌선 (바닥 수평 레이저)
        HeavenlyEcho,       // 4.2 천벌의 잔향 (발밑 연속 폭발)
        Hellfire,           // 4.2 영벌의 업화 (최하단 바닥 화염 장판)
        SeveranceBarrier,   // 4.3 단절의 결계 (2페이즈 진입 확정, 핵심 기믹)
        NebulaShards,       // 4.3 성운의 파편 (별 모양 탄막)
        GravityDistortion,  // 4.3 인력의 왜곡 (흡인 후 폭발)
        PhantomClones,      // 4.3 환영 분신
    }

    /// <summary>
    /// 파네스 패턴 하나. BossPatternData(타임라인·판정·사용 조건)에 파네스 스킬별 설정을 더한 것.
    /// BossController.patterns에 그대로 넣을 수 있고, 선택·DDA·전투 기록은 아라쿠스와 똑같이 동작한다.
    ///
    /// 판정(hits)을 읽는 법
    ///   파네스 판정은 대부분 보스 몸이 아니라 월드 좌표(빛 기둥, 바닥)나 투사체에 실린다.
    ///   그래서 carrier를 World 또는 Projectile로 두고, 스킬 실행기가 위치를 정한다.
    ///   hits[i].time / repeatCount / repeatInterval은 그대로 '언제, 몇 번'의 의미라서 DDA 보정도 그대로 먹는다.
    ///   (예: 유도 광탄 2페이즈 = repeatCount 3, repeatInterval 0.2 → 0.2초 간격 3발)
    ///
    /// 스킬별로 hits 몇 번째가 무엇인지는 각 설정 클래스 설명에 적어 두었다.
    /// </summary>
    [CreateAssetMenu(fileName = "Phanes_New", menuName = "역전의 탑/파네스 패턴 데이터", order = 1)]
    public class PhanesPatternData : BossPatternData
    {
        [Header("파네스 스킬")]
        public PhanesSkill skill = PhanesSkill.HomingOrb;
        [Tooltip("일반 공격인지. 환영 분신이 '시간 초과' 뒤 따라 쏘는 패턴이 이것이다 (기획서 4.3 환영 분신).")]
        public bool isBasicAttack = false;
        [Tooltip("시전 전에 순간이동할 확률(0~1). 굴절의 광탄은 기획서상 '순간이동 후'라서 1.\n" +
                 "켜면 선딜(telegraphTime)이 순간이동 시간(약 0.35초)보다 길어야 한다.")]
        [Range(0f, 1f)] public float teleportBeforeChance = 0f;
        [Tooltip("끝나면 원래 체공 위치로 돌아간다 (위로 떠오르거나 바닥으로 내려간 스킬)")]
        public bool returnHomeAfter = false;

        // 스킬별 설정 — 인스펙터에는 고른 스킬의 칸만 보인다 (PhanesPatternDataEditor)
        public HomingOrbSettings homing = new HomingOrbSettings();
        public FanVolleySettings fan = new FanVolleySettings();
        public RefractionOrbSettings refraction = new RefractionOrbSettings();
        public JudgmentPillarSettings pillar = new JudgmentPillarSettings();
        public PunishmentLineSettings line = new PunishmentLineSettings();
        public HeavenlyEchoSettings echo = new HeavenlyEchoSettings();
        public HellfireSettings hellfire = new HellfireSettings();
        public SeveranceBarrierSettings barrier = new SeveranceBarrierSettings();
        public NebulaShardsSettings nebula = new NebulaShardsSettings();
        public GravityDistortionSettings gravity = new GravityDistortionSettings();
        public PhantomClonesSettings clones = new PhantomClonesSettings();

        void Reset()
        {
            kind = PatternKind.Scripted;
            maxDistance = 99f;
        }

        /// <summary>이 판정이 hits 배열 몇 번째 HitData에서 나왔는지 (스킬이 판정을 구분할 때 쓴다). 없으면 -1.</summary>
        public int SourceIndexOf(in HitEvent e)
        {
            if (hits == null || e.Source == null) return -1;
            return Array.IndexOf(hits, e.Source);
        }
    }

    // ─────────────────────────────────── 스킬별 설정 ───────────────────────────────────
    // 기획서에 적힌 숫자는 그대로, 없는 숫자는 '임시'로 두었다. 임시값은 PhanesSamplePatterns의 designNote에도 적혀 있다.

    /// <summary>
    /// 일반 공격 1 · 유도 광탄. hits[0] (carrier = Projectile) 한 타 = 광탄 한 발.
    /// 1페이즈 1발, 2페이즈 repeatCount 3 / repeatInterval 0.2 (기획서 4.1).
    /// </summary>
    [Serializable]
    public class HomingOrbSettings
    {
        [Tooltip("판정 지름(m). 기획서 4.1: 0.5m 원형")]
        [Min(0.05f)] public float diameter = 0.5f;
        [Tooltip("비행 속도(m/s). 임시")]
        [Min(0.1f)] public float speed = 6.5f;
        [Tooltip("초당 꺾이는 각도(도). 임시. 클수록 잘 따라온다")]
        [Min(0f)] public float turnRate = 200f;
        [Tooltip("유도 시간(초). 기획서 4.1: 발사 후 1.5초가 지나면 유도를 잃고 직선 비행")]
        [Min(0f)] public float homingDuration = 1.5f;
        [Tooltip("최대 수명(초). 임시")]
        [Min(0.1f)] public float lifetime = 5f;
        [Tooltip("패링하면 광탄이 사라진다 (굴절의 광탄만 되돌아간다)")]
        public bool destroyOnParry = true;
        public Color color = new Color(1f, 0.86f, 0.35f, 1f);
    }

    /// <summary>
    /// 일반 공격 2 · 부채꼴 산탄. hits[0] (carrier = Projectile) 한 타 = 한 번의 부채꼴 방출 (바늘 여러 개가 판정 하나를 나눠 든다).
    /// 같은 방출의 바늘에 두 번 맞지는 않는다.
    /// </summary>
    [Serializable]
    public class FanVolleySettings
    {
        [Tooltip("갈래 수. 기획서 4.1: 1페이즈 3갈래 / 2페이즈 5갈래")]
        [Min(1)] public int count = 3;
        [Tooltip("부채꼴 전체 각도(도). 기획서 4.1: 보스 전방 60° — 같은 60°에 3갈래면 간격 넓음, 5갈래면 좁음")]
        [Range(0f, 180f)] public float totalAngle = 60f;
        [Tooltip("투사체 폭(m). 기획서 4.1: 0.4m")]
        [Min(0.05f)] public float width = 0.4f;
        [Tooltip("바늘 길이(m). 임시")]
        [Min(0.05f)] public float length = 0.9f;
        [Tooltip("속도(m/s). 임시")]
        [Min(0.1f)] public float speed = 9f;
        [Min(0.1f)] public float lifetime = 4f;
        [Tooltip("조준 시점. 켜면 시전(선딜 시작) 순간 플레이어 위치로 고정 — 기획서 4.1 '캐스팅 시점의 플레이어 위치'")]
        public bool lockAimAtCast = true;
        public Color color = new Color(1f, 0.8f, 0.3f, 1f);
    }

    /// <summary>
    /// 굴절의 광탄. hits[0] (carrier = Projectile, parryable) = 대형 유도 구체.
    /// 패링하면 푸른빛/백금빛으로 바뀌어 보스에게 되돌아가 피해 + 높은 그로기 수치 (기획서 4.2).
    /// </summary>
    [Serializable]
    public class RefractionOrbSettings
    {
        [Tooltip("판정 지름(m). 기획서 4.2: 1.2m 대형 구체")]
        [Min(0.05f)] public float diameter = 1.2f;
        [Tooltip("느린 전진 속도(m/s). 기획서 4.2 '저속 전진' — 숫자는 임시")]
        [Min(0.1f)] public float speed = 3.2f;
        [Min(0f)] public float turnRate = 70f;
        [Tooltip("유도 시간(초). 임시")]
        [Min(0f)] public float homingDuration = 4f;
        [Min(0.1f)] public float lifetime = 9f;
        [Header("반사")]
        [Tooltip("되돌아가는 속도(m/s). 임시")]
        [Min(0.1f)] public float reflectSpeed = 12f;
        [Tooltip("보스가 받는 피해 = 보스 최대 체력 × 이 값. 임시 (체력을 바꿔도 비율이 유지되도록)")]
        [Range(0f, 1f)] public float reflectDamageRatio = 0.08f;
        [Tooltip("쌓이는 그로기 수치 (PhanesBrain.groggyGaugeMax 기준). 기획서 '높은 그로기 수치' — 숫자는 임시")]
        [Min(0f)] public float reflectGroggy = 50f;
        public Color color = new Color(1f, 0.92f, 0.55f, 1f);
        [Tooltip("반사 후 색. 기획서 4.2: 푸른빛/백금빛")]
        public Color reflectedColor = new Color(0.62f, 0.85f, 1f, 1f);
    }

    /// <summary>
    /// 단죄의 빛 기둥. hits[0] (carrier = World) = 기둥 다단 히트 (repeatCount = 타수).
    /// 시전 순간 플레이어 발밑에 경고 마법진 → 선딜이 끝나면 바닥~천장 기둥 분출 (기획서 4.2).
    /// 점프로는 못 피하고, X축으로 벗어나거나 무적 대시로만 피한다.
    /// </summary>
    [Serializable]
    public class JudgmentPillarSettings
    {
        [Tooltip("기둥 폭(m). 기획서 4.2: 1.6m")]
        [Min(0.1f)] public float width = 1.6f;
        [Tooltip("기둥 개수. 1이면 플레이어 발밑 하나. 늘리면 양옆으로 spacing 간격")]
        [Min(1)] public int count = 1;
        [Min(0.1f)] public float spacing = 3.2f;
        public Color warningColor = new Color(1f, 0.25f, 0.2f, 1f);
        public Color pillarColor = new Color(1f, 0.93f, 0.62f, 1f);
    }

    /// <summary>
    /// 신벌의 징벌선. hits[0] (World) = 바닥 수평 레이저, hits[1] (World, 선택) = 잔류 불꽃 틱.
    /// 지팡이를 지면에 꽂고 바닥을 따라 고속으로 훑는다 (기획서 4.2).
    /// </summary>
    [Serializable]
    public class PunishmentLineSettings
    {
        [Tooltip("판정 높이(m, 바닥 기준). 기획서 4.2: 0.8m — 점프로 넘는다")]
        [Min(0.05f)] public float height = 0.8f;
        [Tooltip("레이저가 뻗어 나가는 속도(m/s). 기획서 '고속' — 숫자는 임시")]
        [Min(1f)] public float sweepSpeed = 22f;
        [Tooltip("끝까지 뻗은 뒤 남아 있는 시간(초). 임시. 점프 체공 시간보다 짧게")]
        [Min(0f)] public float lingerTime = 0.3f;
        [Tooltip("양방향 동시 방사. 기획서 4.2: 2페이즈 '양방향 동시 방사 또는 잔류 불꽃'")]
        public bool bothDirections = false;
        [Tooltip("레이저가 지나간 바닥에 잔류 불꽃 (hits[1]이 있어야 피해가 들어간다)")]
        public bool residualFire = false;
        [Min(0.05f)] public float residualFireHeight = 0.45f;
        public Color color = new Color(1f, 0.82f, 0.3f, 1f);
    }

    /// <summary>
    /// 천벌의 잔향. hits[0] (World) = 가시 폭발, repeatCount = 표식 수, repeatInterval = 표식 간격.
    /// hits[0].time = markLead이면 첫 표식이 공격 시작과 동시에 생긴다 (기획서 4.2: 1.0초 간격 3~4회, 표식 0.8초 뒤 폭발).
    /// </summary>
    [Serializable]
    public class HeavenlyEchoSettings
    {
        [Tooltip("표식이 생기고 폭발까지(초). 기획서 4.2: 0.8초")]
        [Min(0f)] public float markLead = 0.8f;
        [Tooltip("폭발 폭(m). 기획서 4.2: 1.0m")]
        [Min(0.1f)] public float width = 1.0f;
        [Tooltip("폭발 높이(m). 기획서 4.2: 1.5m")]
        [Min(0.1f)] public float height = 1.5f;
        [Tooltip("켜면 표식이 계속 생기는 동안 보스는 다음 패턴으로 넘어간다 (기획서 '다른 공격과 동시 시전')")]
        public bool runInBackground = true;
        public Color markColor = new Color(1f, 0.2f, 0.25f, 1f);
        public Color spikeColor = new Color(1f, 0.95f, 0.7f, 1f);
    }

    /// <summary>
    /// 영벌의 업화. hits[0] (World) = 화상 틱, repeatCount × repeatInterval = 지속 시간 (기획서 4.2: 5초).
    /// 선딜(1.2초) 동안 바닥이 붉게 달아오르고, 최하단 바닥 전체가 화염 장판이 된다. 공중 발판은 안전지대.
    /// </summary>
    [Serializable]
    public class HellfireSettings
    {
        [Tooltip("장판 높이(m, 최하단 바닥 기준). 기획서 4.2: 1.2m")]
        [Min(0.05f)] public float height = 1.2f;
        [Tooltip("보스가 떠오르는 높이(m, 최하단 바닥 기준). 기획서 '공중으로 부유' — 숫자는 임시")]
        [Min(0f)] public float riseHeight = 5.5f;
        [Tooltip("켜면 장판이 타는 동안 보스는 다음 패턴으로 넘어간다 (기획서 '타 투사체 패턴과의 복합 회피 압박')")]
        public bool runInBackground = true;
        public Color color = new Color(1f, 0.45f, 0.15f, 1f);
    }

    /// <summary>
    /// 단절의 결계 (2페이즈 핵심 기믹). hits[0] (World) = 파괴 실패 시 맵 전체 폭발.
    /// hits[0].time = 제한 시간. 폭발은 repeatCount를 여러 번 두어 대시 무적으로 빠져나가지 못하게 한다.
    /// 성공(결계 파괴) → 보스 추락 + 행동 불능 3초 + 맵 전체 공격 취소 (기획서 4.3 상세 플로우 분기 A).
    /// </summary>
    [Serializable]
    public class SeveranceBarrierSettings
    {
        [Tooltip("결계벽 폭(m). 기획서 4.3: 가로 2.0m, 세로 천장 관통, 통과 불가")]
        [Min(0.1f)] public float wallWidth = 2.0f;
        [Tooltip("결계벽 체력 (플레이어 공격력 단위). 기획서에 없음 — 임시. 기본 공격력 1이면 12대")]
        [Min(1)] public int wallHp = 12;
        [Tooltip("벽이 솟아오르는 시간(초). 임시")]
        [Min(0f)] public float riseTime = 0.6f;
        [Tooltip("벽 위치. 켜면 맵 중앙(기획서 4.3). 끄면 플레이어와 보스 사이")]
        public bool wallAtArenaCenter = true;
        [Tooltip("보스가 벽 너머 얼마나 떨어져 서는지(m). 임시")]
        [Min(0.5f)] public float bossDistanceFromWall = 4f;
        [Tooltip("성공 시 행동 불능(초). 기획서 4.3 분기 A: 3초")]
        [Min(0f)] public float successGroggyTime = 3f;
        [Tooltip("켜면 '벽 파괴 + 보스 타격'까지 해야 성공 (기획서 2.2 문구). 끄면 벽 파괴만으로 성공 (행동 트리·4.3 플로우)")]
        public bool requireBossHitAfterBreak = false;
        public Color wallColor = new Color(1f, 0.85f, 0.35f, 0.55f);
    }

    /// <summary>
    /// 성운의 파편. hits[0] (World) = 중심 구체 접촉, hits[1] (Projectile) = 탄막 한 번 (repeatCount = 탄막 횟수).
    /// 중앙 상공으로 떠올라 거대 구체를 소환하고 별 모양으로 줄기를 방사한다 (기획서 4.3).
    /// </summary>
    [Serializable]
    public class NebulaShardsSettings
    {
        [Tooltip("중심 구체 반경(m). 기획서 4.3: 1.5m 원형")]
        [Min(0.1f)] public float sphereRadius = 1.5f;
        [Tooltip("보스가 떠오르는 높이(m, 최하단 바닥 기준). 임시")]
        [Min(1f)] public float bossHeight = 4.2f;
        [Tooltip("구체가 보스 머리 위 얼마에 생기는지(m). 상세 플로우 그림 기준 — 임시")]
        [Min(0f)] public float sphereAboveBoss = 2.6f;
        [Tooltip("별 모양 줄기 수. 상세 플로우 그림 12갈래")]
        [Min(3)] public int rays = 12;
        [Tooltip("줄기 하나에 실리는 파편 수 (그림의 마름모)")]
        [Min(1)] public int shardsPerRay = 2;
        [Min(0.1f)] public float shardSpacing = 1.1f;
        [Tooltip("파편 속도(m/s). 임시")]
        [Min(0.1f)] public float shardSpeed = 8.5f;
        [Min(0.05f)] public float shardWidth = 0.35f;
        [Min(0.05f)] public float shardLength = 0.8f;
        [Tooltip("탄막마다 각도를 반 칸씩 엇갈려 안전지대를 바꾼다")]
        public bool alternateOffset = true;
        [Tooltip("경고선이 탄막보다 몇 초 먼저 보이는지. 임시")]
        [Min(0f)] public float warnLead = 0.6f;
        public Color sphereColor = new Color(0.75f, 0.55f, 1f, 1f);
        public Color shardColor = new Color(1f, 0.85f, 0.4f, 1f);
    }

    /// <summary>
    /// 인력의 왜곡. hits[0] (World) = 종료 폭발 (time = 흡인 시간).
    /// 특이점이 반경 안의 플레이어를 끌어당기고(가까울수록 강하게), 끝나면 폭발 (기획서 4.3).
    /// </summary>
    [Serializable]
    public class GravityDistortionSettings
    {
        [Tooltip("특이점 높이(m, 최하단 바닥 기준). 기획서 '맵 중앙 상공' — 숫자는 임시 (폭발 반경 2m가 바닥에 닿게)")]
        [Min(0f)] public float height = 1.8f;
        [Tooltip("흡인 반경(m). 기획서 4.3: 4.5m")]
        [Min(0.1f)] public float pullRadius = 4.5f;
        [Tooltip("가장자리 흡인 속도(m/s). 임시")]
        [Min(0f)] public float pullSpeedEdge = 1.2f;
        [Tooltip("중심 흡인 속도(m/s). 임시 — 플레이어 이동 속도(5.5)보다 낮아야 반대로 달려 버틸 수 있다")]
        [Min(0f)] public float pullSpeedCenter = 4.2f;
        [Tooltip("세로 방향 흡인 비율. 0이면 가로로만 끌린다. 임시")]
        [Range(0f, 1f)] public float verticalFactor = 0.3f;
        [Tooltip("종료 폭발 반경(m). 기획서 4.3: 2.0m")]
        [Min(0.1f)] public float explosionRadius = 2f;
        public Color color = new Color(0.55f, 0.35f, 0.95f, 1f);
    }

    /// <summary>
    /// 환영 분신. hits[0] (World) = 오답 분신 자폭 (오답을 때렸을 때만 터진다).
    /// 보스가 흩어지고 좌우 3m에 분신 2체. 진짜를 때리면 보스가 드러나며 경직, 가짜를 때리면 자폭 + 암흑/침묵,
    /// 시간 초과면 분신이 남아 일반 공격을 따라 쏜다 (기획서 4.3).
    /// </summary>
    [Serializable]
    public class PhantomClonesSettings
    {
        [Tooltip("분신 위치: 보스 기준 좌우(m). 기획서 4.3: 3.0m")]
        [Min(0.5f)] public float offset = 3f;
        [Tooltip("분신을 고를 시간(초). 기획서에 없음 — 임시")]
        [Min(0.5f)] public float decisionTime = 5f;
        [Tooltip("정답 시 보스 경직(초). 기획서 '짧은 경직' — 숫자는 임시")]
        [Min(0f)] public float revealStagger = 1.5f;
        [Tooltip("오답 자폭 반경(m). 기획서 4.3: 2.0m")]
        [Min(0.1f)] public float explosionRadius = 2f;
        [Tooltip("암흑 디버프 시간(초). 임시")]
        [Min(0f)] public float darknessTime = 4f;
        [Tooltip("침묵 디버프 시간(초). 임시. PlayerMovement가 PhanesStatusEffects.IsSilenced를 확인해야 실제로 스킬이 막힌다")]
        [Min(0f)] public float silenceTime = 4f;
        [Tooltip("시간 초과 뒤 분신이 남아 일반 공격을 따라 쏘는 시간(초). 기획서 '일정 시간' — 임시")]
        [Min(0f)] public float mimicDuration = 8f;
        [Header("구분 단서 (기획서: 지팡이 보석 색, 마법진 회전 방향 등)")]
        public Color trueGemColor = new Color(1f, 0.82f, 0.25f, 1f);
        public Color fakeGemColor = new Color(0.55f, 0.9f, 1f, 1f);
        [Tooltip("진짜 분신의 마법진 회전(도/초). 가짜는 반대 방향")]
        public float trueCircleSpin = -90f;
    }
}
