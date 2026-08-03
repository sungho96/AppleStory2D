using UnityEngine;

/// <summary>
/// �Ϲ� �̵� / ���� ����.
/// - �¿� �̵�
/// - ����
/// 
/// ��ٸ� / �ǰ� ���� / ���� ��ȯ�� ���⼭ ������� �ʽ��ϴ�.
/// </summary>
public class PlayerMovement2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 12f;

    private Rigidbody2D rb;

    //�⺻�� 1 = ���� �ӵ�
    // 1.5 = 50% ����
    private float moveSpeedMultiplier = 1f;

    /// <summary>
    /// ���� ���� �̵��ӵ�,
    /// �⺻ �̵��ӵ� * ���� ����
    /// </summary>
    public float CurrentMoveSpeed => moveSpeed * moveSpeedMultiplier;

    /// <summary>
    /// �ܺο��� Rigidbody2D ���޹޾� �ʱ�ȭ.
    /// </summary>
    public void Initialize(Rigidbody2D targetRb)
    {
        rb = targetRb;
    }

    /// <summary>
    /// �Ϲ� ���� �̵� ó��.
    /// - �˹� ������ �̵��� ���� ���¸� ó������ ����
    /// </summary>
    public void HandleNormalMove(float moveInput, bool blockMove)
    {
        if (rb == null)
            return;

        if (blockMove)
            return;

        rb.linearVelocity = new Vector2(moveInput * CurrentMoveSpeed, rb.linearVelocity.y);
    }
    /// <summary>
    /// �̵��ӵ� ���� ����
    /// 1.5 �Է½� 50% ����
    /// </summary>
    public void SetMoveSpeedMultiplier(float multiplier)
    {
        moveSpeedMultiplier = Mathf.Max(0f, multiplier);
    }
    /// <summary>
    /// �̵��ӵ���  �⺻ ���·� ����.
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
    /// ���� ó��.
    /// - ���� x �ӵ��� �����ϰ� y�� ���� �ӵ��� ����
    /// </summary>
    public void Jump()
    {
        if (rb == null)
            return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
    }
}
