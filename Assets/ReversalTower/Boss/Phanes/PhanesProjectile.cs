using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 파네스의 광탄·바늘·파편. 스킬이 만들고, 그 뒤로는 스스로 날아가다 사라진다.
    ///
    ///   유도      homingDuration 동안 플레이어 쪽으로 꺾인다. 그 뒤로는 직선 (기획서 4.1: 1.5초 후 유도 상실)
    ///   판정      원형(radius) 또는 진행 방향으로 기울어진 사각형(boxSize)
    ///   패링      닿기 parrySignalLead초 전에 패링 신호를 보낸다.
    ///             패링당하면 사라지거나(일반 광탄), 보스에게 되돌아간다(굴절의 광탄, 기획서 4.2)
    ///   반사      되돌아간 광탄이 보스에 닿으면 보스 피해 + 그로기 게이지
    ///
    /// 판정 하나(PhanesHit)를 여러 투사체가 나눠 들 수 있다. 모두 사라질 때까지 아무도 못 맞혔으면 회피로 보고한다.
    /// </summary>
    [AddComponentMenu("")]
    public class PhanesProjectile : MonoBehaviour
    {
        public struct Config
        {
            public float radius;           // 0보다 크면 원형 판정
            public Vector2 boxSize;        // radius가 0이면 사각형 판정 (x = 진행 방향 길이, y = 폭)
            public float speed;
            public bool homing;
            public float turnRate;         // 도/초
            public float homingDuration;
            public float lifetime;
            public bool destroyOnParry;
            public bool reflectOnParry;
            public float reflectSpeed;
            public float reflectDamage;
            public float reflectGroggy;
            public Color reflectColor;
            public bool despawnOnHit;
            public Color color;
            public bool trail;
        }

        PhanesBrain brain;
        BossController boss;
        PatternRun run;
        PhanesHit hit;
        Config cfg;

        Vector2 velocity;
        float life;
        bool reflected;
        bool finished;
        float trailTimer;
        SpriteRenderer core, glow;

        public bool IsReflected => reflected;

        public static PhanesProjectile Spawn(PhanesBrain brain, PatternRun run, PhanesHit hit, Vector2 origin, Vector2 direction, Config cfg)
        {
            if (brain == null || run == null || hit == null) return null;
            var go = new GameObject($"PhanesProjectile_{run.PatternId}");
            go.transform.position = origin;
            var p = go.AddComponent<PhanesProjectile>();
            p.Init(brain, run, hit, direction, cfg);
            return p;
        }

        void Init(PhanesBrain owner, PatternRun source, PhanesHit h, Vector2 direction, Config c)
        {
            brain = owner;
            boss = owner.Boss;
            run = source;
            hit = h;
            cfg = c;
            hit.AddCarrier();

            if (direction.sqrMagnitude < 0.0001f) direction = new Vector2(boss.Facing, 0f);
            velocity = direction.normalized * cfg.speed;

            bool round = cfg.radius > 0f;
            Vector2 size = round ? Vector2.one * cfg.radius * 2f : cfg.boxSize;

            glow = MakeChild("Glow", PhanesFx.Glow, round ? size * 2.2f : new Vector2(size.x * 1.6f, size.y * 3f),
                             PhanesFx.WithAlpha(cfg.color, 0.45f), PhanesFx.OrderProjectile - 1);
            core = MakeChild("Core", round ? PhanesFx.Circle : PhanesFx.Diamond, size,
                             Color.Lerp(cfg.color, Color.white, 0.35f), PhanesFx.OrderProjectile);
            FaceVelocity();
        }

        SpriteRenderer MakeChild(string name, Sprite sprite, Vector2 size, Color color, int order)
        {
            var sr = PhanesFx.Spawn(name, sprite, transform.position, size, color, order, transform);
            sr.transform.localPosition = Vector3.zero;
            return sr;
        }

        void Update()
        {
            if (finished) return;
            float dt = Time.deltaTime;
            life += dt;

            if (boss == null || boss.IsDead) { Finish(); return; }
            if (life >= cfg.lifetime) { Finish(); return; }

            if (reflected) MoveReflected(dt);
            else MoveForward(dt);
            if (finished) return;

            FaceVelocity();
            Trail(dt);

            if (!reflected) ResolveHit();
            if (!finished) CheckBounds();
        }

        void MoveForward(float dt)
        {
            if (cfg.homing && life <= cfg.homingDuration && brain.HasTarget)
            {
                Vector2 want = brain.TargetCenter - (Vector2)transform.position;
                if (want.sqrMagnitude > 0.0001f)
                {
                    float cur = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                    float goal = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
                    float next = Mathf.MoveTowardsAngle(cur, goal, cfg.turnRate * dt);
                    float rad = next * Mathf.Deg2Rad;
                    velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * cfg.speed;
                }
            }
            transform.position += (Vector3)(velocity * dt);
            TrySignalParry();
        }

        void MoveReflected(float dt)
        {
            Vector2 home = brain.BodyCenter;
            Vector2 delta = home - (Vector2)transform.position;
            float reach = Mathf.Max(0.3f, cfg.radius) + Mathf.Max(0.3f, boss.bodyWidth * 0.5f);
            if (delta.magnitude <= reach)
            {
                OnReflectArrived();
                return;
            }
            velocity = delta.normalized * cfg.reflectSpeed;
            transform.position += (Vector3)(velocity * dt);
        }

        void TrySignalParry()
        {
            if (hit.Signaled || hit.Event.Source == null || !hit.Event.Source.parryable || !brain.HasTarget) return;
            float halfSize = cfg.radius > 0f ? cfg.radius : cfg.boxSize.x * 0.5f;
            float dist = Vector2.Distance(transform.position, brain.TargetCenter) - halfSize - 0.35f;
            float speed = Mathf.Max(0.01f, velocity.magnitude);
            if (dist / speed > boss.parrySignalLead) return;
            hit.Signaled = true;
            boss.SignalParry(run, hit.Event);
        }

        void ResolveHit()
        {
            HitResult result;
            bool contact;
            if (cfg.radius > 0f)
            {
                contact = boss.ResolveHitCircle(run, hit.Event, transform.position, cfg.radius, hit.Touched, out result, fromProjectile: true);
            }
            else
            {
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                contact = boss.ResolveHitBox(run, hit.Event, transform.position, cfg.boxSize, angle, hit.Touched, out result, fromProjectile: true);
            }
            if (!contact) return;
            hit.AnyContact = true;

            switch (result)
            {
                case HitResult.Parried:
                    if (cfg.reflectOnParry) Reflect();
                    else if (cfg.destroyOnParry) Burst(0.8f, Finish);
                    break;
                case HitResult.Hit:
                    if (cfg.despawnOnHit) Burst(0.8f, Finish);
                    break;
            }
        }

        void Reflect()
        {
            reflected = true;
            life = 0f; // 돌아가는 시간을 새로 센다
            var c = cfg.reflectColor;
            core.color = Color.Lerp(c, Color.white, 0.45f);
            glow.color = PhanesFx.WithAlpha(c, 0.55f);
            cfg.color = c;
            PhanesFx.SpawnFx("ParryFlash", PhanesFx.Ring, transform.position, Vector2.one * 2.2f, c, PhanesFx.OrderAttack, 0.05f).FadeOut(0.3f);
            if (brain.logToConsole) Debug.Log($"[{boss.bossId}] {run.PatternId} 패링 성공 — 광탄이 보스에게 되돌아갑니다", boss);
        }

        void OnReflectArrived()
        {
            if (cfg.reflectDamage > 0f) boss.TakeDamage(cfg.reflectDamage, $"{run.PatternId} 반사");
            if (cfg.reflectGroggy > 0f) brain.AddGroggy(cfg.reflectGroggy, "굴절의 광탄 반사");
            Burst(1.6f, Finish);
        }

        void CheckBounds()
        {
            var a = brain.Arena;
            if (a == null) return;
            Vector2 p = transform.position;
            const float m = 3f;
            if (p.x < a.left - m || p.x > a.right + m || p.y < a.floorY - m || p.y > a.ceilingY + m) Finish();
        }

        void FaceVelocity()
        {
            if (cfg.radius > 0f || velocity.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }

        void Trail(float dt)
        {
            if (!cfg.trail) return;
            trailTimer += dt;
            if (trailTimer < 0.05f) return;
            trailTimer = 0f;
            float d = cfg.radius > 0f ? cfg.radius * 1.6f : 0.4f;
            PhanesFx.SpawnFx("Trail", PhanesFx.Glow, transform.position, Vector2.one * d, PhanesFx.WithAlpha(cfg.color, 0.4f),
                             PhanesFx.OrderProjectile - 2, 0.01f).FadeOut(0.3f);
        }

        void Burst(float size, System.Action then)
        {
            PhanesFx.SpawnFx("Burst", PhanesFx.Glow, transform.position, Vector2.one * size, PhanesFx.WithAlpha(cfg.color, 0.9f),
                             PhanesFx.OrderAttack, 0.03f).FadeOut(0.25f);
            then?.Invoke();
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            hit.ReleaseCarrier(boss, run);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // 씬 전환 등으로 갑자기 사라져도 판정 기록이 남지 않게
            if (!finished && hit != null)
            {
                finished = true;
                hit.ReleaseCarrier(boss, run);
            }
        }
    }
}
