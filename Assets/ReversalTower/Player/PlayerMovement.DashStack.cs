using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerMovement — 스택형 대시
///
/// 근거: 전투방식설계서 v0.4  2.3 대시 — 스택형
///   대시 거리 / 지속   4m / 0.2s   (지상·공중 동일)
///   대시 무적          0.15s       (시작 즉시. 끝 0.05s는 무적 없음)
///   대시 스택          2회 / 충전 2.5s
///   연속 사용 최소 간격  0.3s
///
/// 왜 스택형인가 (설계서 2.3):
///   스태미너를 두면 관리할 게이지가 체력·스태미너·궁극기 3개가 되고,
///   패링에 스태미너 비용을 매기면 '패링을 장려하는 설계'와 정면으로 충돌한다.
///   스택형은 급할 때 2번 연속 구르고 이후 5초를 감수하는 선택을 만든다.
///
/// 기존 코드와의 차이:
///   기존은 _canDash 하나에 dashCooldown(0.8s) 단일 쿨이었다.
///   무적도 대시 전체 구간(0.18s)에 걸려 있어, 설계서의 "끝 0.05s는 무적 없음"이 없었다.
/// </summary>
public partial class PlayerMovement
{
    [Header("대시 스택 (전투방식설계서 2.3)")]

    [Tooltip("보유 가능한 최대 대시 스택. 설계서 기준 2")]
    [Min(1)]
    [SerializeField] private int dashMaxStacks = 2;

    [Tooltip("스택 1개가 차는 데 걸리는 시간(초). 설계서 기준 2.5")]
    [Min(0.1f)]
    [SerializeField] private float dashStackRechargeTime = 2.5f;

    [Tooltip("연속 대시 사이의 최소 간격(초). 설계서 기준 0.3")]
    [Min(0f)]
    [SerializeField] private float dashMinInterval = 0.3f;

    [Tooltip("대시 시작부터 무적이 유지되는 시간(초). 설계서 기준 0.15 (대시 지속 0.2보다 짧다)")]
    [Min(0f)]
    [SerializeField] private float dashInvincibleTime = 0.15f;

    // 현재 보유 스택. -1은 '아직 초기화 전'을 뜻한다.
    private int _dashStacks = -1;

    // 다음 스택이 찰 때까지 남은 시간.
    private float _dashRechargeTimer;

    // 마지막으로 대시를 시작한 시각. 연속 간격 체크에 쓴다.
    private float _lastDashStartTime = -999f;

    /// <summary>현재 보유한 대시 스택 수 (UI·수집기용)</summary>
    public int DashStacks => Mathf.Max(0, _dashStacks);

    /// <summary>최대 대시 스택 수 (UI용)</summary>
    public int DashMaxStacks => dashMaxStacks;

    /// <summary>
    /// 지금 대시를 쓸 수 있는가.
    /// 기존 Update의 `_canDash` 자리를 이 프로퍼티가 대신한다.
    /// </summary>
    public bool CanDashNow
    {
        get
        {
            EnsureDashStacksInitialized();

            if (_isDashing) return false;
            if (_dashStacks <= 0) return false;

            // 연속 대시 최소 간격 — 두 스택을 같은 프레임에 쏟아붓지 못하게 한다.
            if (Time.time - _lastDashStartTime < dashMinInterval) return false;

            return true;
        }
    }

    private void EnsureDashStacksInitialized()
    {
        if (_dashStacks < 0)
        {
            _dashStacks = dashMaxStacks;
            _dashRechargeTimer = dashStackRechargeTime;
        }
    }

    /// <summary>
    /// 매 프레임 호출한다. PlayerMovement.Update()가 불러 준다.
    /// 스택이 꽉 차 있으면 아무것도 하지 않는다.
    /// </summary>
    private void TickDashStacks()
    {
        EnsureDashStacksInitialized();

        if (_dashStacks >= dashMaxStacks)
        {
            // 꽉 찼으면 타이머를 되감아 둔다.
            // 안 그러면 스택을 쓰자마자 즉시 하나가 차 버린다.
            _dashRechargeTimer = dashStackRechargeTime;
            return;
        }

        _dashRechargeTimer -= Time.deltaTime;

        if (_dashRechargeTimer <= 0f)
        {
            _dashStacks++;
            _dashRechargeTimer = dashStackRechargeTime;
        }
    }

    /// <summary>
    /// 대시를 시작할 때 스택을 하나 소모한다. DashRoutine이 불러 준다.
    /// </summary>
    private void ConsumeDashStack()
    {
        EnsureDashStacksInitialized();

        _dashStacks = Mathf.Max(0, _dashStacks - 1);
        _lastDashStartTime = Time.time;
    }

    /// <summary>
    /// 대시 무적을 dashInvincibleTime 동안만 켠다.
    ///
    /// 설계서 2.3: "대시 무적 0.15s — 대시 시작 즉시, 끝 0.05s는 무적 없음".
    /// 이 0.05s가 있어야 대시로 모든 걸 흘릴 수 없고, 거리를 읽는 플레이가 생긴다.
    /// </summary>
    private IEnumerator DashInvincibilityRoutine()
    {
        _isInvincible = true;

        yield return new WaitForSeconds(dashInvincibleTime);

        // 피격 무적 등 다른 이유로 무적이 켜져 있을 수 있으므로,
        // 대시가 끝난 뒤에만 끈다. (HitRoutine이 따로 관리한다)
        if (!_isKnockedBack)
        {
            _isInvincible = false;
        }
    }
}
