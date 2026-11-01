using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ReversalTower.Boss.Phanes.EditorTools
{
    /// <summary>
    /// 파네스 패턴 인스펙터. 고른 스킬의 설정 칸만 보여 주고, 파네스에 쓰지 않는 칸(돌진·방패 투사체)은 숨긴다.
    /// </summary>
    [CustomEditor(typeof(PhanesPatternData))]
    public class PhanesPatternDataEditor : UnityEditor.Editor
    {
        static readonly Dictionary<PhanesSkill, string> BlockOf = new Dictionary<PhanesSkill, string>
        {
            { PhanesSkill.Teleport, null },
            { PhanesSkill.HomingOrb, "homing" },
            { PhanesSkill.FanVolley, "fan" },
            { PhanesSkill.RefractionOrb, "refraction" },
            { PhanesSkill.JudgmentPillar, "pillar" },
            { PhanesSkill.PunishmentLine, "line" },
            { PhanesSkill.HeavenlyEcho, "echo" },
            { PhanesSkill.Hellfire, "hellfire" },
            { PhanesSkill.SeveranceBarrier, "barrier" },
            { PhanesSkill.NebulaShards, "nebula" },
            { PhanesSkill.GravityDistortion, "gravity" },
            { PhanesSkill.PhantomClones, "clones" },
        };

        static readonly Dictionary<PhanesSkill, string> HitsHelp = new Dictionary<PhanesSkill, string>
        {
            { PhanesSkill.Teleport, "판정 없음. 선딜 = 흐려짐, 공격 단계(attackDuration) = 나타남." },
            { PhanesSkill.HomingOrb, "hits[0] (Projectile) 한 타 = 광탄 한 발. repeatCount·repeatInterval로 연속 발사." },
            { PhanesSkill.FanVolley, "hits[0] (Projectile) 한 타 = 부채꼴 한 번. 바늘 수는 fan.count." },
            { PhanesSkill.RefractionOrb, "hits[0] (Projectile, 패링 가능) = 대형 구체. 패링하면 보스에게 되돌아간다." },
            { PhanesSkill.JudgmentPillar, "hits[0] (World) = 빛 기둥 다단 히트. 선딜 = 경고 마법진 시간." },
            { PhanesSkill.PunishmentLine, "hits[0] (World) = 바닥 레이저, hits[1] (World, 선택) = 잔류 불꽃 틱." },
            { PhanesSkill.HeavenlyEcho, "hits[0] (World) = 가시 폭발. repeatCount = 표식 수, repeatInterval = 간격, time = 표식→폭발 시간(markLead)." },
            { PhanesSkill.Hellfire, "hits[0] (World) = 화상 틱. repeatCount × repeatInterval = 장판 지속 시간." },
            { PhanesSkill.SeveranceBarrier, "hits[0] (World) = 실패 시 맵 전체 폭발. time = 제한 시간." },
            { PhanesSkill.NebulaShards, "hits[0] (World) = 중심 구체 접촉, hits[1] (Projectile) = 탄막 (repeatCount = 탄막 횟수)." },
            { PhanesSkill.GravityDistortion, "hits[0] (World) = 종료 폭발. time = 흡인 시간." },
            { PhanesSkill.PhantomClones, "hits[0] (World) = 오답 분신 자폭 (가짜를 때렸을 때만)." },
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var data = (PhanesPatternData)target;

            if (data.kind != PatternKind.Scripted)
                EditorGUILayout.HelpBox("kind가 Scripted가 아니면 파네스 스킬이 실행되지 않습니다.", MessageType.Warning);

            var excluded = new List<string> { "dash", "projectile" };
            foreach (var kv in BlockOf)
                if (kv.Value != null && kv.Key != data.skill) excluded.Add(kv.Value);

            DrawPropertiesExcluding(serializedObject, excluded.ToArray());

            if (HitsHelp.TryGetValue(data.skill, out var help))
                EditorGUILayout.HelpBox("판정 읽는 법 — " + help, MessageType.Info);

            if (data.teleportBeforeChance > 0f && data.telegraphTime < 0.4f)
                EditorGUILayout.HelpBox("시전 전 순간이동을 켰다면 선딜(telegraphTime)이 0.4초 이상이어야 순간이동이 끝난 뒤 공격합니다.", MessageType.Warning);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
