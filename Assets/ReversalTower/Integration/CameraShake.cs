using UnityEngine;
using UnityEngine.Rendering;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 화면 흔들림.
    ///
    /// 카메라는 시네머신이 매 프레임 자리를 정하므로, 위치를 직접 바꾸면 서로 다툰다.
    /// 그래서 '그리기 직전'에만 살짝 옮겼다가 '그린 뒤' 바로 되돌린다 — 다른 스크립트는 흔들림을 모른다.
    /// URP(SRP)와 기본 렌더 파이프라인 둘 다 지원한다.
    ///
    ///   CameraShake.Shake(0.1f, 0.2f);   // 세기(m), 길이(초)
    ///
    /// 씬에 아무것도 놓을 필요가 없다. 겹치면 더 센 쪽이 남는다. 실제 시간 기준이라 히트스톱 중에도 흔들린다.
    /// </summary>
    public static class CameraShake
    {
        static float amplitude;
        static float duration;
        static float endTime;
        static bool hooked;

        static Camera shiftedCamera;
        static Vector3 savedPosition;
        static int offsetFrame = -1;
        static Vector3 offset;

        /// <summary>전체 끄기 (설정 메뉴 등에서)</summary>
        public static bool Enabled = true;

        public static bool IsShaking => Time.unscaledTime < endTime;

        public static void Shake(float strength, float seconds)
        {
            if (!Enabled || strength <= 0f || seconds <= 0f) return;
            Hook();
            float now = Time.unscaledTime;
            float current = IsShaking ? amplitude * Mathf.Clamp01((endTime - now) / Mathf.Max(0.0001f, duration)) : 0f;
            if (strength >= current)
            {
                amplitude = strength;
                duration = seconds;
                endTime = now + seconds;
            }
        }

        public static void Stop()
        {
            endTime = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Unhook();
            amplitude = duration = endTime = 0f;
            shiftedCamera = null;
            offsetFrame = -1;
            Enabled = true;
        }

        static void Hook()
        {
            if (hooked) return;
            RenderPipelineManager.beginCameraRendering += OnBeginSrp;
            RenderPipelineManager.endCameraRendering += OnEndSrp;
            Camera.onPreCull += OnPreCull;
            Camera.onPostRender += OnPostRender;
            hooked = true;
        }

        static void Unhook()
        {
            if (!hooked) return;
            RenderPipelineManager.beginCameraRendering -= OnBeginSrp;
            RenderPipelineManager.endCameraRendering -= OnEndSrp;
            Camera.onPreCull -= OnPreCull;
            Camera.onPostRender -= OnPostRender;
            hooked = false;
        }

        static void OnBeginSrp(ScriptableRenderContext ctx, Camera cam) => Begin(cam);
        static void OnEndSrp(ScriptableRenderContext ctx, Camera cam) => End(cam);
        static void OnPreCull(Camera cam) => Begin(cam);
        static void OnPostRender(Camera cam) => End(cam);

        static void Begin(Camera cam)
        {
            if (shiftedCamera != null || cam == null || !IsShaking || !Application.isPlaying) return;
            if (cam != Camera.main) return;
            if (offsetFrame != Time.frameCount)
            {
                offsetFrame = Time.frameCount;
                float k = Mathf.Clamp01((endTime - Time.unscaledTime) / Mathf.Max(0.0001f, duration));
                Vector2 r = Random.insideUnitCircle * (amplitude * k);
                offset = new Vector3(r.x, r.y, 0f);
            }
            savedPosition = cam.transform.position;
            cam.transform.position = savedPosition + offset;
            shiftedCamera = cam;
        }

        static void End(Camera cam)
        {
            if (cam == null || cam != shiftedCamera) return;
            cam.transform.position = savedPosition;
            shiftedCamera = null;
        }
    }
}
