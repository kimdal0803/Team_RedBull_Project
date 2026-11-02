using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>프레임을 바꾸는 기준 시각 (패턴 시작부터 잰다)</summary>
    public enum SpriteKeyEvent
    {
        Start,      // 선딜 시작
        Attack,     // 선딜이 끝나고 공격 단계 시작
        Recovery,   // 공격 단계가 끝나고 후딜 시작
        DashStart,  // 이동 시작 (Dash·Projectile 패턴)
        DashEnd,    // 이동 끝
        Hit,        // hitIndex번째 판정 시각
        EachHit,    // 모든 판정마다 반복 (DDA가 타수를 늘려도 그대로 따라간다)
    }

    /// <summary>
    /// "이 시각부터 이 프레임" 한 줄.
    /// EachHit이면 타마다 [판정 + offset → frame], [판정 + afterOffset → afterFrame] 두 줄로 펼쳐지고,
    /// 마지막 타 뒤에는 afterFrame 대신 lastAfterFrame을 쓴다.
    /// </summary>
    [Serializable]
    public class SpriteKey
    {
        public SpriteKeyEvent at = SpriteKeyEvent.Start;
        [Tooltip("at = Hit일 때 몇 번째 판정인지 (0부터)")]
        [Min(0)] public int hitIndex;
        [Tooltip("기준 시각에서 몇 초 뒤 (음수면 그만큼 먼저). 속도 배율이 적용된다")]
        public float offset;
        [Tooltip("시트 안의 프레임 번호 (0부터)")]
        [Min(0)] public int frame;
        [Tooltip("이 프레임으로 바뀌는 순간 몸이 앞으로 늘어났다 돌아온다 (타격감)")]
        public bool punch;
        [Tooltip("화면 흔들림 — 0 없음, 1 약하게, 2 세게")]
        [Range(0, 2)] public int shake;

        [Header("EachHit 전용")]
        [Tooltip("판정 뒤 몇 초에 다음 프레임으로")]
        public float afterOffset = 0.25f;
        [Min(0)] public int afterFrame;
        [Tooltip("마지막 판정 뒤에 쓸 프레임 (보통 복귀 프레임)")]
        [Min(0)] public int lastAfterFrame;
    }

    /// <summary>패턴 하나의 도트 애니메이션 (시트 한 장 + 언제 어느 프레임인지)</summary>
    [Serializable]
    public class BossSpriteAnim
    {
        [Tooltip("BossPatternData.patternId와 같아야 한다")]
        public string patternId;
        [Tooltip("엑셀 에셋 목록의 파일 이름 (참고용)")]
        public string sheetName;
        public Sprite[] frames = new Sprite[0];
        public List<SpriteKey> keys = new List<SpriteKey>();

        [Header("점프 곡선 (그림만 뜨고, 판정 위치는 그대로)")]
        public bool arc;
        public SpriteKeyEvent arcFrom = SpriteKeyEvent.Attack;
        public SpriteKeyEvent arcTo = SpriteKeyEvent.Recovery;
        [Tooltip("가장 높이 뜨는 높이 (m)")]
        [Min(0)] public float arcHeight = 0.5f;

        [Header("선딜 끝 떨림")]
        [Tooltip("공격 직전 0.2초 동안 1픽셀씩 떤다 (선딜이 0.45초 이상일 때만)")]
        public bool tremble = true;
    }

    /// <summary>
    /// 보스 하나의 도트 스프라이트 묶음. BossSpriteAnimator가 읽는다.
    /// 아라쿠스는 메뉴 '역전의 탑 ▸ 아라쿠스 도트 ▸ 1'이 만들어 준다.
    /// </summary>
    [CreateAssetMenu(menuName = "역전의 탑/보스 도트 스프라이트 세트", fileName = "BossSpriteSet")]
    public class BossSpriteSet : ScriptableObject
    {
        [Header("대기 (패턴이 없을 때)")]
        public Sprite[] idle = new Sprite[0];
        [Tooltip("재생 순서 (프레임 번호). 비우면 0,1,2…")]
        public int[] idleOrder = { 0, 1, 2, 1 };
        [Min(0.1f)] public float idleFps = 4f;

        [Header("그로기 / 사망")]
        public Sprite[] groggy = new Sprite[0];
        [Min(0.1f)] public float groggyFps = 3f;

        [Header("던지는 방패 (투사체)")]
        public Sprite projectileSprite;

        [Header("패턴별")]
        public List<BossSpriteAnim> patterns = new List<BossSpriteAnim>();

        public BossSpriteAnim Find(string patternId)
        {
            if (string.IsNullOrEmpty(patternId)) return null;
            foreach (var p in patterns)
                if (p != null && p.patternId == patternId) return p;
            return null;
        }
    }
}
