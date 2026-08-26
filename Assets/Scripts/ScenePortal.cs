using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    [SerializeField] private string targetSceneName = "BossTestScene";

    [Header("상호작용 키 설정")]
    [Tooltip("기본값: 방향키 위쪽(UpArrow)")]
    [SerializeField] private KeyCode interactKey = KeyCode.UpArrow;
    [Tooltip("W키로도 입장 가능하게 할지 여부")]
    [SerializeField] private bool allowWKey = true;

    [Header("상호작용 안내 UI (선택사항)")]
    [SerializeField] private GameObject interactGuideUI; // "↑ 키를 눌러 입장" 안내 UI

    private bool _isPlayerNearby = false;

    void Start()
    {
        if (interactGuideUI != null)
        {
            interactGuideUI.SetActive(false);
        }
    }

    void Update()
    {
        if (!_isPlayerNearby) return;

        // 방향키 위(UpArrow) 또는 W 키 입력 시 부드러운 씬 전환
        if (Input.GetKeyDown(interactKey) || (allowWKey && Input.GetKeyDown(KeyCode.W)))
        {
            Time.timeScale = 1f;

            if (SceneFader.Instance != null)
            {
                SceneFader.Instance.FadeToScene(targetSceneName);
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _isPlayerNearby = true;
            if (interactGuideUI != null) interactGuideUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _isPlayerNearby = false;
            if (interactGuideUI != null) interactGuideUI.SetActive(false);
        }
    }
}