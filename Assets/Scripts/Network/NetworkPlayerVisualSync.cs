using Unity.Netcode;
using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;

public class NetworkPlayerVisualSync : NetworkBehaviour
{
    [Header("Visual State")]
    [SerializeField] private PlayerDirection2D playerDirection;
    [SerializeField] private PlayerLadder2D playerLadder;
    [SerializeField] private AnimationManager animationManager;

    private readonly NetworkVariable<int> syncedDirection = new(
        (int)PlayerDirection2D.FacingDir.Right,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<int> syncedJump = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private int lastSentDirection = (int)PlayerDirection2D.FacingDir.Right;
    private int lastSentJump;

    private void Awake()
    {
        if (playerDirection == null)
            playerDirection = GetComponent<PlayerDirection2D>();

        if (playerLadder == null)
            playerLadder = GetComponent<PlayerLadder2D>();

        if (animationManager == null)
            animationManager = GetComponent<AnimationManager>();
    }

    public override void OnNetworkSpawn()
    {
        syncedDirection.OnValueChanged += OnDirectionChanged;
        syncedJump.OnValueChanged += OnJumpChanged;

        if (!IsOwner)
        {
            ApplyDirection(syncedDirection.Value);
            ApplyJump(syncedJump.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedDirection.OnValueChanged -= OnDirectionChanged;
        syncedJump.OnValueChanged -= OnJumpChanged;
    }

    private void LateUpdate()
    {
        if (!IsOwner || playerDirection == null)
            return;

        SyncDirection();
        SyncJump();
    }

    private void SyncDirection()
    {
        int currentDirection = (int)playerDirection.CurrentDir;
        if (currentDirection == lastSentDirection)
            return;

        // Codex: Share only the owner's current visual direction through a NetworkVariable.
        lastSentDirection = currentDirection;
        syncedDirection.Value = currentDirection;
    }

    private void SyncJump()
    {
        if (playerLadder == null)
            return;

        bool isAirborne = !playerLadder.IsGrounded && !playerLadder.IsClimbing;
        bool facingLeft = playerDirection.GetHorizontalFacingDir() < 0f;
        int currentJump = isAirborne ? (facingLeft ? -1 : 1) : 0;

        if (currentJump == lastSentJump)
            return;

        // Codex: Backup-sync jump visuals because child direction objects affect JumpL / JumpR display.
        lastSentJump = currentJump;
        syncedJump.Value = currentJump;
    }

    private void OnDirectionChanged(int previousValue, int newValue)
    {
        if (IsOwner)
            return;

        ApplyDirection(newValue);
    }

    private void OnJumpChanged(int previousValue, int newValue)
    {
        if (IsOwner)
            return;

        ApplyJump(newValue);
    }

    private void ApplyDirection(int directionValue)
    {
        if (playerDirection == null)
            return;

        playerDirection.SetDirectionFromNetwork((PlayerDirection2D.FacingDir)directionValue);
    }

    private void ApplyJump(int jumpValue)
    {
        if (animationManager == null)
            return;

        bool isJumping = jumpValue != 0;
        bool facingLeft = jumpValue < 0;
        animationManager.SetJump(isJumping, facingLeft);
    }
}
