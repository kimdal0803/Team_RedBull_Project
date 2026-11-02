using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReversalTower.Lobby
{
    /// <summary>
    /// 메인 화면 (GPT 시안 0.4의 타이틀을 옮김). 왼쪽 어두운 패널에 제목·버튼, 오른쪽은 씬의 배경 그림이 그대로 보인다.
    /// IMGUI로 1600×900 기준 좌표에 그린다. 키: ENTER 탑에 들어가기 · H 조작 안내 · ESC 창 닫기
    /// 데이터 초기화는 MenuManager.ResetAllGameData (골드·강화·진행 기록)를 그대로 부른다.
    /// </summary>
    public sealed class TitleMenu : MonoBehaviour
    {
        [SerializeField] private string lobbySceneName = "LobbyScene";

        const string VolumeKey = "MasterVolume";
        const float W = 1600f, H = 900f;
        static readonly Color Gold = new Color(1f, .76f, .37f);
        static readonly Color White = new Color(.91f, .92f, .87f);
        static readonly Color Muted = new Color(.55f, .64f, .69f);
        static readonly Color Ink = new Color(.035f, .05f, .075f);

        enum Modal { None, Help, Settings, Reset }
        Modal modal;
        bool leaving;
        string notice;
        float noticeUntil;
        Font font;
        GUIStyle label, button;
        Texture2D white;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 22);
            white = Texture2D.whiteTexture;
        }

        void Update()
        {
            if (leaving) return;
            if (modal != Modal.None)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) modal = Modal.None;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Enter();
            else if (Input.GetKeyDown(KeyCode.H)) modal = Modal.Help;
        }

        void Enter()
        {
            if (leaving) return;
            leaving = true;
            Time.timeScale = 1f;
            if (SceneFader.Instance != null) SceneFader.Instance.FadeToScene(lobbySceneName);
            else SceneManager.LoadScene(lobbySceneName);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ResetAll()
        {
            if (MenuManager.Instance != null) MenuManager.Instance.ResetAllGameData();
            else
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Progress.ProgressStore.ResetAll();
            }
            modal = Modal.None;
            notice = "골드·강화·진행 기록을 모두 지웠습니다";
            noticeUntil = Time.unscaledTime + 3f;
        }

        // ---------------------------------------------------------------- 그리기

        static Color A(Color c, float a) { c.a = a; return c; }

        void Fill(float x, float y, float w, float h, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = old;
        }

        void Text(float x, float y, float w, float h, string s, int size, Color c, TextAnchor a = TextAnchor.UpperLeft)
        {
            label.fontSize = size; label.normal.textColor = c; label.alignment = a;
            GUI.Label(new Rect(x, y, w, h), s, label);
        }

        bool Button(float x, float y, float w, float h, string s, bool primary = false, int size = 19)
        {
            var r = new Rect(x, y, w, h);
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            Fill(x, y, w, h, primary ? A(Gold, hover ? 1f : .87f) : hover ? new Color(.14f, .21f, .25f, .96f) : new Color(.065f, .10f, .14f, .95f));
            Fill(x, y, primary ? 3 : 1, h, A(Gold, .75f));
            button.fontSize = size;
            button.normal.textColor = primary ? Ink : White;
            button.hover.textColor = primary ? Ink : Gold;
            return GUI.Button(r, s, button);
        }

        void Panel(float x, float y, float w, float h, string title)
        {
            Fill(-4000, -4000, 9600, 8900, new Color(0f, 0f, 0f, .62f));
            Fill(x, y, w, h, new Color(.04f, .06f, .09f, 1f));
            Fill(x, y, w, 2, Gold);
            Text(x + 34, y + 26, w - 68, 46, title, 30, White);
        }

        void OnGUI()
        {
            if (font == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { font = font, wordWrap = true, richText = false };
                button = new GUIStyle(GUIStyle.none) { font = font, alignment = TextAnchor.MiddleCenter };
            }

            GUI.matrix = Matrix4x4.identity;
            Fill(0, 0, Screen.width, Screen.height, A(Ink, .35f));   // 배경 그림을 살짝 어둡게
            float s = Mathf.Min(Screen.width / W, Screen.height / H);
            float ox = (Screen.width - W * s) / 2f;
            Fill(0, 0, ox + 760f * s, Screen.height, new Color(.02f, .035f, .055f, .86f));   // 왼쪽 패널 (화면 왼쪽 끝까지)
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, (Screen.height - H * s) / 2f, 0f), Quaternion.identity, new Vector3(s, s, 1f));

            bool prev = GUI.enabled;
            GUI.enabled = prev && modal == Modal.None && !leaving;

            Text(100, 113, 600, 35, "T O W E R   O F   R E V E R S A L", 18, Gold);
            Fill(100, 169, 72, 1, Gold);
            Text(93, 198, 700, 125, "역전의 탑", 83, White);
            Text(102, 341, 590, 100, "무너진 맹세를 넘어,\n신의 기술로 신에게 맞서라.", 27, White);
            Text(103, 467, 545, 65, "패턴을 읽고. 검을 받아내고.\n쓰러뜨린 자의 힘을 계승하세요.", 18, Muted);
            bool returning = Progress.ProgressStore.Current.battles > 0;
            if (Button(103, 577, 360, 62, returning ? "탑으로 돌아가기   →" : "탑에 들어가기   →", true)) Enter();
            if (Button(103, 655, 170, 45, "조작 안내")) modal = Modal.Help;
            if (Button(287, 655, 176, 45, "설정")) modal = Modal.Settings;
            Text(103, 730, 530, 32, "검과 패링으로 오르는 다섯 개의 문", 16, Muted);
            if (Button(103, 785, 170, 36, "게임 종료", false, 15)) Quit();
            if (Button(287, 785, 176, 36, "데이터 초기화", false, 15)) modal = Modal.Reset;
            Text(1040, 168, 450, 40, "01 — THE BROKEN OATH", 15, Gold, TextAnchor.UpperRight);

            Fill(76, 844, 1448, 1, new Color(.22f, .28f, .33f, .6f));
            Text(78, 861, 600, 22, "REVERSAL TOWER  /  TEAM REDBULL", 12, Muted);
            Text(760, 858, 765, 28, "ENTER  입장    ·    ← →  이동    ·    Z  공격    ·    F  패링", 14, Muted, TextAnchor.UpperRight);
            if (notice != null && Time.unscaledTime < noticeUntil) Text(103, 545, 600, 26, notice, 15, Gold);

            GUI.enabled = prev;
            if (modal == Modal.Help) HelpUI();
            else if (modal == Modal.Settings) SettingsUI();
            else if (modal == Modal.Reset) ResetUI();
            GUI.matrix = Matrix4x4.identity;
        }

        void HelpUI()
        {
            Panel(260, 110, 1080, 690, "검보다 먼저, 패턴을 읽으세요");
            string[] k = { "← / →", "SPACE", "Z", "F", "SHIFT", "Q / W / E", "G", "I  ·  ESC" };
            string[] v = { "이동", "점프", "공격", "패링 · 보스의 신호에 맞춰 받아내기", "회피", "돌진 베기 / 회전 베기 / 처형 베기", "포션", "상태 창  ·  일시정지" };
            for (int i = 0; i < k.Length; i++)
            {
                Text(298, 202 + i * 48, 320, 32, k[i], 18, Gold);
                Text(650, 202 + i * 48, 640, 34, v[i], 18, White);
            }
            Fill(298, 608, 1000, 1, A(Gold, .25f));
            Text(298, 628, 1000, 80, "로비에서 C로 강화 카드를 고르고 ENTER로 입장합니다. 보스를 처치하면 다음 문이 열립니다.", 16, Muted);
            if (Button(1070, 726, 230, 44, "알겠습니다", true)) modal = Modal.None;
        }

        void SettingsUI()
        {
            Panel(440, 190, 720, 520, "설정");
            Text(478, 280, 600, 32, "전체 음량", 20, White);
            float v = GUI.HorizontalSlider(new Rect(480, 337, 560, 28), AudioListener.volume, 0f, 1f);
            if (!Mathf.Approximately(v, AudioListener.volume)) { AudioListener.volume = v; PlayerPrefs.SetFloat(VolumeKey, v); }
            Text(1050, 328, 80, 30, Mathf.RoundToInt(AudioListener.volume * 100f) + "%", 18, Gold, TextAnchor.UpperRight);
            if (Button(870, 640, 250, 44, "닫기", true)) { PlayerPrefs.Save(); modal = Modal.None; }
        }

        void ResetUI()
        {
            Panel(560, 320, 480, 260, "데이터 초기화");
            Text(594, 390, 420, 70, "골드·강화·처치·전투 기록을 모두 지웁니다.\n되돌릴 수 없습니다.", 17, Muted);
            if (Button(594, 505, 200, 44, "초기화", true)) ResetAll();
            if (Button(806, 505, 200, 44, "취소")) modal = Modal.None;
        }
    }
}
