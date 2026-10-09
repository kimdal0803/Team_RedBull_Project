using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CombatHUD : MonoBehaviour
{
    [Header("플레이어")]
    [SerializeField] private PlayerMovement player;

    [Header("Q 스킬")]
    [SerializeField] private TMP_Text qCooldownText;
    [SerializeField] private Image qCooldownFill;

    [Header("W 스킬")]
    [SerializeField] private TMP_Text wCooldownText;
    [SerializeField] private Image wCooldownFill;

    [Header("E 스킬")]
    [SerializeField] private TMP_Text eCooldownText;
    [SerializeField] private Image eCooldownFill;

    [Header("포션")]
    [SerializeField] private TMP_Text potionCountText;

    private void Start()
    {
        // Inspector에서 안 넣어도 자동 탐색
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        UpdateSkill(
          qCooldownText,
          qCooldownFill,
          player.QCooldownRemaining,
          player.QCooldown,
          "Q"
      );

        UpdateSkill(
            wCooldownText,
            wCooldownFill,
            player.WCooldownRemaining,
            player.WCooldown,
            "W"
        );

        UpdateSkill(
            eCooldownText,
            eCooldownFill,
            player.ECooldownRemaining,
            player.ECooldown,
            "E"
        );

        UpdatePotion();
    }

    private void UpdateSkill(
    TMP_Text cooldownText,
    Image cooldownFill,
    float remaining,
    float maxCooldown,
    string skillKey)
    {
        bool isCoolingDown = remaining > 0f;

        // -------------------------
        // 텍스트
        // -------------------------

        if (cooldownText != null)
        {
            // 항상 표시
            cooldownText.gameObject.SetActive(true);

            if (isCoolingDown)
            {
                // 쿨타임 중에는 남은 시간
                cooldownText.text =
                    remaining.ToString("0.0");
            }
            else
            {
                // 사용 가능하면 Q / W / E 표시
                cooldownText.text = skillKey;
            }
        }

        // -------------------------
        // 원형 쿨타임 마스크
        // -------------------------

        if (cooldownFill != null)
        {
            if (isCoolingDown)
            {
                cooldownFill.gameObject.SetActive(true);

                cooldownFill.fillAmount =
                    maxCooldown > 0f
                        ? remaining / maxCooldown
                        : 0f;
            }
            else
            {
                // 사용 가능하면 어두운 마스크 제거
                cooldownFill.fillAmount = 0f;
                cooldownFill.gameObject.SetActive(false);
            }
        }
    }

    private void UpdatePotion()
    {
        if (potionCountText == null)
            return;

        potionCountText.text =
            $"× {player.CurrentPotionCount}";
    }
}