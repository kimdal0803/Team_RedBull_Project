using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("플레이어 체력바")]
    [SerializeField] private Image hpBarFill;

    [Header("스테이터스 패널 (I키 토글)")]
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private Text statusHpText;
    [SerializeField] private Text statusAttackText;
    [SerializeField] private Text statusSpeedText;
    [SerializeField] private Text statusJumpText;
    [SerializeField] private Text statusGoldText;

    [Header("플레이어 참조 (비어있으면 자동 탐색)")]
    [SerializeField] private PlayerMovement playerMovement;

    private void Start()
    {
        if (statusPanel != null)
        {
            statusPanel.SetActive(false);
        }

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
        if (hpBarFill != null)
        {
            float hpRatio = maxHP > 0f
                ? Mathf.Clamp01(currentHP / maxHP)
                : 0f;

            hpBarFill.fillAmount = hpRatio;
        }

        if (statusPanel != null && statusPanel.activeSelf)
        {
            UpdateStatusTexts();
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
                statusHpText.text =
                    $"체력 : {playerMovement.CurrentHealth} / {playerMovement.MaxHealth}";

            if (statusAttackText != null)
                statusAttackText.text =
                    $"공격력 : {playerMovement.BaseAttackDamage}";

            if (statusSpeedText != null)
                statusSpeedText.text =
                    $"이동 속도 : {playerMovement.MoveSpeed:F1}";

            if (statusJumpText != null)
                statusJumpText.text =
                    $"점프력 : {playerMovement.JumpForce:F1}";
        }

        if (statusGoldText != null)
        {
            statusGoldText.text =
                $"보유 골드 : {BattleStatsManager.GetCurrentGold()} G";
        }
    }
}