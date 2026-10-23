using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 화면 전체를 한 번 번쩍이게 한다.
    ///
    /// 패링 성공은 흰색, 페이즈 전환은 붉은색처럼 색을 달리하면
    /// 플레이어가 소리를 못 들어도 무슨 일이 일어났는지 바로 안다.
    ///
    /// 씬에 캔버스를 미리 놓을 필요가 없다 — 처음 부를 때 스스로 만든다.
    ///   ScreenFlash.Flash(Color.white, 0.12f);
    ///
    /// 시간이 멈춰 있어도(히트스톱) 보이도록 언스케일드 시간으로 돈다.
    /// </summary>
    public static class ScreenFlash
    {
        class FlashRunner : MonoBehaviour { }

        static FlashRunner runner;
        static Image image;
        static Coroutine running;

        /// <param name="color">번쩍일 색 (알파는 최대 세기로 쓰인다)</param>
        /// <param name="duration">사라지기까지 걸리는 시간(초, 실제 시간 기준)</param>
        public static void Flash(Color color, float duration = 0.12f)
        {
            if (duration <= 0f) return;
            EnsureCanvas();
            if (image == null || runner == null) return;

            if (running != null) runner.StopCoroutine(running);
            running = runner.StartCoroutine(Routine(color, duration));
        }

        static IEnumerator Routine(Color color, float duration)
        {
            float peak = color.a <= 0f ? 1f : color.a;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(peak, 0f, t / duration);
                image.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            image.color = new Color(color.r, color.g, color.b, 0f);
            running = null;
        }

        static void EnsureCanvas()
        {
            if (image != null) return;

            var go = new GameObject("~ScreenFlash") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<FlashRunner>();

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;        // 기존 UI보다 위
            go.AddComponent<CanvasScaler>();

            var imgGo = new GameObject("Flash");
            imgGo.transform.SetParent(go.transform, false);
            image = imgGo.AddComponent<Image>();
            image.raycastTarget = false;       // 버튼 클릭을 가로채지 않게
            image.color = new Color(1f, 1f, 1f, 0f);

            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            runner = null;
            image = null;
            running = null;
        }
    }
}
