using System;
using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 보스 패턴 하나의 설정값. 코드 없이 수치만 바꿔서 패턴을 만들고 고친다.
    /// 시간은 모두 "속도 배율 1" 기준 값이고, 2페이즈 속도 증가는 BossController가 나눠서 적용한다.
    ///
    /// 타임라인 읽는 법 (보스 기획서 타임라인과 같은 방식):
    ///   0s ── 선딜(telegraphTime) ──▶ 공격 시작 ── 판정들(hits[].time) ── 공격 끝 ── 후딜(recoveryTime) ──▶ 다음 행동
    ///   예) 아디쿠스 X자 베기: Hit1 0.7s, Hit2 1.3s, 총 2.0s
    ///       → telegraphTime 0.7 / hits[0].time 0, hits[1].time 0.6 / recoveryTime 0.7
    /// </summary>
    [CreateAssetMenu(fileName = "Pattern_New", menuName = "역전의 탑/보스 패턴 데이터", order = 0)]
    public class BossPatternData : ScriptableObject
    {
        [Header("기본")]
        [Tooltip("에셋 이름 규칙과 같은 키. 예: Arakus_SwordSwinging. 로그·DDA가 이 값으로 패턴을 구분한다.")]
        public string patternId;
        public string displayName;
        public PatternKind kind = PatternKind.Melee;
        [Tooltip("근거 문서, 임시값 표시 등 메모")]
        [TextArea(2, 5)] public string designNote;

        [Header("사용 조건")]
        [Min(1)] public int minPhase = 1;
        [Min(1)] public int maxPhase = 99;
        [Tooltip("보스 정면 끝 ~ 플레이어 몸 가장자리까지 거리(m)가 이 범위일 때만 후보가 된다.")]
        [Min(0)] public float minDistance = 0f;
        [Min(0)] public float maxDistance = 2f;
        [Tooltip("다시 쓸 수 있을 때까지 기다리는 시간(초). 패턴을 시작할 때부터 센다.")]
        [Min(0)] public float cooldown = 0f;
        [Tooltip("무작위 선택 가중치. 0이면 무작위 선택에서 빠진다 (페이즈 전환처럼 강제로만 쓰는 패턴).")]
        [Min(0)] public float weight = 1f;
        [Tooltip("높을수록 먼저 고른다. 조건형 패턴(순간이동, 저울 반사)을 거리 패턴보다 먼저 확인할 때 사용.")]
        public int priority = 0;
        [Tooltip("비워 두면 조건 없음. BossController.RegisterCondition으로 등록한 이름. 기본 제공: TargetBehind")]
        public string conditionKey = "";
        [Tooltip("시작할 때 플레이어 쪽으로 돌아선다")]
        public bool faceTargetOnStart = true;

        [Header("타임라인 (초, 속도 배율 1 기준)")]
        [Tooltip("선딜: 패턴 시작 ~ 공격 시작. 전투방식설계서의 '선딜' 정의와 같다. DDA 조정 대상.")]
        [Min(0)] public float telegraphTime = 0.8f;
        [Tooltip("공격 단계 최소 길이. 판정이 끝난 뒤에도 동작이 이어지면 설정. 0이면 마지막 판정이 끝날 때 공격 단계도 끝난다.")]
        [Min(0)] public float attackDuration = 0f;
        [Tooltip("후딜: 공격 단계가 끝난 뒤 다음 행동까지의 빈틈.")]
        [Min(0)] public float recoveryTime = 0.7f;

        [Header("판정 (공격 시작 기준 시간)")]
        public HitData[] hits = { new HitData() };

        [Header("패링 보상")]
        [Tooltip("패링 가능한 타를 전부 패링하면 걸리는 그로기(초). 0이면 없음. 예: 아디쿠스 낫 전체회전 2초")]
        [Min(0)] public float fullParryGroggyTime = 0f;

        [Header("받는 피해 (패턴 진행 중)")]
        [Tooltip("1 = 그대로, 0.5 = 절반, 0 = 무적. 예: 맹목의 심판 0.5, 순간이동 소멸 중 0")]
        [Range(0f, 1f)] public float damageTakenMultiplier = 1f;

        [Header("돌진 (kind가 Dash 또는 Projectile일 때)")]
        public DashSettings dash = new DashSettings();

        [Header("투사체 (kind가 Projectile일 때만)")]
        public ProjectileSettings projectile = new ProjectileSettings();

        [Header("연출 (선택)")]
        [Tooltip("Animator 트리거 이름. 비우면 재생하지 않는다.")]
        public string telegraphAnimTrigger;
        public string attackAnimTrigger;
        [Tooltip("이펙트·사운드를 찾을 키. 에셋 목록 파일명과 맞춘다. 예: Arakus_SwordSwinging_FX")]
        public string fxKey;
        public string sfxKey;

        void OnValidate()
        {
            if (maxDistance < minDistance) maxDistance = minDistance;
            if (maxPhase < minPhase) maxPhase = minPhase;
            if (string.IsNullOrEmpty(patternId)) patternId = name;
        }
    }

    /// <summary>판정 한 개 (다단히트면 repeatCount만큼 반복)</summary>
    [Serializable]
    public class HitData
    {
        [Tooltip("공격 시작 기준 시간(초). 첫 타는 보통 0.")]
        [Min(0)] public float time = 0f;
        [Tooltip("판정 유지 시간(초). 0이면 한 순간. 돌진처럼 이동하며 닿는 공격은 이동 시간만큼. 같은 대상은 한 번만 맞는다.")]
        [Min(0)] public float activeTime = 0f;
        [Tooltip("다단히트 타수. DDA 조정 대상.")]
        [Min(1)] public int repeatCount = 1;
        [Tooltip("다단히트 간격(초). 피격 무적 0.35초보다 짧으면 사이 타는 무적에 막힌다. DDA 조정 대상.")]
        [Min(0.01f)] public float repeatInterval = 0.25f;
        public HitboxShape box = new HitboxShape();
        [Tooltip("보스 공격력 × 계수. 1 = 100%. DDA 조정 대상.")]
        [Min(0)] public float damageCoefficient = 1f;
        public KnockbackLevel knockback = KnockbackLevel.Light;
        [Tooltip("패링 가능. 켜면 판정 0.3초 전에 패링 신호 이벤트가 나간다 (전투방식설계서 4.1).")]
        public bool parryable = false;
        [Tooltip("패링당하면 보스가 이 시간만큼 경직(초). 예: X자 베기 0.6, 사슬낫 1.5")]
        [Min(0)] public float parryStaggerTime = 0.6f;
        [Tooltip("켜면 패링당했을 때 패턴을 끊고 경직. 끄면 패턴은 계속 (낫 전체회전처럼 박자 패링).")]
        public bool parryInterruptsPattern = true;

        [Tooltip("회피 불가. 켜면 플레이어의 구르기 무적·피격 무적을 무시하고 그대로 맞는다.\n" +
                 "기획서의 '회피불가' 표기에 해당한다 (아라쿠스 일대 휩쓸기). 패링은 막히지 않는다.")]
        public bool ignoresDodge = false;

        [Tooltip("이 판정을 누가 들고 있는지. Projectile로 두면 날아가는 투사체가 판정을 들고 간다.\n" +
                 "kind가 Projectile인 패턴에서만 의미가 있다.")]
        public HitCarrier carrier = HitCarrier.Boss;
    }

    /// <summary>
    /// 판정 박스. 2D 측면 기준 '가로 × 세로(m)'에 판정 높이를 함께 적는다.
    /// 판정 꼭대기(Top)가 점프 높이 1.8m보다 높으면 점프로 넘을 수 없다.
    ///
    /// carrier가 Projectile인 판정은 anchor·forwardStart·bottom을 쓰지 않고,
    /// width × height 크기의 박스가 투사체 한가운데를 따라다닌다.
    /// </summary>
    [Serializable]
    public class HitboxShape
    {
        public HitboxAnchor anchor = HitboxAnchor.FrontEdge;
        [Tooltip("FrontEdge일 때만: 보스 정면 끝에서 앞으로 얼마 떨어진 곳부터 시작하는지(m). 음수면 몸 안쪽부터.")]
        public float forwardStart = 0f;
        [Tooltip("가로(m)")]
        [Min(0.01f)] public float width = 2f;
        [Tooltip("판정 아래쪽 높이(m). 지면 = 0. 예: 사슬낫 0.3")]
        [Min(0)] public float bottom = 0f;
        [Tooltip("세로(m). 판정 높이 = bottom + height")]
        [Min(0.01f)] public float height = 2f;

        public float Top => bottom + height;
    }

    /// <summary>돌진 설정 (kind = Dash, 또는 Projectile에서 던진 뒤 따라붙는 대쉬)</summary>
    [Serializable]
    public class DashSettings
    {
        [Tooltip("공격 시작 기준, 이동을 시작하는 시점(초)")]
        [Min(0)] public float startTime = 0f;
        [Tooltip("이동 거리(m). 0이면 이동하지 않는다.")]
        [Min(0)] public float distance = 5f;
        [Tooltip("이동에 걸리는 시간(초)")]
        [Min(0.01f)] public float duration = 1f;
        [Tooltip("켜면 뒤로 물러난다 (퀵스텝)")]
        public bool backward = false;
        [Tooltip("BossController의 wallMask(맵 벽)에 닿으면 멈춘다")]
        public bool stopAtWall = true;
        [Tooltip("이 레이어 오브젝트에 부딪히면 그로기 (아라쿠스 돌진 기믹의 저지 오브젝트)")]
        public LayerMask groggyObjectMask;
        [Tooltip("그로기 시간(초). 0이면 그로기 없이 멈추기만 한다.")]
        [Min(0)] public float groggyTime = 0f;
    }

    /// <summary>
    /// 투사체 설정 (kind = Projectile).
    /// 판정은 hits 중 carrier가 Projectile인 것들이 투사체에 실린다.
    /// 그 판정들은 time·activeTime을 쓰지 않고 투사체가 살아 있는 동안 계속 켜져 있다
    /// (같은 대상은 판정 하나당 한 번만 맞는다).
    /// </summary>
    [Serializable]
    public class ProjectileSettings
    {
        [Tooltip("공격 시작 기준, 투사체가 생기는 시점(초). 기획서의 '투척' 시점.")]
        [Min(0)] public float spawnTime = 0f;
        [Tooltip("보스 발밑 기준 생성 높이(m). 손 높이쯤.")]
        public float spawnHeight = 1.2f;
        [Tooltip("보스 정면 끝에서 앞으로 얼마 떨어진 곳에서 생기는지(m)")]
        public float spawnForward = 0.3f;
        [Tooltip("날아가는 속도(m/s)")]
        [Min(0.01f)] public float speed = 12f;
        [Tooltip("날아가는 최대 거리(m). 여기까지 가면 멈추거나 돌아온다.")]
        [Min(0.1f)] public float maxDistance = 7f;
        [Tooltip("켜면 최대 거리에서 보스 쪽으로 되돌아온다 (방패 부메랑)")]
        public bool returns = true;
        [Tooltip("돌아오는 속도(m/s). 0이면 speed와 같다.")]
        [Min(0)] public float returnSpeed = 0f;
        [Tooltip("보스에게 이만큼 가까워지면 사라진다(m). 돌아오는 방패를 다시 잡는 지점.")]
        [Min(0.05f)] public float catchDistance = 0.6f;
        [Tooltip("켜면 누군가를 맞힌 즉시 사라진다. 끄면 통과하며 계속 날아간다.")]
        public bool despawnOnHit = false;
        [Tooltip("보이는 크기(m). 아트가 붙기 전 임시 네모의 크기이기도 하다.")]
        public Vector2 visualSize = new Vector2(0.8f, 0.8f);
        [Tooltip("임시 네모 색 (아트 스프라이트를 넣으면 이 색으로 물든다)")]
        public Color visualColor = new Color(0.85f, 0.8f, 0.5f, 1f);
        [Tooltip("비우면 임시 네모를 만든다. 방패 스프라이트가 나오면 여기에 넣는다.")]
        public Sprite sprite;
        [Tooltip("초당 회전(도). 날아가는 방패가 도는 연출.")]
        public float spinSpeed = 720f;
        [Tooltip("안전장치: 이 시간이 지나면 무조건 사라진다(초).")]
        [Min(0.1f)] public float maxLifetime = 6f;
    }
}
