using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 임시 이펙트 하나의 수명·깜빡임·회전·커지기를 맡는다. PhanesFx.SpawnFx가 붙인다.
    /// 판정과는 상관없는 그림 전용이다 (판정은 스킬 실행기가 따로 계산한다).
    ///
    /// 쓰는 법 (이어서 부르기):
    ///   PhanesFx.SpawnFx(...).FadeIn(0.1f).Pulse(6f, 0.3f).Spin(90f);
    ///   fx.Kill(0.2f);   // 0.2초 동안 흐려지며 사라짐
    /// </summary>
    [AddComponentMenu("")]
    public class PhanesFxObject : MonoBehaviour
    {
        SpriteRenderer sr;
        Color baseColor;
        Vector3 baseScale;
        float age;
        float life = -1f;          // -1 = Kill 할 때까지
        float fadeIn, fadeOut = 0.15f;
        float pulseFreq, pulseAmount;
        float spin;
        float growTime;
        Vector2 growFrom = Vector2.one;
        bool growFromBottom;
        float bottomY;
        bool dying;
        float dieAge, dieDuration;
        float alphaScale = 1f;
        Transform follow;
        Vector3 followOffset;

        public SpriteRenderer Renderer => sr;

        public void Init(SpriteRenderer renderer, float lifetime)
        {
            sr = renderer;
            baseColor = sr.color;
            baseScale = transform.localScale;
            life = lifetime;
            Apply();
        }

        public PhanesFxObject FadeIn(float seconds) { fadeIn = Mathf.Max(0f, seconds); Apply(); return this; }
        public PhanesFxObject FadeOut(float seconds) { fadeOut = Mathf.Max(0f, seconds); return this; }
        public PhanesFxObject Pulse(float frequency, float amount) { pulseFreq = frequency; pulseAmount = Mathf.Clamp01(amount); return this; }
        public PhanesFxObject Spin(float degreesPerSecond) { spin = degreesPerSecond; return this; }

        /// <summary>처음 크기(비율)에서 원래 크기까지 커진다. fromBottom이면 아래 끝을 고정한 채 위로 솟는다.</summary>
        public PhanesFxObject Grow(float seconds, Vector2 fromRatio, bool fromBottom = false)
        {
            growTime = Mathf.Max(0.0001f, seconds);
            growFrom = fromRatio;
            growFromBottom = fromBottom;
            bottomY = transform.position.y - baseScale.y * 0.5f;
            Apply();
            return this;
        }

        /// <summary>매 프레임 이 Transform 위치(+offset)를 따라간다 (지팡이 끝에 모이는 빛 등)</summary>
        public PhanesFxObject Follow(Transform t, Vector3 offset = default)
        {
            follow = t;
            followOffset = offset;
            if (t != null) transform.position = t.position + offset;
            return this;
        }

        public void SetColor(Color c) { baseColor = c; Apply(); }
        public Color BaseColor => baseColor;

        /// <summary>전체 투명도 배율 (결계벽 카운트다운처럼 바깥에서 세기를 조절할 때)</summary>
        public void SetAlphaScale(float k) { alphaScale = Mathf.Max(0f, k); }

        /// <summary>흐려지며 사라진다. 이미 사라지는 중이면 무시.</summary>
        public void Kill(float fadeSeconds = 0.15f)
        {
            if (this == null || dying) return;
            dying = true;
            dieAge = age;
            dieDuration = Mathf.Max(0.0001f, fadeSeconds);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (!dying && life >= 0f && age >= life)
            {
                dying = true;
                dieAge = age;
                dieDuration = Mathf.Max(0.0001f, fadeOut);
            }
            if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime, Space.Self);
            if (follow != null) transform.position = follow.position + followOffset;
            Apply();
            if (dying && age - dieAge >= dieDuration) Destroy(gameObject);
        }

        void Apply()
        {
            if (sr == null) return;
            float a = 1f;
            if (fadeIn > 0f) a *= Mathf.Clamp01(age / fadeIn);
            if (pulseFreq > 0f) a *= 1f - pulseAmount * (0.5f + 0.5f * Mathf.Sin(age * pulseFreq * Mathf.PI * 2f));
            if (dying) a *= 1f - Mathf.Clamp01((age - dieAge) / dieDuration);
            var c = baseColor;
            c.a *= a * alphaScale;
            sr.color = c;

            if (growTime > 0f)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / growTime));
                var s = new Vector3(
                    baseScale.x * Mathf.Lerp(growFrom.x, 1f, k),
                    baseScale.y * Mathf.Lerp(growFrom.y, 1f, k),
                    baseScale.z);
                transform.localScale = s;
                if (growFromBottom)
                {
                    var p = transform.position;
                    p.y = bottomY + s.y * 0.5f;
                    transform.position = p;
                }
            }
        }
    }
}
