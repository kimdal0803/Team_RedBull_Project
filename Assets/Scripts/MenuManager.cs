using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("UI 패널 연결 (해당 씬에 있는 것만 연결)")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject optionsPanel;

    private bool _isPaused = false;

    void Start()
    {
        // 씬 시작 시 무조건 게임 속도 정상화 및 패널 비활성화
        Time.timeScale = 1f;
        _isPaused = false;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    void Update()
    {
        // ESC 키로 일시정지 창 켜고 끄기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pauseMenuPanel != null)
            {
                if (_isPaused)
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

    // [게임 시작] 타이틀 -> 로비 씬 이동
    public void OnStartGameButtonClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LobbyScene");
    }

    // [계속하기] 일시정지 닫기
    public void ResumeGame()
    {
        _isPaused = false;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // [일시정지]
    public void PauseGame()
    {
        _isPaused = true;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    // [타이틀로 이동]
    public void OnTitleButtonClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("TitleScene");
    }

    // [옵션 창 토글]
    public void ToggleOptionsPanel(bool isOpen)
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(isOpen);
        }
    }

    // [게임 종료]
    public void OnQuitGameButtonClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}