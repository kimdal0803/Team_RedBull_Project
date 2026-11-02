using System.Collections.Generic;
using ReversalTower.Progress;
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

/// <summary>
/// 로비 제단의 강화 카드 — 3장 중 1장 고르기.
///
/// - 제단에서 ↑/W/E → 카드 3장이 바로 보인다 (비용·보유 골드 표시). 고를 때만 골드를 낸다.
/// - 3장은 progress.json에 저장된다 → 창을 닫았다 열거나 게임을 다시 켜도 그대로.
/// - 로비 방문 1번에 1장. 전투 정산(BattleStatsManager.SettleAndSaveGold) 때 다시 열리고 새 3장이 뽑힌다.
/// - 고른 강화는 저장되어 계속 쌓인다. 플레이어는 씬마다 자기 기본값 위에 다시 쌓는다 (PlayerMovement.ApplyPermanentCards).
/// - 비용 = gachaCost + costStep × 지금까지 고른 장수.
/// - 등급(하급~전설·혼돈)과 확률·수치는 예전 그대로 인스펙터/디버그 패널에서 조절한다.
///   제단에 놓인 카드는 스탯·등급만 저장하고 수치는 열 때마다 지금 설정에서 읽는다.
/// </summary>
public class BuffGachaManager : MonoBehaviour
{
    public static BuffGachaManager Instance { get; private set; }

    [Header("플레이어 직접 연결")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("가챠 비용 및 골드 UI")]
    [Tooltip("첫 카드 비용")]
    [SerializeField] private int gachaCost = 10;
    [Tooltip("카드를 한 장 고를 때마다 오르는 비용")]
    [SerializeField] private int costStep = 25;
    [SerializeField] private Text goldWarningText;      // (확인창 전용 — 지금 흐름에서는 카드 창 아래 안내 문구를 씀)
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

    /// <summary>다음 카드 한 장의 비용</summary>
    public int CurrentCost => Mathf.Max(0, gachaCost + Mathf.Max(0, costStep) * ProgressStore.Current.picks.Count);

    private enum PanelMode { Choose, Message }

    private PanelMode mode = PanelMode.Message;
    private readonly List<BuffOption> shownOptions = new List<BuffOption>();

    // 카드 창 위·아래에 붙이는 문구와 버튼 (실행 중에 만듦 — 씬·프리팹은 그대로)
    private bool uiBuilt;
    private Text headerText;
    private Text infoText;
    private Text statusText;
    private Button closeButton;
    private Text closeLabel;

    private const string Gold = "#FDCB00";
    private const string Red = "#FF6B6B";
    private const string Dim = "#C8C8C8";

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

        EnsureUi();
        UpdateLobbyGoldUI();
    }

    private void LateUpdate()
    {
        // ESC = 건너뛰기/닫기. (MenuManager는 이 창이 열려 있으면 ESC를 무시함 — 일시정지 메뉴와 겹치지 않게)
        if (gachaPanel != null && gachaPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseCardPanel();
        }
    }

    public void UpdateLobbyGoldUI()
    {
        if (lobbyGoldText != null)
        {
            lobbyGoldText.text = $"{BattleStatsManager.GetCurrentGold()} G";
        }
    }

    // =========================================================
    // 열기 / 닫기
    // =========================================================

    /// <summary>제단 상호작용 (AltarTrigger). 이름은 예전 그대로 — 이제 확인창 없이 카드 창을 바로 연다.</summary>
    public void OpenConfirmDialog()
    {
        if (IsOpen) return;
        OpenCardPanel();
    }

    /// <summary>(확인창 '취소' 버튼) 열린 창을 모두 닫는다.</summary>
    public void OnClickCancel()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        CloseCardPanel();
    }

    /// <summary>(확인창 '확인' 버튼) 예전 흐름과의 호환용 — 카드 창으로 넘어간다. 골드는 카드를 고를 때 낸다.</summary>
    public void OnClickConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
        OpenCardPanel();
    }

    private void OpenCardPanel()
    {
        if (gachaPanel == null || cardSlots == null || cardSlots.Length < 3)
        {
            Debug.LogWarning("BuffGachaManager: gachaPanel 또는 카드 슬롯 3개가 연결되지 않았습니다!");
            return;
        }

        EnsureUi();
        gachaPanel.SetActive(true);
        Time.timeScale = 0f;

        if (ProgressStore.Current.cardChosen)
        {
            ShowAlreadyChosen();
            return;
        }

        ShowOffer(EnsureOffer());
    }

    private void CloseCardPanel()
    {
        if (gachaPanel != null) gachaPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // =========================================================
    // 제단에 놓인 3장
    // =========================================================

    /// <summary>저장된 3장을 쓰고, 없으면 새로 뽑아 저장한다.</summary>
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

    private void ShowOffer(List<CardOfferSlot> offer)
    {
        mode = PanelMode.Choose;
        shownOptions.Clear();

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null) continue;
            bool has = i < offer.Count;
            cardSlots[i].gameObject.SetActive(has);
            if (!has) continue;

            BuffOption option = CreateBuffOption(offer[i].stat, offer[i].tier);
            shownOptions.Add(option);
            cardSlots[i].Setup(option);
        }

        int cost = CurrentCost;
        int gold = BattleStatsManager.GetCurrentGold();
        string goldPart = gold >= cost
            ? $"보유 {gold}G"
            : $"보유 {gold}G  <color={Red}>(부족)</color>";

        SetHeader("강화 카드 — 한 장을 고르세요");
        SetInfo($"비용 <color={Gold}>{cost}G</color>    ·    {goldPart}");
        SetStatus($"{TotalsLine()}\n<color={Dim}>전투를 한 번 마칠 때마다 한 장 · 고른 강화는 게임을 꺼도 남습니다</color>");
        SetClose("건너뛰기");
    }

    private void ShowMessage(string header, string info, string status)
    {
        mode = PanelMode.Message;
        shownOptions.Clear();
        foreach (var slot in cardSlots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }

        SetHeader(header);
        SetInfo(info);
        SetStatus(status);
        SetClose("닫기");
    }

    // =========================================================
    // 고르기 (카드 버튼 → BuffCardUI → 여기)
    // =========================================================

    public void SelectBuff(BuffOption buff)
    {
        if (buff == null || mode != PanelMode.Choose || !shownOptions.Contains(buff)) return;

        var data = ProgressStore.Current;
        if (data.cardChosen)
        {
            ShowAlreadyChosen();
            return;
        }

        int cost = CurrentCost;
        int gold = BattleStatsManager.GetCurrentGold();
        if (gold < cost)
        {
            Debug.LogWarning($"[로비 카드] 골드가 부족합니다! 필요 {cost}G / 보유 {gold}G");
            SetStatus($"<color={Red}>골드가 부족합니다 — 필요 {cost}G / 보유 {gold}G</color>\n<color={Dim}>건너뛰고 전투에서 골드를 모아 오세요</color>");
            return;
        }

        float finalValue = ResolveValue(buff);

        // 1) 기록 먼저 저장 — 실패하면 아무것도 바꾸지 않는다
        var next = ProgressStore.Clone(data);
        next.picks.Add(new CardPick { stat = buff.statType, tier = buff.tier, value = finalValue, cost = cost });
        next.cardChosen = true;
        next.offer.Clear();

        if (!ProgressStore.Commit(next))
        {
            SetStatus($"<color={Red}>저장에 실패해 강화를 적용하지 않았습니다 (골드는 그대로입니다)</color>\n<color={Dim}>{ProgressStore.LastError}</color>");
            return;
        }

        // 2) 저장이 끝난 뒤에만 골드를 빼고 지금 플레이어에 반영
        BattleStatsManager.ConsumeGold(cost);

        if (playerMovement != null)
        {
            playerMovement.ApplyPermanentPick(buff.statType, finalValue, true);
        }
        else
        {
            Debug.LogWarning("BuffGachaManager: playerMovement가 인스펙터에 연결되지 않았습니다! (강화는 저장됐고 다음 씬부터 적용됩니다)");
        }

        UpdateLobbyGoldUI();
        PlayerUI playerUI = Object.FindAnyObjectByType<PlayerUI>();
        if (playerUI != null) playerUI.UpdateStatusTexts();

        Debug.Log($"<color=yellow>[로비 카드] {TierName(buff.tier)} {StatName(buff.statType)} {Signed(buff.statType, finalValue)} · {cost}G 사용 · 남은 골드 {BattleStatsManager.GetCurrentGold()}G</color>");

        string result = buff.tier == BuffTier.Chaos
            ? $"혼돈의 주사위 → {StatName(buff.statType)} <color={Gold}>{Signed(buff.statType, finalValue)}</color>"
            : $"[ {TierName(buff.tier)} ]  {StatName(buff.statType)} <color={Gold}>{Signed(buff.statType, finalValue)}</color>";

        ShowMessage(
            "강화 완료",
            result,
            $"{TotalsLine()}\n<color={Dim}>남은 골드 {BattleStatsManager.GetCurrentGold()}G · 다음 카드는 전투를 마치고 오면 열립니다</color>");
    }

    private void ShowAlreadyChosen()
    {
        ShowMessage(
            "이번 방문의 강화는 이미 골랐습니다",
            TotalsLine(),
            $"<color={Dim}>전투를 마치고 돌아오면 새 카드 3장이 열립니다</color>");
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

    [ContextMenu("⭐ [테스트] 로비 카드 기록 전부 지우기")]
    private void DebugResetCards()
    {
        ProgressStore.ResetAll();
        Debug.Log("<color=yellow>[테스트] 로비 카드 기록(progress.json)을 지웠습니다. 씬을 다시 시작하면 기본 스탯으로 돌아갑니다.</color>");
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

    private BuffOption CreateBuffOption(StatType stat, BuffTier tier)
    {
        BuffOption buff = new BuffOption { statType = stat, tier = tier };
        StatTierValues setting = GetStatSetting(stat);
        bool isSpeed = stat == StatType.Speed;

        string statName = StatName(stat);

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

    // =========================================================
    // 문구
    // =========================================================

    private static string StatName(StatType stat) => stat switch
    {
        StatType.Attack => "공격력",
        StatType.Health => "체력",
        StatType.Speed => "이동 속도",
        StatType.JumpPower => "점프력",
        _ => ""
    };

    private static string TierName(BuffTier tier) => tier switch
    {
        BuffTier.Common => "하급",
        BuffTier.Rare => "중급",
        BuffTier.Epic => "상급",
        BuffTier.Legendary => "전설",
        BuffTier.Chaos => "혼돈",
        _ => ""
    };

    /// <summary>플레이어에 실제로 더해지는 모양 그대로 (공격력·체력은 정수로 잘림, 이동 속도는 카드처럼 소수 한 자리)</summary>
    private static string Signed(StatType stat, float value)
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

    /// <summary>"쌓인 강화: 공격력 +3 · 체력 +2 …" (아직 없으면 안내)</summary>
    private static string TotalsLine()
    {
        var picks = ProgressStore.Current.picks;
        if (picks.Count == 0) return "아직 고른 강화가 없습니다";

        var sums = new Dictionary<StatType, float>();
        foreach (var p in picks)
        {
            float add = (p.stat == StatType.Attack || p.stat == StatType.Health) ? (int)p.value : p.value;
            sums.TryGetValue(p.stat, out float s);
            sums[p.stat] = s + add;
        }

        var parts = new List<string>();
        foreach (StatType stat in new[] { StatType.Attack, StatType.Health, StatType.Speed, StatType.JumpPower })
        {
            if (sums.TryGetValue(stat, out float total))
                parts.Add($"{StatName(stat)} {Signed(stat, total)}");
        }
        return $"쌓인 강화 ({picks.Count}장):  " + string.Join("  ·  ", parts);
    }

    // =========================================================
    // 카드 창 문구·버튼 만들기
    // =========================================================

    private void EnsureUi()
    {
        if (uiBuilt || gachaPanel == null) return;
        uiBuilt = true;

        var panel = gachaPanel.transform as RectTransform;
        Text sample = gachaPanel.GetComponentInChildren<Text>(true);
        Font font = sample != null && sample.font != null
            ? sample.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        headerText = MakeText("CardHeader", panel, font, 34, FontStyle.Bold, new Vector2(0f, 300f), new Vector2(1100f, 48f));
        infoText = MakeText("CardInfo", panel, font, 24, FontStyle.Normal, new Vector2(0f, 252f), new Vector2(1100f, 36f));
        statusText = MakeText("CardStatus", panel, font, 20, FontStyle.Normal, new Vector2(0f, -266f), new Vector2(1100f, 56f));

        closeButton = MakeButton(panel, font, new Vector2(0f, -338f), new Vector2(170f, 48f));
        closeLabel = closeButton != null ? closeButton.GetComponentInChildren<Text>(true) : null;
    }

    private static Text MakeText(string name, RectTransform parent, Font font, int size, FontStyle style, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
        text.raycastTarget = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    /// <summary>확인창의 '취소' 버튼을 본떠 카드 창 아래에 둔다 (없으면 단순한 버튼을 만든다).</summary>
    private Button MakeButton(RectTransform parent, Font font, Vector2 pos, Vector2 box)
    {
        GameObject go = null;
        Button template = FindButton(confirmPanel, "CancelButton");
        if (template != null)
        {
            go = Instantiate(template.gameObject, parent, false);
        }
        else
        {
            go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            var label = MakeText("Text", (RectTransform)go.transform, font, 22, FontStyle.Normal, Vector2.zero, box);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero;
        }

        go.name = "CardCloseButton";
        go.SetActive(true);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;

        var button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();   // 본뜬 버튼의 원래 연결(OnClickCancel)을 떼어냄
        button.onClick.AddListener(CloseCardPanel);
        return button;
    }

    private static Button FindButton(GameObject root, string name)
    {
        if (root == null) return null;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            if (b.name == name) return b;
        }
        return null;
    }

    private void SetHeader(string s) { if (headerText != null) headerText.text = s; }
    private void SetInfo(string s) { if (infoText != null) infoText.text = s; }
    private void SetStatus(string s) { if (statusText != null) statusText.text = s; }
    private void SetClose(string s) { if (closeLabel != null) closeLabel.text = s; }
}
