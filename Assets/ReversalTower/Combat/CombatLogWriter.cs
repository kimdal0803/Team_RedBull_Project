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
        };

        static readonly string[] PatternHeader =
        {
            "battleId", "bossId", "patternId", "uses", "strikes", "hitCount", "parrySuccess", "parryChances",
            "ignoredCount", "dodgeCount", "damageTaken", "interrupted", "telegraphScale", "damageScale",
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
                    },
                });

                var patternRows = new List<object[]>();
                foreach (var p in report.Patterns)
                {
                    patternRows.Add(new object[]
                    {
                        p.battleId, p.bossId, p.patternId, p.uses, p.strikes, p.hitCount, p.parrySuccess, p.parryChances,
                        p.ignoredCount, p.dodgeCount, p.damageTaken, p.interrupted, p.TelegraphScale, p.DamageScale,
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
            string path = Path.Combine(Folder, fileName);
            var sb = new StringBuilder();
            bool isNew = !File.Exists(path);
            if (isNew) sb.AppendLine(string.Join(",", header));
            foreach (var row in rows) sb.AppendLine(ToCsvLine(row));
            File.AppendAllText(path, sb.ToString(), isNew ? new UTF8Encoding(true) : new UTF8Encoding(false));
        }

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
