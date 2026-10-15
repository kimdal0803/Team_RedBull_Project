using System.Collections.Generic;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 다음 패턴 고르기.
    ///   1) 페이즈 · 조건 · 거리 · 쿨타임을 통과한 패턴만 후보
    ///   2) 후보 중 priority가 가장 높은 묶음만 남김 (조건형 패턴을 먼저 쓰도록)
    ///   3) 다른 후보가 있으면 직전 패턴은 뺌 (같은 패턴 연속 금지, 아디쿠스 기획서 3.2)
    ///   4) weight 비율로 무작위
    /// randomSeed를 고정하면 같은 순서가 재현된다 (버그 재현, DDA 실험).
    /// </summary>
    public class PatternSelector
    {
        readonly System.Random rng;
        readonly List<BossPatternData> candidates = new List<BossPatternData>();

        public PatternSelector(int seed)
        {
            rng = seed == 0 ? new System.Random() : new System.Random(seed);
        }

        public BossPatternData Pick(BossController boss, IReadOnlyList<BossPatternData> patterns, BossPatternData last)
        {
            candidates.Clear();
            if (patterns == null) return null;

            float distance = boss.DistanceToTarget();
            int bestPriority = int.MinValue;

            foreach (var p in patterns)
            {
                if (!IsUsable(boss, p, distance)) continue;
                if (p.priority > bestPriority)
                {
                    bestPriority = p.priority;
                    candidates.Clear();
                }
                if (p.priority == bestPriority) candidates.Add(p);
            }

            if (candidates.Count > 1 && last != null) candidates.Remove(last);
            return WeightedPick();
        }

        public static bool IsUsable(BossController boss, BossPatternData p, float distance)
        {
            if (p == null || p.weight <= 0f) return false;
            if (boss.Phase < p.minPhase || boss.Phase > p.maxPhase) return false;
            if (distance < p.minDistance || distance > p.maxDistance) return false;
            if (boss.IsOnCooldown(p)) return false;
            return boss.CheckCondition(p.conditionKey);
        }

        BossPatternData WeightedPick()
        {
            if (candidates.Count == 0) return null;
            float total = 0f;
            foreach (var p in candidates) total += p.weight;

            double r = rng.NextDouble() * total;
            foreach (var p in candidates)
            {
                r -= p.weight;
                if (r <= 0) return p;
            }
            return candidates[candidates.Count - 1];
        }
    }
}
