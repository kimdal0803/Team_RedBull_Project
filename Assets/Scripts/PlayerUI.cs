using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("분할 체력바 컨테이너 및 프리팹")]
    [SerializeField] private Transform cellContainer;
    [SerializeField] private GameObject hpCellPrefab;

    [Header("색상 세팅")]
    [SerializeField] private Color activeColor = new Color32(160, 0, 20, 255);
    [SerializeField] private Color emptyColor = new Color32(40, 20, 20, 160);

    [Header("스테이터스 패널 (I키 토글)")]
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private Text statusHpText;
    [SerializeField] private Text statusAttackText;
    [SerializeField] private Text statusSpeedText;
    [SerializeField] private Text statusJumpText;
    [SerializeField] private Text statusGoldText;

    [Header("플레이어 참조 (비어있으면 자동 탐색)")]
    [SerializeField] private PlayerMovement playerMovement;

    private List<Image> _cellImages = new List<Image>();

    private void Start()
    {
        if (statusPanel != null)
        {
            statusPanel.SetActive(false);
        }

        // 최신 Unity 권장 함수 FindAnyObjectByType 사용
        if (playerMovement == null)
        {
            playerMovement = Object.FindAnyObjectByType<PlayerMovement>();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleStatusPanel();
        }
    }

    public void UpdateHPBar(float currentHP, float maxHP)
    {
        int maxInt = Mathf.RoundToInt(maxHP);
        int currentInt = Mathf.RoundToInt(currentHP);

        if (_cellImages.Count != maxInt)
        {
            CreateCells(maxInt);
        }

        for (int i = 0; i < _cellImages.Count; i++)
        {
            if (_cellImages[i] == null) continue;
            _cellImages[i].color = (i < currentInt) ? activeColor : emptyColor;
        }

        if (statusPanel != null && statusPanel.activeSelf)
        {
            UpdateStatusTexts();
        }
    }

    private void CreateCells(int count)
    {
        if (cellContainer != null)
        {
            foreach (Transform child in cellContainer)
            {
                Destroy(child.gameObject);
            }
        }
        _cellImages.Clear();

        if (hpCellPrefab == null || cellContainer == null) return;

        for (int i = 0; i < count; i++)
        {
            GameObject cell = Instantiate(hpCellPrefab, cellContainer);
            Image img = cell.GetComponent<Image>();
            if (img != null)
            {
                _cellImages.Add(img);
            }
        }
    }

    public void ToggleStatusPanel()
    {
        if (statusPanel == null) return;

        bool nextState = !statusPanel.activeSelf;
        statusPanel.SetActive(nextState);

        if (nextState)
        {
            UpdateStatusTexts();
        }
    }

    public void UpdateStatusTexts()
    {
        if (playerMovement != null)
        {
            if (statusHpText != null)
                statusHpText.text = $"체력 : {playerMovement.CurrentHealth} / {playerMovement.MaxHealth}";

            if (statusAttackText != null)
                statusAttackText.text = $"공격력 : {playerMovement.BaseAttackDamage}";

            if (statusSpeedText != null)
                statusSpeedText.text = $"이동 속도 : {playerMovement.MoveSpeed:F1}";

            if (statusJumpText != null)
                statusJumpText.text = $"점프력 : {playerMovement.JumpForce:F1}";
        }

        if (statusGoldText != null)
        {
            statusGoldText.text = $"보유 골드 : {BattleStatsManager.GetCurrentGold()} G";
        }
    }
}