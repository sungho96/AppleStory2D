using System.Collections;
using UnityEngine;

/// <summary>
/// 사다리 전용 처리.
/// - 바닥 체크
/// - 사다리 본체 / 꼭대기 감지
/// - 위에서 아래로 진입
/// - 몸통에서 위로 진입
/// - 등반 이동
/// - 꼭대기 탈출
/// - 발판 충돌 잠시 무시
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
    /// 외부 초기화.
    /// </summary>
    public void Initialize(Rigidbody2D targetRb, CapsuleCollider2D targetCol)
    {
        rb = targetRb;
        playerCol = targetCol;

        if (rb != null)
            defaultGravityScale = rb.gravityScale;
    }

    /// <summary>
    /// 현재 바닥에 닿아있는지 반환.
    /// </summary>
    public bool IsGrounded => isGrounded;

    /// <summary>
    /// 현재 사다리 등반 중인지 반환.
    /// </summary>
    public bool IsClimbing => isClimbing;

    /// <summary>
    /// 현재 밟고 있는 바닥 콜라이더 반환.
    /// </summary>
    public Collider2D CurrentGroundCollider => currentGroundCollider;

    /// <summary>
    /// 바닥 체크.
    /// - GroundCheck 위치에서 아래로 Raycast
    /// - 닿은 Ground Collider 저장
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
    /// 사다리 본체 / 꼭대기 감지 갱신.
    /// - 플레이어 주변 OverlapBox로 탐지
    /// - 꼭대기만 찾은 경우 부모를 타고 올라가 사다리 본체를 찾아 연결
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
    /// 몸통 구간에서 사다리 진입.
    /// - 사다리 본체와 닿아 있고
    /// - 위 입력일 때 진입
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
    /// 위에서 아래로 내려가기 진입.
    /// - 꼭대기 감지 + 아래 입력
    /// - 사다리 중심 x와 플레이어 중심 x 거리 확인
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
    /// 사다리 꼭대기 탈출 처리.
    /// - 등반 중 + 꼭대기 감지 + 위 입력
    /// - 발 위치가 임계값 이상이면 땅 위로 스냅 후 종료
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
    /// 사다리 등반 이동 처리.
    /// - x는 사다리 중심으로 보간 정렬
    /// - y는 입력에 따라 등반
    /// - 범위를 벗어나면 등반 종료
    /// - 꼭대기 위로 발이 지나가지 않도록 제한
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

            rb.velocity = new Vector2(0f, Mathf.Min(0f, verticalInput * climbSpeed));
            return;
        }

        transform.position = pos;

        float climbY = verticalInput * climbSpeed;
        rb.velocity = new Vector2(0f, climbY);
    }

    /// <summary>
    /// 등반 시작 공통 처리.
    /// - 중력 제거
    /// - 속도 초기화
    /// - 사다리 중심 x 정렬
    /// - 위에서 진입 시 발 위치를 살짝 아래로 보정
    /// - 위 발판 충돌을 잠시 무시
    /// </summary>
    private void StartClimbing(bool fromTop)
    {
        if (currentLadderCollider == null)
            return;

        if (rb == null || playerCol == null)
            return;

        isClimbing = true;
        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;

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

            rb.velocity = new Vector2(0f, -climbSpeed);
            return;
        }

        transform.position = pos;
    }

    /// <summary>
    /// 등반 종료 처리.
    /// - 중력 복구
    /// </summary>
    public void StopClimbing()
    {
        isClimbing = false;

        if (rb != null)
            rb.gravityScale = defaultGravityScale;
    }

    /// <summary>
    /// 꼭대기 탈출 위치 보정.
    /// - 발 기준으로 사다리 상단보다 조금 위에 위치시킴
    /// - 탈출 직후 재진입 방지 코루틴 실행
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
            rb.velocity = Vector2.zero;

        if (topBlockRoutine != null)
            StopCoroutine(topBlockRoutine);

        topBlockRoutine = StartCoroutine(BlockTopEnterTemporarily());
    }

    /// <summary>
    /// 꼭대기 콜라이더 기준으로 부모를 타고 올라가
    /// 사다리 본체 Collider2D를 찾는 처리.
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
    /// 발판 충돌 잠시 무시.
    /// - 위에서 내려가기 시작 시 상단 발판에 걸리는 현상 방지
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
    /// 꼭대기 탈출 직후 일정 시간 재진입 방지.
    /// </summary>
    private IEnumerator BlockTopEnterTemporarily()
    {
        blockTopEnter = true;
        yield return new WaitForSeconds(ladderTopReenterBlockTime);
        blockTopEnter = false;
        topBlockRoutine = null;
    }

    /// <summary>
    /// 디버그 Gizmo 표시 함수.
    /// - Controller의 OnDrawGizmosSelected에서 호출
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