using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 보스가 던진 물체 (아라쿠스의 방패).
    /// ProjectileExecutor가 실행 중에 만들고, 그 뒤로는 스스로 날아갔다가 돌아와 사라진다.
    ///
    /// 판정
    ///   ProjectileSettings가 아니라, 자기가 들고 온 HitEvent의 박스 크기(width × height)를 쓴다.
    ///   박스는 투사체 한가운데를 따라다니고, 사는 동안 계속 켜져 있다 (같은 대상은 판정 하나당 한 번만).
    ///   아무도 못 맞히고 사라지면 '회피(Missed)'로 보고한다 — 보스 몸 판정과 같은 규칙이다.
    ///
    /// 프리팹이 필요 없다. 스프라이트를 안 넣으면 임시 네모를 만들어 쓴다.
    /// </summary>
    [AddComponentMenu("")] // 사용자가 직접 붙이는 컴포넌트가 아니다
    public class BossProjectile : MonoBehaviour
    {
        class CarriedHit
        {
            public HitEvent Event;
            public readonly HashSet<IBossAttackReceiver> Touched = new HashSet<IBossAttackReceiver>();
            public bool AnyContact;
        }

        BossController boss;
        PatternRun run;
        ProjectileSettings settings;
        readonly List<CarriedHit> carried = new List<CarriedHit>();

        SpriteRenderer sr;
        Transform visual;
        int direction = 1;
        float traveled;
        float life;
        bool returning;
        bool finished;

        static Sprite fallbackSprite;

        /// <summary>ProjectileExecutor가 부른다.</summary>
        public static BossProjectile Spawn(BossController boss, PatternRun run, List<HitEvent> hits)
        {
            if (boss == null || run == null || hits == null || hits.Count == 0) return null;
            var s = run.Data.projectile;

            int dir = boss.Facing;
            Vector3 origin = boss.transform.position
                             + new Vector3(dir * (boss.bodyWidth * 0.5f + s.spawnForward), s.spawnHeight, 0f);

            var go = new GameObject($"Projectile_{run.PatternId}");
            go.transform.position = origin;

            var p = go.AddComponent<BossProjectile>();
            p.Init(boss, run, hits, dir);
            return p;
        }

        void Init(BossController owner, PatternRun source, List<HitEvent> hits, int dir)
        {
            boss = owner;
            run = source;
            settings = source.Data.projectile;
            direction = dir;

            foreach (var e in hits) carried.Add(new CarriedHit { Event = e });

            // 보이는 부분은 자식으로 둔다 (회전시켜도 판정 박스는 축에 정렬된 채로 둔다)
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            sr = visual.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = settings.sprite != null ? settings.sprite : GetFallbackSprite();
            sr.color = settings.visualColor;

            // 스프라이트 1유닛 기준으로 원하는 m 크기에 맞춘다
            Vector2 spriteSize = sr.sprite != null && sr.sprite.bounds.size.x > 0.0001f
                ? (Vector2)sr.sprite.bounds.size
                : Vector2.one;
            visual.localScale = new Vector3(
                settings.visualSize.x / Mathf.Max(0.0001f, spriteSize.x),
                settings.visualSize.y / Mathf.Max(0.0001f, spriteSize.y),
                1f);
        }

        void Update()
        {
            if (finished) return;

            float dt = Time.deltaTime;
            life += dt;

            if (boss == null || boss.IsDead) { Finish(reportMiss: false); return; }
            if (life >= settings.maxLifetime) { Finish(reportMiss: true); return; }

            Move(dt);
            if (finished) return;

            Spin(dt);
            ResolveHits();
        }

        void Move(float dt)
        {
            float outSpeed = Mathf.Max(0.01f, settings.speed) * run.SpeedMultiplier;
            float backSpeed = (settings.returnSpeed > 0f ? settings.returnSpeed : settings.speed) * run.SpeedMultiplier;

            if (!returning)
            {
                float step = outSpeed * dt;
                float left = settings.maxDistance - traveled;
                if (step >= left)
                {
                    step = Mathf.Max(0f, left);
                    returning = true;
                }
                traveled += step;
                transform.position += new Vector3(direction * step, 0f, 0f);

                if (returning && !settings.returns) Finish(reportMiss: true);
                return;
            }

            // 돌아오는 중 — 보스 손 높이로 향한다
            Vector3 home = boss.transform.position + new Vector3(0f, settings.spawnHeight, 0f);
            Vector3 delta = home - transform.position;
            if (delta.magnitude <= settings.catchDistance) { Finish(reportMiss: true); return; }
            transform.position += delta.normalized * (backSpeed * dt);
        }

        void Spin(float dt)
        {
            if (visual == null || Mathf.Approximately(settings.spinSpeed, 0f)) return;
            visual.Rotate(0f, 0f, -direction * settings.spinSpeed * dt, Space.Self);
        }

        void ResolveHits()
        {
            for (int i = 0; i < carried.Count; i++)
            {
                var c = carried[i];
                var box = c.Event.Source != null ? c.Event.Source.box : null;
                float w = box != null ? box.width : 1f;
                float h = box != null ? box.height : 1f;

                var rect = new Rect(transform.position.x - w * 0.5f, transform.position.y - h * 0.5f, w, h);

                if (boss.ResolveHitAt(run, c.Event, rect, c.Touched, fromProjectile: true))
                {
                    c.AnyContact = true;
                    if (settings.despawnOnHit) { Finish(reportMiss: true); return; }
                }
            }
        }

        /// <param name="reportMiss">아무도 못 맞힌 판정을 '회피'로 보고할지 (보스 사망 때는 보고하지 않는다)</param>
        void Finish(bool reportMiss)
        {
            if (finished) return;
            finished = true;

            if (reportMiss && boss != null && run != null)
                foreach (var c in carried)
                    if (!c.AnyContact) boss.ReportMiss(run, c.Event);

            Destroy(gameObject);
        }

        static Sprite GetFallbackSprite()
        {
            if (fallbackSprite != null) return fallbackSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            fallbackSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            fallbackSprite.name = "BossProjectile_Fallback";
            return fallbackSprite;
        }

        void OnDrawGizmos()
        {
            if (carried.Count == 0) return;
            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
            foreach (var c in carried)
            {
                var box = c.Event.Source != null ? c.Event.Source.box : null;
                float w = box != null ? box.width : 1f;
                float h = box != null ? box.height : 1f;
                Gizmos.DrawWireCube(transform.position, new Vector3(w, h, 0f));
            }
        }
    }
}
