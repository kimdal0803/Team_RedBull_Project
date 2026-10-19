using System;
using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.DDA
{
    /// <summary>
    /// 팔(보정 단계 −2 ~ +2)마다 배율 묶음 (DDA 설계서 5장).
    /// 기획이 코드 없이 숫자만 바꿔 테스트할 수 있게 에셋으로 둔다.
    ///
    /// 패턴에 넣을 때 두 가지를 더 지킨다.
    ///   · 선딜은 배율을 곱한 뒤에도 minTelegraphSeconds(0.4초) 아래로 내리지 않는다 (4장 하한).
    ///     원래 선딜이 0.4초보다 짧은 패턴은 원래 값보다 줄이지 않는다.
    ///   · 연타 수 증감은 '모든 판정이 2타 이상인 연타 패턴'(예: 방패 돌진 8타)에만 건다.
    ///     PatternRun은 증감을 판정 하나하나에 더하므로, 단타 패턴에 +1을 걸면 공격이 통째로 두 번 나가기 때문이다.
    /// </summary>
    [CreateAssetMenu(fileName = "DDAArmTable", menuName = "역전의 탑/DDA 배율 표", order = 50)]
    public class DDAArmTable : ScriptableObject
    {
        [Serializable]
        public class Row
        {
            [Tooltip("보정 단계. −2 많이 쉽게 … +2 많이 어렵게")]
            public int step;
            [Tooltip("선딜 배율. 1.3이면 30% 길어짐 (쉬워짐)")]
            [Min(0.1f)] public float telegraphScale = 1f;
            [Tooltip("데미지 배율")]
            [Min(0f)] public float damageScale = 1f;
            [Tooltip("쿨타임 배율")]
            [Min(0f)] public float cooldownScale = 1f;
            [Tooltip("연타 수 증감 (연타 패턴만)")]
            public int repeatCountDelta;
            [Tooltip("연타 간격 배율")]
            [Min(0.1f)] public float repeatIntervalScale = 1f;
        }

        [Tooltip("단계 −2, −1, 0, +1, +2 순서로 5줄")]
        public Row[] rows = DefaultRows();

        [Tooltip("배율을 곱한 뒤 선딜의 최솟값(초). 패링 신호(0.3초)를 보고 반응할 틈 (DDA 설계서 4장)")]
        [Min(0f)] public float minTelegraphSeconds = 0.4f;

        /// <summary>설계서 5장 표 그대로.</summary>
        public static Row[] DefaultRows()
        {
            return new[]
            {
                new Row { step = -2, telegraphScale = 1.30f, damageScale = 0.80f, cooldownScale = 1.25f, repeatCountDelta = -1, repeatIntervalScale = 1.20f },
                new Row { step = -1, telegraphScale = 1.15f, damageScale = 0.90f, cooldownScale = 1.10f, repeatCountDelta = 0, repeatIntervalScale = 1.10f },
                new Row { step = 0, telegraphScale = 1.00f, damageScale = 1.00f, cooldownScale = 1.00f, repeatCountDelta = 0, repeatIntervalScale = 1.00f },
                new Row { step = 1, telegraphScale = 0.90f, damageScale = 1.10f, cooldownScale = 0.90f, repeatCountDelta = 0, repeatIntervalScale = 0.95f },
                new Row { step = 2, telegraphScale = 0.80f, damageScale = 1.20f, cooldownScale = 0.80f, repeatCountDelta = 1, repeatIntervalScale = 0.90f },
            };
        }

        /// <summary>에셋이 없을 때 쓰는 기본 표 (메모리에만 있음).</summary>
        public static DDAArmTable CreateDefault()
        {
            var t = CreateInstance<DDAArmTable>();
            t.name = "DDAArmTable (기본값)";
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        public Row GetRow(int step)
        {
            if (rows != null)
                foreach (var r in rows)
                    if (r != null && r.step == step) return r;
            return null;
        }

        /// <summary>
        /// 이 단계를 이 패턴에 걸 PatternTuning. 0단계면 null (보정 없음).
        /// </summary>
        public PatternTuning ToTuning(int step, BossPatternData data)
        {
            if (step == 0) return null;
            var row = GetRow(step);
            if (row == null)
            {
                Debug.LogWarning($"[DDA] 배율 표에 {step}단계 줄이 없어 보정을 걸지 않습니다.", this);
                return null;
            }

            var t = new PatternTuning
            {
                telegraphScale = row.telegraphScale,
                damageScale = row.damageScale,
                cooldownScale = row.cooldownScale,
                repeatCountDelta = 0,
                repeatIntervalScale = row.repeatIntervalScale,
            };

            if (data != null)
            {
                // 선딜 하한: 원래 선딜이 0.4초보다 길면 0.4초까지만, 짧으면 원래 값보다 줄이지 않는다.
                if (data.telegraphTime > 0f && t.telegraphScale < 1f && minTelegraphSeconds > 0f)
                {
                    float floorScale = Mathf.Min(1f, minTelegraphSeconds / data.telegraphTime);
                    t.telegraphScale = Mathf.Max(t.telegraphScale, floorScale);
                }
                if (IsMultiHitPattern(data)) t.repeatCountDelta = row.repeatCountDelta;
            }
            return t;
        }

        /// <summary>판정이 하나 이상 있고, 모든 판정이 2타 이상 반복하는 연타 패턴인지.</summary>
        public static bool IsMultiHitPattern(BossPatternData data)
        {
            if (data == null || data.hits == null) return false;
            int count = 0;
            foreach (var h in data.hits)
            {
                if (h == null) continue;
                if (h.repeatCount < 2) return false;
                count++;
            }
            return count > 0;
        }

        void OnValidate()
        {
            if (rows == null || rows.Length == 0) rows = DefaultRows();
        }
    }
}
