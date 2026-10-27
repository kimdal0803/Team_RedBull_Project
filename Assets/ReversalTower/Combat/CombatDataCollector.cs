using System;
using System.Collections.Generic;
using System.Linq;
using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Combat
{
    /// <summary>
    /// 전투 데이터 수집기 (최종보고서 4.4).
    /// 보스 이벤트(BossController)와 플레이어 이벤트(CombatEvents)를 구독해서 세 가지 기록을 만든다.
    ///   이벤트 로그 · 패턴별 집계 · 전투 요약
    ///
    /// 전투가 끝나면 BattleFinished로 묶음(BattleReport)을 내보낸다.
    /// 그 뒤 처리(CSV 저장 등)는 CombatLogBridge가 한다 — 수집기는 저장 방법을 모른다.
    ///
    /// 쓰는 법
    ///   보스 층 씬에 빈 오브젝트를 만들고 이 컴포넌트를 붙인 뒤 boss 칸에 보스를 넣는다.
    ///   autoStartOnPlay를 켜 두면 씬이 시작될 때 자동으로 기록을 시작한다.
    ///   중간에 로비로 나갈 때는 AbortBattle()을 부른다 (안 불러도 씬이 바뀌면 '포기'로 기록된다).
    /// </summary>
    public class CombatDataCollector : MonoBehaviour
    {
        /// <summary>전투 한 판이 끝났을 때. CombatLogBridge·DDA가 구독한다.</summary>
        public static event Action<BattleReport> BattleFinished;
        /// <summary>기록을 시작한 직후 (보스가 첫 패턴을 쓰기 전). DDA 매니저가 여기서 이번 전투의 단계를 건다.</summary>
        public static event Action<CombatDataCollector> BattleBegan;
        /// <summary>지금 기록 중인 수집기 (없으면 null)</summary>
        public static CombatDataCollector Active { get; private set; }

        [Tooltip("기록할 보스")]
        public BossController boss;
        [Tooltip("씬이 시작되면 자동으로 기록 시작")]
        public bool autoStartOnPlay = true;
        [Tooltip("실험 조건: DDA 적용 여부. DDA 연결 전에는 끈 채로 둔다")]
        public bool ddaEnabled = false;
        [Tooltip("이벤트 로그를 전부 남길지 (끄면 요약·집계만)")]
        public bool keepEventLog = true;
        public bool logToConsole = true;

        public bool IsRecording { get; private set; }
        public BattleContext Context { get; private set; }
        /// <summary>지금 기록 중인 전투의 ID (기록 중이 아니면 마지막 전투)</summary>
        public string CurrentBattleId => summary != null ? summary.battleId : null;

        BattleSummary summary;
        DdaBattleLog ddaLog;
        readonly Dictionary<string, PatternStats> patterns = new Dictionary<string, PatternStats>();
        readonly List<CombatLogEntry> events = new List<CombatLogEntry>();
        float startTime;
        float playerHp = 100f;
        static bool quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BattleFinished = null;
            BattleBegan = null;
            Active = null;
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        static void OnQuitting() => quitting = true;

        void Start()
        {
            if (autoStartOnPlay && boss != null)
                BeginBattle(BattleContextFactory.Create(boss.bossId, ddaEnabled), boss);
        }

        void OnDestroy()
        {
            if (!IsRecording) return;
            if (quitting) Unsubscribe();       // 게임 종료 — 기록하지 않음
            else EndBattle(BattleEndReason.Quit); // 씬 전환 — 포기로 기록
        }

        // ─────────────────────────── 시작·끝 ───────────────────────────

        /// <summary>
        /// 기록 시작. autoStartOnPlay를 켜 두면 Start()에서 자동으로 부른다.
        /// 게임 흐름 코드에서 직접 부를 거면 autoStartOnPlay를 끈다.
        /// </summary>
        public void BeginBattle(BattleContext context, BossController target)
        {
            if (IsRecording)
            {
                if (HasActivity()) EndBattle(BattleEndReason.Quit);
                else DiscardCurrent(); // 아무 일도 없던 기록(자동 시작 직후 등)은 도전으로 치지 않는다
            }
            if (target == null)
            {
                Debug.LogWarning("[Collector] 보스가 없어 기록을 시작하지 않습니다.", this);
                return;
            }

            boss = target;
            Context = context ?? new BattleContext { bossId = target.bossId };
            patterns.Clear();
            events.Clear();
            ddaLog = null;
            startTime = Time.time;
            playerHp = 100f;

            summary = new BattleSummary
            {
                battleId = NewBattleId(),
                participantId = Context.participantId,
                bossId = string.IsNullOrEmpty(Context.bossId) ? target.bossId : Context.bossId,
                attemptNo = Context.attemptNo,
                ddaEnabled = Context.ddaEnabled,
                combatPowerIndex = Context.combatPowerIndex,
                startedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                buildVersion = Application.version,
            };

            Subscribe();
            IsRecording = true;
            Active = this;
            if (logToConsole) Debug.Log($"[Collector] 기록 시작 {summary.battleId} — {summary.bossId} {summary.attemptNo}번째 도전, DDA {(summary.ddaEnabled ? "적용" : "미적용")}", this);

            var began = BattleBegan;
            if (began != null)
            {
                foreach (Action<CombatDataCollector> handler in began.GetInvocationList())
                {
                    try { handler(this); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        /// <summary>
        /// DDA가 이번 전투에 건 단계 · 문맥 · 예측을 받아 둔다 (DDA 설계서 11장 ④).
        /// 전투가 끝날 때 전투 요약과 패턴별 집계에 옮겨 적는다.
        /// </summary>
        public void SetDdaLog(DdaBattleLog log) => ddaLog = log;

        /// <summary>중간 포기 (일시정지 → 로비 등)</summary>
        public void AbortBattle() => EndBattle(BattleEndReason.Quit);

        public void EndBattle(BattleEndReason reason)
        {
            if (!IsRecording) return;
            IsRecording = false;
            Unsubscribe();
            if (Active == this) Active = null;

            summary.cleared = reason == BattleEndReason.BossDead;
            summary.endReason = reason.ToString();
            summary.durationSec = Time.time - startTime;
            if (boss != null) summary.phaseReached = Mathf.Max(summary.phaseReached, boss.Phase);
            AddLog("BattleEnd", result: reason.ToString());
            ApplyDdaLog();
            foreach (var stats in patterns.Values) stats.ComputeResponse();

            var report = new BattleReport
            {
                Summary = summary,
                Patterns = patterns.Values.ToList(),
                Events = new List<CombatLogEntry>(events),
            };

            if (logToConsole)
                Debug.Log($"[Collector] 기록 끝 {summary.battleId} — {summary.endReason}, {summary.durationSec:0.0}초, 피격 {summary.hitCount}, 패링 {summary.parrySuccess}/{summary.parryChances}, 받은 피해 {summary.damageTaken:0}", this);
            Publish(report);
        }

        /// <summary>BattleFinished를 부른다. 구독자 하나가 예외를 내도 나머지는 받는다.</summary>
        public static void Publish(BattleReport report)
        {
            var handlers = BattleFinished;
            if (handlers == null) return;
            foreach (Action<BattleReport> handler in handlers.GetInvocationList())
            {
                try { handler(report); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        // ─────────────────────────── 구독 ───────────────────────────

        void Subscribe()
        {
            boss.PatternStarted += OnPatternStarted;
            boss.HitResolved += OnHitResolved;
            boss.PatternEnded += OnPatternEnded;
            boss.PhaseChanged += OnPhaseChanged;
            boss.GroggyStarted += OnGroggy;
            boss.Damaged += OnBossDamaged;
            boss.Died += OnBossDied;

            CombatEvents.SkillUsed += OnSkillUsed;
            CombatEvents.PotionUsed += OnPotionUsed;
            CombatEvents.UltimateUsed += OnUltimateUsed;
            CombatEvents.PlayerHpChanged += OnPlayerHpChanged;
            CombatEvents.PlayerDied += OnPlayerDied;
        }

        void Unsubscribe()
        {
            if (boss != null)
            {
                boss.PatternStarted -= OnPatternStarted;
                boss.HitResolved -= OnHitResolved;
                boss.PatternEnded -= OnPatternEnded;
                boss.PhaseChanged -= OnPhaseChanged;
                boss.GroggyStarted -= OnGroggy;
                boss.Damaged -= OnBossDamaged;
                boss.Died -= OnBossDied;
            }
            CombatEvents.SkillUsed -= OnSkillUsed;
            CombatEvents.PotionUsed -= OnPotionUsed;
            CombatEvents.UltimateUsed -= OnUltimateUsed;
            CombatEvents.PlayerHpChanged -= OnPlayerHpChanged;
            CombatEvents.PlayerDied -= OnPlayerDied;
        }

        // ─────────────────────────── 보스 이벤트 ───────────────────────────

        void OnPatternStarted(PatternRun run)
        {
            var stats = GetStats(run.PatternId);
            stats.uses++;
            if (run.Data.telegraphTime > 0f)
            {
                // 속도 배율을 걷어낸 선딜 배율 = DDA가 적용한 telegraphScale
                stats.telegraphScaleSum += run.Telegraph * run.SpeedMultiplier / run.Data.telegraphTime;
                stats.telegraphScaleCount++;
            }
            AddLog("PatternStart", run.PatternId);
        }

        void OnHitResolved(PatternRun run, HitEvent e, HitResult result)
        {
            var stats = GetStats(run.PatternId);
            stats.strikes++;

            if (e.Source != null && e.Source.parryable)
            {
                stats.parryChances++;
                summary.parryChances++;
            }
            if (e.Source != null && boss != null)
            {
                float baseDamage = boss.attackPower * e.Source.damageCoefficient;
                if (baseDamage > 0f)
                {
                    stats.damageScaleSum += e.Damage / baseDamage;
                    stats.damageScaleCount++;
                }
            }

            // 받은 피해는 보스 쪽 계산값을 쓴다 (방어력이 없는 설계라 플레이어가 실제로 받은 값과 같다).
            // 플레이어 쪽에 피해 감소가 생기면 플레이어가 실제 값을 따로 알려주도록 바꾼다.
            switch (result)
            {
                case HitResult.Hit:
                    stats.hitCount++;
                    stats.damageTaken += e.Damage;
                    summary.hitCount++;
                    summary.damageTaken += e.Damage;
                    break;
                case HitResult.Parried:
                    stats.parrySuccess++;
                    summary.parrySuccess++;
                    break;
                case HitResult.Ignored:
                    stats.ignoredCount++;
                    summary.ignoredCount++;
                    break;
                case HitResult.Missed: // 판정 밖으로 피함 + 대시 무적으로 흘림 (PlayerBossHitAdapter)
                    stats.dodgeCount++;
                    summary.dodgeCount++;
                    break;
            }
            AddLog("HitResult", run.PatternId, e.Index, result.ToString(), result == HitResult.Hit ? e.Damage : 0f);
        }

        void OnPatternEnded(PatternRun run, bool interrupted)
        {
            if (interrupted) GetStats(run.PatternId).interrupted++;
        }

        void OnPhaseChanged(int phase)
        {
            summary.phaseReached = Mathf.Max(summary.phaseReached, phase);
            AddLog("PhaseChange", value: phase);
        }

        void OnGroggy(float duration, string reason) => AddLog("Groggy", value: duration, skill: reason);

        void OnBossDamaged(float dealt, string cause)
        {
            summary.damageDealt += dealt;
            AddLog("Damaged", value: dealt, skill: cause);
        }

        void OnBossDied() => EndBattle(BattleEndReason.BossDead);

        // ─────────────────────────── 플레이어 이벤트 ───────────────────────────

        void OnSkillUsed(string skill)
        {
            summary.skillUsed++;
            AddLog("SkillUse", skill: skill);
        }

        void OnPotionUsed()
        {
            summary.potionUsed++;
            AddLog("PotionUse", skill: "Potion");
        }

        void OnUltimateUsed(string ultimateId)
        {
            summary.ultimateUsed++;
            AddLog("UltimateUse", skill: ultimateId);
        }

        void OnPlayerHpChanged(float hp) => playerHp = hp;

        void OnPlayerDied() => EndBattle(BattleEndReason.PlayerDead);

        // ─────────────────────────── 내부 ───────────────────────────

        void ApplyDdaLog()
        {
            if (ddaLog == null) return;
            summary.experimentGroup = ddaLog.group ?? "";
            summary.ddaPolicy = ddaLog.policy ?? "none";
            summary.priorVersion = ddaLog.priorVersion ?? "";
            summary.protectionApplied = ddaLog.protectionApplied;

            foreach (var stats in patterns.Values)
            {
                if (stats.patternId == null || !ddaLog.patterns.TryGetValue(stats.patternId, out var p) || p == null) continue;
                stats.ddaStep = p.step;
                stats.ddaReason = p.reason ?? "";
                if (p.ctx != null && p.ctx.Length >= 5)
                {
                    stats.ctx1 = p.ctx[0];
                    stats.ctx2 = p.ctx[1];
                    stats.ctx3 = p.ctx[2];
                    stats.ctx4 = p.ctx[3];
                    stats.ctx5 = p.ctx[4];
                }
                stats.ddaPredicted = p.predicted;
                stats.ddaUncertainty = p.uncertainty;
            }
        }

        bool HasActivity() =>
            patterns.Count > 0 || summary.damageDealt > 0f || summary.skillUsed > 0 || summary.potionUsed > 0 || summary.ultimateUsed > 0;

        void DiscardCurrent()
        {
            IsRecording = false;
            Unsubscribe();
            if (Active == this) Active = null;
        }

        PatternStats GetStats(string patternId)
        {
            if (string.IsNullOrEmpty(patternId)) patternId = "(no id)";
            if (!patterns.TryGetValue(patternId, out var stats))
            {
                stats = new PatternStats { battleId = summary.battleId, bossId = summary.bossId, patternId = patternId };
                patterns[patternId] = stats;
            }
            return stats;
        }

        void AddLog(string evt, string patternId = "", int hitIndex = -1, string result = "", float value = 0f, string skill = "")
        {
            if (!keepEventLog || summary == null) return;
            events.Add(new CombatLogEntry
            {
                time = Time.time - startTime,
                battleId = summary.battleId,
                evt = evt,
                patternId = patternId ?? "",
                hitIndex = hitIndex,
                result = result ?? "",
                value = value,
                skill = skill ?? "",
                phase = boss != null ? boss.Phase : 0,
                playerHp = playerHp,
                bossHp = boss != null ? boss.Hp : 0f,
            });
        }

        static string NewBattleId() =>
            "B" + DateTime.UtcNow.ToString("yyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
    }
}
