namespace ReversalTower.Boss
{
    /// <summary>보스 공격 한 타의 정보. 플레이어 쪽이 받아서 패링·무적·피해를 판단한다.</summary>
    public struct BossHitInfo
    {
        public BossController Source;
        public string PatternId;
        public int HitIndex;
        public float Damage;
        public KnockbackLevel Knockback;
        public int Direction;   // +1이면 오른쪽, -1이면 왼쪽으로 밀림
        public bool Parryable;

        /// <summary>
        /// 회피 불가 (기획서의 '회피불가' 표기).
        /// true면 받는 쪽은 구르기 무적·피격 무적으로 이 타를 흘려보내면 안 된다. 패링은 그대로 통한다.
        /// 예: 아라쿠스 일대 휩쓸기.
        /// </summary>
        public bool IgnoresDodge;

        /// <summary>날아온 투사체에 맞았는지 (보스 몸이 아니라 방패 등). 연출 분기에 쓴다.</summary>
        public bool FromProjectile;
    }

    /// <summary>
    /// 보스 공격을 받는 쪽이 구현한다.
    /// 플레이어 전투 보강 작업에서 플레이어 스크립트가 이 인터페이스를 구현하면 보스와 바로 연결된다.
    /// 패링 판정(0.15초), 피격 무적(0.35초) 같은 플레이어 규칙은 받는 쪽에서 처리한다.
    ///
    /// 구현할 때 지켜야 할 순서 (전투방식설계서 4.1):
    ///   1) 패링 창 안이고 info.Parryable이면 → Parried
    ///   2) info.IgnoresDodge가 true면 무적을 무시하고 → Hit
    ///   3) 구르기·피격 무적 중이면 → Ignored
    ///   4) 그 밖에는 → Hit
    /// </summary>
    public interface IBossAttackReceiver
    {
        /// <returns>Hit = 맞음, Parried = 패링 성공, Ignored = 무적 등으로 무시</returns>
        HitResult ReceiveBossHit(in BossHitInfo info);
    }
}
