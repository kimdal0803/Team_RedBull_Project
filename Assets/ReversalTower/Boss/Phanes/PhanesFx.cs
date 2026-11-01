using System;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 아트가 나오기 전까지 쓰는 임시 그림 모음. 텍스처를 코드로 만들어서 프리팹·에셋이 필요 없다.
    /// 모든 스프라이트는 1유닛(1m) 크기라서, Transform 크기 = 실제 크기(m)가 된다.
    ///
    /// 아트가 나오면: PhanesBrain의 스프라이트 칸에 넣거나, 여기서 쓰는 곳을 그 스프라이트로 바꾸면 된다.
    /// </summary>
    public static class PhanesFx
    {
        // 그리기 순서 (보스 몸보다 위, UI보다 아래)
        public const int OrderAura = 8;
        public const int OrderBody = 10;
        public const int OrderWarning = 40;
        public const int OrderAttack = 46;
        public const int OrderProjectile = 52;
        public const int OrderOverlay = 300;

        static Sprite square, circle, glow, ring, diamond, crossCircle, runeCircle, vignette, robe, spike, crack;

        public static Sprite Square => square != null ? square : (square = Make("Square", 4, 4, 4, (u, v) => Color.white));

        /// <summary>가장자리가 부드러운 원 (판정이 있는 구체)</summary>
        public static Sprite Circle => circle != null ? circle : (circle = Make("Circle", 64, 64, 64, (u, v) =>
        {
            float d = Radial(u, v);
            return White(Mathf.Clamp01((1f - d) * 20f));
        }));

        /// <summary>빛 번짐 (가운데 진하고 바깥으로 흐려짐)</summary>
        public static Sprite Glow => glow != null ? glow : (glow = Make("Glow", 64, 64, 64, (u, v) =>
        {
            float d = Mathf.Clamp01(Radial(u, v));
            float a = (1f - d);
            return White(a * a);
        }));

        public static Sprite Ring => ring != null ? ring : (ring = Make("Ring", 64, 64, 64, (u, v) =>
        {
            float d = Radial(u, v);
            return White(Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f));
        }));

        /// <summary>마름모 (바늘·파편). 세로로 길게 늘려 쓴다.</summary>
        public static Sprite Diamond => diamond != null ? diamond : (diamond = Make("Diamond", 32, 32, 32, (u, v) =>
        {
            float x = Mathf.Abs(u * 2f - 1f), y = Mathf.Abs(v * 2f - 1f);
            return White(Mathf.Clamp01((1f - (x + y)) * 8f));
        }));

        /// <summary>십자 마법진 (천벌의 잔향 표식, 빛 기둥 경고)</summary>
        public static Sprite CrossCircle => crossCircle != null ? crossCircle : (crossCircle = Make("CrossCircle", 64, 64, 64, (u, v) =>
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float d = Mathf.Sqrt(x * x + y * y);
            float outer = Mathf.Clamp01(1f - Mathf.Abs(d - 0.88f) / 0.07f);
            float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.5f) / 0.05f);
            float cross = d < 0.85f ? Mathf.Clamp01(1f - Mathf.Min(Mathf.Abs(x), Mathf.Abs(y)) / 0.06f) : 0f;
            return White(Mathf.Max(outer, Mathf.Max(inner * 0.8f, cross * 0.9f)));
        }));

        /// <summary>회전 방향이 보이는 마법진 (환영 분신 구분 단서, 특이점)</summary>
        public static Sprite RuneCircle => runeCircle != null ? runeCircle : (runeCircle = Make("RuneCircle", 64, 64, 64, (u, v) =>
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            float d = Mathf.Sqrt(x * x + y * y);
            float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float outer = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) / 0.05f);
            // 톱니 모양 표시 6개 — 한쪽으로 기울어 있어서 도는 방향이 보인다
            float saw = Mathf.Repeat(ang, 60f) / 60f;
            float teeth = (d > 0.62f && d < 0.84f && saw < 0.35f && (d - 0.62f) / 0.22f < 1f - saw / 0.35f) ? 1f : 0f;
            float inner = Mathf.Clamp01(1f - Mathf.Abs(d - 0.45f) / 0.04f);
            return White(Mathf.Max(outer, Mathf.Max(teeth * 0.9f, inner * 0.7f)));
        }));

        /// <summary>가시 (천벌의 잔향 폭발). 아래가 넓고 위가 뾰족하다. 1×1m, 발밑 피벗.</summary>
        public static Sprite Spike => spike != null ? spike : (spike = Make("Spike", 32, 32, 32, (u, v) =>
        {
            float x = Mathf.Abs(u * 2f - 1f);
            float halfWidth = Mathf.Lerp(0.9f, 0.02f, v);
            return White(Mathf.Clamp01((halfWidth - x) * 10f));
        }, pivot: new Vector2(0.5f, 0f)));

        /// <summary>금 간 무늬 (결계벽 균열)</summary>
        public static Sprite Crack => crack != null ? crack : (crack = Make("Crack", 32, 32, 32, (u, v) =>
        {
            // 지그재그 선 몇 개
            float a = 0f;
            for (int i = 0; i < 3; i++)
            {
                float cx = 0.25f + 0.25f * i + 0.12f * Mathf.Sin(v * 9f + i * 2.1f);
                a = Mathf.Max(a, Mathf.Clamp01(1f - Mathf.Abs(u - cx) / 0.035f));
            }
            return White(a);
        }));

        /// <summary>암흑 디버프 화면 (가운데만 뚫린 어둠)</summary>
        public static Sprite Vignette => vignette != null ? vignette : (vignette = Make("Vignette", 256, 256, 256, (u, v) =>
        {
            float d = Radial(u, v) * 0.5f; // 0 = 가운데, 0.5 = 가장자리
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, 0.13f, d));
            return new Color(0.02f, 0.01f, 0.05f, a * 0.94f);
        }));

        /// <summary>임시 몸 (후드 쓴 마법사 실루엣). 1m × 2m, 발밑 피벗.</summary>
        public static Sprite Robe => robe != null ? robe : (robe = Make("Robe", 64, 128, 64, (u, v) =>
        {
            float x = u - 0.5f;         // -0.5 ~ 0.5 (m)
            float y = v * 2f;           // 0 ~ 2 (m)
            var cloth = new Color(0.96f, 0.93f, 0.82f, 1f);
            var trim = new Color(0.95f, 0.75f, 0.3f, 1f);
            var shadow = new Color(0.16f, 0.12f, 0.22f, 1f);

            // 얼굴 그늘 (후드 안)
            float fx = x - 0.03f, fy = y - 1.6f;
            if (fx * fx / 0.012f + fy * fy / 0.02f < 1f) return shadow;
            // 후드
            float hx = x, hy = y - 1.66f;
            if (hx * hx + hy * hy < 0.22f * 0.22f) return Shade(cloth, 0.9f);
            // 로브
            if (y <= 1.48f)
            {
                float half = Mathf.Lerp(0.46f, 0.17f, y / 1.48f);
                if (Mathf.Abs(x) <= half)
                {
                    if (y < 0.12f) return trim;                                  // 밑단
                    if (Mathf.Abs(x) < 0.035f && y > 0.12f && y < 1.3f) return trim; // 가운데 장식
                    return Shade(cloth, Mathf.Lerp(1f, 0.78f, Mathf.Abs(x) / half));
                }
            }
            return Color.clear;
        }, pivot: new Vector2(0.5f, 0f)));

        // ───────────────────────────── 만들기 도우미 ─────────────────────────────

        /// <summary>스프라이트 하나짜리 오브젝트를 만든다. size는 m 단위.</summary>
        public static SpriteRenderer Spawn(string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Spawn + 수명·깜빡임 조절기를 붙인다.</summary>
        public static PhanesFxObject SpawnFx(string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order, float life = -1f)
        {
            var sr = Spawn(name, sprite, position, size, color, order);
            var fx = sr.gameObject.AddComponent<PhanesFxObject>();
            fx.Init(sr, life);
            return fx;
        }

        /// <summary>a에서 b까지 이어지는 막대 (경고선, 레이저). thickness는 m.</summary>
        public static PhanesFxObject SpawnLine(string name, Vector2 a, Vector2 b, float thickness, Color color, int order, float life = -1f)
        {
            Vector2 d = b - a;
            var fx = SpawnFx(name, Square, (a + b) * 0.5f, new Vector2(d.magnitude, thickness), color, order, life);
            fx.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            return fx;
        }

        /// <summary>바닥에서 위로 솟는 직사각형 (빛 기둥, 장판). bottom-left 기준 rect.</summary>
        public static PhanesFxObject SpawnRect(string name, Rect r, Color color, int order, float life = -1f)
        {
            return SpawnFx(name, Square, r.center, r.size, color, order, life);
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static float Radial(float u, float v)
        {
            float x = u * 2f - 1f, y = v * 2f - 1f;
            return Mathf.Sqrt(x * x + y * y);
        }

        static Color White(float a) => new Color(1f, 1f, 1f, a);
        static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static Sprite Make(string name, int w, int h, float pixelsPerUnit, Func<float, float, Color> paint, Vector2? pivot = null)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "PhanesFx_" + name,
            };
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = paint((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(pixels);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0f, 0f, w, h), pivot ?? new Vector2(0.5f, 0.5f), pixelsPerUnit);
            s.name = "PhanesFx_" + name;
            return s;
        }
    }
}
