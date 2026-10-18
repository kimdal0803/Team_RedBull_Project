using ReversalTower.Boss;
using UnityEngine;

namespace ReversalTower.Integration
{
    /// <summary>
    /// 보스를 바닥에 붙여 둔다. 언덕을 오르내리고, 발판이 없으면 떨어진다.
    ///
    /// BossController는 패턴 데이터대로 transform을 직접 움직이고 물리를 쓰지 않는다.
    /// (그래야 돌진 거리·속도가 기획서 수치 그대로 나온다.)
    /// 그래서 가로 이동만 할 뿐 세로는 아무도 책임지지 않아, 언덕 밖으로 걸어가면 공중에 떠 있었다.
    ///
    /// 이 컴포넌트는 Rigidbody2D를 쓰지 않고 매 프레임 발밑으로 광선을 쏴서 직접 맞춘다.
    /// Rigidbody2D를 붙이면 물리가 BossController의 이동과 싸워서 돌진 거리가 틀어진다.
    ///
    /// LateUpdate에서 돈다 — BossController가 가로로 옮긴 뒤에 세로를 정리해야 하기 때문이다.
    ///
    /// 붙이는 곳: BossController가 붙은 보스 오브젝트
    /// </summary>
    [AddComponentMenu("역전의 탑/보스 바닥 붙기 (중력)")]
    [RequireComponent(typeof(BossController))]
    [DefaultExecutionOrder(100)]
    public class BossGroundSnap : MonoBehaviour
    {
        [Header("바닥")]
        [Tooltip("바닥으로 칠 레이어. 보통 Ground. 비워 두면 Ground 레이어를 찾아 쓴다.")]
        public LayerMask groundMask;

        [Header("중력")]
        [Tooltip("낙하 가속도(m/s²)")]
        [Min(0f)] public float gravity = 30f;
        [Tooltip("최대 낙하 속도(m/s)")]
        [Min(0.1f)] public float maxFallSpeed = 20f;

        [Header("붙기")]
        [Tooltip("발밑 이 거리 안에 바닥이 있으면 붙는다. 작으면 내리막에서 통통 튄다.")]
        [Min(0.01f)] public float snapDistance = 0.45f;
        [Tooltip("이 높이까지는 턱을 올라간다. 이보다 높으면 무시 (벽을 타고 오르지 않게)")]
        [Min(0f)] public float maxStepUp = 0.6f;

        [Header("안전장치")]
        [Tooltip("시작 높이에서 이만큼 아래로 떨어지면 멈춘다 (맵 밖으로 사라지는 것 방지)")]
        [Min(1f)] public float maxFallBelowStart = 40f;

        [Header("디버그")]
        public bool drawGizmo = true;
        public bool logToConsole = false;

        BossController boss;
        Collider2D[] ownColliders;
        float verticalSpeed;
        float startY;
        bool warnedFall;

        void Awake()
        {
            boss = GetComponent<BossController>();
            ownColliders = GetComponentsInChildren<Collider2D>(true);
            startY = transform.position.y;

            if (groundMask.value == 0)
            {
                int ground = LayerMask.NameToLayer("Ground");
                groundMask = ground >= 0 ? (1 << ground) : Physics2D.AllLayers;
                if (ground < 0)
                    Debug.LogWarning("[바닥 붙기] 'Ground' 레이어가 없어 모든 레이어를 바닥으로 봅니다. " +
                                     "Ground Mask를 직접 지정해 주세요.", this);
            }
        }

        void LateUpdate()
        {
            if (boss != null && boss.IsDead) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = transform.position;

            // 이번 프레임에 떨어질 거리만큼은 더 내려다본다 (빠르게 떨어질 때 바닥을 뚫지 않게)
            float fallThisFrame = Mathf.Abs(Mathf.Min(0f, verticalSpeed)) * dt;
            float probeDown = snapDistance + fallThisFrame;

            if (TryFindGround(pos, probeDown, out float groundY))
            {
                // 바닥 위 또는 붙을 만큼 가까움 → 딱 붙이고 낙하 속도 초기화
                pos.y = groundY;
                transform.position = pos;
                verticalSpeed = 0f;
                warnedFall = false;
                return;
            }

            // 공중 → 떨어진다
            verticalSpeed = Mathf.Max(-maxFallSpeed, verticalSpeed - gravity * dt);
            pos.y += verticalSpeed * dt;

            if (pos.y < startY - maxFallBelowStart)
            {
                pos.y = startY - maxFallBelowStart;
                verticalSpeed = 0f;
                if (!warnedFall)
                {
                    warnedFall = true;
                    Debug.LogWarning($"[바닥 붙기] {boss?.bossId} 아래에 바닥이 없어 떨어지다 멈췄습니다. " +
                                     "보스방 바닥 콜라이더가 Ground 레이어인지 확인해 주세요.", this);
                }
            }

            transform.position = pos;
        }

        /// <summary>발밑(조금 위에서부터)으로 광선을 쏴 설 수 있는 바닥의 윗면을 찾는다.</summary>
        bool TryFindGround(Vector3 feet, float probeDown, out float groundY)
        {
            groundY = 0f;
            Vector2 origin = new Vector2(feet.x, feet.y + maxStepUp);
            float length = maxStepUp + probeDown;

            var hits = Physics2D.RaycastAll(origin, Vector2.down, length, groundMask);
            bool found = false;
            float best = 0f;

            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.isTrigger) continue;
                if (IsOwn(h.collider)) continue;
                if (h.collider.GetComponentInParent<PlayerMovement>() != null) continue;

                float y = h.point.y;
                if (y > feet.y + maxStepUp) continue;          // 너무 높은 턱은 안 올라간다
                if (y < feet.y - probeDown) continue;          // 너무 먼 바닥은 아직 공중

                if (!found || y > best) { best = y; found = true; }   // 가장 높은 바닥에 선다
            }

            groundY = best;
            return found;
        }

        bool IsOwn(Collider2D c)
        {
            if (ownColliders == null) return false;
            foreach (var own in ownColliders)
                if (own == c) return true;
            return false;
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmo) return;
            Vector3 p = transform.position;
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.9f);
            Gizmos.DrawLine(p + Vector3.up * maxStepUp, p + Vector3.down * snapDistance);
            Gizmos.DrawWireCube(p, new Vector3(0.25f, 0.04f, 0f));
        }
    }
}
