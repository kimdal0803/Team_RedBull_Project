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
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashTime = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("공격 설정")]
    [SerializeField] private GameObject attackRangeObject;
    [SerializeField] private float attackDuration = 0.15f;
    [SerializeField] private float attackCooldown = 0.3f;

    [Header("플레이어 체력 설정")]
    [SerializeField] private float maxHealth = 5f;
    private float _currentHealth;

    private float _horizontalInput;
    private bool _isJumpPressed;
    private bool _isGrounded;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private readonly Vector2 _boxSize = new Vector2(1.0f, 0.2f);

    private bool _canDash = true;
    private bool _isDashing;
    private bool _canAttack = true;
    private float _facingDirection = 1f; // 바라보는 방향 (1 = 오른쪽, -1 = 왼쪽)

    // 피격 상태 변수
    private bool _isKnockedback;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _currentHealth = maxHealth;

        if (attackRangeObject != null)
            attackRangeObject.SetActive(false);
    }

    void Update()
    {
        // 대시 중이거나 넉백 중일 때는 조작 입력 무시
        if (_isDashing || _isKnockedback) return;

        _horizontalInput = Input.GetAxisRaw("Horizontal");
        if (_horizontalInput != 0)
        {
            _facingDirection = _horizontalInput;
            transform.localScale = new Vector3(_facingDirection, 1f, 1f);
        }

        _isGrounded = Physics2D.OverlapBox(groundCheck.position, _boxSize, 0f, groundLayer);

        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            _isJumpPressed = true;
        }

        if (Input.GetKeyDown(KeyCode.LeftShift) && _canDash)
        {
            StartCoroutine(DashRoutine());
        }

        if ((Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(0)) && _canAttack)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    void FixedUpdate()
    {
        if (_isDashing || _isKnockedback) return;

        _rb.linearVelocity = new Vector2(_horizontalInput * moveSpeed, _rb.linearVelocity.y);

        if (_isJumpPressed)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _isJumpPressed = false;
        }
    }

    // [이 함수가 제대로 있어야 Enemy.cs의 에러가 사라집니다!]
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        Debug.Log($"플레이어 피격! 남은 체력: {_currentHealth}");

        // 맞았을 때 뒤로 약간 튕겨 나가는 넉백 코루틴 실행
        StartCoroutine(KnockbackRoutine());

        if (_currentHealth <= 0)
        {
            Debug.Log("플레이어 사망... 게임 오버!");
        }
    }

    private IEnumerator KnockbackRoutine()
    {
        _isKnockedback = true;

        // 바라보는 반대 방향으로 튕겨 나감
        float knockbackDir = _facingDirection * -1f;
        _rb.linearVelocity = new Vector2(knockbackDir * 4f, 3f); // 뒤로 4, 위로 3만큼 툭 튕김

        // 0.15초 동안 붉게 깜빡임
        Color originalColor = _spriteRenderer.color;
        _spriteRenderer.color = new Color(1f, 0.5f, 0.5f, 1f);

        yield return new WaitForSeconds(0.2f);

        _spriteRenderer.color = originalColor;
        _isKnockedback = false;
    }

    private IEnumerator DashRoutine()
    {
        _canDash = false;
        _isDashing = true;
        float originalGravity = _rb.gravityScale;
        _rb.gravityScale = 0f;
        _rb.linearVelocity = new Vector2(_facingDirection * dashForce, 0f);

        yield return new WaitForSeconds(dashTime);

        _rb.gravityScale = originalGravity;
        _isDashing = false;

        yield return new WaitForSeconds(dashCooldown);
        _canDash = true;
    }

    private IEnumerator AttackRoutine()
    {
        _canAttack = false;

        if (attackRangeObject != null)
            attackRangeObject.SetActive(true);

        yield return new WaitForSeconds(attackDuration);

        if (attackRangeObject != null)
            attackRangeObject.SetActive(false);

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