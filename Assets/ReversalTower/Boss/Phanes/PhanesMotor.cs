using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 파네스의 몸 움직임. 걷지 않고 떠 있다가 순간이동한다 (기획서 2.1 '상시 비행', '순간이동').
    ///
    ///   체공        그림(visualRoot)만 위아래로 살짝 흔든다. 판정 기준점(보스 피벗)은 흔들리지 않는다.
    ///   순간이동    흐려짐 → 위치 이동 → 나타남. 흐려진 동안은 콜라이더를 꺼서 맞지 않는다.
    ///   날아가기    부드럽게 이동 (성운의 파편: 중앙 상공으로, 신벌의 징벌선: 바닥으로)
    ///   추락        무력화(그로기) 때 바닥으로 떨어진다 (기획서 3.1 '바닥으로 추락해 행동 불가')
    ///
    /// 붙이는 곳: 파네스 보스 오브젝트. BossController.moveSpeed는 0으로 둔다 (걷지 않음).
    /// </summary>
    [AddComponentMenu("역전의 탑/파네스/체공·순간이동 (PhanesMotor)")]
    [RequireComponent(typeof(BossController))]
    public class PhanesMotor : MonoBehaviour
    {
        [Header("체공 흔들림 (그림만)")]
        [Min(0f)] public float bobAmplitude = 0.12f;
        [Min(0f)] public float bobFrequency = 0.55f;

        [Header("순간이동 (기획서 3.1)")]
        [Tooltip("흐려지는 시간(초)")]
        [Min(0.01f)] public float fadeOutTime = 0.17f;
        [Tooltip("나타나는 시간(초)")]
        [Min(0.01f)] public float fadeInTime = 0.17f;
        public Color teleportFlashColor = new Color(1f, 0.92f, 0.6f, 0.9f);

        [Header("무력화 추락 (기획서 3.1)")]
        [Min(0.01f)] public float fallDuration = 0.3f;
        [Min(0.01f)] public float riseDuration = 0.45f;

        BossController boss;
        PhanesArena arena;
        SpriteRenderer[] renderers = new SpriteRenderer[0];
        Color[] baseColors = new Color[0];
        Collider2D[] bodyColliders = new Collider2D[0];
        Vector3 visualBaseLocal;
        bool hasVisualBase;
        float bobTime;
        float alpha = 1f;
        bool forcedHidden;

        /// <summary>순간이동·날아가기·추락 중인지</summary>
        public bool IsBusy => hasCurrent || steps.Count > 0;
        /// <summary>무력화로 바닥에 떨어져 있는지</summary>
        public bool IsGrounded { get; private set; }
        /// <summary>안 보이는 상태인지 (순간이동 중, 환영 분신 중)</summary>
        public bool IsHidden => forcedHidden || alpha < 0.5f;
        public float Alpha => alpha;

        Vector2 Feet => transform.position;

        void Awake()
        {
            boss = GetComponent<BossController>();
            arena = GetComponent<PhanesArena>();
            bodyColliders = GetComponents<Collider2D>();
        }

        void Start()
        {
            // 모든 Awake가 끝난 뒤라서, PhanesBrain이 만든 임시 그림까지 모인다
            RefreshRenderers();
        }

        /// <summary>그림을 새로 붙였으면 부른다 (투명도 조절 대상과 원래 색을 다시 모은다). 보이는 상태에서 불러야 한다.</summary>
        public void RefreshRenderers()
        {
            var root = boss != null && boss.visualRoot != null ? boss.visualRoot : transform;
            renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
            if (boss != null && boss.visualRoot != null)
            {
                visualBaseLocal = boss.visualRoot.localPosition;
                hasVisualBase = true;
            }
            ApplyAlpha();
        }

        void LateUpdate()
        {
            if (!hasVisualBase || boss == null || boss.visualRoot == null) return;
            if (IsGrounded || boss.IsDead)
            {
                boss.visualRoot.localPosition = visualBaseLocal;
                return;
            }
            bobTime += Time.deltaTime;
            float y = Mathf.Sin(bobTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            boss.visualRoot.localPosition = visualBaseLocal + new Vector3(0f, y, 0f);
        }

        // ───────────────────────────── 동작 ─────────────────────────────
        // 동작은 '단계' 묶음으로 넣고 Update에서 하나씩 진행한다 (코루틴 중첩·중단 문제를 피하려고).

        /// <summary>흐려짐 → dest로 이동 → 나타남. 음수면 기본 시간.</summary>
        public void Teleport(Vector2 dest, float fadeOut = -1f, float fadeIn = -1f)
        {
            ClearSteps();
            IsGrounded = false;
            steps.Enqueue(new Step { kind = StepKind.Flash, dest = Feet });
            steps.Enqueue(new Step { kind = StepKind.Fade, target = 0f, duration = fadeOut > 0f ? fadeOut : fadeOutTime });
            steps.Enqueue(new Step { kind = StepKind.Place, dest = dest });
            steps.Enqueue(new Step { kind = StepKind.Flash, dest = dest });
            steps.Enqueue(new Step { kind = StepKind.Fade, target = 1f, duration = fadeIn > 0f ? fadeIn : fadeInTime });
        }

        public void FlyTo(Vector2 dest, float duration)
        {
            ClearSteps();
            IsGrounded = false;
            steps.Enqueue(new Step { kind = StepKind.Move, dest = dest, duration = duration, smooth = true });
        }

        /// <summary>바로 밑 지면으로 떨어진다 (무력화)</summary>
        public void FallToGround()
        {
            ClearSteps();
            SetVisible(true);
            float ground = arena != null ? arena.SurfaceBelow(Feet.x, Feet.y + 0.1f) : Feet.y;
            IsGrounded = true;
            steps.Enqueue(new Step { kind = StepKind.Move, dest = new Vector2(Feet.x, ground), duration = fallDuration, smooth = false });
        }

        /// <summary>다시 떠오른다 (무력화가 끝났을 때)</summary>
        public void RiseToHover()
        {
            ClearSteps();
            IsGrounded = false;
            float y = arena != null ? arena.HoverY(Feet.x) : Feet.y + 1.3f;
            steps.Enqueue(new Step { kind = StepKind.Move, dest = new Vector2(Feet.x, y), duration = riseDuration, smooth = true });
        }

        /// <summary>움직임을 멈춘다. restoreVisible이면 바로 보이게 한다.</summary>
        public void Stop(bool restoreVisible = true)
        {
            ClearSteps();
            if (restoreVisible) SetVisible(true);
        }

        /// <summary>환영 분신처럼 오래 숨을 때. fade초 동안 흐려지거나 나타난다.</summary>
        public void SetHidden(bool hidden, float fade = 0.2f)
        {
            ClearSteps();
            forcedHidden = hidden;
            steps.Enqueue(new Step { kind = StepKind.Fade, target = hidden ? 0f : 1f, duration = fade });
            ApplyAlpha();
        }

        /// <summary>진행 중인 동작은 그대로 두고 바로 보이게만 한다 (끊긴 패턴 정리용).</summary>
        public void ForceVisible()
        {
            SetVisible(true);
        }

        /// <summary>숨은 채로 자리만 옮긴다 (환영 분신에서 진짜 분신 자리로 드러날 때).</summary>
        public void PlaceAt(Vector2 dest)
        {
            transform.position = new Vector3(dest.x, dest.y, transform.position.z);
        }

        /// <summary>그림을 복사한다 (환영 분신의 몸). 원래 색으로 되돌려서 돌려준다.</summary>
        public GameObject CloneVisual(Transform parent)
        {
            if (boss == null || boss.visualRoot == null) return null;
            var copy = Instantiate(boss.visualRoot.gameObject, parent, false);
            copy.name = "CloneVisual";
            copy.transform.localPosition = Vector3.zero;
            foreach (var c in copy.GetComponentsInChildren<Collider2D>(true)) Destroy(c);
            var srs = copy.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length && i < baseColors.Length; i++) srs[i].color = baseColors[i];
            return copy;
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        enum StepKind { Fade, Move, Place, Flash }

        struct Step
        {
            public StepKind kind;
            public float target;     // Fade: 목표 투명도
            public Vector2 dest;     // Move·Place·Flash: 위치
            public float duration;
            public bool smooth;      // Move: 부드럽게(true) / 점점 빠르게(false, 추락)
        }

        readonly System.Collections.Generic.Queue<Step> steps = new System.Collections.Generic.Queue<Step>();
        Step current;
        bool hasCurrent;
        float stepTime;
        Vector2 moveFrom;
        float fadeFrom;

        void ClearSteps()
        {
            steps.Clear();
            hasCurrent = false;
        }

        void Update()
        {
            // 한 프레임에 길이 0인 단계(배치·번쩍임)는 몰아서 처리한다
            for (int guard = 0; guard < 8; guard++)
            {
                if (!hasCurrent)
                {
                    if (steps.Count == 0) return;
                    current = steps.Dequeue();
                    hasCurrent = true;
                    stepTime = 0f;
                    moveFrom = Feet;
                    fadeFrom = alpha;
                }

                switch (current.kind)
                {
                    case StepKind.Place:
                        PlaceAt(current.dest);
                        if (boss != null) boss.FaceTarget();
                        hasCurrent = false;
                        continue;
                    case StepKind.Flash:
                        Flash(current.dest);
                        hasCurrent = false;
                        continue;
                }

                stepTime += Time.deltaTime;
                float duration = Mathf.Max(0.01f, current.duration);
                float k = Mathf.Clamp01(stepTime / duration);

                if (current.kind == StepKind.Fade)
                {
                    // 오래 숨는 중에는 나타나는 단계를 건너뛴다
                    float target = forcedHidden ? 0f : current.target;
                    alpha = Mathf.Lerp(fadeFrom, target, k);
                    ApplyAlpha();
                }
                else // Move
                {
                    float e = current.smooth ? Mathf.SmoothStep(0f, 1f, k) : k * k;
                    PlaceAt(Vector2.Lerp(moveFrom, current.dest, e));
                }

                if (k < 1f) return;
                hasCurrent = false;
                return;
            }
        }

        void SetVisible(bool visible)
        {
            if (visible) forcedHidden = false;
            alpha = visible ? 1f : 0f;
            ApplyAlpha();
        }

        void ApplyAlpha()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = baseColors[i];
                c.a *= alpha;
                renderers[i].color = c;
            }
            bool solid = !IsHidden;
            foreach (var c in bodyColliders)
                if (c != null) c.enabled = solid;
        }

        void Flash(Vector2 at)
        {
            float h = boss != null ? boss.bodyHeight : 2f;
            PhanesFx.SpawnFx("TeleportFlash", PhanesFx.Glow, at + new Vector2(0f, h * 0.5f), new Vector2(1.6f, h * 1.2f),
                             teleportFlashColor, PhanesFx.OrderAttack, 0.05f).FadeOut(0.25f);
        }
    }
}
