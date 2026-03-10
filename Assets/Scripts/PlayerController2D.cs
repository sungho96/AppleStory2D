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

    private enum FacingDir { Left, Right, Back, Front }

    private Transform tLeft;
    private Transform tRight;
    private Transform tFront;
    private Transform tBack;
    private FacingDir currentDir = FacingDir.Right;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCol = GetComponent<CapsuleCollider2D>();
        defaultGravityScale = rb.gravityScale;

        CacheDirectionTransforms();
        ApplyDirection();

        if (animationManager == null)
            animationManager = GetComponent<AnimationManager>();
    }

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

    private void HandleNormalMove()
    {
        rb.velocity = new Vector2(moveInput * moveSpeed, rb.velocity.y);
    }

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

    private void HandleLadderEnterFromBody()
    {
        if (isClimbing)
            return;

        if (currentLadderCollider == null)
            return;

        // 아래에서 위로 올라갈 때만 Body 진입 허용
        if (verticalInput <= 0.01f)
            return;

        StartClimbing(false);
    }

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

    private void StopClimbing()
    {
        isClimbing = false;
        rb.gravityScale = defaultGravityScale;
    }

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

    private IEnumerator BlockTopEnterTemporarily()
    {
        blockTopEnter = true;
        yield return new WaitForSeconds(ladderTopReenterBlockTime);
        blockTopEnter = false;
        topBlockRoutine = null;
    }

    private void CacheDirectionTransforms()
    {
        tLeft = transform.Find("Left");
        tRight = transform.Find("Right");
        tFront = transform.Find("Front");
        tBack = transform.Find("Back");
    }

    private void UpdateAnimationState(float xInput)
    {
        if (animationManager == null)
            return;

        if (Mathf.Abs(xInput) > 0.01f)
            animationManager.SetState(CharacterState.Run);
        else
            animationManager.SetState(CharacterState.Idle);
    }

    private void SetDirection(FacingDir dir)
    {
        currentDir = dir;
        ApplyDirection();
    }

    private void ApplyDirection()
    {
        if (tLeft != null) tLeft.gameObject.SetActive(currentDir == FacingDir.Left);
        if (tRight != null) tRight.gameObject.SetActive(currentDir == FacingDir.Right);
        if (tFront != null) tFront.gameObject.SetActive(currentDir == FacingDir.Front);
        if (tBack != null) tBack.gameObject.SetActive(currentDir == FacingDir.Back);
    }

    private void SetFacing(bool facingRight)
    {
        SetDirection(facingRight ? FacingDir.Right : FacingDir.Left);
    }

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
}