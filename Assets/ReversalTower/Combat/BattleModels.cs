using System;
using System.Collections.Generic;

namespace ReversalTower.Combat
{
    /// <summary>
    /// 전투를 시작할 때 수집기에 넘기는 조건. 전투 중에는 바뀌지 않는다.
    /// 만들 때는 SaveSystem.CreateBattleContext(bossId, ddaEnabled)를 쓰면
    /// 도전 번호·전투력 지수·참가자 ID가 자동으로 채워진다.
    /// </summary>
    [Serializable]
    public class BattleContext
    {
        public string bossId = "Arakus";
        /// <summary>이 보스에 몇 번째 도전인지 (1부터)</summary>
        public int attemptNo = 1;
        /// <summary>실험 조건: DDA 적용 여부</summary>
        public bool ddaEnabled;
        /// <summary>전투력 지수 [임시 결정, 전투방식설계서 6.1]</summary>
        public float combatPowerIndex;
        /// <summary>참가자 ID = 서버 플레이어 ID. 오프라인이면 "offline"</summary>
        public string participantId = "offline";
    }

    /// <summary>전투가 끝난 이유</summary>
    public enum BattleEndReason
    {
        BossDead,   // 보스 처치
        PlayerDead, // 플레이어 사망
        Quit,       // 일시정지 → 로비 등 중간 포기
    }

    /// <summary>전투 요약 (전투 1회 = 1행). 최종보고서 4.4</summary>
    [Serializable]
    public class BattleSummary
    {
        public string battleId;
        public string participantId;
        public string bossId;
        public int attemptNo;
        public bool ddaEnabled;
        public float combatPowerIndex;

        public bool cleared;
        public string endReason;
        public float durationSec;
        public int phaseReached = 1;

        public float damageDealt;   // 준 피해 (Damage_dealt)
        public float damageTaken;   // 받은 피해 (DamageTaken)
        public int hitCount;        // 피격 수 (HitCount)
        public int ignoredCount;    // 무적 등으로 무시된 타 — 피격과 따로 센다
        public int dodgeCount;      // 회피 = 판정 안에 아무도 없던 타 + 대시 무적으로 흘린 타
        public int parrySuccess;    // 패링 성공 수 (P_count)
        public int parryChances;    // 패링 가능한 타가 날아온 수

        public int potionUsed;
        public int ultimateUsed;
        public int skillUsed;

        public long startedAtUnixMs;
        public string buildVersion;

        // DDA (DDA 설계서 10장). DDA 매니저가 없는 씬이면 policy = "none"
        public string experimentGroup = "";   // dda / control
        public string ddaPolicy = "none";     // off / rule / random / linucb
        public string priorVersion = "";      // 모델을 시작한 초기값 파일 버전
        public bool protectionApplied;        // 이번 전투에 3연패 보호가 켜졌는지

        public float ParryRate => parryChances > 0 ? (float)parrySuccess / parryChances : 0f;
    }

    /// <summary>패턴별 집계 (전투 × 패턴 = 1행). DDA의 입력이 된다</summary>
    [Serializable]
    public class PatternStats
    {
        public string battleId;
        public string bossId;
        public string patternId;

        public int uses;            // 패턴 사용 횟수
        public int strikes;         // 날아온 타 수 (결과와 상관없이)
        public int hitCount;
        public int parrySuccess;
        public int parryChances;
        public int ignoredCount;
        public int dodgeCount;
        public float damageTaken;
        public int interrupted;     // 그로기·사망으로 끊긴 횟수

        // 적용된 보정 배율 (DDA가 바꾼 값). 평균을 내기 위해 합과 개수를 같이 둔다.
        public float telegraphScaleSum;
        public int telegraphScaleCount;
        public float damageScaleSum;
        public int damageScaleCount;

        public float TelegraphScale => telegraphScaleCount > 0 ? telegraphScaleSum / telegraphScaleCount : 1f;
        public float DamageScale => damageScaleCount > 0 ? damageScaleSum / damageScaleCount : 1f;

        // DDA (DDA 설계서 10장). 전투가 끝날 때 수집기가 채운다.
        public int ddaStep;                    // 이번 전투에 걸린 단계 (−2~+2)
        public string ddaReason = "";          // excluded / off / first / noData / learned / fallback / protected / forced
        public double ctx1 = -1, ctx2 = -1, ctx3 = -1, ctx4 = -1, ctx5 = -1; // 단계를 고를 때 본 문맥 x1~x5 (x0은 항상 1)
        public double ddaPredicted = -1;       // 걸린 단계의 예측 대응 성공률 ŷ (LinUCB만)
        public double ddaUncertainty = -1;     // 걸린 단계의 불확실성 u (LinUCB만)
        public double responseRate = -1;       // 실제 대응 성공률 y = (패링 + 회피) ÷ 유효 타. 유효 타 3 미만이면 −1
        public double ddaReward = -1;          // 구간 보상 r (0.6~0.8이면 1)

        /// <summary>유효 타 = 날아온 타 − 무적 등으로 무시된 타 (대응 성공률의 분모)</summary>
        public int EffectiveStrikes => Math.Max(0, strikes - ignoredCount);

        /// <summary>responseRate · ddaReward를 계산한다 (DDA 설계서 7장).</summary>
        public void ComputeResponse()
        {
            int n = EffectiveStrikes;
            if (n >= 3)
            {
                responseRate = (double)(parrySuccess + dodgeCount) / n;
                double dist = responseRate < 0.6 ? 0.6 - responseRate : (responseRate > 0.8 ? responseRate - 0.8 : 0.0);
                ddaReward = Math.Max(0.0, 1.0 - dist / 0.3);
            }
            else
            {
                responseRate = -1;
                ddaReward = -1;
            }
        }
    }

    /// <summary>DDA가 패턴 하나에 무엇을 걸었는지 (DDA 매니저 → 수집기)</summary>
    public class DdaPatternLog
    {
        public int step;
        public string reason = "";
        /// <summary>문맥 x1~x5. 제외 패턴은 null</summary>
        public double[] ctx;
        public double predicted = -1;
        public double uncertainty = -1;
    }

    /// <summary>DDA가 이번 전투에 무엇을 걸었는지 (DDA 매니저 → 수집기). 수집기는 전투가 끝날 때 기록에 옮겨 적는다.</summary>
    public class DdaBattleLog
    {
        public string group = "";
        public string policy = "none";
        public string priorVersion = "";
        public bool protectionApplied;
        public readonly Dictionary<string, DdaPatternLog> patterns = new Dictionary<string, DdaPatternLog>();
    }

    /// <summary>이벤트 로그 (이벤트 1개 = 1행). 로컬 CSV로만 남긴다 (서버로는 요약·집계만 보낸다)</summary>
    [Serializable]
    public class CombatLogEntry
    {
        public float time;
        public string battleId;
        public string evt;          // PatternStart, HitResult, Damaged, SkillUse, PotionUse, UltimateUse, Groggy, PhaseChange, BattleEnd
        public string patternId = "";
        public int hitIndex = -1;
        public string result = "";  // Hit, Parried, Ignored, Missed
        public float value;
        public string skill = "";
        public int phase;
        public float playerHp;
        public float bossHp;
    }

    /// <summary>전투 한 판이 끝나면 수집기가 내보내는 묶음</summary>
    public class BattleReport
    {
        public BattleSummary Summary;
        public List<PatternStats> Patterns = new List<PatternStats>();
        public List<CombatLogEntry> Events = new List<CombatLogEntry>();
    }
}
