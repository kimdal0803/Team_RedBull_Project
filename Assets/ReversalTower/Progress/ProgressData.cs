using System;
using System.Collections.Generic;

namespace ReversalTower.Progress
{
    /// <summary>
    /// 게임을 꺼도 남는 진행 기록 (progress.json).
    /// 골드는 여기 없다 — 팀 방식대로 PlayerPrefs "PlayerGold"에 있다 (BattleStatsManager).
    /// </summary>
    [Serializable]
    public class ProgressData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>지금 로비에 놓인 강화 카드 3장 (등급까지 정해짐). 비어 있으면 다음에 열 때 새로 뽑는다.</summary>
        public List<CardOfferSlot> offer = new List<CardOfferSlot>();

        /// <summary>이번 로비 방문에서 이미 한 장 골랐는지. 전투 정산 때 false로 돌아간다.</summary>
        public bool cardChosen;

        /// <summary>지금까지 고른 카드 (순서대로). 플레이어는 씬마다 기본값 위에 이 목록을 다시 쌓는다.</summary>
        public List<CardPick> picks = new List<CardPick>();

        /// <summary>정산까지 간 전투 수</summary>
        public int battles;

        /// <summary>한 번이라도 처치한 보스 (BossController.bossId). 로비에서 다음 문을 여는 데 씀.</summary>
        public List<string> clearedBosses = new List<string>();

        /// <summary>최근 전투 기록 (새것이 앞, 최대 ProgressStore.HistoryLimit개)</summary>
        public List<BattleRecord> history = new List<BattleRecord>();
    }

    /// <summary>전투 한 번의 정산 기록 (BattleStatsManager.SettleAndSaveGold에서 만듦)</summary>
    [Serializable]
    public class BattleRecord
    {
        public string boss;      // BossController.bossId (없으면 씬 이름)
        public bool clear;       // 보스 처치
        public int damage;       // 입힌 피해
        public int parries;      // 패링 횟수
        public int gold;         // 얻은 골드
        public float seconds;    // 씬에 들어온 뒤 정산까지 걸린 시간
        public string date;      // "yyyy-MM-dd HH:mm"
    }

    /// <summary>로비에 놓인 카드 한 장. 수치는 저장하지 않는다 — 열 때마다 BuffGachaManager의 현재 설정에서 읽는다.</summary>
    [Serializable]
    public class CardOfferSlot
    {
        public StatType stat;
        public BuffTier tier;
    }

    /// <summary>고른 카드 한 장과 실제로 오른 수치 (혼돈 등급은 고른 순간 정해진 값).</summary>
    [Serializable]
    public class CardPick
    {
        public StatType stat;
        public BuffTier tier;
        public float value;
        public int cost;
    }
}
