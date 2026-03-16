using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;

public class PlayerController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 12f;

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

    [Header("Player")]
    [SerializeField] private AnimationManager animationManager;

    [Header("KnockBack")]
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Hit")]
    [SerializeField] private float hitCooldown = 1f;

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCol;
    private float defaultGravityScale;

    private float moveInput;
    private float verticalInput;

    private bool isGrounded;
    private bool isClimbing;
    private bool blockTopEnter;

    private Collider2D currentGroundCollider;
    private Collider2D currentLadderCollider;
    private Collider2D currentLadderTopCollider;

    private Coroutine ignoreGroundRoutine;
    private Coroutine topBlockRoutine;

    private bool isHitCooldown;
    private SpriteRenderer[] renderers;

    private int playerLayer;
    private int enemyLayer;

    private enum FacingDir { Left, Right, Back, Front }

    private Transform tLeft;
    private Transform tRight;
    private Transform tFront;
    private Transform tBack;
    private FacingDir currentDir = FacingDir.Right;

    private bool isKnockback;
    /// <summary>
    /// 컴포넌트/기본값 캐싱.
    /// - Rigidbody2D / CapsuleCollider2D 캐싱
    /// - 기본 중력값 저장(사다리 중력 0 처리 후 복구용)
    /// - 방향 오브젝트(Left/Right/Front/Back) 캐싱 및 초기 적용
    /// - AnimationManager 자동 연결(없을 때만)
    /// </summary>
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCol = GetComponent<CapsuleCollider2D>();
        defaultGravityScale = rb.gravityScale;

        CacheDirectionTransforms();
        ApplyDirection();

        if (animationManager == null)
            animationManager = GetComponent<AnimationManager>();

        renderers = GetComponentsInChildren<SpriteRenderer>(true);

        playerLayer = LayerMask.NameToLayer("Player");
        enemyLayer = LayerMask.NameToLayer("Enemey");

    }

    /// <summary>
    /// 입력/감지 기반 상태 처리.
    /// - 바닥 체크, 사다리 접촉 갱신
    /// - 위에서 내려가기/몸통으로 올라가기 진입 처리
    /// - 꼭대기 탈출 처리
    /// - 일반 상태일 때만 방향/이동 애니 갱신
    /// - 점프(일반/사다리 탈출) 처리
    /// </summary>
    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        CheckGround();
        RefreshLadderContacts();

        HandleLadderEnterFromTop();
        HandleLadderEnterFromBody();
        HandleLadderTopExit();

        if (!isClimbing)
        {
            if (moveInput > 0.01f)
                SetFacing(true);
            else if (moveInput < -0.01f)
                SetFacing(false);

            UpdateAnimationState(moveInput);
        }

        HandleJump();
    }

    /// <summary>
    /// 물리 이동 처리.
    /// - 사다리 상태면 등반 이동
    /// - 일반 상태면 수평 이동
    /// </summary>
    private void FixedUpdate()
    {
        if (isClimbing)
        {
            HandleClimbMove();
        }
        else
        {
            HandleNormalMove();
        }
    }

    /// <summary>
    /// 사다리 상태 등반 이동.
    /// - 사다리 중심으로 x를 보간 정렬
    /// - y 입력으로 등반 속도 적용
    /// - 사다리 범위를 벗어나면 등반 종료
    /// - 꼭대기 근처에서 발 위치를 제한(클램프)
    /// </summary>
    private void HandleClimbMove()
    {
        if (currentLadderCollider == null)
        {
            StopClimbing();
            return;
        }

        SetDirection(FacingDir.Back);

        if (animationManager != null)
        {
            if (Mathf.Abs(verticalInput) > 0.01f)
                animationManager.SetState(CharacterState.Run);
            else
                animationManager.SetState(CharacterState.Idle);
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
    /// 점프 처리.
    /// - 사다리 중 점프: 등반 종료 후 점프
    /// - 일반 점프: 바닥 상태일 때만 점프
    /// </summary>
    private void HandleJump()
    {
        if (!Input.GetButtonDown("Jump"))
            return;

        if (isClimbing)
        {
            StopClimbing();
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
            return;
        }

        if (isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
        }
    }

    /// <summary>
    /// 몸통 구간에서 사다리 진입(아래→위로 올라가기).
    /// - 사다리 접촉 상태 + 위 입력일 때만 진입
    /// </summary>
    private void HandleLadderEnterFromBody()
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
    /// 꼭대기에서 사다리 진입(위→아래로 내려가기).
    /// - 꼭대기 감지 + 아래 입력일 때만 진입
    /// - 사다리 중심과의 x 오차가 허용 범위 이내일 때만 진입
    /// </summary>
    private void HandleLadderEnterFromTop()
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
    /// 사다리 꼭대기 탈출(올라가기).
    /// - 사다리 상태 + 꼭대기 감지 + 위 입력 조건에서
    /// - 발 위치가 일정 임계치 이상이면 꼭대기 위치로 스냅 후 등반 종료
    /// </summary>
    private void HandleLadderTopExit()
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
    /// 등반 시작 공통 처리.
    /// - 중력 제거, 속도 초기화
    /// - 사다리 중심으로 x 정렬
    /// - fromTop이면 상단 진입 높이 보정 + 잠시 바닥 충돌 무시
    /// </summary>
    private void StartClimbing(bool fromTop)
    {
        if (currentLadderCollider == null)
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
    /// - 중력값 복구
    /// </summary>
    private void StopClimbing()
    {
        isClimbing = false;
        rb.gravityScale = defaultGravityScale;
    }

    /// <summary>
    /// 꼭대기 탈출 위치 보정.
    /// - 발 기준으로 사다리 상단보다 살짝 위로 스냅
    /// - 재진입 방지 블록 코루틴 시작
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
        rb.velocity = Vector2.zero;

        if (topBlockRoutine != null)
            StopCoroutine(topBlockRoutine);

        topBlockRoutine = StartCoroutine(BlockTopEnterTemporarily());
    }

    /// <summary>
    /// 사다리/꼭대기 접촉 갱신.
    /// - 플레이어 주변 OverlapBox로 사다리 본체/꼭대기 감지
    /// - 꼭대기만 감지된 경우, 상위 트랜스폼을 타고 올라가 사다리 본체 콜라이더를 찾아 연결
    /// </summary>
    private void RefreshLadderContacts()
    {
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
    /// 꼭대기 콜라이더 기준으로 사다리 본체 콜라이더 찾기.
    /// - 부모 트랜스폼을 타고 올라가면서 ladderMask 레이어의 Collider2D를 탐색
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
    /// 바닥 체크.
    /// - GroundCheck 기준 아래 Raycast로 지면 충돌 여부 갱신
    /// - 현재 밟고 있는 바닥 Collider를 저장(상단 진입 시 충돌 무시 처리에 사용)
    /// </summary>
    private void CheckGround()
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
    /// 바닥(발판) 충돌을 잠시 무시하는 처리.
    /// - 위에서 내려가기 시작할 때 상단 발판에 걸리는 현상을 방지
    /// </summary>
    private IEnumerator IgnoreCollisionTemporarily(Collider2D targetCollider, float duration)
    {
        if (targetCollider == null)
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
    /// 꼭대기 탈출 직후 재진입(아래로 다시 진입)을 잠시 막는 처리.
    /// - ladderTopReenterBlockTime 동안 blockTopEnter 유지
    /// </summary>
    private IEnumerator BlockTopEnterTemporarily()
    {
        blockTopEnter = true;
        yield return new WaitForSeconds(ladderTopReenterBlockTime);
        blockTopEnter = false;
        topBlockRoutine = null;
    }

    /// <summary>
    /// 방향 표시용 자식 오브젝트(Left/Right/Front/Back) 캐싱.
    /// </summary>
    private void CacheDirectionTransforms()
    {
        tLeft = transform.Find("Left");
        tRight = transform.Find("Right");
        tFront = transform.Find("Front");
        tBack = transform.Find("Back");
    }

    /// <summary>
    /// 이동 입력 기반 애니 상태 갱신(일반 상태에서만 호출).
    /// - 수평 입력 있으면 Run, 없으면 Idle
    /// </summary>
    private void UpdateAnimationState(float xInput)
    {
        if (animationManager == null)
            return;

        if (Mathf.Abs(xInput) > 0.01f)
            animationManager.SetState(CharacterState.Run);
        else
            animationManager.SetState(CharacterState.Idle);
    }

    /// <summary>
    /// 방향 상태 변경 후 표시 적용.
    /// </summary>
    private void SetDirection(FacingDir dir)
    {
        currentDir = dir;
        ApplyDirection();
    }

    /// <summary>
    /// 현재 방향에 맞는 오브젝트만 활성화.
    /// </summary>
    private void ApplyDirection()
    {
        if (tLeft != null) tLeft.gameObject.SetActive(currentDir == FacingDir.Left);
        if (tRight != null) tRight.gameObject.SetActive(currentDir == FacingDir.Right);
        if (tFront != null) tFront.gameObject.SetActive(currentDir == FacingDir.Front);
        if (tBack != null) tBack.gameObject.SetActive(currentDir == FacingDir.Back);
    }

    /// <summary>
    /// 좌/우 바라보기 전환(일반 상태에서만 사용).
    /// </summary>
    private void SetFacing(bool facingRight)
    {
        SetDirection(facingRight ? FacingDir.Right : FacingDir.Left);
    }

    /// <summary>
    /// 디버그 Gizmo.
    /// - 사다리 감지 OverlapBox 2종(본체/꼭대기)과 GroundCheck Ray를 시각화
    /// </summary>
    private void OnDrawGizmosSelected()
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
    public float GetHorizontalFacingDir()
    {
        if (currentDir == FacingDir.Left)
            return -1f;

        return 1f;
    }

    public void ApplyKnockback(Vector2 force)
    {
        if (isKnockback || isHitCooldown)
            return;

        StopClimbing();
        StartCoroutine(CoKnockback(force));
    }

    private IEnumerator CoKnockback(Vector2 force)
    {
        isKnockback = true;
        isHitCooldown = true;

        SetDamageCooldownVisual(true);
        SetEnemyCollisionEnabled(false);

        rb.velocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(knockbackDuration);
        isKnockback = false;

        yield return new WaitForSeconds(hitCooldown);

        isHitCooldown = false;
        SetDamageCooldownVisual(false);
        SetEnemyCollisionEnabled(true);
    }
    private void HandleNormalMove()
    {
        if (isKnockback)
            return;

        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);
    }

    private void SetDamageCooldownVisual(bool active)
    {
        if (renderers == null)
            return;
        Color targetColor = active
            ? new Color(0.6f, 0.6f, 0.6f, 0.6f)
            : Color.white;

        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != null)
                sr.color = targetColor;
        }
    }

    private void SetEnemyCollisionEnabled(bool enabled)
    {
        Collider2D[] playerCols = GetComponentsInChildren<Collider2D>(true);

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1)
        {
            Debug.LogWarning("Enemy 레이어가 없습니다.");
            return;
        }

        Collider2D[] allCols = Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Collider2D playerCol in playerCols)
        {
            if (playerCol == null)
                continue;

            foreach (Collider2D otherCol in allCols)
            {
                if (otherCol == null)
                    continue;

                if (otherCol.gameObject.layer != enemyLayer)
                    continue;

                Physics2D.IgnoreCollision(playerCol, otherCol, !enabled);
            }
        }
    }
}