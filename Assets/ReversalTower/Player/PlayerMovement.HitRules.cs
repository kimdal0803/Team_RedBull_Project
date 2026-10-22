using UnityEngine;

/// <summary>
/// PlayerMovement — 피격 규칙 (패링 가능 / 회피 불가)
///
/// 해결하는 문제 (README_FSM보스_수집기.md '알려진 한계'):
///   1) 패링이 모든 공격을 막았다 — 패링 불가로 설계된 패턴까지 막혔다
///   2) 회피불가 공격이 대시 무적에 그대로 막혔다
///
/// 보스 쪽은 이미 정보를 갖고 있었다 (HitData.parryable, BossHitInfo.IgnoresDodge).
/// 플레이어의 TakeDamage가 그 정보를 받을 통로가 없어서 버려지고 있었을 뿐이다.
/// 여기서 통로를 연다.
///
/// 근거: SPEC.md §2.4 패링 / §5.1 아라쿠스 패턴별 패링 가부
///   패링 가능 패턴을 패링   → 피해 0 · 게이지 +15 · 보스 경직
///   패링 불가 패턴을 패링   → 판정 자체가 발생하지 않음 → 정상 피해
///   회피 불가 공격         → 대시·피격 무적을 뚫고 적중
/// </summary>
public partial class PlayerMovement
{
    // 지금 들어오는 공격이 패링 가능한가. 기본값 true.
    //
    // true가 기본인 이유: 기존 Enemy.cs 등은 2개짜리 TakeDamage를 그대로 부른다.
    // 그쪽 동작이 바뀌면 안 되므로, 정보를 안 넘기면 예전처럼 패링이 통한다.
    private bool _incomingParryable = true;

    // 지금 들어오는 공격이 무적을 무시하는가. 기본값 false.
    private bool _incomingIgnoresDodge = false;

    /// <summary>
    /// 패링 가부 · 회피 가부를 함께 받는 TakeDamage.
    /// FSM 보스(PlayerBossHitAdapter)가 이쪽을 부른다.
    ///
    /// 기존 2개짜리 TakeDamage는 그대로 살아 있다. 기존 호출부는 건드릴 필요가 없다.
    /// </summary>
    /// <param name="damage">피해량 (플레이어 체력 단위)</param>
    /// <param name="knockbackDir">넉백 방향</param>
    /// <param name="parryable">이 공격을 패링으로 막을 수 있는가</param>
    /// <param name="ignoresDodge">이 공격이 대시·피격 무적을 뚫는가</param>
    public void TakeDamage(
        int damage,
        Vector2 knockbackDir,
        bool parryable,
        bool ignoresDodge)
    {
        _incomingParryable = parryable;
        _incomingIgnoresDodge = ignoresDodge;

        try
        {
            // 기존 TakeDamage를 그대로 태운다.
            // 그 안에서 아래 두 프로퍼티를 읽어 판정이 갈린다.
            TakeDamage(damage, knockbackDir);
        }
        finally
        {
            // 어떤 경로로 빠져나가든 기본값으로 되돌린다.
            // 안 되돌리면 다음에 들어오는 일반 공격까지 이 설정을 물려받는다.
            _incomingParryable = true;
            _incomingIgnoresDodge = false;
        }
    }

    /// <summary>
    /// 지금 들어온 공격이 무적에 막히는가.
    ///
    /// 기존 조건: `_isDead || _isInvincible || _isDashing`
    /// 바뀐 점:
    ///   - 회피 불가(ignoresDodge) 공격은 무적을 뚫는다
    ///   - _isDashing을 빼고 _isInvincible만 본다
    ///     → 대시 무적이 0.15s로 짧아졌으므로(설계서 2.3), 대시 끝 0.05s에는 맞아야 한다
    /// </summary>
    private bool IsHitBlockedByInvincibility
    {
        get
        {
            if (_incomingIgnoresDodge) return false;
            return _isInvincible;
        }
    }

    /// <summary>
    /// 지금 들어온 공격을 패링으로 막아 내는가.
    ///
    /// 패링 중이어도 패링 불가 패턴이면 막히지 않는다 (SPEC §2.4).
    /// 이게 "패링 가능 비율 35~40%" 설계가 실제로 작동하는 근거다.
    /// </summary>
    private bool IsHitParried
    {
        get
        {
            if (!IsParrying) return false;
            return _incomingParryable;
        }
    }
}
