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

    //기본값 1 = 원래 속도
    // 1.5 = 50% 증가
    private float moveSpeedMultiplier = 1f;

    /// <summary>
    /// 현제 실제 이동속도,
    /// 기본 이동속도 * 버프 배율
    /// </summary>
    public float CurrentMoveSpeed => moveSpeed * moveSpeedMultiplier;

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

        rb.velocity = new Vector2(moveInput * CurrentMoveSpeed, rb.velocity.y);
    }
    /// <summary>
    /// 이동속도 배율 적용
    /// 1.5 입력시 50% 증가
    /// </summary>
    public void SetMoveSpeedMultiplier(float multiplier)
    {
        moveSpeedMultiplier = Mathf.Max(0f, multiplier);
    }
    /// <summary>
    /// 이동속도를  기본 상태로 복구.
    /// </summary>
    public void ResetmoveSpeedMultiplier()
    {
        moveSpeedMultiplier = 1f;
    }

    // Codex recovery compatibility: boss ice slow uses the existing move speed multiplier.
    public void SetSlowMultiplier(float multiplier)
    {
        SetMoveSpeedMultiplier(multiplier);
    }

    // Codex recovery compatibility: reset boss ice slow back to the default move speed.
    public void ResetSlowMultiplier()
    {
        ResetmoveSpeedMultiplier();
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
