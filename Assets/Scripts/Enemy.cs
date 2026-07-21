using System.Collections;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("몬스터 능력치")]
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;

    private int _currentHealth;
    private Transform _playerTransform;
    private Rigidbody2D _rb;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private bool _isHit = false;

    void Start()
    {
        _currentHealth = maxHealth;
        _rb = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
    }

    void Update()
    {
        // 피격 중이 아닐 때 플레이어 추적
        if (_playerTransform != null && !_isHit)
        {
            Vector2 direction = (_playerTransform.position - transform.position).normalized;
            _rb.linearVelocity = new Vector2(direction.x * moveSpeed, _rb.linearVelocity.y);

            if (direction.x != 0)
            {
                transform.localScale = new Vector3(direction.x > 0 ? 1f : -1f, 1f, 1f);
            }
        }
    }

    // 플레이어의 AttackRange(Trigger)에 닿았을 때
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Attack"))
        {
            PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
            int damage = player != null ? player.GetDamage() : 1;
            TakeDamage(damage);
        }
    }

    public void TakeDamage(int damage)
    {
        _currentHealth -= damage;
        Debug.Log($"몬스터 피격! 남은 체력: {_currentHealth}");

        StartCoroutine(FlashRedRoutine());

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashRedRoutine()
    {
        _isHit = true;
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = hitColor;
        }

        yield return new WaitForSeconds(flashDuration);

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
        _isHit = false;
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}