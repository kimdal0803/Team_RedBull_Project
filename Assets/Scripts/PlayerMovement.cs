using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private PlayerUI playerUI;

    [Header("이동 및 점프")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("대시 세팅")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private Color dashColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [SerializeField] private float dashAnimationFrame = 0.2f;

    [Header("잔상(Ghost) 세팅")]
    [SerializeField] private Color ghostColor = new Color(0.3f, 0.6f, 1f, 0.6f);
    [SerializeField] private float ghostInterval = 0.03f;
    [SerializeField] private float ghostFadeTime = 0.2f;

    [Header("스피디 액션 공격 세팅")]
    [SerializeField] private GameObject attackArea;
    [SerializeField] private float attack1Duration = 0.22f;
    [SerializeField] private float attack2Duration = 0.38f;
    [SerializeField] private float singleAttackCooldown = 0.08f;
    [SerializeField] private float comboEndCooldown = 0.18f;
    [SerializeField] private float step2DashForce = 3.2f;
    [SerializeField] private int baseAttackDamage = 1;

    [Header("피격 및 넉백")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color hitColor = Color.red;

    private int _currentHealth;
    private float _horizontalInput;
    private bool _isJumpPressed;
    private bool _isGrounded;
    private bool _wasGrounded;
    private bool _isAttacking;
    private bool _isAttackCoolingDown;
    private bool _isDashing;
    private bool _canDash = true;
    private bool _isInvincible;
    private bool _isKnockedBack;
    private bool _isDead;

    private int _comboStep = 0;
    private bool _bufferedAttack2 = false;
    private bool _hasTurnedDuringStep2 = false;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;
    private Color _originalColor;
    private readonly Vector2 _boxSize = new Vector2(0.4f, 0.2f);

    private float _facingDirection = 1f;
    private float _dashDirection = 1f; // [추가] 대시 발동 시점의 정확한 방향 저장
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

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(_currentHealth, maxHealth);
        }
    }

    void Update()
    {
        if (_isDead) return;

        if (groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapBox(groundCheck.position, _boxSize, 0f, groundLayer);
        }

        if (!_wasGrounded && _isGrounded)
        {
            OnLanded();
        }
        _wasGrounded = _isGrounded;

        // 1. 공격 중 대시 캔슬 (현재 누른 방향키를 즉시 반영)
        if (Input.GetKeyDown(KeyCode.LeftShift) && _canDash && !_isKnockedBack)
        {
            float currentInput = Input.GetAxisRaw("Horizontal");

            // 대시를 누른 순간 방향키를 누르고 있었다면 그 방향으로 즉시 전환
            if (currentInput != 0)
            {
                _facingDirection = currentInput;
                transform.localScale = new Vector3(_facingDirection, 1f, 1f);
            }

            _dashDirection = _facingDirection;
            StartCoroutine(DashRoutine());
        }

        // 2. 공격 입력
        if (Input.GetKeyDown(KeyCode.Z) && !_isKnockedBack && !_isDashing)
        {
            if (!_isAttacking && !_isAttackCoolingDown)
            {
                _comboStep = 1;
                _bufferedAttack2 = false;
                _hasTurnedDuringStep2 = false;
                if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
                _attackCoroutine = StartCoroutine(ComboRoutine());
            }
            else if (_isAttacking && _comboStep == 1 && _isGrounded)
            {
                _bufferedAttack2 = true;
            }
        }

        // 3. 이동 및 방향 전환 조작
        if (!_isKnockedBack && !_isDashing)
        {
            float rawInput = Input.GetAxisRaw("Horizontal");

            if (rawInput != 0)
            {
                if (_isAttacking && !_isGrounded)
                {
                    if (rawInput != _facingDirection)
                    {
                        _facingDirection = rawInput;
                        transform.localScale = new Vector3(_facingDirection, 1f, 1f);
                        CancelAirAttackMotion();
                    }
                }
                else if (_isAttacking && _isGrounded && _comboStep == 2)
                {
                    if (!_hasTurnedDuringStep2 && rawInput != _facingDirection)
                    {
                        _facingDirection = rawInput;
                        transform.localScale = new Vector3(_facingDirection, 1f, 1f);
                        _hasTurnedDuringStep2 = true;

                        if (Mathf.Abs(_rb.linearVelocity.x) > 0.1f)
                        {
                            _rb.linearVelocity = new Vector2(_facingDirection * step2DashForce, _rb.linearVelocity.y);
                        }
                    }
                }
                else if (!_isAttacking)
                {
                    _facingDirection = rawInput;
                    transform.localScale = new Vector3(_facingDirection, 1f, 1f);
                }
            }

            if (_isAttacking && _isGrounded)
            {
                _horizontalInput = 0f;
            }
            else
            {
                _horizontalInput = rawInput;
            }

            if (Input.GetKeyDown(KeyCode.Space) && _isGrounded && !_isAttacking)
            {
                _isJumpPressed = true;
                if (_animator != null)
                {
                    _animator.Play("Hero_Jump", 0, 0f);
                }
            }
        }

        if (_animator != null && !_isDashing && !_isAttacking)
        {
            bool isMoving = Mathf.Abs(_horizontalInput) > 0.01f;
            _animator.SetInteger("AnimState", isMoving ? 1 : 0);
            _animator.SetBool("IsGrounded", _isGrounded);

            if (!_isGrounded && _rb.linearVelocity.y < -0.1f)
            {
                _animator.Play("Hero_Fall");
            }
        }
    }

    private void OnLanded()
    {
        if (_animator == null) return;

        _animator.ResetTrigger("Jump");
        _animator.SetBool("IsGrounded", true);

        if (!_isAttacking && !_isDashing)
        {
            if (Mathf.Abs(_horizontalInput) > 0.01f)
            {
                _animator.Play("Hero_Run", 0, 0f);
                _animator.SetInteger("AnimState", 1);
            }
            else
            {
                _animator.Play("Hero_Idle", 0, 0f);
                _animator.SetInteger("AnimState", 0);
            }
        }
    }

    void FixedUpdate()
    {
        if (_isDead) return;

        // [수정됨] 대시 발동 시 기록된 방향(_dashDirection)으로 확정 이동
        if (_isDashing)
        {
            _rb.linearVelocity = new Vector2(_dashDirection * dashSpeed, 0f);
            return;
        }

        if (!_isKnockedBack)
        {
            if (_isAttacking && _isGrounded)
            {
                if (_comboStep == 1)
                {
                    _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                }
            }
            else
            {
                _rb.linearVelocity = new Vector2(_horizontalInput * moveSpeed, _rb.linearVelocity.y);

                if (_isJumpPressed)
                {
                    _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
                    _isJumpPressed = false;
                }
            }
        }
    }

    private IEnumerator ComboRoutine()
    {
        _isAttacking = true;
        bool executedCombo2 = false;

        _comboStep = 1;
        _bufferedAttack2 = false;
        _hasTurnedDuringStep2 = false;

        if (_isGrounded)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }

        if (_animator != null)
        {
            _animator.Play("Hero_Attack1", 0, 0f);
        }

        if (attackArea != null) attackArea.SetActive(true);

        yield return new WaitForSeconds(attack1Duration * 0.45f);

        if (attackArea != null) attackArea.SetActive(false);

        yield return new WaitForSeconds(attack1Duration * 0.55f);

        if (_isGrounded && _bufferedAttack2)
        {
            executedCombo2 = true;
            _comboStep = 2;
            _bufferedAttack2 = false;

            _rb.linearVelocity = new Vector2(_facingDirection * step2DashForce, _rb.linearVelocity.y);

            if (_animator != null)
            {
                _animator.Play("Hero_Attack2", 0, 0f);
            }

            if (attackArea != null) attackArea.SetActive(true);

            yield return new WaitForSeconds(attack2Duration * 0.35f);
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            yield return new WaitForSeconds(attack2Duration * 0.15f);
            if (attackArea != null) attackArea.SetActive(false);

            yield return new WaitForSeconds(attack2Duration * 0.5f);
        }

        _comboStep = 0;
        _bufferedAttack2 = false;
        _hasTurnedDuringStep2 = false;
        _isAttacking = false;

        if (_animator != null && _isGrounded)
        {
            if (Mathf.Abs(_horizontalInput) > 0.01f)
            {
                _animator.Play("Hero_Run", 0, 0f);
            }
            else
            {
                _animator.Play("Hero_Idle", 0, 0f);
            }
        }

        float coolTime = executedCombo2 ? comboEndCooldown : singleAttackCooldown;
        StartCoroutine(AttackCooldownRoutine(coolTime));
    }

    private void CancelAirAttackMotion()
    {
        if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
        if (attackArea != null) attackArea.SetActive(false);

        _comboStep = 0;
        _bufferedAttack2 = false;
        _isAttacking = false;

        if (_animator != null)
        {
            _animator.Play("Hero_Fall", 0, 0f);
        }

        StartCoroutine(AttackCooldownRoutine(singleAttackCooldown));
    }

    private IEnumerator AttackCooldownRoutine(float duration)
    {
        _isAttackCoolingDown = true;
        yield return new WaitForSeconds(duration);
        _isAttackCoolingDown = false;
    }

    public int GetDamage()
    {
        return _comboStep switch
        {
            1 => baseAttackDamage,
            2 => baseAttackDamage * 2,
            _ => baseAttackDamage
        };
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
            _comboStep = 0;
            _bufferedAttack2 = false;
            _hasTurnedDuringStep2 = false;
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