using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("점프 설정")]
    [SerializeField] private float jumpForce = 10f;     // 점프 힘
    [SerializeField] private Transform groundCheck;    // 바닥 체크 오브젝트의 위치
    [SerializeField] private LayerMask groundLayer;     // 바닥으로 인식할 레이어

    private float _horizontalInput;
    private bool _isJumpPressed;
    private bool _isGrounded;
    private Rigidbody2D _rb;
    private readonly Vector2 _boxSize = new Vector2(0.8f, 0.1f); // 바닥 감지 박스 크기

    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 1. 좌우 입력 감지
        _horizontalInput = Input.GetAxisRaw("Horizontal");

        // 2. 바닥에 닿아 있는지 체크 (위치, 크기, 각도, 레이어)
        _isGrounded = Physics2D.OverlapBox(groundCheck.position, _boxSize, 0f, groundLayer);

        // 3. 바닥에 있고 스페이스바를 누르면 점프 예약
        if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
        {
            _isJumpPressed = true;
        }
    }

    void FixedUpdate()
    {
        // 좌우 이동 처리
        _rb.linearVelocity = new Vector2(_horizontalInput * moveSpeed, _rb.linearVelocity.y);

        // 점프 처리
        if (_isJumpPressed)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _isJumpPressed = false; // 점프 소모
        }
    }

    // 에디터 뷰에서 바닥 체크 영역을 눈으로 보기 위한 함수
    private void OnDrawGizmos()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(groundCheck.position, _boxSize);
        }
    }
}