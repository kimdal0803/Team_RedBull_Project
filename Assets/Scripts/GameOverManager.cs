using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance;

    [Header("UI 요소 연결")]
    public GameObject gameOverPanel;         // GameOverPanel 오브젝트 자체
    public CanvasGroup gameOverCanvasGroup;
    public Image fadeBlackImage;
    public TextMeshProUGUI youDiedText;
    public CanvasGroup buttonGroup;

    [Header("사망 정산 텍스트 (Legacy Text)")]
    public Text damageStatsText;     // "총 입힌 피해 : XX"
    public Text parryStatsText;      // "패링 성공 : X회"
    public Text totalGoldText;       // "최종 획득 : +XXG"

    [Header("연출 시간 설정 (초)")]
    public float blackFadeDuration = 1.5f;
    public float textFadeDuration = 2.0f;
    public float statsInterval = 0.35f;       // 정산 텍스트 출력 간격
    public float buttonDelay = 0.3f;
    public float buttonFadeDuration = 1.0f;

    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 시작 시 패널을 완전히 꺼서 다른 UI(ESC 등)의 마우스 입력을 방해하지 않음
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        StartCoroutine(GameOverSequence());
    }

    IEnumerator GameOverSequence()
    {
        // 사망 시 패널 활성화
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (gameOverCanvasGroup != null)
        {
            gameOverCanvasGroup.alpha = 1f;
            gameOverCanvasGroup.blocksRaycasts = true;
        }

        // 초기 투명화 세팅
        if (fadeBlackImage != null)
        {
            Color c = fadeBlackImage.color;
            c.a = 0f;
            fadeBlackImage.color = c;
        }

        if (youDiedText != null)
        {
            Color c = youDiedText.color;
            c.a = 0f;
            youDiedText.color = c;
        }

        if (buttonGroup != null)
        {
            buttonGroup.alpha = 0f;
            buttonGroup.interactable = false;
            buttonGroup.blocksRaycasts = false;
        }

        // 1. 검은 화면 페이드
        float timer = 0f;
        while (timer < blackFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(timer / blackFadeDuration);

            if (fadeBlackImage != null)
            {
                Color c = fadeBlackImage.color;
                c.a = alpha;
                fadeBlackImage.color = c;
            }
            yield return null;
        }

        // 2. YOU DIED 텍스트 페이드
        timer = 0f;
        while (timer < textFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(timer / textFadeDuration);

            if (youDiedText != null)
            {
                Color c = youDiedText.color;
                c.a = alpha;
                youDiedText.color = c;
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.2f);

        // 3. 사망 정산 집계 및 PlayerPrefs 영구 저장
        int damage = BattleStatsManager.Instance != null ? BattleStatsManager.Instance.totalDamageDealt : 0;
        int parry = BattleStatsManager.Instance != null ? BattleStatsManager.Instance.totalParryCount : 0;
        int earnedGold = BattleStatsManager.Instance != null ? BattleStatsManager.Instance.SettleAndSaveGold() : 0;

        // 4. 정산 텍스트 순차 출력
        if (damageStatsText != null)
            damageStatsText.text = $"총 입힌 피해 : {damage}";
        yield return new WaitForSecondsRealtime(statsInterval);

        if (parryStatsText != null)
            parryStatsText.text = $"패링 성공 : {parry}회";
        yield return new WaitForSecondsRealtime(statsInterval);

        if (totalGoldText != null)
            totalGoldText.text = $"최종 획득 : +{earnedGold}G";
        yield return new WaitForSecondsRealtime(statsInterval);

        yield return new WaitForSecondsRealtime(buttonDelay);

        // 5. 버튼 페이드
        timer = 0f;
        while (timer < buttonFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            if (buttonGroup != null)
            {
                buttonGroup.alpha = Mathf.Clamp01(timer / buttonFadeDuration);
            }
            yield return null;
        }

        if (buttonGroup != null)
        {
            buttonGroup.interactable = true;
            buttonGroup.blocksRaycasts = true;
        }
    }

    public void OnClickGoToLobby()
    {
        Time.timeScale = 1f;
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.GoToLobby();
        }
    }

    public void OnClickGoToTitle()
    {
        Time.timeScale = 1f;
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.GoToTitle();
        }
    }

    public void OnClickQuitGame()
    {
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.QuitGame();
        }
    }
}