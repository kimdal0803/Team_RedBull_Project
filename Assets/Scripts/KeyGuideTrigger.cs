using UnityEngine;

public class KeyGuideTrigger : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject keyGuidePanel;

    void Start()
    {
        if (keyGuidePanel != null)
        {
            keyGuidePanel.SetActive(false);
        }
        else
        {
            Debug.LogError("<color=red>[KeyGuide] KeyGuidePanel 슬롯이 비어있습니다!</color>");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("<color=green>[KeyGuide] 무언가 닿음 -> 이름: " + other.name + " / 태그: " + other.tag + "</color>");

        if (other.CompareTag("Player"))
        {
            if (keyGuidePanel != null)
            {
                keyGuidePanel.SetActive(true);
                Debug.Log("<color=cyan>[KeyGuide] Player 감지 성공! 패널 활성화</color>");
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (keyGuidePanel != null)
            {
                keyGuidePanel.SetActive(false);
            }
        }
    }
}