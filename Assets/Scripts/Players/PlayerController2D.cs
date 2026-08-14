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

    [Header("Quick Step Sound")]
    [Tooltip("교체할 퀵스텝 AudioClip을 넣으세요. 비워두면 기본 합성음을 사용합니다.")]
    [SerializeField] private AudioClip quickStepSound;

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCol;

    private PlayerMovement2D movement;
    private PlayerLadder2D ladder;
    private PlayerDirection2D direction;
    private PlayerHitReaction2D hitReaction;
    private PlayerHealth2D health;
    private PlayerQuickStep2D quickStep;
    private WarriorDownStrike2D warriorDownStrike;
    private WarriorShieldBlock2D warriorShieldBlock;

    private float moveInput;
    private float verticalInput;
    private bool isHorizontalFacingLocked;
    private float lockedHorizontalFacing = 1f;

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
        quickStep = GetComponent<PlayerQuickStep2D>();
        warriorDownStrike = GetComponent<WarriorDownStrike2D>();
        warriorShieldBlock = GetComponent<WarriorShieldBlock2D>();

        // [퀵 스텝 추가] 씬 참조를 늘리지 않고 플레이어에 필요한 스텝 컴포넌트를 한 번만 보장합니다.
        if (quickStep == null)
            quickStep = gameObject.AddComponent<PlayerQuickStep2D>();

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

        if (quickStep != null)
            quickStep.Initialize(rb, playerCol, quickStepSound);
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

        HandleQuickStepInput();
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

        // [퀵 스텝 추가] 스텝 중에는 일반 이동이 Rigidbody 이동을 덮어쓰지 않게 합니다.
        if (quickStep != null && quickStep.IsStepping)
        {
            bool wasStepping = quickStep.IsStepping;
            quickStep.HandleStepMove();

            // [퀵 스텝 방향 수정] 이동 중에는 기존 방향을 유지하고, 종료 순간 이동한 방향을 바라봅니다.
            if (wasStepping && !quickStep.IsStepping && direction != null &&
                !isHorizontalFacingLocked)
            {
                direction.SetFacingByHorizontalInput(quickStep.StepDirection);
            }

            return;
        }

        if (movement != null)
        {
            bool blockMove = hitReaction != null && hitReaction.IsKnockback;
            movement.HandleNormalMove(moveInput, blockMove);
        }
    }

    private void LateUpdate()
    {
        // [Codex Animator 루트 고정 방지] HeroEditor Animator가 루트 Transform 위치를 덮어써도 Rigidbody2D 이동 위치를 최종값으로 유지합니다.
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || !rb.simulated)
            return;

        Vector3 currentPosition = transform.position;
        Vector2 physicsPosition = rb.position;

        if (Mathf.Abs(currentPosition.x - physicsPosition.x) < 0.001f &&
            Mathf.Abs(currentPosition.y - physicsPosition.y) < 0.001f)
            return;

        transform.position = new Vector3(physicsPosition.x, physicsPosition.y, currentPosition.z);
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

        if (quickStep != null && quickStep.IsStepping)
            return;

        // [래피드 볼리 방향 잠금] 3연사 중 반대 입력은 이동에만 사용하고 캐릭터 방향은 유지합니다.
        if (isHorizontalFacingLocked)
            return;

        direction.SetFacingByHorizontalInput(moveInput);
    }

    private void HandleQuickStepInput()
    {
        if (quickStep == null || direction == null)
            return;

        // [래피드 볼리 방향 잠금] 연사 도중 방향 더블 탭이 캐릭터를 뒤집지 않게 합니다.
        if (isHorizontalFacingLocked)
            return;

        float tappedDirection = 0f;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            tappedDirection = -1f;
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            tappedDirection = 1f;

        if (Mathf.Approximately(tappedDirection, 0f))
            return;

        bool isGrounded = ladder != null && ladder.IsGrounded;
        bool isClimbing = ladder != null && ladder.IsClimbing;
        bool isKnockback = hitReaction != null && hitReaction.IsKnockback;
        bool canStep = isGrounded && !isClimbing && !isKnockback;

        // [퀵 스텝 방향 수정] 첫 번째 입력부터 즉시 입력 방향을 바라보게 합니다.
        direction.SetFacingByHorizontalInput(tappedDirection);

        // 두 번째 같은 방향 입력에서는 방금 전환한 방향을 유지한 채 스텝합니다.
        quickStep.RegisterDirectionTap(tappedDirection, canStep);
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

    private void UpdateAnimationState()
    {
        if (animationManager == null)
            return;

        // [Codex Warrior Skill Animation] 워리어 스킬 중에는 Idle/Run 갱신이 스킬 포즈를 덮지 않게 합니다.
        if ((warriorDownStrike != null && warriorDownStrike.IsUsingSkill) ||
            (warriorShieldBlock != null && warriorShieldBlock.IsBlocking))
            return;

        // 현재 바라보는 방향
        // 왼쪽이면 true, 오른쪽이면 false
        bool facingLeft = GetHorizontalFacingDir() < 0f;

        // 넉백 중
        if (hitReaction != null && hitReaction.IsKnockback)
        {
            animationManager.SetJump(false, facingLeft);
            animationManager.SetState(CharacterState.Idle);
            return;
        }

        // 사다리 중
        if (ladder != null && ladder.IsClimbing)
        {
            animationManager.SetJump(false, facingLeft);

            if (Mathf.Abs(verticalInput) > 0.01f)
                animationManager.SetState(CharacterState.Run);
            else
                animationManager.SetState(CharacterState.Idle);

            return;
        }

        // 공중 여부
        bool isAirborne = ladder != null && !ladder.IsGrounded;

        // 공중이면 현재 바라보는 방향에 맞는 점프 애니메이션 실행
        if (isAirborne)
        {
            animationManager.SetJump(true, facingLeft);
            return;
        }

        // 착지하면 JumpL / JumpR 모두 해제
        animationManager.SetJump(false, facingLeft);

        // 착지 후 Idle / Run
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
        if (isHorizontalFacingLocked)
            return lockedHorizontalFacing;

        if (direction == null)
            return 1f;

        return direction.GetHorizontalFacingDir();
    }

    public void LockHorizontalFacing(float facingDirection)
    {
        // [래피드 볼리 방향 잠금] 시작 순간의 좌우 방향을 세 발이 끝날 때까지 고정합니다.
        lockedHorizontalFacing = facingDirection < 0f ? -1f : 1f;
        isHorizontalFacingLocked = true;
        direction?.SetFacingByHorizontalInput(lockedHorizontalFacing);
    }

    public void UnlockHorizontalFacing()
    {
        if (!isHorizontalFacingLocked)
            return;

        isHorizontalFacingLocked = false;

        // [래피드 볼리 방향 해제] 종료 순간 누르고 있는 방향을 바로 반영합니다.
        direction?.SetFacingByHorizontalInput(moveInput);
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
