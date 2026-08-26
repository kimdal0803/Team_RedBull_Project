using UnityEngine;

public class BattleStatsManager : MonoBehaviour
{
    public static BattleStatsManager Instance { get; private set; }

    [Header("실시간 전투 통계")]
    public int totalDamageDealt = 0;   // 총 입힌 피해량
    public int totalParryCount = 0;    // 패링 횟수
    public bool isBossKilled = false;  // 처치 여부
    public int bossMaxHpBonus = 0;     // 보스 최대 체력 보너스

    private const string GOLD_KEY = "PlayerGold";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddDamage(int dmg)
    {
        totalDamageDealt += dmg;
    }

    public void AddParryCount()
    {
        totalParryCount++;
    }

    public void SetBossKilled(int bossMaxHp)
    {
        isBossKilled = true;
        bossMaxHpBonus = bossMaxHp;
    }

    public int CalculateEarnedGold()
    {
        int fromDmg = totalDamageDealt;              // 피해량 1:1
        int fromParry = totalParryCount * 2;         // 패링 x 2
        int fromBoss = isBossKilled ? bossMaxHpBonus : 0; // 보스 체력만큼

        return fromDmg + fromParry + fromBoss;
    }

    public int SettleAndSaveGold()
    {
        int earnedGold = CalculateEarnedGold();
        int currentTotalGold = PlayerPrefs.GetInt(GOLD_KEY, 0);
        int newTotalGold = currentTotalGold + earnedGold;

        PlayerPrefs.SetInt(GOLD_KEY, newTotalGold);
        PlayerPrefs.Save();

        Debug.Log($"<color=green>[정산 완료] 획득: +{earnedGold}G | 누적: {newTotalGold}G</color>");
        return earnedGold;
    }

    public static int GetCurrentGold()
    {
        return PlayerPrefs.GetInt(GOLD_KEY, 0);
    }

    public static bool ConsumeGold(int amount)
    {
        int cur = GetCurrentGold();
        if (cur >= amount)
        {
            PlayerPrefs.SetInt(GOLD_KEY, cur - amount);
            PlayerPrefs.Save();
            return true;
        }
        return false;
    }
    [Header("디버그/테스트용 골드 설정")]
    public int debugGoldAmount = 9999; // 원하는 골드 입력

    // 유니티 인스펙터에서 우클릭으로 실행할 수 있는 버튼 생성
    [ContextMenu("⭐ 인스펙터 값으로 골드 덮어쓰기")]
    public void SetGoldDebug()
    {
        PlayerPrefs.SetInt(GOLD_KEY, debugGoldAmount);
        PlayerPrefs.Save();
        Debug.Log($"<color=yellow>[테스트] 골드가 {debugGoldAmount}G 로 강제 설정되었습니다!</color>");
    }
}