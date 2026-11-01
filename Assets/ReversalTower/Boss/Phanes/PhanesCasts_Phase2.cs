using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    // 기획서 4.3 2페이즈 스킬

    /// <summary>
    /// 단절의 결계 (기획서 4.3, 2페이즈 진입 시 확정 1회 — 핵심 기믹).
    ///   선딜   보스가 벽 너머로 순간이동하고, 맵 중앙에 결계벽(폭 2m, 천장까지, 통과 불가)이 솟는다
    ///   공격   제한 시간(hits[0].time) 동안 영창. 그 안에 벽을 부수면 성공
    ///   성공   보스 추락 + 행동 불능 3초, 맵 전체 공격 취소 (분기 A)
    ///   실패   맵 전체 폭발 — 즉사급 (분기 B)
    /// </summary>
    public class SeveranceBarrierCast : PhanesCast
    {
        PhanesBarrierWall wall;
        List<PhanesHit> blasts;
        float deadline = 10f;
        bool broken, succeeded, bossHitAfterBreak, flashShown;
        Rect[] arenaRect;

        public override string HudText
        {
            get
            {
                if (succeeded || Cleaned) return null;
                float left = AttackStarted ? Mathf.Max(0f, deadline - T) : deadline;
                if (broken) return $"결계 파괴! 보스를 공격하세요  {left:0.0}초";
                if (wall == null) return null;
                return $"단절의 결계 — 붕괴까지 {left:0.0}초   결계 {Mathf.Max(0, wall.Hp)}/{wall.MaxHp}";
            }
        }

        protected override void OnTelegraphStart()
        {
            var s = data.barrier;
            blasts = HitsOf(0);
            foreach (var h in blasts) { deadline = h.Event.Time; break; }
            arenaRect = new[] { new Rect(arena.left - 3f, arena.floorY - 3f, arena.Width + 6f, arena.ceilingY - arena.floorY + 6f) };

            Vector2 player = brain.TargetFeet;
            float half = s.wallWidth * 0.5f;
            float wallX = s.wallAtArenaCenter ? arena.CenterX : (player.x + boss.transform.position.x) * 0.5f;

            // 플레이어 쪽의 반대편으로 보스가 간다
            int playerSide = player.x >= wallX ? 1 : -1;
            if (Mathf.Abs(player.x - wallX) < 0.01f) playerSide = brain.Roll() < 0.5f ? 1 : -1;
            // 벽이 플레이어와 겹치면 보스 쪽으로 밀어서 세운다 (플레이어가 벽에 끼지 않게)
            if (Mathf.Abs(player.x - wallX) < half + 0.7f) wallX = player.x - playerSide * (half + 0.9f);
            wallX = arena.ClampX(wallX, half + 0.5f);

            float bossX = arena.ClampX(wallX - playerSide * (half + s.bossDistanceFromWall), 0.8f);
            if ((bossX - wallX) * playerSide > 0f) bossX = wallX - playerSide * (half + 0.8f); // 벽이 끝에 붙은 경우
            motor.Teleport(new Vector2(bossX, arena.HoverY(bossX)), Dur(0.2f), Dur(0.25f));

            wall = PhanesBarrierWall.Create(wallX, arena.floorY, arena.ceilingY, s.wallWidth, s.wallHp, s.wallColor, Dur(s.riseTime), 0);
            wall.Broken += OnWallBroken; // 선딜 중에 부숴도 놓치지 않게
            boss.Damaged += OnBossDamaged;
            if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 단절의 결계 — 제한 시간 {deadline:0.#}초, 결계 체력 {s.wallHp}", boss);
        }

        protected override void OnAttackStart()
        {
            // 영창하는 동안 지팡이 끝에 빛이 모인다
            Track(brain.SpawnStaffCharge(data.barrier.wallColor, deadline, 1.8f));
        }

        void OnWallBroken()
        {
            broken = true;
            if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 결계벽 파괴", boss);
        }

        void OnBossDamaged(float dealt, string cause)
        {
            if (broken && !succeeded) bossHitAfterBreak = true;
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.barrier;
            if (succeeded) return true;

            if (broken && (!s.requireBossHitAfterBreak || bossHitAfterBreak))
            {
                Succeed();
                return true;
            }
            if (wall != null) wall.SetCountdown(1f - Mathf.Clamp01(t / Mathf.Max(0.01f, deadline)));

            if (t < deadline) return false;

            // 실패: 맵 전체 폭발
            if (!flashShown)
            {
                flashShown = true;
                PhanesFx.SpawnRect("BarrierBlast", arenaRect[0], new Color(1f, 0.97f, 0.85f, 0.85f), PhanesFx.OrderOverlay - 1, 0.1f).FadeOut(0.7f);
                if (wall != null) wall.Dissolve(0.3f);
                if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 단절의 결계 실패 — 맵 전체 폭발", boss);
            }
            return ProcessTimeline(blasts, t, h => arenaRect);
        }

        void Succeed()
        {
            succeeded = true;
            AbandonAll(blasts); // 폭발 취소 — 회피로 기록하지 않는다
            if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 단절의 결계 파훼 성공 — 행동 불능 {data.barrier.successGroggyTime:0.#}초", boss);
            boss.EnterGroggy(data.barrier.successGroggyTime, "결계 파괴");
        }

        protected override void OnCleanup(bool interrupted)
        {
            boss.Damaged -= OnBossDamaged;
            if (wall != null)
            {
                wall.Broken -= OnWallBroken;
                if (!wall.IsBroken) wall.Dissolve(0.3f);
            }
        }
    }

    /// <summary>
    /// 성운의 파편 (기획서 4.3). 중앙 상공으로 떠올라 거대 성운 구체(반경 1.5m)를 소환하고,
    /// 별 모양(12갈래)으로 경고선 → 번개 줄기와 함께 파편 탄막을 방사한다. 탄막마다 각도를 엇갈려 안전지대를 바꾼다.
    /// hits[0] = 구체 접촉, hits[1] = 탄막 (repeatCount = 탄막 횟수)
    /// </summary>
    public class NebulaShardsCast : PhanesCast
    {
        Vector2 sphereCenter;
        PhanesFxObject sphere, sphereGlow;
        List<PhanesHit> sphereHits, waves;
        float baseAngle;
        float sphereEnd = -1f;
        readonly Dictionary<PhanesHit, List<PhanesFxObject>> warnLines = new Dictionary<PhanesHit, List<PhanesFxObject>>();

        protected override void OnTelegraphStart()
        {
            var s = data.nebula;
            sphereHits = HitsOf(0);
            waves = HitsOf(1);
            baseAngle = brain.Roll() * 360f / Mathf.Max(3, s.rays);

            var dest = new Vector2(arena.CenterX, Mathf.Min(arena.floorY + s.bossHeight, arena.ceilingY - s.sphereAboveBoss - s.sphereRadius - 0.3f));
            motor.FlyTo(dest, Mathf.Min(Dur(0.7f), run.Telegraph * 0.6f));
            sphereCenter = dest + new Vector2(0f, s.sphereAboveBoss);

            sphereGlow = Track(PhanesFx.SpawnFx("NebulaGlow", PhanesFx.Glow, sphereCenter, Vector2.one * s.sphereRadius * 4.5f,
                                                PhanesFx.WithAlpha(s.sphereColor, 0.5f), PhanesFx.OrderAttack - 1)
                .Grow(run.Telegraph, new Vector2(0.1f, 0.1f)).Pulse(1.5f, 0.25f));
            sphere = Track(PhanesFx.SpawnFx("NebulaSphere", PhanesFx.Circle, sphereCenter, Vector2.one * s.sphereRadius * 2f,
                                            Color.Lerp(s.sphereColor, new Color(1f, 0.9f, 0.5f), 0.35f), PhanesFx.OrderAttack)
                .Grow(run.Telegraph, new Vector2(0.05f, 0.05f)));
            Track(PhanesFx.SpawnFx("NebulaRunes", PhanesFx.RuneCircle, sphereCenter, Vector2.one * s.sphereRadius * 2.8f,
                                   PhanesFx.WithAlpha(s.shardColor, 0.6f), PhanesFx.OrderAttack + 1).Spin(70f).FadeIn(run.Telegraph));
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.nebula;
            float lead = Dur(s.warnLead);
            int rays = Mathf.Max(3, s.rays);
            float step = 360f / rays;
            bool wavesDone = true;
            float lastWave = 0f;

            for (int k = 0; k < waves.Count; k++)
            {
                var h = waves[k];
                lastWave = Mathf.Max(lastWave, h.Event.Time);
                if (h.Started) continue;
                wavesDone = false;
                float offset = baseAngle + (s.alternateOffset && k % 2 == 1 ? step * 0.5f : 0f);

                // 경고선
                if (!warnLines.ContainsKey(h) && t >= h.Event.Time - lead)
                {
                    var list = new List<PhanesFxObject>();
                    for (int i = 0; i < rays; i++)
                    {
                        Vector2 dir = Dir(offset + i * step);
                        list.Add(Track(PhanesFx.SpawnLine("NebulaWarning", sphereCenter + dir * s.sphereRadius, sphereCenter + dir * 30f,
                                                          0.05f, PhanesFx.WithAlpha(s.shardColor, 0.35f), PhanesFx.OrderWarning)
                            .FadeIn(0.1f).Pulse(6f, 0.4f)));
                    }
                    warnLines[h] = list;
                }
                if (t < h.Event.Time) continue;

                // 방사: 번개 줄기 번쩍 + 파편
                h.Started = true;
                if (warnLines.TryGetValue(h, out var lines)) foreach (var l in lines) if (l != null) l.Kill(0.05f);
                for (int i = 0; i < rays; i++)
                {
                    Vector2 dir = Dir(offset + i * step);
                    PhanesFx.SpawnLine("NebulaBolt", sphereCenter + dir * s.sphereRadius, sphereCenter + dir * 30f, 0.14f,
                                       new Color(1f, 0.95f, 0.8f, 0.85f), PhanesFx.OrderAttack, 0.05f).FadeOut(0.2f);
                    for (int j = 0; j < Mathf.Max(1, s.shardsPerRay); j++)
                    {
                        Vector2 origin = sphereCenter + dir * (s.sphereRadius + 0.4f + j * s.shardSpacing);
                        PhanesProjectile.Spawn(brain, run, h, origin, dir, new PhanesProjectile.Config
                        {
                            boxSize = new Vector2(s.shardLength, s.shardWidth),
                            speed = Speed(s.shardSpeed),
                            lifetime = Dur(6f),
                            destroyOnParry = true,
                            despawnOnHit = true,
                            color = s.shardColor,
                        });
                    }
                }
            }

            // 구체 접촉 판정 — 마지막 탄막 뒤 조금 더 남았다가 사라진다
            if (sphereEnd < 0f && wavesDone) sphereEnd = Mathf.Max(t, lastWave) + Dur(0.5f);
            bool sphereDone = true;
            foreach (var h in sphereHits)
            {
                if (h.Finished) continue;
                ResolveCircle(h, sphereCenter, s.sphereRadius, out _);
                if (sphereEnd >= 0f && t >= sphereEnd) h.Finish(boss, run);
                else sphereDone = false;
            }

            if (wavesDone && sphereEnd >= 0f && t >= sphereEnd)
            {
                if (sphere != null) sphere.Kill(0.3f);
                if (sphereGlow != null) sphereGlow.Kill(0.3f);
                return sphereDone;
            }
            return false;
        }

        static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }
    }

    /// <summary>
    /// 인력의 왜곡 (기획서 4.3). 맵 중앙 상공에 특이점을 만들고 3초 동안 반경 4.5m 안의 플레이어를 끌어당긴다
    /// (가까울수록 강하게). 끝나면 반경 2m 척력 폭발. hits[0] = 종료 폭발 (time = 흡인 시간).
    /// </summary>
    public class GravityDistortionCast : PhanesCast
    {
        Vector2 center;
        float pullEnd = 3f;
        List<PhanesHit> blasts;
        PhanesFxObject core, swirl, field;
        bool exploded;
        readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
        ContactFilter2D castFilter;
        bool filterReady;

        protected override void OnTelegraphStart()
        {
            var s = data.gravity;
            blasts = HitsOf(0);
            foreach (var h in blasts) { pullEnd = h.Event.Time; break; }
            center = new Vector2(arena.CenterX, arena.floorY + s.height);

            field = Track(PhanesFx.SpawnFx("GravityField", PhanesFx.Ring, center, Vector2.one * s.pullRadius * 2f,
                                           PhanesFx.WithAlpha(s.color, 0.35f), PhanesFx.OrderWarning).FadeIn(run.Telegraph).Spin(-25f));
            swirl = Track(PhanesFx.SpawnFx("GravitySwirl", PhanesFx.RuneCircle, center, Vector2.one * 2.2f,
                                           PhanesFx.WithAlpha(s.color, 0.85f), PhanesFx.OrderAttack).Grow(run.Telegraph, new Vector2(0.1f, 0.1f)).Spin(-260f));
            core = Track(PhanesFx.SpawnFx("GravityCore", PhanesFx.Circle, center, Vector2.one * 0.9f,
                                          new Color(0.12f, 0.05f, 0.2f, 0.95f), PhanesFx.OrderAttack + 1).Grow(run.Telegraph, new Vector2(0.05f, 0.05f)));
            Track(brain.SpawnStaffCharge(s.color, run.Telegraph));
        }

        protected override void OnAttackStart()
        {
            if (field != null) field.Pulse(2f, 0.3f);
        }

        protected override void OnFixedTick(float dt)
        {
            if (T >= pullEnd) return;
            var s = data.gravity;
            var rb = brain.TargetBody;
            if (rb == null) return;

            Vector2 p = brain.TargetCenter;
            Vector2 d = center - p;
            float dist = d.magnitude;
            if (dist > s.pullRadius || dist < 0.05f) return;

            float k = 1f - dist / s.pullRadius; // 가까울수록 1
            float speed = Mathf.Lerp(s.pullSpeedEdge, s.pullSpeedCenter, k) * run.SpeedMultiplier;
            Vector2 v = d / dist * speed;
            v.y *= s.verticalFactor;
            Vector2 move = v * dt;
            float len = move.magnitude;
            if (len < 0.0001f) return;

            // 벽을 뚫지 않게 미리 쓸어 본다
            if (!filterReady)
            {
                castFilter = new ContactFilter2D();
                castFilter.useTriggers = false;
                castFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(rb.gameObject.layer));
                filterReady = true;
            }
            Vector2 dir = move / len;
            int n = rb.Cast(dir, castFilter, castHits, len + 0.02f);
            float allowed = len;
            for (int i = 0; i < n; i++)
            {
                var c = castHits[i].collider;
                if (c == null || c.isTrigger) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, castHits[i].distance - 0.01f));
            }
            if (allowed > 0f) rb.position += dir * allowed;
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.gravity;
            if (t < pullEnd)
            {
                // 빨려 드는 빛 알갱이
                if (brain.Roll() < dt * 18f)
                {
                    Vector2 from = center + Random.insideUnitCircle.normalized * s.pullRadius;
                    PhanesFx.SpawnLine("GravityMote", from, Vector2.Lerp(from, center, 0.25f), 0.06f,
                                       PhanesFx.WithAlpha(s.color, 0.7f), PhanesFx.OrderWarning, 0.05f).FadeOut(0.3f);
                }
                return false;
            }

            if (!exploded)
            {
                exploded = true;
                if (core != null) core.Kill(0.05f);
                if (swirl != null) swirl.Kill(0.1f);
                if (field != null) field.Kill(0.1f);
                PhanesFx.SpawnFx("GravityBlast", PhanesFx.Glow, center, Vector2.one * s.explosionRadius * 2.6f,
                                 new Color(1f, 1f, 1f, 0.95f), PhanesFx.OrderAttack + 2, 0.06f).FadeOut(0.35f);
                PhanesFx.SpawnFx("GravityBlastRing", PhanesFx.Ring, center, Vector2.one * s.explosionRadius * 2f,
                                 s.color, PhanesFx.OrderAttack + 2, 0.05f).Grow(0.15f, new Vector2(0.3f, 0.3f)).FadeOut(0.3f);
            }

            bool allDone = true;
            foreach (var h in blasts)
            {
                if (h.Finished) continue;
                if (t < h.Event.Time) { allDone = false; continue; }
                ResolveCircle(h, center, s.explosionRadius, out _);
                if (t >= h.Event.EndTime) h.Finish(boss, run);
                else allDone = false;
            }
            return allDone;
        }
    }

    /// <summary>
    /// 환영 분신 (기획서 4.3). 보스가 흩어지고 좌우 3m에 분신 2체 (체력 1, 지팡이 보석 색·마법진 회전 방향이 다르다).
    ///   진짜 타격   분신 소멸, 보스가 진짜 자리에서 드러나며 짧은 경직
    ///   가짜 타격   가짜가 자폭 (반경 2m) + 플레이어에게 암흑/침묵
    ///   시간 초과   두 분신이 남아 일정 시간 동안 보스의 일반 공격을 같이 쏜다 (뒤에서 계속)
    /// hits[0] = 오답 자폭 (가짜를 때렸을 때만)
    /// </summary>
    public class PhantomClonesCast : PhanesCast
    {
        PhanesClone trueClone, fakeClone;
        Vector2 bossPos;
        bool decided, timedOut;
        float decideEnd, mimicEnd;
        List<PhanesHit> blasts;
        Vector2 blastPoint;
        float blastAt = -1f;

        protected override void OnTelegraphStart()
        {
            var s = data.clones;
            blasts = HitsOf(0);
            bossPos = boss.transform.position;

            // 보스가 흩어진다
            motor.SetHidden(true, Mathf.Max(0.05f, run.Telegraph * 0.5f));
            PhanesFx.SpawnFx("PhantomScatter", PhanesFx.Glow, brain.BodyCenter, new Vector2(2.2f, 2.8f),
                             new Color(0.85f, 0.85f, 1f, 0.8f), PhanesFx.OrderAttack, 0.1f).FadeOut(0.4f);

            float off = s.offset;
            float left = bossPos.x - off, right = bossPos.x + off;
            // 벽에 붙어 있으면 둘 다 안쪽으로 민다
            float shift = 0f;
            if (left < arena.left + 0.8f) shift = arena.left + 0.8f - left;
            if (right > arena.right - 0.8f) shift = arena.right - 0.8f - right;
            left += shift; right += shift;

            bool trueOnLeft = brain.Roll() < 0.5f;
            trueClone = PhanesClone.Create(brain, new Vector2(trueOnLeft ? left : right, arena.HoverY(trueOnLeft ? left : right)), true, s, OnCloneHit);
            fakeClone = PhanesClone.Create(brain, new Vector2(trueOnLeft ? right : left, arena.HoverY(trueOnLeft ? right : left)), false, s, OnCloneHit);
            trueClone.FadeIn(run.Telegraph);
            fakeClone.FadeIn(run.Telegraph);
            if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 환영 분신 — 진짜는 {(trueOnLeft ? "왼쪽" : "오른쪽")}", boss);
        }

        protected override void OnAttackStart()
        {
            decideEnd = Dur(data.clones.decisionTime);
            if (trueClone != null) trueClone.Hittable = true;
            if (fakeClone != null) fakeClone.Hittable = true;
        }

        void OnCloneHit(PhanesClone clone)
        {
            var s = data.clones;
            if (clone == trueClone)
            {
                trueClone = null;
                brain.RemoveMimic(clone);
                clone.Shatter(s.trueGemColor);
                if (decided) return; // 따라 쏘기 중에는 조용히 사라진다

                // 정답: 분신 소멸, 보스가 진짜 자리에서 드러나며 경직
                decided = true;
                if (fakeClone != null) { brain.RemoveMimic(fakeClone); fakeClone.Dismiss(0.15f); fakeClone = null; }
                AbandonAll(blasts);
                motor.PlaceAt(clone.transform.position);
                motor.SetHidden(false, 0.1f);
                if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 환영 분신 간파 — 보스 경직 {s.revealStagger:0.#}초", boss);
                boss.EnterGroggy(s.revealStagger, "환영 분신 간파");
                return;
            }

            if (clone == fakeClone)
            {
                // 오답: 자폭 + 암흑/침묵
                fakeClone = null;
                brain.RemoveMimic(clone);
                blastPoint = clone.Center;
                blastAt = T;
                clone.Shatter(s.fakeGemColor);
                PhanesFx.SpawnFx("PhantomBlast", PhanesFx.Glow, blastPoint, Vector2.one * s.explosionRadius * 2.4f,
                                 new Color(0.5f, 0.3f, 0.8f, 0.9f), PhanesFx.OrderAttack + 1, 0.05f).FadeOut(0.35f);
                if (brain.HasTarget) PhanesStatusEffects.ApplyDarkness(boss.target, s.darknessTime);
                PhanesStatusEffects.ApplySilence(s.silenceTime);
                if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 환영 분신 오답 — 자폭, 암흑/침묵", boss);
            }
        }

        protected override bool OnAttackTick(float t, float dt)
        {
            var s = data.clones;

            // 오답 자폭 판정
            bool blastDone = true;
            foreach (var h in blasts)
            {
                if (h.Finished) continue;
                if (blastAt < 0f) { blastDone = false; continue; }
                ResolveCircle(h, blastPoint, s.explosionRadius, out _);
                if (t >= blastAt + 0.15f) h.Finish(boss, run);
                else blastDone = false;
            }

            // 시간 초과: 분신이 남아 일반 공격을 따라 쏜다
            if (!decided && t >= decideEnd)
            {
                decided = true;
                timedOut = true;
                motor.PlaceAt(bossPos);
                motor.SetHidden(false, 0.2f);
                mimicEnd = t + Dur(s.mimicDuration);
                if (trueClone != null) brain.AddMimic(trueClone);
                if (fakeClone != null) brain.AddMimic(fakeClone);
                released = true; // 보스는 다음 패턴으로 — 분신은 뒤에서 남아 있는다
                if (brain.logToConsole) Debug.Log($"[{boss.bossId}] 환영 분신 시간 초과 — {s.mimicDuration:0.#}초 동안 일반 공격 복제", boss);
            }

            if (!decided) return false;
            if (timedOut && t < mimicEnd && (trueClone != null || fakeClone != null)) return false;
            if (blastAt >= 0f && !blastDone) return false;
            // 오답 자폭이 아직 안 터졌으면 그 판정은 버린다 (자폭은 가짜를 때렸을 때만 생긴다)
            if (blastAt < 0f) AbandonAll(blasts);
            return true;
        }

        protected override void OnCleanup(bool interrupted)
        {
            AbandonAll(blasts);
            if (trueClone != null) { brain.RemoveMimic(trueClone); trueClone.Dismiss(); trueClone = null; }
            if (fakeClone != null) { brain.RemoveMimic(fakeClone); fakeClone.Dismiss(); fakeClone = null; }
            // 숨은 채로 끊겼으면 원래 자리에서 보이게 (그로기 추락은 PhanesBrain이 맡는다)
            if (motor.IsHidden)
            {
                if (!decided) motor.PlaceAt(bossPos);
                motor.ForceVisible();
            }
        }
    }
}
