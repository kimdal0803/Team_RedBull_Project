using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    [Header("UI 패널 연결")]
    [Tooltip("ESC를 눌렀을 때 켜고 끌 PausePanel 오브젝트")]
    public GameObject pauseMenuPanel;

    [Header("씬 이름 설정")]
    public string lobbySceneName = "LobbyScene";
    public string titleSceneName = "TitleScene";

    private bool isPaused = false;

    void Awake()
    {

        // 모니터 주사율에 맞게 프레임을 매끄럽게 동기화 (60Hz, 144Hz 등)
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60; // 60프레임 이상 부드럽게 제한
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
        Time.timeScale = 1f;
        isPaused = false;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseMenuPanel != null)
            {
                if (isPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }
    }

    public void PauseGame()
    {
        Debug.Log("[PauseMenu] 게임 일시정지");
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        Debug.Log("[PauseMenu] 게임 계속하기 (재개)");
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    public void GoToLobby()
    {
        Debug.Log("[PauseMenu] 로비로 이동");
        Time.timeScale = 1f;
        isPaused = false;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(lobbySceneName);
        }
        else
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }

    public void GoToTitle()
    {
        Debug.Log("[PauseMenu] 타이틀 화면으로 이동");
        Time.timeScale = 1f;
        isPaused = false;

        if (SceneFader.Instance != null)
        {
            SceneFader.Instance.FadeToScene(titleSceneName);
        }
        else
        {
            SceneManager.LoadScene(titleSceneName);
        }
    }

    public void QuitGame()
    {
        Debug.Log("[PauseMenu] 게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// ⭐ 타이틀 씬 전용: 모든 저장 데이터(골드, 강화 스탯 등)를 완전 초기화
    /// </summary>
    public void ResetAllGameData()
    {
        // 1. PlayerPrefs에 저장된 모든 데이터(골드 등) 삭제
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        // 2. static 메모리에 남아있는 플레이어 강화 스탯 초기화 (-1: 기본값 복구)
        PlayerMovement.savedAttack = -1;
        PlayerMovement.savedMaxHp = -1;
        PlayerMovement.savedSpeed = -1f;
        PlayerMovement.savedJump = -1f;

        Debug.Log("<color=cyan><b>[초기화 완료]</b> 모든 저장 데이터(골드: 0G, 강화 스탯 리셋)가 초기 상태로 복구되었습니다.</color>");
    }
}