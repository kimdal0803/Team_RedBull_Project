using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower.Progress
{
    /// <summary>
    /// progress.json 읽기·쓰기.
    ///
    ///   위치: Application.persistentDataPath/progress.json
    ///         (윈도우: C:\Users\이름\AppData\LocalLow\회사명\제품명\)
    ///
    /// - 쓰기는 임시 파일(.tmp)에 다 쓴 뒤 한 번에 바꿔 끼운다. 직전 파일은 .bak으로 남는다.
    ///   그래서 쓰다가 꺼져도 파일이 반쯤 깨진 채로 남지 않는다.
    /// - 읽다가 깨진 파일을 만나면 .bak에서 읽고, 그것도 안 되면 새로 시작한다 (경고 로그).
    /// - 값이 이상하면(없는 등급, 숫자가 아님 등) 읽을 때 정리한다.
    ///
    /// 바꿀 때는 Current를 직접 고치지 말고, Clone으로 복사본을 고쳐서 Commit 한다.
    /// 저장에 성공했을 때만 Current가 바뀐다 → 저장이 실패하면 게임 안의 값도 그대로다.
    /// </summary>
    public static class ProgressStore
    {
        const string FileName = "progress.json";

        static ProgressData current;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        static string BackupPath => SavePath + ".bak";
        static string TempPath => SavePath + ".tmp";

        /// <summary>마지막 읽기·쓰기 문제 (없으면 null)</summary>
        public static string LastError { get; private set; }

        /// <summary>지금 진행 기록 (처음 부를 때 파일에서 읽음). 직접 고치지 말 것 — Clone → Commit.</summary>
        public static ProgressData Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
            LastError = null;
        }

        // ------------------------------------------------------------------ 읽기

        static ProgressData Load()
        {
            foreach (string path in new[] { SavePath, BackupPath })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(path, Encoding.UTF8));
                    if (data == null) throw new InvalidDataException("빈 파일");
                    Normalize(data);
                    if (path == BackupPath)
                        Debug.LogWarning($"[진행 저장] 본 파일을 읽지 못해 백업에서 불러왔습니다: {path}");
                    return data;
                }
                catch (Exception e)
                {
                    LastError = $"{Path.GetFileName(path)} 읽기 실패: {e.Message}";
                    Debug.LogWarning($"[진행 저장] {LastError}");
                }
            }
            return new ProgressData();
        }

        /// <summary>읽어 온 값 정리 — 없는 등급·스탯, 숫자가 아닌 값, 어긋난 카드 묶음을 바로잡는다.</summary>
        public static void Normalize(ProgressData d)
        {
            d.version = ProgressData.CurrentVersion;
            d.battles = Mathf.Max(0, d.battles);

            if (d.picks == null) d.picks = new System.Collections.Generic.List<CardPick>();
            d.picks.RemoveAll(p => p == null
                || !Enum.IsDefined(typeof(StatType), p.stat)
                || !Enum.IsDefined(typeof(BuffTier), p.tier)
                || float.IsNaN(p.value) || float.IsInfinity(p.value));
            foreach (var p in d.picks)
            {
                p.value = Mathf.Clamp(p.value, -1000f, 1000f);
                p.cost = Mathf.Max(0, p.cost);
            }

            if (d.offer == null) d.offer = new System.Collections.Generic.List<CardOfferSlot>();
            bool offerBroken = d.offer.Count != 0 && d.offer.Count != 3;
            var seen = new System.Collections.Generic.HashSet<StatType>();
            foreach (var o in d.offer)
            {
                if (o == null
                    || !Enum.IsDefined(typeof(StatType), o.stat)
                    || !Enum.IsDefined(typeof(BuffTier), o.tier)
                    || !seen.Add(o.stat))
                {
                    offerBroken = true;
                    break;
                }
            }
            if (offerBroken || d.cardChosen) d.offer.Clear();
        }

        // ------------------------------------------------------------------ 쓰기

        /// <summary>깊은 복사 (고칠 때는 이걸 고쳐서 Commit)</summary>
        public static ProgressData Clone(ProgressData d)
        {
            var copy = JsonUtility.FromJson<ProgressData>(JsonUtility.ToJson(d));
            Normalize(copy);
            return copy;
        }

        /// <summary>파일에 저장하고, 성공하면 Current를 바꾼다. 실패하면 false (Current 그대로).</summary>
        public static bool Commit(ProgressData next)
        {
            if (next == null) return false;
            Normalize(next);
            if (!WriteFile(next)) return false;
            current = next;
            return true;
        }

        static bool WriteFile(ProgressData data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(TempPath, JsonUtility.ToJson(data, true), new UTF8Encoding(false));

                if (!File.Exists(SavePath))
                {
                    File.Move(TempPath, SavePath);
                }
                else
                {
                    try
                    {
                        File.Replace(TempPath, SavePath, BackupPath);
                    }
                    catch (Exception e) when (e is PlatformNotSupportedException || e is IOException)
                    {
                        // 바꿔 끼우기를 못 하는 환경 — 복사로 대신 (백업 먼저)
                        File.Copy(SavePath, BackupPath, true);
                        File.Copy(TempPath, SavePath, true);
                        File.Delete(TempPath);
                    }
                }

                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = "저장 실패: " + e.Message;
                Debug.LogError($"[진행 저장] {LastError}");
                return false;
            }
        }

        // ------------------------------------------------------------------ 게임 쪽에서 부르는 것

        /// <summary>전투 정산 때 (BattleStatsManager.SettleAndSaveGold) — 로비 카드 선택을 다시 연다.</summary>
        public static void OnBattleSettled()
        {
            var next = Clone(Current);
            next.cardChosen = false;
            next.offer.Clear();          // 다음 로비 방문 때 새 3장
            next.battles++;
            Commit(next);
        }

        /// <summary>처음부터 다시 (타이틀의 데이터 초기화). 파일과 백업을 지운다.</summary>
        public static void ResetAll()
        {
            foreach (string path in new[] { SavePath, BackupPath, TempPath })
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[진행 저장] {Path.GetFileName(path)} 지우기 실패: {e.Message}");
                }
            }
            current = new ProgressData();
            LastError = null;
        }
    }
}
