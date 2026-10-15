using System;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 상태 하나. 들어갈 때 준비(Enter), 머무는 동안 진행(Tick), 나갈 때 정리(Exit).
    /// 상태는 '지금 어느 단계인지'만 관리하고, 수치는 PatternRun, 공격 동작은 실행기가 맡는다.
    /// </summary>
    public abstract class BossState
    {
        protected readonly BossController boss;
        protected float timer; // 이 상태에 들어온 뒤 흐른 시간

        public abstract string Name { get; }

        protected BossState(BossController boss)
        {
            this.boss = boss;
        }

        public virtual void Enter() { timer = 0f; }
        public virtual void Tick(float dt) { timer += dt; }
        public virtual void Exit() { }
    }

    public class BossStateMachine
    {
        public BossState Current { get; private set; }

        /// <summary>상태가 바뀔 때 (이전, 다음). 로그·디버그용.</summary>
        public event Action<BossState, BossState> Changed;

        public void Change(BossState next)
        {
            if (next == null) return;
            var prev = Current;
            prev?.Exit();
            Current = next;
            Changed?.Invoke(prev, next);
            next.Enter();
        }

        public void Tick(float dt)
        {
            Current?.Tick(dt);
        }
    }
}
