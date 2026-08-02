using System.Collections;
using UnityEngine;

/// <summary>
/// ��ٸ� ���� ó��.
/// - �ٴ� üũ
/// - ��ٸ� ��ü / ����� ����
/// - ������ �Ʒ��� ����
/// - ���뿡�� ���� ����
/// - ��� �̵�
/// - ����� Ż��
/// - ���� �浹 ��� ����
/// </summary>
public class PlayerLadder2D : MonoBehaviour
{
    [Header("Ladder")]
    [SerializeField] private float climbSpeed = 4f;
    [SerializeField] private float ladderAlignSpeed = 20f;
    [SerializeField] private float ladderTopEnterInset = 0.65f;
    [SerializeField] private float ladderTopExitOffset = 0.08f;
    [SerializeField] private float ladderCenterEnterTolerance = 1.0f;
    [SerializeField] private float ladderPlatformIgnoreTime = 0.6f;
    [SerializeField] private float ladderTopReenterBlockTime = 0.15f;

    [Header("Detection")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask ladderMask;
    [SerializeField] private LayerMask ladderTopMask;
    [SerializeField] private Vector2 ladderCheckBoxSize = new Vector2(0.8f, 1.8f);
    [SerializeField] private Vector2 ladderTopCheckBoxSize = new Vector2(1.2f, 0.5f);
    [SerializeField] private float ladderTopCheckYOffset = 0.1f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.15f;

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCol;
    private float defaultGravityScale;

    private bool isGrounded;
    private bool isClimbing;
    private bool blockTopEnter;

    private Collider2D currentGroundCollider;
    private Collider2D currentLadderCollider;
    private Collider2D currentLadderTopCollider;

    private Coroutine ignoreGroundRoutine;
    private Coroutine topBlockRoutine;

    /// <summary>
    /// �ܺ� �ʱ�ȭ.
    /// </summary>
    public void Initialize(Rigidbody2D targetRb, CapsuleCollider2D targetCol)
    {
        rb = targetRb;
        playerCol = targetCol;

        if (rb != null)
            defaultGravityScale = rb.gravityScale;
    }

    /// <summary>
    /// ���� �ٴڿ� ����ִ��� ��ȯ.
    /// </summary>
    public bool IsGrounded => isGrounded;

    /// <summary>
    /// ���� ��ٸ� ��� ������ ��ȯ.
    /// </summary>
    public bool IsClimbing => isClimbing;

    /// <summary>
    /// ���� ��� �ִ� �ٴ� �ݶ��̴� ��ȯ.
    /// </summary>
    public Collider2D CurrentGroundCollider => currentGroundCollider;

    /// <summary>
    /// �ٴ� üũ.
    /// - GroundCheck ��ġ���� �Ʒ��� Raycast
    /// - ���� Ground Collider ����
    /// </summary>
    public void CheckGround()
    {
        currentGroundCollider = null;

        if (groundCheck == null)
        {
            isGrounded = false;
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            groundMask
        );

        isGrounded = hit.collider != null;
        currentGroundCollider = hit.collider;
    }

    /// <summary>
    /// ��ٸ� ��ü / ����� ���� ����.
    /// - �÷��̾� �ֺ� OverlapBox�� Ž��
    /// - ����⸸ ã�� ��� �θ� Ÿ�� �ö� ��ٸ� ��ü�� ã�� ����
    /// </summary>
    public void RefreshLadderContacts()
    {
        if (playerCol == null)
            return;

        Bounds playerBounds = playerCol.bounds;

        Vector2 ladderCheckCenter = playerBounds.center;
        Collider2D foundLadder = Physics2D.OverlapBox(
            ladderCheckCenter,
            ladderCheckBoxSize,
            0f,
            ladderMask
        );

        Vector2 topCheckCenter = new Vector2(
            playerBounds.center.x,
            playerBounds.min.y + ladderTopCheckYOffset
        );

        Collider2D foundTop = Physics2D.OverlapBox(
            topCheckCenter,
            ladderTopCheckBoxSize,
            0f,
            ladderTopMask
        );

        if (foundLadder != null)
        {
            currentLadderCollider = foundLadder;
        }
        else
        {
            if (!isClimbing)
                currentLadderCollider = null;
        }

        if (foundTop != null)
        {
            currentLadderTopCollider = foundTop;
        }
        else
        {
            if (!isClimbing)
                currentLadderTopCollider = null;
        }

        if (currentLadderTopCollider != null && currentLadderCollider == null)
        {
            ResolveLadderFromTop();
        }
    }

    /// <summary>
    /// ���� �������� ��ٸ� ����.
    /// - ��ٸ� ��ü�� ��� �ְ�
    /// - �� �Է��� �� ����
    /// </summary>
    public void TryEnterFromBody(float verticalInput)
    {
        if (isClimbing)
            return;

        if (currentLadderCollider == null)
            return;

        if (verticalInput <= 0.01f)
            return;

        StartClimbing(false);
    }

    /// <summary>
    /// ������ �Ʒ��� �������� ����.
    /// - ����� ���� + �Ʒ� �Է�
    /// - ��ٸ� �߽� x�� �÷��̾� �߽� x �Ÿ� Ȯ��
    /// </summary>
    public void TryEnterFromTop(float verticalInput)
    {
        if (isClimbing)
            return;

        if (blockTopEnter)
            return;

        if (currentLadderTopCollider == null)
            return;

        if (verticalInput >= -0.01f)
            return;

        ResolveLadderFromTop();

        if (currentLadderCollider == null)
            return;

        float ladderCenterX = currentLadderCollider.bounds.center.x;
        float playerCenterX = playerCol.bounds.center.x;
        float distanceToCenter = Mathf.Abs(playerCenterX - ladderCenterX);

        if (distanceToCenter > ladderCenterEnterTolerance)
            return;

        StartClimbing(true);
    }

    /// <summary>
    /// ��ٸ� ����� Ż�� ó��.
    /// - ��� �� + ����� ���� + �� �Է�
    /// - �� ��ġ�� �Ӱ谪 �̻��̸� �� ���� ���� �� ����
    /// </summary>
    public void TryExitToTop(float verticalInput)
    {
        if (!isClimbing)
            return;

        if (currentLadderCollider == null)
            return;

        if (currentLadderTopCollider == null)
            return;

        if (verticalInput <= 0.01f)
            return;

        Bounds ladderBounds = currentLadderCollider.bounds;

        float feetOffsetFromTransform = playerCol.bounds.min.y - transform.position.y;
        float currentFeetY = transform.position.y + feetOffsetFromTransform;

        float topExitThresholdY = ladderBounds.max.y - 0.15f;

        if (currentFeetY < topExitThresholdY)
            return;

        ExitLadderToTopGround();
    }

    /// <summary>
    /// ��ٸ� ��� �̵� ó��.
    /// - x�� ��ٸ� �߽����� ���� ����
    /// - y�� �Է¿� ���� ���
    /// - ������ ����� ��� ����
    /// - ����� ���� ���� �������� �ʵ��� ����
    /// </summary>
    public void HandleClimbMove(float verticalInput)
    {
        if (!isClimbing)
            return;

        if (currentLadderCollider == null)
        {
            StopClimbing();
            return;
        }

        Bounds ladderBounds = currentLadderCollider.bounds;
        Bounds playerBounds = playerCol.bounds;

        bool outOfLadderX = Mathf.Abs(playerBounds.center.x - ladderBounds.center.x) > (ladderBounds.extents.x + 0.8f);
        bool outOfLadderY = playerBounds.max.y < ladderBounds.min.y - 0.5f || playerBounds.min.y > ladderBounds.max.y + 0.5f;

        if (outOfLadderX || outOfLadderY)
        {
            StopClimbing();
            return;
        }

        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, ladderBounds.center.x, ladderAlignSpeed * Time.fixedDeltaTime);

        float feetOffsetFromTransform = playerCol.bounds.min.y - transform.position.y;
        float currentFeetY = transform.position.y + feetOffsetFromTransform;
        float maxFeetY = ladderBounds.max.y - 0.02f;

        if (currentFeetY > maxFeetY)
        {
            pos.y = maxFeetY - feetOffsetFromTransform;
            transform.position = pos;

            rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, verticalInput * climbSpeed));
            return;
        }

        transform.position = pos;

        float climbY = verticalInput * climbSpeed;
        rb.linearVelocity = new Vector2(0f, climbY);
    }

    /// <summary>
    /// ��� ���� ���� ó��.
    /// - �߷� ����
    /// - �ӵ� �ʱ�ȭ
    /// - ��ٸ� �߽� x ����
    /// - ������ ���� �� �� ��ġ�� ��¦ �Ʒ��� ����
    /// - �� ���� �浹�� ��� ����
    /// </summary>
    private void StartClimbing(bool fromTop)
    {
        if (currentLadderCollider == null)
            return;

        if (rb == null || playerCol == null)
            return;

        isClimbing = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        Bounds ladderBounds = currentLadderCollider.bounds;

        Vector3 pos = transform.position;
        pos.x = ladderBounds.center.x;

        if (fromTop)
        {
            float feetOffsetFromTransform = playerCol.bounds.min.y - transform.position.y;
            float targetFeetY = ladderBounds.max.y - ladderTopEnterInset;
            pos.y = targetFeetY - feetOffsetFromTransform;

            transform.position = pos;

            if (currentGroundCollider != null)
            {
                if (ignoreGroundRoutine != null)
                    StopCoroutine(ignoreGroundRoutine);

                ignoreGroundRoutine = StartCoroutine(
                    IgnoreCollisionTemporarily(currentGroundCollider, ladderPlatformIgnoreTime)
                );
            }

            rb.linearVelocity = new Vector2(0f, -climbSpeed);
            return;
        }

        transform.position = pos;
    }

    /// <summary>
    /// ��� ���� ó��.
    /// - �߷� ����
    /// </summary>
    public void StopClimbing()
    {
        isClimbing = false;

        if (rb != null)
            rb.gravityScale = defaultGravityScale;
    }

    /// <summary>
    /// ����� Ż�� ��ġ ����.
    /// - �� �������� ��ٸ� ��ܺ��� ���� ���� ��ġ��Ŵ
    /// - Ż�� ���� ������ ���� �ڷ�ƾ ����
    /// </summary>
    private void ExitLadderToTopGround()
    {
        Bounds ladderBounds = currentLadderCollider.bounds;

        float feetOffsetFromTransform = playerCol.bounds.min.y - transform.position.y;

        Vector3 pos = transform.position;
        pos.x = ladderBounds.center.x;

        float targetFeetY = ladderBounds.max.y + ladderTopExitOffset;
        pos.y = targetFeetY - feetOffsetFromTransform;

        transform.position = pos;

        StopClimbing();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (topBlockRoutine != null)
            StopCoroutine(topBlockRoutine);

        topBlockRoutine = StartCoroutine(BlockTopEnterTemporarily());
    }

    /// <summary>
    /// ����� �ݶ��̴� �������� �θ� Ÿ�� �ö�
    /// ��ٸ� ��ü Collider2D�� ã�� ó��.
    /// </summary>
    private void ResolveLadderFromTop()
    {
        if (currentLadderTopCollider == null)
            return;

        Transform t = currentLadderTopCollider.transform;

        while (t != null)
        {
            if (((1 << t.gameObject.layer) & ladderMask) != 0)
            {
                Collider2D col = t.GetComponent<Collider2D>();
                if (col != null)
                {
                    currentLadderCollider = col;
                    return;
                }
            }

            t = t.parent;
        }
    }

    /// <summary>
    /// ���� �浹 ��� ����.
    /// - ������ �������� ���� �� ��� ���ǿ� �ɸ��� ���� ����
    /// </summary>
    private IEnumerator IgnoreCollisionTemporarily(Collider2D targetCollider, float duration)
    {
        if (targetCollider == null || playerCol == null)
            yield break;

        Physics2D.IgnoreCollision(playerCol, targetCollider, true);

        yield return new WaitForSeconds(duration);

        if (playerCol != null && targetCollider != null)
        {
            Physics2D.IgnoreCollision(playerCol, targetCollider, false);
        }

        ignoreGroundRoutine = null;
    }

    /// <summary>
    /// ����� Ż�� ���� ���� �ð� ������ ����.
    /// </summary>
    private IEnumerator BlockTopEnterTemporarily()
    {
        blockTopEnter = true;
        yield return new WaitForSeconds(ladderTopReenterBlockTime);
        blockTopEnter = false;
        topBlockRoutine = null;
    }

    /// <summary>
    /// ����� Gizmo ǥ�� �Լ�.
    /// - Controller�� OnDrawGizmosSelected���� ȣ��
    /// </summary>
    public void DrawDebugGizmos()
    {
        CapsuleCollider2D col = GetComponent<CapsuleCollider2D>();
        if (col != null)
        {
            Bounds b = col.bounds;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(b.center, ladderCheckBoxSize);

            Vector3 topCenter = new Vector3(
                b.center.x,
                b.min.y + ladderTopCheckYOffset,
                0f
            );

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(topCenter, ladderTopCheckBoxSize);
        }

        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Vector3 start = groundCheck.position;
            Vector3 end = groundCheck.position + Vector3.down * groundCheckDistance;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.03f);
        }
    }
}