using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// FSM 보스(BossController)를 정훈이 만든 게임 흐름에 끼워 넣는다.
    /// DummyBoss가 하던 일을 그대로 대신한다 — 기존 스크립트는 한 줄도 고치지 않는다.
    ///
    ///   전투 시작  → BossUI에 이름·최대 체력 표시
    ///   보스 피격  → BossUI 체력바 갱신 + BattleStatsManager에 피해 누적 (골드 정산용)
    ///   보스 사망  → BossClearManager.TriggerBossClear (클리어 연출·정산)
    ///
    /// 붙이는 곳: BossController가 붙어 있는 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 UI 브리지 (정훈 UI 연결)")]
    [RequireComponent(typeof(BossController))]
    public class BossUIBridge : MonoBehaviour
    {
        [Header("표시")]
        [Tooltip("BossUI에 뜰 이름. 비우면 BossController의 bossId를 쓴다")]
        public string displayName = "아라쿠스";

        [Header("기존 시스템 연결")]
        [Tooltip("보스가 받은 피해를 BattleStatsManager에 넘긴다 (골드 정산에 들어감)")]
        public bool reportDamageToBattleStats = true;
        [Tooltip("보스를 잡으면 BossClearManager의 클리어 연출을 띄운다")]
        public bool triggerBossClearOnDeath = true;

        [Tooltip("클리어 보상 계산에 넘길 보스 체력. 0이면 BossController.maxHp를 그대로 쓴다.\n" +
                 "FSM 보스는 기본 체력이 커서(2000) 골드가 과하게 나올 수 있으니 여기서 따로 정할 수 있다.")]
        [Min(0)] public int rewardHpOverride = 0;

        [Header("디버그")]
        public bool logToConsole = true;

        BossController boss;
        bool clearTriggered;

        void Awake()
        {
            boss = GetComponent<BossController>();
            if (string.IsNullOrEmpty(displayName)) displayName = boss.bossId;
        }

        void OnEnable()
        {
            boss.Damaged += OnBossDamaged;
            boss.Died += OnBossDied;
        }

        void OnDisable()
        {
            boss.Damaged -= OnBossDamaged;
            boss.Died -= OnBossDied;
        }

        void Start()
        {
            if (BossUI.Instance != null)
                BossUI.Instance.InitBossUI(displayName, Mathf.RoundToInt(boss.maxHp));
            else if (logToConsole)
                Debug.Log("[보스 UI 브리지] 씬에 BossUI가 없어 체력바 표시를 건너뜁니다.", this);
        }

        void OnBossDamaged(float dealt, string cause)
        {
            if (BossUI.Instance != null)
                BossUI.Instance.UpdateBossHP(Mathf.CeilToInt(boss.Hp), Mathf.RoundToInt(boss.maxHp));

            if (reportDamageToBattleStats && BattleStatsManager.Instance != null)
                BattleStatsManager.Instance.AddDamage(Mathf.RoundToInt(dealt));
        }

        void OnBossDied()
        {
            if (clearTriggered) return;
            clearTriggered = true;

            if (!triggerBossClearOnDeath) return;

            int rewardHp = rewardHpOverride > 0 ? rewardHpOverride : Mathf.RoundToInt(boss.maxHp);

            if (BossClearManager.Instance != null)
            {
                if (logToConsole) Debug.Log($"[보스 UI 브리지] 클리어 정산 (보상 기준 체력 {rewardHp})", this);
                BossClearManager.Instance.TriggerBossClear(rewardHp);
            }
            else if (logToConsole)
            {
                Debug.Log("[보스 UI 브리지] 씬에 BossClearManager가 없어 클리어 연출을 건너뜁니다.", this);
            }
        }
    }
}
