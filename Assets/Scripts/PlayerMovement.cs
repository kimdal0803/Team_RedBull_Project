using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동 및 점프")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("대시 설정")]
    [SerializeField] private float dashForce = 15f;     // 대시 속도
    [SerializeField] private float dashTime = 0.2f;      // 대시 지속 시간
    [SerializeField] private float dashCooldown = 1f;  // 대시 쿨타임

    [Header("공격 설정")]
    [SerializeField] private GameObject attackRangeObject; // 공격 범위 오브젝트
    [SerializeField] private float attackDuration = 0.15f; // 공격 판정 지속 시간
    [SerializeField] private float attackCooldown = 0.3f; // 공격 쿨타임

    private float _horizontalInput;
    private bool _isJumpPressed;
    private bool _isGrounded;
    private Rigidbody2D _rb;
    private readonly Vector2 _boxSize = new Vector2(0.8f, 0.1f);

    // 상태 변수들
    private bool _canDash = true;
    private bool _isDashing;
    private bool _canAttack = true;
    private bool _isAttacking;
    private float _facingDirection = 1f; // 바라보는 방향 (1 = 오른쪽, -1 = 왼쪽)

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();

        // 시작할 때 공격 범위는 꺼둡니다.
        if (attackRangeObject != null)
            attackRangeObject.SetActive(false);
    }

    void Update()
    {
        // 대시 중에는 입력 및 이동을 무시합니다.
        if (_isDashing) return;

        // 1. 방향 전환 기억하기 (대시 및 공격 방향 결정용)
        _horizontalInput = Input.GetAxisRaw("Horizontal");
        if (_horizontalInput != 0)
        {
            _facingDirection = _horizontalInput;
            // 캐릭터 회전 (오른쪽/왼쪽 바라보기)
            transform.localScale = new Vector3(_facingDirection, 1f, 1f);
        }

        // 2. 바닥 체크
        _isGrounded = Physics2D.OverlapBox(groundCheck.position, _boxSize, 0f, groundLayer);

        // 3. 점프 입력
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            _isJumpPressed = true;
        }

        // 4. 대시 입력 (Left Shift)
        if (Input.GetKeyDown(KeyCode.LeftShift) && _canDash)
        {
            StartCoroutine(DashRoutine());
        }

        // 5. 공격 입력 (Z 키 또는 마우스 좌클릭)
        if ((Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(0)) && _canAttack)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    void FixedUpdate()
    {
        if (_isDashing) return;

        // 좌우 이동 처리
        _rb.linearVelocity = new Vector2(_horizontalInput * moveSpeed, _rb.linearVelocity.y);

        // 점프 처리
        if (_isJumpPressed)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _isJumpPressed = false;
        }
    }

    // 대시 코루틴 시스템
    private IEnumerator DashRoutine()
    {
        _canDash = false;
        _isDashing = true;

        // 대시할 때 중력 영향을 안 받게 잠시 0으로 만듦
        float originalGravity = _rb.gravityScale;
        _rb.gravityScale = 0f;

        // 바라보는 방향으로 강한 속도 부여 (y축은 0으로 고정해서 직선 대시)
        _rb.linearVelocity = new Vector2(_facingDirection * dashForce, 0f);

        yield return new WaitForSeconds(dashTime);

        // 대시 종료 후 원상복구
        _rb.gravityScale = originalGravity;
        _isDashing = false;

        // 쿨타임 대기
        yield return new WaitForSeconds(dashCooldown);
        _canDash = true;
    }

    // 공격 코루틴 시스템
    private IEnumerator AttackRoutine()
    {
        _canAttack = false;
        _isAttacking = true;

        // 공격 범위 오브젝트 켜기 (판정 발생)
        if (attackRangeObject != null)
            attackRangeObject.SetActive(true);

        yield return new WaitForSeconds(attackDuration);

        // 공격 범위 오브젝트 끄기
        if (attackRangeObject != null)
            attackRangeObject.SetActive(false);

        _isAttacking = false;

        // 공격 쿨타임 대기
        yield return new WaitForSeconds(attackCooldown);
        _canAttack = true;
    }

    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(groundCheck.position, _boxSize);
        }
    }
}