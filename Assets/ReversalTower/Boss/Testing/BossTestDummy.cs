using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ReversalTower.Boss.Testing
{
    /// <summary>
    /// 플레이어 전투가 완성되기 전에 보스 패턴을 시험하는 허수아비.
    /// 플레이어 자리에 두고 플레이어 레이어를 준다 (BoxCollider2D는 자동으로 붙는다).
    ///
    ///   P : 패링 (입력 지연 0.05초 뒤 0.15초 동안 판정 — 전투방식설계서 4.1)
    ///   O : 보스에게 피해 (페이즈 전환 시험)
    ///   ← → : 좌우 이동 (등 뒤 조건, 거리별 패턴 시험)
    ///   L : 구르기 무적 흉내 (0.5초). 회피불가 공격이 정말 무적을 뚫는지 시험할 때 쓴다
    ///
    /// 플레이어 전투 보강이 끝나면 실제 플레이어 스크립트가 IBossAttackReceiver를 구현하고 이 컴포넌트는 지운다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class BossTestDummy : MonoBehaviour, IBossAttackReceiver
    {
        [Header("플레이어 기준값 (전투방식설계서 2.1 · 4.1)")]
        public float maxHp = 100f;
        public float parryInputDelay = 0.05f;
        public float parryWindow = 0.15f;
        public float hitInvincibleTime = 0.35f;
        public float moveSpeed = 7f;
        public bool applyKnockback = true;
        [Tooltip("켜면 패링 가능한 타를 자동으로 전부 패링 (그로기 흐름 시험용)")]
        public bool autoParry = false;

        [Header("구르기 흉내 (L키)")]
        [Tooltip("L을 누르면 이 시간만큼 무적이 된다. 회피불가 공격 시험용.")]
        public float rollInvincibleTime = 0.5f;

        [Header("보스 피해 시험")]
        public BossController boss;
        public float testDamage = 200f;

        public float Hp { get; private set; }

        float parryStart = -99f;
        float invincibleUntil = -99f;
        float rollUntil = -99f;

        void Awake()
        {
            Hp = maxHp;
        }

        void Update()
        {
            if (Pressed(TestKey.P)) parryStart = Time.time + parryInputDelay;
            if (Pressed(TestKey.O) && boss != null) boss.TakeDamage(testDamage, "테스트");
            if (Pressed(TestKey.L))
            {
                rollUntil = Time.time + rollInvincibleTime;
                Debug.Log($"[허수아비] 구르기 무적 {rollInvincibleTime:0.##}초");
            }

            float move = 0f;
            if (Held(TestKey.Left)) move -= 1f;
            if (Held(TestKey.Right)) move += 1f;
            transform.position += new Vector3(move * moveSpeed * Time.deltaTime, 0f, 0f);
        }

        public HitResult ReceiveBossHit(in BossHitInfo info)
        {
            float now = Time.time;

            // 1) 패링이 가장 먼저. 회피불가 공격도 패링은 통한다 (전투방식설계서 4.1)
            bool inParryWindow = now >= parryStart && now <= parryStart + parryWindow;
            if (info.Parryable && (autoParry || inParryWindow))
            {
                Debug.Log($"[허수아비] 패링 성공 — {info.PatternId} {info.HitIndex + 1}타");
                return HitResult.Parried;
            }

            // 2) 회피불가 공격은 구르기 무적·피격 무적을 뚫고 그대로 맞는다
            bool invincible = now < invincibleUntil || now < rollUntil;
            if (invincible && !info.IgnoresDodge) return HitResult.Ignored;

            Hp = Mathf.Max(0f, Hp - info.Damage);
            invincibleUntil = now + hitInvincibleTime;
            if (applyKnockback)
                transform.position += new Vector3(KnockbackTable.Distance(info.Knockback) * info.Direction, 0f, 0f);

            string tag = info.IgnoresDodge && invincible ? " [회피불가 — 무적 관통]" : "";
            string from = info.FromProjectile ? " (투사체)" : "";
            Debug.Log($"[허수아비] 피격 {info.Damage:0.#}{from}{tag} — HP {Hp:0}/{maxHp:0}, 넉백 {info.Knockback}");
            return HitResult.Hit;
        }

        // ── 입력 (새 Input System / 예전 Input Manager 둘 다 동작) ──
        enum TestKey { P, O, L, Left, Right }

        static bool Pressed(TestKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case TestKey.P: return kb.pKey.wasPressedThisFrame;
                case TestKey.O: return kb.oKey.wasPressedThisFrame;
                case TestKey.L: return kb.lKey.wasPressedThisFrame;
                case TestKey.Left: return kb.leftArrowKey.wasPressedThisFrame;
                default: return kb.rightArrowKey.wasPressedThisFrame;
            }
#else
            return Input.GetKeyDown(ToKeyCode(key));
#endif
        }

        static bool Held(TestKey key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case TestKey.P: return kb.pKey.isPressed;
                case TestKey.O: return kb.oKey.isPressed;
                case TestKey.L: return kb.lKey.isPressed;
                case TestKey.Left: return kb.leftArrowKey.isPressed;
                default: return kb.rightArrowKey.isPressed;
            }
#else
            return Input.GetKey(ToKeyCode(key));
#endif
        }

#if !ENABLE_INPUT_SYSTEM
        static KeyCode ToKeyCode(TestKey key)
        {
            switch (key)
            {
                case TestKey.P: return KeyCode.P;
                case TestKey.O: return KeyCode.O;
                case TestKey.L: return KeyCode.L;
                case TestKey.Left: return KeyCode.LeftArrow;
                default: return KeyCode.RightArrow;
            }
        }
#endif
    }
}
