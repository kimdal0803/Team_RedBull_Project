using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>암흑 디버프 화면. 플레이어를 따라다니는 큰 어둠 (가운데만 뚫려 있다).</summary>
    [AddComponentMenu("")]
    public class PhanesDarknessOverlay : MonoBehaviour
    {
        static PhanesDarknessOverlay instance;

        Transform follow;
        SpriteRenderer sr;
        float until;
        float alpha;

        public static float Remaining => instance != null ? Mathf.Max(0f, instance.until - Time.time) : 0f;

        public static void Show(Transform follow, float seconds)
        {
            if (instance == null)
            {
                var r = PhanesFx.Spawn("Phanes_Darkness", PhanesFx.Vignette, follow != null ? (Vector2)follow.position : Vector2.zero,
                                       Vector2.one * 44f, Color.white, PhanesFx.OrderOverlay);
                instance = r.gameObject.AddComponent<PhanesDarknessOverlay>();
                instance.sr = r;
                instance.alpha = 0f;
                instance.Apply();
            }
            instance.follow = follow;
            instance.until = Mathf.Max(instance.until, Time.time + seconds);
        }

        public static void Hide()
        {
            if (instance != null) instance.until = 0f;
        }

        void LateUpdate()
        {
            float target = Time.time < until ? 1f : 0f;
            alpha = Mathf.MoveTowards(alpha, target, Time.deltaTime * (target > alpha ? 5f : 2.5f));
            Apply();
            if (follow != null) transform.position = new Vector3(follow.position.x, follow.position.y + 0.8f, 0f);
            if (target == 0f && alpha <= 0f) Destroy(gameObject);
        }

        void Apply()
        {
            if (sr != null) sr.color = new Color(1f, 1f, 1f, alpha);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
