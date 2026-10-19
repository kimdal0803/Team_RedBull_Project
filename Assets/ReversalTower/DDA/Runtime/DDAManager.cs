using ReversalTower.Boss;
using ReversalTower.Combat;
using UnityEngine;

namespace ReversalTower.DDA
{
    /// <summary>
    /// DDA 설정을 들고 있다가, 수집기가 전투 기록을 시작하면 이번 전투의 단계를 건다.
    ///
    /// 붙이는 곳: 보스 층 씬의 CombatDataCollector 오브젝트
    ///   (메뉴 '역전의 탑 ▸ DDA ▸ 지금 씬에 DDA 붙이기'가 대신 해 준다)
    ///
    /// 개발용 패널: F2 (InGameDebugPanel이 F1을 쓴다). 배포 빌드에서는 열리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [AddComponentMenu("역전의 탑/DDA 매니저")]
    public class DDAManager : MonoBehaviour
    {
        public enum GroupMode
        {
            [InspectorName("항상 DDA (개발 · 테스트)")] AlwaysDda,
            [InspectorName("항상 대조군 (DDA 끔)")] AlwaysControl,
            [InspectorName("PC마다 50:50 (본 실험)")] Auto5050,
        }

        [Header("연결 (비우면 자동으로 찾는다)")]
        public CombatDataCollector collector;
        [Tooltip("비우면 수집기의 보스를 쓴다")]
        public BossController boss;
        [Tooltip("배율 표 (DDA 설계서 5장). 비우면 설계서 기본값")]
        public DDAArmTable armTable;

        [Header("실험 조건 (DDA 설계서 9장)")]
        [Tooltip("본 실험에서는 'PC마다 50:50'. 한 번 정해지면 그 PC에서는 바뀌지 않는다 (PlayerPrefs rt.dda.group).")]
        public GroupMode groupMode = GroupMode.AlwaysDda;
        [Tooltip("LinUcb = 채택안 · Rule = 규칙 기반 · Random = 플레이테스트(초기값 재료) · Off = 끔")]
        public DdaPolicyKind policy = DdaPolicyKind.LinUcb;

        [Header("LinUCB (DDA 설계서 8.2)")]
        [Tooltip("초기값 파일 dda_prior_*.json. 비어 있으면 LinUCB 대신 규칙 기반으로 고른다 (이유 fallback).\n" +
                 "dda_prior_sim0는 가상 플레이테스트로 만든 시험용 — 실험 · 배포 전에 실제 플레이테스트 초기값으로 바꾼다.")]
        public TextAsset priorFile;
        [Tooltip("불확실성 가산 α. 시뮬레이션에서 0~0.2가 비슷했고 0.5부터 나빠졌다.")]
        [Range(0f, 1f)] public float alpha = 0.2f;
        [Tooltip("기록이 모자랄 때 x1 · x4에 넣는 실패율. 플레이테스트 평균으로 바꾼다.")]
        [Range(0f, 1f)] public float defaultFailRate = 0.4f;

        [Header("개발용")]
        public KeyCode panelKey = KeyCode.F2;
        public bool showPanel = false;
        public bool logToConsole = true;

        public DdaGroup Group { get; private set; }

        const string GroupPrefKey = "rt.dda.group";
        const int PanelWindowId = 0x0DDA;
        Rect panelRect = new Rect(20f, 60f, 620f, 430f);
        Vector2 scroll;

        void Awake()
        {
            if (collector == null) collector = GetComponent<CombatDataCollector>();
            if (collector == null) collector = FindAnyObjectByType<CombatDataCollector>();
            Group = ResolveGroup();

            if (collector != null)
                collector.ddaEnabled = Group == DdaGroup.Dda && policy != DdaPolicyKind.Off; // 수집기가 Start에서 읽는다
            else
                Debug.LogWarning("[DDA] CombatDataCollector를 찾지 못했습니다. DDA는 수집기가 전투를 시작할 때 걸립니다.", this);
        }

        void OnEnable() => CombatDataCollector.BattleBegan += OnBattleBegan;
        void OnDisable() => CombatDataCollector.BattleBegan -= OnBattleBegan;

        void OnBattleBegan(CombatDataCollector c)
        {
            if (c == null || (collector != null && c != collector)) return;
            DdaRuntime.Prepare(this, c, c.boss != null ? c.boss : boss);
        }

        public DdaSettings BuildSettings()
        {
            return new DdaSettings
            {
                Group = Group,
                Policy = policy,
                Alpha = System.Math.Round(alpha, 6),               // float 0.2f → double 0.2 (설계값과 같게)
                DefaultFailRate = System.Math.Round(defaultFailRate, 6),
                Rng = new System.Random(),
            };
        }

        DdaGroup ResolveGroup()
        {
            switch (groupMode)
            {
                case GroupMode.AlwaysControl:
                    return DdaGroup.Control;
                case GroupMode.Auto5050:
                    string g = PlayerPrefs.GetString(GroupPrefKey, "");
                    if (g != "dda" && g != "control")
                    {
                        g = UnityEngine.Random.value < 0.5f ? "dda" : "control";
                        PlayerPrefs.SetString(GroupPrefKey, g);
                        PlayerPrefs.Save();
                    }
                    return g == "dda" ? DdaGroup.Dda : DdaGroup.Control;
                default:
                    return DdaGroup.Dda;
            }
        }

        // ─────────────────────────── 개발용 패널 (F2) ───────────────────────────

        static bool PanelAllowed => Application.isEditor || Debug.isDebugBuild;

        void Update()
        {
            if (PanelAllowed && PanelKeyPressed()) showPanel = !showPanel;
        }

        bool PanelKeyPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(panelKey);
#elif ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.f2Key.wasPressedThisFrame;
#else
            return false;
#endif
        }

        void OnGUI()
        {
            if (!showPanel || !PanelAllowed) return;
            panelRect = GUILayout.Window(PanelWindowId, panelRect, DrawPanel, $"DDA ({panelKey})");
        }

        void DrawPanel(int id)
        {
            var s = DdaRuntime.Current;
            string groupText = Group == DdaGroup.Dda ? "dda" : "control";
            GUILayout.Label($"그룹 {groupText} · 정책 {DdaPolicyNames.Of(policy)} · α {alpha:0.00} · 초기값 파일 {(priorFile != null ? priorFile.name : "없음 (규칙 기반으로 대신)")}");

            if (s == null)
            {
                GUILayout.Label("아직 전투 기록이 시작되지 않았습니다.");
                GUI.DragWindow();
                return;
            }

            GUILayout.Label($"{s.BossId} · 끝낸 전투 {s.State.attempts}번 · 연속 패배 {s.State.loseStreak}번 · " +
                            (s.State.hasModel ? $"모델 {s.State.priorVersion} +{s.State.modelUpdates}회" : "모델 없음") +
                            (s.Decisions.ProtectionApplied ? " · 3연패 보호 중" : "") +
                            (s.Finished ? " · 전투 끝 (학습 완료)" : ""));

            GUILayout.BeginHorizontal();
            GUILayout.Label("패턴", GUILayout.Width(190));
            GUILayout.Label("단계", GUILayout.Width(36));
            GUILayout.Label("이유", GUILayout.Width(72));
            GUILayout.Label("예측", GUILayout.Width(42));
            GUILayout.Label("±", GUILayout.Width(42));
            GUILayout.Label("최근 성공", GUILayout.Width(62));
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(250));
            foreach (var d in s.Decisions.Decisions)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(s.DisplayName(d.PatternId), GUILayout.Width(190));
                GUILayout.Label(DdaRuntime.StepText(d.Step), GUILayout.Width(36));
                GUILayout.Label(d.Reason ?? "", GUILayout.Width(72));
                GUILayout.Label(d.Predicted >= 0 ? d.Predicted.ToString("0.00") : "-", GUILayout.Width(42));
                GUILayout.Label(d.Uncertainty >= 0 ? d.Uncertainty.ToString("0.00") : "-", GUILayout.Width(42));
                GUILayout.Label(RecentRateText(s.State, d), GUILayout.Width(62));

                bool canForce = !d.Excluded && !s.Finished;
                GUI.enabled = canForce && d.Step > DdaConstants.MinStep;
                if (GUILayout.Button("−", GUILayout.Width(26))) DdaRuntime.ForceStep(d.PatternId, d.Step - 1);
                GUI.enabled = canForce && d.Step < DdaConstants.MaxStep;
                if (GUILayout.Button("+", GUILayout.Width(26))) DdaRuntime.ForceStep(d.PatternId, d.Step + 1);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Label("−/+는 지금 전투에 바로 걸립니다 (개발용, 기록의 이유 forced). '최근 성공'은 최근 3전 이 패턴의 대응 성공률 (유효 타 3 미만이면 -).");
            if (GUILayout.Button($"{s.BossId} DDA 기록 지우기 (다음 전투가 첫 전투가 됨)"))
                DdaRuntime.ResetBoss(s.BossId);
            GUI.DragWindow();
        }

        static string RecentRateText(DdaState state, DdaDecision d)
        {
            if (d.Excluded) return "-";
            DdaPatternState ps = null;
            foreach (var p in state.patterns)
                if (p.id == d.PatternId) { ps = p; break; }
            if (ps == null) return "-";
            int st = 0, ss = 0;
            foreach (var r in ps.history)
            {
                st += r.eff;
                ss += r.succ;
            }
            return st >= DdaConstants.MinStrikes ? ((double)ss / st).ToString("0.00") : "-";
        }
    }
}
