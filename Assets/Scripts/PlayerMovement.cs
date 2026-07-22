using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private PlayerUI playerUI; // Canvas 또는 PlayerUI 스크립트 연결

    [Header("이동 및 점프")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("대시 세팅")]
    [SerializeField] private float dashSpeed = 15f;       // 대시 속도
    [SerializeField] private float dashDuration = 0.2f;    // 대시 지속 시간
    [SerializeField] private float dashCooldown = 1.0f;    // 대시 재사용 대기시간
    [SerializeField] private Color dashColor = new Color(0.4f, 0.4f, 0.4f, 1f); // 대시 중 본체 색상
    [SerializeField] private float dashAnimationFrame = 0.2f; // Run 애니메이션 중 대시 자세로 쓸 프레임 위치

    [Header("잔상(Ghost) 세팅")]
    [SerializeField] private Color ghostColor = new Color(0.3f, 0.6f, 1f, 0.6f); // 잔상 색상
    [SerializeField] private float ghostInterval = 0.03f; // 잔상 생성 간격
    [SerializeField] private float ghostFadeTime = 0.2f;   // 잔상 사라지는 시간

    [Header("공격 세팅")]
    [SerializeField] private GameObject attackArea;
    [SerializeField] private float attackDuration = 0.15f;
    [SerializeField] private int attackDamage = 1;

    [Header("피격 및 넉백")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color hitColor = Color.red;

    private int _currentHealth;
    private float _horizontalInput;
    private bool _isJumpPressed;
    private bool _isGrounded;
    private bool _isAttacking;
    private bool _isDashing;
    private bool _canDash = true;
    private bool _isInvincible;
    private bool _isKnockedBack;
    private bool _isDead;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Color _originalColor;
    private readonly Vector2 _boxSize = new Vector2(1.0f, 0.2f);

    private float _facingDirection = 1f;
    private Coroutine _attackCoroutine;

    void Start()
    {
        _currentHealth = maxHealth;
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator = GetComponent<Animator>();

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }

        if (attackArea != null)
        {
            attackArea.SetActive(false);
        }

        // 게임 시작 시 체력바 초기화
        if (playerUI != null)
        {
            playerUI.UpdateHPBar(_currentHealth, maxHealth);
        }
    }

    void Update()
    {
        if (_isDead) return;

        if (!_isKnockedBack && !_isDashing)
        {
            _horizontalInput = Input.GetAxisRaw("Horizontal");

            if (_horizontalInput != 0 && !_isAttacking)
            {
                _facingDirection = _horizontalInput;
                transform.localScale = new Vector3(_facingDirection, 1f, 1f);
            }

            if (Input.GetKeyDown(KeyCode.LeftShift) && _canDash)
            {
                StartCoroutine(DashRoutine());
            }

            if (Input.GetKeyDown(KeyCode.Space) && _isGrounded && !_isAttacking)
            {
                _isJumpPressed = true;
                if (_animator != null)
                {
                    _animator.SetTrigger("Jump");
                }
            }

            if (Input.GetKeyDown(KeyCode.Z) && !_isAttacking)
            {
                _attackCoroutine = StartCoroutine(AttackRoutine());
            }
        }

        if (groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapBox(groundCheck.position, _boxSize, 0f, groundLayer);
        }

        // 애니메이터 파라미터 연동
        if (_animator != null && !_isDashing)
        {
            bool isMoving = Mathf.Abs(_horizontalInput) > 0.01f && !_isAttacking;
            _animator.SetInteger("AnimState", isMoving ? 1 : 0);
            _animator.SetBool("IsGrounded", _isGrounded);

            // 절벽 낙하 처리
            if (!_isGrounded && _rb.linearVelocity.y < -0.1f && !_isAttacking)
            {
                _animator.Play("Hero_Fall");
            }
        }
    }

    void FixedUpdate()
    {
        if (_isDead) return;

        if (_isDashing)
        {
            float dashDir = _horizontalInput != 0 ? _horizontalInput : _facingDirection;
            _rb.linearVelocity = new Vector2(dashDir * dashSpeed, 0f);
            return;
        }

        if (!_isKnockedBack)
        {
            float currentMoveSpeed = _isAttacking ? 0f : _horizontalInput * moveSpeed;
            _rb.linearVelocity = new Vector2(currentMoveSpeed, _rb.linearVelocity.y);

            if (_isJumpPressed)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
                _isJumpPressed = false;
            }
        }
    }

    private IEnumerator DashRoutine()
    {
        _canDash = false;
        _isDashing = true;
        _isInvincible = true;

        if (_isAttacking)
        {
            if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
            if (attackArea != null) attackArea.SetActive(false);
            _isAttacking = false;
        }

        int playerLayer = gameObject.layer;
        int enemyLayerIndex = LayerMask.NameToLayer("Enemy");
        if (enemyLayerIndex != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, true);
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = dashColor;
        }

        if (_animator != null)
        {
            _animator.SetInteger("AnimState", 1);
            _animator.Play("Hero_Run", 0, dashAnimationFrame);
            _animator.speed = 0f;
        }

        float originalGravity = _rb.gravityScale;
        _rb.gravityScale = 0f;

        StartCoroutine(SpawnGhostsRoutine());

        yield return new WaitForSeconds(dashDuration);

        _rb.gravityScale = originalGravity;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }

        if (_animator != null)
        {
            _animator.speed = 1f;
        }

        if (enemyLayerIndex != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, false);
        }

        _isDashing = false;
        _isInvincible = false;

        yield return new WaitForSeconds(dashCooldown);
        _canDash = true;
    }

    private IEnumerator SpawnGhostsRoutine()
    {
        while (_isDashing)
        {
            CreateGhost();
            yield return new WaitForSeconds(ghostInterval);
        }
    }

    private void CreateGhost()
    {
        if (_spriteRenderer == null) return;

        GameObject ghostObj = new GameObject("DashGhost");
        ghostObj.transform.position = transform.position;
        ghostObj.transform.localScale = transform.localScale;

        SpriteRenderer ghostSR = ghostObj.AddComponent<SpriteRenderer>();
        ghostSR.sprite = _spriteRenderer.sprite;
        ghostSR.color = ghostColor;
        ghostSR.sortingLayerID = _spriteRenderer.sortingLayerID;
        ghostSR.sortingOrder = _spriteRenderer.sortingOrder - 1;

        StartCoroutine(FadeAndDestroyGhost(ghostObj, ghostSR));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghostObj, SpriteRenderer ghostSR)
    {
        float timer = 0f;
        Color initialColor = ghostSR.color;

        while (timer < ghostFadeTime)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(initialColor.a, 0f, timer / ghostFadeTime);
            ghostSR.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
            yield return null;
        }

        Destroy(ghostObj);
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }

        if (attackArea != null)
        {
            attackArea.SetActive(true);
        }

        yield return new WaitForSeconds(attackDuration);

        if (attackArea != null)
        {
            attackArea.SetActive(false);
        }

        _isAttacking = false;
    }

    public int GetDamage() => attackDamage;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_isDead || _isInvincible || _isDashing) return;

        if (collision.gameObject.CompareTag("Enemy") && !_isKnockedBack)
        {
            Vector2 knockbackDir = (transform.position - collision.transform.position).normalized;
            TakeDamage(1, knockbackDir);
        }
    }

    public void TakeDamage(int damage, Vector2 knockbackDir)
    {
        if (_isDead || _isInvincible || _isDashing) return;

        _currentHealth -= damage;
        Debug.Log($"플레이어 피격! 남은 체력: {_currentHealth}");

        // 피격 시 체력바 업데이트
        if (playerUI != null)
        {
            playerUI.UpdateHPBar(_currentHealth, maxHealth);
        }

        if (_currentHealth <= 0) Die();
        else StartCoroutine(HitRoutine(knockbackDir));
    }

    private IEnumerator HitRoutine(Vector2 knockbackDir)
    {
        _isKnockedBack = true;

        if (_animator != null) _animator.SetTrigger("Hurt");

        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(new Vector2(knockbackDir.x * knockbackForce, knockbackForce * 0.5f), ForceMode2D.Impulse);

        if (_spriteRenderer != null) _spriteRenderer.color = hitColor;

        yield return new WaitForSeconds(flashDuration);

        if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;

        yield return new WaitForSeconds(0.2f);
        _isKnockedBack = false;
    }

    private void Die()
    {
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(0, maxHealth);
        }

        if (_animator != null) _animator.SetTrigger("Die");
        Debug.Log("플레이어 사망!");
    }
}