using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // 씬 이동(로비 -> BossTest) 시 강화된 스탯 보존용 static 변수
    public static int savedAttack = -1;
    public static int savedMaxHp = -1;
    public static float savedSpeed = -1f;
    public static float savedJump = -1f;

    [Header("UI 연결")]
    [SerializeField] private PlayerUI playerUI;

    [Header("이동 및 점프")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("대시(회피) 세팅")]
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

    [Header("패링(Parry) 세팅")]
    [SerializeField] private float parryWindow = 0.25f;
    [SerializeField] private float parryCooldown = 0.8f; // 회피와 동일한 0.8초 쿨타임
    [SerializeField] private Color parryColor = Color.cyan;
    [SerializeField] private GameObject parryTextUI;
    private float _parryTimer = 0f;
    private bool _canParry = true; // 패링 가능 여부 플래그
    public bool IsParrying => _parryTimer > 0f;

    [Header("피격 및 넉백")]
    [SerializeField] private int maxHealth = 5;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float flashDuration = 0.1f;
    [Tooltip("피격 애니메이션(Hero_TakeHit) 및 경직 유지 시간")]
    [SerializeField] private float hitStunDuration = 0.35f;
    [SerializeField] private Color hitColor = Color.red;

    // 외부 UI/매니저에서 읽을 수 있는 프로퍼티
    public int CurrentHealth => _currentHealth;
    public int MaxHealth => maxHealth;
    public int BaseAttackDamage => baseAttackDamage;
    public float MoveSpeed => moveSpeed;
    public float JumpForce => jumpForce;

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
    private float _dashDirection = 1f;
    private Coroutine _attackCoroutine;
    private Coroutine _parryCoroutine;
    private Coroutine _parryTextCoroutine;

    void Start()
    {
        if (savedAttack != -1) baseAttackDamage = savedAttack;
        if (savedMaxHp != -1) maxHealth = savedMaxHp;
        if (savedSpeed != -1f) moveSpeed = savedSpeed;
        if (savedJump != -1f) jumpForce = savedJump;

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

        if (parryTextUI != null)
        {
            parryTextUI.SetActive(false);
        }

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(_currentHealth, maxHealth);
        }
    }

    void Update()
    {
        if (_isDead) return;

        if (_parryTimer > 0f)
        {
            _parryTimer -= Time.deltaTime;
        }

        if (groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapBox(groundCheck.position, _boxSize, 0f, groundLayer);
        }

        if (!_wasGrounded && _isGrounded)
        {
            OnLanded();
        }
        _wasGrounded = _isGrounded;

        // 1. 대시/회피 (LeftShift)
        if (Input.GetKeyDown(KeyCode.LeftShift) && _canDash && !_isKnockedBack)
        {
            float currentInput = Input.GetAxisRaw("Horizontal");
            if (currentInput != 0)
            {
                _facingDirection = currentInput;
                transform.localScale = new Vector3(_facingDirection, 1f, 1f);
            }

            _dashDirection = _facingDirection;
            StartCoroutine(DashRoutine());
        }

        // 2. 공격 (Z키)
        if (Input.GetKeyDown(KeyCode.Z) && !_isKnockedBack && !_isDashing && !IsParrying)
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

        // 3. 패링 (F키 또는 마우스 우클릭 - 쿨타임 검사 추가)
        if ((Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1)) && _canParry && !_isKnockedBack && !_isDashing && !IsParrying)
        {
            TriggerParry();
        }

        // 4. 이동 조작
        if (!_isKnockedBack && !_isDashing && !IsParrying)
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

        if (_animator != null && !_isDashing && !_isAttacking && !_isKnockedBack && !IsParrying)
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

    private void TriggerParry()
    {
        _parryTimer = parryWindow;
        _canParry = false; // 패링 쿨타임 시작

        if (_isAttacking)
        {
            if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
            if (attackArea != null) attackArea.SetActive(false);
            _isAttacking = false;
            _comboStep = 0;
            _bufferedAttack2 = false;
        }

        if (_parryCoroutine != null) StopCoroutine(_parryCoroutine);
        _parryCoroutine = StartCoroutine(ParryActionRoutine());
        StartCoroutine(ParryCooldownRoutine());
    }

    private IEnumerator ParryActionRoutine()
    {
        if (_animator != null)
        {
            _animator.Play("Hero_Parry", 0, 0f);
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = parryColor;
        }

        yield return new WaitForSeconds(parryWindow);

        if (_spriteRenderer != null && !_isKnockedBack && !_isDashing && !_isDead)
        {
            _spriteRenderer.color = _originalColor;
        }

        if (!_isDead && !_isKnockedBack && !_isDashing && _animator != null)
        {
            if (_isGrounded)
            {
                _animator.Play(Mathf.Abs(_horizontalInput) > 0.01f ? "Hero_Run" : "Hero_Idle", 0, 0f);
            }
            else
            {
                _animator.Play("Hero_Fall", 0, 0f);
            }
        }
    }

    private IEnumerator ParryCooldownRoutine()
    {
        yield return new WaitForSeconds(parryCooldown);
        _canParry = true;
    }

    private void OnLanded()
    {
        if (_animator == null || _isKnockedBack || _isDead || IsParrying) return;

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

        if (_isDashing)
        {
            _rb.linearVelocity = new Vector2(_dashDirection * dashSpeed, 0f);
            return;
        }

        if (!_isKnockedBack)
        {
            if (IsParrying && _isGrounded)
            {
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            }
            else if (_isAttacking && _isGrounded)
            {
                if (_comboStep == 1) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
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

        if (_isGrounded) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        if (_animator != null)
        {
            _animator.Play("Hero_Attack1", -1, 0f);
            _animator.Update(0f);
        }

        if (attackArea != null) attackArea.SetActive(false);
        yield return null;
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
                _animator.Play("Hero_Attack2", -1, 0f);
                _animator.Update(0f);
            }

            if (attackArea != null) attackArea.SetActive(false);
            yield return null;
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

        if (_animator != null && !_isKnockedBack && !_isDashing && !IsParrying)
        {
            if (_isGrounded)
            {
                _animator.Play("Hero_Idle", -1, 0f);
                _animator.SetInteger("AnimState", 0);
            }
            else
            {
                _animator.Play("Hero_Fall", -1, 0f);
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

        if (_animator != null && !_isKnockedBack) _animator.Play("Hero_Fall", 0, 0f);
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
        return _comboStep == 2 ? (baseAttackDamage * 2) : baseAttackDamage;
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
        if (enemyLayerIndex != -1) Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, true);

        if (_spriteRenderer != null) _spriteRenderer.color = dashColor;
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
        if (_spriteRenderer != null) _spriteRenderer.color = _originalColor;
        if (_animator != null) _animator.speed = 1f;
        if (enemyLayerIndex != -1) Physics2D.IgnoreLayerCollision(playerLayer, enemyLayerIndex, false);

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
            ghostSR.color = new Color(initialColor.r, initialColor.g, initialColor.b, Mathf.Lerp(initialColor.a, 0f, timer / ghostFadeTime));
            yield return null;
        }
        Destroy(ghostObj);
    }

    public void TakeDamage(int damage, Vector2 knockbackDir)
    {
        if (_isDead || _isInvincible || _isDashing) return;

        if (IsParrying)
        {
            Debug.Log("<color=green>[Parry Success!] 완벽한 패링 성공! 대미지 0</color>");

            if (BattleStatsManager.Instance != null)
            {
                BattleStatsManager.Instance.AddParryCount();
            }

            if (parryTextUI != null)
            {
                if (_parryTextCoroutine != null) StopCoroutine(_parryTextCoroutine);
                float remainDuration = Mathf.Max(0.1f, _parryTimer);
                _parryTextCoroutine = StartCoroutine(ShowParryTextRoutine(remainDuration));
            }

            return;
        }

        _currentHealth -= damage;
        Debug.Log($"<color=red>[Player] 피격! 남은 체력: {_currentHealth}</color>");

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(_currentHealth, maxHealth);
        }

        if (_isAttacking)
        {
            if (_attackCoroutine != null) StopCoroutine(_attackCoroutine);
            if (attackArea != null) attackArea.SetActive(false);
            _isAttacking = false;
            _comboStep = 0;
            _bufferedAttack2 = false;
        }

        if (_currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(HitRoutine(knockbackDir));
        }
    }

    private IEnumerator HitRoutine(Vector2 knockbackDir)
    {
        _isKnockedBack = true;

        if (_animator != null)
        {
            _animator.Play("Hero_TakeHit", 0, 0f);
        }

        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(new Vector2(knockbackDir.x * knockbackForce, knockbackForce * 0.4f), ForceMode2D.Impulse);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = hitColor;
        }

        yield return new WaitForSeconds(flashDuration);

        if (!_isDead && _spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }

        float remainStun = Mathf.Max(0f, hitStunDuration - flashDuration);
        yield return new WaitForSeconds(remainStun);

        _isKnockedBack = false;

        if (!_isDead && _animator != null && !_isAttacking && !_isDashing && !IsParrying)
        {
            if (_isGrounded)
            {
                _animator.Play(Mathf.Abs(_horizontalInput) > 0.01f ? "Hero_Run" : "Hero_Idle", 0, 0f);
            }
        }
    }

    private void Die()
    {
        _isDead = true;

        _rb.linearVelocity = Vector2.zero;
        _horizontalInput = 0f;

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(0, maxHealth);
        }

        StartCoroutine(DeathFlashRoutine());

        if (_animator != null)
        {
            _animator.Play("Hero_Death", 0, 0f);
        }

        Debug.Log("<color=red>[Player] 플레이어 사망!</color>");

        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.TriggerGameOver();
        }
    }

    private IEnumerator DeathFlashRoutine()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = hitColor;
        }

        yield return new WaitForSeconds(flashDuration);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
    }

    private IEnumerator ShowParryTextRoutine(float duration)
    {
        if (parryTextUI != null)
        {
            // 플레이어가 좌측/우측 어느 방향을 보든 텍스트의 X 스케일은 정방향 유지
            Vector3 currentScale = parryTextUI.transform.localScale;
            float parentDirection = transform.localScale.x >= 0 ? 1f : -1f;
            parryTextUI.transform.localScale = new Vector3(Mathf.Abs(currentScale.x) * parentDirection, currentScale.y, currentScale.z);

            parryTextUI.SetActive(true);
            yield return new WaitForSeconds(duration);
            parryTextUI.SetActive(false);
        }
    }

    public void ApplySingleBuff(StatType statType, float delta)
    {
        switch (statType)
        {
            case StatType.Attack:
                baseAttackDamage = Mathf.Max(1, baseAttackDamage + (int)delta);
                savedAttack = baseAttackDamage;
                Debug.Log($"<color=cyan>[버프 적용] 실제 공격력: {baseAttackDamage}</color>");
                break;

            case StatType.Health:
                maxHealth = Mathf.Max(1, maxHealth + (int)delta);
                savedMaxHp = maxHealth;
                _currentHealth = Mathf.Max(1, Mathf.Min(_currentHealth + (int)delta, maxHealth));
                if (playerUI != null)
                {
                    playerUI.UpdateHPBar(_currentHealth, maxHealth);
                }
                Debug.Log($"<color=cyan>[버프 적용] 실제 최대 체력: {maxHealth} / 현재 체력: {_currentHealth}</color>");
                break;

            case StatType.Speed:
                moveSpeed = Mathf.Max(1f, moveSpeed + delta);
                savedSpeed = moveSpeed;
                Debug.Log($"<color=cyan>[버프 적용] 실제 이동 속도: {moveSpeed:F1}</color>");
                break;

            case StatType.JumpPower:
                jumpForce = Mathf.Max(1f, jumpForce + delta);
                savedJump = jumpForce;
                Debug.Log($"<color=cyan>[버프 적용] 실제 점프력: {jumpForce:F1}</color>");
                break;
        }
    }
}