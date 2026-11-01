using System;
using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 파네스가 플레이어에게 거는 상태 이상 (기획서 4.3 환영 분신 오답: 암흑/침묵).
    ///
    ///   암흑   화면이 어두워지고 플레이어 주변만 보인다 — 여기서 바로 적용된다.
    ///   침묵   스킬 사용 불가 — 플레이어 코드가 확인해야 한다.
    ///          PlayerMovement의 Q/W/E 입력 조건에 아래 한 줄을 더하면 된다 (정훈 담당 코드라 직접 고치지 않았다):
    ///              && !ReversalTower.Boss.Phanes.PhanesStatusEffects.IsSilenced
    /// </summary>
    public static class PhanesStatusEffects
    {
        static float silencedUntil = -1f;

        public static bool IsSilenced => Time.time < silencedUntil;
        public static float SilenceRemaining => Mathf.Max(0f, silencedUntil - Time.time);
        public static float DarknessRemaining => PhanesDarknessOverlay.Remaining;

        /// <summary>침묵이 걸릴 때 (지속 시간). UI·플레이어가 구독할 수 있다.</summary>
        public static event Action<float> SilenceApplied;

        public static void ApplySilence(float seconds)
        {
            if (seconds <= 0f) return;
            silencedUntil = Mathf.Max(silencedUntil, Time.time + seconds);
            Debug.Log($"[파네스] 침묵 {seconds:0.#}초 — 스킬 사용 불가 (PlayerMovement 연결 필요)");
            SilenceApplied?.Invoke(seconds);
        }

        public static void ApplyDarkness(Transform follow, float seconds)
        {
            if (seconds <= 0f) return;
            PhanesDarknessOverlay.Show(follow, seconds);
            Debug.Log($"[파네스] 암흑 {seconds:0.#}초");
        }

        // 에디터에서 Domain Reload를 꺼 두어도 이전 Play의 값이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            silencedUntil = -1f;
            SilenceApplied = null;
        }

        public static void ClearAll()
        {
            silencedUntil = -1f;
            PhanesDarknessOverlay.Hide();
        }
    }
}
