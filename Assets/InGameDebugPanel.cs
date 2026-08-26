using UnityEngine;

public class InGameDebugPanel : MonoBehaviour
{
    public static InGameDebugPanel Instance { get; private set; }

    [Header("단축키 설정")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private KeyCode altToggleKey = KeyCode.BackQuote; // ~ 키

    private bool _showDebugMenu = false;
    private Rect _windowRect = new Rect(20, 20, 520, 750); // 창 크기 확대 (고정 위치)
    private Vector2 _scrollPos;

    private string _inputGold = "1000";
    private string _inputAtk = "";
    private string _inputHp = "";
    private string _inputSpd = "";
    private string _inputJump = "";
    private string _inputBossHp = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 이동 시 유지
        }
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(altToggleKey))
        {
            _showDebugMenu = !_showDebugMenu;
            if (_showDebugMenu) SyncValues(); // 창 열 때 현재 씬의 최신 정보 로드
        }
    }

    private void SyncValues()
    {
        PlayerMovement p = Object.FindAnyObjectByType<PlayerMovement>();
        if (p != null)
        {
            _inputAtk = p.BaseAttackDamage.ToString();
            _inputHp = p.MaxHealth.ToString();
            _inputSpd = p.MoveSpeed.ToString("F1");
            _inputJump = p.JumpForce.ToString("F1");
        }
        DummyBoss b = Object.FindAnyObjectByType<DummyBoss>();
        if (b != null) _inputBossHp = b.currentHp.ToString();
    }

    private void OnGUI()
    {
        if (!_showDebugMenu) return;

        GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.fontSize = 18; // 제목 폰트 확대

        // 드래그(GUI.DragWindow) 제거, 지정된 Rect 위치에 고정
        _windowRect = GUI.Window(9999, _windowRect, DrawDebugWindow, "⚡ [DEV] 인게임 디버그 패널", windowStyle);
    }

    private void DrawDebugWindow(int windowID)
    {
        // 내부 UI 폰트 일괄 확대
        GUI.skin.label.fontSize = 15;
        GUI.skin.button.fontSize = 15;
        GUI.skin.textField.fontSize = 15;
        GUI.color = Color.white;

        _scrollPos = GUILayout.BeginScrollView(_scrollPos);
        GUILayout.Space(5);

        // 1. 골드 변경
        GUILayout.Label($"<b>[ 골드 관리 ]</b> 현재: {BattleStatsManager.GetCurrentGold()} G");
        GUILayout.BeginHorizontal();
        _inputGold = GUILayout.TextField(_inputGold, GUILayout.Width(80));
        if (GUILayout.Button("+ 증가", GUILayout.Width(60)))
            if (int.TryParse(_inputGold, out int amt)) ModifyGold(amt);
        if (GUILayout.Button("- 감소", GUILayout.Width(60)))
            if (int.TryParse(_inputGold, out int amt)) ModifyGold(-amt);
        if (GUILayout.Button("설정", GUILayout.Width(60)))
            if (int.TryParse(_inputGold, out int amt)) ModifyGold(amt - BattleStatsManager.GetCurrentGold());
        GUILayout.EndHorizontal();
        GUILayout.Space(15);

        // 2. 플레이어 스탯 즉시 변경
        GUILayout.Label("<b>[ 플레이어 스탯 (현재 씬 반영) ]</b>");
        PlayerMovement p = Object.FindAnyObjectByType<PlayerMovement>();
        if (p != null)
        {
            DrawStatRow("공격력", ref _inputAtk, () => p.ApplySingleBuff(StatType.Attack, float.Parse(_inputAtk) - p.BaseAttackDamage));
            DrawStatRow("최대 체력", ref _inputHp, () => p.ApplySingleBuff(StatType.Health, float.Parse(_inputHp) - p.MaxHealth));
            DrawStatRow("이동 속도", ref _inputSpd, () => p.ApplySingleBuff(StatType.Speed, float.Parse(_inputSpd) - p.MoveSpeed));
            DrawStatRow("점프력", ref _inputJump, () => p.ApplySingleBuff(StatType.JumpPower, float.Parse(_inputJump) - p.JumpForce));
        }
        else GUILayout.Label("<color=grey>현재 씬에 플레이어가 없습니다.</color>");
        GUILayout.Space(15);

        // 3. 보스 체력 조작
        GUILayout.Label("<b>[ 보스 체력 제어 ]</b>");
        DummyBoss b = Object.FindAnyObjectByType<DummyBoss>();
        if (b != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"현재 HP: {b.currentHp} / {b.maxHp}", GUILayout.Width(150));
            _inputBossHp = GUILayout.TextField(_inputBossHp, GUILayout.Width(60));
            if (GUILayout.Button("HP 적용", GUILayout.Width(80)))
            {
                if (int.TryParse(_inputBossHp, out int hp))
                {
                    b.currentHp = hp;
                    if (BossUI.Instance != null) BossUI.Instance.UpdateBossHP(b.currentHp, b.maxHp);
                }
            }
            if (GUILayout.Button("즉사", GUILayout.Width(60))) b.TakeDamage(99999);
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label("<color=grey>현재 씬에 보스가 없습니다.</color>");
        GUILayout.Space(15);

        // 4. 가챠 매니저 확률 및 카드 수치 조작
        GUILayout.Label("<b>[ 가챠 뽑기 밸런스 실시간 설정 ]</b>");
        if (BuffGachaManager.Instance != null)
        {
            TierDropRates rates = BuffGachaManager.Instance.dropRates;

            GUILayout.Label("<color=yellow>1) 카드 등장 확률(%) - 증감치 5</color>");
            GUILayout.BeginHorizontal();
            rates.commonChance = DrawStepper("하급", rates.commonChance, 5f);
            rates.rareChance = DrawStepper("중급", rates.rareChance, 5f);
            rates.epicChance = DrawStepper("상급", rates.epicChance, 5f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            rates.legendaryChance = DrawStepper("전설", rates.legendaryChance, 1f);
            rates.chaosChance = DrawStepper("혼돈", rates.chaosChance, 1f);
            GUILayout.EndHorizontal();

            GUILayout.Space(15);
            GUILayout.Label("<color=yellow>2) 버프 카드 스탯 부여 수치 - 증감치 1</color>");
            DrawStatTierUI("⚔️ 공격력", BuffGachaManager.Instance.attackValues, 1f);
            DrawStatTierUI("❤️ 체력", BuffGachaManager.Instance.healthValues, 1f);
            DrawStatTierUI("💨 이동속도 (증감치 0.1)", BuffGachaManager.Instance.speedValues, 0.1f);
            DrawStatTierUI("🦘 점프력", BuffGachaManager.Instance.jumpValues, 1f);
        }
        else GUILayout.Label("<color=grey>현재 씬에 가챠 매니저가 없습니다 (로비 씬 전용).</color>");

        GUILayout.EndScrollView();
        GUILayout.Space(10);

        if (GUILayout.Button("❌ 디버그 패널 닫기 (F1 / ~)", GUILayout.Height(40)))
        {
            _showDebugMenu = false;
        }
    }

    private void ModifyGold(int amount)
    {
        int cur = BattleStatsManager.GetCurrentGold();
        PlayerPrefs.SetInt("PlayerGold", Mathf.Max(0, cur + amount));
        PlayerPrefs.Save();

        if (BuffGachaManager.Instance != null) BuffGachaManager.Instance.UpdateLobbyGoldUI();
        PlayerUI pUI = Object.FindAnyObjectByType<PlayerUI>();
        if (pUI != null) pUI.UpdateStatusTexts();
    }

    private void DrawStatRow(string label, ref string input, System.Action applyAction)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));
        input = GUILayout.TextField(input, GUILayout.Width(60));
        if (GUILayout.Button("적용", GUILayout.Width(80)))
        {
            try { applyAction?.Invoke(); ModifyGold(0); } catch { }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawStatTierUI(string title, StatTierValues vals, float step)
    {
        GUILayout.Label(title);
        GUILayout.BeginHorizontal();
        vals.common = DrawStepper("하급", vals.common, step);
        vals.rare = DrawStepper("중급", vals.rare, step);
        vals.epic = DrawStepper("상급", vals.epic, step);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        vals.legendary = DrawStepper("전설", vals.legendary, step);
        vals.chaosMin = DrawStepper("혼돈Min", vals.chaosMin, step);
        vals.chaosMax = DrawStepper("혼돈Max", vals.chaosMax, step);
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
    }

    // 타이핑 대신 좌우 화살표 버튼으로 숫자를 쉽게 조절하는 공통 UI 모듈
    private float DrawStepper(string label, float val, float step)
    {
        GUILayout.BeginHorizontal(GUILayout.Width(155));
        GUILayout.Label(label, GUILayout.Width(60));
        if (GUILayout.Button("<", GUILayout.Width(25))) val -= step;
        GUILayout.Label((Mathf.Round(val * 10f) / 10f).ToString(), GUILayout.Width(35));
        if (GUILayout.Button(">", GUILayout.Width(25))) val += step;
        GUILayout.EndHorizontal();
        return val;
    }
}