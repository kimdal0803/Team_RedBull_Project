using UnityEngine;
using UnityEngine.UI;

public class BuffCardUI : MonoBehaviour
{
    [Header("UI 텍스트 (Legacy Text)")]
    [SerializeField] private Text tierText;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descText;

    [Header("UI 이미지 & 버튼")]
    [SerializeField] private Image cardBackgroundImage;
    [SerializeField] private Image cardFrameImage;
    [SerializeField] private Button selectButton;

    // 밝고 선명한 등급별 고유 색상 (폰트 및 테두리용)
    private static readonly Color CommonColor = new Color(0.95f, 0.95f, 0.95f, 1f); // 하급 (순백색)
    private static readonly Color RareColor = new Color(0.35f, 1.00f, 0.45f, 1f); // 중급 (형광 연두)
    private static readonly Color EpicColor = new Color(0.35f, 0.75f, 1.00f, 1f); // 상급 (밝은 하늘)
    private static readonly Color LegendColor = new Color(1.00f, 0.88f, 0.20f, 1f); // 전설 (황금 노랑)
    private static readonly Color ChaosColor = new Color(0.88f, 0.40f, 1.00f, 1f); // 혼돈 (밝은 보라)

    // 카드 배경색 (진한 톤)
    private static readonly Color CommonBg = new Color(0.15f, 0.15f, 0.15f, 0.95f);
    private static readonly Color RareBg = new Color(0.06f, 0.20f, 0.08f, 0.95f);
    private static readonly Color EpicBg = new Color(0.06f, 0.14f, 0.22f, 0.95f);
    private static readonly Color LegendBg = new Color(0.22f, 0.18f, 0.04f, 0.95f);
    private static readonly Color ChaosBg = new Color(0.16f, 0.05f, 0.20f, 0.95f);

    private BuffOption currentBuff;

    public void Setup(BuffOption buff)
    {
        currentBuff = buff;

        // 제목과 설명 텍스트는 항상 밝고 선명한 흰색으로 표시
        if (titleText != null)
        {
            titleText.text = buff.buffTitle;
            titleText.color = Color.white;
        }

        if (descText != null)
        {
            descText.text = buff.description;
            descText.color = new Color(0.9f, 0.9f, 0.9f, 1f); // 가독성 높은 밝은 아이보리
        }

        // 등급별 라벨 및 고유 색상 적용
        switch (buff.tier)
        {
            case BuffTier.Common:
                ApplyStyle("[ 하급 ]", CommonColor, CommonBg);
                break;
            case BuffTier.Rare:
                ApplyStyle("[ 중급 ]", RareColor, RareBg);
                break;
            case BuffTier.Epic:
                ApplyStyle("[ 상급 ]", EpicColor, EpicBg);
                break;
            case BuffTier.Legendary:
                ApplyStyle("[ 전설 ]", LegendColor, LegendBg);
                break;
            case BuffTier.Chaos:
                ApplyStyle("[ 혼돈 ]", ChaosColor, ChaosBg);
                break;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() =>
            {
                BuffGachaManager.Instance.SelectBuff(currentBuff);
            });
        }
    }

    private void ApplyStyle(string label, Color themeColor, Color bgColor)
    {
        // 등급 문구는 등급 고유 컬러로 출력
        if (tierText != null)
        {
            tierText.text = label;
            tierText.color = themeColor;
        }

        if (cardFrameImage != null) cardFrameImage.color = themeColor;
        if (cardBackgroundImage != null) cardBackgroundImage.color = bgColor;
    }
}