using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 스프라이트 여러 장을 순서대로 넘겨 재생하는 한 번짜리 이펙트.
    /// 애니메이터 컨트롤러를 만들지 않아도 되도록 가장 단순하게 만들었다.
    ///
    /// 프리팹은 '아트 세팅' 메뉴가 자동으로 만든다 (Assets/Data/Effects).
    /// 재생이 끝나면 스스로 사라진다.
    /// </summary>
    [AddComponentMenu("역전의 탑/스프라이트 이펙트")]
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteSheetEffect : MonoBehaviour
    {
        [Tooltip("순서대로 재생할 스프라이트")]
        public Sprite[] frames;
        [Tooltip("초당 프레임 수")]
        [Min(1f)] public float fps = 18f;
        [Tooltip("반복 재생. 끄면 한 번 재생하고 사라진다")]
        public bool loop = false;
        [Tooltip("재생이 끝나면 오브젝트를 없앤다")]
        public bool destroyOnFinish = true;
        [Tooltip("재생 중 서서히 투명해진다")]
        public bool fadeOut = false;

        SpriteRenderer sr;
        float t;
        Color baseColor;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
            if (frames != null && frames.Length > 0) sr.sprite = frames[0];
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;

            t += Time.deltaTime * fps;
            int i = Mathf.FloorToInt(t);

            if (i >= frames.Length)
            {
                if (loop) { t -= frames.Length; i = 0; }
                else
                {
                    if (destroyOnFinish) Destroy(gameObject);
                    else sr.enabled = false;
                    return;
                }
            }

            sr.sprite = frames[i];

            if (fadeOut)
            {
                var c = baseColor;
                c.a = baseColor.a * (1f - (float)i / frames.Length);
                sr.color = c;
            }
        }

        /// <summary>코드에서 바로 띄울 때 쓴다.</summary>
        public static SpriteSheetEffect Spawn(GameObject prefab, Vector3 position, int facing = 1, float scale = 1f)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, position, Quaternion.identity);
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(Mathf.Abs(s.x) * facing * scale, Mathf.Abs(s.y) * scale, s.z);
            return go.GetComponent<SpriteSheetEffect>();
        }
    }
}
