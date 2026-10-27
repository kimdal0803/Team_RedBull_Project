using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower.DDA
{
    /// <summary>
    /// 보스별 DDA 상태(모델 · 패턴별 단계 · 최근 기록 · 연속 패배)를 PC에 JSON으로 저장한다.
    /// 위치: Application.persistentDataPath/dda/state_&lt;보스ID&gt;.json
    ///
    /// 세이브 시스템(간트 W7)이 생기면 SaveData로 옮긴다 (DDA 설계서 10장 SaveData.ddaModels).
    /// </summary>
    public static class DdaStateStore
    {
        static readonly Dictionary<string, DdaState> cache = new Dictionary<string, DdaState>();

        public static string Folder => Path.Combine(Application.persistentDataPath, "dda");

        public static string PathFor(string bossId) => Path.Combine(Folder, "state_" + Sanitize(bossId) + ".json");

        public static DdaState Load(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) bossId = "Boss";
            if (cache.TryGetValue(bossId, out var cached)) return cached;

            DdaState state = null;
            string path = PathFor(bossId);
            try
            {
                if (File.Exists(path)) state = FromJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DDA] 상태 파일을 읽지 못해 처음부터 시작합니다 ({path}): {e.Message}");
                state = null;
            }

            if (state == null) state = new DdaState();
            state.bossId = bossId;
            state.Normalize();
            cache[bossId] = state;
            return state;
        }

        /// <summary>상태 → JSON. 모델 숫자는 문자열 칸으로 옮겨 쓴다.</summary>
        public static string ToJson(DdaState state)
        {
            state.EncodeModelText();
            return JsonUtility.ToJson(state, true);
        }

        /// <summary>JSON → 상태. 읽지 못하면 null.</summary>
        public static DdaState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var state = JsonUtility.FromJson<DdaState>(json);
            if (state == null) return null;
            state.DecodeModelText();
            state.Normalize();
            return state;
        }

        public static void Save(DdaState state)
        {
            if (state == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                string path = PathFor(state.bossId);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, ToJson(state));
                if (!File.Exists(path))
                {
                    File.Move(tmp, path);
                }
                else
                {
                    try { File.Replace(tmp, path, null); }
                    catch (Exception)
                    {
                        File.Delete(path);
                        File.Move(tmp, path);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DDA] 상태를 저장하지 못했습니다: {e.Message}");
            }
        }

        /// <summary>이 보스의 DDA 기록을 지운다. 다음 전투가 '첫 전투'가 된다.</summary>
        public static void Delete(string bossId)
        {
            if (string.IsNullOrEmpty(bossId)) return;
            cache.Remove(bossId);
            try
            {
                string path = PathFor(bossId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DDA] 상태 파일을 지우지 못했습니다: {e.Message}");
            }
        }

        public static void DeleteAll()
        {
            cache.Clear();
            try
            {
                if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DDA] 상태 폴더를 지우지 못했습니다: {e.Message}");
            }
        }

        public static void ClearCache() => cache.Clear();

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Boss";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }

    /// <summary>
    /// 초기값 파일 (dda_prior_*.json, DDA 설계서 8.2).
    /// 플레이테스트에서 단계를 무작위로 준 기록(ddaPolicy = random)으로만 만든다.
    /// </summary>
    [Serializable]
    public class DdaPriorFile
    {
        public string version = "";
        public string bossId = "";
        public int rows;
        public double alpha;
        public string note = "";
        // A · b는 JsonUtility가 아니라 DdaNumberText로 직접 읽고 쓴다 (지수 표기 문제).
        [NonSerialized] public double[] A = new double[0];
        [NonSerialized] public double[] b = new double[0];

        public static bool TryParse(string json, out DdaPriorFile prior, out string error)
        {
            prior = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "파일이 비어 있습니다.";
                return false;
            }
            try
            {
                prior = JsonUtility.FromJson<DdaPriorFile>(json);
            }
            catch (Exception e)
            {
                error = "JSON 형식이 아닙니다: " + e.Message;
                return false;
            }
            if (prior != null)
            {
                prior.A = DdaNumberText.TryExtractJsonArray(json, "A", out var a) ? a : null;
                prior.b = DdaNumberText.TryExtractJsonArray(json, "b", out var bb) ? bb : null;
            }
            const int F = DdaConstants.FeatureSize;
            if (prior == null) error = "내용이 없습니다.";
            else if (string.IsNullOrEmpty(prior.version)) error = "version이 비어 있습니다.";
            else if (prior.A == null || prior.A.Length != F * F) error = $"A가 {F * F}개가 아닙니다.";
            else if (prior.b == null || prior.b.Length != F) error = $"b가 {F}개가 아닙니다.";
            else if (!DdaMath.AllFinite(prior.A) || !DdaMath.AllFinite(prior.b)) error = "NaN 또는 무한대가 있습니다.";
            else if (!new DdaModel((double[])prior.A.Clone(), (double[])prior.b.Clone()).IsHealthy()) error = "A가 양의 정부호가 아닙니다.";
            if (error != null)
            {
                prior = null;
                return false;
            }
            return true;
        }

        /// <summary>초기값 파일 글. 파이썬(json.load)으로도 읽힌다.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(Quote(version)).Append(",\n");
            sb.Append("  \"bossId\": ").Append(Quote(bossId)).Append(",\n");
            sb.Append("  \"rows\": ").Append(rows.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"alpha\": ").Append(alpha.ToString("G17", CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"note\": ").Append(Quote(note)).Append(",\n");
            sb.Append("  \"A\": [").Append(DdaNumberText.Join(A)).Append("],\n");
            sb.Append("  \"b\": [").Append(DdaNumberText.Join(b)).Append("]\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
