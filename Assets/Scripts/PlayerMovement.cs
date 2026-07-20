using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f; // 캐릭터 이동 속도

    private float _horizontalInput;
    private Rigidbody2D _rb;

    void Start()
    {
        // 오브젝트가 가진 Rigidbody2D 컴포넌트를 가져옵니다.
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 키보드 좌우 입력 감지 (A/D, 왼쪽/오른쪽 화살표)
        _horizontalInput = Input.GetAxisRaw("Horizontal");
    }

    void FixedUpdate()
    {
        // 물리적인 이동 처리 (유니티 6 공식 속도 프로퍼티 사용)
        _rb.linearVelocity = new Vector2(_horizontalInput * moveSpeed, _rb.linearVelocity.y);
    }
}