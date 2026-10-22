using System.Collections;
using UnityEngine;

// partial — 기능별 추가 구현이 Assets/ReversalTower/Player/ 아래에 나뉘어 있다.
//   PlayerMovement.Locomotion.cs  코요테 타임 · 점프 버퍼 · 가변 점프
//   PlayerMovement.DashStack.cs   스택형 대시 (2스택 / 충전 2.5s / 무적 0.15s)
//   PlayerMovement.HitRules.cs    패링 가능 · 회피 불가 판정
public partial class PlayerMovement : MonoBehaviour
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
    // dashCooldown 제거됨 — 스택형 대시로 대체.
    // 충전 시간은 PlayerMovement.DashStack.cs의 dashStackRechargeTime(기본 2.5초)을 쓴다.
    [SerializeField]
    private Color dashColor =
        new Color(0.4f, 0.4f, 0.4f, 1f);
    [SerializeField] private float dashAnimationFrame = 0.2f;

    [Header("잔상(Ghost) 세팅")]
    [SerializeField]
    private Color ghostColor =
        new Color(0.3f, 0.6f, 1f, 0.6f);
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

    // =========================================================
    // Q SKILL
    // =========================================================

    [Header("Q 스킬 - 돌진 베기")]
    [SerializeField] private float qCooldown = 6f;
    [SerializeField] private float qDamageMultiplier = 1.8f;
    [SerializeField] private float qWindup = 0.2f;
    [SerializeField] private float qTotalDuration = 0.7f;
    [SerializeField] private float qDashDistance = 3f;

    [SerializeField]
    private Vector2 qAttackSize =
        new Vector2(3f, 1.5f);

    [SerializeField]
    private Vector2 qAttackOffset =
        new Vector2(1.5f, 0f);

    [SerializeField] private LayerMask qEnemyLayer;

    [Header("Q 스킬 - 에셋 연결")]
    [SerializeField] private GameObject qVfxPrefab;
    [SerializeField] private string qAnimationStateName = "";

    [Header("Q 스킬 - 임시 범위 표시")]
    [SerializeField]
    private Color qDebugColor =
        new Color(1f, 0.2f, 0.2f, 0.35f);
    [SerializeField] private float qDebugDuration = 0.2f;

    // =========================================================
    // W SKILL
    // =========================================================

    [Header("W 스킬 - 회전 베기")]
    [SerializeField] private float wCooldown = 10f;
    [SerializeField] private float wDamageMultiplier = 1.2f;
    [SerializeField] private float wRadius = 2.5f;
    [SerializeField] private float wWindup = 0.35f;
    [SerializeField] private float wTotalDuration = 0.9f;

    [Header("W 스킬 - 에셋 연결")]
    [SerializeField] private GameObject wVfxPrefab;
    [SerializeField] private string wAnimationStateName = "";

    [Header("W 스킬 - 임시 범위 표시")]
    [SerializeField]
    private Color wDebugColor =
        new Color(1f, 0.6f, 0.1f, 0.35f);
    [SerializeField] private float wDebugDuration = 0.25f;
    [SerializeField] private int wCircleSegments = 48;

    // =========================================================
    // E SKILL
    // =========================================================

    [Header("E 스킬 - 처형 베기")]
    [SerializeField] private float eCooldown = 15f;
    [SerializeField] private float eDamageMultiplier = 3f;
    [SerializeField] private float eChargeTime = 0.7f;
    [SerializeField] private float eTotalDuration = 1.3f;

    [SerializeField]
    private Vector2 eAttackSize =
        new Vector2(2.5f, 1.8f);

    [SerializeField]
    private Vector2 eAttackOffset =
        new Vector2(1.25f, 0f);

    [Header("E 스킬 - 에셋 연결")]
    [SerializeField] private GameObject eVfxPrefab;
    [SerializeField] private string eAnimationStateName = "";

    [Header("E 스킬 - 임시 범위 표시")]
    [SerializeField]
    private Color eDebugColor =
        new Color(0.7f, 0.1f, 1f, 0.35f);

    [SerializeField]
    private Color eChargeDebugColor =
        new Color(0.7f, 0.1f, 1f, 0.15f);

    [SerializeField] private float eDebugDuration = 0.25f;

    // =========================================================
    // POTION
    // =========================================================

    [Header("포션 - 프로토타입")]
    [SerializeField] private KeyCode potionKey = KeyCode.G;

    [Tooltip("전투 시작 시 보유하는 포션 개수")]
    [SerializeField] private int maxPotionCount = 1;

    [Tooltip("포션 1회 사용 시 회복되는 HP")]
    [SerializeField] private int potionHealAmount = 1;

    [Tooltip("포션을 마시는 데 걸리는 시간")]
    [SerializeField] private float potionUseDuration = 0.9f;

    [Header("포션 - 에셋 연결")]
    [SerializeField] private GameObject potionVfxPrefab;
    [SerializeField] private string potionAnimationStateName = "";

    // =========================================================
    // PARRY
    // =========================================================

    [Header("패링(Parry) 세팅")]
    [SerializeField] private float parryWindow = 0.25f;
    [SerializeField] private float parryCooldown = 0.8f;
    [SerializeField] private Color parryColor = Color.cyan;
    [SerializeField] private GameObject parryTextUI;

    private float _parryTimer = 0f;
    private bool _canParry = true;

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

    // =========================================================
    // INTERNAL STATE
    // =========================================================

    private int _currentHealth;
    private float _horizontalInput;

    private bool _isJumpPressed;
    private bool _isGrounded;
    private bool _wasGrounded;

    private bool _isAttacking;
    private bool _isAttackCoolingDown;

    private bool _isDashing;
    // _canDash 제거됨 — 스택형 대시로 대체 (PlayerMovement.DashStack.cs의 CanDashNow)
    private bool _isInvincible;

    private bool _isKnockedBack;
    private bool _isDead;

    // 스킬 공통
    private bool _isUsingSkill;

    // Q
    private bool _canUseQ = true;
    private Coroutine _qCoroutine;

    // W
    private bool _canUseW = true;
    private Coroutine _wCoroutine;

    // E
    private bool _canUseE = true;
    private bool _isChargingE = false;
    private Coroutine _eCoroutine;
    private Coroutine _eChargeVisualCoroutine;

    private int _comboStep = 0;
    private bool _bufferedAttack2 = false;
    private bool _hasTurnedDuringStep2 = false;

    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Animator _animator;

    private Color _originalColor;

    private readonly Vector2 _boxSize =
        new Vector2(0.4f, 0.2f);

    private float _facingDirection = 1f;
    private float _dashDirection = 1f;

    private Coroutine _attackCoroutine;
    private Coroutine _parryCoroutine;
    private Coroutine _parryTextCoroutine;

    // 포션
    private int _currentPotionCount;
    private bool _isUsingPotion = false;
    private Coroutine _potionCoroutine;
    public int CurrentPotionCount => _currentPotionCount;
    public int MaxPotionCount => maxPotionCount;

    // =========================================================
    // HUD용 실시간 전투 정보
    // =========================================================

    private float _qCooldownRemaining = 0f;
    private float _wCooldownRemaining = 0f;
    private float _eCooldownRemaining = 0f;

    public float QCooldownRemaining => _qCooldownRemaining;
    public float WCooldownRemaining => _wCooldownRemaining;
    public float ECooldownRemaining => _eCooldownRemaining;

    public float QCooldown => qCooldown;
    public float WCooldown => wCooldown;
    public float ECooldown => eCooldown;

    public int PotionCount => _currentPotionCount;

    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        if (savedAttack != -1)
            baseAttackDamage = savedAttack;

        if (savedMaxHp != -1)
            maxHealth = savedMaxHp;

        if (savedSpeed != -1f)
            moveSpeed = savedSpeed;

        if (savedJump != -1f)
            jumpForce = savedJump;

        _currentHealth = maxHealth;

        _currentPotionCount = maxPotionCount;

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
            playerUI.UpdateHPBar(
                _currentHealth,
                maxHealth
            );
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    void Update()
    {
        if (_isDead)
            return;

        if (_parryTimer > 0f)
        {
            _parryTimer -= Time.deltaTime;
        }

        // Ground Check
        if (groundCheck != null)
        {
            _isGrounded = Physics2D.OverlapBox(
                groundCheck.position,
                _boxSize,
                0f,
                groundLayer
            );
        }

        if (!_wasGrounded && _isGrounded)
        {
            OnLanded();
        }

        _wasGrounded = _isGrounded;

        // -----------------------------------------------------
        // 0. 기동 보조 · 대시 스택 (전투방식설계서 2.2 / 2.3)
        //    구현은 PlayerMovement.Locomotion.cs / .DashStack.cs
        // -----------------------------------------------------

        TickDashStacks();
        TickJumpAssist();

        // -----------------------------------------------------
        // 1. 대시 / 회피
        // -----------------------------------------------------

        if (Input.GetKeyDown(KeyCode.LeftShift)
            && CanDashNow
            && !_isKnockedBack)
        {
            float currentInput =
                Input.GetAxisRaw("Horizontal");

            if (currentInput != 0)
            {
                _facingDirection = currentInput;

                transform.localScale =
                    new Vector3(
                        _facingDirection,
                        1f,
                        1f
                    );
            }

            _dashDirection = _facingDirection;
            // 스킬 사용 중 대시하면 현재 스킬 취소
            if (_isUsingSkill)
            {
                CancelCurrentSkillByDash();
            }
            if (_isUsingPotion)
            {
                CancelPotionByDash();
            }

            StartCoroutine(DashRoutine());
        }

        // -----------------------------------------------------
        // 2. 기본 공격
        // -----------------------------------------------------

        if (Input.GetKeyDown(KeyCode.Z)
            && !_isKnockedBack
            && !_isDashing
            && !_isUsingSkill
            && !IsParrying)
        {
            if (!_isAttacking
                && !_isAttackCoolingDown)
            {
                _comboStep = 1;
                _bufferedAttack2 = false;
                _hasTurnedDuringStep2 = false;

                if (_attackCoroutine != null)
                {
                    StopCoroutine(_attackCoroutine);
                }

                _attackCoroutine =
                    StartCoroutine(ComboRoutine());
            }
            else if (_isAttacking
                && _comboStep == 1
                && _isGrounded)
            {
                _bufferedAttack2 = true;
            }
        }

        // -----------------------------------------------------
        // 3. 패링
        // -----------------------------------------------------

        if ((Input.GetKeyDown(KeyCode.F)
            || Input.GetMouseButtonDown(1))
            && _canParry
            && !_isKnockedBack
            && !_isDashing
            && !_isUsingSkill
            && !IsParrying)
        {
            TriggerParry();
        }

        // -----------------------------------------------------
        // 4. Q - 돌진 베기
        // 지상에서만 사용 가능
        // -----------------------------------------------------

        if (Input.GetKeyDown(KeyCode.Q)
            && _canUseQ
            && _isGrounded
            && !_isUsingSkill
            && !_isKnockedBack
            && !_isDashing
            && !_isAttacking
            && !IsParrying)
        {
            _qCoroutine =
                StartCoroutine(QSkillRoutine());
        }
        // -----------------------------------------------------
        // 5. W - 회전 베기
        // -----------------------------------------------------

        if (Input.GetKeyDown(KeyCode.W)
            && _canUseW
            && !_isUsingSkill
            && !_isKnockedBack
            && !_isDashing
            && !_isAttacking
            && !IsParrying)
        {
            _wCoroutine =
                StartCoroutine(WSkillRoutine());
        }

        // -----------------------------------------------------
        // 6. E - 처형 베기
        // 지상에서만 사용 가능
        // -----------------------------------------------------

        if (Input.GetKeyDown(KeyCode.E)
            && _canUseE
            && _isGrounded
            && !_isUsingSkill
            && !_isKnockedBack
            && !_isDashing
            && !_isAttacking
            && !IsParrying)
        {
            _eCoroutine =
                StartCoroutine(ESkillRoutine());
        }

        // -----------------------------------------------------
        // POTION
        // -----------------------------------------------------

        if (Input.GetKeyDown(potionKey)
            && !_isUsingPotion
            && !_isUsingSkill
            && !_isKnockedBack
            && !_isDashing
            && !_isAttacking
            && !IsParrying
            && _currentPotionCount > 0
            && _currentHealth < maxHealth)
        {
            _potionCoroutine =
                StartCoroutine(PotionRoutine());
        }

        // -----------------------------------------------------
        // 7. 이동
        // -----------------------------------------------------

        if (!_isKnockedBack
              && !_isDashing
              && !_isUsingSkill
              && !_isUsingPotion
              && !IsParrying)
        {
            float rawInput =
                Input.GetAxisRaw("Horizontal");

            if (rawInput != 0)
            {
                if (_isAttacking && !_isGrounded)
                {
                    if (rawInput != _facingDirection)
                    {
                        _facingDirection = rawInput;

                        transform.localScale =
                            new Vector3(
                                _facingDirection,
                                1f,
                                1f
                            );

                        CancelAirAttackMotion();
                    }
                }
                else if (_isAttacking
                    && _isGrounded
                    && _comboStep == 2)
                {
                    if (!_hasTurnedDuringStep2
                        && rawInput != _facingDirection)
                    {
                        _facingDirection = rawInput;

                        transform.localScale =
                            new Vector3(
                                _facingDirection,
                                1f,
                                1f
                            );

                        _hasTurnedDuringStep2 = true;

                        if (Mathf.Abs(
                            _rb.linearVelocity.x) > 0.1f)
                        {
                            _rb.linearVelocity =
                                new Vector2(
                                    _facingDirection
                                    * step2DashForce,
                                    _rb.linearVelocity.y
                                );
                        }
                    }
                }
                else if (!_isAttacking)
                {
                    _facingDirection = rawInput;

                    transform.localScale =
                        new Vector3(
                            _facingDirection,
                            1f,
                            1f
                        );
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

            // 점프 입력은 TickJumpAssist()가 처리한다.
            // (PlayerMovement.Locomotion.cs — 코요테 타임 · 점프 버퍼 · 가변 점프)
            //
            // 기존에는 여기서 `GetKeyDown(Space) && _isGrounded`로 바로 받았는데,
            // 발판 끝에서 한 프레임만 늦어도 점프가 씹히는 문제가 있었다.
        }

        // -----------------------------------------------------
        // 기본 애니메이션
        // -----------------------------------------------------

        if (_animator != null
            && !_isDashing
            && !_isAttacking
            && !_isKnockedBack
            && !_isUsingSkill
            && !IsParrying)
        {
            bool isMoving =
                Mathf.Abs(_horizontalInput) > 0.01f;

            _animator.SetInteger(
                "AnimState",
                isMoving ? 1 : 0
            );

            _animator.SetBool(
                "IsGrounded",
                _isGrounded
            );

            if (!_isGrounded
                && _rb.linearVelocity.y < -0.1f)
            {
                _animator.Play("Hero_Fall");
            }
        }
    }

    // =========================================================
    // FIXED UPDATE
    // =========================================================

    void FixedUpdate()
    {
        if (_isDead)
            return;

        // Q/W가 Rigidbody를 직접 제어하는 동안
        // 일반 이동이 덮어쓰지 않도록 막음
        if (_isUsingSkill || _isUsingPotion)
        {
            return;
        }

        if (_isDashing)
        {
            _rb.linearVelocity =
                new Vector2(
                    _dashDirection * dashSpeed,
                    0f
                );

            return;
        }

        if (!_isKnockedBack)
        {
            if (IsParrying && _isGrounded)
            {
                _rb.linearVelocity =
                    new Vector2(
                        0f,
                        _rb.linearVelocity.y
                    );
            }
            else if (_isAttacking
                && _isGrounded)
            {
                if (_comboStep == 1)
                {
                    _rb.linearVelocity =
                        new Vector2(
                            0f,
                            _rb.linearVelocity.y
                        );
                }
            }
            else
            {
                _rb.linearVelocity =
                    new Vector2(
                        _horizontalInput * moveSpeed,
                        _rb.linearVelocity.y
                    );

                if (_isJumpPressed)
                {
                    _rb.linearVelocity =
                        new Vector2(
                            _rb.linearVelocity.x,
                            jumpForce
                        );

                    _isJumpPressed = false;
                }
            }
        }
    }

    // =========================================================
    // Q SKILL
    // =========================================================

    private IEnumerator QSkillRoutine()
    {
        _isUsingSkill = true;
        _canUseQ = false;

        _horizontalInput = 0f;

        _rb.linearVelocity =
            new Vector2(
                0f,
                _rb.linearVelocity.y
            );

        if (_animator != null
            && !string.IsNullOrEmpty(
                qAnimationStateName))
        {
            _animator.Play(
                qAnimationStateName,
                0,
                0f
            );
        }

        // 선딜
        yield return new WaitForSeconds(qWindup);

        // 전방 돌진
        float dashTime =
            Mathf.Max(
                0.01f,
                qTotalDuration - qWindup
            );

        float startX = _rb.position.x;

        float targetX =
            startX
            + (_facingDirection
            * qDashDistance);

        float elapsed = 0f;

        while (elapsed < dashTime)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / dashTime
                );

            Vector2 position =
                _rb.position;

            position.x =
                Mathf.Lerp(
                    startX,
                    targetX,
                    t
                );

            _rb.MovePosition(position);

            yield return null;
        }

        ExecuteQAttack();

        // 정식 VFX 연결용
        if (qVfxPrefab != null)
        {
            Vector3 spawnPosition =
                transform.position
                + new Vector3(
                    qAttackOffset.x
                    * _facingDirection,
                    qAttackOffset.y,
                    0f
                );

            GameObject vfx =
                Instantiate(
                    qVfxPrefab,
                    spawnPosition,
                    Quaternion.identity
                );

            Vector3 scale =
                vfx.transform.localScale;

            scale.x *= _facingDirection;

            vfx.transform.localScale =
                scale;
        }

        _isUsingSkill = false;
        _qCoroutine = null;

        StartCoroutine(
            QCooldownRoutine()
        );
    }

    private void ExecuteQAttack()
    {
        Vector2 center =
            (Vector2)transform.position
            + new Vector2(
                qAttackOffset.x
                * _facingDirection,
                qAttackOffset.y
            );

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                qAttackSize,
                0f,
                qEnemyLayer
            );

        int damage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    baseAttackDamage
                    * qDamageMultiplier
                )
            );

        foreach (Collider2D hit in hits)
        {
            DummyBoss boss =
                hit.GetComponentInParent<DummyBoss>();

            if (boss != null)
            {
                boss.TakeDamage(damage);

                Debug.Log(
                    $"<color=yellow>" +
                    $"[Q 돌진 베기] " +
                    $"{damage} 대미지!" +
                    $"</color>"
                );

                break;
            }
        }

        StartCoroutine(
            ShowSkillDebugBox(
                center,
                qAttackSize,
                qDebugColor,
                qDebugDuration
            )
        );
    }

    private IEnumerator QCooldownRoutine()
    {
        _qCooldownRemaining = qCooldown;

        Debug.Log(
            $"<color=cyan>[Q] 쿨타임 시작 : {qCooldown}초</color>"
        );

        while (_qCooldownRemaining > 0f)
        {
            _qCooldownRemaining -= Time.deltaTime;

            if (_qCooldownRemaining < 0f)
                _qCooldownRemaining = 0f;

            yield return null;
        }

        _canUseQ = true;

        Debug.Log(
            "<color=green>[Q] 사용 가능</color>"
        );
    }

    // =========================================================
    // W SKILL
    // =========================================================

    private IEnumerator WSkillRoutine()
    {
        _isUsingSkill = true;
        _canUseW = false;

        _horizontalInput = 0f;

        _rb.linearVelocity =
            new Vector2(
                0f,
                _rb.linearVelocity.y
            );

        // 나중에 정식 애니메이션 연결
        if (_animator != null
            && !string.IsNullOrEmpty(
                wAnimationStateName))
        {
            _animator.Play(
                wAnimationStateName,
                0,
                0f
            );
        }

        // W 선딜
        yield return
            new WaitForSeconds(wWindup);

        ExecuteWAttack();

        // 정식 VFX 연결용
        if (wVfxPrefab != null)
        {
            Instantiate(
                wVfxPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        // 총 동작 시간이 0.9초가 되도록
        float remainTime =
            Mathf.Max(
                0f,
                wTotalDuration - wWindup
            );

        yield return
            new WaitForSeconds(remainTime);

        _isUsingSkill = false;
        _wCoroutine = null;

        StartCoroutine(
            WCooldownRoutine()
        );
    }

    private void ExecuteWAttack()
    {
        Vector2 center =
            transform.position;

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                center,
                wRadius,
                qEnemyLayer
            );

        int damage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    baseAttackDamage
                    * wDamageMultiplier
                )
            );

        // 같은 보스가 Collider를 여러 개 가지고 있어도
        // 한 번만 맞게 하기 위한 변수
        DummyBoss damagedBoss = null;

        foreach (Collider2D hit in hits)
        {
            DummyBoss boss =
                hit.GetComponentInParent<DummyBoss>();

            if (boss == null)
                continue;

            if (boss == damagedBoss)
                continue;

            boss.TakeDamage(damage);
            damagedBoss = boss;

            Debug.Log(
                $"<color=orange>" +
                $"[W 회전 베기] " +
                $"{damage} 대미지!" +
                $"</color>"
            );
        }

        StartCoroutine(
            ShowSkillDebugCircle(
                center,
                wRadius,
                wDebugColor,
                wDebugDuration
            )
        );
    }

private IEnumerator WCooldownRoutine()
{
    _wCooldownRemaining = wCooldown;

    Debug.Log(
        $"<color=cyan>[W] 쿨타임 시작 : {wCooldown}초</color>"
    );

    while (_wCooldownRemaining > 0f)
    {
        _wCooldownRemaining -= Time.deltaTime;

        if (_wCooldownRemaining < 0f)
            _wCooldownRemaining = 0f;

        yield return null;
    }

    _canUseW = true;

    Debug.Log(
        "<color=green>[W] 사용 가능</color>"
    );
}
    // =========================================================
    // E SKILL
    // =========================================================

    private IEnumerator ESkillRoutine()
    {
        _isUsingSkill = true;
        _isChargingE = true;
        _canUseE = false;

        _horizontalInput = 0f;

        // 차징 중 완전히 정지
        _rb.linearVelocity =
            new Vector2(
                0f,
                _rb.linearVelocity.y
            );

        // 나중에 정식 E 애니메이션 연결
        if (_animator != null
            && !string.IsNullOrEmpty(
                eAnimationStateName))
        {
            _animator.Play(
                eAnimationStateName,
                0,
                0f
            );
        }

        // 차징 중 공격 예정 범위를 미리 표시
        Vector2 chargeCenter =
            (Vector2)transform.position
            + new Vector2(
                eAttackOffset.x
                * _facingDirection,
                eAttackOffset.y
            );

        _eChargeVisualCoroutine =
            StartCoroutine(
                ShowSkillDebugBox(
                    chargeCenter,
                    eAttackSize,
                    eChargeDebugColor,
                    eChargeTime
                )
            );

        Debug.Log(
            "<color=magenta>" +
            "[E 처형 베기] 차징 시작" +
            "</color>"
        );

        // 0.7초 차징
        yield return
            new WaitForSeconds(
                eChargeTime
            );

        // 여기까지 왔으면 차징 성공
        _isChargingE = false;

        ExecuteEAttack();

        // 나중에 정식 VFX 연결
        if (eVfxPrefab != null)
        {
            Vector3 spawnPosition =
                transform.position
                + new Vector3(
                    eAttackOffset.x
                    * _facingDirection,
                    eAttackOffset.y,
                    0f
                );

            GameObject vfx =
                Instantiate(
                    eVfxPrefab,
                    spawnPosition,
                    Quaternion.identity
                );

            Vector3 scale =
                vfx.transform.localScale;

            scale.x *= _facingDirection;

            vfx.transform.localScale =
                scale;
        }

        // 전체 동작시간 1.3초
        float remainTime =
            Mathf.Max(
                0f,
                eTotalDuration
                - eChargeTime
            );

        yield return
            new WaitForSeconds(
                remainTime
            );

        _isUsingSkill = false;
        _eCoroutine = null;

        StartCoroutine(
            ECooldownRoutine(
                eCooldown
            )
        );
    }

    private void ExecuteEAttack()
    {
        Vector2 center =
            (Vector2)transform.position
            + new Vector2(
                eAttackOffset.x
                * _facingDirection,
                eAttackOffset.y
            );

        Collider2D[] hits =
            Physics2D.OverlapBoxAll(
                center,
                eAttackSize,
                0f,
                qEnemyLayer
            );

        int damage =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    baseAttackDamage
                    * eDamageMultiplier
                )
            );

        DummyBoss damagedBoss = null;

        foreach (Collider2D hit in hits)
        {
            DummyBoss boss =
                hit.GetComponentInParent<DummyBoss>();

            if (boss == null)
                continue;

            if (boss == damagedBoss)
                continue;

            boss.TakeDamage(damage);
            damagedBoss = boss;

            Debug.Log(
                $"<color=magenta>" +
                $"[E 처형 베기] " +
                $"{damage} 대미지!" +
                $"</color>"
            );
        }

        StartCoroutine(
            ShowSkillDebugBox(
                center,
                eAttackSize,
                eDebugColor,
                eDebugDuration
            )
        );
    }

    private IEnumerator ECooldownRoutine(float cooldown)
    {
        _eCooldownRemaining = cooldown;

        Debug.Log(
            $"<color=cyan>[E] 쿨타임 시작 : {cooldown}초</color>"
        );

        while (_eCooldownRemaining > 0f)
        {
            _eCooldownRemaining -= Time.deltaTime;

            if (_eCooldownRemaining < 0f)
                _eCooldownRemaining = 0f;

            yield return null;
        }

        _canUseE = true;

        Debug.Log(
            "<color=green>[E] 사용 가능</color>"
        );
    }

    private void CancelEByHit()
    {
        if (!_isChargingE)
            return;

        _isChargingE = false;

        if (_eCoroutine != null)
        {
            StopCoroutine(
                _eCoroutine
            );

            _eCoroutine = null;
        }

        _isUsingSkill = false;

        // 기획상 차징 중 피격 시
        // 전체 쿨타임의 절반만 적용
        float halfCooldown =
            eCooldown * 0.5f;

        Debug.Log(
            $"<color=orange>" +
            $"[E 처형 베기] " +
            $"차징 중 피격으로 취소! " +
            $"쿨타임 {halfCooldown}초" +
            $"</color>"
        );

        StartCoroutine(
            ECooldownRoutine(
                halfCooldown
            )
        );
    }

    private void CancelCurrentSkillByDash()
    {
        if (!_isUsingSkill)
            return;

        // =========================
        // Q 취소
        // =========================
        if (_qCoroutine != null)
        {
            StopCoroutine(_qCoroutine);
            _qCoroutine = null;

            // Q는 이미 사용했으므로 정상 쿨타임 적용
            StartCoroutine(QCooldownRoutine());

            Debug.Log(
                "<color=orange>" +
                "[Q] 대시로 스킬 취소" +
                "</color>"
            );
        }

        // =========================
        // W 취소
        // =========================
        if (_wCoroutine != null)
        {
            StopCoroutine(_wCoroutine);
            _wCoroutine = null;

            // W도 정상 쿨타임 적용
            StartCoroutine(WCooldownRoutine());

            Debug.Log(
                "<color=orange>" +
                "[W] 대시로 스킬 취소" +
                "</color>"
            );
        }

        // =========================
        // E 취소
        // =========================
        if (_eCoroutine != null)
        {
            StopCoroutine(_eCoroutine);
            _eCoroutine = null;

            _isChargingE = false;

            // 대시 취소는 반쿨이 아니라 정상 15초 쿨
            StartCoroutine(
                ECooldownRoutine(eCooldown)
            );

            Debug.Log(
                "<color=orange>" +
                "[E] 대시로 스킬 취소" +
                "</color>"
            );
        }

        _isUsingSkill = false;
    }

    // =========================================================
    // POTION
    // =========================================================

    private IEnumerator PotionRoutine()
    {
        _isUsingPotion = true;

        // 사용을 시작하는 순간 포션 소모
        _currentPotionCount--;

        Debug.Log(
            $"<color=lime>" +
            $"[Potion] 사용 시작 / " +
            $"남은 포션: {_currentPotionCount}" +
            $"</color>"
        );

        // 사용 중 이동 정지
        _horizontalInput = 0f;

        _rb.linearVelocity =
            new Vector2(
                0f,
                _rb.linearVelocity.y
            );

        // 나중에 실제 포션 애니메이션 연결
        if (_animator != null
            && !string.IsNullOrEmpty(
                potionAnimationStateName))
        {
            _animator.Play(
                potionAnimationStateName,
                0,
                0f
            );
        }

        // 0.9초 사용 시간
        yield return
            new WaitForSeconds(
                potionUseDuration
            );

        // 사용 완료
        int beforeHealth =
            _currentHealth;

        _currentHealth =
            Mathf.Min(
                maxHealth,
                _currentHealth
                + potionHealAmount
            );

        int actualHeal =
            _currentHealth
            - beforeHealth;

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(
                _currentHealth,
                maxHealth
            );
        }

        // 나중에 실제 회복 VFX 연결
        if (potionVfxPrefab != null)
        {
            Instantiate(
                potionVfxPrefab,
                transform.position,
                Quaternion.identity
            );
        }

        Debug.Log(
            $"<color=green>" +
            $"[Potion] 회복 완료! " +
            $"+{actualHeal} HP / " +
            $"현재 HP: {_currentHealth}/{maxHealth}" +
            $"</color>"
        );

        _isUsingPotion = false;
        _potionCoroutine = null;
    }

    private void CancelPotionByDash()
    {
        if (!_isUsingPotion)
            return;

        if (_potionCoroutine != null)
        {
            StopCoroutine(
                _potionCoroutine
            );

            _potionCoroutine = null;
        }

        _isUsingPotion = false;

        Debug.Log(
            "<color=orange>" +
            "[Potion] 대시로 사용 취소! " +
            "포션은 이미 소모되었으며 회복되지 않습니다." +
            "</color>"
        );
    }

    // =========================================================
    // DEBUG SKILL VISUALS
    // =========================================================

    private IEnumerator ShowSkillDebugBox(
        Vector2 center,
        Vector2 size,
        Color color,
        float duration)
    {
        GameObject visual =
            new GameObject(
                "QSkillRange_Debug"
            );

        visual.transform.position =
            new Vector3(
                center.x,
                center.y,
                0f
            );

        visual.transform.localScale =
            new Vector3(
                size.x,
                size.y,
                1f
            );

        SpriteRenderer sr =
            visual.AddComponent<SpriteRenderer>();

        Texture2D texture =
            Texture2D.whiteTexture;

        sr.sprite =
            Sprite.Create(
                texture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f
            );

        sr.color = color;

        if (_spriteRenderer != null)
        {
            sr.sortingLayerID =
                _spriteRenderer.sortingLayerID;

            sr.sortingOrder =
                _spriteRenderer.sortingOrder + 5;
        }

        yield return
            new WaitForSeconds(duration);

        Destroy(visual);
    }

    private IEnumerator ShowSkillDebugCircle(
        Vector2 center,
        float radius,
        Color color,
        float duration)
    {
        GameObject visual =
            new GameObject(
                "WSkillRange_Debug"
            );

        visual.transform.position =
            new Vector3(
                center.x,
                center.y,
                0f
            );

        LineRenderer line =
            visual.AddComponent<LineRenderer>();

        int segments =
            Mathf.Max(
                12,
                wCircleSegments
            );

        line.positionCount =
            segments + 1;

        line.loop = true;
        line.useWorldSpace = false;

        line.startWidth = 0.08f;
        line.endWidth = 0.08f;

        line.startColor = color;
        line.endColor = color;

        Material lineMaterial =
            new Material(
                Shader.Find(
                    "Sprites/Default"
                )
            );

        line.material =
            lineMaterial;

        if (_spriteRenderer != null)
        {
            line.sortingLayerID =
                _spriteRenderer.sortingLayerID;

            line.sortingOrder =
                _spriteRenderer.sortingOrder + 5;
        }

        for (int i = 0;
            i <= segments;
            i++)
        {
            float angle =
                ((float)i / segments)
                * Mathf.PI
                * 2f;

            float x =
                Mathf.Cos(angle)
                * radius;

            float y =
                Mathf.Sin(angle)
                * radius;

            line.SetPosition(
                i,
                new Vector3(
                    x,
                    y,
                    0f
                )
            );
        }

        yield return
            new WaitForSeconds(duration);

        Destroy(lineMaterial);
        Destroy(visual);
    }

    // =========================================================
    // PARRY
    // =========================================================

    private void TriggerParry()
    {
        _parryTimer = parryWindow;
        _canParry = false;

        if (_isAttacking)
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(
                    _attackCoroutine
                );
            }

            if (attackArea != null)
            {
                attackArea.SetActive(false);
            }

            _isAttacking = false;
            _comboStep = 0;
            _bufferedAttack2 = false;
        }

        if (_parryCoroutine != null)
        {
            StopCoroutine(
                _parryCoroutine
            );
        }

        _parryCoroutine =
            StartCoroutine(
                ParryActionRoutine()
            );

        StartCoroutine(
            ParryCooldownRoutine()
        );
    }

    private IEnumerator ParryActionRoutine()
    {
        if (_animator != null)
        {
            _animator.Play(
                "Hero_Parry",
                0,
                0f
            );
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color =
                parryColor;
        }

        yield return
            new WaitForSeconds(parryWindow);

        if (_spriteRenderer != null
            && !_isKnockedBack
            && !_isDashing
            && !_isDead)
        {
            _spriteRenderer.color =
                _originalColor;
        }

        if (!_isDead
            && !_isKnockedBack
            && !_isDashing
            && _animator != null)
        {
            if (_isGrounded)
            {
                _animator.Play(
                    Mathf.Abs(
                        _horizontalInput
                    ) > 0.01f
                        ? "Hero_Run"
                        : "Hero_Idle",
                    0,
                    0f
                );
            }
            else
            {
                _animator.Play(
                    "Hero_Fall",
                    0,
                    0f
                );
            }
        }
    }

    private IEnumerator ParryCooldownRoutine()
    {
        yield return
            new WaitForSeconds(
                parryCooldown
            );

        _canParry = true;
    }

    // =========================================================
    // LANDING
    // =========================================================

    private void OnLanded()
    {
        if (_animator == null
            || _isKnockedBack
            || _isDead
            || _isUsingSkill
            || IsParrying)
        {
            return;
        }

        _animator.ResetTrigger("Jump");

        _animator.SetBool(
            "IsGrounded",
            true
        );

        if (!_isAttacking
            && !_isDashing)
        {
            if (Mathf.Abs(
                _horizontalInput) > 0.01f)
            {
                _animator.Play(
                    "Hero_Run",
                    0,
                    0f
                );

                _animator.SetInteger(
                    "AnimState",
                    1
                );
            }
            else
            {
                _animator.Play(
                    "Hero_Idle",
                    0,
                    0f
                );

                _animator.SetInteger(
                    "AnimState",
                    0
                );
            }
        }
    }

    // =========================================================
    // BASIC ATTACK
    // =========================================================

    private IEnumerator ComboRoutine()
    {
        _isAttacking = true;

        bool executedCombo2 = false;

        _comboStep = 1;
        _bufferedAttack2 = false;
        _hasTurnedDuringStep2 = false;

        if (_isGrounded)
        {
            _rb.linearVelocity =
                new Vector2(
                    0f,
                    _rb.linearVelocity.y
                );
        }

        if (_animator != null)
        {
            _animator.Play(
                "Hero_Attack1",
                -1,
                0f
            );

            _animator.Update(0f);
        }

        if (attackArea != null)
        {
            attackArea.SetActive(false);
        }

        yield return null;

        if (attackArea != null)
        {
            attackArea.SetActive(true);
        }

        yield return
            new WaitForSeconds(
                attack1Duration * 0.45f
            );

        if (attackArea != null)
        {
            attackArea.SetActive(false);
        }

        yield return
            new WaitForSeconds(
                attack1Duration * 0.55f
            );

        if (_isGrounded
            && _bufferedAttack2)
        {
            executedCombo2 = true;

            _comboStep = 2;
            _bufferedAttack2 = false;

            _rb.linearVelocity =
                new Vector2(
                    _facingDirection
                    * step2DashForce,
                    _rb.linearVelocity.y
                );

            if (_animator != null)
            {
                _animator.Play(
                    "Hero_Attack2",
                    -1,
                    0f
                );

                _animator.Update(0f);
            }

            if (attackArea != null)
            {
                attackArea.SetActive(false);
            }

            yield return null;

            if (attackArea != null)
            {
                attackArea.SetActive(true);
            }

            yield return
                new WaitForSeconds(
                    attack2Duration * 0.35f
                );

            _rb.linearVelocity =
                new Vector2(
                    0f,
                    _rb.linearVelocity.y
                );

            yield return
                new WaitForSeconds(
                    attack2Duration * 0.15f
                );

            if (attackArea != null)
            {
                attackArea.SetActive(false);
            }

            yield return
                new WaitForSeconds(
                    attack2Duration * 0.5f
                );
        }

        _comboStep = 0;
        _bufferedAttack2 = false;
        _hasTurnedDuringStep2 = false;
        _isAttacking = false;

        if (_animator != null
            && !_isKnockedBack
            && !_isDashing
            && !IsParrying)
        {
            if (_isGrounded)
            {
                _animator.Play(
                    "Hero_Idle",
                    -1,
                    0f
                );

                _animator.SetInteger(
                    "AnimState",
                    0
                );
            }
            else
            {
                _animator.Play(
                    "Hero_Fall",
                    -1,
                    0f
                );
            }
        }

        float coolTime =
            executedCombo2
                ? comboEndCooldown
                : singleAttackCooldown;

        StartCoroutine(
            AttackCooldownRoutine(
                coolTime
            )
        );
    }

    private void CancelAirAttackMotion()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(
                _attackCoroutine
            );
        }

        if (attackArea != null)
        {
            attackArea.SetActive(false);
        }

        _comboStep = 0;
        _bufferedAttack2 = false;
        _isAttacking = false;

        if (_animator != null
            && !_isKnockedBack)
        {
            _animator.Play(
                "Hero_Fall",
                0,
                0f
            );
        }

        StartCoroutine(
            AttackCooldownRoutine(
                singleAttackCooldown
            )
        );
    }

    private IEnumerator AttackCooldownRoutine(
        float duration)
    {
        _isAttackCoolingDown = true;

        yield return
            new WaitForSeconds(duration);

        _isAttackCoolingDown = false;
    }

    public int GetDamage()
    {
        return _comboStep == 2
            ? baseAttackDamage * 2
            : baseAttackDamage;
    }

    // =========================================================
    // DASH
    // =========================================================

    private IEnumerator DashRoutine()
    {
        // 스택 소모 + 무적 타이머 시작.
        // 구현은 PlayerMovement.DashStack.cs
        //   설계서 2.3: 스택 2회 / 충전 2.5s / 무적은 앞 0.15s만
        ConsumeDashStack();
        _isDashing = true;
        StartCoroutine(DashInvincibilityRoutine());

        if (_isAttacking)
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(
                    _attackCoroutine
                );
            }

            if (attackArea != null)
            {
                attackArea.SetActive(false);
            }

            _isAttacking = false;
            _comboStep = 0;
            _bufferedAttack2 = false;
            _hasTurnedDuringStep2 = false;
        }

        int playerLayer =
            gameObject.layer;

        int enemyLayerIndex =
            LayerMask.NameToLayer("Enemy");

        if (enemyLayerIndex != -1)
        {
            Physics2D.IgnoreLayerCollision(
                playerLayer,
                enemyLayerIndex,
                true
            );
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color =
                dashColor;
        }

        if (_animator != null)
        {
            _animator.SetInteger(
                "AnimState",
                1
            );

            _animator.Play(
                "Hero_Run",
                0,
                dashAnimationFrame
            );

            _animator.speed = 0f;
        }

        float originalGravity =
            _rb.gravityScale;

        _rb.gravityScale = 0f;

        StartCoroutine(
            SpawnGhostsRoutine()
        );

        yield return
            new WaitForSeconds(
                dashDuration
            );

        _rb.gravityScale =
            originalGravity;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color =
                _originalColor;
        }

        if (_animator != null)
        {
            _animator.speed = 1f;
        }

        if (enemyLayerIndex != -1)
        {
            Physics2D.IgnoreLayerCollision(
                playerLayer,
                enemyLayerIndex,
                false
            );
        }

        _isDashing = false;

        // 무적 해제는 DashInvincibilityRoutine이 0.15초 뒤에 따로 한다.
        // 쿨타임 대신 스택 충전(TickDashStacks)을 쓰므로 여기서 기다리지 않는다.
    }

    private IEnumerator SpawnGhostsRoutine()
    {
        while (_isDashing)
        {
            CreateGhost();

            yield return
                new WaitForSeconds(
                    ghostInterval
                );
        }
    }

    private void CreateGhost()
    {
        if (_spriteRenderer == null)
            return;

        GameObject ghostObj =
            new GameObject("DashGhost");

        ghostObj.transform.position =
            transform.position;

        ghostObj.transform.localScale =
            transform.localScale;

        SpriteRenderer ghostSR =
            ghostObj.AddComponent<SpriteRenderer>();

        ghostSR.sprite =
            _spriteRenderer.sprite;

        ghostSR.color =
            ghostColor;

        ghostSR.sortingLayerID =
            _spriteRenderer.sortingLayerID;

        ghostSR.sortingOrder =
            _spriteRenderer.sortingOrder - 1;

        StartCoroutine(
            FadeAndDestroyGhost(
                ghostObj,
                ghostSR
            )
        );
    }

    private IEnumerator FadeAndDestroyGhost(
        GameObject ghostObj,
        SpriteRenderer ghostSR)
    {
        float timer = 0f;

        Color initialColor =
            ghostSR.color;

        while (timer < ghostFadeTime)
        {
            timer += Time.deltaTime;

            ghostSR.color =
                new Color(
                    initialColor.r,
                    initialColor.g,
                    initialColor.b,
                    Mathf.Lerp(
                        initialColor.a,
                        0f,
                        timer / ghostFadeTime
                    )
                );

            yield return null;
        }

        Destroy(ghostObj);
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public void TakeDamage(
        int damage,
        Vector2 knockbackDir)
    {
        if (_isDead)
        {
            return;
        }

        // 무적 판정 — 회피 불가(IgnoresDodge) 공격은 무적을 뚫는다.
        // 구현은 PlayerMovement.HitRules.cs
        if (IsHitBlockedByInvincibility)
        {
            return;
        }

        // 패링 판정 — 패링 불가 패턴은 패링 중이어도 막히지 않는다.
        if (IsHitParried)
        {
            Debug.Log(
                "<color=green>" +
                "[Parry Success!] " +
                "완벽한 패링 성공! " +
                "대미지 0" +
                "</color>"
            );

            if (BattleStatsManager.Instance
                != null)
            {
                BattleStatsManager.Instance
                    .AddParryCount();
            }

            if (parryTextUI != null)
            {
                if (_parryTextCoroutine != null)
                {
                    StopCoroutine(
                        _parryTextCoroutine
                    );
                }

                float remainDuration =
                    Mathf.Max(
                        0.1f,
                        _parryTimer
                    );

                _parryTextCoroutine =
                    StartCoroutine(
                        ShowParryTextRoutine(
                            remainDuration
                        )
                    );
            }

            return;
        }

        // E 차징 중 피격되면 처형 베기 취소
        if (_isChargingE)
        {
            CancelEByHit();
        }

        _currentHealth -= damage;

        Debug.Log(
            $"<color=red>" +
            $"[Player] 피격! " +
            $"남은 체력: " +
            $"{_currentHealth}" +
            $"</color>"
        );

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(
                _currentHealth,
                maxHealth
            );
        }

        if (_isAttacking)
        {
            if (_attackCoroutine != null)
            {
                StopCoroutine(
                    _attackCoroutine
                );
            }

            if (attackArea != null)
            {
                attackArea.SetActive(false);
            }

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
            StartCoroutine(
                HitRoutine(
                    knockbackDir
                )
            );
        }
    }

    private IEnumerator HitRoutine(
        Vector2 knockbackDir)
    {
        _isKnockedBack = true;

        if (_animator != null)
        {
            _animator.Play(
                "Hero_TakeHit",
                0,
                0f
            );
        }

        _rb.linearVelocity =
            Vector2.zero;

        _rb.AddForce(
            new Vector2(
                knockbackDir.x
                * knockbackForce,
                knockbackForce * 0.4f
            ),
            ForceMode2D.Impulse
        );

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color =
                hitColor;
        }

        yield return
            new WaitForSeconds(
                flashDuration
            );

        if (!_isDead
            && _spriteRenderer != null)
        {
            _spriteRenderer.color =
                _originalColor;
        }

        float remainStun =
            Mathf.Max(
                0f,
                hitStunDuration
                - flashDuration
            );

        yield return
            new WaitForSeconds(
                remainStun
            );

        _isKnockedBack = false;

        if (!_isDead
            && _animator != null
            && !_isAttacking
            && !_isDashing
            && !IsParrying)
        {
            if (_isGrounded)
            {
                _animator.Play(
                    Mathf.Abs(
                        _horizontalInput
                    ) > 0.01f
                        ? "Hero_Run"
                        : "Hero_Idle",
                    0,
                    0f
                );
            }
        }
    }

    // =========================================================
    // DEATH
    // =========================================================

    private void Die()
    {
        _isDead = true;

        _rb.linearVelocity =
            Vector2.zero;

        _horizontalInput = 0f;

        if (playerUI != null)
        {
            playerUI.UpdateHPBar(
                0,
                maxHealth
            );
        }

        StartCoroutine(
            DeathFlashRoutine()
        );

        if (_animator != null)
        {
            _animator.Play(
                "Hero_Death",
                0,
                0f
            );
        }

        Debug.Log(
            "<color=red>" +
            "[Player] 플레이어 사망!" +
            "</color>"
        );

        if (GameOverManager.Instance
            != null)
        {
            GameOverManager.Instance
                .TriggerGameOver();
        }
    }

    private IEnumerator DeathFlashRoutine()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color =
                hitColor;
        }

        yield return
            new WaitForSeconds(
                flashDuration
            );

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color =
                _originalColor;
        }
    }

    // =========================================================
    // PARRY TEXT
    // =========================================================

    private IEnumerator ShowParryTextRoutine(
        float duration)
    {
        if (parryTextUI != null)
        {
            Vector3 currentScale =
                parryTextUI.transform.localScale;

            float parentDirection =
                transform.localScale.x >= 0
                    ? 1f
                    : -1f;

            parryTextUI.transform.localScale =
                new Vector3(
                    Mathf.Abs(
                        currentScale.x
                    ) * parentDirection,
                    currentScale.y,
                    currentScale.z
                );

            parryTextUI.SetActive(true);

            yield return
                new WaitForSeconds(
                    duration
                );

            parryTextUI.SetActive(false);
        }
    }

    // =========================================================
    // BUFF
    // =========================================================

    public void ApplySingleBuff(
        StatType statType,
        float delta)
    {
        switch (statType)
        {
            case StatType.Attack:

                baseAttackDamage =
                    Mathf.Max(
                        1,
                        baseAttackDamage
                        + (int)delta
                    );

                savedAttack =
                    baseAttackDamage;

                Debug.Log(
                    $"<color=cyan>" +
                    $"[버프 적용] 실제 공격력: " +
                    $"{baseAttackDamage}" +
                    $"</color>"
                );

                break;

            case StatType.Health:

                maxHealth =
                    Mathf.Max(
                        1,
                        maxHealth
                        + (int)delta
                    );

                savedMaxHp =
                    maxHealth;

                _currentHealth =
                    Mathf.Max(
                        1,
                        Mathf.Min(
                            _currentHealth
                            + (int)delta,
                            maxHealth
                        )
                    );

                if (playerUI != null)
                {
                    playerUI.UpdateHPBar(
                        _currentHealth,
                        maxHealth
                    );
                }

                Debug.Log(
                    $"<color=cyan>" +
                    $"[버프 적용] 실제 최대 체력: " +
                    $"{maxHealth} / " +
                    $"현재 체력: " +
                    $"{_currentHealth}" +
                    $"</color>"
                );

                break;

            case StatType.Speed:

                moveSpeed =
                    Mathf.Max(
                        1f,
                        moveSpeed + delta
                    );

                savedSpeed =
                    moveSpeed;

                Debug.Log(
                    $"<color=cyan>" +
                    $"[버프 적용] 실제 이동 속도: " +
                    $"{moveSpeed:F1}" +
                    $"</color>"
                );

                break;

            case StatType.JumpPower:

                jumpForce =
                    Mathf.Max(
                        1f,
                        jumpForce + delta
                    );

                savedJump =
                    jumpForce;

                Debug.Log(
                    $"<color=cyan>" +
                    $"[버프 적용] 실제 점프력: " +
                    $"{jumpForce:F1}" +
                    $"</color>"
                );

                break;
        }
    }
}