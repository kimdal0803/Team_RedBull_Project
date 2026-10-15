using System.Collections.Generic;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 공격 종류별 동작. FSM은 '지금 공격 중인지'만 알고, 판정·이동은 실행기가 맡는다.
    /// 기본 동작: PatternRun의 판정(Hits)을 시간에 맞춰 켜고, 켜져 있는 동안 대상과 겹치는지 검사한다.
    ///
    /// 새 공격 종류를 만들 때:
    ///   1) PatternKind에 이름 추가
    ///   2) 이 클래스를 상속해 Tick에서 이동·투사체 생성 등을 하고 base.Tick으로 판정 처리
    ///   3) BossController.Awake의 RegisterExecutor에 등록
    /// </summary>
    public abstract class PatternExecutor
    {
        protected BossController boss;
        protected PatternRun run;

        int nextHit;
        readonly List<ActiveHit> active = new List<ActiveHit>();

        class ActiveHit
        {
            public HitEvent Event;
            public readonly HashSet<IBossAttackReceiver> Touched = new HashSet<IBossAttackReceiver>();
            public bool AnyContact;
        }

        public virtual void Begin(BossController boss, PatternRun run)
        {
            this.boss = boss;
            this.run = run;
            nextHit = 0;
            active.Clear();
        }

        /// <param name="attackTime">공격 시작부터 흐른 시간(초)</param>
        /// <returns>공격 단계가 끝났으면 true</returns>
        public virtual bool Tick(float attackTime, float dt)
        {
            ProcessHits(attackTime);
            if (run.Interrupted) return true;
            return attackTime >= run.AttackDuration && nextHit >= run.Hits.Count && active.Count == 0;
        }

        /// <summary>공격 단계를 벗어날 때 (정상 종료든 중단이든) 정리. 이펙트 제거 등.</summary>
        public virtual void End()
        {
            active.Clear();
        }

        /// <summary>
        /// 이 판정을 실행기가 직접(보스 몸 기준으로) 처리할지.
        /// false를 돌려주면 기본 처리에서 건너뛴다 — 다른 무언가(투사체)가 그 판정을 들고 간다는 뜻이다.
        /// </summary>
        protected virtual bool HandlesHit(in HitEvent e)
        {
            return e.Source == null || e.Source.carrier == HitCarrier.Boss;
        }

        protected void ProcessHits(float attackTime)
        {
            // 시간이 된 판정을 켠다 (다른 담당자가 들고 가는 판정은 건너뛴다)
            while (nextHit < run.Hits.Count && run.Hits[nextHit].Time <= attackTime)
            {
                var e = run.Hits[nextHit];
                nextHit++;
                if (!HandlesHit(e)) continue;
                active.Add(new ActiveHit { Event = e });
            }

            // 켜져 있는 판정마다 겹치는 대상 검사 (같은 대상은 판정 하나당 한 번만)
            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                if (boss.ResolveHit(run, a.Event, a.Touched)) a.AnyContact = true;
                if (run.Interrupted)
                {
                    active.Clear();
                    return;
                }
                if (attackTime >= a.Event.EndTime)
                {
                    if (!a.AnyContact) boss.ReportMiss(run, a.Event); // 아무도 안 맞음 = 회피
                    active.RemoveAt(i);
                    i--;
                }
            }
        }
    }

    /// <summary>제자리 판정. 기본 동작 그대로.</summary>
    public class MeleeExecutor : PatternExecutor { }
}
