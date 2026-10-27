using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ReversalTower.DDA
{
    /// <summary>한 패턴의 한 전투 기록 (문맥 x1 · x3을 만들 때 쓴다).</summary>
    [Serializable]
    public class DdaRecord
    {
        public int eff;   // 유효 타 = 날아온 타 − 무시된 타
        public int succ;  // 패링 + 회피
        public int step;  // 그 전투에 걸려 있던 단계
    }

    /// <summary>한 전투의 전체 기록 (문맥 x4).</summary>
    [Serializable]
    public class DdaOverallRecord
    {
        public int eff;
        public int succ;
    }

    /// <summary>패턴 하나의 DDA 상태.</summary>
    [Serializable]
    public class DdaPatternState
    {
        public string id = "";
        /// <summary>다음 전투에 걸 단계 = 지난 전투에 걸려 있던 단계</summary>
        public int step;
        /// <summary>지난 전투의 유효 타 (3 미만이면 다음 전투 단계를 그대로 둔다)</summary>
        public int lastEff;
        /// <summary>유효 타 3 이상이던 마지막 전투의 대응 성공률. 없으면 −1 (규칙 기반용)</summary>
        public double lastValidRate = -1.0;
        /// <summary>최근 3전 (유효 타가 1 이상이던 전투만)</summary>
        public List<DdaRecord> history = new List<DdaRecord>();
    }

    /// <summary>
    /// 보스 하나의 DDA 상태 전체. JSON으로 저장한다 (DdaStateStore).
    /// 모델(A · b)은 숫자 342개다 (설계서 8.2).
    /// </summary>
    [Serializable]
    public class DdaState
    {
        public int formatVersion = 1;
        public string bossId = "";
        /// <summary>이 보스와 끝낸 전투 수 (문맥 x5, 첫 전투 판단)</summary>
        public int attempts;
        /// <summary>연속 패배 수 (3연패 보호)</summary>
        public int loseStreak;

        public bool hasModel;
        /// <summary>모델을 시작한 초기값 파일의 버전 (로그의 priorVersion)</summary>
        public string priorVersion = "";
        /// <summary>초기값 뒤로 이 플레이어 기록으로 업데이트한 횟수</summary>
        public int modelUpdates;

        // 모델 숫자는 JSON에 문자열로 저장한다 (UnityEngine.JsonUtility가 1e-05 같은 지수 표기를 제대로 읽지 못한다).
        // 메모리에서는 아래 double 배열을 쓰고, 저장 직전 · 읽은 직후에 Encode · Decode로 옮긴다 (DdaStateStore).
        [NonSerialized] public double[] modelA = new double[0];
        [NonSerialized] public double[] modelB = new double[0];
        public string modelAText = "";
        public string modelBText = "";

        public List<DdaPatternState> patterns = new List<DdaPatternState>();
        public List<DdaOverallRecord> overall = new List<DdaOverallRecord>();

        public DdaPatternState GetPattern(string id)
        {
            if (id == null) id = "";
            for (int i = 0; i < patterns.Count; i++)
                if (patterns[i].id == id) return patterns[i];
            var p = new DdaPatternState { id = id };
            patterns.Add(p);
            return p;
        }

        /// <summary>저장된 모델. 없거나 크기가 맞지 않으면 null. 돌려주는 모델은 이 상태의 배열을 그대로 쓴다.</summary>
        public DdaModel GetModel()
        {
            if (!hasModel) return null;
            if (modelA == null || modelA.Length != DdaConstants.FeatureSize * DdaConstants.FeatureSize) return null;
            if (modelB == null || modelB.Length != DdaConstants.FeatureSize) return null;
            return new DdaModel(modelA, modelB);
        }

        /// <summary>초기값으로 모델을 새로 시작한다 (배열은 복사한다).</summary>
        public void SetModel(double[] a, double[] b, string version)
        {
            modelA = (double[])a.Clone();
            modelB = (double[])b.Clone();
            priorVersion = version ?? "";
            modelUpdates = 0;
            hasModel = true;
        }

        public void ClearModel()
        {
            hasModel = false;
            priorVersion = "";
            modelUpdates = 0;
            modelA = new double[0];
            modelB = new double[0];
        }

        /// <summary>저장 직전: 모델 배열을 문자열 칸으로 옮긴다.</summary>
        public void EncodeModelText()
        {
            modelAText = hasModel ? DdaNumberText.Join(modelA) : "";
            modelBText = hasModel ? DdaNumberText.Join(modelB) : "";
        }

        /// <summary>읽은 직후: 문자열 칸에서 모델 배열을 되살린다. 읽지 못하면 모델을 비운다.</summary>
        public void DecodeModelText()
        {
            if (hasModel && DdaNumberText.TryParse(modelAText, out var a) && DdaNumberText.TryParse(modelBText, out var b))
            {
                modelA = a;
                modelB = b;
            }
            else
            {
                modelA = new double[0];
                modelB = new double[0];
            }
        }

        /// <summary>JSON에서 읽은 뒤 빈 칸을 채우고 범위를 맞춘다.</summary>
        public void Normalize()
        {
            if (bossId == null) bossId = "";
            if (priorVersion == null) priorVersion = "";
            if (modelAText == null) modelAText = "";
            if (modelBText == null) modelBText = "";
            if (patterns == null) patterns = new List<DdaPatternState>();
            if (overall == null) overall = new List<DdaOverallRecord>();
            if (modelA == null) modelA = new double[0];
            if (modelB == null) modelB = new double[0];
            if (attempts < 0) attempts = 0;
            if (loseStreak < 0) loseStreak = 0;
            patterns.RemoveAll(p => p == null);
            foreach (var p in patterns)
            {
                if (p.id == null) p.id = "";
                if (p.history == null) p.history = new List<DdaRecord>();
                p.history.RemoveAll(r => r == null);
                Trim(p.history);
                p.step = DdaConstants.ClampStep(p.step);
            }
            overall.RemoveAll(r => r == null);
            Trim(overall);
            if (hasModel && GetModel() == null) ClearModel();
        }

        public static void Trim<T>(List<T> list)
        {
            int extra = list.Count - DdaConstants.HistoryLength;
            if (extra > 0) list.RemoveRange(0, extra);
        }
    }

    /// <summary>
    /// 숫자 배열 ↔ 글자. 저장 파일 · 초기값 파일의 A · b에 쓴다.
    /// UnityEngine.JsonUtility는 1e-05 같은 지수 표기를 제대로 읽지 못하므로 숫자 배열은 여기서 직접 읽고 쓴다.
    /// </summary>
    public static class DdaNumberText
    {
        public static string Join(double[] values)
        {
            if (values == null || values.Length == 0) return "";
            var sb = new StringBuilder(values.Length * 24);
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(values[i].ToString("G17", CultureInfo.InvariantCulture)); // G17은 항상 같은 값으로 되돌아온다
            }
            return sb.ToString();
        }

        public static bool TryParse(string text, out double[] values)
        {
            values = new double[0];
            if (string.IsNullOrWhiteSpace(text)) return true;
            var parts = text.Split(new[] { ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new double[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])) return false;
            values = result;
            return true;
        }

        /// <summary>JSON 글에서 "key": [ 숫자, … ] 배열을 직접 읽는다.</summary>
        public static bool TryExtractJsonArray(string json, string key, out double[] values)
        {
            values = null;
            if (string.IsNullOrEmpty(json)) return false;
            string needle = "\"" + key + "\"";
            int from = 0;
            while (true)
            {
                int k = json.IndexOf(needle, from, StringComparison.Ordinal);
                if (k < 0) return false;
                int i = k + needle.Length;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i < json.Length && json[i] == ':')
                {
                    i++;
                    while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                    if (i < json.Length && json[i] == '[')
                    {
                        int end = json.IndexOf(']', i + 1);
                        if (end < 0) return false;
                        return TryParse(json.Substring(i + 1, end - i - 1), out values);
                    }
                }
                from = k + needle.Length;
            }
        }
    }
}
