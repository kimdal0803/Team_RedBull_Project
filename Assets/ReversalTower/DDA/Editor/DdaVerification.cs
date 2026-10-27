#pragma warning disable 0649 // 정답 파일 클래스는 JsonUtility가 채운다
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ReversalTower.Boss;
using UnityEditor;
using UnityEngine;

namespace ReversalTower.DDA.EditorTools
{
    /// <summary>
    /// DDA 오프라인 검증 (간트 차트 'DDA 코어: LinUCB 구현 + 오프라인 검증').
    ///
    /// 파이썬 시뮬레이션(ddasim)이 만든 정답 파일과 C# 결과를 비교한다.
    ///   1. 모델    초기값 sim-0에서 예측 ŷ · 불확실성 u · 고른 단계 (45개)
    ///   2. 업데이트 관측 30개를 넣은 뒤 A · b와 예측 (20개)
    ///   3. 문맥    x0~x5 계산 (40개)
    ///   4. 레일    한 칸 제한 · 동점 처리 (33개)
    ///   5. 규칙    규칙 기반 (45개)
    ///   6. 시나리오 결정 → 결과 → 학습을 16전 반복 (정책 4가지, 안전장치 전부 포함)
    ///   7. 게임 쪽  배율 표 · 선딜 하한 · 연타 · 제외 패턴 · 저장 · 초기값 파일 · 고장 시 복귀
    /// 결과는 콘솔과 창에 나오고, 프로젝트의 Logs/dda_verification_report.txt에도 남는다.
    /// </summary>
    public static class DdaVerification
    {
        public const string GoldenPath = "Assets/ReversalTower/DDA/Editor/Golden/dda_golden_v1.json";
        public const string SimPriorPath = "Assets/ReversalTower/DDA/Priors/dda_prior_sim0.json";

        const double TolPrediction = 1e-7;
        const double TolContext = 1e-9;
        const double TolMatrixRel = 1e-9;

        // ─────────────── 정답 파일 형식 (export_golden.py와 같은 이름) ───────────────

        [Serializable] class ModelCase { public double[] x; public int cur; public double[] yhat; public double[] unc; public int choice; }
        [Serializable] class UpdateRow { public double[] x; public int arm; public double y; public int n; }
        [Serializable] class ContextCase { public int[] recEff; public int[] recSucc; public int[] recStep; public int[] allEff; public int[] allSucc; public int attempts; public int cur; public double[] x; }
        [Serializable] class RailCase { public double[] scores; public int cur; public int choice; }
        [Serializable] class RuleCase { public double lastRate; public int cur; public int choice; }
        [Serializable] class Outcome { public string id; public int strikes; public int ignored; public int parry; public int dodge; }
        [Serializable] class Expect { public string id; public int step; public string reason; public double[] x; public double predicted; public double uncertainty; }
        [Serializable] class Battle { public bool cleared; public Outcome[] outcomes; public Expect[] expects; public bool protectionApplied; }
        [Serializable]
        class Scenario
        {
            public string name; public string group; public string policy; public bool usePrior;
            public string[] patternIds; public bool[] excluded; public Battle[] battles;
            public int finalAttempts; public int finalLoseStreak; public double[] finalA; public double[] finalB; public int finalUpdates;
        }
        [Serializable]
        class Golden
        {
            public string version; public double alpha; public double defaultFail; public double bandLow; public double bandHigh; public double target;
            public int minStrikes; public int protectAfter; public string priorVersion; public double[] priorA; public double[] priorB;
            public ModelCase[] modelCases; public UpdateRow[] updates; public double[] afterA; public double[] afterB; public ModelCase[] afterCases;
            public ContextCase[] contextCases; public RailCase[] railCases; public RuleCase[] ruleCases; public Scenario[] scenarios;
        }

        // ─────────────────────────── 실행 ───────────────────────────

        class Report
        {
            public readonly StringBuilder Text = new StringBuilder();
            public readonly List<string> Failures = new List<string>();
            public int Passed;
            string section;
            int sectionPass, sectionFail;

            public void Section(string name)
            {
                EndSection();
                section = name;
                sectionPass = sectionFail = 0;
            }

            public void EndSection()
            {
                if (section == null) return;
                Text.AppendLine($"{(sectionFail == 0 ? "통과" : "실패")}  {section}  ({sectionPass}/{sectionPass + sectionFail})");
                section = null;
            }

            public void Check(bool ok, string what)
            {
                if (ok) { Passed++; sectionPass++; return; }
                sectionFail++;
                string line = $"[{section}] {what}";
                Failures.Add(line);
            }
        }

        [MenuItem("역전의 탑/DDA/오프라인 검증 실행", false, 300)]
        public static void RunFromMenu()
        {
            var report = Run();
            string summary = report.Failures.Count == 0
                ? $"DDA 오프라인 검증 통과 — {report.Passed}개 항목 모두 맞습니다.\n\nC# DDA 코드가 파이썬 시뮬레이션(설계서 12장)과 같은 값을 냅니다."
                : $"DDA 오프라인 검증 실패 — {report.Failures.Count}개 틀림 / {report.Passed}개 맞음.\n\n처음 몇 개:\n" +
                  string.Join("\n", report.Failures.GetRange(0, Math.Min(6, report.Failures.Count))) +
                  "\n\n콘솔의 전체 결과를 그대로 보내 주세요.";
            EditorUtility.DisplayDialog("역전의 탑 · DDA 오프라인 검증", summary + $"\n\n보고서: {ReportPath}", "확인");
        }

        static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "dda_verification_report.txt"));

        static Report Run()
        {
            var r = new Report();
            r.Text.AppendLine($"DDA 오프라인 검증 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            r.Text.AppendLine($"정답 파일: {GoldenPath}");
            r.Text.AppendLine();

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(GoldenPath);
            Golden g = null;
            if (asset != null)
            {
                try { g = JsonUtility.FromJson<Golden>(asset.text); }
                catch (Exception e) { Debug.LogException(e); }
            }

            r.Section("정답 파일 읽기");
            r.Check(g != null && g.modelCases != null && g.scenarios != null, $"{GoldenPath}를 읽지 못했습니다.");
            if (g != null && g.modelCases != null && g.scenarios != null)
            {
                RunConstants(g, r);
                RunModel(g, r);
                RunUpdates(g, r);
                RunContext(g, r);
                RunRail(g, r);
                RunRule(g, r);
                foreach (var s in g.scenarios) RunScenario(g, s, r);
            }
            RunGameSide(g, r);
            r.EndSection();

            r.Text.AppendLine();
            r.Text.AppendLine(r.Failures.Count == 0 ? $"전체 통과: {r.Passed}개" : $"실패 {r.Failures.Count}개 / 통과 {r.Passed}개");
            foreach (var f in r.Failures) r.Text.AppendLine("  " + f);

            string text = r.Text.ToString();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
                File.WriteAllText(ReportPath, text, new UTF8Encoding(true));
            }
            catch (Exception e) { Debug.LogWarning("[DDA 검증] 보고서를 쓰지 못했습니다: " + e.Message); }

            if (r.Failures.Count == 0) Debug.Log("[DDA 검증] " + text);
            else Debug.LogError("[DDA 검증] " + text);
            return r;
        }

        // ─────────────────────────── 1~5 ───────────────────────────

        static void RunConstants(Golden g, Report r)
        {
            r.Section("고정값 (설계서 7 · 8 · 9장)");
            r.Check(Near(g.alpha, DdaConstants.DefaultAlpha, 1e-12), $"alpha {g.alpha} ≠ {DdaConstants.DefaultAlpha}");
            r.Check(Near(g.defaultFail, DdaConstants.DefaultFailRate, 1e-12), $"defaultFail {g.defaultFail}");
            r.Check(Near(g.bandLow, DdaConstants.BandLow, 1e-12) && Near(g.bandHigh, DdaConstants.BandHigh, 1e-12), "목표 구간");
            r.Check(Near(g.target, DdaConstants.Target, 1e-12), "목표값");
            r.Check(g.minStrikes == DdaConstants.MinStrikes, "최소 유효 타");
            r.Check(g.protectAfter == DdaConstants.ProtectAfterLosses, "연패 보호 기준");
        }

        static void RunModel(Golden g, Report r)
        {
            r.Section("모델 예측 · 선택 (초기값 sim-0)");
            var model = new DdaModel(Copy(g.priorA), Copy(g.priorB));
            r.Check(model.IsHealthy(), "초기값 A가 양의 정부호가 아닙니다.");
            CheckCases(model, g.modelCases, g.alpha, r, "예측");
        }

        static void RunUpdates(Golden g, Report r)
        {
            r.Section("모델 업데이트 (관측 30개)");
            var model = new DdaModel(Copy(g.priorA), Copy(g.priorB));
            foreach (var u in g.updates) model.Update(u.x, u.arm, u.y, u.n);
            r.Check(MatrixNear(model.A, g.afterA, out string ea), "A " + ea);
            r.Check(MatrixNear(model.B, g.afterB, out string eb), "b " + eb);
            CheckCases(model, g.afterCases, g.alpha, r, "업데이트 후 예측");
        }

        static void CheckCases(DdaModel model, ModelCase[] cases, double alpha, Report r, string label)
        {
            var yhat = new double[5];
            var unc = new double[5];
            var buf = new double[5];
            for (int k = 0; k < cases.Length; k++)
            {
                var c = cases[k];
                bool ok = model.Predict(c.x, yhat, unc);
                r.Check(ok, $"{label} {k}: 계산 실패");
                if (!ok) continue;
                double worst = 0;
                for (int i = 0; i < 5; i++)
                    worst = Math.Max(worst, Math.Max(Math.Abs(yhat[i] - c.yhat[i]), Math.Abs(unc[i] - c.unc[i])));
                r.Check(worst <= TolPrediction, $"{label} {k}: 최대 차이 {worst:E2}");
                int choice = LinUcbPolicy.ChooseFromPrediction(yhat, unc, c.cur, alpha, buf);
                r.Check(choice == c.choice, $"{label} {k}: 단계 {choice} ≠ 정답 {c.choice} (지금 {c.cur})");
            }
        }

        static void RunContext(Golden g, Report r)
        {
            r.Section("문맥 x0~x5 (설계서 6장)");
            for (int k = 0; k < g.contextCases.Length; k++)
            {
                var c = g.contextCases[k];
                var ps = new DdaPatternState { id = "P" };
                for (int i = 0; i < c.recEff.Length; i++)
                    ps.history.Add(new DdaRecord { eff = c.recEff[i], succ = c.recSucc[i], step = c.recStep[i] });
                var overall = new List<DdaOverallRecord>();
                for (int i = 0; i < c.allEff.Length; i++)
                    overall.Add(new DdaOverallRecord { eff = c.allEff[i], succ = c.allSucc[i] });
                var x = DdaEngine.BuildContext(ps, overall, c.cur, c.attempts, g.defaultFail);
                r.Check(VecNear(x, c.x, TolContext, out string e), $"문맥 {k}: {e}");
            }
        }

        static void RunRail(Golden g, Report r)
        {
            r.Section("한 칸 제한 · 동점 처리 (설계서 9장)");
            for (int k = 0; k < g.railCases.Length; k++)
            {
                var c = g.railCases[k];
                int got = LinUcbPolicy.Rail(c.scores, c.cur);
                r.Check(got == c.choice, $"레일 {k}: {got} ≠ {c.choice} (지금 {c.cur})");
            }
        }

        static void RunRule(Golden g, Report r)
        {
            r.Section("규칙 기반 (설계서 8.3)");
            foreach (var c in g.ruleCases)
            {
                int got = DdaEngine.ClampMove(RulePolicy.ChooseStep(c.lastRate, c.cur), c.cur);
                r.Check(got == c.choice, $"성공률 {c.lastRate}, 지금 {c.cur}: {got} ≠ {c.choice}");
            }
        }

        // ─────────────────────────── 6. 시나리오 ───────────────────────────

        static void RunScenario(Golden g, Scenario sc, Report r)
        {
            r.Section($"시나리오 {sc.name} (16전)");
            var settings = new DdaSettings
            {
                Group = sc.group == "dda" ? DdaGroup.Dda : DdaGroup.Control,
                Policy = ParsePolicy(sc.policy),
                Alpha = g.alpha,
                DefaultFailRate = g.defaultFail,
            };
            var state = new DdaState { bossId = "Golden" };
            if (sc.usePrior) state.SetModel(g.priorA, g.priorB, g.priorVersion);

            var infos = new List<DdaPatternInfo>();
            for (int i = 0; i < sc.patternIds.Length; i++) infos.Add(new DdaPatternInfo(sc.patternIds[i], sc.excluded[i]));

            for (int t = 0; t < sc.battles.Length; t++)
            {
                var b = sc.battles[t];
                var set = DdaEngine.Decide(state, infos, settings);
                r.Check(set.ProtectionApplied == b.protectionApplied, $"{t + 1}전: 3연패 보호 {set.ProtectionApplied} ≠ {b.protectionApplied}");
                r.Check(set.Decisions.Count == b.expects.Length, $"{t + 1}전: 결정 수 {set.Decisions.Count} ≠ {b.expects.Length}");

                foreach (var e in b.expects)
                {
                    var d = set.Find(e.id);
                    if (d == null) { r.Check(false, $"{t + 1}전 {e.id}: 결정 없음"); continue; }
                    string where = $"{t + 1}전 {e.id}";
                    r.Check(d.Step == e.step, $"{where}: 단계 {d.Step} ≠ {e.step} ({d.Reason} / 정답 {e.reason})");
                    r.Check(d.Reason == e.reason, $"{where}: 이유 {d.Reason} ≠ {e.reason}");
                    if (e.x == null || e.x.Length == 0) r.Check(d.Context == null, $"{where}: 제외 패턴인데 문맥이 있음");
                    else r.Check(VecNear(d.Context, e.x, TolContext, out string ex), $"{where}: 문맥 {ex}");
                    r.Check(Math.Abs(d.Predicted - e.predicted) <= TolPrediction, $"{where}: 예측 {d.Predicted} ≠ {e.predicted}");
                    r.Check(Math.Abs(d.Uncertainty - e.uncertainty) <= TolPrediction, $"{where}: 불확실성 {d.Uncertainty} ≠ {e.uncertainty}");
                }

                var results = new List<DdaPatternResult>();
                foreach (var o in b.outcomes)
                    results.Add(new DdaPatternResult { PatternId = o.id, Strikes = o.strikes, Ignored = o.ignored, Parry = o.parry, Dodge = o.dodge });
                DdaEngine.Learn(state, set.Decisions, results, b.cleared, settings);
            }

            r.Check(state.attempts == sc.finalAttempts, $"끝낸 전투 {state.attempts} ≠ {sc.finalAttempts}");
            r.Check(state.loseStreak == sc.finalLoseStreak, $"연속 패배 {state.loseStreak} ≠ {sc.finalLoseStreak}");
            r.Check(state.modelUpdates == sc.finalUpdates, $"모델 업데이트 {state.modelUpdates} ≠ {sc.finalUpdates}");
            if (sc.finalA != null && sc.finalA.Length > 0)
            {
                r.Check(state.hasModel, "모델이 없어졌습니다.");
                if (state.hasModel)
                {
                    r.Check(MatrixNear(state.modelA, sc.finalA, out string ea), "마지막 A " + ea);
                    r.Check(MatrixNear(state.modelB, sc.finalB, out string eb), "마지막 b " + eb);
                }
            }
            else
            {
                r.Check(!state.hasModel, "초기값이 없는데 모델이 생겼습니다.");
            }
        }

        static DdaPolicyKind ParsePolicy(string s)
        {
            switch (s)
            {
                case "linucb": return DdaPolicyKind.LinUcb;
                case "rule": return DdaPolicyKind.Rule;
                case "random": return DdaPolicyKind.Random;
                default: return DdaPolicyKind.Off;
            }
        }

        // ─────────────────────────── 7. 게임 쪽 ───────────────────────────

        static void RunGameSide(Golden g, Report r)
        {
            r.Section("배율 표 (설계서 5장)");
            var table = DDAArmTable.CreateDefault();
            float[,] expect =
            {
                { 1.30f, 0.80f, 1.25f, -1, 1.20f },
                { 1.15f, 0.90f, 1.10f, 0, 1.10f },
                { 1.00f, 1.00f, 1.00f, 0, 1.00f },
                { 0.90f, 1.10f, 0.90f, 0, 0.95f },
                { 0.80f, 1.20f, 0.80f, 1, 0.90f },
            };
            for (int i = 0; i < 5; i++)
            {
                var row = table.GetRow(i - 2);
                bool ok = row != null && Mathf.Approximately(row.telegraphScale, expect[i, 0]) && Mathf.Approximately(row.damageScale, expect[i, 1])
                          && Mathf.Approximately(row.cooldownScale, expect[i, 2]) && row.repeatCountDelta == (int)expect[i, 3]
                          && Mathf.Approximately(row.repeatIntervalScale, expect[i, 4]);
                r.Check(ok, $"{i - 2}단계 줄이 설계서와 다릅니다.");
            }
            r.Check(table.ToTuning(0, null) == null, "0단계는 보정이 없어야 합니다.");

            r.Section("선딜 하한 · 연타 (설계서 4 · 5장)");
            var slow = MakePattern(1.05f, 1, 1f);   // 일반 공격 1처럼 선딜 1.05초
            var fast = MakePattern(0.45f, 1, 1f);   // 선딜 0.45초
            var tiny = MakePattern(0.1f, 1, 1f);    // 선딜 0.1초 (원래 0.4초보다 짧음)
            var multi = MakePattern(0.5f, 8, 1f);   // 방패 돌진처럼 8연타
            r.Check(Mathf.Approximately(table.ToTuning(2, slow).telegraphScale, 0.8f), "선딜 1.05초 +2단계 → ×0.8이어야 합니다.");
            r.Check(Mathf.Approximately(table.ToTuning(2, fast).telegraphScale, 0.4f / 0.45f), "선딜 0.45초 +2단계 → 0.4초에서 멈춰야 합니다.");
            r.Check(Mathf.Approximately(table.ToTuning(1, tiny).telegraphScale, 1f), "원래 0.4초보다 짧은 선딜은 줄이면 안 됩니다.");
            r.Check(Mathf.Approximately(table.ToTuning(-2, fast).telegraphScale, 1.3f), "쉬운 쪽 선딜은 그대로 늘어나야 합니다.");
            r.Check(table.ToTuning(2, multi).repeatCountDelta == 1 && table.ToTuning(-2, multi).repeatCountDelta == -1, "연타 패턴은 타 수가 ±1 바뀌어야 합니다.");
            r.Check(table.ToTuning(2, slow).repeatCountDelta == 0, "단타 패턴에는 타 수를 더하면 안 됩니다.");

            r.Section("제외 패턴 (설계서 4장)");
            r.Check(!DdaRuntime.IsExcluded(slow), "보통 패턴이 제외로 잡혔습니다.");
            var noHits = MakePattern(0.1f, 1, 1f);
            noHits.hits = new HitData[0];
            r.Check(DdaRuntime.IsExcluded(noHits), "타격 없는 패턴(퀵스텝 · 분노)은 제외여야 합니다.");
            var forced = MakePattern(1.1f, 1, 0f);
            r.Check(DdaRuntime.IsExcluded(forced), "weight 0 강제 패턴(돌진 기믹)은 제외여야 합니다.");
            var flagged = MakePattern(1f, 1, 1f);
            flagged.ddaExcluded = true;
            r.Check(DdaRuntime.IsExcluded(flagged), "ddaExcluded를 켠 패턴은 제외여야 합니다.");
            foreach (var p in new[] { slow, fast, tiny, multi, noHits, forced, flagged }) UnityEngine.Object.DestroyImmediate(p);
            UnityEngine.Object.DestroyImmediate(table);

            r.Section("고장 시 복귀 · 범위 (설계서 9장)");
            {
                var st = new DdaState { bossId = "T", attempts = 2 };
                st.GetPattern("P").lastEff = 5;
                st.GetPattern("P").lastValidRate = 0.4;
                st.GetPattern("P").step = -2;
                st.SetModel(Identity(), new double[DdaConstants.FeatureSize], "broken");
                st.modelA[0] = double.NaN;
                var set = DdaEngine.Decide(st, new List<DdaPatternInfo> { new DdaPatternInfo("P", false) },
                                           new DdaSettings { Policy = DdaPolicyKind.LinUcb });
                var d = set.Decisions[0];
                r.Check(d.Reason == DdaReasons.Fallback, $"모델에 NaN이 있으면 fallback이어야 합니다 (지금 {d.Reason}).");
                r.Check(d.Step == -2, $"−2에서 더 쉽게 갈 수 없어야 합니다 (지금 {d.Step}).");
                r.Check(d.Predicted < 0, "고장 난 모델로 예측을 남기면 안 됩니다.");

                var rnd = new DdaState { bossId = "T", attempts = 3, loseStreak = 5 };
                rnd.GetPattern("P").lastEff = 5;
                var rs = DdaEngine.Decide(rnd, new List<DdaPatternInfo> { new DdaPatternInfo("P", false) },
                                          new DdaSettings { Policy = DdaPolicyKind.Random, Rng = new System.Random(1) });
                r.Check(!rs.ProtectionApplied && Math.Abs(rs.Decisions[0].Step) <= 1, "random 정책은 3연패 보호를 걸지 않고 ±1 안에서 골라야 합니다.");
            }

            r.Section("상태 저장 (JSON)");
            {
                var st = new DdaState { bossId = "T", attempts = 4, loseStreak = 1 };
                st.SetModel(Identity(), new double[DdaConstants.FeatureSize], "v-test");
                st.modelB[3] = 0.125;
                st.modelB[4] = 1.5e-7;             // 지수 표기가 필요한 작은 값
                st.modelA[1] = 1.0 / 3.0;
                st.modelUpdates = 7;
                var p = st.GetPattern("Arakus_NormalAttack_R");
                p.step = -1; p.lastEff = 4; p.lastValidRate = 0.5;
                p.history.Add(new DdaRecord { eff = 4, succ = 2, step = -1 });
                st.overall.Add(new DdaOverallRecord { eff = 20, succ = 12 });
                var back = DdaStateStore.FromJson(DdaStateStore.ToJson(st));
                r.Check(back != null, "저장한 JSON을 다시 읽지 못했습니다.");
                if (back == null) back = new DdaState();
                var bp = back.GetPattern("Arakus_NormalAttack_R");
                r.Check(back.hasModel && back.priorVersion == "v-test" && back.modelUpdates == 7, "모델 정보가 저장 · 복원되지 않습니다.");
                r.Check(back.modelA.Length == st.modelA.Length && back.modelB.Length == st.modelB.Length
                        && back.modelB[3] == 0.125 && back.modelB[4] == 1.5e-7 && back.modelA[1] == 1.0 / 3.0,
                        "모델 숫자가 그대로 저장 · 복원되지 않습니다 (작은 값 1.5e-7 포함).");
                r.Check(bp.step == -1 && bp.lastEff == 4 && Near(bp.lastValidRate, 0.5, 0) && bp.history.Count == 1 && bp.history[0].succ == 2,
                        "패턴 상태가 저장 · 복원되지 않습니다.");
                r.Check(back.attempts == 4 && back.loseStreak == 1 && back.overall.Count == 1, "전투 수 · 연속 패배가 저장 · 복원되지 않습니다.");
            }

            r.Section("초기값 파일");
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(SimPriorPath);
                r.Check(asset != null, $"{SimPriorPath}가 없습니다.");
                if (asset != null)
                {
                    bool ok = DdaPriorFile.TryParse(asset.text, out var prior, out string error);
                    r.Check(ok, "시험용 초기값을 읽지 못했습니다: " + error);
                    if (ok && g != null && g.priorA != null)
                    {
                        r.Check(prior.version == g.priorVersion, $"초기값 버전 {prior.version} ≠ 정답 파일 {g.priorVersion}");
                        r.Check(MatrixNear(prior.A, g.priorA, out string ea) && MatrixNear(prior.b, g.priorB, out _), "초기값 파일과 정답 파일의 A · b가 다릅니다 " + ea);
                    }
                }
                r.Check(!DdaPriorFile.TryParse("{\"version\":\"x\",\"A\":[1,2],\"b\":[1]}", out _, out _), "크기가 틀린 초기값을 받아들였습니다.");

                var made = new DdaPriorFile { version = "t-1", bossId = "Arakus", rows = 3, alpha = 0.2, note = "따옴표 \" 와 줄바꿈\n 시험", A = Identity(), b = new double[DdaConstants.FeatureSize] };
                made.b[0] = 2.5e-6;
                made.A[1] = made.A[DdaConstants.FeatureSize] = 1e-5;
                bool parsed = DdaPriorFile.TryParse(made.ToJson(), out var again, out string err2);
                r.Check(parsed, "초기값 만들기로 쓴 파일을 다시 읽지 못했습니다: " + err2);
                if (parsed)
                    r.Check(again.version == "t-1" && again.bossId == "Arakus" && again.rows == 3 && again.note == made.note
                            && again.b[0] == 2.5e-6 && again.A[1] == 1e-5, "초기값 파일 쓰기 · 읽기에서 값이 바뀝니다 (지수 표기 포함).");
            }
        }

        static BossPatternData MakePattern(float telegraph, int repeat, float weight)
        {
            var p = ScriptableObject.CreateInstance<BossPatternData>();
            p.hideFlags = HideFlags.HideAndDontSave;
            p.patternId = "Test";
            p.telegraphTime = telegraph;
            p.weight = weight;
            p.hits = new[] { new HitData { repeatCount = repeat } };
            return p;
        }

        // ─────────────────────────── 도우미 ───────────────────────────

        static double[] Copy(double[] a) => (double[])a.Clone();

        static double[] Identity()
        {
            int F = DdaConstants.FeatureSize;
            var a = new double[F * F];
            for (int i = 0; i < F; i++) a[i * F + i] = 1.0;
            return a;
        }

        static bool Near(double a, double b, double tol) => Math.Abs(a - b) <= tol;

        static bool VecNear(double[] a, double[] b, double tol, out string error)
        {
            error = "";
            if (a == null || b == null || a.Length != b.Length) { error = "길이가 다릅니다"; return false; }
            for (int i = 0; i < a.Length; i++)
                if (!(Math.Abs(a[i] - b[i]) <= tol)) { error = $"x{i} {a[i]:R} ≠ {b[i]:R}"; return false; }
            return true;
        }

        static bool MatrixNear(double[] a, double[] b, out string error)
        {
            error = "";
            if (a == null || b == null || a.Length != b.Length) { error = "크기가 다릅니다"; return false; }
            for (int i = 0; i < a.Length; i++)
            {
                double tol = TolMatrixRel * Math.Max(1.0, Math.Abs(b[i]));
                if (!(Math.Abs(a[i] - b[i]) <= tol)) { error = $"[{i}] {a[i]:R} ≠ {b[i]:R}"; return false; }
            }
            return true;
        }
    }
}
