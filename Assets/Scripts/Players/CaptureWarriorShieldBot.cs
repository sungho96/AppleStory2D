using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class CaptureWarriorShieldBot : MonoBehaviour
{
    [Header("Capture Settings")]
    [SerializeField] private bool captureModeEnabled = true;

    [Header("Auto Approach")]
    [SerializeField] private bool enableAutoApproach = true;
    [SerializeField] private float approachStopDistance = 2.0f;
    [SerializeField] private float approachMoveSpeed = 2.5f;

    [Header("Auto Basic Attack")]
    [SerializeField] private bool enableAutoBasicAttack = true;
    [SerializeField] private float basicAttackInterval = 1.0f;

    [Header("Auto Shield")]
    [SerializeField] private float shieldReactionDelay = 0.3f;
    [SerializeField] private float shieldHoldDuration = 1.0f;

    [Header("Refs")]
    [SerializeField] private GoblinBossCombatController2D goblinBoss;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private WarriorAttack2D warriorAttack;
    [SerializeField] private WarriorShieldBlock2D shieldBlock;
    [SerializeField] private NetworkObject networkObject;
    [SerializeField] private NetworkPlayerOwner networkPlayerOwner;

    [Header("Facing")]
    [SerializeField] private float sameXDeadZone = 0.05f;

    private Coroutine autoShieldRoutine;
    private bool isAutoShieldRunning;
    private bool lockedFacingForAutoShield;
    private bool isAutoApproaching;
    private bool hasTriedFindBoss;
    private float nextBasicAttackTime;

    private bool CanRunCaptureShield => enabled &&
        captureModeEnabled &&
        IsLocalOwnerWarrior();

    private bool CanRunAutoApproach => CanRunCaptureShield &&
        enableAutoApproach &&
        !isAutoShieldRunning;

    private bool CanRunAutoBasicAttack => CanRunCaptureShield &&
        enableAutoBasicAttack &&
        !isAutoShieldRunning &&
        !isAutoApproaching &&
        (shieldBlock == null || !shieldBlock.IsBlocking);

    private void Awake()
    {
        CacheRefs();
    }

    private void OnEnable()
    {
        CacheRefs();
        GoblinBossCombatController2D.BossAttackStarted += HandleBossAttackStarted;

        if (goblinBoss == null)
        {
            goblinBoss = FindFirstObjectByType<GoblinBossCombatController2D>();
            hasTriedFindBoss = true;
        }

        if (captureModeEnabled)
            Debug.Log("[CaptureShieldBot] Capture mode started");
    }

    private void OnDisable()
    {
        GoblinBossCombatController2D.BossAttackStarted -= HandleBossAttackStarted;
        StopCaptureShieldFlow();
        Debug.Log("[CaptureShieldBot] Capture mode stopped");
    }

    private void OnValidate()
    {
        if (Application.isPlaying && (!captureModeEnabled || !enableAutoApproach))
            StopAutoApproach(false, false);

        if (Application.isPlaying && !captureModeEnabled)
            StopCaptureShieldFlow();
    }

    private void Update()
    {
        HandleAutoApproach();
        HandleAutoBasicAttack();
    }

    private void CacheRefs()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
        if (warriorAttack == null)
            warriorAttack = GetComponent<WarriorAttack2D>();
        if (shieldBlock == null)
            shieldBlock = GetComponent<WarriorShieldBlock2D>();
        if (networkObject == null)
            networkObject = GetComponent<NetworkObject>();
        if (networkPlayerOwner == null)
            networkPlayerOwner = GetComponent<NetworkPlayerOwner>();
    }

    private void HandleBossAttackStarted(GoblinBossCombatController2D boss)
    {
        if (!CanRunCaptureShield || boss == null || isAutoShieldRunning)
            return;

        goblinBoss = goblinBoss != null ? goblinBoss : boss;
        StopAutoApproach(true);
        ResetAutoBasicAttack();
        Debug.Log("[CaptureShieldBot] Boss attack detected");
        autoShieldRoutine = StartCoroutine(CoAutoShieldBlock(boss.transform));
    }

    private void HandleAutoApproach()
    {
        if (!CanRunAutoApproach)
        {
            StopAutoApproach(false, false);
            return;
        }

        Transform bossTransform = GetBossTransform();
        if (bossTransform == null || playerController == null)
        {
            StopAutoApproach(false, false);
            return;
        }

        float deltaX = bossTransform.position.x - transform.position.x;
        float distanceX = Mathf.Abs(deltaX);
        if (distanceX <= Mathf.Max(0f, approachStopDistance) ||
            distanceX <= Mathf.Max(0f, sameXDeadZone))
        {
            StopAutoApproach(false, true);
            return;
        }

        float moveDirection = deltaX > 0f ? 1f : -1f;
        FaceBoss(bossTransform);
        playerController.SetCaptureMoveInput(moveDirection, approachMoveSpeed);

        if (!isAutoApproaching)
        {
            isAutoApproaching = true;
            Debug.Log("[CaptureShieldBot] Auto approach started");
        }
    }

    private void HandleAutoBasicAttack()
    {
        if (!CanRunAutoBasicAttack)
        {
            if (!captureModeEnabled || !enableAutoBasicAttack)
                ResetAutoBasicAttack();
            return;
        }

        Transform bossTransform = GetBossTransform();
        if (bossTransform == null || warriorAttack == null)
            return;

        float distanceX = Mathf.Abs(bossTransform.position.x - transform.position.x);
        if (distanceX > Mathf.Max(0f, approachStopDistance))
            return;

        if (Time.time < nextBasicAttackTime || !warriorAttack.CanUseBasicAttack)
            return;

        playerController?.ClearCaptureMoveInput();
        FaceBoss(bossTransform);
        warriorAttack.TriggerBasicAttackForCapture();
        nextBasicAttackTime = Time.time + Mathf.Max(0.05f, basicAttackInterval);
    }

    private IEnumerator CoAutoShieldBlock(Transform bossTransform)
    {
        isAutoShieldRunning = true;

        float facingDirection = FaceBoss(bossTransform);

        float delay = Mathf.Max(0f, shieldReactionDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (!CanRunCaptureShield || shieldBlock == null)
        {
            shieldBlock?.StopShieldBlockForCapture();
            ReleaseFacingLock();
            isAutoShieldRunning = false;
            autoShieldRoutine = null;
            yield break;
        }

        Debug.Log("[CaptureShieldBot] Auto ShieldBlock Start");
        playerController?.LockHorizontalFacing(facingDirection);
        lockedFacingForAutoShield = playerController != null;
        shieldBlock.StartShieldBlockForCapture(shieldHoldDuration);

        float holdDuration = Mathf.Max(0f, shieldHoldDuration);
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        shieldBlock.StopShieldBlockForCapture();
        ReleaseFacingLock();
        Debug.Log("[CaptureShieldBot] Auto ShieldBlock End");

        isAutoShieldRunning = false;
        autoShieldRoutine = null;
    }

    private float FaceBoss(Transform bossTransform)
    {
        float currentFacing = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        if (bossTransform == null || playerController == null)
            return currentFacing;

        float deltaX = bossTransform.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= Mathf.Max(0f, sameXDeadZone))
            return currentFacing;

        float facingDirection = deltaX > 0f ? 1f : -1f;
        if (!Mathf.Approximately(currentFacing, facingDirection))
        {
            Debug.Log("[CaptureShieldBot] Facing boss");
            playerController.FaceHorizontalDirectionForCapture(facingDirection);
        }

        return facingDirection;
    }

    private void StopCaptureShieldFlow()
    {
        if (autoShieldRoutine != null)
            StopCoroutine(autoShieldRoutine);

        StopAutoApproach(false, false);
        ResetAutoBasicAttack();
        shieldBlock?.StopShieldBlockForCapture();
        ReleaseFacingLock();
        autoShieldRoutine = null;
        isAutoShieldRunning = false;
    }

    private void StopAutoApproach(bool pausedForShieldBlock)
    {
        StopAutoApproach(pausedForShieldBlock, pausedForShieldBlock);
    }

    private void StopAutoApproach(bool pausedForShieldBlock, bool logReached)
    {
        playerController?.ClearCaptureMoveInput();

        if (!isAutoApproaching)
            return;

        isAutoApproaching = false;
        if (pausedForShieldBlock)
            Debug.Log("[CaptureShieldBot] Auto approach paused for ShieldBlock");
        else if (logReached)
            Debug.Log("[CaptureShieldBot] Approach distance reached");
    }

    private void ReleaseFacingLock()
    {
        if (!lockedFacingForAutoShield)
            return;

        playerController?.UnlockHorizontalFacing();
        lockedFacingForAutoShield = false;
    }

    private void ResetAutoBasicAttack()
    {
        nextBasicAttackTime = 0f;
    }

    private bool IsLocalOwnerWarrior()
    {
        if (networkObject == null || !networkObject.IsOwner)
            return false;

        return networkPlayerOwner == null ||
            networkPlayerOwner.CharacterType == PlayerCharacterType.Warrior;
    }

    private Transform GetBossTransform()
    {
        if (goblinBoss != null)
            return goblinBoss.transform;

        if (hasTriedFindBoss)
            return null;

        goblinBoss = FindFirstObjectByType<GoblinBossCombatController2D>();
        hasTriedFindBoss = true;
        return goblinBoss != null ? goblinBoss.transform : null;
    }
}
