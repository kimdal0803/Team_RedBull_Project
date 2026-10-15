using System.Collections.Generic;
using ReversalTower.Boss;   // FSM 보스(BossController)를 때리기 위해 추가
using UnityEngine;

public class PlayerAttackTrigger : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    private Collider2D _col;
    private readonly HashSet<Collider2D> _hitList = new HashSet<Collider2D>();

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        if (player == null)
        {
            player = GetComponentInParent<PlayerMovement>();
        }
    }

    void OnEnable()
    {
        _hitList.Clear();
        // 켜지는 순간 물리 프레임을 기다리지 않고 즉시 범위 내 적 검사
        CheckDirectOverlap();
    }

    void OnDisable()
    {
        _hitList.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        // 이번 1회 공격 동안 이미 때린 콜라이더는 스킵
        if (_hitList.Contains(other)) return;

        // 플레이어 본인 콜라이더 제외
        if (player != null && other.transform.IsChildOf(player.transform)) return;

        int dmg = player != null ? player.GetDamage() : 1;
        Vector2 myPos = transform.position;

        // 1. 보스 타격
        DummyBoss boss = other.GetComponent<DummyBoss>();
        if (boss != null)
        {
            _hitList.Add(other);
            boss.TakeDamage(dmg);
            return;
        }

        // 1-2. FSM 보스(아라쿠스 등) 타격
        //      콜라이더가 자식 오브젝트에 있을 수 있어 부모까지 거슬러 찾는다.
        //      TakeDamage의 두 번째 인자는 '원인'으로, 전투 기록의 skill 칸에 남는다.
        BossController fsmBoss = other.GetComponentInParent<BossController>();
        if (fsmBoss != null)
        {
            _hitList.Add(other);
            fsmBoss.TakeDamage(dmg, "플레이어 공격");
            return;
        }

        // 2. 일반 적 타격
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            _hitList.Add(other);
            enemy.TakeDamage(dmg);
            return;
        }
    }

    private void CheckDirectOverlap()
    {
        if (_col == null) return;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.SetLayerMask(Physics2D.AllLayers);

        Collider2D[] results = new Collider2D[10];
        int count = _col.Overlap(filter, results);

        for (int i = 0; i < count; i++)
        {
            if (results[i] != null && results[i] != _col)
            {
                TryHit(results[i]);
            }
        }
    }
}
