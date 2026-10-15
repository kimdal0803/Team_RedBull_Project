namespace ReversalTower.Boss
{
    // 흐름:  Idle → Telegraph → Attack → Recovery → Idle …
    //        패링·기믹 파훼 → Groggy → Idle
    //        체력 조건 → (진행 중 패턴이 끝난 뒤) PhaseChange → Idle
    //        체력 0 → Dead (어느 상태에서든)

    /// <summary>대기: 다음 패턴을 고른다. 쓸 수 있는 패턴이 없으면 플레이어 쪽으로 걷는다.</summary>
    public class IdleState : BossState
    {
        public override string Name => "Idle";
        public IdleState(BossController boss) : base(boss) { }

        public override void Tick(float dt)
        {
            base.Tick(dt);

            // 체력 조건을 넘었으면 패턴을 고르기 전에 페이즈부터 바꾼다
            if (boss.HasPendingPhaseTrigger)
            {
                boss.Fsm.Change(boss.PhaseChange);
                return;
            }

            // 강제 패턴(페이즈 전환 연출, 기믹)은 대기 없이 바로
            if (!boss.HasForcedPattern && timer < boss.IdleTime) return;

            var next = boss.PickNextPattern();
            if (next != null)
            {
                boss.BeginPattern(next);
                boss.Fsm.Change(boss.Telegraph);
                return;
            }

            boss.ChaseTarget(dt);
        }
    }

    /// <summary>선딜(Telegraph): 공격 범위·방향을 미리 보여주는 준비 동작.</summary>
    public class TelegraphState : BossState
    {
        public override string Name => "Telegraph";
        public TelegraphState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            base.Enter();
            boss.PlayAnim(boss.CurrentRun.Data.telegraphAnimTrigger);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            boss.AdvanceRunClock(dt); // 패링 신호는 선딜 중에도 나갈 수 있다 (판정 0.3초 전)
            if (timer >= boss.CurrentRun.Telegraph)
                boss.Fsm.Change(boss.Attack);
        }
    }

    /// <summary>공격(Attack): 패턴 실행기에 넘겨 판정·이동을 처리한다.</summary>
    public class AttackState : BossState
    {
        PatternExecutor executor;

        public override string Name => "Attack";
        public AttackState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            base.Enter();
            var run = boss.CurrentRun;
            boss.PlayAnim(run.Data.attackAnimTrigger);
            executor = boss.GetExecutor(run.Data.kind);
            executor.Begin(boss, run);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            boss.AdvanceRunClock(dt);

            bool done = executor.Tick(timer, dt);

            // 판정 도중 패링·기믹으로 그로기가 되면 이미 다른 상태로 바뀌어 있다
            if (boss.Fsm.Current != this) return;
            if (!done) return;

            // 패링 가능한 타를 전부 패링했으면 후딜 대신 그로기
            if (boss.TryFullParryGroggy()) return;

            boss.Fsm.Change(boss.Recovery);
        }

        public override void Exit()
        {
            executor?.End();
            executor = null;
        }
    }

    /// <summary>후딜(Recovery): 공격 후 잠시 빈틈.</summary>
    public class RecoveryState : BossState
    {
        public override string Name => "Recovery";
        public RecoveryState(BossController boss) : base(boss) { }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (timer < boss.CurrentRun.Recovery) return;

            boss.EndPattern(false);
            if (boss.Fsm.Current == this) boss.Fsm.Change(boss.Idle);
        }
    }

    /// <summary>그로기·경직: 패링당하거나 기믹이 파훼되어 아무 행동도 못 하는 상태 (프리딜 구간).</summary>
    public class GroggyState : BossState
    {
        public float Duration;

        public override string Name => "Groggy";
        public GroggyState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            base.Enter();
            boss.PlayAnim(boss.groggyAnimTrigger);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (timer >= Duration) boss.Fsm.Change(boss.Idle);
        }
    }

    /// <summary>
    /// 페이즈 전환: 페이즈 번호·속도 배율을 바꾸고 전환 패턴(연출, 기믹)을 강제 대기열에 넣는다.
    /// 전환 연출 자체는 일반 패턴처럼 Telegraph → Attack → Recovery로 실행된다.
    /// </summary>
    public class PhaseChangeState : BossState
    {
        public override string Name => "PhaseChange";
        public PhaseChangeState(BossController boss) : base(boss) { }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            boss.ApplyPendingPhaseTriggers();
            boss.Fsm.Change(boss.Idle);
        }
    }

    /// <summary>사망: 전투 종료.</summary>
    public class DeadState : BossState
    {
        public override string Name => "Dead";
        public DeadState(BossController boss) : base(boss) { }

        public override void Enter()
        {
            base.Enter();
            boss.PlayAnim(boss.deathAnimTrigger);
        }

        public override void Tick(float dt) { }
    }
}
