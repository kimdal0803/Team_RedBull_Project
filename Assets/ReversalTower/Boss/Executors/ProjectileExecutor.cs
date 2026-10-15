using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 날아가는 물체를 띄우는 패턴 (아라쿠스 방패 투척 후 대쉬, 방패 던지고 내려찍기).
    ///
    /// DashExecutor를 물려받아서, 던지기와 이동을 한 패턴에서 같이 쓸 수 있다.
    ///   - dash.distance가 0이면 이동하지 않고 던지기만 한다 (방패 던지고 내려찍기)
    ///   - dash.distance가 있으면 던진 뒤 대쉬한다 (방패 투척 후 대쉬)
    ///
    /// 판정 나눠 갖기
    ///   hits 중 carrier == Projectile 인 것은 투사체가 들고 간다 (여기서는 건너뛴다).
    ///   carrier == Boss 인 것은 평소처럼 보스 몸 기준으로 처리한다 (대쉬 베기, 내려찍기).
    ///
    /// 투사체의 수명
    ///   한 번 날아간 투사체는 패턴이 끝난 뒤에도 스스로 날아가다 돌아와 사라진다.
    ///   보스가 죽으면 같이 사라진다. 패링·그로기로 패턴이 끊겨도 이미 던진 방패는 돌아온다.
    /// </summary>
    public class ProjectileExecutor : DashExecutor
    {
        readonly List<HitEvent> carried = new List<HitEvent>();
        bool spawned;

        public override void Begin(BossController boss, PatternRun run)
        {
            base.Begin(boss, run);
            spawned = false;

            carried.Clear();
            foreach (var e in run.Hits)
                if (e.Source != null && e.Source.carrier == HitCarrier.Projectile)
                    carried.Add(e);

            if (carried.Count == 0)
                Debug.LogWarning($"[{boss.bossId}] {run.PatternId}: kind가 Projectile인데 carrier가 Projectile인 판정이 없습니다. 던질 것이 없어 이동만 합니다.", boss);
        }

        protected override bool HandlesHit(in HitEvent e)
        {
            // 투사체가 들고 가는 판정은 보스 몸 기준으로 처리하지 않는다
            return e.Source == null || e.Source.carrier != HitCarrier.Projectile;
        }

        public override bool Tick(float attackTime, float dt)
        {
            var s = run.Data.projectile;
            if (!spawned && carried.Count > 0 && attackTime >= s.spawnTime / run.SpeedMultiplier)
            {
                spawned = true;
                BossProjectile.Spawn(boss, run, carried);
            }
            return base.Tick(attackTime, dt);
        }
    }
}
