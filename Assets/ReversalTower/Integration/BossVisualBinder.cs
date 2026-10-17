using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// FSM 보스에 붙인 그림(Dark Knight 등)을 전투 상황에 맞게 움직인다.
    ///
    ///   보스가 맞으면     Hurt 트리거
    ///   보스가 걸으면     Speed 값 (Idle ↔ Walk 전환용)
    ///   보스가 죽으면     Death 트리거 (없으면 조용히 건너뜀)
    ///
    /// 없는 파라미터는 건드리지 않으므로, 어떤 애니메이터를 붙여도 경고가 나지 않는다.
    /// 패턴별 공격 모션은 패턴 에셋의 attackAnimTrigger가 맡는다 (BossController가 직접 재생).
    ///
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 그림 연결 (애니메이터)")]
    [RequireComponent(typeof(BossController))]
    public class BossVisualBinder : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("비워 두면 자식에서 찾는다")]
        public Animator animator;

        [Header("애니메이터 파라미터 이름")]
        public string hurtTrigger = "Hurt";
        public string deathTrigger = "Death";
        public string speedFloat = "Speed";

        [Header("걷기 판정")]
        [Tooltip("이 속도(m/s)를 넘으면 걷는 것으로 본다")]
        [Min(0f)] public float walkThreshold = 0.05f;

        BossController boss;
        float lastX;
        bool hasHurt, hasDeath, hasSpeed;

        void Awake()
        {
            boss = GetComponent<BossController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();

            hasHurt = HasParameter(hurtTrigger, AnimatorControllerParameterType.Trigger);
            hasDeath = HasParameter(deathTrigger, AnimatorControllerParameterType.Trigger);
            hasSpeed = HasParameter(speedFloat, AnimatorControllerParameterType.Float);

            lastX = transform.position.x;
        }

        void OnEnable()
        {
            boss.Damaged += OnDamaged;
            boss.Died += OnDied;
        }

        void OnDisable()
        {
            boss.Damaged -= OnDamaged;
            boss.Died -= OnDied;
        }

        void Update()
        {
            if (!hasSpeed || animator == null) return;
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            float speed = Mathf.Abs(transform.position.x - lastX) / dt;
            lastX = transform.position.x;
            animator.SetFloat(speedFloat, speed > walkThreshold ? speed : 0f);
        }

        void OnDamaged(float dealt, string cause)
        {
            if (hasHurt && animator != null) animator.SetTrigger(hurtTrigger);
        }

        void OnDied()
        {
            if (hasDeath && animator != null) animator.SetTrigger(deathTrigger);
        }

        bool HasParameter(string name, AnimatorControllerParameterType type)
        {
            if (animator == null || string.IsNullOrEmpty(name) || animator.runtimeAnimatorController == null)
                return false;
            foreach (var p in animator.parameters)
                if (p.type == type && p.name == name) return true;
            return false;
        }
    }
}
