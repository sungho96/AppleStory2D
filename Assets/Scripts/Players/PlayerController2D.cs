using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;

/// <summary>
/// 플레이어 전체 흐름 총관리.
/// - 입력 수집
/// - 사다리 / 이동 / 방향 / 피격 시스템 호출
/// - 애니메이션 상태 갱신
/// 
/// 이 스크립트는 "직접 모든 기능을 처리"하지 않고
/// 각 분리된 스크립트를 연결하고 순서를 제어하는 역할만 담당합니다.
/// </summary>
public class PlayerController2D : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private AnimationManager animationManager;

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCol;

    private PlayerMovement2D movement;
    private PlayerLadder2D ladder;
    private PlayerDirection2D direction;
    private PlayerHitReaction2D hitReaction;
    private PlayerHealth2D health;

    private float moveInput;
    private float verticalInput;

    /// <summary>
    /// 공통 컴포넌트 및 분리 스크립트 캐싱.
    /// </summary>
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCol = GetComponent<CapsuleCollider2D>();

        movement = GetComponent<PlayerMovement2D>();
        ladder = GetComponent<PlayerLadder2D>();
        direction = GetComponent<PlayerDirection2D>();
        hitReaction = GetComponent<PlayerHitReaction2D>();
        health = GetComponent<PlayerHealth2D>();

        if (animationManager == null)
            animationManager = GetComponent<AnimationManager>();

        if (movement != null)
            movement.Initialize(rb);

        if (ladder != null)
            ladder.Initialize(rb, playerCol);

        if (direction != null)
            direction.Initialize();

        if (hitReaction != null)
            hitReaction.Initialize(rb, playerCol, ladder);
    }

    /// <summary>
    /// 입력 / 감지 / 상태 판단 처리.
    /// </summary>
    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        moveInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");

        if (ladder != null)
        {
            ladder.CheckGround();
            ladder.RefreshLadderContacts();

            // 위에서 아래로 내려가기 진입
            ladder.TryEnterFromTop(verticalInput);

            // 몸통에서 위로 올라가기 진입
            ladder.TryEnterFromBody(verticalInput);

            // 꼭대기 탈출 처리
            ladder.TryExitToTop(verticalInput);
        }

        HandleDirection();
        HandleJump();
        UpdateAnimationState();
    }

    /// <summary>
    /// 실제 물리 이동 처리.
    /// - 사다리 중이면 등반 이동
    /// - 아니면 일반 수평 이동
    /// </summary>
    private void FixedUpdate()
    {
        if (health != null && health.IsDead)
            return;

        if (ladder != null && ladder.IsClimbing)
        {
            if (direction != null)
                direction.SetBack();

            ladder.HandleClimbMove(verticalInput);
            return;
        }

        if (movement != null)
        {
            bool blockMove = hitReaction != null && hitReaction.IsKnockback;
            movement.HandleNormalMove(moveInput, blockMove);
        }
    }

    /// <summary>
    /// 방향 갱신.
    /// - 사다리 중이 아닐 때만 좌/우 방향 갱신
    /// </summary>
    private void HandleDirection()
    {
        if (direction == null)
            return;

        if (ladder != null && ladder.IsClimbing)
            return;

        direction.SetFacingByHorizontalInput(moveInput);
    }

    /// <summary>
    /// 점프 처리.
    /// - 사다리 중 점프면 사다리 종료 후 점프
    /// - 일반 상태면 바닥일 때만 점프
    /// </summary>
    private void HandleJump()
    {
        if (!Input.GetButtonDown("Jump"))
            return;

        if (movement == null)
            return;

        if (ladder != null && ladder.IsClimbing)
        {
            ladder.StopClimbing();
            movement.Jump();
            return;
        }

        if (ladder != null && ladder.IsGrounded)
        {
            movement.Jump();
        }
    }

    /// <summary>
    /// 애니메이션 상태 갱신.
    /// - 사다리 중이면 세로 입력 기준
    /// - 일반 상태면 수평 입력 기준
    /// </summary>
    private void UpdateAnimationState()
    {
        if (animationManager == null)
            return;

        // 넉백 중일 때는 일단 Idle 유지
        if (hitReaction != null && hitReaction.IsKnockback)
        {
            animationManager.SetState(CharacterState.Idle);
            return;
        }

        if (ladder != null && ladder.IsClimbing)
        {
            if (Mathf.Abs(verticalInput) > 0.01f)
                animationManager.SetState(CharacterState.Run);
            else
                animationManager.SetState(CharacterState.Idle);

            return;
        }

        if (Mathf.Abs(moveInput) > 0.01f)
            animationManager.SetState(CharacterState.Run);
        else
            animationManager.SetState(CharacterState.Idle);
    }

    /// <summary>
    /// 현재 좌우 바라보는 방향값 반환.
    /// - Left = -1
    /// - Right / Front / Back = 1
    /// </summary>
    public float GetHorizontalFacingDir()
    {
        if (direction == null)
            return 1f;

        return direction.GetHorizontalFacingDir();
    }

    /// <summary>
    /// 디버그 Gizmo 표시.
    /// - 사다리 감지 박스
    /// - GroundCheck Ray
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        PlayerLadder2D ladderComp = GetComponent<PlayerLadder2D>();
        if (ladderComp != null)
            ladderComp.DrawDebugGizmos();
    }
}