using ReversalTower.Combat;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 플레이어가 한 행동을 전투 데이터 수집기에 알린다.
    ///
    /// 원래는 PlayerMovement 안에 CombatEvents.RaiseXxx() 다섯 줄을 넣어야 하지만,
    /// 정훈의 코드를 고치지 않기 위해 공개된 프로퍼티를 매 프레임 지켜보는 방식으로 대신한다.
    ///
    ///   CurrentHealth 가 바뀜        → 체력 변화
    ///   CurrentHealth 가 0 이하      → 사망
    ///   CurrentPotionCount 가 줄어듦 → 포션 사용
    ///   쿨다운이 갑자기 올라감        → 그 스킬을 씀
    ///
    /// 나중에 PlayerMovement에 직접 한 줄씩 넣게 되면 이 컴포넌트는 지우면 된다.
    /// (그쪽이 더 정확하다. 이 방식은 같은 프레임에 두 번 쓰는 경우를 놓칠 수 있다.)
    ///
    /// 붙이는 곳: 플레이어 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/플레이어 전투 이벤트 릴레이")]
    public class PlayerCombatEventRelay : MonoBehaviour
    {
        [Tooltip("비워 두면 부모·자신에서 찾고, 그래도 없으면 씬에서 찾는다")]
        public PlayerMovement player;

        [Header("무엇을 알릴지")]
        public bool reportHealth = true;
        public bool reportPotion = true;
        public bool reportSkills = true;

        [Header("디버그")]
        public bool logToConsole = false;

        int lastHealth = int.MinValue;
        int lastPotion = int.MinValue;
        float lastQ, lastW, lastE;
        bool deathReported;

        void Start()
        {
            if (player == null) player = GetComponentInParent<PlayerMovement>();
            if (player == null) player = GetComponentInChildren<PlayerMovement>();
            if (player == null) player = FindAnyObjectByType<PlayerMovement>();
            if (player == null)
            {
                Debug.LogWarning("[전투 이벤트 릴레이] PlayerMovement를 찾지 못했습니다. Player 칸에 직접 넣어 주세요.", this);
                enabled = false;
                return;
            }

            lastHealth = player.CurrentHealth;
            lastPotion = player.CurrentPotionCount;
            lastQ = player.QCooldownRemaining;
            lastW = player.WCooldownRemaining;
            lastE = player.ECooldownRemaining;
        }

        void Update()
        {
            if (player == null) return;

            if (reportHealth)
            {
                int hp = player.CurrentHealth;
                if (hp != lastHealth)
                {
                    lastHealth = hp;
                    CombatEvents.RaisePlayerHpChanged(hp);
                    if (logToConsole) Debug.Log($"[릴레이] 체력 {hp}", this);
                }

                if (hp <= 0 && !deathReported)
                {
                    deathReported = true;
                    CombatEvents.RaisePlayerDied();
                    if (logToConsole) Debug.Log("[릴레이] 사망", this);
                }
                else if (hp > 0)
                {
                    deathReported = false; // 다시 살아나면(부활·재시작) 초기화
                }
            }

            if (reportPotion)
            {
                int potion = player.CurrentPotionCount;
                if (potion < lastPotion)
                {
                    CombatEvents.RaisePotionUsed();
                    if (logToConsole) Debug.Log("[릴레이] 포션 사용", this);
                }
                lastPotion = potion;
            }

            if (reportSkills)
            {
                CheckSkill("Q", player.QCooldownRemaining, ref lastQ);
                CheckSkill("W", player.WCooldownRemaining, ref lastW);
                CheckSkill("E", player.ECooldownRemaining, ref lastE);
            }
        }

        /// <summary>쿨다운이 줄어들다가 갑자기 늘어나면 그 스킬을 쓴 것이다.</summary>
        void CheckSkill(string skillId, float remaining, ref float last)
        {
            if (remaining > last + 0.01f)
            {
                CombatEvents.RaiseSkillUsed(skillId);
                if (logToConsole) Debug.Log($"[릴레이] 스킬 {skillId} 사용", this);
            }
            last = remaining;
        }
    }
}
