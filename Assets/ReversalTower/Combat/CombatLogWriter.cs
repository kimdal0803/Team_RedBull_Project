using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ReversalTower.Combat
{
    /// <summary>
    /// 전투 기록을 PC에 CSV로 남긴다 (서버 전송과 별개로 항상 남는 백업이자, 테스트 때 바로 열어보는 용도).
    /// 위치: Application.persistentDataPath/logs/
    ///   battles.csv  — 전투 요약 (전투 1회 = 1행)
    ///   patterns.csv — 패턴별 집계 (전투 × 패턴 = 1행)
    ///   events.csv   — 이벤트 로그 (이벤트 1개 = 1행)
    /// 파일이 없으면 첫 줄에 머리글을 쓰고, 있으면 아래에 이어 쓴다. 엑셀로 바로 열린다 (UTF-8 BOM).
    /// </summary>
    public static class CombatLogWriter
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "logs");

        static readonly string[] BattleHeader =
        {
            "battleId", "participantId", "bossId", "attemptNo", "ddaEnabled", "combatPower", "cleared", "endReason",
            "durationSec", "phaseReached", "damageDealt", "damageTaken", "hitCount", "ignoredCount", "dodgeCount",
            "parrySuccess", "parryChances", "potionUsed", "ultimateUsed", "skillUsed", "startedAtUtc", "buildVersion",
            "experimentGroup", "ddaPolicy", "priorVersion", "protectionApplied",
        };

        static readonly string[] PatternHeader =
        {
            "battleId", "bossId", "patternId", "uses", "strikes", "hitCount", "parrySuccess", "parryChances",
            "ignoredCount", "dodgeCount", "damageTaken", "interrupted", "telegraphScale", "damageScale",
            "ddaPolicy", "ddaStep", "ddaReason", "ctx1", "ctx2", "ctx3", "ctx4", "ctx5",
            "ddaPredicted", "ddaUncertainty", "responseRate", "ddaReward",
        };

        static readonly string[] EventHeader =
        {
            "time", "battleId", "event", "patternId", "hitIndex", "result", "value", "skill", "phase", "playerHp", "bossHp",
        };

        public static void Write(BattleReport report)
        {
            if (report?.Summary == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                var s = report.Summary;

                Append("battles.csv", BattleHeader, new List<object[]>
                {
                    new object[]
                    {
                        s.battleId, s.participantId, s.bossId, s.attemptNo, s.ddaEnabled, s.combatPowerIndex, s.cleared, s.endReason,
                        s.durationSec, s.phaseReached, s.damageDealt, s.damageTaken, s.hitCount, s.ignoredCount, s.dodgeCount,
                        s.parrySuccess, s.parryChances, s.potionUsed, s.ultimateUsed, s.skillUsed,
                        DateTimeOffset.FromUnixTimeMilliseconds(s.startedAtUnixMs).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        s.buildVersion,
                        s.experimentGroup, s.ddaPolicy, s.priorVersion, s.protectionApplied,
                    },
                });

                var patternRows = new List<object[]>();
                foreach (var p in report.Patterns)
                {
                    patternRows.Add(new object[]
                    {
                        p.battleId, p.bossId, p.patternId, p.uses, p.strikes, p.hitCount, p.parrySuccess, p.parryChances,
                        p.ignoredCount, p.dodgeCount, p.damageTaken, p.interrupted, p.TelegraphScale, p.DamageScale,
                        s.ddaPolicy, p.ddaStep, p.ddaReason, Precise(p.ctx1), Precise(p.ctx2), Precise(p.ctx3), Precise(p.ctx4), Precise(p.ctx5),
                        Precise(p.ddaPredicted), Precise(p.ddaUncertainty), Precise(p.responseRate), Precise(p.ddaReward),
                    });
                }
                Append("patterns.csv", PatternHeader, patternRows);

                var eventRows = new List<object[]>();
                foreach (var e in report.Events)
                {
                    eventRows.Add(new object[]
                    {
                        e.time, e.battleId, e.evt, e.patternId, e.hitIndex, e.result, e.value, e.skill, e.phase, e.playerHp, e.bossHp,
                    });
                }
                Append("events.csv", EventHeader, eventRows);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CombatLog] CSV를 쓰지 못했습니다: {e.Message}");
            }
        }

        static void Append(string fileName, string[] header, List<object[]> rows)
        {
            if (rows.Count == 0) return;
            string path = PrepareFile(Path.Combine(Folder, fileName), string.Join(",", header));
            var sb = new StringBuilder();
            bool isNew = !File.Exists(path);
            if (isNew) sb.AppendLine(string.Join(",", header));
            foreach (var row in rows) sb.AppendLine(ToCsvLine(row));
            File.AppendAllText(path, sb.ToString(), isNew ? new UTF8Encoding(true) : new UTF8Encoding(false));
        }

        /// <summary>
        /// 열이 바뀐 뒤(예: DDA 열 추가) 예전 파일에 이어 쓰면 칸이 어긋난다.
        /// 머리글이 다르면 예전 파일을 이름을 바꿔 남겨 두고 새 파일을 시작한다.
        /// 옮기지 못하면(엑셀로 열려 있는 등) 날짜를 붙인 새 파일에 쓴다. 돌려주는 값이 실제로 쓸 경로다.
        /// </summary>
        static string PrepareFile(string path, string header)
        {
            if (!File.Exists(path)) return path;
            string first;
            try
            {
                using (var reader = new StreamReader(path, Encoding.UTF8, true))
                    first = reader.ReadLine() ?? "";
            }
            catch (Exception)
            {
                return path; // 읽지도 못하면 이어 쓰기도 실패한다 — Write의 catch가 경고를 남긴다
            }
            first = first.TrimStart('\uFEFF');
            if (first == header) return path;

            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string stamp = DateTime.Now.ToString("yyMMdd_HHmmss");
            try
            {
                string old = Path.Combine(dir, $"{name}_old_{stamp}.csv");
                File.Move(path, old);
                Debug.Log($"[CombatLog] 열이 바뀌어 예전 기록을 {Path.GetFileName(old)}로 옮기고 새 {Path.GetFileName(path)}를 시작합니다.");
                return path;
            }
            catch (Exception e)
            {
                string fallback = Path.Combine(dir, $"{name}_{stamp}.csv");
                Debug.LogWarning($"[CombatLog] 예전 {Path.GetFileName(path)}를 옮기지 못해 ({e.Message}) {Path.GetFileName(fallback)}에 씁니다.");
                return fallback;
            }
        }

        /// <summary>DDA 문맥 · 예측은 초기값을 다시 만들 때 쓰므로 소수점 6자리까지 남긴다.</summary>
        static string Precise(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        static string ToCsvLine(object[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) parts[i] = Escape(Format(values[i]));
            return string.Join(",", parts);
        }

        static string Format(object value)
        {
            switch (value)
            {
                case null: return "";
                case float f: return f.ToString("0.###", CultureInfo.InvariantCulture);
                case double d: return d.ToString("0.###", CultureInfo.InvariantCulture);
                case bool b: return b ? "1" : "0";
                case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
                default: return value.ToString();
            }
        }

        static string Escape(string text)
        {
            if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
