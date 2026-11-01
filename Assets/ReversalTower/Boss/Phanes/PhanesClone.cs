using System;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 환영 분신 (기획서 4.3). 보스 그림을 그대로 복사한 몸 + 구분 단서(지팡이 보석 색, 뒤 마법진 회전 방향).
    /// 체력 1 — 한 대 맞으면 PhantomClonesCast가 정답/오답을 처리한다.
    /// '시간 초과' 뒤에는 남아서 보스의 일반 공격을 따라 쏜다 (PhanesBrain.GetCasterOrigins).
    /// </summary>
    [AddComponentMenu("")]
    public class PhanesClone : MonoBehaviour, IPlayerAttackTarget
    {
        public bool IsTrue { get; private set; }
        public bool Hittable { get; set; }
        public bool Alive { get; private set; } = true;

        PhanesBrain brain;
        Action<PhanesClone> onHit;
        Transform visual;
        SpriteRenderer[] renderers = new SpriteRenderer[0];
        Color[] baseColors = new Color[0];
        float alpha;
        float alphaTarget = 1f;
        float fadeSpeed = 4f;
        bool destroyWhenFaded;
        Vector2 staffTipLocal;
        Transform circle;
        float circleSpin;

        public Vector2 StaffTipWorld
        {
            get
            {
                int f = visual != null && visual.localScale.x < 0f ? -1 : 1;
                return (Vector2)transform.position + new Vector2(staffTipLocal.x * f, staffTipLocal.y);
            }
        }

        public Vector2 Center => (Vector2)transform.position + new Vector2(0f, brain != null ? brain.Boss.bodyHeight * 0.5f : 1f);

        public static PhanesClone Create(PhanesBrain brain, Vector2 feet, bool isTrue, PhantomClonesSettings s, Action<PhanesClone> onHit)
        {
            var go = new GameObject("Phanes_Clone"); // 이름으로 정답이 드러나지 않게 둘 다 같은 이름
            go.layer = brain.gameObject.layer;
            go.transform.position = feet;

            var c = go.AddComponent<PhanesClone>();
            c.brain = brain;
            c.IsTrue = isTrue;
            c.onHit = onHit;
            c.staffTipLocal = brain.staffTipOffset;

            // 몸: 보스 그림 복사본 (같은 실루엣)
            var copy = brain.Motor.CloneVisual(go.transform);
            if (copy != null) c.visual = copy.transform;

            // 구분 단서 1: 뒤에서 도는 마법진 — 진짜는 trueCircleSpin 방향, 가짜는 반대
            float h = brain.Boss.bodyHeight;
            var circle = PhanesFx.Spawn("CloneCircle", PhanesFx.RuneCircle, feet + new Vector2(0f, h * 0.55f), Vector2.one * h * 1.05f,
                                        new Color(0.85f, 0.8f, 1f, 0.55f), PhanesFx.OrderBody - 1);
            circle.transform.SetParent(go.transform, true);
            c.circle = circle.transform;
            c.circleSpin = isTrue ? s.trueCircleSpin : -s.trueCircleSpin;

            // 구분 단서 2: 지팡이 보석 색 (그림을 따라 좌우로 뒤집히도록 그림 밑에 붙인다)
            Color gem = isTrue ? s.trueGemColor : s.fakeGemColor;
            var tip = c.StaffTipWorld;
            Transform markerParent = c.visual != null ? c.visual : go.transform;
            PhanesFx.Spawn("CloneGemGlow", PhanesFx.Glow, tip, Vector2.one * 0.75f, PhanesFx.WithAlpha(gem, 0.75f), PhanesFx.OrderBody + 3)
                .transform.SetParent(markerParent, true);
            PhanesFx.Spawn("CloneGem", PhanesFx.Circle, tip, Vector2.one * 0.24f, gem, PhanesFx.OrderBody + 4)
                .transform.SetParent(markerParent, true);

            // 맞는 판정: 보스 몸 크기 (플레이어가 통과할 수 있게 트리거)
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(brain.Boss.bodyWidth, h);
            box.offset = new Vector2(0f, h * 0.5f);

            c.renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            c.baseColors = new Color[c.renderers.Length];
            for (int i = 0; i < c.renderers.Length; i++) c.baseColors[i] = c.renderers[i].color;
            c.alpha = 0f;
            c.Apply();
            return c;
        }

        public void FadeIn(float seconds)
        {
            alphaTarget = 1f;
            fadeSpeed = 1f / Mathf.Max(0.01f, seconds);
        }

        /// <summary>조용히 사라진다</summary>
        public void Dismiss(float seconds = 0.25f)
        {
            if (!Alive) return;
            Alive = false;
            Hittable = false;
            alphaTarget = 0f;
            fadeSpeed = 1f / Mathf.Max(0.01f, seconds);
            destroyWhenFaded = true;
        }

        /// <summary>거울처럼 깨지며 사라진다 ('타격 시 거울이 깨지는 파편 이펙트')</summary>
        public void Shatter(Color color)
        {
            if (!Alive) return;
            Alive = false;
            Hittable = false;
            var c = Center;
            PhanesFx.SpawnFx("CloneShatter", PhanesFx.Glow, c, new Vector2(2f, 2.6f), PhanesFx.WithAlpha(color, 0.9f), PhanesFx.OrderAttack, 0.05f).FadeOut(0.35f);
            for (int i = 0; i < 8; i++)
            {
                var p = c + UnityEngine.Random.insideUnitCircle * 0.8f;
                PhanesFx.SpawnFx("CloneShard", PhanesFx.Diamond, p, new Vector2(0.18f, 0.45f), new Color(0.85f, 0.9f, 1f, 0.9f), PhanesFx.OrderAttack, 0.1f)
                    .Spin(UnityEngine.Random.Range(-600f, 600f)).FadeOut(0.4f);
            }
            Destroy(gameObject);
        }

        public void TakePlayerHit(int damage, string cause)
        {
            if (!Alive || !Hittable) return;
            onHit?.Invoke(this);
        }

        void Update()
        {
            alpha = Mathf.MoveTowards(alpha, alphaTarget, fadeSpeed * Time.deltaTime);
            Apply();
            if (destroyWhenFaded && alpha <= 0.001f) Destroy(gameObject);
            if (circle != null) circle.Rotate(0f, 0f, circleSpin * Time.deltaTime, Space.Self);

            // 플레이어 쪽을 본다
            if (visual != null && brain != null && brain.HasTarget)
            {
                int f = brain.TargetCenter.x < transform.position.x ? -1 : 1;
                var s = visual.localScale;
                s.x = Mathf.Abs(s.x) * f;
                visual.localScale = s;
            }
        }

        void Apply()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = baseColors[i];
                c.a *= alpha;
                renderers[i].color = c;
            }
        }
    }
}
