using System.Collections;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 짧게 시간을 멈춰 타격감을 만든다 (히트스톱).
    ///
    /// 패링 성공 순간 0.08초쯤 멈췄다 풀면 "막아냈다"는 느낌이 확 산다.
    /// 소울라이크에서 패링 손맛의 절반은 이 연출이다.
    ///
    /// 씬에 아무것도 놓을 필요가 없다 — 처음 부를 때 스스로 만들어진다.
    ///   HitStopper.Stop(0.08f);
    /// </summary>
    public static class HitStopper
    {
        class HitStopRunner : MonoBehaviour { }

        static HitStopRunner runner;
        static Coroutine running;
        static float normalScale = 1f;
        static float endTime;      // 언스케일드 기준 종료 시각

        /// <summary>지금 멈춰 있는지</summary>
        public static bool IsStopped => running != null;

        /// <param name="duration">멈출 시간(초, 실제 시간 기준)</param>
        /// <param name="timeScale">멈춤 중 시간 배속. 0이면 완전 정지, 0.05면 아주 느리게</param>
        public static void Stop(float duration, float timeScale = 0f)
        {
            if (duration <= 0f) return;
            EnsureRunner();
            if (runner == null) return;

            float newEnd = Time.unscaledTime + duration;

            if (running != null)
            {
                // 이미 멈춰 있으면 더 긴 쪽으로 늘리기만 한다 (겹쳐도 시간 배속이 꼬이지 않게)
                if (newEnd > endTime) endTime = newEnd;
                return;
            }

            normalScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            endTime = newEnd;
            running = runner.StartCoroutine(Routine(timeScale));
        }

        static IEnumerator Routine(float scale)
        {
            Time.timeScale = Mathf.Max(0f, scale);
            while (Time.unscaledTime < endTime) yield return null;
            Time.timeScale = normalScale;
            running = null;
        }

        /// <summary>일시정지 메뉴 등에서 강제로 되돌려야 할 때</summary>
        public static void CancelNow()
        {
            if (runner != null && running != null) runner.StopCoroutine(running);
            running = null;
            Time.timeScale = normalScale;
        }

        static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("~HitStopper") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<HitStopRunner>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            runner = null;
            running = null;
            normalScale = 1f;
            endTime = 0f;
        }
    }
}
