using System.Collections;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;

public class PlayerController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 12f;

    [Header("Climb")]
    [SerializeField] private float climbSpeed = 4f;
    [SerializeField] private LayerMask ladderMask;
    [SerializeField] private float ladderTopSnapExtra = 0.15f;
    [SerializeField] private float ladderGrabCooldown = 0.05f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private LayerMask groundMask;

    [Header("Player")]
    [SerializeField] private AnimationManager animationManager;

    private Rigidbody2D rb;
    private bool canClimb;             // 사다리 트리거 안에 들어왔는지
    private bool isOnLadder;           // 현재 사다리 상태인지
    private float defaultGravity;      // 사다리 중력 0 처리 후 복구용
    private float lastLadderGrabTime;  // 사다리 재진입 쿨다운용 시간

    private enum FacingDir { Left, Right, Back, Front }

    private Transform tLeft, tRight, tFront, tBack;
    private FacingDir currentDir = FacingDir.Right;

    private Collider2D bodyCol;

    /// <summary>
    /// 컴포넌트/참조 초기 캐싱.
    /// - Rigidbody2D/기본 중력값 캐싱
    /// - 방향 오브젝트(Left/Right/Front/Back) 참조 캐싱 및 초기 적용
    /// - AnimationManager/groundCheck 자동 연결(없을 때만)
    /// </summary>
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCol = GetComponent<Collider2D>();
        defaultGravity = rb.gravityScale;

        CacheDirectionTransforms();
        ApplyDirection();

        if (animationManager == null) animationManager = GetComponent<AnimationManager>();

        if (groundCheck == null)
        {
            var t = transform.Find("GroundCheck");
            if (t != null) groundCheck = t;
        }
    }

    /// <summary>
    /// 입력 기반 상태 처리(Update).
    /// - 사다리 진입/등반/탈출 처리
    /// - 일반 이동 방향/애니/점프 처리
    /// </summary>
    private void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        // 사다리 진입: 사다리 영역 + 위/아래 입력 + 쿨다운 통과
        if (!isOnLadder && canClimb && Mathf.Abs(y) > 0.01f && Time.time - lastLadderGrabTime > ladderGrabCooldown)
        {
            isOnLadder = true;
            lastLadderGrabTime = Time.time;
        }

        // 사다리 상태: 중력 제거 + 수직 이동 + 꼭대기 스냅/바닥 탈출/점프 탈출
        if (isOnLadder)
        {
            // 꼭대기 스냅은 "위로 올라갈 때만" 체크
            // 꼭대기 스냅은 "위로 올라갈 때만" 조금 더 일찍 체크
            if (y > 0.01f)
            {
                Vector2 origin = rb.position + Vector2.up * 0.5f;
                RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.up, 0.6f, groundMask);

                if (hit.collider != null)
                {
                    float playerHalfHeight = 0f;
                    if (bodyCol != null)
                        playerHalfHeight = bodyCol.bounds.extents.y;

                    float targetY = hit.collider.bounds.max.y + playerHalfHeight + ladderTopSnapExtra;

                    isOnLadder = false;
                    canClimb = false;
                    rb.gravityScale = defaultGravity;
                    rb.velocity = Vector2.zero;
                    rb.position = new Vector2(rb.position.x, targetY);

                    SetDirection(FacingDir.Right);
                    return;
                }
            }

            // 사다리 중엔 수평 이동을 막고, 방향은 뒤(Back)로 고정
            SetDirection(FacingDir.Back);
            rb.gravityScale = 0f;

            float vy = (Mathf.Abs(y) > 0.01f) ? y * climbSpeed : 0f;
            rb.velocity = new Vector2(0f, vy);

            // 내려가다 바닥/발판 감지 시 사다리 종료
            if (y < -0.01f && IsGrounded())
            {
                isOnLadder = false;
                rb.gravityScale = defaultGravity;
                SetDirection(FacingDir.Right);
                return;
            }

            // 애니: 등반 전용 상태 대신 Run/Idle로 처리
            if (animationManager != null)
            {
                if (Mathf.Abs(y) > 0.01f) animationManager.SetState(CharacterState.Run);
                else animationManager.SetState(CharacterState.Idle);
            }

            // 사다리에서 점프로 탈출
            if (Input.GetKeyDown(KeyCode.Space))
            {
                isOnLadder = false;
                rb.gravityScale = defaultGravity;
                rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
            }

            return; // 사다리 중엔 일반 이동/점프 로직을 실행하지 않음
        }

        // 일반 상태: 이동 방향(좌/우)
        if (x > 0.01f) SetFacing(true);
        else if (x < -0.01f) SetFacing(false);

        // 일반 상태: 애니(이동/대기)
        UpdateAnimationState(x);

        // 일반 상태: 점프(지면일 때만)
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
        }
    }

    /// <summary>
    /// 물리 이동 처리(FixedUpdate).
    /// - 사다리 상태가 아닐 때만 수평 속도 적용
    /// </summary>
    private void FixedUpdate()
    {
        if (isOnLadder) return;

        float x = Input.GetAxisRaw("Horizontal");
        rb.velocity = new Vector2(x * moveSpeed, rb.velocity.y);
    }

    /// <summary>
    /// 방향 표시용 자식 오브젝트(Left/Right/Front/Back) 캐싱.
    /// - 존재하지 않는 방향 오브젝트는 null로 유지
    /// </summary>
    private void CacheDirectionTransforms()
    {
        tLeft = transform.Find("Left");
        tRight = transform.Find("Right");
        tFront = transform.Find("Front");
        tBack = transform.Find("Back");
    }

    /// <summary>
    /// 지면 체크.
    /// - GroundCheck 기준으로 아래로 Raycast하여 groundMask 충돌 여부 확인
    /// </summary>
    private bool IsGrounded()
    {
        if (groundCheck == null) return false;

        RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, groundMask);
        return hit.collider != null;
    }

    /// <summary>
    /// 이동 입력 기반 애니 상태 갱신.
    /// - 수평 입력이 있으면 Run, 없으면 Idle
    /// </summary>
    private void UpdateAnimationState(float xInput)
    {
        if (animationManager == null) return;

        if (Mathf.Abs(xInput) > 0.01f)
            animationManager.SetState(CharacterState.Run);
        else
            animationManager.SetState(CharacterState.Idle);
    }

    /// <summary>
    /// 방향 상태 변경 후 표시 적용.
    /// - Left/Right/Front/Back 중 현재 방향만 활성화
    /// </summary>
    private void SetDirection(FacingDir dir)
    {
        currentDir = dir;
        ApplyDirection();
    }

    /// <summary>
    /// 현재 방향에 맞는 오브젝트만 활성화.
    /// - 각 방향 오브젝트가 존재할 때만 SetActive 수행
    /// </summary>
    private void ApplyDirection()
    {
        if (tLeft != null) tLeft.gameObject.SetActive(currentDir == FacingDir.Left);
        if (tRight != null) tRight.gameObject.SetActive(currentDir == FacingDir.Right);
        if (tFront != null) tFront.gameObject.SetActive(currentDir == FacingDir.Front);
        if (tBack != null) tBack.gameObject.SetActive(currentDir == FacingDir.Back);
    }

    /// <summary>
    /// 좌/우 바라보기 전환.
    /// </summary>
    private void SetFacing(bool facingRight)
    {
        SetDirection(facingRight ? FacingDir.Right : FacingDir.Left);
    }

    /// <summary>
    /// 사다리 트리거 진입 처리.
    /// - ladderMask에 해당하는 레이어면 등반 가능 상태(canClimb)로 전환
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & ladderMask) != 0)
            canClimb = true;
    }

    /// <summary>
    /// 사다리 트리거 이탈 처리.
    /// - 등반 가능 상태 해제
    /// - 사다리 상태였다면 강제 종료 후 중력/방향 복구
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & ladderMask) != 0)
        {
            canClimb = false;
            isOnLadder = false;
            rb.gravityScale = defaultGravity;
            SetDirection(FacingDir.Right);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// GroundCheck Raycast 범위를 Scene에서 확인하기 위한 Gizmo.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);
    }
#endif
}