using System.Collections.Generic;
using ReversalTower.Progress;
using UnityEngine;

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

/// <summary>
/// 로비 강화 카드의 규칙 (화면은 ReversalTower.Lobby.LobbyMenu가 그림).
///
/// - 카드 3장은 스탯이 겹치지 않게 뽑고, 등급은 아래 확률로 정한다 (예전 가챠 규칙 그대로).
/// - 3장은 progress.json에 저장된다 → 창을 닫았다 열거나 게임을 다시 켜도 그대로.
/// - 로비 방문 1번에 1장. 전투 정산(BattleStatsManager.SettleAndSaveGold) 때 다시 열리고 새 3장이 뽑힌다.
/// - 비용 = gachaCost + costStep × 지금까지 고른 장수. 고를 때만 골드를 낸다.
/// - 고른 강화는 저장되어 계속 쌓이고, 플레이어가 씬마다 기본값 위에 다시 쌓는다 (PlayerMovement.ApplyPermanentCards).
/// - 확률·수치는 인스펙터와 디버그 패널(F1)에서 조절한다. 놓인 카드는 스탯·등급만 저장하고 수치는 볼 때마다 지금 설정에서 읽는다.
/// </summary>
public class BuffGachaManager : MonoBehaviour
{
    public static BuffGachaManager Instance { get; private set; }

    [Header("비용")]
    [Tooltip("첫 카드 비용")]
    [SerializeField] private int gachaCost = 100;
    [Tooltip("카드를 한 장 고를 때마다 오르는 비용")]
    [SerializeField] private int costStep = 25;

    [Header("⭐ 등급별 등장 확률 (%)")]
    [SerializeField] public TierDropRates dropRates = new TierDropRates();

    [Header("⭐ 스탯별/등급별 증가 수치 인스펙터 설정")]
    [SerializeField] public StatTierValues attackValues = new StatTierValues { common = 1f, rare = 2f, epic = 3f, legendary = 6f, chaosMin = -5f, chaosMax = 10f };
    [SerializeField] public StatTierValues healthValues = new StatTierValues { common = 1f, rare = 2f, epic = 3f, legendary = 6f, chaosMin = -15f, chaosMax = 10f };
    [SerializeField] public StatTierValues speedValues = new StatTierValues { common = 0.1f, rare = 0.3f, epic = 0.5f, legendary = 1.0f, chaosMin = -5.0f, chaosMax = 5.0f };
    [SerializeField] public StatTierValues jumpValues = new StatTierValues { common = 1f, rare = 2f, epic = 3f, legendary = 5f, chaosMin = -10f, chaosMax = 10f };

    /// <summary>카드 한 장을 고른 결과</summary>
    public struct PickResult
    {
        public StatType stat;
        public BuffTier tier;
        public float value;
        public int cost;
    }

    /// <summary>다음 카드 한 장의 비용</summary>
    public int CurrentCost => Mathf.Max(0, gachaCost + Mathf.Max(0, costStep) * ProgressStore.Current.picks.Count);

    /// <summary>이번 로비 방문에서 이미 골랐는지</summary>
    public bool CardChosen => ProgressStore.Current.cardChosen;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>(디버그 패널 호환용) 메뉴형 로비는 골드를 매 프레임 다시 읽으므로 할 일이 없다.</summary>
    public void UpdateLobbyGoldUI()
    {
    }

    // =========================================================
    // 놓인 3장
    // =========================================================

    /// <summary>
    /// 지금 놓인 카드 3장 (수치는 현재 설정 기준). 없으면 새로 뽑아 저장한다.
    /// 이번 방문에 이미 골랐으면 빈 목록.
    /// </summary>
    public List<BuffOption> GetOffer()
    {
        var options = new List<BuffOption>();
        if (CardChosen) return options;

        foreach (var slot in EnsureOffer())
        {
            options.Add(CreateBuffOption(slot.stat, slot.tier));
        }
        return options;
    }

    private List<CardOfferSlot> EnsureOffer()
    {
        var data = ProgressStore.Current;
        if (data.offer.Count == 3) return data.offer;

        var next = ProgressStore.Clone(data);
        next.offer.Clear();
        foreach (StatType stat in GetRandomStatTypes(3))
        {
            next.offer.Add(new CardOfferSlot { stat = stat, tier = RollTier() });
        }

        if (!ProgressStore.Commit(next))
        {
            // 저장이 안 되는 환경이어도 이번에는 보여 준다 (다음에 열면 다시 뽑힐 수 있음)
            Debug.LogWarning($"[로비 카드] 카드 3장을 저장하지 못했습니다: {ProgressStore.LastError}");
        }
        return next.offer;
    }

    // =========================================================
    // 고르기
    // =========================================================

    /// <summary>
    /// index번째 카드를 고른다. 순서: 기록 저장 → 성공하면 골드 차감. 실패하면 아무것도 바뀌지 않고 error에 이유.
    /// 플레이어 스탯은 다음 전투 씬에서 저장 기록을 읽어 적용된다.
    /// </summary>
    public bool TryPick(int index, out PickResult result, out string error)
    {
        result = default;
        error = null;

        var data = ProgressStore.Current;
        if (data.cardChosen)
        {
            error = "이번 로비에서는 이미 골랐습니다 — 전투를 마치면 다시 열립니다";
            return false;
        }

        var offer = EnsureOffer();
        if (index < 0 || index >= offer.Count)
        {
            error = "카드를 찾지 못했습니다. 창을 닫고 다시 열어 주세요";
            return false;
        }

        int cost = CurrentCost;
        int gold = BattleStatsManager.GetCurrentGold();
        if (gold < cost)
        {
            error = $"골드가 부족합니다 — 필요 {cost}G / 보유 {gold}G";
            return false;
        }

        BuffOption option = CreateBuffOption(offer[index].stat, offer[index].tier);
        float finalValue = ResolveValue(option);

        var next = ProgressStore.Clone(data);
        next.picks.Add(new CardPick { stat = option.statType, tier = option.tier, value = finalValue, cost = cost });
        next.cardChosen = true;
        next.offer.Clear();

        if (!ProgressStore.Commit(next))
        {
            error = $"저장에 실패해 강화를 적용하지 않았습니다 (골드는 그대로입니다) · {ProgressStore.LastError}";
            return false;
        }

        // 기록이 저장된 뒤에만 골드를 뺀다
        BattleStatsManager.ConsumeGold(cost);

        result = new PickResult { stat = option.statType, tier = option.tier, value = finalValue, cost = cost };
        Debug.Log($"<color=yellow>[로비 카드] {TierName(option.tier)} {StatName(option.statType)} {Signed(option.statType, finalValue)} · {cost}G 사용 · 남은 골드 {BattleStatsManager.GetCurrentGold()}G</color>");
        return true;
    }

    /// <summary>실제로 오를 수치. 혼돈 등급은 고르는 순간 인스펙터 범위 안에서 정한다 (예전 규칙 그대로).</summary>
    private float ResolveValue(BuffOption buff)
    {
        float finalValue = buff.deltaValue;

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

        return finalValue;
    }

    // =========================================================
    // 테스트용 (인스펙터 우클릭)
    // =========================================================

    [ContextMenu("⭐ [테스트] 이번 방문 카드 다시 열기")]
    private void DebugReopenCards()
    {
        var next = ProgressStore.Clone(ProgressStore.Current);
        next.cardChosen = false;
        next.offer.Clear();
        ProgressStore.Commit(next);
        Debug.Log("<color=yellow>[테스트] 로비 카드 선택을 다시 열었습니다 (새 3장).</color>");
    }

    [ContextMenu("⭐ [테스트] 진행 기록 전부 지우기 (카드·처치·전투 기록)")]
    private void DebugResetProgress()
    {
        ProgressStore.ResetAll();
        Debug.Log("<color=yellow>[테스트] progress.json을 지웠습니다. 골드는 그대로입니다.</color>");
    }

    // =========================================================
    // 등급 뽑기 (예전 규칙 그대로)
    // =========================================================

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
        for (int i = 0; i < count && allStats.Count > 0; i++)
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

    /// <summary>혼돈 등급의 범위 문구 ("-5 ~ +10")</summary>
    public string ChaosRange(StatType stat)
    {
        StatTierValues setting = GetStatSetting(stat);
        bool isSpeed = stat == StatType.Speed;
        string minStr = isSpeed ? $"{setting.chaosMin:F1}" : $"{(int)setting.chaosMin}";
        string maxStr = isSpeed ? $"{setting.chaosMax:F1}" : $"{(int)setting.chaosMax}";
        return $"{minStr} ~ +{maxStr}";
    }

    private BuffOption CreateBuffOption(StatType stat, BuffTier tier)
    {
        BuffOption buff = new BuffOption { statType = stat, tier = tier };
        StatTierValues setting = GetStatSetting(stat);
        bool isSpeed = stat == StatType.Speed;

        string statName = StatName(stat);

        buff.buffTitle = $"{statName} 강화";

        if (tier == BuffTier.Chaos)
        {
            buff.description = $"{statName} {ChaosRange(stat)}";
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

    // =========================================================
    // 문구 (로비 화면에서도 씀)
    // =========================================================

    public static string StatName(StatType stat) => stat switch
    {
        StatType.Attack => "공격력",
        StatType.Health => "체력",
        StatType.Speed => "이동 속도",
        StatType.JumpPower => "점프력",
        _ => ""
    };

    public static string TierName(BuffTier tier) => tier switch
    {
        BuffTier.Common => "하급",
        BuffTier.Rare => "중급",
        BuffTier.Epic => "상급",
        BuffTier.Legendary => "전설",
        BuffTier.Chaos => "혼돈",
        _ => ""
    };

    /// <summary>플레이어에 실제로 더해지는 모양 그대로 (공격력·체력은 정수로 잘림, 이동 속도는 소수 한 자리)</summary>
    public static string Signed(StatType stat, float value)
    {
        if (stat == StatType.Attack || stat == StatType.Health)
        {
            int v = (int)value;
            return v >= 0 ? $"+{v}" : $"{v}";
        }
        if (stat == StatType.Speed)
        {
            return value >= 0 ? $"+{value:F1}" : $"{value:F1}";
        }
        bool whole = Mathf.Approximately(value, Mathf.Round(value));
        string s = whole ? $"{Mathf.RoundToInt(value)}" : $"{value:0.0#}";
        return value >= 0 ? $"+{s}" : s;
    }

    /// <summary>지금까지 고른 카드에서 이 스탯이 오른 합 (공격력·체력은 장마다 정수로 잘라 더함)</summary>
    public static float StatTotal(StatType stat)
    {
        float total = 0f;
        foreach (var p in ProgressStore.Current.picks)
        {
            if (p.stat != stat) continue;
            total += (stat == StatType.Attack || stat == StatType.Health) ? (int)p.value : p.value;
        }
        return total;
    }
}
