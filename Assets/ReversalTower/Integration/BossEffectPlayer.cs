using System;
using System.Collections.Generic;
using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 보스 패턴에 이펙트를 띄운다.
    ///
    /// 패턴 데이터에는 원래 fxKey 칸만 있고 재생하는 코드가 없었다. 그 자리를 메운다.
    ///   패턴 시작   → 그 패턴에 지정된 이펙트를 보스 앞에 띄운다
    ///   타격        → 맞은 자리에 타격 이펙트
    ///   패링당함    → 패링 이펙트
    ///
    /// 패턴마다 다른 이펙트를 주는 것이 목적이다. 11개 패턴이 전부 똑같아 보이면
    /// 플레이어가 무슨 공격인지 구분을 못 하고, 그러면 DDA 플레이테스트 데이터도 의미가 없다.
    ///
    /// 연결은 '게임 흐름에 아라쿠스 넣기' 메뉴가 자동으로 해 준다.
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 이펙트 재생")]
    [RequireComponent(typeof(BossController))]
    public class BossEffectPlayer : MonoBehaviour
    {
        [Serializable]
        public class PatternEffect
        {
            [Tooltip("패턴 ID (예: Arakus_SwordSwinging)")]
            public string patternId;
            [Tooltip("패턴이 시작될 때 띄울 이펙트 프리팹")]
            public GameObject prefab;
            [Tooltip("보스 정면에서 앞으로 얼마나 떨어뜨릴지(m)")]
            public float forward = 1.2f;
            [Tooltip("바닥에서 높이(m)")]
            public float height = 1.1f;
            [Tooltip("크기 배율")]
            [Min(0.1f)] public float scale = 1f;
        }

        [Header("패턴별 이펙트")]
        public List<PatternEffect> patternEffects = new List<PatternEffect>();

        [Header("공통 이펙트")]
        [Tooltip("플레이어가 맞았을 때")]
        public GameObject hitEffect;
        [Tooltip("플레이어가 패링했을 때")]
        public GameObject parryEffect;
        [Tooltip("공통 이펙트 크기 배율")]
        [Min(0.1f)] public float commonScale = 0.8f;

        [Header("기타")]
        [Tooltip("패턴 시작이 아니라 '첫 판정 직전'에 띄운다 (선딜이 길 때 자연스러움)")]
        public bool playOnTelegraphEnd = true;
        public bool logToConsole = false;

        BossController boss;
        readonly Dictionary<string, PatternEffect> lookup = new Dictionary<string, PatternEffect>();
        PatternRun pending;

        void Awake()
        {
            boss = GetComponent<BossController>();
            RebuildLookup();
        }

        void RebuildLookup()
        {
            lookup.Clear();
            foreach (var e in patternEffects)
                if (e != null && !string.IsNullOrEmpty(e.patternId) && e.prefab != null)
                    lookup[e.patternId] = e;
        }

        void OnEnable()
        {
            boss.PatternStarted += OnPatternStarted;
            boss.HitResolved += OnHitResolved;
            boss.PatternEnded += OnPatternEnded;
        }

        void OnDisable()
        {
            boss.PatternStarted -= OnPatternStarted;
            boss.HitResolved -= OnHitResolved;
            boss.PatternEnded -= OnPatternEnded;
            pending = null;
        }

        void OnPatternStarted(PatternRun run)
        {
            if (playOnTelegraphEnd) { pending = run; return; }
            Play(run);
        }

        void Update()
        {
            // 선딜이 끝나는 시점에 띄운다
            if (pending == null) return;
            if (pending.Elapsed < pending.Telegraph) return;
            var run = pending;
            pending = null;
            Play(run);
        }

        void OnPatternEnded(PatternRun run, bool interrupted) => pending = null;

        void Play(PatternRun run)
        {
            if (run == null || !lookup.TryGetValue(run.PatternId, out var e)) return;

            Vector3 pos = transform.position
                          + new Vector3(boss.Facing * (boss.bodyWidth * 0.5f + e.forward), e.height, 0f);
            SpriteSheetEffect.Spawn(e.prefab, pos, boss.Facing, e.scale);

            if (logToConsole) Debug.Log($"[이펙트] {run.PatternId} → {e.prefab.name}", this);
        }

        void OnHitResolved(PatternRun run, HitEvent hit, HitResult result)
        {
            GameObject prefab =
                result == HitResult.Hit ? hitEffect :
                result == HitResult.Parried ? parryEffect : null;
            if (prefab == null) return;

            Rect r = boss.GetHitboxRect(hit.Source.box);
            SpriteSheetEffect.Spawn(prefab, new Vector3(r.center.x, r.center.y, 0f), boss.Facing, commonScale);
        }
    }
}
