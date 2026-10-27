using System;

namespace ReversalTower.DDA
{
    /// <summary>
    /// DDA 설계서의 고정값 (5 · 7 · 8 · 9장). 바꾸면 ddasim/ref_engine.py도 같이 바꾸고 정답 파일을 다시 만든다.
    /// </summary>
    public static class DdaConstants
    {
        /// <summary>문맥 숫자 개수 x0~x5 (6장)</summary>
        public const int ContextSize = 6;
        /// <summary>특징 φ(x, a) = [x, (a/2)·x, (a/2)²·x] 의 길이 (8.1)</summary>
        public const int FeatureSize = 3 * ContextSize;
        /// <summary>팔 = 보정 단계 −2 ~ +2 (5장)</summary>
        public static readonly int[] Arms = { -2, -1, 0, 1, 2 };
        public const int MinStep = -2;
        public const int MaxStep = 2;

        /// <summary>목표 구간 (7장)</summary>
        public const double BandLow = 0.6;
        public const double BandHigh = 0.8;
        public const double Target = 0.7;

        /// <summary>유효 타가 이보다 적으면 배우지 않고 단계도 그대로 (9장)</summary>
        public const int MinStrikes = 3;
        /// <summary>이만큼 연속으로 지면 모든 패턴을 한 칸 더 쉽게 (9장)</summary>
        public const int ProtectAfterLosses = 3;
        /// <summary>문맥에 쓰는 최근 전투 수 (6장)</summary>
        public const int HistoryLength = 3;

        public const double DefaultAlpha = 0.2;
        /// <summary>기록이 모자랄 때 x1 · x4에 넣는 실패율 (6장, 플레이테스트 평균으로 바꾼다)</summary>
        public const double DefaultFailRate = 0.4;

        public static int ArmIndex(int step) => step - MinStep;
        public static int ClampStep(int step) => Math.Max(MinStep, Math.Min(MaxStep, step));
    }

    /// <summary>
    /// 작은 대칭 양의 정부호 행렬(18×18)용 계산. 행렬은 행 우선 1차원 배열이다 (a[i * n + j]).
    /// 역행렬을 직접 만들지 않고 촐레스키 분해(A = L·Lᵀ)로 푼다. 값은 파이썬(numpy)과 1e-9 안에서 같다.
    /// </summary>
    public static class DdaMath
    {
        /// <summary>A = L·Lᵀ. A가 양의 정부호가 아니거나 NaN이 있으면 false.</summary>
        public static bool Cholesky(double[] a, int n, double[] l)
        {
            Array.Clear(l, 0, n * n);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = a[i * n + j];
                    for (int k = 0; k < j; k++) sum -= l[i * n + k] * l[j * n + k];
                    if (i == j)
                    {
                        if (!(sum > 0.0) || double.IsInfinity(sum)) return false; // NaN도 여기서 걸린다
                        l[i * n + i] = Math.Sqrt(sum);
                    }
                    else
                    {
                        l[i * n + j] = sum / l[j * n + j];
                    }
                }
            }
            return true;
        }

        /// <summary>L·z = v 를 푼다 (전진 대입).</summary>
        public static void ForwardSubstitute(double[] l, int n, double[] v, double[] z)
        {
            for (int i = 0; i < n; i++)
            {
                double s = v[i];
                for (int k = 0; k < i; k++) s -= l[i * n + k] * z[k];
                z[i] = s / l[i * n + i];
            }
        }

        /// <summary>Lᵀ·x = z 를 푼다 (후진 대입).</summary>
        public static void BackSubstitute(double[] l, int n, double[] z, double[] x)
        {
            for (int i = n - 1; i >= 0; i--)
            {
                double s = z[i];
                for (int k = i + 1; k < n; k++) s -= l[k * n + i] * x[k];
                x[i] = s / l[i * n + i];
            }
        }

        public static double Dot(double[] a, double[] b, int n)
        {
            double s = 0.0;
            for (int i = 0; i < n; i++) s += a[i] * b[i];
            return s;
        }

        public static bool AllFinite(double[] a)
        {
            if (a == null) return false;
            for (int i = 0; i < a.Length; i++)
                if (double.IsNaN(a[i]) || double.IsInfinity(a[i])) return false;
            return true;
        }
    }
}
