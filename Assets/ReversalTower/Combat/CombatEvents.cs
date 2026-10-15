using System;
using UnityEngine;

namespace ReversalTower.Combat
{
    /// <summary>
    /// 플레이어 쪽 이벤트를 수집기에 알리는 창구.
    /// 보스 쪽 이벤트는 BossController가 이미 내보내므로, 플레이어 스크립트만 아래 한 줄씩 넣으면 된다.
    ///
    ///   스킬 쓸 때      CombatEvents.RaiseSkillUsed("Q");
    ///   포션 먹을 때    CombatEvents.RaisePotionUsed();
    ///   궁극기 쓸 때    CombatEvents.RaiseUltimateUsed("Arakus_Ultimate");
    ///   체력이 바뀔 때  CombatEvents.RaisePlayerHpChanged(hp);
    ///   죽었을 때       CombatEvents.RaisePlayerDied();
    ///
    /// 보스에게 피해를 줄 때는 BossController.TakeDamage(피해, "원인")만 부르면 된다 (수집기가 보스 쪽에서 센다).
    /// </summary>
    public static class CombatEvents
    {
        public static event Action<string> SkillUsed;
        public static event Action PotionUsed;
        public static event Action<string> UltimateUsed;
        public static event Action<float> PlayerHpChanged;
        public static event Action PlayerDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SkillUsed = null;
            PotionUsed = null;
            UltimateUsed = null;
            PlayerHpChanged = null;
            PlayerDied = null;
        }

        public static void RaiseSkillUsed(string skill) => SkillUsed?.Invoke(skill);
        public static void RaisePotionUsed() => PotionUsed?.Invoke();
        public static void RaiseUltimateUsed(string ultimateId) => UltimateUsed?.Invoke(ultimateId);
        public static void RaisePlayerHpChanged(float hp) => PlayerHpChanged?.Invoke(hp);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
    }
}
