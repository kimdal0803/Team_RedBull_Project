using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class StatTierValues
{
    [Header("고정 등급 증가량")]
    public float common = 1f;
    public float rare = 2f;
    public float epic = 3f;
    public float legendary = 6f;

    [Header("혼돈 등급 랜덤 범위 (Min ~ Max)")]
    public float chaosMin = -5f;
    public float chaosMax = 10f;
}

[System.Serializable]
public class TierDropRates
{
    [Range(0f, 100f)] public float commonChance = 40f;     // 하급 40%
    [Range(0f, 100f)] public float rareChance = 30f;       // 중급 30%
    [Range(0f, 100f)] public float epicChance = 18f;       // 상급 18%
    [Range(0f, 100f)] public float legendaryChance = 9f;   // 전설 9%
    [Range(0f, 100f)] public float chaosChance = 3f;       // 혼돈 3%
}

public class BuffGachaManager : MonoBehaviour
{
    public static BuffGachaManager Instance { get; private set; }

    [Header("플레이어 직접 연결")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("가챠 비용 및 골드 UI")]
    [SerializeField] private int gachaCost = 10;
    [SerializeField] private Text goldWarningText;      // "골드가 부족합니다!" 텍스트
    [SerializeField] private Text lobbyGoldText;        // 로비 화면 골드 표시 텍스트

    [Header("UI 패널 연결")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private GameObject gachaPanel;
    [SerializeField] private BuffCardUI[] cardSlots;

    [Header("⭐ 등급별 등장 확률 (%)")]
    [SerializeField] public TierDropRates dropRates = new TierDropRates();

    [Header("⭐ 스탯별/등급별 증가 수치 인스펙터 설정")]
    [SerializeField] public StatTierValues attackValues = new StatTierValues { common = 1f, rare = 2f, epic = 3f, legendary = 6f, chaosMin = -5f, chaosMax = 10f };
    [SerializeField] public StatTierValues healthValues = new StatTierValues { common = 1f, rare = 2f, epic = 3f, legendary = 6f, chaosMin = -15f, chaosMax = 10f };
    [SerializeField] public StatTierValues speedValues = new StatTierValues { common = 0.1f, rare = 0.3f, epic = 0.5f, legendary = 1.0f, chaosMin = -5.0f, chaosMax = 5.0f };
    [SerializeField] public StatTierValues jumpValues = new StatTierValues { common = 1f, rare = 2f, epic = 3f, legendary = 5f, chaosMin = -10f, chaosMax = 10f };

    public bool IsOpen => (confirmPanel != null && confirmPanel.activeSelf) ||
                          (gachaPanel != null && gachaPanel.activeSelf);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (gachaPanel != null) gachaPanel.SetActive(false);
        if (goldWarningText != null) goldWarningText.gameObject.SetActive(false);

        UpdateLobbyGoldUI();
    }

    public void UpdateLobbyGoldUI()
    {
        if (lobbyGoldText != null)
        {
            lobbyGoldText.text = $"{BattleStatsManager.GetCurrentGold()} G";
        }
    }

    public void OpenConfirmDialog()
    {
        if (IsOpen) return;

        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            if (goldWarningText != null) goldWarningText.gameObject.SetActive(false);
            Time.timeScale = 0f;
        }
    }

    public void OnClickCancel()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnClickConfirm()
    {
        bool canSpend = BattleStatsManager.ConsumeGold(gachaCost);
        if (!canSpend)
        {
            Debug.LogWarning($"[가챠 실패] 골드가 부족합니다! 보유 골드: {BattleStatsManager.GetCurrentGold()}G");
            if (goldWarningText != null) goldWarningText.gameObject.SetActive(true);
            return;
        }

        Debug.Log($"<color=yellow>[골드 차감] {gachaCost}G 소모 성공! 남은 골드: {BattleStatsManager.GetCurrentGold()}G</color>");
        UpdateLobbyGoldUI();

        if (confirmPanel != null) confirmPanel.SetActive(false);
        OpenGachaPanel();
    }

    private void OpenGachaPanel()
    {
        if (gachaPanel == null || cardSlots == null || cardSlots.Length < 3) return;

        gachaPanel.SetActive(true);

        List<StatType> pickedStats = GetRandomStatTypes(3);
        for (int i = 0; i < cardSlots.Length; i++)
        {
            BuffTier rolledTier = RollTier();
            BuffOption buff = CreateBuffOption(pickedStats[i], rolledTier);
            cardSlots[i].Setup(buff);
        }
    }

    public void SelectBuff(BuffOption buff)
    {
        float finalValue = buff.deltaValue;

        // 혼돈 등급은 인스펙터에 지정된 Min/Max 범위 내에서 무작위 결정
        if (buff.tier == BuffTier.Chaos)
        {
            StatTierValues setting = GetStatSetting(buff.statType);
            if (buff.statType == StatType.Speed)
            {
                float randSpeed = Random.Range(setting.chaosMin, setting.chaosMax);
                finalValue = Mathf.Round(randSpeed * 10f) / 10f;
            }
            else
            {
                finalValue = Random.Range((int)setting.chaosMin, (int)setting.chaosMax + 1);
            }
            Debug.Log($"<color=magenta>[혼돈의 주사위] {buff.statType} 최종 결정 수치: {finalValue}</color>");
        }

        if (playerMovement != null)
        {
            playerMovement.ApplySingleBuff(buff.statType, finalValue);
        }
        else
        {
            Debug.LogWarning("BuffGachaManager: playerMovement가 인스펙터에 연결되지 않았습니다!");
        }

        if (gachaPanel != null) gachaPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private List<StatType> GetRandomStatTypes(int count)
    {
        List<StatType> allStats = new List<StatType>
        {
            StatType.Attack,
            StatType.Health,
            StatType.Speed,
            StatType.JumpPower
        };

        List<StatType> result = new List<StatType>();
        for (int i = 0; i < count; i++)
        {
            int randIndex = Random.Range(0, allStats.Count);
            result.Add(allStats[randIndex]);
            allStats.RemoveAt(randIndex);
        }
        return result;
    }

    // ⭐ 인스펙터에 입력된 확률 기반 가중치 롤링 (합계가 100이 아니어도 비율에 맞게 정밀 계산)
    private BuffTier RollTier()
    {
        float totalWeight = dropRates.commonChance + dropRates.rareChance +
                            dropRates.epicChance + dropRates.legendaryChance +
                            dropRates.chaosChance;

        if (totalWeight <= 0f) return BuffTier.Common;

        float roll = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        currentWeight += dropRates.commonChance;
        if (roll < currentWeight) return BuffTier.Common;

        currentWeight += dropRates.rareChance;
        if (roll < currentWeight) return BuffTier.Rare;

        currentWeight += dropRates.epicChance;
        if (roll < currentWeight) return BuffTier.Epic;

        currentWeight += dropRates.legendaryChance;
        if (roll < currentWeight) return BuffTier.Legendary;

        return BuffTier.Chaos;
    }

    private StatTierValues GetStatSetting(StatType stat)
    {
        return stat switch
        {
            StatType.Attack => attackValues,
            StatType.Health => healthValues,
            StatType.Speed => speedValues,
            StatType.JumpPower => jumpValues,
            _ => attackValues
        };
    }

    private BuffOption CreateBuffOption(StatType stat, BuffTier tier)
    {
        BuffOption buff = new BuffOption { statType = stat, tier = tier };
        StatTierValues setting = GetStatSetting(stat);
        bool isSpeed = stat == StatType.Speed;

        string statName = stat switch
        {
            StatType.Attack => "공격력",
            StatType.Health => "체력",
            StatType.Speed => "이동 속도",
            StatType.JumpPower => "점프력",
            _ => ""
        };

        buff.buffTitle = $"{statName} 강화";

        if (tier == BuffTier.Chaos)
        {
            string minStr = isSpeed ? $"{setting.chaosMin:F1}" : $"{(int)setting.chaosMin}";
            string maxStr = isSpeed ? $"{setting.chaosMax:F1}" : $"{(int)setting.chaosMax}";
            buff.description = $"{statName} {minStr} ~ +{maxStr}";
        }
        else
        {
            buff.deltaValue = tier switch
            {
                BuffTier.Common => setting.common,
                BuffTier.Rare => setting.rare,
                BuffTier.Epic => setting.epic,
                BuffTier.Legendary => setting.legendary,
                _ => setting.common
            };
            buff.description = FormatDesc(statName, buff.deltaValue, isFloat: isSpeed);
        }

        return buff;
    }

    private string FormatDesc(string statName, float value, bool isFloat = false)
    {
        string valStr = isFloat
            ? (value >= 0 ? $"+{value:F1}" : $"{value:F1}")
            : (value >= 0 ? $"+{(int)value}" : $"{(int)value}");
        return $"{statName} {valStr}";
    }
}