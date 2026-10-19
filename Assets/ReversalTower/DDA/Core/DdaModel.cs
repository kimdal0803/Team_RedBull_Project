using System;

namespace ReversalTower.DDA
{
    /// <summary>
    /// LinUCB 모델 (DDA 설계서 8.1 채택안 · ddasim의 LinUCBTrack).
    ///
    ///   특징      φ(x, a) = [ x , (a/2)·x , (a/2)²·x ]       x는 문맥 6개, φ는 18개
    ///   예측      θ = A⁻¹ b,  ŷ(a) = θ · φ(x, a)              다음 전투 대응 성공률
    ///   불확실성  u(a) = √( φᵀ A⁻¹ φ )
    ///   업데이트  유효 타 n ≥ 3이면  A += (n/6)·φφᵀ,  b += (n/6)·y·φ
    ///
    /// 보스마다 하나이고 패턴들이 같이 쓴다. A · b 배열은 DdaState의 배열을 그대로 가리키므로
    /// Update하면 상태가 바로 바뀐다 (저장은 DdaStateStore가 한다).
    /// Unity 없이 돌아가는 순수 C#이다.
    /// </summary>
    public sealed class DdaModel
    {
        const int F = DdaConstants.FeatureSize;

        public readonly double[] A; // F × F, 행 우선
        public readonly double[] B; // F

        readonly double[] l = new double[F * F];
        readonly double[] z = new double[F];
        readonly double[] theta = new double[F];
        readonly double[] phi = new double[F];
        readonly double[] w = new double[F];

        public DdaModel(double[] a, double[] b)
        {
            if (a == null || a.Length != F * F) throw new ArgumentException($"A는 {F * F}개여야 합니다.");
            if (b == null || b.Length != F) throw new ArgumentException($"b는 {F}개여야 합니다.");
            A = a;
            B = b;
        }

        /// <summary>A = I, b = 0 (초기값 없음). 설계서 8.3: 실제로는 쓰지 않는다.</summary>
        public static DdaModel CreateIdentity()
        {
            var a = new double[F * F];
            for (int i = 0; i < F; i++) a[i * F + i] = 1.0;
            return new DdaModel(a, new double[F]);
        }

        /// <summary>φ(x, a). 파이썬 np.concatenate([x, u*x, u*u*x])와 같은 순서 · 같은 곱셈 순서.</summary>
        public static void Phi(double[] x, int arm, double[] f)
        {
            double u = arm / 2.0;
            for (int i = 0; i < DdaConstants.ContextSize; i++)
            {
                f[i] = x[i];
                f[DdaConstants.ContextSize + i] = u * x[i];
                f[2 * DdaConstants.ContextSize + i] = u * u * x[i];
            }
        }

        /// <summary>A · b에 NaN · 무한대가 없고 A가 양의 정부호인지.</summary>
        public bool IsHealthy()
        {
            return DdaMath.AllFinite(A) && DdaMath.AllFinite(B) && DdaMath.Cholesky(A, F, l);
        }

        /// <summary>
        /// 다섯 팔(−2 ~ +2)의 예측 대응 성공률과 불확실성. 계산할 수 없으면 false.
        /// yhat · unc는 길이 5 이상이어야 한다.
        /// </summary>
        public bool Predict(double[] x, double[] yhat, double[] unc)
        {
            if (x == null || x.Length < DdaConstants.ContextSize) return false;
            if (!DdaMath.Cholesky(A, F, l)) return false;

            DdaMath.ForwardSubstitute(l, F, B, z);
            DdaMath.BackSubstitute(l, F, z, theta); // θ = A⁻¹ b

            var arms = DdaConstants.Arms;
            for (int i = 0; i < arms.Length; i++)
            {
                Phi(x, arms[i], phi);
                yhat[i] = DdaMath.Dot(theta, phi, F);
                DdaMath.ForwardSubstitute(l, F, phi, w); // w = L⁻¹ φ  →  φᵀ A⁻¹ φ = |w|²
                unc[i] = Math.Sqrt(DdaMath.Dot(w, w, F));
                if (double.IsNaN(yhat[i]) || double.IsNaN(unc[i]) || double.IsInfinity(yhat[i])) return false;
            }
            return true;
        }

        /// <summary>관측 하나를 반영한다. n = 유효 타 수, y = 대응 성공률(0~1).</summary>
        public void Update(double[] x, int arm, double y, int n)
        {
            double weight = n / 6.0;
            Phi(x, arm, phi);
            for (int i = 0; i < F; i++)
            {
                for (int j = 0; j < F; j++) A[i * F + j] += weight * (phi[i] * phi[j]);
                B[i] += weight * y * phi[i];
            }
        }
    }
}
