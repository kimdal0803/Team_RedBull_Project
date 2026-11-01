using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 판정 한 번(HitEvent)을 월드 좌표·투사체로 처리할 때 쓰는 기록.
    /// 같은 판정을 투사체 여러 개가 나눠 들 수 있다 (부채꼴 바늘, 별 모양 파편). 그래도 같은 대상은 한 번만 맞는다.
    /// 끝날 때까지 아무도 못 맞혔으면 '회피(Missed)'로 보고한다 — 보스 몸 판정과 같은 규칙이라 전투 기록·DDA가 그대로 읽는다.
    /// </summary>
    public class PhanesHit
    {
        public readonly HitEvent Event;
        public readonly HashSet<IBossAttackReceiver> Touched = new HashSet<IBossAttackReceiver>();
        public bool AnyContact;
        public bool Finished { get; private set; }
        public bool Started;     // 스킬이 이 판정을 시작했는지 (투사체를 쐈는지, 표식을 놓았는지)
        public bool Signaled;    // 패링 신호를 보냈는지
        public Vector2 Point;    // 스킬이 정한 위치 (표식 자리 등)
        int carriers;

        public PhanesHit(HitEvent e) { Event = e; }

        public void AddCarrier() => carriers++;

        /// <summary>투사체 하나가 사라졌다. 이 판정을 든 투사체가 다 사라지면 판정도 끝난다.</summary>
        public void ReleaseCarrier(BossController boss, PatternRun run)
        {
            carriers = Mathf.Max(0, carriers - 1);
            if (carriers == 0) Finish(boss, run);
        }

        /// <summary>판정 끝. 아무도 못 맞혔으면 회피로 보고한다 (보스가 죽었으면 보고하지 않는다).</summary>
        public void Finish(BossController boss, PatternRun run)
        {
            if (Finished) return;
            Finished = true;
            if (!AnyContact && boss != null && !boss.IsDead && run != null) boss.ReportMiss(run, Event);
        }

        /// <summary>판정이 끊겼다 (보스 그로기·사망, 결계 성공으로 폭발 취소). 회피로 보고하지 않는다.</summary>
        public void Abandon() { Finished = true; }
    }

    /// <summary>
    /// 파네스 패턴 한 번 실행. 스킬마다 이 클래스를 상속한다 (PhanesCasts_Phase1/2.cs).
    ///
    /// 흐름 (BossController의 Telegraph → Attack → Recovery와 맞물린다)
    ///   OnTelegraphStart   패턴이 골라진 순간 (선딜 시작). 경고 표시·조준 고정·순간이동을 여기서 한다.
    ///   OnTelegraphTick    선딜 동안 매 프레임
    ///   OnAttackStart      공격 시작
    ///   OnAttackTick       공격 중 매 프레임. true를 돌려주면 이 스킬의 일이 다 끝났다는 뜻
    ///   OnCleanup          끝·중단 때 정리 (경고·장판 그림 지우기)
    ///
    /// 뒤에서 계속하기 (기획서 '다른 공격과 동시 시전')
    ///   released = true로 두면 일이 남아 있어도 공격 단계를 끝내고 보스는 다음 패턴으로 넘어간다.
    ///   남은 일은 PhanesBrain이 매 프레임 OnAttackTick을 계속 불러 마무리한다 (천벌의 잔향, 영벌의 업화, 분신 따라 쏘기).
    /// </summary>
    public abstract class PhanesCast
    {
        protected PhanesBrain brain;
        protected BossController boss;
        protected PhanesMotor motor;
        protected PhanesArena arena;
        protected PatternRun run;
        protected PhanesPatternData data;
        protected readonly List<PhanesHit> hits = new List<PhanesHit>();
        protected float T;           // 공격 시작부터 흐른 시간(초)
        protected float telegraphT;  // 선딜 시작부터 흐른 시간(초)
        protected Vector2 homePosition;
        protected bool released;

        readonly List<PhanesFxObject> trackedFx = new List<PhanesFxObject>();
        readonly List<GameObject> trackedObjects = new List<GameObject>();
        bool wentHome;

        public PatternRun Run => run;
        public PhanesPatternData Data => data;
        public bool AttackStarted { get; private set; }
        public bool WorkDone { get; private set; }
        public bool Cleaned { get; private set; }
        public bool InBackground { get; private set; }

        /// <summary>화면 위쪽 가운데에 크게 띄울 글 (결계 카운트다운 등). 없으면 null.</summary>
        public virtual string HudText => null;

        // ───────────────────────────── PhanesBrain·실행기가 부른다 ─────────────────────────────

        internal void Setup(PhanesBrain owner, PatternRun r, PhanesPatternData d)
        {
            brain = owner;
            boss = owner.Boss;
            motor = owner.Motor;
            arena = owner.Arena;
            run = r;
            data = d;
            foreach (var e in r.Hits) hits.Add(new PhanesHit(e));
        }

        internal void BeginTelegraph()
        {
            homePosition = boss.transform.position;
            if (data.teleportBeforeChance > 0f && brain.Roll() < data.teleportBeforeChance)
                motor.Teleport(brain.PickTeleportPoint());
            OnTelegraphStart();
        }

        internal void TickTelegraph(float dt)
        {
            telegraphT += dt;
            OnTelegraphTick(telegraphT, dt);
        }

        internal void BeginAttack()
        {
            AttackStarted = true;
            T = 0f;
            OnAttackStart();
        }

        /// <returns>공격 단계를 끝내도 되면 true (일을 다 했거나, 뒤에서 계속하기로 했거나)</returns>
        internal bool TickAttack(float dt)
        {
            Step(dt);
            return WorkDone || released;
        }

        internal void Step(float dt)
        {
            if (WorkDone || Cleaned) return;
            T += dt;
            if (OnAttackTick(T, dt)) WorkDone = true;
        }

        internal void FixedStep(float dt)
        {
            if (!AttackStarted || WorkDone || Cleaned) return;
            OnFixedTick(dt);
        }

        internal void MoveToBackground()
        {
            InBackground = true;
            OnReleased();
            GoHome();
        }

        internal void Cleanup(bool interrupted)
        {
            if (Cleaned) return;
            Cleaned = true;
            OnCleanup(interrupted);
            foreach (var fx in trackedFx) if (fx != null) fx.Kill(0.15f);
            foreach (var go in trackedObjects) if (go != null) Object.Destroy(go);
            trackedFx.Clear();
            trackedObjects.Clear();
            // 끊겼으면(그로기·사망) 몸은 PhanesBrain이 바닥으로 떨어뜨리므로 여기서 움직이지 않는다
            if (!interrupted && !InBackground) GoHome();
        }

        // ───────────────────────────── 스킬이 채우는 곳 ─────────────────────────────

        protected virtual void OnTelegraphStart() { }
        protected virtual void OnTelegraphTick(float t, float dt) { }
        protected virtual void OnAttackStart() { }
        protected abstract bool OnAttackTick(float t, float dt);
        protected virtual void OnFixedTick(float dt) { }
        protected virtual void OnReleased() { }
        protected virtual void OnCleanup(bool interrupted) { }

        // ───────────────────────────── 도우미 ─────────────────────────────

        protected float Speed(float metersPerSecond) => metersPerSecond * run.SpeedMultiplier;
        protected float Dur(float seconds) => seconds / run.SpeedMultiplier;

        /// <summary>hits 배열 index번째 HitData에서 나온 판정들 (시간 순)</summary>
        protected List<PhanesHit> HitsOf(int sourceIndex)
        {
            var list = new List<PhanesHit>();
            foreach (var h in hits)
                if (data.SourceIndexOf(h.Event) == sourceIndex) list.Add(h);
            return list;
        }

        protected PhanesFxObject Track(PhanesFxObject fx)
        {
            if (fx != null) trackedFx.Add(fx);
            return fx;
        }

        protected GameObject Track(GameObject go)
        {
            if (go != null) trackedObjects.Add(go);
            return go;
        }

        protected bool ResolveRect(PhanesHit h, Rect r) => ResolveBox(h, r.center, r.size, 0f, out _);

        protected bool ResolveBox(PhanesHit h, Vector2 center, Vector2 size, float angle, out HitResult result, bool fromProjectile = false)
        {
            bool contact = boss.ResolveHitBox(run, h.Event, center, size, angle, h.Touched, out result, fromProjectile);
            if (contact) h.AnyContact = true;
            return contact;
        }

        protected bool ResolveCircle(PhanesHit h, Vector2 center, float radius, out HitResult result, bool fromProjectile = false)
        {
            bool contact = boss.ResolveHitCircle(run, h.Event, center, radius, h.Touched, out result, fromProjectile);
            if (contact) h.AnyContact = true;
            return contact;
        }

        /// <summary>
        /// 시간표대로 켜졌다 꺼지는 월드 판정 처리 (빛 기둥 다단 히트, 장판 틱).
        /// e.Time ~ e.EndTime 동안 shapeOf가 준 사각형으로 검사하고, 끝나면 회피 여부를 보고한다.
        /// </summary>
        /// <returns>목록의 판정이 모두 끝났으면 true</returns>
        protected bool ProcessTimeline(List<PhanesHit> list, float t, System.Func<PhanesHit, Rect[]> shapeOf, float timeOffset = 0f)
        {
            bool allDone = true;
            foreach (var h in list)
            {
                if (h.Finished) continue;
                float start = h.Event.Time + timeOffset;
                float end = h.Event.EndTime + timeOffset;
                if (t < start) { allDone = false; continue; }
                h.Started = true;
                var rects = shapeOf(h);
                if (rects != null)
                    foreach (var r in rects) ResolveRect(h, r);
                if (t >= end) h.Finish(boss, run);
                else allDone = false;
            }
            return allDone;
        }

        protected void AbandonAll(List<PhanesHit> list)
        {
            foreach (var h in list) if (!h.Finished) h.Abandon();
        }

        void GoHome()
        {
            if (wentHome) return;
            wentHome = true;
            if (!data.returnHomeAfter || boss.IsDead || motor.IsGrounded) return;
            motor.FlyTo(homePosition, Dur(0.45f));
        }
    }

    /// <summary>
    /// PatternKind.Scripted 실행기. 공격 단계를 지금 실행 중인 파네스 스킬에 넘긴다.
    /// PhanesBrain이 BossController에 등록한다.
    /// </summary>
    public class PhanesExecutor : PatternExecutor
    {
        readonly PhanesBrain brain;
        PhanesCast cast;

        public PhanesExecutor(PhanesBrain brain) { this.brain = brain; }

        public override void Begin(BossController boss, PatternRun run)
        {
            base.Begin(boss, run);
            cast = brain.CastFor(run);
            if (cast == null)
                Debug.LogWarning($"[{boss.bossId}] {run.PatternId}: 파네스 패턴 데이터(PhanesPatternData)가 아니라 실행할 스킬이 없습니다.", boss);
            else
                cast.BeginAttack();
        }

        public override bool Tick(float attackTime, float dt)
        {
            if (run.Interrupted) return true;
            if (cast == null) return attackTime >= run.AttackDuration;
            return cast.TickAttack(dt);
        }

        public override void End()
        {
            base.End();
            if (cast != null) brain.OnAttackPhaseEnded(cast, run.Interrupted);
            cast = null;
        }
    }
}
