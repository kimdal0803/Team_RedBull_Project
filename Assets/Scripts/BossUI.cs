using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BossUI : MonoBehaviour
{
    public static BossUI Instance;

    [Header("UI 오브젝트")]
    [SerializeField] private GameObject bossUIRoot; // BossUI_Root 본인
    [SerializeField] private Image hpFillImage;     // HP_Fill (Image Type: Filled)
    [SerializeField] private Text bossNameText;     // 레거시 Text (보스 이름)
    [SerializeField] private Text hpText;           // 레거시 Text (체력 수치)

    [Header("연출 설정")]
    [SerializeField] private float smoothSpeed = 5f;

    private float _targetFillAmount = 1f;
    private Coroutine _smoothFillCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 처음 씬 시작 시 체력바 표시
        if (bossUIRoot != null)
        {
            bossUIRoot.SetActive(true);
        }
    }

    public void InitBossUI(string bossName, int maxHP)
    {
        if (bossUIRoot != null) bossUIRoot.SetActive(true);
        if (bossNameText != null) bossNameText.text = bossName;

        _targetFillAmount = 1f;
        if (hpFillImage != null) hpFillImage.fillAmount = 1f;

        UpdateHPText(maxHP, maxHP);
    }

    public void UpdateBossHP(int currentHP, int maxHP)
    {
        if (bossUIRoot != null && !bossUIRoot.activeSelf)
        {
            bossUIRoot.SetActive(true);
        }

        _targetFillAmount = Mathf.Clamp01((float)currentHP / maxHP);

        if (_smoothFillCoroutine != null) StopCoroutine(_smoothFillCoroutine);
        _smoothFillCoroutine = StartCoroutine(SmoothFillRoutine());

        UpdateHPText(currentHP, maxHP);

        if (currentHP <= 0)
        {
            StartCoroutine(HideUIAfterDelay(2.5f));
        }
    }

    private IEnumerator SmoothFillRoutine()
    {
        while (hpFillImage != null && Mathf.Abs(hpFillImage.fillAmount - _targetFillAmount) > 0.002f)
        {
            hpFillImage.fillAmount = Mathf.Lerp(hpFillImage.fillAmount, _targetFillAmount, Time.deltaTime * smoothSpeed);
            yield return null;
        }

        if (hpFillImage != null) hpFillImage.fillAmount = _targetFillAmount;
    }

    private void UpdateHPText(int current, int max)
    {
        if (hpText != null)
        {
            hpText.text = $"{Mathf.Max(0, current)} / {max}";
        }
    }

    private IEnumerator HideUIAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (bossUIRoot != null) bossUIRoot.SetActive(false);
    }
}