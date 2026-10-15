using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>판정 한 번 (다단히트면 타마다 하나씩 만들어진다)</summary>
    public struct HitEvent
    {
        public int Index;       // 이 패턴 안에서 몇 번째 타인지 (0부터)
        public float Time;      // 공격 시작 기준 시간(초, 속도 배율 적용 후)
        public float EndTime;   // 판정이 꺼지는 시간 (activeTime이 0이면 Time과 같음)
        public float Damage;    // 실제 피해량 = 보스 공격력 × 계수 × 보정
        public HitData Source;  // 원본 설정 (박스, 넉백, 패링 여부)
    }

    /// <summary>
    /// 패턴 한 번 실행에 쓰는 값 묶음.
    /// 시작할 때 데이터 × 보정값 × 속도 배율을 한 번 계산해 두고, 끝날 때까지 바꾸지 않는다.
    /// (중간에 DDA 값이 바뀌어도 진행 중인 패턴은 흔들리지 않는다.)
    /// </summary>
    public class PatternRun
    {
        public readonly BossPatternData Data;
        public readonly int RunId;            // 전투 중 몇 번째 패턴인지 (로그용)
        public readonly float SpeedMultiplier;
        public readonly float Telegraph;      // 선딜(초)
        public readonly float AttackDuration; // 공격 단계 길이(초)
        public readonly float Recovery;       // 후딜(초)
        public readonly float Cooldown;       // 쿨타임(초)
        public readonly int ParryableCount;
        public readonly List<HitEvent> Hits = new List<HitEvent>();

        public float Elapsed;     // 패턴 시작(선딜 시작)부터 흐른 시간
        public int ParriedCount;
        public bool Interrupted;  // 그로기·사망으로 끊겼는지
        internal int nextSignalIndex;

        public string PatternId => Data.patternId;
        public bool AllParried => ParryableCount > 0 && ParriedCount >= ParryableCount;
        public float TotalLength => Telegraph + AttackDuration + Recovery;

        public PatternRun(BossPatternData data, PatternTuning tuning, float attackPower, float speedMultiplier, int runId)
        {
            Data = data;
            RunId = runId;
            if (tuning == null) tuning = new PatternTuning();
            SpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);

            Telegraph = data.telegraphTime * tuning.telegraphScale / SpeedMultiplier;
            Recovery = data.recoveryTime / SpeedMultiplier;
            Cooldown = data.cooldown * tuning.cooldownScale;

            float lastEnd = 0f;
            int parryable = 0;
            if (data.hits != null)
            {
                foreach (var h in data.hits)
                {
                    if (h == null) continue;
                    int count = Mathf.Max(1, h.repeatCount + tuning.repeatCountDelta);
                    float interval = h.repeatInterval * tuning.repeatIntervalScale;
                    for (int i = 0; i < count; i++)
                    {
                        float t = (h.time + interval * i) / SpeedMultiplier;
                        float end = t + h.activeTime / SpeedMultiplier;
                        Hits.Add(new HitEvent
                        {
                            Time = t,
                            EndTime = end,
                            Damage = attackPower * h.damageCoefficient * tuning.damageScale,
                            Source = h,
                        });
                        lastEnd = Mathf.Max(lastEnd, end);
                        if (h.parryable) parryable++;
                    }
                }
            }
            ParryableCount = parryable;

            Hits.Sort((a, b) => a.Time.CompareTo(b.Time));
            for (int i = 0; i < Hits.Count; i++)
            {
                var e = Hits[i];
                e.Index = i;
                Hits[i] = e;
            }

            float minAttack = data.attackDuration / SpeedMultiplier;
            if (data.kind == PatternKind.Dash)
                minAttack = Mathf.Max(minAttack, (data.dash.startTime + data.dash.duration) / SpeedMultiplier);
            AttackDuration = Mathf.Max(lastEnd, minAttack);
        }
    }
}
