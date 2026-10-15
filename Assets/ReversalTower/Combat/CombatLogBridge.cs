using UnityEngine;

namespace ReversalTower.Combat
{
    /// <summary>
    /// 연결부. 전투가 끝나면 기록을 어디에 남길지 여기서만 정한다.
    /// 수집기는 저장 방법을 모르고, 저장 쪽은 전투를 모른다.
    ///
    /// 지금은 PC에 CSV로만 남긴다 (Application.persistentDataPath/logs/).
    /// 나중에 서버(UGS)를 붙이면 여기에 한 줄만 더하면 되고, 수집기는 건드리지 않는다.
    ///
    /// 게임이 시작될 때 유니티가 자동으로 연결하므로 씬에 따로 놓을 필요가 없다.
    /// </summary>
    public static class CombatLogBridge
    {
        /// <summary>전투가 끝난 뒤 추가로 할 일 (골드 정산, 서버 전송 등)을 붙이는 자리.</summary>
        public static event System.Action<BattleReport> AfterBattle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Wire()
        {
            CombatDataCollector.BattleFinished -= OnBattleFinished;
            CombatDataCollector.BattleFinished += OnBattleFinished;
        }

        static void OnBattleFinished(BattleReport report)
        {
            CombatLogWriter.Write(report);

            if (Debug.isDebugBuild || Application.isEditor)
                Debug.Log($"[CombatLog] CSV 저장 위치: {CombatLogWriter.Folder}");

            try { AfterBattle?.Invoke(report); }
            catch (System.Exception e) { Debug.LogException(e); }
        }
    }
}
