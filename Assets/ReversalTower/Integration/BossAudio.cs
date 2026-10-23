using System;
using System.Collections.Generic;
using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 보스의 소리를 담당한다. 패턴·패링·그로기·페이즈 전환·사망을 귀로 구분할 수 있게 한다.
    ///
    /// 지금 프로젝트에 쓸 수 있는 효과음이 6개뿐이라, 같은 클립을 피치(재생 속도)로 바꿔
    /// 패턴 11개를 구분한다. 거검은 낮게(묵직), 발차기는 높게(가볍게) 같은 식이다.
    /// 나중에 진짜 효과음이 들어오면 Inspector에서 클립만 갈아끼우면 된다.
    ///
    /// PlayOneShot은 호출마다 피치를 따로 줄 수 없어서, AudioSource를 몇 개 돌려 쓴다.
    ///
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 사운드")]
    [RequireComponent(typeof(BossController))]
    public class BossAudio : MonoBehaviour
    {
        [Serializable]
        public class PatternSound
        {
            [Tooltip("패턴 ID (예: Arakus_SwordSwinging)")]
            public string patternId;
            public AudioClip clip;
            [Tooltip("재생 속도. 낮을수록 묵직하고 낮은 소리가 난다")]
            [Range(0.4f, 2f)] public float pitch = 1f;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Serializable]
        public class Cue
        {
            public AudioClip clip;
            [Range(0.4f, 2f)] public float pitch = 1f;
            [Range(0f, 1f)] public float volume = 1f;

            public Cue() { }
            public Cue(AudioClip c, float p, float v) { clip = c; pitch = p; volume = v; }
        }

        [Header("패턴별 소리 (선딜 시작과 함께 울린다 = 귀로 듣는 예고)")]
        public List<PatternSound> patternSounds = new List<PatternSound>();

        [Header("상태 소리")]
        [Tooltip("플레이어가 패링에 성공했을 때 — 쇳소리")]
        public Cue parrySuccess = new Cue(null, 1.8f, 1f);
        [Tooltip("보스가 그로기에 빠졌을 때 — 둔탁하게")]
        public Cue groggy = new Cue(null, 0.6f, 1f);
        [Tooltip("페이즈가 바뀔 때 (2페이즈 진입 / 분노)")]
        public Cue phaseChange = new Cue(null, 0.8f, 1f);
        [Tooltip("보스 사망")]
        public Cue death = new Cue(null, 1f, 1f);
        [Tooltip("보스가 플레이어 공격에 맞았을 때")]
        public Cue bossHurt = new Cue(null, 1f, 0.6f);

        [Header("설정")]
        [Tooltip("동시에 울릴 수 있는 소리 개수")]
        [Range(2, 12)] public int voices = 6;
        [Tooltip("전체 음량")]
        [Range(0f, 1f)] public float masterVolume = 0.9f;
        [Tooltip("피격음이 너무 자주 울리지 않게 하는 최소 간격(초)")]
        [Min(0f)] public float hurtInterval = 0.12f;
        [Tooltip("매번 조금씩 피치를 흔들어 기계음처럼 들리지 않게 한다")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.06f;
        public bool logToConsole = false;

        BossController boss;
        AudioSource[] sources;
        int next;
        float lastHurtTime = -99f;
        readonly Dictionary<string, PatternSound> lookup = new Dictionary<string, PatternSound>();

        void Awake()
        {
            boss = GetComponent<BossController>();
            BuildLookup();
            BuildSources();
        }

        void BuildLookup()
        {
            lookup.Clear();
            foreach (var p in patternSounds)
                if (p != null && !string.IsNullOrEmpty(p.patternId) && p.clip != null)
                    lookup[p.patternId] = p;
        }

        void BuildSources()
        {
            var holder = new GameObject("~Audio");
            holder.transform.SetParent(transform, false);
            sources = new AudioSource[Mathf.Max(2, voices)];
            for (int i = 0; i < sources.Length; i++)
            {
                var a = holder.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;   // 2D — 거리에 따라 작아지지 않게
                sources[i] = a;
            }
        }

        void OnEnable()
        {
            boss.PatternStarted += OnPatternStarted;
            boss.HitResolved += OnHitResolved;
            boss.GroggyStarted += OnGroggy;
            boss.PhaseChanged += OnPhaseChanged;
            boss.Damaged += OnDamaged;
            boss.Died += OnDied;
        }

        void OnDisable()
        {
            boss.PatternStarted -= OnPatternStarted;
            boss.HitResolved -= OnHitResolved;
            boss.GroggyStarted -= OnGroggy;
            boss.PhaseChanged -= OnPhaseChanged;
            boss.Damaged -= OnDamaged;
            boss.Died -= OnDied;
        }

        // ─────────────────────── 이벤트 ───────────────────────

        void OnPatternStarted(PatternRun run)
        {
            // 분노는 페이즈 전환 2번째이기도 하다.
            // (30% 트리거는 setPhase가 0이라 PhaseChanged가 울리지 않는다)
            if (run.PatternId == "Arakus_Rage") PlayCue(phaseChange);

            if (lookup.TryGetValue(run.PatternId, out var s))
            {
                Play(s.clip, s.pitch, s.volume);
                if (logToConsole) Debug.Log($"[보스 사운드] {run.PatternId} ({s.clip.name}, 피치 {s.pitch:0.00})", this);
            }
        }

        void OnHitResolved(PatternRun run, HitEvent hit, HitResult result)
        {
            if (result == HitResult.Parried) PlayCue(parrySuccess);
        }

        void OnGroggy(float duration, string reason) => PlayCue(groggy);

        void OnPhaseChanged(int phase) => PlayCue(phaseChange);

        void OnDamaged(float dealt, string cause)
        {
            if (Time.unscaledTime - lastHurtTime < hurtInterval) return;
            lastHurtTime = Time.unscaledTime;
            PlayCue(bossHurt);
        }

        void OnDied() => PlayCue(death);

        // ─────────────────────── 재생 ───────────────────────

        void PlayCue(Cue c)
        {
            if (c != null) Play(c.clip, c.pitch, c.volume);
        }

        /// <summary>소리 하나를 재생한다. 쓸 수 있는 AudioSource를 돌려 쓴다.</summary>
        public void Play(AudioClip clip, float pitch, float volume)
        {
            if (clip == null || sources == null || sources.Length == 0) return;

            var a = sources[next];
            next = (next + 1) % sources.Length;

            a.clip = clip;
            a.pitch = Mathf.Clamp(pitch + UnityEngine.Random.Range(-pitchJitter, pitchJitter), 0.1f, 3f);
            a.volume = Mathf.Clamp01(volume * masterVolume);
            a.Play();
        }

        /// <summary>에디터에서 목록을 바꾼 뒤 바로 반영하고 싶을 때</summary>
        public void Refresh() => BuildLookup();
    }
}
