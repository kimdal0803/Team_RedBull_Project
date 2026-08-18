using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    [SerializeField] private string targetSceneName = "GameScene";

    [Header("상호작용 키")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("상호작용 안내 UI (선택사항)")]
    [SerializeField] private GameObject interactGuideUI; // "F키를 눌러 입장" 같은 안내 텍스트/아이콘

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
        // 플레이어가 범위 안에 있고 상호작용 키를 누르면 씬 전환
        if (_isPlayerNearby && Input.GetKeyDown(interactKey))
        {
            SceneManager.LoadScene(targetSceneName);
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