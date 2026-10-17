using System.Collections.Generic;
using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 보스 패턴을 게임 화면에서 눈으로 볼 수 있게 한다.
    ///
    /// BossController의 기즈모는 Scene 뷰에서만 보이기 때문에, Play 중 Game 창에서는
    /// 보스가 "언제 어디를 치는지" 전혀 알 수 없다. 이 컴포넌트가 그걸 실제 사각형으로 그려 준다.
    ///
    /// 색으로 읽는 법
    ///   노랑 (점점 진해짐)  준비 중. 진해질수록 판정이 가까움
    ///   하늘               패링 가능한 타 (패링 신호가 오면 하얗게 번쩍)
    ///   보라               회피 불가 (구르기로 못 피함)
    ///   빨강 (번쩍)        지금 판정이 들어감
    ///
    /// 날아가는 방패(투사체)는 BossProjectile이 스스로 보이므로 여기서 그리지 않는다.
    ///
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 패턴 시각화 (게임 화면)")]
    [RequireComponent(typeof(BossController))]
    public class BossHitboxVisualizer : MonoBehaviour
    {
        [Header("켜고 끄기")]
        public bool showHitboxes = true;
        [Tooltip("화면 왼쪽 위에 현재 패턴과 다음 판정까지 남은 시간을 글자로 표시")]
        public bool showLabel = true;

        [Header("색")]
        public Color telegraphColor = new Color(1f, 0.85f, 0.2f, 1f);   // 노랑 — 준비 중
        public Color parryableColor = new Color(0.35f, 0.85f, 1f, 1f);  // 하늘 — 패링 가능
        public Color ignoresDodgeColor = new Color(0.8f, 0.4f, 1f, 1f); // 보라 — 회피 불가
        public Color activeColor = new Color(1f, 0.2f, 0.2f, 1f);       // 빨강 — 판정 발생
        public Color parrySignalColor = Color.white;                    // 패링 신호 번쩍

        [Header("세기")]
        [Tooltip("준비 중 박스가 가장 흐릴 때의 투명도")]
        [Range(0f, 1f)] public float minAlpha = 0.08f;
        [Tooltip("판정 직전 박스가 가장 진할 때의 투명도")]
        [Range(0f, 1f)] public float maxAlpha = 0.45f;
        [Tooltip("판정이 들어간 뒤 빨갛게 번쩍이는 시간(초)")]
        [Min(0.02f)] public float activeFlashTime = 0.18f;
        [Tooltip("판정 몇 초 전부터 진해지기 시작할지")]
        [Min(0.05f)] public float rampTime = 0.8f;

        [Header("그리기 순서")]
        [Tooltip("보스 스프라이트보다 이만큼 앞에 그린다")]
        public int sortingOrderOffset = 20;

        class Box
        {
            public HitEvent Event;
            public GameObject Go;
            public SpriteRenderer Sr;
            public bool Signaled;     // 패링 신호가 왔는지
            public float SignalTime;
        }

        BossController boss;
        Transform root;
        readonly List<Box> boxes = new List<Box>();
        PatternRun current;
        string labelText = "";

        static Sprite quadSprite;

        void Awake()
        {
            boss = GetComponent<BossController>();
        }

        void OnEnable()
        {
            boss.PatternStarted += OnPatternStarted;
            boss.PatternEnded += OnPatternEnded;
            boss.ParrySignal += OnParrySignal;
            boss.Died += OnDied;
        }

        void OnDisable()
        {
            boss.PatternStarted -= OnPatternStarted;
            boss.PatternEnded -= OnPatternEnded;
            boss.ParrySignal -= OnParrySignal;
            boss.Died -= OnDied;
            ClearBoxes();
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        // ─────────────── 보스 이벤트 ───────────────

        void OnPatternStarted(PatternRun run)
        {
            ClearBoxes();
            current = run;
            if (!showHitboxes) return;

            EnsureRoot();
            foreach (var e in run.Hits)
            {
                // 투사체가 들고 가는 판정은 방패 자신이 보여 준다
                if (e.Source == null || e.Source.carrier == HitCarrier.Projectile) continue;
                boxes.Add(CreateBox(e));
            }
        }

        void OnParrySignal(PatternRun run, HitEvent e)
        {
            foreach (var b in boxes)
            {
                if (b.Event.Index != e.Index) continue;
                b.Signaled = true;
                b.SignalTime = Time.time;
            }
        }

        void OnPatternEnded(PatternRun run, bool interrupted)
        {
            current = null;
            ClearBoxes();
        }

        void OnDied()
        {
            current = null;
            ClearBoxes();
        }

        // ─────────────── 매 프레임 ───────────────

        void Update()
        {
            if (current == null || !showHitboxes)
            {
                labelText = "";
                return;
            }

            float elapsed = current.Elapsed;
            float nextHitIn = float.MaxValue;

            foreach (var b in boxes)
            {
                if (b.Go == null) continue;

                float hitAt = current.Telegraph + b.Event.Time;
                float endAt = current.Telegraph + b.Event.EndTime + activeFlashTime;
                float remain = hitAt - elapsed;
                if (remain > 0f && remain < nextHitIn) nextHitIn = remain;

                if (elapsed > endAt)
                {
                    b.Go.SetActive(false);
                    continue;
                }

                // 판정 박스는 보스를 따라 움직인다 (돌진 중에도 맞게 그려진다)
                Rect r = boss.GetHitboxRect(b.Event.Source.box);
                b.Go.SetActive(true);
                b.Go.transform.position = new Vector3(r.center.x, r.center.y, 0f);
                b.Go.transform.localScale = new Vector3(r.size.x, r.size.y, 1f);

                b.Sr.color = ColorFor(b, remain);
            }

            if (showLabel)
            {
                string tail = nextHitIn < float.MaxValue ? $"   다음 판정 {nextHitIn:0.00}초" : "";
                labelText = $"{current.Data.displayName}{tail}";
            }
        }

        Color ColorFor(Box b, float remainToHit)
        {
            // 판정이 지난 직후 — 빨갛게 번쩍
            if (remainToHit <= 0f)
            {
                var c = activeColor;
                c.a = maxAlpha + (1f - maxAlpha) * 0.5f;
                return c;
            }

            // 패링 신호 직후 짧게 하얗게
            if (b.Signaled && Time.time - b.SignalTime < 0.12f)
            {
                var c = parrySignalColor;
                c.a = maxAlpha;
                return c;
            }

            Color baseColor =
                b.Event.Source.ignoresDodge ? ignoresDodgeColor :
                b.Event.Source.parryable ? parryableColor :
                telegraphColor;

            // 판정이 가까울수록 진해진다
            float t = Mathf.Clamp01(1f - remainToHit / rampTime);
            baseColor.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            return baseColor;
        }

        // ─────────────── 박스 만들기 ───────────────

        void EnsureRoot()
        {
            if (root != null) return;
            var go = new GameObject($"__{boss.bossId}_HitboxVisuals");
            root = go.transform;
        }

        Box CreateBox(HitEvent e)
        {
            var go = new GameObject("Hitbox");
            go.transform.SetParent(root, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetQuadSprite();
            sr.color = Color.clear;

            var bossSr = GetComponentInChildren<SpriteRenderer>();
            if (bossSr != null)
            {
                sr.sortingLayerID = bossSr.sortingLayerID;
                sr.sortingOrder = bossSr.sortingOrder + sortingOrderOffset;
            }
            else
            {
                sr.sortingOrder = sortingOrderOffset;
            }

            return new Box { Event = e, Go = go, Sr = sr };
        }

        void ClearBoxes()
        {
            foreach (var b in boxes)
                if (b.Go != null) Destroy(b.Go);
            boxes.Clear();
            labelText = "";
        }

        static Sprite GetQuadSprite()
        {
            if (quadSprite != null) return quadSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            quadSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            quadSprite.name = "BossHitbox_Quad";
            return quadSprite;
        }

        void OnGUI()
        {
            if (!showLabel || string.IsNullOrEmpty(labelText)) return;
            // BossController의 디버그 상자(위쪽 10,10,360,96) 바로 아래에 둔다
            var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            GUI.Box(new Rect(10, 112, 360, 26), "  " + labelText, style);
        }
    }
}
