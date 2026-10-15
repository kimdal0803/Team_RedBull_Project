using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 체력 조건으로 한 번 발동하는 전환. 페이즈 변경, 속도 배율 변경, 강제 패턴(전환 연출·기믹)을 묶는다.
    /// 예) 아디쿠스 60%: 페이즈 2, 속도 1.2, [맹목의 심판, 심판의 저울]
    ///     아디쿠스 30%: 페이즈 유지, [심판의 저울]
    /// </summary>
    [Serializable]
    public class PhaseTrigger
    {
        [Tooltip("보스 체력이 이 비율 이하가 되면 한 번 발동 (0.6 = 60%). 진행 중인 패턴이 끝난 직후 적용된다.")]
        [Range(0f, 1f)] public float hpRatio = 0.6f;
        [Tooltip("바꿀 페이즈 번호. 0이면 그대로.")]
        [Min(0)] public int setPhase = 2;
        [Tooltip("바꿀 속도 배율. 0이면 그대로. 예: 아디쿠스 2페이즈 1.2, 아라쿠스 분노 1.5")]
        [Min(0f)] public float setSpeedMultiplier = 0f;
        [Tooltip("순서대로 강제 실행할 패턴. 예: 맹목의 심판 → 심판의 저울")]
        public BossPatternData[] forcedPatterns;
    }

    /// <summary>
    /// 보스 한 마리를 움직이는 컴포넌트.
    /// 피벗은 보스 발밑 가운데로 둔다 (판정 박스와 거리를 발밑 기준으로 잰다).
    /// </summary>
    [DisallowMultipleComponent]
    public class BossController : MonoBehaviour
    {
        [Header("보스 기본 (전투방식설계서 2.1 기준)")]
        public string bossId = "Arakus";
        [Tooltip("보스 체력. 전투방식설계서 4.4의 게이지 계산 기준은 2000")]
        [Min(1)] public float maxHp = 2000f;
        [Tooltip("보스 공격력. 일반 공격 100% = 20 피해")]
        [Min(0)] public float attackPower = 20f;
        [Tooltip("보스 몸 폭(m). 판정 박스와 거리를 '보스 정면 끝'부터 잰다")]
        [Min(0.1f)] public float bodyWidth = 0.5f;
        [Tooltip("보스 키(m). 돌진 벽 검사 높이에 쓴다")]
        [Min(0.1f)] public float bodyHeight = 2.4f;
        [Tooltip("쓸 패턴이 없을 때 플레이어 쪽으로 걷는 속도(m/s)")]
        [Min(0)] public float moveSpeed = 2f;
        [Tooltip("이 거리(m) 안이면 더 다가가지 않고 기다린다")]
        [Min(0)] public float chaseStopDistance = 0.8f;
        [Tooltip("패턴 하나가 끝나고 다음 패턴을 고르기 전 대기(초). 아디쿠스 기획서 제안값 0.6")]
        [Min(0)] public float idleTime = 0.6f;

        [Header("대상")]
        [Tooltip("비워 두면 Player 태그로 찾는다")]
        public Transform target;
        [Tooltip("판정 박스가 찾을 레이어 (플레이어)")]
        public LayerMask targetMask;

        [Header("패턴")]
        public List<BossPatternData> patterns = new List<BossPatternData>();
        [Tooltip("체력이 높은 것부터 순서대로 적는다")]
        public PhaseTrigger[] phaseTriggers;
        [Tooltip("0이면 매번 다른 무작위. 숫자를 넣으면 같은 순서가 재현된다")]
        public int randomSeed = 0;

        [Header("판정")]
        [Tooltip("패링 신호를 판정 몇 초 전에 보낼지 (전투방식설계서 4.1: 0.3초)")]
        [Min(0)] public float parrySignalLead = 0.3f;
        [Tooltip("돌진이 멈출 맵 벽 레이어")]
        public LayerMask wallMask;
        [Tooltip("'TargetBehind' 조건: 플레이어가 등 뒤에 이 시간 이상 있으면 참 (아디쿠스 순간이동 1.5초)")]
        [Min(0)] public float behindConditionTime = 1.5f;

        [Header("연출 (선택)")]
        public Animator animator;
        [Tooltip("좌우 반전할 그림 오브젝트. 비우면 반전하지 않는다")]
        public Transform visualRoot;
        public string groggyAnimTrigger = "";
        public string deathAnimTrigger = "";

        [Header("디버그")]
        [Tooltip("씬 뷰에서 판정 박스를 미리 볼 패턴 (빨강 = 패링 불가, 노랑 = 패링 가능, 보라 = 회피 불가)")]
        public BossPatternData previewPattern;
        [Tooltip("씬 뷰에 그릴 플레이어 점프 높이 기준선 (전투방식설계서 2.1: 1.8m)")]
        public float referenceJumpHeight = 1.8f;
        public bool logToConsole = true;
        public bool showDebugOverlay = true;

        // ─────────────── 이벤트 (전투 데이터 수집기·연출·UI가 구독) ───────────────
        public event Action<PatternRun> PatternStarted;                     // 선딜 시작
        public event Action<PatternRun, HitEvent> ParrySignal;              // 패링 가능한 타의 판정 0.3초 전
        public event Action<PatternRun, HitEvent, HitResult> HitResolved;   // 타마다 결과 (Hit/Parried/Ignored/Missed)
        public event Action<PatternRun, bool> PatternEnded;                 // bool: 중간에 끊겼는지
        public event Action<PatternRun, bool> DashBlocked;                  // bool: 그로기 오브젝트에 막혔는지
        public event Action<int> PhaseChanged;
        public event Action<float, string> GroggyStarted;                   // 시간, 이유
        public event Action<float, string> Damaged;                         // 실제 피해, 원인
        public event Action Died;

        // ─────────────── 상태 ───────────────
        public BossStateMachine Fsm { get; private set; }
        public IdleState Idle { get; private set; }
        public TelegraphState Telegraph { get; private set; }
        public AttackState Attack { get; private set; }
        public RecoveryState Recovery { get; private set; }
        public GroggyState Groggy { get; private set; }
        public PhaseChangeState PhaseChange { get; private set; }
        public DeadState Dead { get; private set; }

        public float Hp { get; private set; }
        public float HpRatio => maxHp > 0f ? Hp / maxHp : 0f;
        public int Phase { get; private set; } = 1;
        public float SpeedMultiplier { get; private set; } = 1f;
        public int Facing { get; private set; } = 1;              // +1 오른쪽, -1 왼쪽
        public PatternRun CurrentRun { get; private set; }
        public float TargetBehindTime { get; private set; }
        public float BattleTime { get; private set; }
        public bool IsDead => Fsm != null && Fsm.Current == Dead;
        public float IdleTime => idleTime / SpeedMultiplier;
        public bool HasForcedPattern => forcedQueue.Count > 0;
        public bool HasPendingPhaseTrigger => pendingTriggers.Count > 0;

        readonly Dictionary<BossPatternData, float> cooldownUntil = new Dictionary<BossPatternData, float>();
        readonly Dictionary<string, PatternTuning> tunings = new Dictionary<string, PatternTuning>();
        readonly Dictionary<string, Func<bool>> conditions = new Dictionary<string, Func<bool>>();
        readonly Dictionary<PatternKind, PatternExecutor> executors = new Dictionary<PatternKind, PatternExecutor>();
        readonly Queue<BossPatternData> forcedQueue = new Queue<BossPatternData>();
        readonly List<int> pendingTriggers = new List<int>();
        readonly HashSet<int> firedTriggers = new HashSet<int>();
        PatternSelector selector;
        BossPatternData lastPattern;
        Collider2D targetCollider;
        int runCounter;
        float lastParrySignalTime = -99f;

        // ─────────────── 유니티 ───────────────

        void Awake()
        {
            Hp = maxHp;
            selector = new PatternSelector(randomSeed);

            Fsm = new BossStateMachine();
            Idle = new IdleState(this);
            Telegraph = new TelegraphState(this);
            Attack = new AttackState(this);
            Recovery = new RecoveryState(this);
            Groggy = new GroggyState(this);
            PhaseChange = new PhaseChangeState(this);
            Dead = new DeadState(this);

            RegisterExecutor(PatternKind.Melee, new MeleeExecutor());
            RegisterExecutor(PatternKind.Dash, new DashExecutor());
            RegisterExecutor(PatternKind.Projectile, new ProjectileExecutor());

            RegisterCondition("TargetBehind", () => TargetBehindTime >= behindConditionTime);

            Fsm.Changed += (from, to) =>
            {
                if (logToConsole)
                    Debug.Log($"[{bossId}] {(from != null ? from.Name : "-")} → {to.Name}   (t={BattleTime:0.00}s, HP {Hp:0}/{maxHp:0})", this);
            };
        }

        void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }
            if (target != null) targetCollider = target.GetComponentInChildren<Collider2D>();
            if (target == null) Debug.LogWarning($"[{bossId}] 대상(target)이 없습니다. Player 태그를 달거나 직접 지정하세요.", this);

            Fsm.Change(Idle);
        }

        void Update()
        {
            if (IsDead) return;
            float dt = Time.deltaTime;
            BattleTime += dt;
            UpdateBehindTimer(dt);
            Fsm.Tick(dt);
        }

        // ─────────────── 패턴 진행 (상태에서 부름) ───────────────

        public BossPatternData PickNextPattern()
        {
            while (forcedQueue.Count > 0)
            {
                var forced = forcedQueue.Dequeue();
                if (forced != null) return forced;
            }
            return selector.Pick(this, patterns, lastPattern);
        }

        public void BeginPattern(BossPatternData data)
        {
            if (data.faceTargetOnStart) FaceTarget();

            PatternTuning tuning = null;
            if (!string.IsNullOrEmpty(data.patternId)) tunings.TryGetValue(data.patternId, out tuning);
            CurrentRun = new PatternRun(data, tuning, attackPower, SpeedMultiplier, ++runCounter);
            cooldownUntil[data] = BattleTime + CurrentRun.Cooldown;
            lastPattern = data;

            if (logToConsole)
                Debug.Log($"[{bossId}] 패턴 시작: {data.displayName} ({data.patternId})  선딜 {CurrentRun.Telegraph:0.00}s · 판정 {CurrentRun.Hits.Count}타 · 공격 {CurrentRun.AttackDuration:0.00}s · 후딜 {CurrentRun.Recovery:0.00}s", this);
            PatternStarted?.Invoke(CurrentRun);
        }

        /// <summary>선딜·공격 단계에서 시간을 흘리고, 때가 된 패링 신호를 보낸다.</summary>
        public void AdvanceRunClock(float dt)
        {
            var run = CurrentRun;
            if (run == null) return;
            run.Elapsed += dt;

            while (run.nextSignalIndex < run.Hits.Count)
            {
                var e = run.Hits[run.nextSignalIndex];
                if (!e.Source.parryable)
                {
                    run.nextSignalIndex++;
                    continue;
                }
                float signalAt = run.Telegraph + e.Time - parrySignalLead;
                if (run.Elapsed < signalAt) break;
                run.nextSignalIndex++;
                lastParrySignalTime = Time.time;
                if (logToConsole) Debug.Log($"[{bossId}] 패링 신호 — {run.PatternId} {e.Index + 1}타 ({parrySignalLead:0.##}초 뒤 판정)", this);
                ParrySignal?.Invoke(run, e);
            }
        }

        /// <summary>
        /// 판정 박스 안의 대상에게 공격을 보낸다 (보스 몸 기준).
        /// 같은 판정으로 같은 대상은 한 번만 맞는다.
        /// </summary>
        /// <returns>대상과 한 번이라도 겹쳤으면 true</returns>
        public bool ResolveHit(PatternRun run, HitEvent e, HashSet<IBossAttackReceiver> touched)
        {
            return ResolveHitAt(run, e, GetHitboxRect(e.Source.box), touched);
        }

        /// <summary>
        /// 판정 박스를 직접 지정해서 공격을 보낸다.
        /// 날아가는 투사체처럼 박스가 보스 몸을 따라가지 않는 경우에 쓴다 (BossProjectile).
        /// 로그와 이벤트는 ResolveHit과 완전히 같아서, 전투 데이터 수집기 쪽은 바뀐 것을 몰라도 된다.
        /// </summary>
        public bool ResolveHitAt(PatternRun run, HitEvent e, Rect r, HashSet<IBossAttackReceiver> touched, bool fromProjectile = false)
        {
            var cols = Physics2D.OverlapBoxAll(r.center, r.size, 0f, targetMask);
            bool contact = false;

            foreach (var c in cols)
            {
                var receiver = c.GetComponentInParent<IBossAttackReceiver>();
                if (receiver == null) continue;
                contact = true;
                if (!touched.Add(receiver)) continue;

                var info = new BossHitInfo
                {
                    Source = this,
                    PatternId = run.PatternId,
                    HitIndex = e.Index,
                    Damage = e.Damage,
                    Knockback = e.Source.knockback,
                    Direction = c.bounds.center.x >= r.center.x ? 1 : -1,
                    Parryable = e.Source.parryable,
                    IgnoresDodge = e.Source.ignoresDodge,
                    FromProjectile = fromProjectile,
                };
                var result = receiver.ReceiveBossHit(info);

                if (logToConsole)
                {
                    string tag = fromProjectile ? " [투사체]" : "";
                    Debug.Log($"[{bossId}] {run.PatternId} {e.Index + 1}타{tag} → {result} ({e.Damage:0.#})", this);
                }
                HitResolved?.Invoke(run, e, result);

                if (result == HitResult.Parried)
                {
                    run.ParriedCount++;
                    if (e.Source.parryInterruptsPattern && e.Source.parryStaggerTime > 0f)
                    {
                        EnterGroggy(e.Source.parryStaggerTime, "패링");
                        return true;
                    }
                }
            }
            return contact;
        }

        /// <summary>판정이 끝날 때까지 아무도 맞지 않은 타 (= 플레이어가 피함)</summary>
        public void ReportMiss(PatternRun run, HitEvent e)
        {
            if (logToConsole) Debug.Log($"[{bossId}] {run.PatternId} {e.Index + 1}타 → Missed", this);
            HitResolved?.Invoke(run, e, HitResult.Missed);
        }

        /// <summary>공격 단계가 끝났을 때: 패링 가능한 타를 전부 패링했으면 그로기로 보낸다.</summary>
        public bool TryFullParryGroggy()
        {
            var run = CurrentRun;
            if (run == null || run.Data.fullParryGroggyTime <= 0f || !run.AllParried) return false;
            EndPattern(false);
            EnterGroggy(run.Data.fullParryGroggyTime, "전부 패링");
            return true;
        }

        public void EndPattern(bool interrupted)
        {
            var run = CurrentRun;
            if (run == null) return;
            CurrentRun = null;
            run.Interrupted |= interrupted;
            PatternEnded?.Invoke(run, interrupted);
        }

        /// <summary>그로기·경직. 진행 중인 패턴은 끊긴다.</summary>
        public void EnterGroggy(float duration, string reason)
        {
            if (IsDead) return;
            if (CurrentRun != null) CurrentRun.Interrupted = true;
            Groggy.Duration = duration;
            if (logToConsole) Debug.Log($"[{bossId}] 그로기 {duration:0.##}초 ({reason})", this);
            GroggyStarted?.Invoke(duration, reason);
            Fsm.Change(Groggy);
            EndPattern(true);
        }

        public void NotifyDashBlocked(PatternRun run, bool groggyObject)
        {
            if (logToConsole) Debug.Log($"[{bossId}] 돌진 멈춤 ({(groggyObject ? "기믹 오브젝트" : "벽")})", this);
            DashBlocked?.Invoke(run, groggyObject);
        }

        // ─────────────── 체력·페이즈 ───────────────

        /// <summary>플레이어 공격이 부른다. 패턴의 받는 피해 배율(무적·피해 감소)을 적용한다.</summary>
        /// <returns>실제로 들어간 피해</returns>
        public float TakeDamage(float amount, string cause = "")
        {
            if (IsDead || amount <= 0f) return 0f;
            float multiplier = CurrentRun != null ? CurrentRun.Data.damageTakenMultiplier : 1f;
            float dealt = Mathf.Min(Hp, amount * multiplier);
            if (dealt <= 0f) return 0f;

            Hp -= dealt;
            Damaged?.Invoke(dealt, cause);
            CheckPhaseTriggers();
            if (Hp <= 0f) Die();
            return dealt;
        }

        void CheckPhaseTriggers()
        {
            if (phaseTriggers == null) return;
            for (int i = 0; i < phaseTriggers.Length; i++)
            {
                if (firedTriggers.Contains(i)) continue;
                if (HpRatio > phaseTriggers[i].hpRatio) continue;
                firedTriggers.Add(i);
                pendingTriggers.Add(i);
            }
        }

        /// <summary>PhaseChange 상태에서 부른다. 체력이 높은 조건부터 적용.</summary>
        public void ApplyPendingPhaseTriggers()
        {
            pendingTriggers.Sort((a, b) => phaseTriggers[b].hpRatio.CompareTo(phaseTriggers[a].hpRatio));
            foreach (int i in pendingTriggers)
            {
                var t = phaseTriggers[i];
                if (t.setPhase > 0 && t.setPhase != Phase)
                {
                    Phase = t.setPhase;
                    if (logToConsole) Debug.Log($"[{bossId}] {Phase}페이즈 진입 (HP {HpRatio:P0})", this);
                    PhaseChanged?.Invoke(Phase);
                }
                if (t.setSpeedMultiplier > 0f)
                {
                    SpeedMultiplier = t.setSpeedMultiplier;
                    if (animator != null) animator.speed = SpeedMultiplier;
                }
                if (t.forcedPatterns != null)
                    foreach (var p in t.forcedPatterns)
                        if (p != null) forcedQueue.Enqueue(p);
            }
            pendingTriggers.Clear();
        }

        void Die()
        {
            Hp = 0f;
            if (CurrentRun != null) CurrentRun.Interrupted = true;
            forcedQueue.Clear();
            pendingTriggers.Clear();
            Fsm.Change(Dead);
            EndPattern(true);
            if (logToConsole) Debug.Log($"[{bossId}] 사망 (전투 시간 {BattleTime:0.0}s)", this);
            Died?.Invoke();
        }

        // ─────────────── 위치·방향 ───────────────

        public int DirectionToTarget()
        {
            if (target == null) return Facing;
            return target.position.x < transform.position.x ? -1 : 1;
        }

        public void FaceTarget() => SetFacing(DirectionToTarget());

        public void SetFacing(int dir)
        {
            dir = dir < 0 ? -1 : 1;
            if (dir == Facing) return;
            Facing = dir;
            TargetBehindTime = 0f;
            if (visualRoot != null)
            {
                var s = visualRoot.localScale;
                s.x = Mathf.Abs(s.x) * Facing;
                visualRoot.localScale = s;
            }
        }

        /// <summary>보스 정면 끝(플레이어 쪽) ~ 플레이어 몸 가장자리까지 가로 거리(m)</summary>
        public float DistanceToTarget()
        {
            if (target == null) return float.MaxValue;
            int dir = DirectionToTarget();
            float front = transform.position.x + dir * bodyWidth * 0.5f;
            float tx = target.position.x;
            if (targetCollider != null)
                tx = dir > 0 ? targetCollider.bounds.min.x : targetCollider.bounds.max.x;
            return Mathf.Max(0f, (tx - front) * dir);
        }

        /// <summary>
        /// 가로로 이동. Rigidbody2D를 쓴다면 Kinematic으로 두는 것을 권장
        /// (보스는 물리 대신 패턴 데이터대로 움직인다).
        /// </summary>
        public void MoveHorizontal(float dx)
        {
            transform.position += new Vector3(dx, 0f, 0f);
        }

        public void ChaseTarget(float dt)
        {
            if (target == null || moveSpeed <= 0f) return;
            FaceTarget();
            float distance = DistanceToTarget();
            if (distance <= chaseStopDistance) return;
            MoveHorizontal(Facing * Mathf.Min(moveSpeed * SpeedMultiplier * dt, distance - chaseStopDistance));
        }

        public DashBlock CheckDashBlock(float step, int dir, DashSettings d)
        {
            Vector2 origin = (Vector2)transform.position + Vector2.up * (bodyHeight * 0.5f);
            Vector2 rayDir = Vector2.right * (dir * Facing);
            float reach = bodyWidth * 0.5f + step;

            if (d.groggyObjectMask.value != 0 && Physics2D.Raycast(origin, rayDir, reach, d.groggyObjectMask).collider != null)
                return DashBlock.GroggyObject;
            if (d.stopAtWall && wallMask.value != 0 && Physics2D.Raycast(origin, rayDir, reach, wallMask).collider != null)
                return DashBlock.Wall;
            return DashBlock.None;
        }

        void UpdateBehindTimer(float dt)
        {
            if (target == null) return;
            bool behind = DirectionToTarget() != Facing;
            TargetBehindTime = behind ? TargetBehindTime + dt : 0f;
        }

        // ─────────────── 판정 박스 ───────────────

        public Rect GetHitboxRect(HitboxShape box) => GetHitboxRect(box, transform.position, Facing);

        public Rect GetHitboxRect(HitboxShape box, Vector2 feet, int facing)
        {
            float minX, maxX;
            if (box.anchor == HitboxAnchor.BossCenter)
            {
                minX = feet.x - box.width * 0.5f;
                maxX = feet.x + box.width * 0.5f;
            }
            else
            {
                float front = feet.x + facing * bodyWidth * 0.5f;
                float a = front + facing * box.forwardStart;
                float b = a + facing * box.width;
                minX = Mathf.Min(a, b);
                maxX = Mathf.Max(a, b);
            }
            return new Rect(minX, feet.y + box.bottom, maxX - minX, box.height);
        }

        // ─────────────── 확장 지점 ───────────────

        /// <summary>DDA 연결 지점. null을 넣으면 보정 해제.</summary>
        public void SetTuning(string patternId, PatternTuning tuning)
        {
            if (string.IsNullOrEmpty(patternId)) return;
            if (tuning == null) tunings.Remove(patternId);
            else tunings[patternId] = tuning;
        }

        /// <summary>조건형 패턴용. BossPatternData.conditionKey에 같은 이름을 적는다.</summary>
        public void RegisterCondition(string key, Func<bool> check)
        {
            if (!string.IsNullOrEmpty(key)) conditions[key] = check;
        }

        public bool CheckCondition(string key)
        {
            if (string.IsNullOrEmpty(key)) return true;
            return conditions.TryGetValue(key, out var check) && check != null && check();
        }

        public void RegisterExecutor(PatternKind kind, PatternExecutor executor)
        {
            if (executor != null) executors[kind] = executor;
        }

        public PatternExecutor GetExecutor(PatternKind kind)
        {
            if (executors.TryGetValue(kind, out var e)) return e;
            Debug.LogWarning($"[{bossId}] {kind} 실행기가 없어 Melee로 실행합니다.", this);
            return executors[PatternKind.Melee];
        }

        public void EnqueueForcedPattern(BossPatternData pattern)
        {
            if (pattern != null) forcedQueue.Enqueue(pattern);
        }

        public bool IsOnCooldown(BossPatternData pattern)
        {
            return cooldownUntil.TryGetValue(pattern, out var until) && BattleTime < until;
        }

        public void PlayAnim(string trigger)
        {
            if (animator != null && !string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
        }

        // ─────────────── 디버그 ───────────────

        void OnDrawGizmosSelected()
        {
            Vector2 feet = transform.position;
            int facing = Application.isPlaying ? Facing : (visualRoot != null && visualRoot.localScale.x < 0f ? -1 : 1);

            // 보스 몸
            Gizmos.color = new Color(0.3f, 0.8f, 0.9f, 0.9f);
            Gizmos.DrawWireCube(feet + new Vector2(0f, bodyHeight * 0.5f), new Vector3(bodyWidth, bodyHeight, 0f));

            // 점프 높이 기준선
            Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
            Gizmos.DrawLine(feet + new Vector2(-6f, referenceJumpHeight), feet + new Vector2(6f, referenceJumpHeight));

            var p = Application.isPlaying && CurrentRun != null ? CurrentRun.Data : previewPattern;
            if (p == null || p.hits == null) return;
            foreach (var h in p.hits)
            {
                if (h == null) continue;

                // 투사체가 들고 가는 판정은 보스 앞에 그리면 거짓말이 되므로 생성 위치에 그린다
                if (h.carrier == HitCarrier.Projectile)
                {
                    var s = p.projectile;
                    Vector2 origin = feet + new Vector2(facing * (bodyWidth * 0.5f + s.spawnForward), s.spawnHeight);
                    Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
                    Gizmos.DrawWireCube(origin, new Vector3(h.box.width, h.box.height, 0f));
                    Gizmos.DrawLine(origin, origin + new Vector2(facing * s.maxDistance, 0f));
                    continue;
                }

                var r = GetHitboxRect(h.box, feet, facing);
                if (h.ignoresDodge) Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.95f);       // 보라 = 회피 불가
                else if (h.parryable) Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);      // 노랑 = 패링 가능
                else Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);                      // 빨강 = 패링 불가
                Gizmos.DrawWireCube(r.center, r.size);
            }
        }

        void OnGUI()
        {
            if (!showDebugOverlay || Fsm == null) return;
            string pattern = CurrentRun != null ? $"{CurrentRun.Data.displayName}  {CurrentRun.Elapsed:0.00}s / {CurrentRun.TotalLength:0.00}s" : "-";
            GUI.Box(new Rect(10, 10, 360, 96),
                $"{bossId}   HP {Hp:0} / {maxHp:0} ({HpRatio:P0})\n" +
                $"상태 {Fsm.Current?.Name}   페이즈 {Phase}   속도 ×{SpeedMultiplier:0.##}\n" +
                $"패턴 {pattern}\n" +
                $"등 뒤 {TargetBehindTime:0.0}s   거리 {DistanceToTarget():0.00}m");

            // 패링 신호가 나간 뒤 판정까지 동안 크게 표시 (시각 신호 연출이 붙기 전 시험용)
            if (Time.time - lastParrySignalTime <= parrySignalLead)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
                style.normal.textColor = new Color(1f, 0.85f, 0.2f);
                GUI.Label(new Rect(Screen.width * 0.5f - 90f, Screen.height * 0.3f, 300f, 50f), "패링 신호!", style);
            }
        }
    }
}
