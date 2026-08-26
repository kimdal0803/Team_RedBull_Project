using UnityEngine;

public class AltarTrigger : MonoBehaviour
{
    [Header("UI 및 매니저 직접 연결")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private BuffGachaManager gachaManager;

    private bool _isPlayerNearby = false;

    private void Start()
    {
        if (interactPrompt != null) interactPrompt.SetActive(false);
    }

    private void Update()
    {
        // ⭐ 가챠/확인창이 이미 열려있다면 상호작용 및 안내 문구 비활성화
        if (gachaManager != null && gachaManager.IsOpen)
        {
            if (interactPrompt != null && interactPrompt.activeSelf)
            {
                interactPrompt.SetActive(false);
            }
            return;
        }

        // 창이 닫혀있고 근처에 있을 때 안내 문구 다시 켜기
        if (_isPlayerNearby && interactPrompt != null && !interactPrompt.activeSelf)
        {
            interactPrompt.SetActive(true);
        }

        // 제단 근처에서 키 입력 시 오픈
        if (_isPlayerNearby && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.E)))
        {
            if (gachaManager != null)
            {
                gachaManager.OpenConfirmDialog();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _isPlayerNearby = true;
            if (interactPrompt != null && (gachaManager == null || !gachaManager.IsOpen))
            {
                interactPrompt.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            _isPlayerNearby = false;
            if (interactPrompt != null)
            {
                interactPrompt.SetActive(false);
            }
        }
    }
}