using System.Collections;
using UnityEngine;

public class DummyBoss : MonoBehaviour
{
    [Header("보스 스탯")]
    [SerializeField] private string bossName = "Dummy Boss";
    public int maxHp = 50;
    public int currentHp;

    [Header("공격 타이밍")]
    [SerializeField] private float attackInterval = 3f;
    [SerializeField] private float warningDuration = 0.5f;
    [SerializeField] private float attackActiveTime = 0.25f;
    [SerializeField] private int attackDamage = 1;

    [Header("공격 범위 (보스 중심 기준 좌우 전체 크기)")]
    [SerializeField] private float attackWidth = 10f;
    [SerializeField] private float attackHeight = 3f;
    [SerializeField] private LayerMask playerLayer;

    [Header("머리 위 경고 노란 박스 세팅")]
    [SerializeField] private Vector2 warningBoxOffset = new Vector2(0f, 1.8f);
    [SerializeField] private Vector2 warningBoxSize = new Vector2(0.5f, 0.5f);
    [SerializeField] private Color warningBoxColor = Color.yellow;

    [Header("공격 판 및 피격 색상")]
    [SerializeField] private Color attackBoxColor = new Color(1f, 0.1f, 0.1f, 0.45f);
    [SerializeField] private Color hitColor = new Color(0.7f, 0f, 1f, 1f);

    private SpriteRenderer _spriteRenderer;
    private Color _originColor;
    private GameObject _hitboxVisual;
    private SpriteRenderer _hitboxSr;
    private GameObject _warningVisual;
    private SpriteRenderer _warningSr;
    private Coroutine _hitFlashCoroutine;

    void Start()
    {
        currentHp = maxHp;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _originColor = _spriteRenderer.color;

        if (BossUI.Instance != null)
        {
            BossUI.Instance.InitBossUI(bossName, maxHp);
        }

        CreateVisuals();
        StartCoroutine(BossPatternRoutine());
    }

    private void CreateVisuals()
    {
        _hitboxVisual = new GameObject("BossAttackBox_Visual");
        _hitboxVisual.transform.SetParent(null);
        _hitboxSr = _hitboxVisual.AddComponent<SpriteRenderer>();

        Texture2D tex = Texture2D.whiteTexture;
        _hitboxSr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _hitboxSr.color = attackBoxColor;

        if (_spriteRenderer != null)
        {
            _hitboxSr.sortingLayerName = _spriteRenderer.sortingLayerName;
            _hitboxSr.sortingOrder = _spriteRenderer.sortingOrder + 1;
        }
        _hitboxVisual.SetActive(false);

        _warningVisual = new GameObject("BossWarningBox_Visual");
        _warningVisual.transform.SetParent(null);
        _warningSr = _warningVisual.AddComponent<SpriteRenderer>();

        _warningSr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        _warningSr.color = warningBoxColor;

        if (_spriteRenderer != null)
        {
            _warningSr.sortingLayerName = _spriteRenderer.sortingLayerName;
            _warningSr.sortingOrder = _spriteRenderer.sortingOrder + 2;
        }
        _warningVisual.SetActive(false);
    }

    private IEnumerator BossPatternRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(attackInterval);

            if (_warningVisual != null)
            {
                Vector2 warnPos = (Vector2)transform.position + warningBoxOffset;
                _warningVisual.transform.position = new Vector3(warnPos.x, warnPos.y, 0f);
                _warningVisual.transform.localScale = new Vector3(warningBoxSize.x, warningBoxSize.y, 1f);
                _warningVisual.SetActive(true);
            }

            yield return new WaitForSeconds(warningDuration);

            if (_warningVisual != null) _warningVisual.SetActive(false);

            Vector2 center = transform.position;
            Vector2 boxSize = new Vector2(attackWidth, attackHeight);

            if (_hitboxVisual != null)
            {
                _hitboxVisual.transform.position = new Vector3(center.x, center.y, 0f);
                _hitboxVisual.transform.localScale = new Vector3(boxSize.x, boxSize.y, 1f);
                _hitboxVisual.SetActive(true);
            }

            ExecuteAttack(center, boxSize);

            yield return new WaitForSeconds(attackActiveTime);

            if (_hitboxVisual != null) _hitboxVisual.SetActive(false);
        }
    }

    private void ExecuteAttack(Vector2 center, Vector2 boxSize)
    {
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, boxSize, 0f);
        foreach (Collider2D col in hits)
        {
            if (col.CompareTag("Player") || ((1 << col.gameObject.layer) & playerLayer) != 0)
            {
                PlayerMovement player = col.GetComponentInParent<PlayerMovement>();
                if (player != null)
                {
                    float dirX = (player.transform.position.x >= transform.position.x) ? 1f : -1f;
                    Vector2 knockbackDir = new Vector2(dirX, 0.5f).normalized;
                    player.TakeDamage(attackDamage, knockbackDir);
                    break;
                }
            }
        }
    }

    // ⭐ Player 공격으로부터 한 번만 호출되는 단일 피격 함수
    public void TakeDamage(int damage)
    {
        currentHp -= damage;
        currentHp = Mathf.Max(0, currentHp);
        Debug.Log($"<color=magenta>[DummyBoss] 피격! 받은 대미지: {damage} | 남은 HP: {currentHp}</color>");

        if (BattleStatsManager.Instance != null)
        {
            BattleStatsManager.Instance.AddDamage(damage);
        }

        if (BossUI.Instance != null)
        {
            BossUI.Instance.UpdateBossHP(currentHp, maxHp);
        }

        if (_hitFlashCoroutine != null) StopCoroutine(_hitFlashCoroutine);
        _hitFlashCoroutine = StartCoroutine(HitFlashRoutine());

        if (currentHp <= 0)
        {
            Debug.Log("<color=green>[DummyBoss] 보스 처치! 클리어 정산 시작</color>");
            CleanupVisuals();

            if (BossClearManager.Instance != null)
            {
                BossClearManager.Instance.TriggerBossClear(maxHp);
            }

            Destroy(gameObject, 0.1f);
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = hitColor;
            yield return new WaitForSeconds(0.1f);
            _spriteRenderer.color = _originColor;
        }
    }

    private void CleanupVisuals()
    {
        if (_hitboxVisual != null) Destroy(_hitboxVisual);
        if (_warningVisual != null) Destroy(_warningVisual);
    }

    private void OnDestroy()
    {
        CleanupVisuals();
    }
}