using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ReversalTower.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ReversalTower.DDA.EditorTools
{
    /// <summary>
    /// 메뉴 '역전의 탑 ▸ DDA'.
    ///   지금 씬에 DDA 붙이기 · 오프라인 검증 실행 · 초기값 만들기 · 저장된 DDA 상태 지우기 · 기록 폴더 열기
    /// </summary>
    public static class DdaEditorMenus
    {
        const string DataFolder = "Assets/ReversalTower/DDA/Data";
        const string ArmTablePath = DataFolder + "/DDAArmTable.asset";
        const string PriorFolder = "Assets/ReversalTower/DDA/Priors";

        // ─────────────────────────── 씬에 붙이기 ───────────────────────────

        [MenuItem("역전의 탑/DDA/지금 씬에 DDA 붙이기", false, 200)]
        public static void AttachToOpenScene()
        {
            var collector = UnityEngine.Object.FindAnyObjectByType<CombatDataCollector>(FindObjectsInactive.Include);
            if (collector == null)
            {
                EditorUtility.DisplayDialog("역전의 탑 · DDA",
                    "이 씬에 전투 데이터 수집기(CombatDataCollector)가 없습니다.\n\n" +
                    "먼저 '역전의 탑 ▸ ★ 1. 아라쿠스 테스트 씬 만들기'로 보스와 수집기를 놓은 뒤 다시 눌러 주세요.", "확인");
                return;
            }

            var table = GetOrCreateArmTable();
            var prior = AssetDatabase.LoadAssetAtPath<TextAsset>(DdaVerification.SimPriorPath);

            var manager = collector.GetComponent<DDAManager>();
            if (manager == null) manager = Undo.AddComponent<DDAManager>(collector.gameObject);
            Undo.RecordObject(manager, "DDA 붙이기");
            manager.collector = collector;
            manager.boss = collector.boss;
            manager.armTable = table;
            if (manager.priorFile == null) manager.priorFile = prior;
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(collector.gameObject.scene);

            Selection.activeGameObject = collector.gameObject;
            EditorGUIUtility.PingObject(collector.gameObject);

            string bossName = collector.boss != null ? collector.boss.bossId : "(없음 — 수집기에 보스를 넣어 주세요)";
            EditorUtility.DisplayDialog("역전의 탑 · DDA",
                $"DDA 매니저를 '{collector.gameObject.name}'에 붙였습니다.\n\n" +
                $"보스        {bossName}\n" +
                $"정책        {DdaPolicyNames.Of(manager.policy)} · 그룹 {manager.groupMode}\n" +
                $"배율 표     {ArmTablePath}\n" +
                $"초기값      {(manager.priorFile != null ? manager.priorFile.name : "없음 (규칙 기반으로 대신)")}\n\n" +
                "※ dda_prior_sim0는 가상 플레이테스트로 만든 시험용입니다. 실제 플레이테스트 뒤 '초기값 만들기'로 바꿔 주세요.\n\n" +
                "씬을 저장(Ctrl+S)한 뒤 Play → F2로 DDA 패널을 엽니다.", "좋아요");
        }

        static DDAArmTable GetOrCreateArmTable()
        {
            var table = AssetDatabase.LoadAssetAtPath<DDAArmTable>(ArmTablePath);
            if (table != null) return table;
            EnsureFolder(DataFolder);
            table = ScriptableObject.CreateInstance<DDAArmTable>();
            AssetDatabase.CreateAsset(table, ArmTablePath);
            AssetDatabase.SaveAssets();
            return table;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ─────────────────────────── 상태 · 기록 ───────────────────────────

        [MenuItem("역전의 탑/DDA/저장된 DDA 상태 지우기 (이 PC)", false, 320)]
        public static void ClearSavedState()
        {
            if (!EditorUtility.DisplayDialog("역전의 탑 · DDA",
                    $"이 PC에 저장된 DDA 상태(모델 · 단계 · 연속 패배)를 모두 지웁니다.\n\n{DdaStateStore.Folder}\n\n" +
                    "다음 전투가 '첫 전투'가 됩니다. 전투 기록 CSV는 지우지 않습니다.", "지우기", "취소"))
                return;
            DdaStateStore.DeleteAll();
            if (DdaRuntime.Current != null) DdaRuntime.Current.Finished = true; // Play 중이면 지금 전투가 끝날 때 다시 저장하지 않게
            Debug.Log("[DDA] 저장된 DDA 상태를 지웠습니다: " + DdaStateStore.Folder);
        }

        [MenuItem("역전의 탑/DDA/전투 기록 폴더 열기", false, 321)]
        public static void OpenLogFolder()
        {
            Directory.CreateDirectory(CombatLogWriter.Folder);
            EditorUtility.RevealInFinder(CombatLogWriter.Folder);
        }

        // ─────────────────────────── 초기값 만들기 ───────────────────────────

        /// <summary>
        /// 플레이테스트 기록(patterns*.csv)으로 초기값 파일을 만든다 (DDA 설계서 8.2 · 10장).
        ///   ddaPolicy = random 인 줄만 쓴다 (LinUCB가 고른 기록은 한쪽으로 치우쳐 있어서).
        ///   유효 타 3 이상, 제외 · 대조군 줄은 뺀다.
        ///   A = I + Σ (n/6)·φφᵀ,  b = Σ (n/6)·y·φ  (ddasim의 웜 스타트와 같은 식)
        /// 여러 PC의 기록은 한 폴더에 모아 두면 된다 (patterns.csv, patterns (2).csv … 이름은 상관없음).
        /// </summary>
        [MenuItem("역전의 탑/DDA/초기값 만들기 (플레이테스트 기록에서)", false, 310)]
        public static void BuildPriorFromLogs()
        {
            string folder = EditorUtility.OpenFolderPanel("플레이테스트 patterns*.csv가 있는 폴더", CombatLogWriter.Folder, "");
            if (string.IsNullOrEmpty(folder)) return;

            var files = Directory.GetFiles(folder, "patterns*.csv", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog("역전의 탑 · DDA", "patterns*.csv 파일이 없습니다.", "확인");
                return;
            }

            var model = DdaModel.CreateIdentity();
            var bossCount = new Dictionary<string, int>();
            int used = 0, skipped = 0, filesUsed = 0;
            var battles = new HashSet<string>();
            var x = new double[DdaConstants.ContextSize];

            foreach (var file in files)
            {
                List<Dictionary<string, string>> rows;
                try { rows = ReadCsv(file); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DDA 초기값] {file}을 읽지 못했습니다: {e.Message}");
                    continue;
                }
                if (rows.Count == 0 || !rows[0].ContainsKey("ddaPolicy") || !rows[0].ContainsKey("ctx1"))
                {
                    Debug.LogWarning($"[DDA 초기값] {Path.GetFileName(file)}에는 DDA 열이 없어 건너뜁니다 (DDA를 붙이기 전 기록).");
                    continue;
                }
                filesUsed++;

                foreach (var row in rows)
                {
                    if (Get(row, "ddaPolicy") != DdaPolicyNames.Random) { skipped++; continue; }
                    string reason = Get(row, "ddaReason");
                    if (reason == DdaReasons.Excluded || reason == DdaReasons.Off || reason == DdaReasons.Forced) { skipped++; continue; }

                    int strikes = Int(row, "strikes"), ignored = Int(row, "ignoredCount");
                    int parry = Int(row, "parrySuccess"), dodge = Int(row, "dodgeCount");
                    int n = Math.Max(0, strikes - ignored);
                    if (n < DdaConstants.MinStrikes) { skipped++; continue; }

                    x[0] = 1.0;
                    bool ok = true;
                    for (int i = 1; i <= 5; i++)
                    {
                        x[i] = Dbl(row, "ctx" + i);
                        if (x[i] < -1.0001 || double.IsNaN(x[i])) ok = false;
                    }
                    if (Dbl(row, "ctx1") < 0 || Dbl(row, "ctx4") < 0) ok = false; // 실패율이 −1이면 문맥이 없는 줄
                    if (!ok) { skipped++; continue; }

                    int step = DdaConstants.ClampStep(Int(row, "ddaStep"));
                    double y = (double)Math.Min(n, parry + dodge) / n;
                    model.Update(x, step, y, n);
                    used++;

                    string boss = Get(row, "bossId");
                    bossCount[boss] = bossCount.TryGetValue(boss, out int c) ? c + 1 : 1;
                    string battle = Get(row, "battleId");
                    if (!string.IsNullOrEmpty(battle)) battles.Add(battle);
                }
            }

            if (used == 0)
            {
                EditorUtility.DisplayDialog("역전의 탑 · DDA",
                    $"쓸 수 있는 줄이 없습니다 (파일 {files.Length}개, 건너뜀 {skipped}줄).\n\n" +
                    "플레이테스트는 DDA 매니저의 정책을 Random으로 두고 해야 초기값 재료가 됩니다.", "확인");
                return;
            }
            if (!model.IsHealthy())
            {
                EditorUtility.DisplayDialog("역전의 탑 · DDA", "만든 모델이 계산할 수 없는 상태입니다 (기록에 이상한 값이 있는지 확인).", "확인");
                return;
            }

            string mainBoss = bossCount.OrderByDescending(kv => kv.Value).First().Key;
            string version = $"pt-{DateTime.Now:yyMMdd-HHmm}-{used}";
            var prior = new DdaPriorFile
            {
                version = version,
                bossId = bossCount.Count == 1 ? mainBoss : "",
                rows = used,
                alpha = DdaConstants.DefaultAlpha,
                note = $"플레이테스트 기록 {filesUsed}개 파일, 전투 {battles.Count}판, 패턴 줄 {used}개 (random). 만든 시각 {DateTime.Now:yyyy-MM-dd HH:mm}",
                A = model.A,
                b = model.B,
            };

            EnsureFolder(PriorFolder);
            string assetPath = $"{PriorFolder}/dda_prior_{(string.IsNullOrEmpty(prior.bossId) ? "all" : prior.bossId)}_{DateTime.Now:yyMMdd_HHmm}.json";
            File.WriteAllText(Path.GetFullPath(assetPath), prior.ToJson(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath);
            var created = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (created != null) EditorGUIUtility.PingObject(created);

            string warn = used < 300 ? "\n\n※ 줄이 적습니다. 설계서 기준은 12명 × 10전 이상입니다 (시뮬레이션에서 12명 × 10전 ≈ 500줄)." : "";
            EditorUtility.DisplayDialog("역전의 탑 · DDA",
                $"초기값을 만들었습니다.\n\n{assetPath}\n버전 {version}\n\n" +
                $"쓴 줄 {used}개 · 건너뛴 줄 {skipped}개 · 전투 {battles.Count}판 · 파일 {filesUsed}개{warn}\n\n" +
                "DDA 매니저의 '초기값 파일' 칸에 이 파일을 넣으면, 다음 전투부터 모델이 이 초기값으로 새로 시작합니다.", "좋아요");
        }

        static string Get(Dictionary<string, string> row, string key) => row.TryGetValue(key, out var v) ? v : "";

        static int Int(Dictionary<string, string> row, string key) =>
            int.TryParse(Get(row, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

        static double Dbl(Dictionary<string, string> row, string key) =>
            double.TryParse(Get(row, key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;

        /// <summary>CombatLogWriter가 쓴 CSV (UTF-8, 첫 줄 머리글, 큰따옴표 이스케이프)를 읽는다.</summary>
        static List<Dictionary<string, string>> ReadCsv(string path)
        {
            var result = new List<Dictionary<string, string>>();
            string[] header = null;
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.TrimStart('\uFEFF');
                if (line.Length == 0) continue;
                var cells = SplitCsvLine(line);
                if (header == null)
                {
                    header = cells.ToArray();
                    continue;
                }
                var row = new Dictionary<string, string>();
                for (int i = 0; i < header.Length && i < cells.Count; i++) row[header[i]] = cells[i];
                result.Add(row);
            }
            return result;
        }

        static List<string> SplitCsvLine(string line)
        {
            var cells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else quoted = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { cells.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            cells.Add(sb.ToString());
            return cells;
        }
    }
}
