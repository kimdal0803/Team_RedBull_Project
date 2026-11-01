using UnityEngine;

namespace ReversalTower.Boss.Phanes
{
    /// <summary>
    /// 파네스가 싸우는 방의 크기와 모양. 순간이동 좌표, 빛 기둥 높이, 바닥 장판 범위가 모두 여기서 나온다.
    ///
    /// 자동 측정 (기본)
    ///   Play를 누르면 보스 시작 위치에서 아래·좌우·위로 레이를 쏴서
    ///   최하단 바닥, 좌우 벽, 천장을 찾는다. 못 찾으면 대체값(fallback)을 쓴다.
    ///   방이 특이하면 autoFit을 끄고 left/right/floorY/ceilingY를 직접 적는다.
    ///
    /// 기획서 연결
    ///   3.1 이동: '화면 내 지정된 체공 좌표로 텔레포트' → teleportPoints(직접 지정) 또는 자동 좌표
    ///   4.2 영벌의 업화: '최하단 바닥 전체' → floorY, left~right
    ///   4.2 단죄의 빛 기둥 / 4.3 결계벽: 'Y축 무한' → floorY~ceilingY
    ///
    /// 붙이는 곳: 파네스 보스 오브젝트 (PhanesBrain과 같이)
    /// </summary>
    [AddComponentMenu("역전의 탑/파네스/전투 구역 (PhanesArena)")]
    public class PhanesArena : MonoBehaviour
    {
        [Header("자동 측정")]
        [Tooltip("Play 시작 때 바닥·벽·천장을 레이로 찾는다")]
        public bool autoFit = true;
        [Tooltip("바닥·발판 레이어 (플레이어가 밟는 것)")]
        public LayerMask groundMask = 1 << 6;
        [Tooltip("좌우 벽·천장 레이어")]
        public LayerMask wallMask = 1 | (1 << 6) | (1 << 10);
        [Tooltip("벽을 못 찾으면 시작 위치에서 좌우로 이만큼(m)")]
        [Min(2f)] public float fallbackHalfWidth = 12f;
        [Tooltip("천장을 못 찾으면 바닥에서 이만큼(m) 위")]
        [Min(3f)] public float fallbackHeight = 12f;

        [Header("구역 (자동 측정 결과 또는 직접 입력, 월드 좌표)")]
        public float left = -12f;
        public float right = 12f;
        [Tooltip("최하단 바닥 높이")]
        public float floorY = 0f;
        public float ceilingY = 12f;

        [Header("순간이동 (기획서 3.1)")]
        [Tooltip("직접 정한 체공 좌표. 비워 두면 플레이어 주변에서 자동으로 고른다")]
        public Transform[] teleportPoints;
        [Tooltip("플레이어와 최소 거리(m) — '간격 유지'")]
        [Min(0f)] public float teleportMinDistance = 3.5f;
        [Tooltip("플레이어와 최대 거리(m)")]
        [Min(0.5f)] public float teleportMaxDistance = 8f;
        [Tooltip("발밑 지면에서 떠 있는 높이(m). 점프 공격이 닿는 높이로 둔다")]
        [Min(0f)] public float hoverHeight = 1.3f;
        [Tooltip("이 확률로 더 높이 뜬다")]
        [Range(0f, 1f)] public float highHoverChance = 0.3f;
        [Min(0f)] public float highHoverExtra = 1.4f;
        [Tooltip("켜면 카메라에 보이는 범위 안에서만 고른다 ('화면 내')")]
        public bool keepInCameraView = true;
        [Tooltip("벽·화면 끝에서 띄울 여유(m)")]
        [Min(0f)] public float edgeMargin = 1.2f;

        public bool IsFitted { get; private set; }
        public float Width => right - left;
        public float CenterX => (left + right) * 0.5f;
        public Rect Bounds => new Rect(left, floorY, right - left, ceilingY - floorY);

        void Start()
        {
            EnsureFitted();
        }

        public void EnsureFitted()
        {
            if (IsFitted) return;
            if (autoFit) Fit(transform.position);
            IsFitted = true;
        }

        /// <summary>probe 위치에서 레이를 쏴서 구역을 잰다.</summary>
        public void Fit(Vector2 probe)
        {
            // 최하단 바닥: 아래로 쏴서 맞은 면 중 가장 낮은 것 (중간 발판은 건너뛴다)
            float floor = float.NaN;
            foreach (var h in Physics2D.RaycastAll(probe + Vector2.up * 0.2f, Vector2.down, 14f, groundMask))
            {
                if (!IsStatic(h.collider)) continue;
                floor = float.IsNaN(floor) ? h.point.y : Mathf.Min(floor, h.point.y);
            }
            if (float.IsNaN(floor)) floor = probe.y;
            floorY = floor;

            // 좌우 벽: 여러 높이에서 쏴서 가장 먼 벽 (낮은 장애물·발판에 속지 않도록)
            right = FindWall(probe.x, +1, out bool foundRight);
            left = FindWall(probe.x, -1, out bool foundLeft);
            if (!foundRight) right = probe.x + fallbackHalfWidth;
            if (!foundLeft) left = probe.x - fallbackHalfWidth;

            // 천장
            float ceiling = floorY + fallbackHeight;
            var up = Physics2D.RaycastAll(new Vector2(probe.x, floorY + 0.5f), Vector2.up, fallbackHeight * 2f, wallMask);
            foreach (var h in up)
            {
                if (!IsStatic(h.collider)) continue;
                if (h.point.y < floorY + 4f) continue; // 낮은 발판은 천장이 아니다
                ceiling = h.point.y;
                break;
            }
            ceilingY = ceiling;
            IsFitted = true;
        }

        float FindWall(float x, int dir, out bool found)
        {
            found = false;
            float best = x;
            for (int i = 0; i < 3; i++)
            {
                var origin = new Vector2(x, floorY + 3f + i * 1.5f);
                foreach (var h in Physics2D.RaycastAll(origin, Vector2.right * dir, 80f, wallMask))
                {
                    if (!IsStatic(h.collider)) continue;
                    if (h.distance <= 0.01f) continue; // 시작점이 콜라이더 안 (발판 속)
                    float hx = h.point.x;
                    if (!found || (hx - best) * dir > 0f) best = hx;
                    found = true;
                    break;
                }
            }
            return best;
        }

        bool IsStatic(Collider2D c)
        {
            if (c == null || c.isTrigger) return false;
            if (c.transform.IsChildOf(transform)) return false;          // 보스 자신
            var rb = c.attachedRigidbody;
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) return false; // 플레이어 등 움직이는 것
            return true;
        }

        // ───────────────────────────── 질의 ─────────────────────────────

        /// <summary>x 위치에서 fromY 아래로 처음 닿는 면 (발판 포함). 없으면 최하단 바닥.</summary>
        public float SurfaceBelow(float x, float fromY)
        {
            EnsureFitted();
            float dist = Mathf.Max(0.1f, fromY - floorY + 0.5f);
            foreach (var h in Physics2D.RaycastAll(new Vector2(x, fromY), Vector2.down, dist, groundMask))
            {
                if (!IsStatic(h.collider)) continue;
                if (h.distance <= 0.01f) continue;
                return h.point.y;
            }
            return floorY;
        }

        /// <summary>그 x가 발판 없이 최하단 바닥에 바로 닿는지</summary>
        public bool IsFloorExposed(float x)
        {
            return SurfaceBelow(x, ceilingY - 0.1f) <= floorY + 0.05f;
        }

        public float ClampX(float x, float margin) => Mathf.Clamp(x, left + margin, right - margin);

        /// <summary>지금 카메라에 보이는 가로 범위 (직교 카메라만)</summary>
        public bool TryGetViewX(out float min, out float max)
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                min = max = 0f;
                return false;
            }
            float half = cam.orthographicSize * cam.aspect;
            min = cam.transform.position.x - half;
            max = cam.transform.position.x + half;
            return true;
        }

        /// <summary>
        /// 순간이동할 체공 좌표를 고른다 (기획서 3.1: 간격 재조정, 화면 안).
        /// 직접 정한 좌표가 있으면 그중에서, 없으면 플레이어 좌우 min~max 거리에서 고른다.
        /// </summary>
        public Vector2 PickTeleportPoint(Vector2 current, Vector2 target, System.Random rng)
        {
            EnsureFitted();

            if (teleportPoints != null && teleportPoints.Length > 0)
            {
                Vector2? fallback = null;
                int start = rng.Next(teleportPoints.Length);
                for (int k = 0; k < teleportPoints.Length; k++)
                {
                    var t = teleportPoints[(start + k) % teleportPoints.Length];
                    if (t == null) continue;
                    Vector2 p = t.position;
                    if (fallback == null) fallback = p;
                    if (Vector2.Distance(p, current) < 1f) continue;
                    if (Mathf.Abs(p.x - target.x) < teleportMinDistance) continue;
                    return p;
                }
                if (fallback != null) return fallback.Value;
            }

            float minX = left + edgeMargin, maxX = right - edgeMargin;
            if (keepInCameraView && TryGetViewX(out float vMin, out float vMax))
            {
                float a = Mathf.Max(minX, vMin + edgeMargin), b = Mathf.Min(maxX, vMax - edgeMargin);
                if (b - a > 2f) { minX = a; maxX = b; }
            }

            for (int i = 0; i < 16; i++)
            {
                int side = rng.NextDouble() < 0.5 ? -1 : 1;
                float dist = Mathf.Lerp(teleportMinDistance, teleportMaxDistance, (float)rng.NextDouble());
                float x = Mathf.Clamp(target.x + side * dist, minX, maxX);
                if (Mathf.Abs(x - target.x) < teleportMinDistance * 0.75f) continue;
                if (Mathf.Abs(x - current.x) < 1.5f) continue;
                return new Vector2(x, HoverY(x, rng));
            }

            // 마땅한 곳이 없으면 플레이어 기준 반대편
            float fx = Mathf.Clamp(target.x + (current.x < target.x ? 1 : -1) * teleportMinDistance, minX, maxX);
            return new Vector2(fx, HoverY(fx, rng));
        }

        /// <summary>그 x에서 떠 있을 높이 (발밑 면 + hoverHeight, 가끔 더 높이)</summary>
        public float HoverY(float x, System.Random rng = null)
        {
            float extra = rng != null && rng.NextDouble() < highHoverChance ? highHoverExtra : 0f;
            float y = SurfaceBelow(x, ceilingY - 0.2f) + hoverHeight + extra;
            return Mathf.Min(y, ceilingY - 2.6f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
            var b = Bounds;
            Gizmos.DrawWireCube(b.center, b.size);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawLine(new Vector3(left, floorY + 1.2f), new Vector3(right, floorY + 1.2f)); // 영벌의 업화 높이
            Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
            Gizmos.DrawLine(new Vector3(CenterX, floorY), new Vector3(CenterX, ceilingY));     // 맵 중앙 (결계벽)
            if (teleportPoints != null)
                foreach (var t in teleportPoints)
                    if (t != null) Gizmos.DrawWireSphere(t.position, 0.3f);
        }
    }
}
