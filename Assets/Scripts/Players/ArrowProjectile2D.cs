using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowProjectile2D : MonoBehaviour
{
    [SerializeField] private int damage = 10;     // 화살이 가하는 데미지 값
    [SerializeField] private float lifeTime = 3f; // 일정 시간 후 자동 제거 (메모리 관리용)

    private float moveDir; // 발사 방향 (1: 오른쪽 / -1: 왼쪽)

    /// <summary>
    /// 초기 실행.
    /// - 화살이 일정 시간 후 자동으로 삭제되도록 설정
    /// - 씬에 남아있는 발사체 누적 방지 (성능 관리)
    /// </summary>
    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 충돌 처리 (Trigger 기반).
    /// - 충돌한 대상에서 GoblinHealth2D를 찾아 데미지 전달
    /// - 부모까지 탐색하여 콜라이더 구조에 유연하게 대응
    /// - 적과 충돌 시 즉시 화살 제거
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 부모까지 포함하여 적 체력 컴포넌트 탐색
        GoblinHealth2D enemyHealth = other.GetComponentInParent<GoblinHealth2D>();

        if (enemyHealth != null)
        {
            Debug.Log($"화살 충돌 대상 : {other.name}");

            // 데미지 + 방향 전달 (넉백 등에서 사용 가능)
            enemyHealth.TakeDamage(damage, moveDir);

            // 적 명중 시 화살 제거
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 발사 방향 설정.
    /// - PlayerAttack2D에서 화살 생성 직후 호출됨
    /// - 방향 값은 넉백 방향, 히트 효과 등에 활용됨
    /// </summary>
    public void SetDirection(float dir)
    {
        moveDir = dir;
    }
}