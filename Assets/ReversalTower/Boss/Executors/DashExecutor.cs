using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 이동하면서 판정. 판정 박스는 보스 위치를 따라 움직인다.
    /// 방패 돌진, 돌진 기믹(벽·오브젝트에 막히면 그로기), 퀵스텝(backward)에 쓴다.
    /// </summary>
    public class DashExecutor : PatternExecutor
    {
        float moved;
        bool stopped;

        public override void Begin(BossController boss, PatternRun run)
        {
            base.Begin(boss, run);
            moved = 0f;
            stopped = false;
        }

        public override bool Tick(float attackTime, float dt)
        {
            var d = run.Data.dash;
            float start = d.startTime / run.SpeedMultiplier;
            float duration = Mathf.Max(0.01f, d.duration / run.SpeedMultiplier);

            if (!stopped && attackTime >= start && moved < d.distance)
            {
                float step = Mathf.Min(d.distance / duration * dt, d.distance - moved);
                int dir = d.backward ? -1 : 1;
                var block = boss.CheckDashBlock(step, dir, d);

                if (block == DashBlock.GroggyObject && d.groggyTime > 0f)
                {
                    stopped = true;
                    boss.NotifyDashBlocked(run, true);
                    boss.EnterGroggy(d.groggyTime, "돌진 저지");
                    return true;
                }

                if (block != DashBlock.None)
                {
                    stopped = true;
                    boss.NotifyDashBlocked(run, false);
                }
                else
                {
                    boss.MoveHorizontal(step * dir * boss.Facing);
                    moved += step;
                }
            }

            return base.Tick(attackTime, dt);
        }
    }
}
