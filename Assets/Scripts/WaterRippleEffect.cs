using UnityEngine;

public class WaterRippleEffect : MonoBehaviour
{
    [SerializeField] private float expandSpeed = 2.5f;   // 퍼지는 속도
    [SerializeField] private float fadeSpeed = 2.0f;     // 사라지는 속도
    [SerializeField] private Vector3 maxScale = new Vector3(2.5f, 0.8f, 1f); // 타원형 최대 크기

    private SpriteRenderer _sr;
    private Color _currentColor;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr != null)
        {
            _currentColor = _sr.color;
        }
    }

    void Update()
    {
        // 1. 가로로 넓은 타원 형태로 파장 확장 (2.5D 바닥 느낌)
        transform.localScale = Vector3.MoveTowards(transform.localScale, maxScale, expandSpeed * Time.deltaTime);

        // 2. 서서히 투명해짐
        if (_sr != null)
        {
            _currentColor.a -= fadeSpeed * Time.deltaTime;
            _sr.color = _currentColor;

            if (_currentColor.a <= 0f)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject, 0.5f);
        }
    }
}