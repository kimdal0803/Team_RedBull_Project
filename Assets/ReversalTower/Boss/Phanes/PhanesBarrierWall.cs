using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 단절의 결계 결계벽 (기획서 4.3). 가로 2.0m, 바닥~천장, 통과 불가.
    /// 플레이어 공격(PlayerAttackTrigger → IPlayerAttackTarget)으로 체력을 깎아 부순다.
    /// 시간이 줄어들수록 빠르게 깜빡이고, 맞을수록 금이 간다 ('카운트다운 균열 이펙트').
    /// SeveranceBarrierCast가 만들고 지운다.
    /// </summary>
    [AddComponentMenu("")]
    public class PhanesBarrierWall : MonoBehaviour, IPlayerAttackTarget
    {
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public bool IsBroken { get; private set; }

        /// <summary>부서진 순간 (벽 오브젝트는 곧 사라지므로 상태는 이 이벤트로 받는다)</summary>
        public event System.Action Broken;

        BoxCollider2D col;
        PhanesFxObject body, core, cracks;
        Color color;
        float remaining01 = 1f;
        float hitFlash;
        bool dissolving;

        public static PhanesBarrierWall Create(float x, float floorY, float ceilingY, float width, int hp, Color color, float riseTime, int layer)
        {
            var go = new GameObject("Phanes_BarrierWall");
            go.layer = layer;
            float height = Mathf.Max(1f, ceilingY - floorY);
            go.transform.position = new Vector3(x, floorY + height * 0.5f, 0f);

            var w = go.AddComponent<PhanesBarrierWall>();
            w.MaxHp = w.Hp = Mathf.Max(1, hp);
            w.color = color;

            // 통과 불가 — 플레이어와 부딪히는 일반 콜라이더 (높이 전체를 처음부터 막는다)
            w.col = go.AddComponent<BoxCollider2D>();
            w.col.isTrigger = false;
            w.col.size = new Vector2(width, height + 2f);

            var rect = new Rect(x - width * 0.5f, floorY, width, height);
            w.body = PhanesFx.SpawnRect("WallBody", rect, color, PhanesFx.OrderAttack).Grow(riseTime, new Vector2(1f, 0.02f), true);
            w.core = PhanesFx.SpawnRect("WallCore", new Rect(x - width * 0.12f, floorY, width * 0.24f, height),
                                        new Color(1f, 1f, 0.92f, 0.85f), PhanesFx.OrderAttack + 1).Grow(riseTime, new Vector2(1f, 0.02f), true);
            w.cracks = PhanesFx.SpawnFx("WallCracks", PhanesFx.Crack, rect.center, rect.size, new Color(1f, 1f, 1f, 0f), PhanesFx.OrderAttack + 2);
            foreach (var fx in new[] { w.body, w.core, w.cracks }) fx.transform.SetParent(go.transform, true);

            // 솟구치는 순간 바닥 충격
            PhanesFx.SpawnFx("WallRise", PhanesFx.Glow, new Vector2(x, floorY + 0.4f), new Vector2(width * 3f, 1.6f), color, PhanesFx.OrderAttack, 0.1f).FadeOut(0.4f);
            return w;
        }

        /// <summary>남은 시간 비율(1 → 0). 0에 가까울수록 빠르게 깜빡인다.</summary>
        public void SetCountdown(float remaining) { remaining01 = Mathf.Clamp01(remaining); }

        public void TakePlayerHit(int damage, string cause)
        {
            if (IsBroken || dissolving) return;
            Hp -= Mathf.Max(1, damage);
            hitFlash = 1f;
            Debug.Log($"[파네스 결계벽] {cause} {damage} → 남은 체력 {Mathf.Max(0, Hp)}/{MaxHp}", this);
            if (Hp <= 0) Break();
        }

        void Break()
        {
            IsBroken = true;
            col.enabled = false;
            Broken?.Invoke();
            Vector2 c = transform.position;
            var size = col.size;
            PhanesFx.SpawnFx("WallShatter", PhanesFx.Glow, c, new Vector2(size.x * 4f, size.y), new Color(1f, 1f, 0.9f, 0.9f), PhanesFx.OrderAttack + 3, 0.05f).FadeOut(0.5f);
            // 파편 몇 조각
            for (int i = 0; i < 10; i++)
            {
                var p = c + new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(-size.y * 0.3f, size.y * 0.3f));
                PhanesFx.SpawnFx("WallShard", PhanesFx.Diamond, p, new Vector2(0.25f, 0.6f), color, PhanesFx.OrderAttack + 3, 0.1f)
                    .Spin(Random.Range(-540f, 540f)).FadeOut(0.5f);
            }
            body.Kill(0.12f);
            core.Kill(0.12f);
            cracks.Kill(0.12f);
            Destroy(gameObject, 0.2f);
        }

        /// <summary>파괴되지 않은 채로 사라진다 (폭발 뒤, 패턴 중단).</summary>
        public void Dissolve(float seconds = 0.4f)
        {
            if (dissolving) return;
            dissolving = true;
            if (col != null) col.enabled = false;
            if (body != null) body.Kill(seconds);
            if (core != null) core.Kill(seconds);
            if (cracks != null) cracks.Kill(seconds);
            Destroy(gameObject, seconds + 0.05f);
        }

        void Update()
        {
            if (IsBroken || dissolving) return;
            // 카운트다운: 남은 시간이 줄수록 깜빡임이 빨라지고 세진다
            float freq = Mathf.Lerp(1f, 7f, 1f - remaining01);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * freq * Mathf.PI * 2f);
            float k = Mathf.Lerp(0.85f, 1f, pulse * (1f - remaining01 * 0.6f));
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 6f);
            if (body != null) body.SetAlphaScale(k + hitFlash * 0.6f);
            if (cracks != null) cracks.SetColor(new Color(1f, 1f, 1f, 0.9f * (1f - (float)Hp / MaxHp)));
        }
    }
}
