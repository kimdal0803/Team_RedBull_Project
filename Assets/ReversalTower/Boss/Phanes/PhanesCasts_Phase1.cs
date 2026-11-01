using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    // 기획서 3.1 이동 · 4.1 일반 공격 · 4.2 1페이즈 스킬
    // (2페이즈에서도 쓰인다 — 기획서 '2페이즈 상세: 1페이즈 패턴 및 강화된 1페이즈 패턴을 사용')

    /// <summary>순간이동 (기획서 3.1 이동 상태). 선딜 = 흐려짐, 공격 = 나타남. 판정 없음.</summary>
    public class TeleportCast : PhanesCast
    {
        protected override void OnTelegraphStart()
        {
            brain.ConsumeTeleportWish();
            var dest = brain.PickTeleportPoint();
            motor.Teleport(dest, Mathf.Max(0.05f, run.Telegraph), Mathf.Max(0.05f, run.AttackDuration));
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            return !motor.IsBusy || t > 2f;
        }
    }

    /// <summary>
    /// 일반 공격 1 · 유도 광탄 (기획서 4.1).
    /// 판정 하나 = 광탄 한 발. 1페이즈 1발 / 2페이즈 0.2초 간격 3발. 1.5초 뒤 유도를 잃고 직선.
    /// 환영 분신이 '따라 쏘기' 중이면 분신 위치에서도 같이 쏜다.
    /// </summary>
    public class HomingOrbCast : PhanesCast
    {
        readonly List<Vector2> origins = new List<Vector2>();

        protected override void OnTelegraphStart()
        {
            Track(brain.SpawnStaffCharge(data.homing.color, run.Telegraph));
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.homing;
            bool allFired = true;
            foreach (var h in hits)
            {
                if (h.Started) continue;
                if (t < h.Event.Time) { allFired = false; continue; }
                h.Started = true;

                brain.GetCasterOrigins(origins, data.isBasicAttack);
                foreach (var o in origins)
                {
                    PhanesProjectile.Spawn(brain, run, h, o, brain.TargetCenter - o, new PhanesProjectile.Config
                    {
                        radius = s.diameter * 0.5f,
                        speed = Speed(s.speed),
                        homing = true,
                        turnRate = s.turnRate * run.SpeedMultiplier,
                        homingDuration = Dur(s.homingDuration),
                        lifetime = Dur(s.lifetime),
                        destroyOnParry = s.destroyOnParry,
                        despawnOnHit = true,
                        color = s.color,
                        trail = true,
                    });
                }
                if (origins.Count == 0) h.Finish(boss, run);
            }
            return allFired;
        }
    }

    /// <summary>
    /// 일반 공격 2 · 부채꼴 산탄 (기획서 4.1).
    /// 시전 순간 플레이어 위치로 조준을 고정하고, 보스 전방 60° 부채꼴로 직선 바늘을 동시에 쏜다 (1페이즈 3갈래 / 2페이즈 5갈래).
    /// </summary>
    public class FanVolleyCast : PhanesCast
    {
        readonly List<Vector2> origins = new List<Vector2>();
        Vector2 aimPoint;

        protected override void OnTelegraphStart()
        {
            var s = data.fan;
            aimPoint = brain.TargetCenter;
            Track(brain.SpawnStaffCharge(s.color, run.Telegraph));

            // 날아갈 방향을 짧은 선으로 미리 보여 준다
            Vector2 o = brain.StaffTip;
            foreach (var dir in Directions(o))
                Track(PhanesFx.SpawnLine("FanWarning", o, o + dir * 3.2f, 0.05f, PhanesFx.WithAlpha(s.color, 0.45f), PhanesFx.OrderWarning)
                    .FadeIn(0.1f).Pulse(5f, 0.4f));
        }

        protected override void OnAttackStart()
        {
            if (!data.fan.lockAimAtCast) aimPoint = brain.TargetCenter;
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.fan;
            bool allFired = true;
            foreach (var h in hits)
            {
                if (h.Started) continue;
                if (t < h.Event.Time) { allFired = false; continue; }
                h.Started = true;

                brain.GetCasterOrigins(origins, data.isBasicAttack);
                foreach (var o in origins)
                    foreach (var dir in Directions(o))
                        PhanesProjectile.Spawn(brain, run, h, o, dir, new PhanesProjectile.Config
                        {
                            boxSize = new Vector2(s.length, s.width),
                            speed = Speed(s.speed),
                            lifetime = Dur(s.lifetime),
                            destroyOnParry = true,
                            despawnOnHit = true,
                            color = s.color,
                        });
                if (origins.Count == 0) h.Finish(boss, run);
            }
            return allFired;
        }

        IEnumerable<Vector2> Directions(Vector2 origin)
        {
            var s = data.fan;
            Vector2 aim = aimPoint - origin;
            if (aim.sqrMagnitude < 0.0001f) aim = new Vector2(boss.Facing, 0f);
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            int n = Mathf.Max(1, s.count);
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? baseAngle : baseAngle - s.totalAngle * 0.5f + s.totalAngle * i / (n - 1);
                float r = a * Mathf.Deg2Rad;
                yield return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            }
        }
    }

    /// <summary>
    /// 굴절의 광탄 (기획서 4.2). 순간이동 후 느리게 다가오는 대형 유도 구체.
    /// 대시로 피하면 그냥 지나가고, 저스트 패링하면 푸른빛으로 바뀌어 보스에게 되돌아가 피해 + 높은 그로기 수치.
    /// </summary>
    public class RefractionOrbCast : PhanesCast
    {
        PhanesFxObject charge;

        protected override void OnTelegraphTick(float t, float dt)
        {
            // 순간이동이 끝난 뒤 양손에 빛을 모은다
            if (charge == null && !motor.IsBusy)
                charge = Track(brain.SpawnStaffCharge(data.refraction.color, Mathf.Max(0.05f, run.Telegraph - t), 1.6f));
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.refraction;
            bool allFired = true;
            foreach (var h in hits)
            {
                if (h.Started) continue;
                if (t < h.Event.Time) { allFired = false; continue; }
                h.Started = true;
                if (charge != null) charge.Kill(0.05f);

                Vector2 o = brain.StaffTip;
                PhanesProjectile.Spawn(brain, run, h, o, brain.TargetCenter - o, new PhanesProjectile.Config
                {
                    radius = s.diameter * 0.5f,
                    speed = Speed(s.speed),
                    homing = true,
                    turnRate = s.turnRate * run.SpeedMultiplier,
                    homingDuration = Dur(s.homingDuration),
                    lifetime = Dur(s.lifetime),
                    reflectOnParry = true,
                    reflectSpeed = Speed(s.reflectSpeed),
                    reflectDamage = boss.maxHp * s.reflectDamageRatio,
                    reflectGroggy = s.reflectGroggy,
                    reflectColor = s.reflectedColor,
                    despawnOnHit = true,
                    color = s.color,
                    trail = true,
                });
            }
            return allFired;
        }
    }

    /// <summary>
    /// 단죄의 빛 기둥 (기획서 4.2). 시전 순간 플레이어 발밑에 경고 마법진 → 바닥~천장 빛 기둥 다단 히트.
    /// 폭 1.6m, 높이 무한이라 점프로는 못 피한다. X축으로 벗어나거나 무적 대시로만 피한다.
    /// </summary>
    public class JudgmentPillarCast : PhanesCast
    {
        Rect[] rects;
        List<PhanesHit> pillarHits;

        protected override void OnTelegraphStart()
        {
            var s = data.pillar;
            Vector2 feet = brain.TargetFeet;
            int n = Mathf.Max(1, s.count);
            rects = new Rect[n];
            for (int i = 0; i < n; i++)
            {
                float x = arena.ClampX(feet.x + (i - (n - 1) * 0.5f) * s.spacing, s.width * 0.5f);
                rects[i] = new Rect(x - s.width * 0.5f, arena.floorY - 1f, s.width, arena.ceilingY - arena.floorY + 2f);

                float ground = arena.SurfaceBelow(x, feet.y + 0.3f);
                // 발밑 경고 마법진 + 기둥 양쪽 경계선
                Track(PhanesFx.SpawnFx("PillarWarningCircle", PhanesFx.CrossCircle, new Vector2(x, ground + 0.06f),
                                       new Vector2(s.width * 1.3f, 0.45f), s.warningColor, PhanesFx.OrderWarning)
                    .FadeIn(0.12f).Pulse(4f, 0.35f));
                foreach (float edge in new[] { x - s.width * 0.5f, x + s.width * 0.5f })
                    Track(PhanesFx.SpawnLine("PillarWarningEdge", new Vector2(edge, arena.floorY), new Vector2(edge, arena.ceilingY),
                                             0.05f, PhanesFx.WithAlpha(s.warningColor, 0.5f), PhanesFx.OrderWarning)
                        .FadeIn(0.15f).Pulse(6f, 0.5f));
                Track(PhanesFx.SpawnRect("PillarWarningFill", rects[i], PhanesFx.WithAlpha(s.warningColor, 0.08f), PhanesFx.OrderWarning - 1)
                    .FadeIn(0.2f));
            }
            pillarHits = HitsOf(0);
        }

        protected override void OnAttackStart()
        {
            var s = data.pillar;
            float last = 0.2f;
            foreach (var h in pillarHits) last = Mathf.Max(last, h.Event.EndTime);
            foreach (var r in rects)
            {
                PhanesFx.SpawnRect("Pillar", r, PhanesFx.WithAlpha(s.pillarColor, 0.75f), PhanesFx.OrderAttack, last)
                    .Grow(0.08f, new Vector2(0.25f, 1f)).Pulse(12f, 0.2f).FadeOut(0.25f);
                var core = new Rect(r.center.x - r.width * 0.18f, r.y, r.width * 0.36f, r.height);
                PhanesFx.SpawnRect("PillarCore", core, Color.white, PhanesFx.OrderAttack + 1, last).FadeOut(0.2f);
            }
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            return ProcessTimeline(pillarHits, t, h => rects);
        }
    }

    /// <summary>
    /// 신벌의 징벌선 (기획서 4.2). 지팡이를 지면에 꽂고 최하단 바닥을 따라 수평 레이저가 고속으로 훑는다.
    /// 높이 0.8m라 점프로 넘는다. 1페이즈 전방 한 방향 / 2페이즈 양방향 또는 잔류 불꽃.
    /// hits[0] = 레이저, hits[1] = 잔류 불꽃 틱 (있을 때만).
    /// </summary>
    public class PunishmentLineCast : PhanesCast
    {
        float originX, floorY;
        readonly List<int> dirs = new List<int>();
        float beamEnd;            // 레이저가 끝까지 뻗고 남아 있다 사라지는 시각
        float sweepDuration;
        List<PhanesHit> beamHits, fireHits;
        readonly List<SpriteRenderer> beams = new List<SpriteRenderer>();
        Rect fireRect;
        bool fireShown;

        protected override void OnTelegraphStart()
        {
            var s = data.line;
            floorY = arena.floorY;
            float x = boss.transform.position.x;
            if (!arena.IsFloorExposed(x)) x = FindExposedX(x);
            x = arena.ClampX(x, 0.6f);
            var land = new Vector2(x, floorY);

            // 발판 위였으면 순간이동으로, 아니면 그대로 내려앉는다
            if (Mathf.Abs(x - boss.transform.position.x) > 0.3f) motor.Teleport(land, Dur(0.15f), Dur(0.15f));
            else motor.FlyTo(land, Mathf.Min(Dur(0.35f), run.Telegraph * 0.5f));

            int toPlayer = brain.TargetFeet.x >= x ? 1 : -1;
            boss.SetFacing(toPlayer);
            dirs.Clear();
            dirs.Add(toPlayer);
            if (s.bothDirections) dirs.Add(-toPlayer);

            foreach (int d in dirs)
            {
                float end = d > 0 ? arena.right : arena.left;
                var corridor = MakeRect(x, end, s.height);
                Track(PhanesFx.SpawnRect("LineWarning", corridor, PhanesFx.WithAlpha(s.color, 0.12f), PhanesFx.OrderWarning).FadeIn(0.2f).Pulse(5f, 0.4f));
                Track(PhanesFx.SpawnLine("LineWarningEdge", new Vector2(x, floorY + s.height), new Vector2(end, floorY + s.height),
                                         0.04f, PhanesFx.WithAlpha(new Color(1f, 0.3f, 0.2f), 0.7f), PhanesFx.OrderWarning).Pulse(7f, 0.5f));
            }
            beamHits = HitsOf(0);
            fireHits = HitsOf(1);
        }

        protected override void OnAttackStart()
        {
            var s = data.line;
            originX = boss.transform.position.x;
            float longest = 0f;
            foreach (int d in dirs) longest = Mathf.Max(longest, Mathf.Abs((d > 0 ? arena.right : arena.left) - originX));
            sweepDuration = longest / Speed(s.sweepSpeed);
            beamEnd = sweepDuration + Dur(s.lingerTime);

            // 지팡이를 꽂는 충격
            PhanesFx.SpawnFx("StaffPlant", PhanesFx.Glow, new Vector2(originX, floorY + 0.3f), new Vector2(2.2f, 1.2f),
                             s.color, PhanesFx.OrderAttack, 0.05f).FadeOut(0.3f);
            foreach (int d in dirs)
            {
                var beam = PhanesFx.Spawn("LineBeam", PhanesFx.Square, new Vector2(originX, floorY), Vector2.zero,
                                          PhanesFx.WithAlpha(s.color, 0.85f), PhanesFx.OrderAttack);
                Track(beam.gameObject);
                beams.Add(beam);
            }
            if (s.residualFire)
            {
                float a = originX, b = originX;
                foreach (int d in dirs) { float e = d > 0 ? arena.right : arena.left; a = Mathf.Min(a, e); b = Mathf.Max(b, e); }
                fireRect = new Rect(a, floorY, b - a, s.residualFireHeight);
            }
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.line;
            float reach = Speed(s.sweepSpeed) * t;

            // 레이저 그림: 앞부분이 고속으로 뻗어 나간다
            for (int i = 0; i < dirs.Count && i < beams.Count; i++)
            {
                if (beams[i] == null) continue;
                var r = BeamRect(dirs[i], reach, s.height);
                beams[i].transform.position = r.center;
                beams[i].transform.localScale = new Vector3(r.width, r.height, 1f);
                float fade = t > sweepDuration ? 1f - Mathf.Clamp01((t - sweepDuration) / Mathf.Max(0.01f, beamEnd - sweepDuration)) : 1f;
                beams[i].color = PhanesFx.WithAlpha(s.color, 0.85f * fade);
            }

            // 레이저 판정 (끝까지 뻗은 뒤 lingerTime 동안 유지)
            bool beamDone = true;
            foreach (var h in beamHits)
            {
                if (h.Finished) continue;
                if (t < h.Event.Time) { beamDone = false; continue; }
                foreach (int d in dirs) ResolveRect(h, BeamRect(d, reach, s.height));
                if (t >= beamEnd) h.Finish(boss, run);
                else beamDone = false;
            }

            // 잔류 불꽃 (레이저가 다 지나간 뒤부터)
            bool fireDone = true;
            if (fireHits.Count > 0)
            {
                if (!s.residualFire) AbandonAll(fireHits);
                else
                {
                    float offset = sweepDuration - fireHits[0].Event.Time;
                    if (t >= sweepDuration && !fireShown)
                    {
                        fireShown = true;
                        float last = 0f;
                        foreach (var h in fireHits) last = Mathf.Max(last, h.Event.EndTime + offset);
                        Track(PhanesFx.SpawnRect("ResidualFire", fireRect, PhanesFx.WithAlpha(new Color(1f, 0.5f, 0.2f), 0.5f),
                                                 PhanesFx.OrderAttack - 1, last - t).FadeIn(0.1f).Pulse(9f, 0.35f));
                    }
                    fireDone = ProcessTimeline(fireHits, t, h => new[] { fireRect }, offset);
                }
            }

            return beamDone && fireDone && t >= beamEnd;
        }

        Rect BeamRect(int dir, float reach, float height)
        {
            float end = dir > 0 ? arena.right : arena.left;
            float front = originX + dir * Mathf.Min(reach, Mathf.Abs(end - originX));
            return MakeRect(originX, front, height);
        }

        Rect MakeRect(float x0, float x1, float height)
        {
            float a = Mathf.Min(x0, x1), b = Mathf.Max(x0, x1);
            return new Rect(a, floorY, Mathf.Max(0.05f, b - a), height);
        }

        float FindExposedX(float x)
        {
            for (float d = 0.5f; d < arena.Width; d += 0.5f)
            {
                if (x + d < arena.right - 0.6f && arena.IsFloorExposed(x + d)) return x + d;
                if (x - d > arena.left + 0.6f && arena.IsFloorExposed(x - d)) return x - d;
            }
            return x;
        }
    }

    /// <summary>
    /// 천벌의 잔향 (기획서 4.2). 1.0초 간격으로 플레이어 발밑에 붉은 표식 3~4회, 각 표식은 0.8초 뒤 가시 폭발 (폭 1.0 × 높이 1.5m).
    /// runInBackground면 표식이 이어지는 동안 보스는 다음 패턴을 쓴다 ('다른 공격과 동시 시전').
    /// </summary>
    public class HeavenlyEchoCast : PhanesCast
    {
        List<PhanesHit> marks;
        readonly Dictionary<PhanesHit, PhanesFxObject> markFx = new Dictionary<PhanesHit, PhanesFxObject>();
        readonly Rect[] rect = new Rect[1];

        protected override void OnTelegraphStart()
        {
            Track(brain.SpawnStaffCharge(data.echo.markColor, run.Telegraph));
            marks = HitsOf(0);
        }

        protected override void OnAttackStart()
        {
            if (data.echo.runInBackground) released = true;
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.echo;
            float lead = Dur(s.markLead);
            bool allDone = true;

            foreach (var h in marks)
            {
                if (h.Finished) continue;
                allDone = false;

                // 표식 놓기: 폭발 lead초 전, 그 순간의 플레이어 발밑
                if (!h.Started && t >= h.Event.Time - lead)
                {
                    h.Started = true;
                    Vector2 feet = brain.TargetFeet;
                    float ground = arena.SurfaceBelow(feet.x, feet.y + 0.3f);
                    h.Point = new Vector2(arena.ClampX(feet.x, s.width * 0.5f), ground);
                    markFx[h] = Track(PhanesFx.SpawnFx("EchoMark", PhanesFx.CrossCircle, h.Point + new Vector2(0f, 0.06f),
                                                       new Vector2(s.width * 1.4f, 0.5f), s.markColor, PhanesFx.OrderWarning)
                        .FadeIn(0.08f).Pulse(6f, 0.45f));
                }
                if (!h.Started || t < h.Event.Time) continue;

                // 폭발
                if (markFx.TryGetValue(h, out var fx) && fx != null)
                {
                    fx.Kill(0.05f);
                    markFx.Remove(h);
                    PhanesFx.SpawnFx("EchoSpike", PhanesFx.Spike, h.Point, new Vector2(s.width, s.height), s.spikeColor,
                                     PhanesFx.OrderAttack, Mathf.Max(0.12f, h.Event.EndTime - h.Event.Time)).Grow(0.06f, new Vector2(0.6f, 0.1f)).FadeOut(0.2f);
                }
                rect[0] = new Rect(h.Point.x - s.width * 0.5f, h.Point.y, s.width, s.height);
                ResolveRect(h, rect[0]);
                if (t >= h.Event.EndTime) h.Finish(boss, run);
            }
            return allDone;
        }
    }

    /// <summary>
    /// 영벌의 업화 (기획서 4.2). 보스가 공중으로 떠오르고 1.2초 전조 뒤 최하단 바닥 전체가 5초 동안 화염 장판 (높이 1.2m).
    /// 공중 발판은 안전지대. runInBackground면 장판이 타는 동안 보스는 다음 패턴을 쓴다.
    /// </summary>
    public class HellfireCast : PhanesCast
    {
        Rect fireRect;
        Rect[] rects;
        List<PhanesHit> ticks;

        protected override void OnTelegraphStart()
        {
            var s = data.hellfire;
            fireRect = new Rect(arena.left - 0.5f, arena.floorY - 0.3f, arena.Width + 1f, s.height + 0.3f);
            rects = new[] { fireRect };
            ticks = HitsOf(0);

            float x = arena.ClampX(boss.transform.position.x, 1f);
            motor.FlyTo(new Vector2(x, arena.floorY + s.riseHeight), Mathf.Min(Dur(0.6f), run.Telegraph * 0.6f));

            // 바닥이 붉게 달아오른다
            Track(PhanesFx.SpawnRect("HellfireWarning", fireRect, PhanesFx.WithAlpha(new Color(1f, 0.15f, 0.1f), 0.22f), PhanesFx.OrderWarning)
                .FadeIn(run.Telegraph * 0.8f).Pulse(3f, 0.3f));
            Track(PhanesFx.SpawnLine("HellfireWarningTop", new Vector2(fireRect.xMin, fireRect.yMax), new Vector2(fireRect.xMax, fireRect.yMax),
                                     0.05f, new Color(1f, 0.35f, 0.2f, 0.8f), PhanesFx.OrderWarning).Pulse(6f, 0.5f));
            Track(brain.SpawnStaffCharge(s.color, run.Telegraph, 1.3f));
        }

        protected override void OnAttackStart()
        {
            var s = data.hellfire;
            float last = 0.2f;
            foreach (var h in ticks) last = Mathf.Max(last, h.Event.EndTime);
            Track(PhanesFx.SpawnRect("Hellfire", fireRect, PhanesFx.WithAlpha(s.color, 0.6f), PhanesFx.OrderAttack, last)
                .Grow(0.15f, new Vector2(1f, 0.1f), true).Pulse(7f, 0.25f).FadeOut(0.4f));
            Track(PhanesFx.SpawnRect("HellfireCore", new Rect(fireRect.x, fireRect.y, fireRect.width, fireRect.height * 0.45f),
                                     PhanesFx.WithAlpha(new Color(1f, 0.95f, 0.75f), 0.55f), PhanesFx.OrderAttack + 1, last)
                .Pulse(11f, 0.4f).FadeOut(0.4f));
            if (s.runInBackground) released = true;
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            return ProcessTimeline(ticks, t, h => rects);
        }
    }
}
