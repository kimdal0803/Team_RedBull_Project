using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 파네스 전용 두뇌. BossController(FSM·패턴 선택·페이즈·DDA 연결)는 그대로 쓰고, 파네스만의 것을 더한다.
    ///
    ///   스킬 실행     PatternKind.Scripted 실행기를 등록하고, 패턴마다 PhanesCast를 만들어 돌린다
    ///   순간이동      행동 트리의 '랜덤 순간이동' + 플레이어가 붙으면 순간이동 (조건 이름: PhanesTeleport)
    ///   그로기 게이지 굴절의 광탄 반사가 쌓는다. 가득 차면 그로기
    ///   무력화 추락   그로기 동안 바닥으로 떨어졌다가 끝나면 다시 떠오른다 (기획서 3.1)
    ///   2페이즈 아우라, 환영 분신 따라 쏘기, 결계 카운트다운 표시
    ///
    /// 기획서 3.2 행동 트리와의 대응
    ///   ① HP 0 → 사망                      BossController 기본
    ///   ② HP 50% 미만 최초 1회 → 단절의 결계  PhaseTrigger(0.5, 2페이즈, 강제 패턴 Phanes_SeveranceBarrier)
    ///   ③④ 페이즈별 루프 (랜덤 순간이동 → 스킬 하나) 패턴의 minPhase/maxPhase + 이 컴포넌트의 순간이동
    ///
    /// 붙이는 곳: 파네스 보스 오브젝트 (BossController, PhanesMotor, PhanesArena와 같이)
    /// </summary>
    [AddComponentMenu("역전의 탑/파네스/파네스 두뇌 (PhanesBrain)")]
    [RequireComponent(typeof(BossController))]
    [RequireComponent(typeof(PhanesMotor))]
    [RequireComponent(typeof(PhanesArena))]
    public class PhanesBrain : MonoBehaviour
    {
        public const string TeleportCondition = "PhanesTeleport";

        [Header("순간이동 (기획서 3.1 이동, 3.2 '랜덤 순간이동')")]
        [Tooltip("스킬이 끝날 때마다 이 확률로 다음 행동 전에 순간이동한다")]
        [Range(0f, 1f)] public float randomTeleportChance = 0.4f;
        [Tooltip("플레이어가 이 거리(m) 안으로 붙으면 (기획서 4.3 상세 플로우 '플레이어 근접 시 순간이동')")]
        [Min(0f)] public float escapeDistance = 2.2f;
        [Tooltip("그만큼 붙어 있어야 도망간다(초)")]
        [Min(0f)] public float escapeDelay = 0.5f;
        [Tooltip("순간이동 뒤 다시 순간이동할 수 있을 때까지(초)")]
        [Min(0f)] public float teleportCooldown = 1.5f;

        [Header("그로기 게이지 (굴절의 광탄 반사가 쌓는다)")]
        [Min(1f)] public float groggyGaugeMax = 100f;
        [Tooltip("게이지가 가득 찼을 때 그로기(초). 임시")]
        [Min(0f)] public float gaugeGroggyTime = 2.5f;
        [Tooltip("그로기 동안 바닥으로 떨어진다 (기획서 3.1 무력화)")]
        public bool fallOnGroggy = true;

        [Header("지팡이 끝 (투사체가 나가는 곳, 보스 발밑 기준 m, x는 보는 방향)")]
        public Vector2 staffTipOffset = new Vector2(0.5f, 1.95f);

        [Header("임시 외형 (아트가 나오기 전)")]
        [Tooltip("켜면 그림(visualRoot)이 비어 있을 때 후드 쓴 마법사 실루엣을 만든다")]
        public bool buildPlaceholderVisual = true;
        public Color robeTint = Color.white;
        public Color staffColor = new Color(0.62f, 0.48f, 0.28f, 1f);
        public Color gemColor = new Color(1f, 0.82f, 0.25f, 1f);
        [Tooltip("2페이즈 아우라 색 (기획서 '몸 주위에 아우라가 일렁이는 이펙트')")]
        public Color auraColor = new Color(1f, 0.78f, 0.35f, 0.55f);

        [Header("디버그")]
        public bool showHud = true;
        public bool logToConsole = true;

        public BossController Boss { get; private set; }
        public PhanesMotor Motor { get; private set; }
        public PhanesArena Arena { get; private set; }
        public System.Random Rng { get; private set; }
        public float GroggyGauge { get; private set; }

        PhanesCast activeCast;
        readonly List<PhanesCast> background = new List<PhanesCast>();
        readonly List<PhanesClone> mimics = new List<PhanesClone>();
        Transform staffTip;
        PhanesFxObject aura;
        bool wantTeleport;
        float nextTeleportAllowed;
        float closeTimer;
        bool wasGroggy;

        Transform cachedTarget;
        Collider2D targetCollider;
        Rigidbody2D targetBody;

        // ───────────────────────────── 유니티 ─────────────────────────────

        void Awake()
        {
            Boss = GetComponent<BossController>();
            Motor = GetComponent<PhanesMotor>();
            Arena = GetComponent<PhanesArena>();
            Rng = Boss.randomSeed == 0 ? new System.Random() : new System.Random(Boss.randomSeed * 31 + 7);

            Boss.RegisterExecutor(PatternKind.Scripted, new PhanesExecutor(this));
            Boss.RegisterCondition(TeleportCondition, () =>
                wantTeleport && Time.time >= nextTeleportAllowed && !Motor.IsBusy && !Motor.IsGrounded);

            if (buildPlaceholderVisual) BuildPlaceholderVisual();
            EnsureStaffTip();
            CheckPatterns();
        }

        void OnEnable()
        {
            if (Boss == null) Boss = GetComponent<BossController>();
            Boss.PatternStarted += OnPatternStarted;
            Boss.PatternEnded += OnPatternEnded;
            Boss.GroggyStarted += OnGroggyStarted;
            Boss.PhaseChanged += OnPhaseChanged;
            Boss.Died += OnDied;
        }

        void OnDisable()
        {
            Boss.PatternStarted -= OnPatternStarted;
            Boss.PatternEnded -= OnPatternEnded;
            Boss.GroggyStarted -= OnGroggyStarted;
            Boss.PhaseChanged -= OnPhaseChanged;
            Boss.Died -= OnDied;
        }

        void Update()
        {
            if (Boss.IsDead || Boss.Fsm == null) return;
            float dt = Time.deltaTime;
            var state = Boss.Fsm.Current;

            // 대기 중에는 플레이어를 본다 (걷지 않으므로 BossController가 대신 돌려 주지 않는다)
            if (state == Boss.Idle && !Motor.IsHidden && !Motor.IsGrounded) Boss.FaceTarget();

            // 선딜 진행
            if (activeCast != null && !activeCast.AttackStarted && !activeCast.Cleaned && state == Boss.Telegraph)
                activeCast.TickTelegraph(dt);

            // 뒤에서 계속하는 스킬 (천벌의 잔향, 영벌의 업화, 분신 따라 쏘기)
            for (int i = background.Count - 1; i >= 0; i--)
            {
                var c = background[i];
                c.Step(dt);
                if (!c.WorkDone) continue;
                c.Cleanup(false);
                background.RemoveAt(i);
            }

            UpdateEscape(dt, state);

            // 무력화가 끝나면 다시 떠오른다
            bool groggy = state == Boss.Groggy;
            if (wasGroggy && !groggy && Motor.IsGrounded) Motor.RiseToHover();
            wasGroggy = groggy;

            if (aura != null) aura.Renderer.enabled = !Motor.IsHidden;
        }

        void FixedUpdate()
        {
            if (Boss.IsDead) return;
            float dt = Time.fixedDeltaTime;
            if (activeCast != null && !activeCast.InBackground) activeCast.FixedStep(dt);
            foreach (var c in background) c.FixedStep(dt);
        }

        // ───────────────────────────── 보스 이벤트 ─────────────────────────────

        void OnPatternStarted(PatternRun run)
        {
            activeCast = null;
            if (!(run.Data is PhanesPatternData data)) return;

            var cast = CreateCast(data.skill);
            if (cast == null)
            {
                Debug.LogWarning($"[{Boss.bossId}] {run.PatternId}: {data.skill} 스킬 실행기가 없습니다.", this);
                return;
            }
            cast.Setup(this, run, data);
            activeCast = cast;
            cast.BeginTelegraph();
        }

        void OnPatternEnded(PatternRun run, bool interrupted)
        {
            if (activeCast != null && activeCast.Run == run)
            {
                // 선딜 중에 끊긴 경우 (공격 단계를 거치지 않았다)
                if (!activeCast.InBackground && !activeCast.Cleaned) activeCast.Cleanup(interrupted);
                activeCast = null;
            }

            // 행동 트리: 스킬 하나가 끝나면 무작위로 순간이동
            if (!interrupted && run.Data is PhanesPatternData data && data.skill != PhanesSkill.Teleport
                && Rng.NextDouble() < randomTeleportChance)
                wantTeleport = true;
        }

        /// <summary>실행기가 공격 단계를 마쳤을 때 부른다.</summary>
        internal void OnAttackPhaseEnded(PhanesCast cast, bool interrupted)
        {
            if (cast.Cleaned) return;
            if (interrupted) { cast.Cleanup(true); return; }
            if (cast.WorkDone) { cast.Cleanup(false); return; }
            // 할 일이 남았다 → 뒤에서 계속
            cast.MoveToBackground();
            background.Add(cast);
        }

        void OnGroggyStarted(float duration, string reason)
        {
            wantTeleport = false;
            if (fallOnGroggy) Motor.FallToGround();
            else Motor.Stop();
        }

        void OnPhaseChanged(int phase)
        {
            if (phase >= 2 && aura == null) BuildAura();
        }

        void OnDied()
        {
            if (activeCast != null && !activeCast.Cleaned) activeCast.Cleanup(true);
            activeCast = null;
            foreach (var c in background) c.Cleanup(true);
            background.Clear();
            foreach (var m in mimics) if (m != null) m.Dismiss();
            mimics.Clear();
            if (aura != null) aura.Kill(0.5f);
            Motor.FallToGround();
        }

        internal PhanesCast CastFor(PatternRun run) => activeCast != null && activeCast.Run == run ? activeCast : null;

        static PhanesCast CreateCast(PhanesSkill skill)
        {
            switch (skill)
            {
                case PhanesSkill.Teleport: return new TeleportCast();
                case PhanesSkill.HomingOrb: return new HomingOrbCast();
                case PhanesSkill.FanVolley: return new FanVolleyCast();
                case PhanesSkill.RefractionOrb: return new RefractionOrbCast();
                case PhanesSkill.JudgmentPillar: return new JudgmentPillarCast();
                case PhanesSkill.PunishmentLine: return new PunishmentLineCast();
                case PhanesSkill.HeavenlyEcho: return new HeavenlyEchoCast();
                case PhanesSkill.Hellfire: return new HellfireCast();
                case PhanesSkill.SeveranceBarrier: return new SeveranceBarrierCast();
                case PhanesSkill.NebulaShards: return new NebulaShardsCast();
                case PhanesSkill.GravityDistortion: return new GravityDistortionCast();
                case PhanesSkill.PhantomClones: return new PhantomClonesCast();
                default: return null;
            }
        }

        // ───────────────────────────── 순간이동 ─────────────────────────────

        void UpdateEscape(float dt, BossState state)
        {
            bool close = state == Boss.Idle && HasTarget && !Motor.IsHidden && !Motor.IsGrounded
                         && Vector2.Distance(BodyCenter, TargetCenter) < escapeDistance;
            closeTimer = close ? closeTimer + dt : 0f;
            if (closeTimer >= escapeDelay)
            {
                wantTeleport = true;
                closeTimer = 0f;
            }
        }

        /// <summary>순간이동 패턴이 시작될 때 부른다</summary>
        public void ConsumeTeleportWish()
        {
            wantTeleport = false;
            nextTeleportAllowed = Time.time + teleportCooldown;
        }

        public Vector2 PickTeleportPoint()
        {
            Vector2 target = HasTarget ? TargetFeet : (Vector2)transform.position;
            return Arena.PickTeleportPoint(transform.position, target, Rng);
        }

        // ───────────────────────────── 그로기 게이지 ─────────────────────────────

        /// <summary>그로기 수치를 쌓는다. 가득 차면 그로기 (기획서 4.2 굴절의 광탄 '높은 그로기 수치를 누적')</summary>
        public void AddGroggy(float amount, string reason)
        {
            if (Boss.IsDead || amount <= 0f) return;
            GroggyGauge += amount;
            if (logToConsole) Debug.Log($"[{Boss.bossId}] 그로기 게이지 +{amount:0} → {Mathf.Min(GroggyGauge, groggyGaugeMax):0}/{groggyGaugeMax:0} ({reason})", this);
            if (GroggyGauge < groggyGaugeMax) return;
            GroggyGauge = 0f;
            Boss.EnterGroggy(gaugeGroggyTime, reason + " (그로기 게이지)");
        }

        // ───────────────────────────── 환영 분신 따라 쏘기 ─────────────────────────────

        public void AddMimic(PhanesClone c) { if (c != null && !mimics.Contains(c)) mimics.Add(c); }
        public void RemoveMimic(PhanesClone c) { mimics.Remove(c); }

        /// <summary>투사체가 나갈 곳들. 일반 공격이면 따라 쏘는 분신 위치도 더한다.</summary>
        public void GetCasterOrigins(List<Vector2> into, bool includeMimics)
        {
            into.Clear();
            into.Add(StaffTip);
            if (!includeMimics) return;
            for (int i = mimics.Count - 1; i >= 0; i--)
            {
                if (mimics[i] == null || !mimics[i].Alive) { mimics.RemoveAt(i); continue; }
                into.Add(mimics[i].StaffTipWorld);
            }
        }

        // ───────────────────────────── 위치 ─────────────────────────────

        public bool HasTarget { get { CacheTarget(); return cachedTarget != null; } }

        /// <summary>플레이어 몸 가운데</summary>
        public Vector2 TargetCenter
        {
            get
            {
                CacheTarget();
                if (targetCollider != null) return targetCollider.bounds.center;
                return cachedTarget != null ? (Vector2)cachedTarget.position + new Vector2(0f, 0.8f) : (Vector2)transform.position;
            }
        }

        /// <summary>플레이어 발밑</summary>
        public Vector2 TargetFeet
        {
            get
            {
                CacheTarget();
                if (targetCollider != null)
                {
                    var b = targetCollider.bounds;
                    return new Vector2(b.center.x, b.min.y);
                }
                return cachedTarget != null ? (Vector2)cachedTarget.position : (Vector2)transform.position;
            }
        }

        public Rigidbody2D TargetBody { get { CacheTarget(); return targetBody; } }

        /// <summary>보스 몸 가운데</summary>
        public Vector2 BodyCenter => (Vector2)transform.position + new Vector2(0f, Boss.bodyHeight * 0.5f);

        /// <summary>지팡이 끝 (투사체가 나가는 곳)</summary>
        public Vector2 StaffTip => staffTip != null
            ? (Vector2)staffTip.position
            : (Vector2)transform.position + new Vector2(staffTipOffset.x * Boss.Facing, staffTipOffset.y);

        public float Roll() => (float)Rng.NextDouble();

        void CacheTarget()
        {
            var t = Boss != null ? Boss.target : null;
            if (t == cachedTarget) return;
            cachedTarget = t;
            targetCollider = null;
            targetBody = null;
            if (t == null) return;
            foreach (var c in t.GetComponentsInChildren<Collider2D>())
            {
                if (c.isTrigger) continue; // 공격 범위 같은 트리거는 몸이 아니다
                targetCollider = c;
                break;
            }
            targetBody = t.GetComponent<Rigidbody2D>();
            if (targetBody == null && targetCollider != null) targetBody = targetCollider.attachedRigidbody;
        }

        // ───────────────────────────── 그림 ─────────────────────────────

        /// <summary>지팡이 끝에 빛을 모으는 연출 (선딜 동안 커진다)</summary>
        public PhanesFxObject SpawnStaffCharge(Color color, float duration, float size = 0.9f)
        {
            var fx = PhanesFx.SpawnFx("StaffCharge", PhanesFx.Glow, StaffTip, Vector2.one * size, PhanesFx.WithAlpha(color, 0.9f), PhanesFx.OrderAttack);
            fx.Follow(staffTip).Grow(Mathf.Max(0.05f, duration), new Vector2(0.2f, 0.2f)).Pulse(6f, 0.25f);
            return fx;
        }

        void EnsureStaffTip()
        {
            var parent = Boss.visualRoot != null ? Boss.visualRoot : transform;
            var existing = parent.Find("StaffTip");
            if (existing != null) { staffTip = existing; return; }
            staffTip = new GameObject("StaffTip").transform;
            staffTip.SetParent(parent, false);
            // visualRoot는 보는 방향에 따라 좌우가 뒤집히므로, '오른쪽을 볼 때' 기준으로 놓는다
            var s = parent.lossyScale;
            float sx = Mathf.Approximately(s.x, 0f) ? 1f : Mathf.Abs(s.x);
            float sy = Mathf.Approximately(s.y, 0f) ? 1f : Mathf.Abs(s.y);
            float facingSign = parent == transform ? 1f : Mathf.Sign(parent.localScale.x == 0f ? 1f : parent.localScale.x);
            staffTip.localPosition = new Vector3(staffTipOffset.x / sx * facingSign, staffTipOffset.y / sy, 0f);
        }

        /// <summary>visualRoot가 비어 있으면 임시 마법사 실루엣을 만든다 (Play 때마다 코드로 만든다).</summary>
        void BuildPlaceholderVisual()
        {
            var root = Boss.visualRoot;
            if (root == null)
            {
                root = new GameObject("Visual").transform;
                root.SetParent(transform, false);
                Boss.visualRoot = root;
            }
            if (root.GetComponentInChildren<SpriteRenderer>(true) != null) return; // 이미 아트가 있다

            float h = Boss.bodyHeight;
            float k = h / 2f; // Robe 스프라이트는 1m × 2m
            AddPart(root, "Body", PhanesFx.Robe, new Vector3(0f, 0f, 0f), new Vector2(k, k), robeTint, PhanesFx.OrderBody, 0f);
            AddPart(root, "Staff", PhanesFx.Square, new Vector3(staffTipOffset.x - 0.06f, staffTipOffset.y * 0.5f, 0f),
                    new Vector2(0.08f, staffTipOffset.y * 1.04f), staffColor, PhanesFx.OrderBody + 1, -3f);
            AddPart(root, "GemGlow", PhanesFx.Glow, new Vector3(staffTipOffset.x, staffTipOffset.y, 0f), Vector2.one * 0.7f,
                    PhanesFx.WithAlpha(gemColor, 0.6f), PhanesFx.OrderBody + 1, 0f);
            AddPart(root, "Gem", PhanesFx.Circle, new Vector3(staffTipOffset.x, staffTipOffset.y, 0f), Vector2.one * 0.22f,
                    gemColor, PhanesFx.OrderBody + 2, 0f);
        }

        static void AddPart(Transform root, string name, Sprite sprite, Vector3 localPos, Vector2 size, Color color, int order, float rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
        }

        void BuildAura()
        {
            float h = Boss.bodyHeight;
            aura = PhanesFx.SpawnFx("Phanes_Aura", PhanesFx.Glow, BodyCenter, new Vector2(h * 1.4f, h * 1.6f), auraColor, PhanesFx.OrderAura)
                .FadeIn(0.6f).Pulse(1.2f, 0.35f);
            aura.transform.SetParent(transform, true);
            if (logToConsole) Debug.Log($"[{Boss.bossId}] 2페이즈 — 아우라", this);
        }

        void CheckPatterns()
        {
            foreach (var p in Boss.patterns)
                if (p is PhanesPatternData pd && pd.kind != PatternKind.Scripted)
                    Debug.LogWarning($"[{Boss.bossId}] {pd.patternId}: kind가 {pd.kind}입니다. 파네스 패턴은 Scripted여야 스킬이 실행됩니다.", pd);
        }

        // ───────────────────────────── 디버그 표시 ─────────────────────────────

        void OnGUI()
        {
            if (!showHud || Boss == null) return;

            // BossController 디버그 상자(10,10)와 패턴 시각화 글자(10,112) 아래
            string status = "";
            if (PhanesStatusEffects.DarknessRemaining > 0f) status += $"  암흑 {PhanesStatusEffects.DarknessRemaining:0.0}s";
            if (PhanesStatusEffects.IsSilenced) status += $"  침묵 {PhanesStatusEffects.SilenceRemaining:0.0}s";
            GUI.Box(new Rect(10, 142, 360, 44),
                    $"그로기 게이지 {GroggyGauge:0}/{groggyGaugeMax:0}   뒤에서 진행 {background.Count}   분신 {mimics.Count}\n" +
                    $"{(Motor.IsGrounded ? "추락(무력화)" : Motor.IsHidden ? "숨음" : "체공")}{status}");

            string hud = activeCast != null ? activeCast.HudText : null;
            if (string.IsNullOrEmpty(hud)) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 0.88f, 0.4f);
            GUI.Label(new Rect(0f, Screen.height * 0.12f, Screen.width, 40f), hud, style);
        }
    }
}
