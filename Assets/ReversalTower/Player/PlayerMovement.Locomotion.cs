using UnityEngine;

/// <summary>
/// PlayerMovement — 기동 보조 (코요테 타임 · 점프 버퍼 · 가변 점프)
///
/// 근거: 전투방식설계서 v0.4  2.1 기본 스탯 / 2.2 이동·점프
///   코요테 타임 0.1s  — 발판을 벗어난 뒤에도 0.1초 동안 점프 입력을 받아준다
///   점프 버퍼  0.12s — 착지 0.12초 전에 누른 점프를 착지 순간 실행한다
///   가변 점프         — 버튼을 떼면 상승 속도를 깎아 점프 높이를 조절한다
///
/// 왜 별도 파일인가: PlayerMovement.cs가 2614줄이라 기능을 더 얹으면 읽기 어려워진다.
///   partial class로 나누면 Inspector 참조와 씬 연결은 그대로 두고 코드만 분리할 수 있다.
/// </summary>
public partial class PlayerMovement
{
    [Header("기동 보조 (전투방식설계서 2.2)")]

    [Tooltip("발판을 벗어난 뒤에도 점프를 받아주는 시간. 설계서 기준 0.1초")]
    [SerializeField] private float coyoteTime = 0.1f;

    [Tooltip("착지 직전에 누른 점프를 기억하는 시간. 설계서 기준 0.12초")]
    [SerializeField] private float jumpBufferTime = 0.12f;

    [Tooltip("점프 버튼을 떼면 상승 속도에 곱하는 값. 0.5면 상승 속도 절반으로 감쇠")]
    [Range(0f, 1f)]
    [SerializeField] private float variableJumpCutMultiplier = 0.5f;

    [Tooltip("점프에 쓸 키. 기존 코드와 같은 Space")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    // 남은 코요테 시간. 지상에 있으면 계속 coyoteTime으로 채워진다.
    private float _coyoteTimer;

    // 남은 점프 버퍼 시간. 점프를 누른 순간 jumpBufferTime으로 채워진다.
    private float _jumpBufferTimer;

    /// <summary>디버그용 — 지금 코요테 유예가 남아 있는지</summary>
    public bool HasCoyoteGrace => _coyoteTimer > 0f;

    /// <summary>
    /// 매 프레임 호출한다. PlayerMovement.Update()가 불러 준다.
    ///
    /// 기존 점프 입력 처리(`GetKeyDown(Space) && _isGrounded`)를 대체한다.
    /// 기존 방식은 발판 끝에서 한 프레임만 늦어도 점프가 씹혔다.
    /// </summary>
    private void TickJumpAssist()
    {
        // ---- 코요테 타임 ----
        // 지상이면 가득 채우고, 공중이면 줄여 나간다.
        if (_isGrounded)
        {
            _coyoteTimer = coyoteTime;
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }

        // ---- 점프 버퍼 ----
        // 누른 순간 기억해 두고, 조건이 맞는 프레임에 꺼내 쓴다.
        if (Input.GetKeyDown(jumpKey))
        {
            _jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            _jumpBufferTimer -= Time.deltaTime;
        }

        // ---- 실제 점프 발동 ----
        // 버퍼에 입력이 남아 있고, 코요테 유예가 남아 있고, 다른 행동 중이 아닐 때.
        if (_jumpBufferTimer > 0f
            && _coyoteTimer > 0f
            && !_isAttacking
            && !_isDashing
            && !_isUsingSkill
            && !_isUsingPotion
            && !_isKnockedBack
            && !IsParrying)
        {
            _isJumpPressed = true;

            // 한 번 쓴 입력과 유예는 즉시 비운다.
            // 안 비우면 같은 입력으로 공중에서 한 번 더 점프한다.
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;

            if (_animator != null)
            {
                _animator.Play("Hero_Jump", 0, 0f);
            }
        }

        // ---- 가변 점프 ----
        // 버튼을 떼는 순간 아직 올라가는 중이면 상승 속도를 깎는다.
        if (Input.GetKeyUp(jumpKey)
            && _rb != null
            && _rb.linearVelocity.y > 0f)
        {
            _rb.linearVelocity = new Vector2(
                _rb.linearVelocity.x,
                _rb.linearVelocity.y * variableJumpCutMultiplier
            );
        }
    }
}
