using System;

namespace ReversalTower.DDA
{
    /// <summary>정책 종류. 로그의 ddaPolicy 값과 같다 (DDA 설계서 10장).</summary>
    public enum DdaPolicyKind
    {
        Off,    // 항상 0단계 (대조군과 같음)
        Rule,   // 규칙 기반: 지난 전투가 목표 구간 아래면 한 칸 쉽게, 위면 한 칸 어렵게 (8.3)
        Random, // 플레이테스트용: 지금 단계 ±1에서 무작위 (초기값 만들기 재료, 12.3)
        LinUcb, // 채택안 (8장)
    }

    /// <summary>정책이 단계를 고를 때 받는 값.</summary>
    public struct DdaPolicyInput
    {
        /// <summary>문맥 x0~x5 (6장)</summary>
        public double[] Context;
        /// <summary>이 패턴에 지금 걸려 있는 단계</summary>
        public int CurrentStep;
        /// <summary>유효 타 3 이상이던 마지막 전투의 대응 성공률. 없으면 −1</summary>
        public double LastValidRate;
    }

    /// <summary>
    /// 정책 네 개가 따르는 틀. 고른 단계는 DdaEngine이 지금 단계 ±1 · −2~+2로 한 번 더 자른다.
    /// </summary>
    public interface IDdaPolicy
    {
        /// <summary>로그에 남는 이름 (off / rule / random / linucb)</summary>
        string Name { get; }
        int Choose(DdaPolicyInput input);
    }

    public static class DdaPolicyNames
    {
        public const string Off = "off";
        public const string Rule = "rule";
        public const string Random = "random";
        public const string LinUcb = "linucb";

        public static string Of(DdaPolicyKind kind)
        {
            switch (kind)
            {
                case DdaPolicyKind.Rule: return Rule;
                case DdaPolicyKind.Random: return Random;
                case DdaPolicyKind.LinUcb: return LinUcb;
                default: return Off;
            }
        }
    }

    public sealed class OffPolicy : IDdaPolicy
    {
        public string Name => DdaPolicyNames.Off;
        public int Choose(DdaPolicyInput input) => 0;
    }

    /// <summary>규칙 기반 (8.3). 기록이 없으면 그대로.</summary>
    public sealed class RulePolicy : IDdaPolicy
    {
        public string Name => DdaPolicyNames.Rule;

        public int Choose(DdaPolicyInput input) => ChooseStep(input.LastValidRate, input.CurrentStep);

        public static int ChooseStep(double lastRate, int cur)
        {
            if (lastRate < 0.0) return cur;
            if (lastRate < DdaConstants.BandLow) return cur - 1;
            if (lastRate > DdaConstants.BandHigh) return cur + 1;
            return cur;
        }
    }

    /// <summary>플레이테스트용 (12.3). 지금 단계에서 −1 · 0 · +1 중 하나를 같은 확률로.</summary>
    public sealed class RandomPolicy : IDdaPolicy
    {
        readonly System.Random rng;
        public RandomPolicy(System.Random rng) { this.rng = rng ?? new System.Random(); }
        public string Name => DdaPolicyNames.Random;
        public int Choose(DdaPolicyInput input) => input.CurrentStep + rng.Next(-1, 2);
    }

    /// <summary>
    /// 채택안 (8.1). 예측이 목표 구간(0.6~0.8)에 가장 가까운 팔을 고른다.
    ///   gap(a) = max(0, |ŷ(a) − 0.7| − 0.1 − α·u(a))   ← 불확실한 만큼 구간에 가까울 수도 있다고 본다
    ///   지금 단계 ±1 안에서 gap이 가장 작은 a (같으면 덜 움직이는 쪽, 그래도 같으면 쉬운 쪽)
    /// </summary>
    public sealed class LinUcbPolicy : IDdaPolicy
    {
        readonly DdaModel model;
        readonly double alpha;
        readonly double[] yhat = new double[5];
        readonly double[] unc = new double[5];
        readonly double[] score = new double[5];

        public LinUcbPolicy(DdaModel model, double alpha)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.alpha = alpha;
        }

        public string Name => DdaPolicyNames.LinUcb;

        public int Choose(DdaPolicyInput input)
        {
            if (!model.Predict(input.Context, yhat, unc))
                throw new InvalidOperationException("LinUCB 예측을 계산할 수 없습니다 (모델 고장).");
            return ChooseFromPrediction(yhat, unc, input.CurrentStep, alpha, score);
        }

        public static int ChooseFromPrediction(double[] yhat, double[] unc, int cur, double alpha, double[] scoreBuffer)
        {
            for (int i = 0; i < 5; i++)
            {
                double gap = Math.Abs(yhat[i] - DdaConstants.Target)
                             - (DdaConstants.BandHigh - DdaConstants.BandLow) / 2.0
                             - alpha * unc[i];
                scoreBuffer[i] = -Math.Max(0.0, gap);
            }
            return Rail(scoreBuffer, cur);
        }

        /// <summary>
        /// 안전장치 '한 칸 제한' (9장). 지금 단계 ±1 안에서 점수가 가장 높은 팔.
        /// 점수 차이가 1e-12 안이면 같은 것으로 보고, 덜 움직이는 쪽 → 작은 단계 순으로 고른다 (ddasim과 같음).
        /// </summary>
        public static int Rail(double[] scores, int cur)
        {
            var arms = DdaConstants.Arms;
            double max = double.NegativeInfinity;
            for (int i = 0; i < arms.Length; i++)
            {
                if (Math.Abs(arms[i] - cur) > 1) continue;
                if (scores[i] > max) max = scores[i];
            }

            int best = cur;
            int bestDist = int.MaxValue;
            for (int i = 0; i < arms.Length; i++)
            {
                if (Math.Abs(arms[i] - cur) > 1) continue;
                if (!(scores[i] >= max - 1e-12)) continue;
                int dist = Math.Abs(arms[i] - cur);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = arms[i];
                }
            }
            return best;
        }
    }
}
