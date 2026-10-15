using UnityEngine;

namespace ReversalTower.Combat
{
    /// <summary>
    /// 전투를 시작할 때 넘길 조건(BattleContext)을 만든다.
    ///
    /// 원래 설계에서는 ServerUGS의 SaveSystem이 도전 번호·전투력 지수·참가자 ID를 채웠다.
    /// 지금 프로젝트에는 서버 연동이 들어와 있지 않으므로, 같은 값을 PlayerPrefs로 대신 채운다.
    /// 나중에 서버를 붙이면 이 파일만 SaveSystem을 쓰도록 바꾸면 되고, 수집기는 건드릴 필요가 없다.
    /// </summary>
    public static class BattleContextFactory
    {
        const string AttemptKeyPrefix = "rt.attempt.";   // 보스별 도전 횟수
        const string ParticipantKey = "rt.participantId"; // 이 PC의 익명 참가자 ID

        /// <summary>전투력 지수를 바깥에서 넣고 싶을 때 (강화 시스템이 붙으면 여기에 연결).</summary>
        public static System.Func<float> CombatPowerProvider;

        public static BattleContext Create(string bossId, bool ddaEnabled)
        {
            return new BattleContext
            {
                bossId = bossId,
                attemptNo = GetAttemptNo(bossId),
                ddaEnabled = ddaEnabled,
                combatPowerIndex = CombatPowerProvider != null ? CombatPowerProvider() : 0f,
                participantId = GetParticipantId(),
            };
        }

        /// <summary>이 보스에 몇 번째 도전인지 (1부터). 전투를 시작할 때마다 1씩 오른다.</summary>
        public static int GetAttemptNo(string bossId)
        {
            string key = AttemptKeyPrefix + bossId;
            int next = PlayerPrefs.GetInt(key, 0) + 1;
            PlayerPrefs.SetInt(key, next);
            PlayerPrefs.Save();
            return next;
        }

        /// <summary>이 PC를 구분하는 익명 ID. 서버를 붙이면 로그인 ID로 바뀐다.</summary>
        public static string GetParticipantId()
        {
            string id = PlayerPrefs.GetString(ParticipantKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = "local-" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString(ParticipantKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        /// <summary>테스트용: 도전 횟수를 0으로 되돌린다.</summary>
        public static void ResetAttempts(string bossId)
        {
            PlayerPrefs.DeleteKey(AttemptKeyPrefix + bossId);
            PlayerPrefs.Save();
        }
    }
}
