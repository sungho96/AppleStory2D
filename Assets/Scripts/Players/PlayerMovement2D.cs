using UnityEngine;

/// <summary>
/// 일반 이동 / 점프 전용.
/// - 좌우 이동
/// - 점프
/// 
/// 사다리 / 피격 무적 / 방향 전환은 여기서 담당하지 않습니다.
/// </summary>
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 12f;

    private Rigidbody2D rb;

    /// <summary>
    /// 외부에서 Rigidbody2D 전달받아 초기화.
    /// </summary>
    public void Initialize(Rigidbody2D targetRb)
    {
        rb = targetRb;
    }

    /// <summary>
    /// 일반 수평 이동 처리.
    /// - 넉백 등으로 이동이 막힌 상태면 처리하지 않음
    /// </summary>
    public void HandleNormalMove(float moveInput, bool blockMove)
    {
        if (rb == null)
            return;

        if (blockMove)
            return;

        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);
    }

    /// <summary>
    /// 점프 처리.
    /// - 현재 x 속도는 유지하고 y만 점프 속도로 변경
    /// </summary>
    public void Jump()
    {
        if (rb == null)
            return;

        rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
    }
}