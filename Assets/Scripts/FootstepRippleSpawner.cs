using UnityEngine;

public class FootstepRippleSpawner : MonoBehaviour
{
    [SerializeField] private GameObject ripplePrefab;
    [SerializeField] private Transform footPosition;   // 발 위치 (GroundCheck 위치 연결)
    [SerializeField] private float stepInterval = 0.25f; // 파장 생성 주기
    [SerializeField] private LayerMask groundLayer;

    private float _stepTimer;
    private Rigidbody2D _rb;
    private bool _wasGrounded;

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (ripplePrefab == null || footPosition == null) return;

        bool isGrounded = Physics2D.OverlapCircle(footPosition.position, 0.15f, groundLayer);
        bool isMoving = _rb != null && Mathf.Abs(_rb.linearVelocity.x) > 0.2f;

        // 1. 공중에서 착지하는 순간 강한 파장 1회 발생
        if (!_wasGrounded && isGrounded)
        {
            SpawnRipple(1.4f);
        }
        _wasGrounded = isGrounded;

        // 2. 바닥에서 걷거나 뛸 때 주기적으로 파장 발생
        if (isGrounded && isMoving)
        {
            _stepTimer += Time.deltaTime;
            if (_stepTimer >= stepInterval)
            {
                SpawnRipple(1.0f);
                _stepTimer = 0f;
            }
        }
        else
        {
            _stepTimer = stepInterval * 0.8f; // 다시 걸을 때 즉시 반응하도록 보정
        }
    }

    private void SpawnRipple(float scaleMultiplier)
    {
        GameObject ripple = Instantiate(ripplePrefab, footPosition.position, Quaternion.identity);
        ripple.transform.localScale *= scaleMultiplier;
    }
}