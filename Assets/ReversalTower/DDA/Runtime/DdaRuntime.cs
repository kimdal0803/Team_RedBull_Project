using System;
using System.Collections.Generic;
using System.Text;
using ReversalTower.Boss;
using ReversalTower.Combat;
using UnityEngine;

namespace ReversalTower.DDA
{
    /// <summary>
    /// DDA 한 바퀴를 게임에 잇는 곳 (DDA 설계서 3 · 11장).
    ///
    ///   전투 시작 (수집기 BattleBegan → DDAManager → Prepare)
    ///     ① 상태 불러오기 ② 패턴마다 단계 고르기 ③ boss.SetTuning ④ 수집기에 단계 · 문맥 · 예측 넘기기
    ///   전투 끝 (수집기 BattleFinished → OnBattleFinished)
    ///     ⑥ 결과로 모델 · 기록 갱신 ⑦ 3연패 카운트 ⑧ 저장
    ///
    /// 전투 끝 처리를 MonoBehaviour가 아니라 여기(정적 클래스)서 하는 이유:
    ///   중간에 로비로 나가면 씬이 바뀌면서 수집기가 '포기'로 전투를 끝내는데, 그때 DDAManager가 먼저 사라졌을 수 있다.
    ///   포기 전투도 유효 타 3 이상인 패턴은 배워야 한다 (설계서 8.2).
    /// </summary>
    public static class DdaRuntime
    {
        /// <summary>전투 한 판 동안의 DDA 정보.</summary>
        public sealed class Session
        {
            public string BossId;
            public string BattleId;
            public BossController BossRef;
            public CombatDataCollector Collector;
            public DDAArmTable ArmTable;
            public DdaSettings Settings;
            public DdaState State;
            public DdaDecisionSet Decisions;
            public DdaBattleLog Log;
            public readonly Dictionary<string, BossPatternData> Data = new Dictionary<string, BossPatternData>();
            /// <summary>전투가 끝나 학습까지 마쳤는지</summary>
            public bool Finished;
            /// <summary>이번 전투로 모델을 업데이트한 패턴 수</summary>
            public int UpdatedPatterns;
            public bool LogToConsole = true;

            public string DisplayName(string patternId)
            {
                if (patternId != null && Data.TryGetValue(patternId, out var d) && d != null && !string.IsNullOrEmpty(d.displayName))
                    return d.displayName;
                return patternId;
            }
        }

        /// <summary>지금(또는 마지막) 전투의 DDA 정보. 개발용 패널이 읽는다.</summary>
        public static Session Current { get; private set; }

        static DDAArmTable defaultTable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            DdaStateStore.ClearCache();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Wire()
        {
            CombatDataCollector.BattleFinished -= OnBattleFinished;
            CombatDataCollector.BattleFinished += OnBattleFinished;
        }

        // ─────────────────────────── 전투 시작 ───────────────────────────

        public static Session Prepare(DDAManager manager, CombatDataCollector collector, BossController boss)
        {
            if (manager == null) return null;
            if (boss == null)
            {
                Debug.LogWarning("[DDA] 보스를 찾지 못해 이번 전투에는 DDA를 걸지 않습니다.", manager);
                return null;
            }

            string bossId = string.IsNullOrEmpty(boss.bossId) ? "Boss" : boss.bossId;
            var state = DdaStateStore.Load(bossId);
            var settings = manager.BuildSettings();
            ApplyPrior(state, manager.priorFile, bossId);

            var session = new Session
            {
                BossId = bossId,
                BattleId = collector != null ? collector.CurrentBattleId : null,
                BossRef = boss,
                Collector = collector,
                ArmTable = manager.armTable != null ? manager.armTable : DefaultTable(),
                Settings = settings,
                State = state,
                LogToConsole = manager.logToConsole,
            };

            var infos = CollectPatterns(boss, session.Data);
            DdaDecisionSet set;
            try
            {
                set = DdaEngine.Decide(state, infos, settings);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                set = AllZero(infos, settings);
            }
            session.Decisions = set;

            foreach (var d in set.Decisions) ApplyTuning(session, d);

            session.Log = BuildLog(set, settings, state);
            if (collector != null) collector.SetDdaLog(session.Log);

            Current = session;
            if (session.LogToConsole) Debug.Log(Describe(session), manager);
            return session;
        }

        static void ApplyPrior(DdaState state, TextAsset priorFile, string bossId)
        {
            if (priorFile == null) return;
            if (!DdaPriorFile.TryParse(priorFile.text, out var prior, out string error))
            {
                Debug.LogWarning($"[DDA] 초기값 파일 {priorFile.name}을 읽지 못했습니다: {error} — 규칙 기반으로 대신 고릅니다.");
                return;
            }
            if (!string.IsNullOrEmpty(prior.bossId) && prior.bossId != bossId)
                Debug.LogWarning($"[DDA] 초기값 파일 {priorFile.name}은 {prior.bossId}용인데 {bossId}에 씁니다.");
            if (state.hasModel && state.priorVersion == prior.version) return;

            if (state.hasModel && state.modelUpdates > 0)
                Debug.LogWarning($"[DDA] 초기값이 {state.priorVersion} → {prior.version}로 바뀌어 {bossId} 모델을 새로 시작합니다 " +
                                 $"(이전 학습 {state.modelUpdates}회는 버립니다).");
            state.SetModel(prior.A, prior.b, prior.version);
        }

        /// <summary>보스의 일반 패턴 + 페이즈 전환 강제 패턴. 강제 패턴 · 타격 없는 패턴 · ddaExcluded는 제외로 표시한다.</summary>
        static List<DdaPatternInfo> CollectPatterns(BossController boss, Dictionary<string, BossPatternData> data)
        {
            var list = new List<DdaPatternInfo>();
            void Add(BossPatternData p, bool forced)
            {
                if (p == null || string.IsNullOrEmpty(p.patternId) || data.ContainsKey(p.patternId)) return;
                data[p.patternId] = p;
                list.Add(new DdaPatternInfo(p.patternId, forced || IsExcluded(p)));
            }

            if (boss.patterns != null)
                foreach (var p in boss.patterns) Add(p, false);
            if (boss.phaseTriggers != null)
                foreach (var t in boss.phaseTriggers)
                    if (t != null && t.forcedPatterns != null)
                        foreach (var p in t.forcedPatterns) Add(p, true);
            return list;
        }

        /// <summary>DDA가 조정하지 않는 패턴: ddaExcluded, 무작위 선택에서 빠진 강제 전용(weight 0), 타격이 없는 패턴.</summary>
        public static bool IsExcluded(BossPatternData p)
        {
            if (p == null) return true;
            if (p.ddaExcluded) return true;
            if (p.weight <= 0f) return true;
            if (p.hits == null) return true;
            foreach (var h in p.hits)
                if (h != null) return false;
            return true;
        }

        static void ApplyTuning(Session s, DdaDecision d)
        {
            if (s.BossRef == null || d == null) return;
            s.Data.TryGetValue(d.PatternId, out var data);
            s.BossRef.SetTuning(d.PatternId, d.Excluded ? null : s.ArmTable.ToTuning(d.Step, data));
        }

        static DdaBattleLog BuildLog(DdaDecisionSet set, DdaSettings settings, DdaState state)
        {
            var log = new DdaBattleLog
            {
                group = settings.Group == DdaGroup.Dda ? "dda" : "control",
                policy = set.PolicyName,
                priorVersion = state.hasModel ? state.priorVersion : "",
                protectionApplied = set.ProtectionApplied,
            };
            foreach (var d in set.Decisions) log.patterns[d.PatternId] = ToLog(d);
            return log;
        }

        static DdaPatternLog ToLog(DdaDecision d)
        {
            return new DdaPatternLog
            {
                step = d.Step,
                reason = d.Reason ?? "",
                ctx = d.Context != null && d.Context.Length >= DdaConstants.ContextSize
                    ? new[] { d.Context[1], d.Context[2], d.Context[3], d.Context[4], d.Context[5] }
                    : null,
                predicted = d.Predicted,
                uncertainty = d.Uncertainty,
            };
        }

        static DdaDecisionSet AllZero(List<DdaPatternInfo> infos, DdaSettings settings)
        {
            var set = new DdaDecisionSet { PolicyName = settings.Group == DdaGroup.Dda ? DdaPolicyNames.Of(settings.Policy) : DdaPolicyNames.Off };
            foreach (var p in infos)
                set.Decisions.Add(new DdaDecision
                {
                    PatternId = p.Id,
                    Excluded = p.Excluded,
                    Step = 0,
                    Reason = p.Excluded ? DdaReasons.Excluded : DdaReasons.Fallback,
                });
            return set;
        }

        static DDAArmTable DefaultTable()
        {
            if (defaultTable == null) defaultTable = DDAArmTable.CreateDefault();
            return defaultTable;
        }

        // ─────────────────────────── 전투 끝 ───────────────────────────

        static void OnBattleFinished(BattleReport report)
        {
            var s = Current;
            if (s == null || s.Finished || report == null || report.Summary == null) return;
            if (!string.IsNullOrEmpty(s.BattleId))
            {
                if (report.Summary.battleId != s.BattleId) return;
            }
            else if (report.Summary.bossId != s.BossId)
            {
                return;
            }
            s.Finished = true;

            var results = new List<DdaPatternResult>();
            if (report.Patterns != null)
                foreach (var ps in report.Patterns)
                    if (ps != null)
                        results.Add(new DdaPatternResult
                        {
                            PatternId = ps.patternId,
                            Strikes = ps.strikes,
                            Ignored = ps.ignoredCount,
                            Parry = ps.parrySuccess,
                            Dodge = ps.dodgeCount,
                        });

            int before = s.State.modelUpdates;
            try
            {
                DdaEngine.Learn(s.State, s.Decisions.Decisions, results, report.Summary.cleared, s.Settings);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }
            s.UpdatedPatterns = s.State.modelUpdates - before;
            DdaStateStore.Save(s.State);

            if (s.LogToConsole)
                Debug.Log($"[DDA] {s.BossId} 전투 끝 ({report.Summary.endReason}) — 모델 업데이트 {s.UpdatedPatterns}개 패턴, " +
                          $"끝낸 전투 {s.State.attempts}번, 연속 패배 {s.State.loseStreak}번. 저장: {DdaStateStore.PathFor(s.BossId)}");
        }

        // ─────────────────────────── 개발용 ───────────────────────────

        /// <summary>지금 전투의 한 패턴 단계를 바로 바꾼다 (개발용 패널, 로그 이유 forced).</summary>
        public static bool ForceStep(string patternId, int step)
        {
            var s = Current;
            if (s == null || s.Finished) return false;
            var d = s.Decisions.Find(patternId);
            if (d == null || d.Excluded) return false;

            d.Step = DdaConstants.ClampStep(step);
            d.Reason = DdaReasons.Forced;
            d.Predicted = -1.0;
            d.Uncertainty = -1.0;
            var model = s.State.GetModel();
            if (model != null && d.Context != null && s.Settings.IsActive && s.Settings.Policy == DdaPolicyKind.LinUcb)
            {
                var yhat = new double[5];
                var unc = new double[5];
                if (model.Predict(d.Context, yhat, unc))
                {
                    d.Predicted = yhat[DdaConstants.ArmIndex(d.Step)];
                    d.Uncertainty = unc[DdaConstants.ArmIndex(d.Step)];
                }
            }

            ApplyTuning(s, d);
            if (s.Log != null) s.Log.patterns[d.PatternId] = ToLog(d);
            return true;
        }

        /// <summary>이 보스의 DDA 기록을 지운다. 지금 전투는 배우지 않고, 다음 전투가 '첫 전투'가 된다.</summary>
        public static void ResetBoss(string bossId)
        {
            DdaStateStore.Delete(bossId);
            if (Current != null && Current.BossId == bossId) Current.Finished = true;
            Debug.Log($"[DDA] {bossId}의 DDA 기록을 지웠습니다. 다음 전투부터 처음(첫 전투)부터 시작합니다.");
        }

        public static string Describe(Session s)
        {
            var sb = new StringBuilder();
            var set = s.Decisions;
            sb.Append($"[DDA] {s.BossId} {s.State.attempts + 1}번째 전투 — 그룹 {(s.Settings.Group == DdaGroup.Dda ? "dda" : "control")}, 정책 {set.PolicyName}");
            sb.Append(s.State.hasModel ? $", 초기값 {s.State.priorVersion} (+{s.State.modelUpdates}회 학습)" : ", 초기값 없음");
            if (set.ProtectionApplied) sb.Append(", 3연패 보호");
            foreach (var d in set.Decisions)
            {
                if (d.Excluded) continue;
                sb.Append($"\n  {s.DisplayName(d.PatternId)}: {StepText(d.Step)} ({d.Reason}");
                if (d.Predicted >= 0) sb.Append($", 예측 {d.Predicted:0.00} ± {d.Uncertainty:0.00}");
                sb.Append(")");
            }
            return sb.ToString();
        }

        public static string StepText(int step) => step > 0 ? "+" + step : step.ToString();
    }
}
