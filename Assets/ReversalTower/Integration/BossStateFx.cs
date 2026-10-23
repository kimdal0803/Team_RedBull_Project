using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 보스의 상태 변화를 '화면'으로 구분되게 한다. 소리는 BossAudio가 맡는다.
    ///
    ///   패링 성공   히트스톱 + 흰 섬광      — 막아냈다는 손맛
    ///   그로기      보스 노란 펄스 + 머리 위 표시 + 배너  — 지금 때리라는 신호
    ///   페이즈 1    붉은 섬광 + "2 페이즈" 배너
    ///   페이즈 2    더 강한 붉은 섬광 + "분노" 배너 + 보스가 계속 붉게
    ///   사망        슬로우모션 + 어두운 섬광
    ///
    /// 페이즈 전환이 두 번인데 신호가 서로 다르다는 점에 주의:
    ///   60% 트리거는 setPhase=2 라서 PhaseChanged 이벤트가 온다.
    ///   30% 트리거는 setPhase=0(유지) 이라 PhaseChanged가 오지 않는다 —
    ///   대신 강제 패턴 Arakus_Rage가 시작되는 것으로 알아챈다.
    ///
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 상태 연출 (화면)")]
    [RequireComponent(typeof(BossController))]
    public class BossStateFx : MonoBehaviour
    {
        [Header("패링 성공")]
        [Tooltip("시간을 멈추는 길이(초). 0이면 안 멈춘다")]
        [Min(0f)] public float parryHitStop = 0.08f;
        public Color parryFlash = new Color(1f, 1f, 1f, 0.55f);
        [Min(0f)] public float parryFlashTime = 0.12f;

        [Header("그로기")]
        public Color groggyTint = new Color(1f, 0.92f, 0.4f, 1f);
        [Tooltip("초당 깜빡임 횟수")]
        [Min(0.1f)] public float groggyPulseSpeed = 4f;
        [Tooltip("머리 위 표시를 띄운다")]
        public bool showGroggyMarker = true;
        [Tooltip("보스 발밑에서 표시까지 높이(m)")]
        public float groggyMarkerHeight = 3.0f;

        [Header("페이즈 전환")]
        public Color phase2Flash = new Color(1f, 0.35f, 0.2f, 0.45f);
        public Color rageFlash = new Color(1f, 0.15f, 0.1f, 0.7f);
        public Color rageTint = new Color(1f, 0.55f, 0.5f, 1f);
        [Tooltip("분노 패턴 ID — 이게 시작되면 '페이즈 전환 2번째'로 본다")]
        public string ragePatternId = "Arakus_Rage";
        [Min(0f)] public float bannerTime = 1.6f;

        [Header("사망")]
        [Tooltip("사망 시 슬로우모션 길이(초)")]
        [Min(0f)] public float deathSlowTime = 0.9f;
        [Range(0.05f, 1f)] public float deathSlowScale = 0.25f;
        public Color deathFlash = new Color(0.1f, 0f, 0f, 0.6f);

        [Header("배너 글자")]
        public bool showBanner = true;
        public string phase2Text = "2 페이즈";
        public string rageText = "분노";
        public string groggyText = "그로기!  지금 공격";

        BossController boss;
        SpriteRenderer bossSr;
        Color baseColor = Color.white;
        bool raged;

        string bannerText = "";
        float bannerUntil;
        Color bannerColor = Color.white;

        GameObject marker;
        SpriteRenderer markerSr;
        static Sprite quad;

        void Awake()
        {
            boss = GetComponent<BossController>();
            bossSr = GetComponentInChildren<SpriteRenderer>();
            if (bossSr != null) baseColor = bossSr.color;
        }

        void OnEnable()
        {
            boss.HitResolved += OnHitResolved;
            boss.PhaseChanged += OnPhaseChanged;
            boss.PatternStarted += OnPatternStarted;
            boss.Died += OnDied;
        }

        void OnDisable()
        {
            boss.HitResolved -= OnHitResolved;
            boss.PhaseChanged -= OnPhaseChanged;
            boss.PatternStarted -= OnPatternStarted;
            boss.Died -= OnDied;
            if (bossSr != null) bossSr.color = baseColor;
            if (marker != null) marker.SetActive(false);
        }

        void OnDestroy()
        {
            if (marker != null) Destroy(marker);
        }

        // ─────────────────────── 이벤트 ───────────────────────

        void OnHitResolved(PatternRun run, HitEvent hit, HitResult result)
        {
            if (result != HitResult.Parried) return;
            if (parryHitStop > 0f) HitStopper.Stop(parryHitStop);
            ScreenFlash.Flash(parryFlash, parryFlashTime);
        }

        void OnPhaseChanged(int phase)
        {
            ScreenFlash.Flash(phase2Flash, 0.3f);
            Banner($"{phase} 페이즈".Replace("2 페이즈", phase2Text), new Color(1f, 0.6f, 0.4f));
        }

        void OnPatternStarted(PatternRun run)
        {
            if (raged || run.PatternId != ragePatternId) return;
            raged = true;
            ScreenFlash.Flash(rageFlash, 0.45f);
            Banner(rageText, new Color(1f, 0.4f, 0.35f));
        }

        void OnDied()
        {
            if (deathSlowTime > 0f) HitStopper.Stop(deathSlowTime, deathSlowScale);
            ScreenFlash.Flash(deathFlash, 0.6f);
            if (marker != null) marker.SetActive(false);
            if (bossSr != null) bossSr.color = baseColor;
        }

        // ─────────────────────── 매 프레임 ───────────────────────

        void Update()
        {
            bool isGroggy = boss.Fsm != null && boss.Fsm.Current == boss.Groggy && !boss.IsDead;

            if (bossSr != null)
            {
                if (isGroggy)
                {
                    // 노랗게 깜빡 — 멀리서도 "지금 때릴 때"가 보인다
                    float t = Mathf.PingPong(Time.unscaledTime * groggyPulseSpeed, 1f);
                    bossSr.color = Color.Lerp(baseColor, groggyTint, t);
                }
                else if (raged)
                {
                    bossSr.color = rageTint;   // 분노 중에는 계속 붉게
                }
                else
                {
                    bossSr.color = baseColor;
                }
            }

            UpdateMarker(isGroggy);

            if (isGroggy && showBanner && Time.unscaledTime > bannerUntil)
                Banner(groggyText, new Color(1f, 0.9f, 0.4f), 0.4f);
        }

        void UpdateMarker(bool isGroggy)
        {
            if (!showGroggyMarker) return;

            if (!isGroggy)
            {
                if (marker != null && marker.activeSelf) marker.SetActive(false);
                return;
            }

            if (marker == null)
            {
                marker = new GameObject("~GroggyMarker");
                markerSr = marker.AddComponent<SpriteRenderer>();
                markerSr.sprite = GetQuad();
                if (bossSr != null)
                {
                    markerSr.sortingLayerID = bossSr.sortingLayerID;
                    markerSr.sortingOrder = bossSr.sortingOrder + 25;
                }
            }

            marker.SetActive(true);
            marker.transform.position = transform.position + Vector3.up * groggyMarkerHeight;

            float t = Mathf.PingPong(Time.unscaledTime * groggyPulseSpeed, 1f);
            float s = Mathf.Lerp(0.22f, 0.34f, t);
            marker.transform.localScale = new Vector3(s * 0.45f, s * 1.5f, 1f);   // 느낌표처럼 세로로 긴 네모
            var c = groggyTint;
            c.a = Mathf.Lerp(0.5f, 1f, t);
            markerSr.color = c;
        }

        // ─────────────────────── 배너 ───────────────────────

        void Banner(string text, Color color, float duration = -1f)
        {
            if (!showBanner) return;
            bannerText = text;
            bannerColor = color;
            bannerUntil = Time.unscaledTime + (duration > 0f ? duration : bannerTime);
        }

        void OnGUI()
        {
            if (!showBanner || Time.unscaledTime > bannerUntil || string.IsNullOrEmpty(bannerText)) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = bannerColor;

            var rect = new Rect(0f, Screen.height * 0.18f, Screen.width, 50f);

            // 글자가 배경에 묻히지 않게 그림자를 한 번 깔고 그린다
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), bannerText, shadow);
            GUI.Label(rect, bannerText, style);
        }

        static Sprite GetQuad()
        {
            if (quad != null) return quad;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            quad = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            quad.name = "BossStateFx_Quad";
            return quad;
        }
    }
}
