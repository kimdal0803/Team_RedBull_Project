using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BossClearManager : MonoBehaviour
{
    public static BossClearManager Instance { get; private set; }

    [Header("UI 연결 (Canvas)")]
    [SerializeField] private GameObject clearPanel;
    [SerializeField] private CanvasGroup clearCanvasGroup;
    [SerializeField] private Text clearTitleText;

    [Header("정산 텍스트 (Legacy Text)")]
    [SerializeField] private Text damageStatsText;
    [SerializeField] private Text parryStatsText;
    [SerializeField] private Text bonusStatsText;
    [SerializeField] private Text totalGoldText;

    [Header("버튼")]
    [SerializeField] private GameObject returnToLobbyButton;

    [Header("연출 시간 설정")]
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private float statsInterval = 0.3f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (clearPanel != null) clearPanel.SetActive(false);
        if (clearCanvasGroup != null)
        {
            clearCanvasGroup.alpha = 0f;
            clearCanvasGroup.blocksRaycasts = false;
        }
        if (returnToLobbyButton != null) returnToLobbyButton.SetActive(false);
    }

    public void TriggerBossClear(int bossMaxHp)
    {
        Debug.Log($"<color=cyan>[BossClearManager] 보스 처치! 보스 최대 체력: {bossMaxHp}</color>");

        if (BattleStatsManager.Instance != null)
        {
            BattleStatsManager.Instance.SetBossKilled(bossMaxHp);
        }

        StartCoroutine(ClearSequenceRoutine(bossMaxHp));
    }

    private IEnumerator ClearSequenceRoutine(int bossBonus)
    {
        yield return new WaitForSeconds(0.3f);

        if (clearPanel != null) clearPanel.SetActive(true);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (clearCanvasGroup != null)
            {
                clearCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            }
            yield return null;
        }

        if (clearCanvasGroup != null)
        {
            clearCanvasGroup.alpha = 1f;
            clearCanvasGroup.blocksRaycasts = true;
        }

        int damage = BattleStatsManager.Instance != null ? BattleStatsManager.Instance.totalDamageDealt : 0;
        int parry = BattleStatsManager.Instance != null ? BattleStatsManager.Instance.totalParryCount : 0;
        int earnedGold = BattleStatsManager.Instance != null ? BattleStatsManager.Instance.SettleAndSaveGold() : 0;

        if (damageStatsText != null)
            damageStatsText.text = $"총 입힌 피해 : {damage}";
        yield return new WaitForSeconds(statsInterval);

        if (parryStatsText != null)
            parryStatsText.text = $"패링 성공 : {parry}회";
        yield return new WaitForSeconds(statsInterval);

        if (bonusStatsText != null)
            bonusStatsText.text = $"보스 격파 보너스 : +{bossBonus}G";
        yield return new WaitForSeconds(statsInterval);

        if (totalGoldText != null)
            totalGoldText.text = $"최종 획득 : +{earnedGold}G";
        yield return new WaitForSeconds(statsInterval);

        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.SetActive(true);
        }
    }

    public void OnClickReturnToLobby()
    {
        Time.timeScale = 1f;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene("LobbyScene");
        }
        else
        {
            SceneManager.LoadScene("LobbyScene");
        }
    }
}