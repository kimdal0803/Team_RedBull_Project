using System;
using UnityEngine;

namespace ReversalTower.Boss
{
    /// <summary>
    /// 실행 중에만 적용하는 패턴 보정값. DDA가 나중에 여기에 값을 넣는다.
    /// 지금은 전부 기본값(변화 없음)이다.
    ///
    /// 왜 BossPatternData를 직접 바꾸지 않나:
    ///   플레이 중에 ScriptableObject 값을 바꾸면 에디터에서는 에셋 파일에 그대로 저장되어 원래 값이 사라진다.
    ///   그래서 원본 데이터는 두고, 패턴을 시작할 때 이 보정값을 곱해서 PatternRun을 만든다.
    ///
    /// 사용 예: boss.SetTuning("Arakus_SwordSwinging", new PatternTuning { telegraphScale = 1.2f });
    /// </summary>
    [Serializable]
    public class PatternTuning
    {
        [Tooltip("선딜 배율. 1.2면 선딜 20% 증가 (쉬워짐)")]
        [Min(0.1f)] public float telegraphScale = 1f;
        [Tooltip("데미지 배율")]
        [Min(0f)] public float damageScale = 1f;
        [Tooltip("쿨타임 배율")]
        [Min(0f)] public float cooldownScale = 1f;
        [Tooltip("다단히트 타수 증감 (+1, -1 …). 최소 1타는 남는다.")]
        public int repeatCountDelta = 0;
        [Tooltip("다단히트 간격 배율")]
        [Min(0.1f)] public float repeatIntervalScale = 1f;

        // DDA 설계서가 정해지면 추가할 자리: 예고 시간 배율, 소환 수 증감 등

        public PatternTuning Clone() => (PatternTuning)MemberwiseClone();
    }
}
