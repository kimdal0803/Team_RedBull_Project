using UnityEngine;

public enum BuffTier
{
    Common,     // 하급 (하얀색)
    Rare,       // 중급 (초록색)
    Epic,       // 상급 (파란색)
    Legendary,  // 전설 (노란색)
    Chaos       // 혼돈 (보라색)
}

public enum StatType
{
    Attack,     // 공격력
    Health,     // 체력
    Speed,      // 이동 속도
    JumpPower   // 점프력
}

[System.Serializable]
public class BuffOption
{
    public StatType statType;
    public BuffTier tier;
    public string buffTitle;
    public string description;

    // 실제 증감 수치
    public float deltaValue;
}