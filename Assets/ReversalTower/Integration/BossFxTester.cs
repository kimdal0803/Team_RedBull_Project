using System.Collections.Generic;
using System.Reflection;
using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 연출 확인용 치트키. 보스 HP를 다 깎지 않고도 페이즈 전환·그로기·사망 연출을 바로 본다.
    ///
    ///   1  보스 HP를 60% 바로 아래로   → "2 페이즈" 연출
    ///   2  보스 HP를 30% 바로 아래로   → "분노" 연출
    ///   3  강제 그로기 2초             → 노란 펄스 + 머리 위 표시
    ///   4  보스 즉사                   → 슬로우모션
    ///   5  다음 패턴 강제 실행         → 패턴 하나씩 눈으로 확인
    ///   0  도움말 켜기/끄기
    ///
    /// 발표·빌드 전에 반드시 꺼야 한다. 컴포넌트의 '치트키 켜기'를 끄거나 컴포넌트를 지우면 된다.
    ///
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 연출 테스터 (치트키)")]
    [RequireComponent(typeof(BossController))]
    public class BossFxTester : MonoBehaviour
    {
        [Header("켜기 / 끄기")]
        [Tooltip("발표·빌드 전에 반드시 끄기")]
        public bool enableCheats = true;
        [Tooltip("화면 왼쪽 아래에 키 목록을 띄운다")]
        public bool showHelp = true;

        [Header("치트키 (겹치면 여기서 바꾸면 된다)")]
        public KeyCode phase2Key = KeyCode.Alpha1;
        public KeyCode rageKey = KeyCode.Alpha2;
        public KeyCode groggyKey = KeyCode.Alpha3;
        public KeyCode killKey = KeyCode.Alpha4;
        public KeyCode nextPatternKey = KeyCode.Alpha5;
        public KeyCode toggleHelpKey = KeyCode.Alpha0;

        [Header("값")]
        [Tooltip("3번 키로 거는 그로기 길이(초)")]
        [Min(0.1f)] public float groggyDuration = 2f;
        [Tooltip("1번 키가 맞출 HP 비율. 60% 트리거를 넘기려면 0.6보다 조금 낮아야 한다")]
        [Range(0f, 1f)] public float phase2TargetRatio = 0.59f;
        [Tooltip("2번 키가 맞출 HP 비율. 30% 트리거를 넘기려면 0.3보다 조금 낮아야 한다")]
        [Range(0f, 1f)] public float rageTargetRatio = 0.29f;
        [Tooltip("5번 키를 누를 때 진행 중인 패턴을 끊는다. 끊으면 그로기 연출이 섞여서 기본은 꺼 둠")]
        public bool interruptOnForcePattern = false;

        BossController boss;
        Queue<BossPatternData> forcedQueue;   // BossController의 비공개 큐 (리플렉션)
        int patternCursor;

        string toast = "";
        float toastUntil;

        void Awake()
        {
            boss = GetComponent<BossController>();

            // 패턴 강제 실행용. 못 찾아도 나머지 기능은 그대로 동작한다.
            var f = typeof(BossController).GetField("forcedQueue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            forcedQueue = f != null ? f.GetValue(boss) as Queue<BossPatternData> : null;
        }

        void Update()
        {
            if (!enableCheats || boss == null) return;

            if (Input.GetKeyDown(toggleHelpKey)) showHelp = !showHelp;
            if (Input.GetKeyDown(phase2Key)) DropTo(phase2TargetRatio, "2 페이즈");
            if (Input.GetKeyDown(rageKey)) DropTo(rageTargetRatio, "분노");
            if (Input.GetKeyDown(groggyKey)) ForceGroggy();
            if (Input.GetKeyDown(killKey)) Kill();
            if (Input.GetKeyDown(nextPatternKey)) ForceNextPattern();
        }

        // ─────────────────────── 치트 동작 ───────────────────────

        /// <summary>보스 HP를 목표 비율 바로 아래까지 깎는다.</summary>
        void DropTo(float ratio, string label)
        {
            if (boss.IsDead) { Toast("보스가 이미 죽었어 — Play를 다시 눌러"); return; }

            if (boss.HpRatio <= ratio)
            {
                Toast($"이미 HP {boss.HpRatio:P0} — '{label}'는 지나갔어. Play 다시 눌러");
                return;
            }

            // 패턴에 '받는 피해 배율'이 있어서 한 번에 안 깎일 수 있다. 될 때까지 반복.
            int guard = 0;
            while (boss.HpRatio > ratio && !boss.IsDead && guard++ < 300)
            {
                float need = boss.Hp - boss.maxHp * ratio + 0.01f;
                if (boss.TakeDamage(need, "연출 치트") <= 0f) break;
            }

            if (boss.HpRatio > ratio)
                Toast("지금 보스가 피해를 안 받는 패턴 중이야 — 잠깐 뒤에 다시");
            else
                Toast($"HP {boss.HpRatio:P0} → '{label}' 연출이 곧 나와");
        }

        void ForceGroggy()
        {
            if (boss.IsDead) { Toast("보스가 이미 죽었어"); return; }
            boss.EnterGroggy(groggyDuration, "연출 치트");
            Toast($"그로기 {groggyDuration:0.#}초");
        }

        void Kill()
        {
            if (boss.IsDead) { Toast("보스가 이미 죽었어"); return; }

            int guard = 0;
            while (!boss.IsDead && guard++ < 50)
                if (boss.TakeDamage(boss.maxHp, "연출 치트") <= 0f) break;

            Toast(boss.IsDead ? "사망 연출" : "지금 무적 패턴 중 — 잠깐 뒤에 다시");
        }

        void ForceNextPattern()
        {
            if (boss.IsDead) { Toast("보스가 이미 죽었어"); return; }

            if (forcedQueue == null)
            {
                Toast("패턴 강제 실행을 못 써 (BossController 구조가 바뀐 듯)");
                return;
            }

            var list = boss.patterns;
            if (list == null || list.Count == 0) { Toast("보스에 패턴이 하나도 없어"); return; }

            BossPatternData pick = null;
            for (int i = 0; i < list.Count; i++)
            {
                var cand = list[(patternCursor + i) % list.Count];
                if (cand == null) continue;
                pick = cand;
                patternCursor = (patternCursor + i + 1) % list.Count;
                break;
            }
            if (pick == null) { Toast("쓸 수 있는 패턴이 없어"); return; }

            forcedQueue.Clear();
            forcedQueue.Enqueue(pick);

            string name = string.IsNullOrEmpty(pick.displayName) ? pick.patternId : pick.displayName;

            if (interruptOnForcePattern && boss.CurrentRun != null)
            {
                // 지금 패턴을 안전하게 끊는 길은 그로기뿐이다 (FSM 상태까지 같이 바뀌어야 해서).
                boss.EnterGroggy(0.1f, "연출 치트");
                Toast($"다음 패턴: {name}");
            }
            else
            {
                Toast(boss.CurrentRun != null
                    ? $"지금 패턴 끝나면 → {name}"
                    : $"다음 패턴: {name}");
            }
        }

        // ─────────────────────── 화면 표시 ───────────────────────

        void Toast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 2f;
            Debug.Log($"[연출 치트] {message}", this);
        }

        void OnGUI()
        {
            if (!enableCheats) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };

            if (showHelp)
            {
                style.normal.textColor = new Color(0.85f, 0.95f, 1f);
                string help =
                    $"[연출 치트]  HP {boss.HpRatio:P0}\n" +
                    $"{KeyName(phase2Key)}  HP 60% 아래로 (2 페이즈)\n" +
                    $"{KeyName(rageKey)}  HP 30% 아래로 (분노)\n" +
                    $"{KeyName(groggyKey)}  그로기 {groggyDuration:0.#}초\n" +
                    $"{KeyName(killKey)}  즉사 (슬로우모션)\n" +
                    $"{KeyName(nextPatternKey)}  다음 패턴 강제 실행\n" +
                    $"{KeyName(toggleHelpKey)}  이 목록 숨기기";

                var box = new Rect(10f, Screen.height - 150f, 280f, 140f);
                GUI.Box(box, GUIContent.none);
                GUI.Label(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, box.height - 12f), help, style);
            }

            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toast))
            {
                style.fontSize = 18;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = new Color(0.6f, 1f, 0.7f);
                GUI.Label(new Rect(0f, Screen.height - 190f, Screen.width, 28f), toast, style);
            }
        }

        static string KeyName(KeyCode k)
        {
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);       // Alpha1 → 1
            if (s.StartsWith("Keypad")) return "넘패드 " + s.Substring(6);
            return s;
        }
    }
}
