using System;
using System.Collections.Generic;
using ReversalTower.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReversalTower.Lobby
{
    /// <summary>
    /// 메뉴형 로비 "맹세의 회랑" (GPT 시안 0.4의 로비 화면을 팀 데이터로 옮김).
    ///
    ///   왼쪽   도전할 보스 5개 — 앞 보스를 처치하면 다음 문이 열림 (progress.json의 clearedBosses)
    ///   가운데 고른 보스 설명 + 입장 버튼
    ///   오른쪽 탑의 기록 (정산한 전투·처치·고른 강화, 최근 전투)
    ///   아래   영구 강화 요약 + 강화 카드 열기 / 조작 요약
    ///   위     골드 · 기록 · 설정 · 도움말
    ///
    /// 강화 카드 규칙은 같은 오브젝트의 BuffGachaManager (등급·확률·수치·비용).
    /// 화면은 IMGUI로 1600×900 기준 좌표에 그리고 화면 크기에 맞춰 늘린다 — 씬에 UI 오브젝트가 필요 없다.
    ///
    /// 키: ↑↓ 보스 선택 · C 강화 카드 · ENTER 입장 · H 도움말 · ESC 타이틀 (창이 열려 있으면 닫기)
    ///     카드 창에서 1/2/3 으로 고르기
    /// </summary>
    public sealed class LobbyMenu : MonoBehaviour
    {
        [Serializable]
        public class BossEntry
        {
            [Tooltip("BossController.bossId와 같아야 처치 기록이 이어짐")]
            public string id;
            public string displayName;
            public string title;
            [TextArea(2, 4)] public string lesson;
            [Tooltip("입장할 씬 이름 (Build Settings에 있어야 함). 비우면 '준비 중'")]
            public string sceneName;
        }

        [Header("보스 (위에서부터 01~05, 앞 보스를 처치하면 다음이 열림)")]
        [SerializeField] private BossEntry[] bosses = DefaultBosses();

        [Header("씬")]
        [SerializeField] private string titleSceneName = "TitleScene";

        [Header("연결 (비우면 같은 오브젝트에서 찾음)")]
        [SerializeField] private BuffGachaManager cards;

        [Header("배경 (비우면 어두운 단색)")]
        [SerializeField] private Texture2D backdrop;
        [Range(0f, 1f)] [SerializeField] private float backdropDim = 0.7f;

        const string VolumeKey = "MasterVolume";
        const float W = 1600f, H = 900f;

        // 색 (GPT 시안 팔레트)
        static readonly Color Ink = new Color(.035f, .05f, .075f);
        static readonly Color Stone = new Color(.22f, .28f, .33f);
        static readonly Color Teal = new Color(.33f, .89f, .83f);
        static readonly Color Gold = new Color(1f, .76f, .37f);
        static readonly Color Red = new Color(1f, .30f, .32f);
        static readonly Color White = new Color(.91f, .92f, .87f);
        static readonly Color Muted = new Color(.55f, .64f, .69f);
        static readonly Color Panel = new Color(.035f, .055f, .08f, .94f);
        static readonly Color CardBg = new Color(.075f, .10f, .14f);

        int bossIndex;
        bool showCards, showHelp, showHistory, showSettings, leaving;
        List<BuffOption> offer = new List<BuffOption>();
        string cardError;
        string notice;
        Color noticeColor = Gold;
        float noticeUntil;
        Vector2 historyScroll;

        Font font;
        GUIStyle label, button;
        Texture2D white;

        bool AnyModal => showCards || showHelp || showHistory || showSettings;

        // =========================================================
        // 시작
        // =========================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ApplySavedVolume()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        }

        void Awake()
        {
            if (cards == null) cards = GetComponent<BuffGachaManager>();
        }

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (cards == null) cards = BuffGachaManager.Instance;
            if (bosses == null || bosses.Length == 0) bosses = DefaultBosses();

            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Arial" }, 22);
            white = Texture2D.whiteTexture;

            // 아직 처치하지 않은 가장 앞의 열린 문을 고른 상태로 시작
            bossIndex = 0;
            for (int i = 0; i < bosses.Length; i++)
            {
                if (!Unlocked(i)) break;
                bossIndex = i;
                if (!ProgressStore.IsCleared(bosses[i].id)) break;
            }
        }

        // =========================================================
        // 키보드
        // =========================================================

        void Update()
        {
            if (leaving) return;

            if (showCards)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CloseCards();
                else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Pick(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Pick(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) Pick(2);
                return;
            }

            if (AnyModal)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    CloseModals();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveSelection(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) MoveSelection(1);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) EnterBoss();
            else if (Input.GetKeyDown(KeyCode.C)) OpenCards();
            else if (Input.GetKeyDown(KeyCode.H)) showHelp = true;
            else if (Input.GetKeyDown(KeyCode.Escape)) LoadScene(titleSceneName);
        }

        void MoveSelection(int dir)
        {
            int i = bossIndex;
            for (int n = 0; n < bosses.Length; n++)
            {
                i += dir;
                if (i < 0 || i >= bosses.Length) return;
                if (Unlocked(i)) { bossIndex = i; return; }
            }
        }

        // =========================================================
        // 동작
        // =========================================================

        bool Unlocked(int i) => i == 0 || (i > 0 && i < bosses.Length && ProgressStore.IsCleared(bosses[i - 1].id));

        bool Ready(int i) => i >= 0 && i < bosses.Length
                             && !string.IsNullOrEmpty(bosses[i].sceneName)
                             && Application.CanStreamedLevelBeLoaded(bosses[i].sceneName);

        void EnterBoss()
        {
            if (!Unlocked(bossIndex)) { Notify("이전 보스를 처치하면 열리는 문입니다", Muted); return; }
            if (!Ready(bossIndex)) { Notify($"{bosses[bossIndex].displayName}의 문은 아직 준비 중입니다", Muted); return; }
            LoadScene(bosses[bossIndex].sceneName);
        }

        void OpenCards()
        {
            if (cards == null) { Notify("강화 카드 설정(BuffGachaManager)을 찾지 못했습니다", Red); return; }
            if (cards.CardChosen) { Notify("이번 로비의 강화는 이미 골랐습니다 — 전투를 마치면 다시 열립니다", Muted); return; }
            offer = cards.GetOffer();
            cardError = null;
            showCards = true;
        }

        void CloseCards()
        {
            showCards = false;
            cardError = null;
        }

        void CloseModals()
        {
            showCards = showHelp = showHistory = false;
            if (showSettings) PlayerPrefs.Save();
            showSettings = false;
        }

        void Pick(int index)
        {
            if (cards == null || !showCards) return;
            if (cards.TryPick(index, out var r, out string error))
            {
                showCards = false;
                string head = r.tier == BuffTier.Chaos
                    ? $"혼돈의 주사위 → {BuffGachaManager.StatName(r.stat)} {BuffGachaManager.Signed(r.stat, r.value)}"
                    : $"[{BuffGachaManager.TierName(r.tier)}] {CardName(r.stat)} · {BuffGachaManager.StatName(r.stat)} {BuffGachaManager.Signed(r.stat, r.value)}";
                Notify($"강화 완료 — {head}   (남은 골드 {BattleStatsManager.GetCurrentGold()} G)", Gold);
            }
            else
            {
                cardError = error;
            }
        }

        void LoadScene(string sceneName)
        {
            if (leaving || string.IsNullOrEmpty(sceneName)) return;
            leaving = true;
            CloseModals();
            Time.timeScale = 1f;
            if (SceneFader.Instance != null) SceneFader.Instance.FadeToScene(sceneName);
            else SceneManager.LoadScene(sceneName);
        }

        void Notify(string text, Color color)
        {
            notice = text;
            noticeColor = color;
            noticeUntil = Time.unscaledTime + 4f;
        }

        // =========================================================
        // 그리기 도구 (GPT 시안과 같은 방식)
        // =========================================================

        void EnsureStyles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { font = font, richText = false, wordWrap = true };
            // GUIStyle.none에서 시작 — 기본 버튼 스킨은 고해상도 화면에서 회색 배경(scaledBackgrounds)이 남는다
            button = new GUIStyle(GUIStyle.none) { font = font, fontSize = 19, alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false };
        }

        static Color Alpha(Color c, float a) { c.a = a; return c; }

        void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }

        void Fill(float x, float y, float w, float h, Color c) => Fill(new Rect(x, y, w, h), c);

        void Text(float x, float y, float w, float h, string text, int size = 20, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            label.fontSize = size;
            label.normal.textColor = color ?? White;
            label.alignment = align;
            GUI.Label(new Rect(x, y, w, h), text, label);
        }

        void Rule(float x, float y, float w, Color? c = null) => Fill(new Rect(x, y, w, 1), c ?? Alpha(Gold, .25f));

        bool Button(float x, float y, float w, float h, string text, bool primary = false, bool enabled = true)
        {
            Rect r = new Rect(x, y, w, h);
            bool hover = GUI.enabled && enabled && r.Contains(Event.current.mousePosition);
            Fill(r, primary
                ? Alpha(Gold, enabled ? (hover ? 1f : .87f) : .15f)
                : hover ? new Color(.14f, .21f, .25f, .96f) : new Color(.065f, .10f, .14f, .95f));
            Fill(new Rect(x, y, primary ? 3 : 1, h), Alpha(Gold, enabled ? .75f : .15f));

            var old = GUI.enabled;
            GUI.enabled = old && enabled;
            Color textColor = primary ? (enabled ? Ink : Alpha(White, .45f)) : (enabled ? White : Muted);
            button.normal.textColor = textColor;
            button.hover.textColor = primary ? Ink : Gold;
            button.active.textColor = primary ? Ink : Gold;
            button.focused.textColor = textColor;
            bool clicked = GUI.Button(r, text, button);
            GUI.enabled = old;
            return clicked;
        }

        void Modal(float x, float y, float w, float h, string title)
        {
            Fill(-4000, -4000, 9600, 8900, new Color(0f, 0f, 0f, .62f));
            Fill(x, y, w, h, new Color(.04f, .06f, .09f, 1f));
            Fill(x, y, w, 2, Gold);
            Text(x + 34, y + 26, w - 68, 46, title, 30);
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        // =========================================================
        // 화면
        // =========================================================

        void OnGUI()
        {
            if (font == null) return;
            EnsureStyles();

            // 배경 (화면 전체)
            GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0, 0, Screen.width, Screen.height), Ink);
            if (backdrop != null)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), backdrop, ScaleMode.ScaleAndCrop);
                Fill(new Rect(0, 0, Screen.width, Screen.height), Alpha(Ink, backdropDim));
            }

            float scale = Mathf.Min(Screen.width / W, Screen.height / H);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * scale) / 2f, (Screen.height - H * scale) / 2f, 0f),
                                       Quaternion.identity, new Vector3(scale, scale, 1f));

            bool previous = GUI.enabled;
            GUI.enabled = previous && !AnyModal && !leaving;
            LobbyUI();
            GUI.enabled = previous;

            if (!AnyModal && notice != null && Time.unscaledTime < noticeUntil)
            {
                Fill(270, 153, 1060, 42, Panel);
                Text(285, 162, 1030, 32, notice, 16, noticeColor, TextAnchor.UpperCenter);
            }

            if (showCards) CardsUI();
            if (showHistory) HistoryUI();
            if (showSettings) SettingsUI();
            if (showHelp) HelpUI();

            if (!string.IsNullOrEmpty(ProgressStore.LastError))
                Text(20, 872, 1560, 24, "[진행 저장] " + ProgressStore.LastError, 13, Red);

            GUI.matrix = Matrix4x4.identity;
        }

        void LobbyUI()
        {
            int gold = BattleStatsManager.GetCurrentGold();
            var data = ProgressStore.Current;

            // 위
            Text(78, 52, 600, 44, "맹세의 회랑", 35);
            Text(80, 105, 800, 35, "탑은 당신을 기억합니다. 다음 문을 선택하세요.", 17, Muted);
            Text(1140, 57, 380, 42, gold + "  GOLD", 27, Gold, TextAnchor.UpperRight);
            if (Button(1170, 111, 108, 36, "기록")) showHistory = true;
            if (Button(1290, 111, 108, 36, "설정")) showSettings = true;
            if (Button(1410, 111, 108, 36, "도움말")) showHelp = true;

            // 왼쪽 — 보스 목록
            Text(80, 176, 340, 24, "CHAMBERS  /  도전할 보스", 14, Gold);
            for (int i = 0; i < bosses.Length && i < 5; i++)
            {
                var b = bosses[i];
                float y = 219 + i * 73;
                bool locked = !Unlocked(i), selected = i == bossIndex, cleared = ProgressStore.IsCleared(b.id);
                Fill(78, y, 390, 61, selected ? new Color(.15f, .20f, .23f, .96f) : Panel);
                if (selected) Fill(78, y, 3, 61, Gold);
                Text(95, y + 15, 45, 34, (i + 1).ToString("00"), 21, locked ? Muted : Gold);
                Text(150, y + 8, 300, 34, b.displayName, 23, locked ? Muted : White);
                string state = locked ? "이전 보스를 처치하면 개방"
                    : cleared ? "처치 완료 · 다시 도전 가능"
                    : !Ready(i) ? "준비 중인 문"
                    : i == 4 ? "히든 보스 · 심연의 문"
                    : "미완의 맹세";
                Text(150, y + 39, 300, 22, state, 12, Muted);
                if (!locked && GUI.Button(new Rect(78, y, 390, 61), GUIContent.none, GUIStyle.none)) bossIndex = i;
            }

            // 가운데 — 고른 보스
            if (bosses.Length > 0)
            {
                var sel = bosses[Mathf.Clamp(bossIndex, 0, bosses.Length - 1)];
                bool ready = Ready(bossIndex);
                Fill(503, 219, 420, 351, Panel);
                Text(528, 240, 375, 28, bossIndex == 4 ? "HIDDEN CHAMBER" : "CHAMBER 0" + (bossIndex + 1), 14, Gold);
                Text(524, 283, 385, 55, sel.displayName, 42);
                Text(528, 350, 370, 32, sel.title, 18, White);
                Rule(528, 401, 365);
                Text(528, 423, 365, 98, sel.lesson, 18, Muted);
                Text(528, 533, 365, 30, ready ? "처치 보상  /  보스 체력만큼 골드 · 다음 문 개방" : "이 문은 아직 준비 중입니다", 15, ready ? Gold : Muted);

                string enter = !ready ? "준비 중인 문입니다"
                    : data.cardChosen ? "강화 완료 · 보스전 입장   →"
                    : "강화 없이 바로 입장   →";
                if (Button(503, 589, 420, 58, enter, true, ready && Unlocked(bossIndex))) EnterBoss();
            }

            // 오른쪽 — 탑의 기록
            Fill(981, 219, 540, 428, Panel);
            Text(1003, 240, 490, 24, "TOWER RECORD  /  탑의 기록", 14, Gold);
            int clearedCount = 0;
            foreach (var b in bosses) if (ProgressStore.IsCleared(b.id)) clearedCount++;
            StatBlock(1003, data.battles.ToString(), "정산한 전투");
            StatBlock(1173, $"{clearedCount} / {bosses.Length}", "처치한 보스");
            StatBlock(1343, data.picks.Count.ToString(), "고른 강화");
            Rule(1003, 352, 496);
            Text(1003, 366, 300, 24, "최근 전투", 14, Gold);
            if (data.history.Count == 0)
            {
                Text(1003, 420, 496, 60, "첫 번째 문을 열면 기록이 시작됩니다.", 16, Muted, TextAnchor.UpperCenter);
            }
            for (int k = 0; k < data.history.Count && k < 3; k++)
            {
                var h = data.history[k];
                float y = 398 + k * 62;
                Fill(1003, y, 496, 54, CardBg);
                Text(1017, y + 6, 200, 28, BossName(h.boss), 17, h.clear ? Gold : White);
                Text(1190, y + 8, 90, 26, h.clear ? "처치" : "패배", 14, h.clear ? Teal : Muted);
                Text(1290, y + 8, 200, 26, h.date, 12, Muted, TextAnchor.UpperRight);
                Text(1017, y + 31, 470, 22, $"{FormatTime(h.seconds)}  ·  피해 {h.damage}  ·  패링 {h.parries}  ·  +{h.gold} G", 13, Muted);
            }
            if (Button(1311, 595, 190, 40, "전체 기록   →")) showHistory = true;

            // 아래 왼쪽 — 영구 강화
            Fill(80, 677, 843, 134, Panel);
            Text(100, 690, 440, 28, "영구 강화  /  사망해도 유지", 16, Gold);
            Text(100, 722, 440, 28, $"{StatLine(StatType.Attack)}     {StatLine(StatType.Health)}", 17);
            Text(100, 750, 440, 28, $"{StatLine(StatType.Speed)}     {StatLine(StatType.JumpPower)}", 17);
            Text(100, 782, 440, 24, $"고른 카드 {data.picks.Count}장  ·  전투 한 번에 한 장", 13, Muted);
            int cost = cards != null ? cards.CurrentCost : 0;
            string cardButton = cards == null ? "강화 설정 없음"
                : data.cardChosen ? "이번 로비 강화 완료"
                : $"강화 선택  [C]  ·  {cost} G";
            if (Button(560, 732, 340, 52, cardButton, false, cards != null && !data.cardChosen)) OpenCards();

            // 아래 오른쪽 — 조작 요약
            Fill(981, 677, 540, 134, Panel);
            Text(1003, 692, 490, 26, "조작  /  자세한 내용은 도움말 [H]", 15, Gold);
            Text(1003, 726, 500, 28, "← →  이동     SPACE  점프     Z  공격     F  패링", 16);
            Text(1003, 760, 500, 28, "SHIFT  회피     Q W E  스킬     G  포션     I  상태", 16);

            // 아래
            Rule(76, 844, 1448, Alpha(Stone, .6f));
            Text(78, 861, 600, 22, "REVERSAL TOWER  /  TEAM REDBULL", 12, Muted);
            Text(760, 858, 765, 28, "↑↓  보스 선택    ·    C  강화 선택    ·    ENTER  입장    ·    ESC  타이틀", 14, Muted, TextAnchor.UpperRight);
        }

        void StatBlock(float x, string value, string caption)
        {
            Text(x, 270, 160, 46, value, 34, White);
            Text(x, 318, 160, 22, caption, 13, Muted);
        }

        string StatLine(StatType stat) => $"{BuffGachaManager.StatName(stat)} {BuffGachaManager.Signed(stat, BuffGachaManager.StatTotal(stat))}";

        string BossName(string id)
        {
            foreach (var b in bosses)
                if (string.Equals(b.id, id, StringComparison.OrdinalIgnoreCase)) return b.displayName;
            return string.IsNullOrEmpty(id) ? "-" : id;
        }

        // =========================================================
        // 강화 카드
        // =========================================================

        static string CardName(StatType stat) => stat switch
        {
            StatType.Attack => "검의 단련",
            StatType.Health => "생명의 인장",
            StatType.Speed => "질풍의 인장",
            StatType.JumpPower => "도약의 인장",
            _ => ""
        };

        static string Glyph(StatType stat) => stat switch
        {
            StatType.Attack => "검",
            StatType.Health => "심",
            StatType.Speed => "풍",
            StatType.JumpPower => "익",
            _ => "?"
        };

        static Color TierColor(BuffTier tier) => tier switch
        {
            BuffTier.Common => new Color(0.95f, 0.95f, 0.95f),
            BuffTier.Rare => new Color(0.35f, 1.00f, 0.45f),
            BuffTier.Epic => new Color(0.35f, 0.75f, 1.00f),
            BuffTier.Legendary => new Color(1.00f, 0.88f, 0.20f),
            BuffTier.Chaos => new Color(0.88f, 0.40f, 1.00f),
            _ => Color.white
        };

        void CardsUI()
        {
            Modal(230, 205, 1140, 570, "맹세의 인장 · 하나를 선택하세요");
            int gold = BattleStatsManager.GetCurrentGold();
            int cost = cards != null ? cards.CurrentCost : 0;
            bool canPay = gold >= cost;
            Text(270, 273, 900, 30, $"보유 {gold} G  /  선택 비용 {cost} G  ·  강화는 영구 누적됩니다.", 18, Gold);
            if (!canPay) Text(1090, 273, 240, 30, "골드 부족", 18, Red, TextAnchor.UpperRight);
            if (!string.IsNullOrEmpty(cardError)) Text(270, 301, 1060, 24, cardError, 15, Red);

            if (offer == null || offer.Count != 3)
            {
                Text(280, 380, 1040, 80, "강화 후보를 불러오지 못했습니다. 닫고 다시 열어 주세요.", 20, White, TextAnchor.UpperCenter);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var o = offer[i];
                    float x = 280 + i * 350;
                    Color tc = TierColor(o.tier);
                    bool chaos = o.tier == BuffTier.Chaos;
                    float total = BuffGachaManager.StatTotal(o.statType);
                    string statName = BuffGachaManager.StatName(o.statType);

                    Fill(x, 328, 330, 296, CardBg);
                    Fill(x, 328, 330, 2, tc);
                    Text(x + 20, 338, 290, 24, $"[ {BuffGachaManager.TierName(o.tier)} ]", 14, tc, TextAnchor.UpperCenter);
                    Text(x + 20, 362, 290, 50, Glyph(o.statType), 32, Gold, TextAnchor.UpperCenter);
                    Text(x + 20, 414, 290, 40, CardName(o.statType), 23, White, TextAnchor.UpperCenter);
                    string desc = chaos
                        ? $"{statName} {cards.ChaosRange(o.statType)}\n고르는 순간 정해집니다"
                        : o.description;
                    Text(x + 25, 462, 280, 75, desc, 17, Muted, TextAnchor.UpperCenter);
                    string after = chaos ? "?" : BuffGachaManager.Signed(o.statType, total + (o.statType == StatType.Attack || o.statType == StatType.Health ? (int)o.deltaValue : o.deltaValue));
                    Text(x + 20, 560, 290, 30, $"쌓인 {statName} {BuffGachaManager.Signed(o.statType, total)} → {after}", 16, Teal, TextAnchor.UpperCenter);

                    if (Button(x, 639, 330, 51, $"이 카드 선택   [{i + 1}]", true, canPay)) { Pick(i); return; }
                }
            }

            if (Button(650, 711, 300, 42, "선택하지 않고 돌아가기")) CloseCards();
        }

        // =========================================================
        // 도움말 · 기록 · 설정
        // =========================================================

        void HelpUI()
        {
            Modal(260, 110, 1080, 690, "검보다 먼저, 패턴을 읽으세요");
            string[] keys = { "← / →", "SPACE", "Z", "F", "SHIFT", "Q / W / E", "G", "I  ·  ESC" };
            string[] actions =
            {
                "이동", "점프", "공격", "패링 · 보스의 신호에 맞춰 받아내기", "회피",
                "돌진 베기 / 회전 베기 / 처형 베기", "포션", "상태 창  ·  일시정지"
            };
            for (int i = 0; i < keys.Length; i++)
            {
                float y = 202 + i * 48;
                Text(298, y, 320, 32, keys[i], 18, Gold);
                Text(650, y, 640, 34, actions[i], 18, White);
            }
            Rule(298, 608, 1000);
            Text(298, 628, 1000, 80,
                "로비에서 C로 강화 카드를 고르고 ENTER로 입장합니다. 카드는 전투 한 번에 한 장이고, 사망해도 유지됩니다.\n" +
                "보스를 처치하면 다음 문이 열립니다. F1은 개발용 디버그 패널입니다.", 16, Muted);
            if (Button(1070, 726, 230, 44, "알겠습니다", true)) showHelp = false;
        }

        void HistoryUI()
        {
            var history = ProgressStore.Current.history;
            Modal(260, 110, 1080, 690, "탑이 기억하는 전투");
            Text(300, 192, 1000, 35, $"최근 {ProgressStore.HistoryLimit}회의 정산  ·  기록은 이 컴퓨터에 저장됩니다", 17, Muted);
            historyScroll = GUI.BeginScrollView(new Rect(294, 245, 1012, 450), historyScroll,
                                                new Rect(0, 0, 980, Mathf.Max(430, history.Count * 66)));
            if (history.Count == 0)
                Text(15, 80, 940, 80, "첫 번째 문을 열면, 당신의 기록이 시작됩니다.", 22, Muted, TextAnchor.UpperCenter);
            for (int i = 0; i < history.Count; i++)
            {
                var h = history[i];
                float y = i * 66;
                Fill(0, y, 975, 57, CardBg);
                Text(16, y + 9, 205, 35, BossName(h.boss), 20, h.clear ? Gold : White);
                Text(240, y + 12, 160, 30, h.clear ? "처치" : "패배", 16, h.clear ? Teal : Muted);
                Text(420, y + 12, 540, 30, $"{FormatTime(h.seconds)}   /   피해 {h.damage}   /   패링 {h.parries}   /   +{h.gold} G", 16, White);
                Text(15, y + 37, 840, 20, h.date, 11, Muted);
            }
            GUI.EndScrollView();
            if (Button(1069, 727, 230, 44, "닫기", true)) showHistory = false;
        }

        void SettingsUI()
        {
            Modal(440, 190, 720, 520, "설정");
            Text(478, 280, 600, 32, "전체 음량", 20);
            float v = GUI.HorizontalSlider(new Rect(480, 337, 560, 28), AudioListener.volume, 0f, 1f);
            if (!Mathf.Approximately(v, AudioListener.volume))
            {
                AudioListener.volume = v;
                PlayerPrefs.SetFloat(VolumeKey, v);
            }
            Text(1050, 328, 80, 30, Mathf.RoundToInt(AudioListener.volume * 100f) + "%", 18, Gold, TextAnchor.UpperRight);
            Rule(478, 400, 640);
            Text(478, 420, 640, 90, "골드와 진행 기록(강화·처치·전투 기록) 초기화는 타이틀 화면의 초기화 버튼을 쓰세요.", 15, Muted);
            if (Button(870, 640, 250, 44, "닫기", true)) { PlayerPrefs.Save(); showSettings = false; }
        }

        // =========================================================
        // 기본 보스 목록 (씬 이름은 인스펙터에서 바꿀 수 있음)
        // =========================================================

        static BossEntry[] DefaultBosses() => new[]
        {
            new BossEntry
            {
                id = "Arakus", displayName = "아라쿠스", title = "첫 번째 문 · 철의 맹세",
                lesson = "패링 신호에 맞춰 F로 받아내세요. 돌진은 기둥 쪽으로 유도하면 막힙니다.",
                sceneName = "BossTestScene"
            },
            new BossEntry
            {
                id = "Phanes", displayName = "파네스", title = "두 번째 문 · 거짓된 광휘",
                lesson = "빛의 탄환은 받아쳐 돌려보내고, 빛 기둥은 회피로 지나가세요.",
                sceneName = ""
            },
            new BossEntry
            {
                id = "Adicus", displayName = "아디쿠스", title = "세 번째 문 · 눈먼 심판",
                lesson = "딸려 오는 망자를 먼저 정리하고, 심판의 순간에는 빛나는 쪽으로 움직이세요.",
                sceneName = ""
            },
            new BossEntry
            {
                id = "Aethelos", displayName = "에텔로스", title = "탑의 정상 · 창조자의 몰락",
                lesson = "연속 공격을 패링으로 끊고, 남겨진 번개를 피한 뒤 빈틈을 노리세요.",
                sceneName = ""
            },
            new BossEntry
            {
                id = "Kharos", displayName = "카로스", title = "숨겨진 심연 · 먼저 추락한 자",
                lesson = "잔상이 공격을 되풀이합니다. 원본의 움직임을 먼저 읽으세요.",
                sceneName = ""
            },
        };
    }
}
