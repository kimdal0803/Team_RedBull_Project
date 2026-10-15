using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 돌진을 막는 기둥 (아라쿠스 2페이즈 기믹).
    /// 플레이어가 이 기둥을 보스와 자기 사이에 두면 돌진이 막히고 보스가 그로기에 빠진다.
    ///
    /// 동작 원리
    ///   실제 판정은 BossController.CheckDashBlock의 Raycast가 한다.
    ///   이 컴포넌트는 "그 Raycast에 걸리도록 콜라이더와 레이어를 챙겨 주는" 역할이고,
    ///   세팅이 틀리면 Play할 때 콘솔에 경고를 띄운다.
    ///
    /// 쓰는 법
    ///   1) 레이어 'GimmickObject'를 만든다 (Project Settings ▸ Tags and Layers)
    ///   2) 빈 오브젝트에 이 컴포넌트를 붙이고 Layer를 GimmickObject로 바꾼다
    ///   3) 돌진 패턴 에셋(Arakus_Charge)의 dash.groggyObjectMask에 GimmickObject를 체크한다
    ///   4) 보스와 플레이어 사이에 놓는다
    /// </summary>
    [AddComponentMenu("역전의 탑/돌진 저지 기둥 (ChargeStopper)")]
    [RequireComponent(typeof(BoxCollider2D))]
    public class ChargeStopper : MonoBehaviour
    {
        [Header("크기 (m)")]
        [Min(0.05f)] public float width = 0.6f;
        [Min(0.05f)] public float height = 3f;

        [Header("임시 비주얼 (아트가 나오기 전)")]
        [Tooltip("켜면 회색 네모를 자동으로 만들어 눈에 보이게 한다. 아트를 붙였으면 끈다.")]
        public bool createPlaceholderVisual = true;
        public Color placeholderColor = new Color(0.55f, 0.55f, 0.6f, 1f);

        [Header("검사")]
        [Tooltip("Play할 때 레이어 설정이 맞는지 확인하고 콘솔에 알려준다")]
        public bool warnIfMisconfigured = true;
        [Tooltip("이 기둥이 있어야 할 레이어 이름")]
        public string expectedLayerName = "GimmickObject";

        BoxCollider2D box;
        static Sprite placeholderSprite;

        void Reset()
        {
            ApplySize();
        }

        void OnValidate()
        {
            ApplySize();
        }

        void Awake()
        {
            ApplySize();
            if (createPlaceholderVisual) BuildPlaceholder();
            if (warnIfMisconfigured) CheckSetup();
        }

        void ApplySize()
        {
            if (box == null) box = GetComponent<BoxCollider2D>();
            if (box == null) return;
            box.isTrigger = false;
            box.size = new Vector2(width, height);
            // 피벗을 발밑에 두어 바닥에 세우기 편하게 한다
            box.offset = new Vector2(0f, height * 0.5f);
        }

        void BuildPlaceholder()
        {
            if (transform.Find("Placeholder") != null) return;

            var go = new GameObject("Placeholder");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetPlaceholderSprite();
            sr.color = placeholderColor;
            sr.sortingOrder = -1;
            go.transform.localScale = new Vector3(width, height, 1f);
        }

        void CheckSetup()
        {
            int expected = LayerMask.NameToLayer(expectedLayerName);
            if (expected < 0)
            {
                Debug.LogWarning($"[기믹 기둥] '{expectedLayerName}' 레이어가 없습니다. Project Settings ▸ Tags and Layers에서 만들어 주세요.", this);
                return;
            }
            if (gameObject.layer != expected)
            {
                Debug.LogWarning($"[기믹 기둥] 이 오브젝트의 레이어가 '{LayerMask.LayerToName(gameObject.layer)}'입니다. " +
                                 $"'{expectedLayerName}'으로 바꿔야 돌진이 막힙니다.", this);
            }
        }

        static Sprite GetPlaceholderSprite()
        {
            if (placeholderSprite != null) return placeholderSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            placeholderSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            placeholderSprite.name = "ChargeStopper_Placeholder";
            return placeholderSprite;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.7f, 0.7f, 0.8f, 0.8f);
            Gizmos.DrawWireCube(transform.position + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0f));
        }
    }
}
