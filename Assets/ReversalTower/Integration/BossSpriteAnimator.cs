using System.Collections.Generic;
using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 보스 도트 스프라이트를 패턴 시간에 맞춰 넘긴다 (Animator 없이).
    ///
    ///   선딜·공격 단계  BossController가 흘리는 패턴 시계(PatternRun.Elapsed)를 그대로 따라간다
    ///   후딜            후딜에 들어온 뒤 흐른 시간을 이어 붙인다
    ///   그 밖           대기(걸으면 빠르게) / 그로기 / 사망
    ///
    /// 패턴 시계를 쓰기 때문에 DDA가 선딜·타수·속도를 바꿔도 그림이 판정과 어긋나지 않는다.
    /// 어떤 순간에 어느 프레임인지는 BossSpriteSet의 키 목록이 정한다.
    ///
    /// 그림 구조:  보스 ▸ VisualPixel (BossController.visualRoot — 보스가 돌아서면 뒤집힘) ▸ Body (SpriteRenderer)
    /// Body의 위치·크기만 건드린다 (점프 곡선, 늘어남, 떨림). 색은 다른 연출(BossStateFx)이 쓰도록 비워 둔다.
    ///
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트. 메뉴 '역전의 탑 ▸ 아라쿠스 도트 ▸ 2'가 알아서 붙인다.
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 도트 애니메이션")]
    [RequireComponent(typeof(BossController))]
    [DisallowMultipleComponent]
    public class BossSpriteAnimator : MonoBehaviour
    {
        [Header("연결")]
        public BossSpriteSet spriteSet;
        [Tooltip("그림을 그릴 렌더러. BossController.visualRoot 아래에 있어야 보스가 돌아설 때 같이 뒤집힌다")]
        public SpriteRenderer body;

        [Header("연출")]
        [Tooltip("타격 프레임으로 바뀌는 순간 몸이 앞으로 늘어났다 돌아온다")]
        public bool punch = true;
        [Tooltip("공격 직전 0.2초 동안 1픽셀씩 떤다")]
        public bool tremble = true;
        [Tooltip("퀵스텝·내려찍기 같은 점프를 그림으로 띄운다 (판정 위치는 그대로)")]
        public bool jumpArcs = true;
        public bool screenShake = true;
        [Tooltip("약한 흔들림 세기(m), 길이(초)")]
        [Min(0)] public float lightShake = 0.05f;
        [Min(0)] public float lightShakeTime = 0.12f;
        [Tooltip("센 흔들림 세기(m), 길이(초)")]
        [Min(0)] public float heavyShake = 0.12f;
        [Min(0)] public float heavyShakeTime = 0.22f;

        [Header("피격·이동·사망")]
        [Tooltip("맞았을 때 하얗게 번쩍이는 시간(초). 0이면 끔")]
        [Min(0)] public float hurtFlashTime = 0.07f;
        [Tooltip("이 속도(m/s)를 넘게 움직이면 대기 동작을 빠르게 재생한다")]
        [Min(0)] public float walkThreshold = 0.05f;
        [Min(0.1f)] public float walkIdleSpeed = 2f;
        public bool fadeOnDeath = true;
        [Min(0.1f)] public float deathFadeTime = 1.5f;

        [Tooltip("되돌리기 메뉴가 다시 켜 줄 원래 그림 (자동으로 채워짐)")]
        public GameObject previousVisual;

        struct Key
        {
            public float t;
            public int frame;
            public bool punch;
            public int shake;
            public int order;
        }

        const float PunchTime = 0.14f;
        const float SquashInTime = 0.12f;
        const float TrembleTime = 0.2f;
        const float TrembleMinTelegraph = 0.45f;

        BossController boss;
        SpriteRenderer flash;
        readonly List<Key> keys = new List<Key>();
        readonly Dictionary<Sprite, Sprite> whiteSprites = new Dictionary<Sprite, Sprite>();
        readonly Dictionary<Texture2D, Texture2D> whiteTextures = new Dictionary<Texture2D, Texture2D>();
        bool whiteUnavailable;

        PatternRun run;
        BossSpriteAnim anim;
        float evAttack, evRecovery, arcFrom, arcTo;
        bool hasArc;
        int lastKey = -1;

        BossState lastState;
        float recoveryClock, idleClock, groggyClock, deathClock, flashTimer;
        float lastX;
        bool alphaTouched;

        // ─────────────── 유니티 ───────────────

        void Awake()
        {
            boss = GetComponent<BossController>();
            lastX = transform.position.x;
        }

        void OnEnable()
        {
            if (boss == null) boss = GetComponent<BossController>();
            boss.PatternStarted += OnPatternStarted;
            boss.PatternEnded += OnPatternEnded;
            boss.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (boss == null) return;
            boss.PatternStarted -= OnPatternStarted;
            boss.PatternEnded -= OnPatternEnded;
            boss.Damaged -= OnDamaged;
        }

        void Start()
        {
            EnsureFlash();
        }

        void OnDestroy()
        {
            foreach (var s in whiteSprites.Values) if (s != null) Destroy(s);
            foreach (var t in whiteTextures.Values) if (t != null) Destroy(t);
        }

        void LateUpdate()
        {
            if (boss == null || body == null || spriteSet == null) return;
            var fsm = boss.Fsm;
            if (fsm == null) return;

            float dt = Time.deltaTime;
            var state = fsm.Current;
            if (state != lastState)
            {
                if (state == boss.Recovery) recoveryClock = 0f;
                if (state == boss.Groggy) groggyClock = 0f;
                if (state == boss.Dead) deathClock = 0f;
                lastState = state;
            }

            Sprite sprite = null;
            Vector3 pos = Vector3.zero;
            Vector3 scale = Vector3.one;
            float alpha = 1f;
            float ppu = body.sprite != null ? body.sprite.pixelsPerUnit : 45f;

            bool inPattern = run != null && anim != null && boss.CurrentRun == run &&
                             (state == boss.Telegraph || state == boss.Attack || state == boss.Recovery);

            if (state == boss.Dead)
            {
                deathClock += dt;
                sprite = Pick(spriteSet.groggy, spriteSet.groggy.Length - 1) ?? Pick(spriteSet.idle, 0);
                if (fadeOnDeath)
                {
                    float u = Mathf.Clamp01(deathClock / deathFadeTime);
                    alpha = 1f - u;
                    pos.y = -2f / ppu * u;
                }
            }
            else if (state == boss.Groggy && spriteSet.groggy.Length > 0)
            {
                groggyClock += dt;
                int i = (int)(groggyClock * spriteSet.groggyFps) % spriteSet.groggy.Length;
                sprite = spriteSet.groggy[i];
            }
            else if (inPattern && keys.Count > 0)
            {
                float t;
                if (state == boss.Recovery)
                {
                    recoveryClock += dt;
                    t = evRecovery + recoveryClock;
                }
                else
                {
                    t = run.Elapsed;
                }

                int idx = 0;
                for (int i = 0; i < keys.Count; i++)
                    if (keys[i].t <= t) idx = i;
                var key = keys[idx];
                if (idx != lastKey)
                {
                    if (idx > lastKey && key.shake > 0 && screenShake)
                    {
                        if (key.shake >= 2) CameraShake.Shake(heavyShake, heavyShakeTime);
                        else CameraShake.Shake(lightShake, lightShakeTime);
                    }
                    lastKey = idx;
                }

                sprite = Pick(anim.frames, key.frame);
                float since = t - key.t;

                if (punch && key.punch && since >= 0f && since < PunchTime)
                {
                    float a = 1f - since / PunchTime;
                    scale = new Vector3(1f + 0.07f * a, 1f - 0.06f * a, 1f);
                }
                if (idx == 0 && t < SquashInTime)
                {
                    float a = 1f - Mathf.Max(0f, t) / SquashInTime;
                    scale.y *= 1f - 0.06f * a;
                }
                if (tremble && anim.tremble && state == boss.Telegraph && evAttack >= TrembleMinTelegraph &&
                    t >= evAttack - TrembleTime && t < evAttack)
                {
                    pos.x = ((Time.frameCount & 1) == 0 ? 1f : -1f) / ppu;
                }
                if (jumpArcs && hasArc && t >= arcFrom && t < arcTo)
                {
                    float u = (t - arcFrom) / (arcTo - arcFrom);
                    pos.y = 4f * anim.arcHeight * u * (1f - u);
                }
            }
            else
            {
                float speed = Mathf.Abs(transform.position.x - lastX) / Mathf.Max(0.0001f, dt);
                idleClock += dt * (speed > walkThreshold ? walkIdleSpeed : 1f);
                sprite = IdleFrame();
            }
            lastX = transform.position.x;

            if (sprite != null) body.sprite = sprite;
            body.transform.localPosition = pos;
            body.transform.localScale = scale;
            if (alpha < 1f || alphaTouched)
            {
                var c = body.color;
                c.a = alpha;
                body.color = c;
                alphaTouched = alpha < 1f;
            }

            UpdateFlash(dt);
        }

        // ─────────────── 패턴 ───────────────

        void OnPatternStarted(PatternRun r)
        {
            run = r;
            anim = spriteSet != null ? spriteSet.Find(r.PatternId) : null;
            keys.Clear();
            lastKey = -1;
            hasArc = false;
            if (anim == null || anim.frames == null || anim.frames.Length == 0) return;

            float tele = r.Telegraph;
            evAttack = tele;
            evRecovery = tele + r.AttackDuration;
            float sp = Mathf.Max(0.1f, r.SpeedMultiplier);
            int order = 0;

            foreach (var k in anim.keys)
            {
                if (k == null) continue;
                if (k.at == SpriteKeyEvent.EachHit)
                {
                    int n = r.Hits.Count;
                    for (int i = 0; i < n; i++)
                    {
                        float ht = tele + r.Hits[i].Time;
                        Add(ht + k.offset / sp, k.frame, k.punch, k.shake, order++);
                        Add(ht + k.afterOffset / sp, i == n - 1 ? k.lastAfterFrame : k.afterFrame, false, 0, order++);
                    }
                }
                else if (TryEventTime(r, k.at, k.hitIndex, out float et))
                {
                    Add(et + k.offset / sp, k.frame, k.punch, k.shake, order++);
                }
            }
            keys.Sort((a, b) => a.t != b.t ? a.t.CompareTo(b.t) : a.order.CompareTo(b.order));

            if (anim.arc && TryEventTime(r, anim.arcFrom, 0, out arcFrom) && TryEventTime(r, anim.arcTo, 0, out arcTo))
                hasArc = arcTo > arcFrom + 0.01f;
        }

        void OnPatternEnded(PatternRun r, bool interrupted)
        {
            if (r != run) return;
            run = null;
            anim = null;
            keys.Clear();
            idleClock = 0f;
        }

        void Add(float t, int frame, bool punchKey, int shake, int order)
        {
            keys.Add(new Key { t = t, frame = frame, punch = punchKey, shake = shake, order = order });
        }

        /// <summary>패턴 시작(선딜 시작)부터 그 사건까지의 시간</summary>
        static bool TryEventTime(PatternRun r, SpriteKeyEvent e, int hitIndex, out float t)
        {
            float tele = r.Telegraph;
            var d = r.Data;
            float sp = Mathf.Max(0.1f, r.SpeedMultiplier);
            bool dashes = d != null && d.dash != null && d.dash.distance > 0f &&
                          (d.kind == PatternKind.Dash || d.kind == PatternKind.Projectile);
            switch (e)
            {
                case SpriteKeyEvent.Start: t = 0f; return true;
                case SpriteKeyEvent.Attack: t = tele; return true;
                case SpriteKeyEvent.Recovery: t = tele + r.AttackDuration; return true;
                case SpriteKeyEvent.DashStart:
                    t = tele + (dashes ? d.dash.startTime / sp : 0f);
                    return dashes;
                case SpriteKeyEvent.DashEnd:
                    t = tele + (dashes ? (d.dash.startTime + d.dash.duration) / sp : 0f);
                    return dashes;
                case SpriteKeyEvent.Hit:
                    if (hitIndex >= 0 && hitIndex < r.Hits.Count)
                    {
                        t = tele + r.Hits[hitIndex].Time;
                        return true;
                    }
                    t = 0f;
                    return false;
                default:
                    t = 0f;
                    return false;
            }
        }

        // ─────────────── 대기 ───────────────

        Sprite IdleFrame()
        {
            var frames = spriteSet.idle;
            if (frames == null || frames.Length == 0) return null;
            var order = spriteSet.idleOrder;
            int step = (int)(idleClock * spriteSet.idleFps);
            if (order != null && order.Length > 0)
                return Pick(frames, order[step % order.Length]);
            return frames[step % frames.Length];
        }

        static Sprite Pick(Sprite[] frames, int i)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Clamp(i, 0, frames.Length - 1)];
        }

        // ─────────────── 피격 번쩍임 ───────────────

        void OnDamaged(float dealt, string cause)
        {
            if (dealt > 0f && hurtFlashTime > 0f) flashTimer = hurtFlashTime;
        }

        void EnsureFlash()
        {
            if (flash != null || body == null) return;
            var t = body.transform.Find("HurtFlash");
            if (t == null)
            {
                var go = new GameObject("HurtFlash");
                t = go.transform;
                t.SetParent(body.transform, false);
            }
            flash = t.GetComponent<SpriteRenderer>();
            if (flash == null) flash = t.gameObject.AddComponent<SpriteRenderer>();
            flash.sortingLayerID = body.sortingLayerID;
            flash.sortingOrder = body.sortingOrder + 1;
            flash.enabled = false;
        }

        void UpdateFlash(float dt)
        {
            if (flash == null)
            {
                if (flashTimer <= 0f) return;
                EnsureFlash();   // body가 나중에 연결된 경우
                if (flash == null) return;
            }
            if (flashTimer <= 0f || whiteUnavailable)
            {
                if (flash.enabled) flash.enabled = false;
                return;
            }
            flashTimer -= dt;
            var w = WhiteOf(body.sprite);
            if (w == null)
            {
                flash.enabled = false;
                return;
            }
            flash.sprite = w;
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flashTimer / Mathf.Max(0.0001f, hurtFlashTime)) * 0.85f);
            flash.enabled = true;
        }

        /// <summary>같은 모양의 흰 실루엣 (텍스처가 Read/Write 켜져 있어야 함 — 아라쿠스 시트는 켜 둠)</summary>
        Sprite WhiteOf(Sprite s)
        {
            if (s == null || whiteUnavailable) return null;
            if (whiteSprites.TryGetValue(s, out var w)) return w;
            var tex = s.texture;
            if (!whiteTextures.TryGetValue(tex, out var wt))
            {
                if (!tex.isReadable)
                {
                    whiteUnavailable = true;
                    Debug.LogWarning($"[보스 도트] '{tex.name}' 텍스처의 Read/Write가 꺼져 있어 피격 번쩍임을 끕니다.", this);
                    return null;
                }
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    px[i].r = 255;
                    px[i].g = 255;
                    px[i].b = 255;
                }
                wt = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = tex.name + "_White",
                };
                wt.SetPixels32(px);
                wt.Apply(false, true);
                whiteTextures[tex] = wt;
            }
            var r = s.rect;
            w = Sprite.Create(wt, r, new Vector2(s.pivot.x / r.width, s.pivot.y / r.height), s.pixelsPerUnit, 0,
                              SpriteMeshType.FullRect);
            w.name = s.name + "_White";
            whiteSprites[s] = w;
            return w;
        }
    }
}
