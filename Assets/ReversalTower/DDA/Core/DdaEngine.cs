using System;
using System.Collections.Generic;

namespace ReversalTower.DDA
{
    /// <summary>실험 조건 (DDA 설계서 9장). 대조군은 항상 0단계이고 기록만 똑같이 남긴다.</summary>
    public enum DdaGroup
    {
        Dda,
        Control,
    }

    /// <summary>로그의 ddaReason 값 (9장 표).</summary>
    public static class DdaReasons
    {
        public const string Excluded = "excluded";   // 제외 패턴 (타격 없음 · 강제 패턴 · ddaExcluded)
        public const string Off = "off";             // 대조군이거나 정책이 off
        public const string First = "first";         // 이 보스 첫 전투
        public const string NoData = "noData";       // 지난 전투 유효 타 3 미만 → 그대로
        public const string Learned = "learned";     // 정책이 고름 (한 칸 제한 안)
        public const string Fallback = "fallback";   // LinUCB를 못 써서 규칙 기반으로 대신 (초기값 없음 · 모델 고장)
        public const string Protected = "protected"; // 3연패 보호로 한 칸 더 쉽게
        public const string Forced = "forced";       // 개발용 패널에서 강제로 바꿈
    }

    /// <summary>DDA 설정 (DDAManager가 채운다).</summary>
    public class DdaSettings
    {
        public DdaGroup Group = DdaGroup.Dda;
        public DdaPolicyKind Policy = DdaPolicyKind.LinUcb;
        public double Alpha = DdaConstants.DefaultAlpha;
        public double DefaultFailRate = DdaConstants.DefaultFailRate;
        /// <summary>Random 정책용. 비우면 새로 만든다.</summary>
        public System.Random Rng;

        public bool IsActive => Group == DdaGroup.Dda && Policy != DdaPolicyKind.Off;
    }

    /// <summary>DDA가 다루는 패턴 하나.</summary>
    public struct DdaPatternInfo
    {
        public string Id;
        public bool Excluded;

        public DdaPatternInfo(string id, bool excluded)
        {
            Id = id;
            Excluded = excluded;
        }
    }

    /// <summary>이번 전투에 패턴 하나에 무엇을 걸었는지. 로그에 그대로 남는다.</summary>
    public class DdaDecision
    {
        public string PatternId;
        public bool Excluded;
        public int Step;
        public string Reason;
        /// <summary>단계를 고를 때 본 문맥 x0~x5. 제외 패턴은 null</summary>
        public double[] Context;
        /// <summary>걸린 단계의 예측 대응 성공률 ŷ. LinUCB가 아니면 −1</summary>
        public double Predicted = -1.0;
        /// <summary>걸린 단계의 불확실성 u. LinUCB가 아니면 −1</summary>
        public double Uncertainty = -1.0;
    }

    public class DdaDecisionSet
    {
        public readonly List<DdaDecision> Decisions = new List<DdaDecision>();
        public bool ProtectionApplied;
        public string PolicyName = DdaPolicyNames.Off;

        public DdaDecision Find(string patternId)
        {
            for (int i = 0; i < Decisions.Count; i++)
                if (Decisions[i].PatternId == patternId) return Decisions[i];
            return null;
        }
    }

    /// <summary>전투가 끝난 뒤 패턴 하나의 결과 (수집기의 PatternStats에서 옮긴다).</summary>
    public struct DdaPatternResult
    {
        public string PatternId;
        public int Strikes;  // 날아온 타
        public int Ignored;  // 무적 등으로 무시된 타
        public int Parry;    // 패링 성공
        public int Dodge;    // 회피
    }

    /// <summary>
    /// DDA 한 바퀴의 두 단계 (DDA 설계서 3장).
    ///   Decide — 전투 직전: 패턴마다 문맥을 만들고 단계를 고른다 (안전장치 포함)
    ///   Learn  — 전투 직후: 결과로 모델 · 기록을 갱신한다
    /// 규칙은 ddasim/ref_engine.py와 한 줄씩 같다. 바꾸면 둘 다 바꾸고 정답 파일을 다시 만든다.
    /// Unity 없이 돌아가는 순수 C#이다.
    /// </summary>
    public static class DdaEngine
    {
        /// <summary>
        /// 패턴마다 단계를 고른다. 순서 (9장):
        ///   1. 제외 패턴 → 0  2. 대조군 · off → 0  3. 첫 전투 → 0  4. 지난 전투 유효 타 3 미만 → 그대로
        ///   5. 정책 (지금 단계 ±1, −2~+2). LinUCB를 못 쓰면 규칙 기반으로 대신
        ///   6. 3연패 보호 → 한 칸 더 쉽게 (random은 제외: 초기값 재료가 치우치지 않게)
        /// </summary>
        public static DdaDecisionSet Decide(DdaState state, IList<DdaPatternInfo> patterns, DdaSettings settings)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (settings == null) settings = new DdaSettings();

            var set = new DdaDecisionSet { PolicyName = settings.Group == DdaGroup.Dda ? DdaPolicyNames.Of(settings.Policy) : DdaPolicyNames.Off };

            var model = state.GetModel();
            if (model != null && !model.IsHealthy()) model = null;

            bool active = settings.IsActive;
            bool protect = active && settings.Policy != DdaPolicyKind.Random
                           && state.loseStreak >= DdaConstants.ProtectAfterLosses;
            bool logPrediction = active && settings.Policy == DdaPolicyKind.LinUcb && model != null;

            IDdaPolicy policy = CreatePolicy(settings, model, out bool usesFallback);
            var yhat = new double[5];
            var unc = new double[5];

            foreach (var p in patterns)
            {
                var ps = state.GetPattern(p.Id);
                int cur = DdaConstants.ClampStep(ps.step);
                var d = new DdaDecision { PatternId = p.Id, Excluded = p.Excluded };

                if (p.Excluded)
                {
                    d.Step = 0;
                    d.Reason = DdaReasons.Excluded;
                    set.Decisions.Add(d);
                    continue;
                }

                d.Context = BuildContext(ps, state.overall, cur, state.attempts, settings.DefaultFailRate);
                int step;
                string reason;

                if (!active)
                {
                    step = 0;
                    reason = DdaReasons.Off;
                }
                else if (state.attempts == 0)
                {
                    step = 0;
                    reason = DdaReasons.First;
                }
                else if (ps.lastEff < DdaConstants.MinStrikes)
                {
                    step = cur;
                    reason = DdaReasons.NoData;
                }
                else
                {
                    try
                    {
                        var input = new DdaPolicyInput { Context = d.Context, CurrentStep = cur, LastValidRate = ps.lastValidRate };
                        step = ClampMove(policy.Choose(input), cur);
                        reason = usesFallback ? DdaReasons.Fallback : DdaReasons.Learned;
                    }
                    catch (Exception)
                    {
                        // 그것도 안 되면 0단계 (9장 '고장 시 복귀')
                        step = 0;
                        reason = DdaReasons.Fallback;
                    }
                }

                if (protect)
                {
                    step = Math.Max(step - 1, DdaConstants.MinStep);
                    reason = DdaReasons.Protected;
                }

                d.Step = step;
                d.Reason = reason;

                if (logPrediction && model.Predict(d.Context, yhat, unc))
                {
                    int i = DdaConstants.ArmIndex(step);
                    d.Predicted = yhat[i];
                    d.Uncertainty = unc[i];
                }
                set.Decisions.Add(d);
            }

            set.ProtectionApplied = protect;
            return set;
        }

        /// <summary>
        /// 전투 결과로 기록과 모델을 갱신한다 (클리어 · 사망 · 포기 모두).
        /// 유효 타 3 이상인 패턴만 모델을 업데이트한다. 모델은 DDA 그룹이고 정책이 off가 아닐 때만 배운다.
        /// </summary>
        public static void Learn(DdaState state, IList<DdaDecision> decisions, IList<DdaPatternResult> results,
                                 bool cleared, DdaSettings settings)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (settings == null) settings = new DdaSettings();

            var model = state.GetModel();
            if (model != null && !model.IsHealthy()) model = null;
            bool learnModel = model != null && settings.IsActive;

            var byId = new Dictionary<string, DdaPatternResult>();
            if (results != null)
                foreach (var r in results)
                    if (r.PatternId != null) byId[r.PatternId] = r;

            int totEff = 0, totSucc = 0;
            foreach (var d in decisions)
            {
                if (d == null || d.Excluded) continue;
                var ps = state.GetPattern(d.PatternId);
                byId.TryGetValue(d.PatternId ?? "", out var r);

                int eff = Math.Max(0, r.Strikes - r.Ignored);
                int succ = Math.Max(0, Math.Min(eff, r.Parry + r.Dodge));
                ps.lastEff = eff;
                ps.step = DdaConstants.ClampStep(d.Step);

                if (eff > 0)
                {
                    ps.history.Add(new DdaRecord { eff = eff, succ = succ, step = ps.step });
                    DdaState.Trim(ps.history);
                }

                if (eff >= DdaConstants.MinStrikes)
                {
                    double y = (double)succ / eff;
                    ps.lastValidRate = y;
                    if (learnModel && d.Context != null)
                    {
                        model.Update(d.Context, ps.step, y, eff);
                        state.modelUpdates++;
                    }
                }
                totEff += eff;
                totSucc += succ;
            }

            state.overall.Add(new DdaOverallRecord { eff = totEff, succ = totSucc });
            DdaState.Trim(state.overall);
            state.attempts++;
            state.loseStreak = cleared ? 0 : state.loseStreak + 1;
        }

        /// <summary>
        /// 문맥 x0~x5 (6장).
        ///   x0 1 · x1 이 패턴 실패율(최근 3전) · x2 지금 단계/2 · x3 최근 3전 평균 단계/2
        ///   x4 전체 실패율(최근 3전) · x5 min(끝낸 전투 수, 10)/10
        /// 유효 타 합이 3 미만이면 x1 · x4에 defaultFail을 넣는다.
        /// </summary>
        public static double[] BuildContext(DdaPatternState ps, List<DdaOverallRecord> overall, int cur, int attempts, double defaultFail)
        {
            int st = 0, ss = 0, cnt = 0;
            double stepSum = 0.0;
            if (ps != null && ps.history != null)
            {
                int start = Math.Max(0, ps.history.Count - DdaConstants.HistoryLength);
                for (int i = start; i < ps.history.Count; i++)
                {
                    st += ps.history[i].eff;
                    ss += ps.history[i].succ;
                    stepSum += ps.history[i].step;
                    cnt++;
                }
            }
            double stepAvg = cnt > 0 ? stepSum / cnt : cur;
            double fail = st >= DdaConstants.MinStrikes ? 1.0 - (double)ss / st : defaultFail;

            int ast = 0, ass = 0;
            if (overall != null)
            {
                int start = Math.Max(0, overall.Count - DdaConstants.HistoryLength);
                for (int i = start; i < overall.Count; i++)
                {
                    ast += overall[i].eff;
                    ass += overall[i].succ;
                }
            }
            double overallFail = ast >= DdaConstants.MinStrikes ? 1.0 - (double)ass / ast : defaultFail;

            return new[]
            {
                1.0,
                fail,
                cur / 2.0,
                stepAvg / 2.0,
                overallFail,
                Math.Min(attempts, 10) / 10.0,
            };
        }

        /// <summary>지금 단계 ±1, 그리고 −2~+2 안으로 자른다.</summary>
        public static int ClampMove(int step, int cur)
        {
            step = Math.Max(cur - 1, Math.Min(cur + 1, step));
            return DdaConstants.ClampStep(step);
        }

        /// <summary>설정에 맞는 정책. LinUCB인데 모델이 없으면 규칙 기반을 주고 usesFallback = true.</summary>
        public static IDdaPolicy CreatePolicy(DdaSettings settings, DdaModel healthyModelOrNull, out bool usesFallback)
        {
            usesFallback = false;
            switch (settings.Policy)
            {
                case DdaPolicyKind.Rule:
                    return new RulePolicy();
                case DdaPolicyKind.Random:
                    return new RandomPolicy(settings.Rng);
                case DdaPolicyKind.LinUcb:
                    if (healthyModelOrNull != null) return new LinUcbPolicy(healthyModelOrNull, settings.Alpha);
                    usesFallback = true;
                    return new RulePolicy();
                default:
                    return new OffPolicy();
            }
        }

        /// <summary>구간 보상 r (7장). 로그 · 실험 평가용이고 학습에는 쓰지 않는다.</summary>
        public static double BandReward(double y)
        {
            if (y >= DdaConstants.BandLow && y <= DdaConstants.BandHigh) return 1.0;
            double dist = y < DdaConstants.BandLow ? DdaConstants.BandLow - y : y - DdaConstants.BandHigh;
            return Math.Max(0.0, 1.0 - dist / 0.3);
        }
    }
}
