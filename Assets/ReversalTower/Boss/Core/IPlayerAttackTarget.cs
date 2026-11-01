namespace ReversalTower.Boss
{
    /// <summary>
    /// 플레이어 공격에 맞는 보스 쪽 오브젝트 (보스 본체 말고).
    /// 예: 파네스 '단절의 결계' 결계벽, '환영 분신' 분신.
    ///
    /// PlayerAttackTrigger가 맞은 콜라이더의 부모까지 거슬러 이 인터페이스를 찾아 부른다.
    /// 보스 본체(BossController)보다 먼저 확인하므로, 보스 자식에 붙이면 본체 대신 이쪽이 맞는다.
    /// </summary>
    public interface IPlayerAttackTarget
    {
        /// <param name="damage">플레이어 공격력 (PlayerMovement.GetDamage)</param>
        /// <param name="cause">로그용 원인 이름</param>
        void TakePlayerHit(int damage, string cause);
    }
}
