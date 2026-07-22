using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [SerializeField] private Image hpBarFill;

    // 체력바 업데이트 함수
    public void UpdateHPBar(float currentHP, float maxHP)
    {
        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = currentHP / maxHP;
        }
    }
}