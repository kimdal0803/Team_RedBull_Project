using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("능력치 설정")]
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float damage = 1f; // 플레이어에게 줄 피해량
    private float _currentHealth;

    [Header("순찰 범위 설정")]
    [SerializeField] private float patrolDistance = 3f; // 좌우로 움직일 거리

    private Vector3 _startPosition;
    private float _direction = 1f; // 1 = 오른쪽, -1 = 왼쪽
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb;

    void Start()
    {
        _currentHealth = maxHealth;
        _startPosition = transform.position;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();

        // 몬스터도 회전해서 넘어지지 않도록 고정
        if (_rb != null) _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    void Update()
    {
        // 순찰 로직: 시작 위치에서 너무 멀어지면 방향 전환
        float distanceFromStart = transform.position.x - _startPosition.x;
        if (Mathf.Abs(distanceFromStart) >= patrolDistance)
        {
            // 더 멀어지지 않게 방향을 반대로 꺾음
            _direction = distanceFromStart > 0 ? -1f : 1f;
        }
    }

    void FixedUpdate()
    {
        // 좌우 이동 처리
        _rb.linearVelocity = new Vector2(_direction * moveSpeed, _rb.linearVelocity.y);

        // 이동 방향에 맞춰 스프라이트 좌우 반전
        _spriteRenderer.flipX = _direction < 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Attack"))
        {
            TakeDamage(1f);
        }
    }

    // 플레이어와 몸으로 부딪혔을 때 대미지 주기
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // 플레이어의 스크립트를 가져와서 대미지를 줌
            PlayerMovement player = collision.gameObject.GetComponent<PlayerMovement>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
        }
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;

        // 피격 효과음 대신 붉은색 깜빡임 코루틴 실행
        StartCoroutine(HitFlashRoutine());

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        // 원래 색상 기억
        Color originalColor = _spriteRenderer.color;

        // 붉은색으로 변경
        _spriteRenderer.color = Color.red;

        // 0.1초 대기
        yield return new WaitForSeconds(0.1f);

        // 원래 색상으로 복구
        _spriteRenderer.color = originalColor;
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}