namespace ReversalTower.Boss
{
    /// <summary>
    /// 패턴을 어떤 방식으로 실행할지. 이 값으로 실행기(PatternExecutor)를 고른다.
    /// 새 공격 종류(소환, 순간이동 등)는 여기에 추가하고 실행기를 하나 더 만든다.
    /// </summary>
    public enum PatternKind
    {
        Melee,      // 제자리에서 판정 박스를 시간에 맞춰 켠다 (일반 공격, 베기, 회전 베기)
        Dash,       // 이동하면서 판정 (방패 돌진, 돌진 기믹, 퀵스텝)
        Projectile, // 날아가는 물체를 띄운다. 이동(Dash 설정)도 같이 쓸 수 있다
                    // (아라쿠스 방패 투척 후 대쉬, 방패 던지고 내려찍기)
    }

    /// <summary>
    /// 이 판정을 누가 들고 있는지. 판정 박스가 어디를 따라다니는지가 달라진다.
    /// </summary>
    public enum HitCarrier
    {
        Boss,       // 보스 몸 기준 (기본). HitboxShape의 anchor·forwardStart를 그대로 쓴다
        Projectile, // 날아가는 투사체 기준. 박스가 투사체 한가운데를 따라다닌다
                    // (PatternKind.Projectile인 패턴에서만 의미가 있다)
    }

    /// <summary>넉백 4단계 (전투방식설계서 4.2)</summary>
    public enum KnockbackLevel
    {
        None,   // 0m   — 다단히트 중간 타
        Light,  // 0.5m — 일반 공격, 콤보 마무리
        Medium, // 1m   — 대부분의 스킬 패턴
        Heavy,  // 2m + 다운 — 페이즈 전환기, 대형 범위기
    }

    /// <summary>판정 박스를 어디 기준으로 잴지 (전투방식설계서 1.2, 아디쿠스 기획서 1.3)</summary>
    public enum HitboxAnchor
    {
        FrontEdge,  // 보스 정면 끝에서 앞으로 (대부분의 공격)
        BossCenter, // 보스 몸 가운데를 중심으로 좌우 대칭 (회전 베기, 주변 파동)
    }

    /// <summary>보스 공격 한 타의 결과</summary>
    public enum HitResult
    {
        Hit,     // 맞음
        Parried, // 패링 성공
        Ignored, // 무적 등으로 무시됨
        Missed,  // 판정 안에 아무도 없음 (= 회피)
    }

    /// <summary>돌진이 막힌 이유</summary>
    public enum DashBlock
    {
        None,
        Wall,          // 맵 벽 — 그냥 멈춤
        GroggyObject,  // 기믹 오브젝트 — 그로기 (아라쿠스 돌진 기믹)
    }

    public static class KnockbackTable
    {
        public static float Distance(KnockbackLevel level)
        {
            switch (level)
            {
                case KnockbackLevel.Light: return 0.5f;
                case KnockbackLevel.Medium: return 1f;
                case KnockbackLevel.Heavy: return 2f;
                default: return 0f;
            }
        }

        public static bool KnocksDown(KnockbackLevel level) => level == KnockbackLevel.Heavy;
    }
}
