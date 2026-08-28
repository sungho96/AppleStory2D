using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;

/// <summary>
/// 고블린 보스 전용 추적, 방향 전환, 스킬 시전 연출을 담당합니다.
/// </summary>
public class GoblinBossCombatController2D : NetworkBehaviour
{
    public static event System.Action<GoblinBossCombatController2D> BossAttackStarted;

    [Header("Follow")]
    [SerializeField] private float approachSpeed = 1.45f;
    [SerializeField] private float approachDistance = 5.5f;

    [Header("Cast")]
    [SerializeField] private Color castEffectColor = new Color(0.55f, 0.85f, 0.12f, 0.42f);

    [Header("Jump Move")]
    [SerializeField] private float jumpMoveFirstDelay = 1.8f;
    [SerializeField] private float jumpMoveCooldown = 4.5f;
    [SerializeField] private float jumpLandingDistanceFromPlayer = 2.8f;
    [SerializeField] private float jumpMoveDuration = 0.78f;
    [SerializeField] private float jumpMoveHeight = 5.2f;
    [SerializeField] private float jumpCrouchDuration = 0.18f;
    [SerializeField] private float jumpLandingDelay = 0.28f;
    [SerializeField] private float arenaMinX = -8.2f;
    [SerializeField] private float arenaMaxX = 8.2f;

    [Header("Close Counter Attack")]
    [SerializeField, Range(0f, 1f)] private float closeCounterChance = 0.3f;
    [SerializeField] private float closeCounterRange = 3.2f;
    [SerializeField] private float closeCounterWindup = 0.5f;
    [SerializeField] private float closeCounterWindupMax = 0.8f;
    [SerializeField] private float closeCounterRecovery = 0.42f;
    [SerializeField] private float closeCounterRetryInterval = 1.2f;
    [SerializeField] private Vector2 closeCounterKnockback = new Vector2(11f, 3.2f);
    [SerializeField] private Color closeCounterWarningColor = new Color(1f, 0.12f, 0.02f, 0.82f);
    [SerializeField] private float closeCounterWarningScale = 3.2f;
    [SerializeField] private float closeCounterArmRaise = 0.48f;
    [SerializeField] private float closeCounterArmAngle = 92f;
    [SerializeField] private float closeCounterReadyLean = 0.16f;

    [Header("PowerShot Shield")]
    [SerializeField] private bool enablePowerShotShield = true;
    [SerializeField] private bool disablePowerShotShieldForNetworkTest = true;
    [SerializeField] private float shieldFirstDelay = 7f;
    [SerializeField] private Vector2 shieldPhaseOneCooldownRange = new Vector2(12f, 16f);
    [SerializeField] private Vector2 shieldPhaseTwoCooldownRange = new Vector2(16f, 22f);
    [SerializeField] private int shieldPhaseOneBreakDamage = 30;
    [SerializeField] private int shieldPhaseTwoBreakDamage = 50;
    [SerializeField] private float shieldGroggyDuration = 2.2f;
    [SerializeField] private float shieldGroggyDamageMultiplier = 1.3f;
    [SerializeField] private string shieldBlockStateName = "ShieldBlockU";
    [SerializeField] private string shieldGroggyStateName = "ClimbU";
    [SerializeField] private Color shieldPowerShotColor = new Color(1f, 0.62f, 0.16f, 0.7f);
    [SerializeField] private Color shieldBreakColor = new Color(1f, 0.66f, 0.18f, 0.88f);

    [Header("Heal Cast")]
    [SerializeField, Range(0f, 1f)] private float healTriggerHpRatio = 0.3f;
    [SerializeField] private float healCastDuration = 3f;
    [SerializeField] private float healRecoveryDelay = 0.4f;
    [SerializeField] private float healHitReplayInterval = 0.42f;
    [SerializeField, Range(0f, 1f)] private float healAmountRatio = 0.15f;
    [SerializeField] private Color healCastColor = new Color(0.58f, 0.16f, 0.92f, 0.62f);
    [SerializeField] private Color healBurstColor = new Color(1f, 0.78f, 0.22f, 0.88f);

    [Header("Network Sync Diagnostics")]
    [SerializeField] private bool enableBossSyncDiagnosticLog = true;
    [SerializeField] private float bossSyncDiagnosticInterval = 0.1f;

    [Header("Start Facing")]
    [SerializeField] private bool startFacingLeft = true;

    private Rigidbody2D rb;
    private GoblinHealth2D health;
    private AnimationManager animationManager;
    private Transform player;
    private Transform leftVisual;
    private Transform rightVisual;
    private Coroutine castRoutine;
    private float moveDirection;
    private bool isCasting;
    private bool wasMoving;
    private float nextJumpMoveTime;
    private Transform castingArm;
    private Transform weaponArm;
    private Vector3 castingArmBasePosition;
    private Quaternion castingArmBaseRotation;
    private Vector3 weaponArmBasePosition;
    private Quaternion weaponArmBaseRotation;
    private float castingMotionTimer;
    private bool castingFaceLeft;
    private bool isCloseCounterCasting;
    private float nextCloseCounterAttemptTime;
    private Vector3 leftVisualBaseScale;
    private Vector3 rightVisualBaseScale;
    private Coroutine shieldRoutine;
    private float nextShieldTime;
    private bool isShieldBlocking;
    private bool isGroggy;
    private int shieldBreakDamage;
    private GameObject shieldEffect;
    private SpriteRenderer shieldEffectRenderer;
    private SpriteRenderer shieldPlateRenderer;
    private SpriteRenderer shieldCoreRenderer;
    private bool hasUsedHealCast;
    private bool isHealCasting;
    private float healHitWobbleTimer;
    private GameObject healEffect;
    private SpriteRenderer healEffectRenderer;
    private bool isIntroLocked;
    private bool disabledAsDuplicateController;
    private readonly NetworkVariable<int> syncedMoveState = new(
        (int)CharacterState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> syncedFaceLeft = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private bool isJumpMoving;
    private Coroutine clientJumpVisualRoutine;
    private float nextBossSyncDiagnosticTime;
    private bool loggedBossSyncNetworkSettings;

    public bool IsCasting => isIntroLocked || isCasting || isShieldBlocking || isGroggy || isHealCasting;
    public float CurrentDamageMultiplier => isGroggy ? Mathf.Max(1f, shieldGroggyDamageMultiplier) : 1f;

    public override void OnNetworkSpawn()
    {
        syncedMoveState.OnValueChanged += OnSyncedMoveStateChanged;
        syncedFaceLeft.OnValueChanged += OnSyncedFaceLeftChanged;

        LogBossSyncNetworkSettingsOnce("OnNetworkSpawn");

        if (!IsServer)
        {
            ConfigureClientRigidbodyForNetworkTransform();
            ApplySyncedMoveState(syncedMoveState.Value);
            ApplySyncedFaceLeft(syncedFaceLeft.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedMoveState.OnValueChanged -= OnSyncedMoveStateChanged;
        syncedFaceLeft.OnValueChanged -= OnSyncedFaceLeftChanged;
    }

    private void ConfigureClientRigidbodyForNetworkTransform()
    {
        if (rb == null)
            return;

        // [Codex Boss Client Rigidbody] 비서버 클라이언트는 물리 이동을 계산하지 않고 NetworkTransform이 적용하는 서버 위치만 따릅니다.
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
    }

    public void SetIntroLocked(bool locked)
    {
        // [Codex Boss Intro Lock] 카메라 인트로 동안 보스 이동/공격 패턴만 잠시 멈추고 기존 패턴 구조는 그대로 둡니다.
        isIntroLocked = locked;
        if (!locked)
            return;

        moveDirection = 0f;
        SetMoving(false);

        if (rb != null && !IsNetworkClientOnly())
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    /// <summary>
    /// 플레이어가 공격 범위에 들어왔을 때 일반공격 발동을 시도합니다.
    /// </summary>
    public void TryCloseCounterAttack(Transform attacker)
    {
        // [보스 근접 일반공격 추가] 거리와 Inspector 확률을 통과하면 보스가 직접 일반공격을 시작합니다.
        if (IsNetworkClientOnly() || attacker == null || isIntroLocked || isCasting || isShieldBlocking || isGroggy || (health != null && health.IsDead))
            return;

        float horizontalDistance = Mathf.Abs(attacker.position.x - transform.position.x);
        if (horizontalDistance > closeCounterRange || Random.value > closeCounterChance)
            return;

        StartCoroutine(CoCloseCounterAttack(attacker));
    }

    private IEnumerator CoCloseCounterAttack(Transform attacker)
    {
        // [보스 근접 반격 애니메이션] 기존 보스 강공격 자세를 재사용해 짧은 선딜 후 타격하도록 합니다.
        isCasting = true;
        moveDirection = 0f;
        SetMoving(false);
        FacePlayer();

        if (rb != null && !IsNetworkClientOnly())
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animationManager != null)
        {
            animationManager.SetState(CharacterState.Idle);
            animationManager.BossCast();
        }

        // [Codex 보스 근접 견제] 플레이어가 계속 붙어 있을 때 바로 밀지 않고, 무기를 들어 올린 뒤 짧은 예고 시간을 줍니다.
        GameObject warningEffect = CreateCastEffect(closeCounterWarningColor);
        SpriteRenderer warningRenderer = warningEffect.GetComponent<SpriteRenderer>();
        SpriteRenderer weaponRenderer = FindWeaponRenderer();
        Transform weaponTransform = weaponRenderer != null ? weaponRenderer.transform : null;
        weaponArm = FindCastingArm(weaponTransform);
        castingArm = weaponArm;
        castingArmBasePosition = castingArm != null ? castingArm.localPosition : Vector3.zero;
        castingArmBaseRotation = castingArm != null ? castingArm.localRotation : Quaternion.identity;
        weaponArmBasePosition = weaponArm != null ? weaponArm.localPosition : Vector3.zero;
        weaponArmBaseRotation = weaponArm != null ? weaponArm.localRotation : Quaternion.identity;
        castingFaceLeft = leftVisual != null && leftVisual.gameObject.activeSelf;
        castingMotionTimer = 0f;
        isCloseCounterCasting = true;

        float windupDuration = Random.Range(
            Mathf.Max(0f, closeCounterWindup),
            Mathf.Max(closeCounterWindup, closeCounterWindupMax));
        float timer = 0f;
        while (timer < windupDuration)
        {
            timer += Time.deltaTime;
            castingMotionTimer = timer;
            float normalized = Mathf.Clamp01(timer / windupDuration);
            float pulse = 1f + Mathf.Sin(timer * 18f) * 0.1f;

            UpdateCastEffectPosition(warningEffect.transform, weaponRenderer);
            warningEffect.transform.localScale = Vector3.one * Mathf.Lerp(1.3f, closeCounterWarningScale, normalized) * pulse;
            warningRenderer.color = new Color(
                closeCounterWarningColor.r,
                closeCounterWarningColor.g,
                closeCounterWarningColor.b,
                closeCounterWarningColor.a * (1f - normalized * 0.35f));
            yield return null;
        }

        if (attacker != null && Mathf.Abs(attacker.position.x - transform.position.x) <= closeCounterRange)
        {
            PlayerHitReaction2D hitReaction = attacker.GetComponent<PlayerHitReaction2D>();
            if (hitReaction == null)
                hitReaction = attacker.GetComponentInParent<PlayerHitReaction2D>();

            // [보스 근접 반격 넉백] 보스에서 플레이어 쪽을 향하는 방향으로 밀어내며 위쪽 힘도 Inspector에서 조절합니다.
            float knockbackDirection = Mathf.Sign(attacker.position.x - transform.position.x);
            if (Mathf.Approximately(knockbackDirection, 0f))
                knockbackDirection = 1f;
            hitReaction?.ApplyKnockback(new Vector2(
                knockbackDirection * Mathf.Abs(closeCounterKnockback.x),
                closeCounterKnockback.y));
        }

        if (castingArm != null)
        {
            castingArm.localPosition = castingArmBasePosition;
            castingArm.localRotation = castingArmBaseRotation;
        }
        if (weaponArm != null)
        {
            weaponArm.localPosition = weaponArmBasePosition;
            weaponArm.localRotation = weaponArmBaseRotation;
        }

        Destroy(warningEffect);
        castingArm = null;
        weaponArm = null;
        isCloseCounterCasting = false;
        yield return new WaitForSeconds(Mathf.Max(0f, closeCounterRecovery));
        isCasting = false;
    }

    private void Awake()
    {
        if (DisableDuplicateControllerIfNeeded())
            return;

        ClearLegacyShieldDisableFlag();

        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<GoblinHealth2D>();
        animationManager = GetComponent<AnimationManager>();
        leftVisual = transform.Find("Left");
        rightVisual = transform.Find("Right");
        // [Codex Boss Start Facing 복구] 씬 시작 직후에는 기존 보스 연출처럼 Left만 먼저 보이게 맞춥니다.
        ApplySyncedFaceLeft(startFacingLeft);
        leftVisualBaseScale = leftVisual != null ? leftVisual.localScale : Vector3.one;
        rightVisualBaseScale = rightVisual != null ? rightVisual.localScale : Vector3.one;
        nextJumpMoveTime = Time.time + Mathf.Max(0f, jumpMoveFirstDelay);
        // [Codex Boss Shield Toggle] 임시 네트워크 테스트 플래그 대신 실제 사용 토글로 쉴드 시작 여부를 정합니다.
        nextShieldTime = !enablePowerShotShield
            ? float.PositiveInfinity
            : Time.time + Mathf.Max(0f, shieldFirstDelay);

        // [보스 이동] 보스 인스턴스에서는 일반 고블린의 랜덤 순찰을 사용하지 않습니다.
        GoblinController2D normalController = GetComponent<GoblinController2D>();
        if (normalController != null)
            normalController.enabled = false;
    }

    private IEnumerator Start()
    {
        if (disabledAsDuplicateController)
            yield break;

        if (IsNetworkClientOnly())
            yield break;

        // [Codex Boss Start Facing] 씬 로드/네트워크 Spawn 직후 플레이어 위치가 잡힌 다음 기존 Left/Right 방식으로 시작 방향을 맞춥니다.
        yield return null;
        FaceNearestPlayer();
    }

    private void Update()
    {
        if (disabledAsDuplicateController)
            return;

        LogBossSyncDiagnostic("Update");

        if (IsNetworkClientOnly())
            return;

        FindPlayer();

        if (isIntroLocked)
        {
            moveDirection = 0f;
            return;
        }

        if (health != null && health.IsDead)
        {
            moveDirection = 0f;
            return;
        }

        if (player == null)
            return;

        if (isCasting || isShieldBlocking || isGroggy || isHealCasting)
        {
            // [Codex Boss Shield Groggy] During groggy, keep ClimbU instead of overwriting it with Idle every frame.
            if (!isGroggy && !isJumpMoving)
                SetMoving(false);
            moveDirection = 0f;
            return;
        }

        FacePlayer();

        if (Time.time >= nextJumpMoveTime)
        {
            StartCoroutine(CoJumpMove());
            return;
        }

        if (ShouldStartHealCast())
        {
            StartCoroutine(CoHealCast());
            return;
        }

        if (enablePowerShotShield && Time.time >= nextShieldTime)
        {
            shieldRoutine = StartCoroutine(CoShieldBlock());
            return;
        }

        float deltaX = player.position.x - transform.position.x;
        float attackApproachDistance = Mathf.Min(approachDistance, closeCounterRange * 0.8f);
        moveDirection = Mathf.Abs(deltaX) > attackApproachDistance ? Mathf.Sign(deltaX) : 0f;
        SetMoving(Mathf.Abs(moveDirection) > 0.01f);

        // [보스 근접 일반공격 판정] 플레이어가 가까우면 매 프레임이 아닌 설정된 간격마다 한 번만 확률을 판정합니다.
        if (Mathf.Abs(deltaX) <= closeCounterRange && Time.time >= nextCloseCounterAttemptTime)
        {
            nextCloseCounterAttemptTime = Time.time + Mathf.Max(0.1f, closeCounterRetryInterval);
            TryCloseCounterAttack(player);
        }
    }

    private void FixedUpdate()
    {
        if (disabledAsDuplicateController)
            return;

        LogBossSyncDiagnostic("FixedUpdate");

        if (IsNetworkClientOnly())
            return;

        if (rb == null)
            return;

        if (isJumpMoving)
            return;

        float horizontalVelocity = (isIntroLocked || isCasting || isShieldBlocking || isGroggy || isHealCasting) ? 0f : moveDirection * approachSpeed;
        rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
    }

    private void LateUpdate()
    {
        LogBossSyncDiagnostic("LateUpdate");

        // [Codex Animator 루트 고정 방지] Animator가 보스 루트 위치를 덮어써도 Rigidbody2D 이동/점프 위치를 유지합니다.
        SyncRootTransformToRigidbody();

        if ((!isCasting && !isShieldBlocking) || castingArm == null)
            return;

        // [보스 주문 애니메이션] Animator의 공격 자세를 먼저 초기화한 뒤 무기를 든 팔에 전용 주문 자세를 적용합니다.
        if (weaponArm != null)
        {
            weaponArm.localPosition = weaponArmBasePosition;
            weaponArm.localRotation = weaponArmBaseRotation;
        }

        // [보스 주문 애니메이션] Animator가 파츠 자세를 계산한 뒤 팔 전체를 들어 올려 주문 동작이 덮어써지지 않게 합니다.
        float liftRatio = Mathf.Clamp01(castingMotionTimer / 0.18f);
        float shakeAmount = isCloseCounterCasting ? 15f : 7f;
        float shake = Mathf.Sin(Mathf.Min(castingMotionTimer, 1f) * 24f) * shakeAmount;
        float targetAngle = isCloseCounterCasting ? closeCounterArmAngle : 55f;
        float raisedAngle = castingFaceLeft ? -targetAngle : targetAngle;
        float raiseHeight = isCloseCounterCasting ? closeCounterArmRaise : 0.22f;
        float leanDirection = castingFaceLeft ? 1f : -1f;
        Vector3 readyLean = isCloseCounterCasting ? Vector3.right * (leanDirection * closeCounterReadyLean * liftRatio) : Vector3.zero;
        castingArm.localPosition = castingArmBasePosition + Vector3.up * (raiseHeight * liftRatio) + readyLean;
        castingArm.localRotation = castingArmBaseRotation * Quaternion.Euler(0f, 0f, (raisedAngle + shake) * liftRatio);
    }

    private void SyncRootTransformToRigidbody()
    {
        // [Codex Boss Network Position Sync] 클라이언트는 NetworkTransform이 받은 서버 위치를 따라가야 하므로 로컬 Rigidbody 위치로 루트를 덮어쓰지 않습니다.
        if (IsNetworkClientOnly())
            return;

        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || !rb.simulated)
            return;

        Vector3 currentPosition = transform.position;
        Vector2 physicsPosition = rb.position;

        if (Mathf.Abs(currentPosition.x - physicsPosition.x) < 0.001f &&
            Mathf.Abs(currentPosition.y - physicsPosition.y) < 0.001f)
            return;

        transform.position = new Vector3(physicsPosition.x, physicsPosition.y, currentPosition.z);
    }

    private void OnDisable()
    {
        ApplyJumpSquash(1f, 1f);
        isCloseCounterCasting = false;
        DestroyShieldEffect();
        DestroyHealEffect();
    }

    public bool TryHandleShieldDamage(int damage, float hitDir)
    {
        if (IsNetworkClientOnly())
            return false;

        // [Codex Boss Shield Toggle] enablePowerShotShield가 꺼져 있으면 쉴드가 피해를 막지 않습니다.
        if (!enablePowerShotShield)
            return false;

        if (!isShieldBlocking)
            return false;

        // [Codex Boss Shield Break Damage] 파워샷 여부와 관계없이 쉴드에 막힌 공격 데미지를 누적해 일정 피해 이상이면 깨뜨립니다.
        shieldBreakDamage += Mathf.Max(1, damage);
        PlayShieldCrackFeedback(shieldBreakDamage);
        if (IsSpawned && IsServer)
            PlayShieldCrackFeedbackClientRpc(shieldBreakDamage);

        if (shieldBreakDamage >= RequiredShieldBreakDamage())
        {
            if (shieldRoutine != null)
                StopCoroutine(shieldRoutine);
            shieldRoutine = StartCoroutine(CoShieldBreakGroggy());
        }
        else
        {
            PlayShieldBlockedFeedback(hitDir);
            if (IsSpawned && IsServer)
                PlayShieldBlockedFeedbackClientRpc(hitDir);
        }

        return true;
    }

    [ClientRpc]
    private void PlayShieldBlockedFeedbackClientRpc(float hitDir)
    {
        if (IsServer)
            return;

        // [Codex Boss Shield Network Visual] 쉴드에 막힌 충격 흔들림을 클라이언트 비주얼에도 맞춰 재생합니다.
        PlayShieldBlockedFeedback(hitDir);
    }

    [ClientRpc]
    private void PlayShieldCrackFeedbackClientRpc(int currentBreakDamage)
    {
        if (IsServer)
            return;

        // [Codex Boss Shield Network Visual] 서버의 누적 쉴드 피해량 기준으로 클라이언트 균열 이펙트를 재생합니다.
        PlayShieldCrackFeedback(currentBreakDamage);
    }

    public void NotifyHealCastHit()
    {
        // [Codex Boss Heal Cast] 회복은 끊기지 않지만 피격 순간 보스 몸이 짧게 떨리게 합니다.
        if (!isHealCasting)
            return;

        healHitWobbleTimer = 0.18f;
    }

    private IEnumerator CoShieldBlock()
    {
        if (!enablePowerShotShield)
            yield break;

        // [Codex Boss Shield Break Damage] ShieldBlockU stays up until blocked damage reaches the configured break amount.
        isShieldBlocking = true;
        isCasting = true;
        shieldBreakDamage = 0;
        moveDirection = 0f;
        SetMoving(false);
        FacePlayer();

        if (rb != null && !IsNetworkClientOnly())
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animationManager != null && animationManager.Animator != null)
            animationManager.Animator.CrossFade(shieldBlockStateName, 0.08f, 0);

        PrepareShieldVisual();
        if (IsSpawned && IsServer)
            StartShieldVisualClientRpc();

        float timer = 0f;
        while (isShieldBlocking)
        {
            timer += Time.deltaTime;
            castingMotionTimer = timer;
            UpdateShieldEffect(timer, 0.92f + Mathf.Sin(timer * 5f) * 0.08f);
            yield return null;
        }
    }

    private bool ShouldStartHealCast()
    {
        // [Codex Boss Heal Cast] HP 30% 이하에서 딱 한 번만 회복 패턴을 시작합니다.
        return !hasUsedHealCast &&
            health != null &&
            health.HpRatio <= healTriggerHpRatio &&
            !health.IsDead;
    }

    private IEnumerator CoHealCast()
    {
        // [Codex Boss Heal Cast] 회복 패턴은 보스가 뒤로 빠진 뒤 Hit 애니메이션으로 3초 캐스팅합니다.
        hasUsedHealCast = true;
        isHealCasting = true;
        isCasting = true;
        moveDirection = 0f;
        SetMoving(false);
        FacePlayer();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        yield return CoJumpCrouch();

        Vector2 startPosition = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 landingPosition = CalculateHealRetreatPosition(startPosition.y);
        FacePosition(landingPosition.x);
        float jumpDuration = Mathf.Max(0.05f, jumpMoveDuration * 0.82f);
        yield return CoVelocityJumpTo(startPosition, landingPosition, jumpDuration, jumpMoveHeight * 0.72f);

        FacePlayer();
        PlayHealHitAnimation();
        CreateHealEffect();

        float castTimer = 0f;
        float hitReplayTimer = 0f;
        float duration = Mathf.Max(0.2f, healCastDuration);
        while (castTimer < duration)
        {
            castTimer += Time.deltaTime;
            hitReplayTimer += Time.deltaTime;
            float normalized = Mathf.Clamp01(castTimer / duration);
            if (hitReplayTimer >= Mathf.Max(0.05f, healHitReplayInterval))
            {
                // [Codex Boss Heal Cast] 회복 캐스팅 3초 동안 Hit 모션이 끊기지 않게 반복 재생합니다.
                hitReplayTimer = 0f;
                PlayHealHitAnimation();
            }

            UpdateHealEffect(castTimer, normalized);
            UpdateHealHitWobble();
            yield return null;
        }

        ApplyJumpSquash(1f, 1f);
        PlayHealBurstFeedback();
        int healAmount = Mathf.Max(1, Mathf.RoundToInt(health.MaxHp * healAmountRatio));
        health.HealBossHp(healAmount);
        DestroyHealEffect();

        yield return new WaitForSeconds(Mathf.Max(0f, healRecoveryDelay));

        isHealCasting = false;
        isCasting = false;
        nextJumpMoveTime = Time.time + Mathf.Max(0.1f, jumpMoveCooldown);
        ScheduleNextShield();
        if (animationManager != null)
            animationManager.SetState(CharacterState.Idle);
    }

    private Vector2 CalculateHealRetreatPosition(float landingY)
    {
        float centerX = (arenaMinX + arenaMaxX) * 0.5f;
        float retreatX = transform.position.x < centerX
            ? arenaMinX + 1.6f
            : arenaMaxX - 1.6f;

        if (player != null)
        {
            float awayFromPlayer = Mathf.Sign(transform.position.x - player.position.x);
            if (Mathf.Approximately(awayFromPlayer, 0f))
                awayFromPlayer = transform.position.x < centerX ? -1f : 1f;
            retreatX = transform.position.x + awayFromPlayer * jumpLandingDistanceFromPlayer;
        }

        return new Vector2(Mathf.Clamp(retreatX, arenaMinX + 1.1f, arenaMaxX - 1.1f), landingY);
    }

    private void PlayHealHitAnimation()
    {
        if (animationManager == null || animationManager.Animator == null)
            return;

        // [Codex Boss Heal Cast] Hit enum may not exist in every HeroEditor version, so play the Animator state by name.
        Animator animator = animationManager.Animator;
        int hitHash = Animator.StringToHash("Hit");
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (!animator.HasState(layer, hitHash))
                continue;

            animator.CrossFade(hitHash, 0.08f, layer, 0f);
            return;
        }

        animationManager.SetState(CharacterState.Idle);
    }

    private void CreateHealEffect()
    {
        DestroyHealEffect();
        healEffect = new GameObject("GoblinBoss_HealCastEffect");
        healEffectRenderer = healEffect.AddComponent<SpriteRenderer>();
        healEffectRenderer.sprite = CreateCircleSprite(96);
        healEffectRenderer.color = healCastColor;
        healEffectRenderer.sortingOrder = 22;
    }

    private void UpdateHealEffect(float timer, float normalized)
    {
        if (healEffect == null || healEffectRenderer == null)
            return;

        healEffect.transform.position = transform.position + new Vector3(0f, 0.85f, 0f);
        float pulse = 1f + Mathf.Sin(timer * 9f) * 0.08f;
        float scale = Mathf.Lerp(1.3f, 2.05f, normalized) * pulse;
        healEffect.transform.localScale = new Vector3(scale * 1.16f, scale * 0.72f, 1f);
        float alpha = Mathf.Lerp(0.38f, healCastColor.a, normalized) + Mathf.Sin(timer * 13f) * 0.06f;
        healEffectRenderer.color = new Color(healCastColor.r, healCastColor.g, healCastColor.b, Mathf.Clamp01(alpha));
    }

    private void UpdateHealHitWobble()
    {
        if (healHitWobbleTimer <= 0f)
            return;

        healHitWobbleTimer -= Time.deltaTime;
        float ratio = Mathf.Clamp01(healHitWobbleTimer / 0.18f);
        float wobble = Mathf.Sin(Time.time * 46f) * 0.045f * ratio;
        ApplyJumpSquash(1f + wobble, 1f - Mathf.Abs(wobble));
    }

    private void PlayHealBurstFeedback()
    {
        GameObject burst = new GameObject("GoblinBoss_HealBurst");
        SpriteRenderer renderer = burst.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateCircleSprite(112);
        renderer.color = healBurstColor;
        renderer.sortingOrder = 30;
        burst.transform.position = transform.position + new Vector3(0f, 0.8f, 0f);
        burst.transform.localScale = Vector3.one * 0.8f;
        StartCoroutine(CoHealBurstFade(burst, renderer));

        CameraShake2D shake = Camera.main != null ? Camera.main.GetComponent<CameraShake2D>() : null;
        if (shake != null)
            shake.Shake(0.08f, 0.035f);
    }

    private IEnumerator CoHealBurstFade(GameObject burst, SpriteRenderer renderer)
    {
        float timer = 0f;
        const float duration = 0.24f;
        while (timer < duration && burst != null && renderer != null)
        {
            timer += Time.deltaTime;
            float ratio = Mathf.Clamp01(timer / duration);
            burst.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 3.1f, ratio);
            renderer.color = new Color(healBurstColor.r, healBurstColor.g, healBurstColor.b, healBurstColor.a * (1f - ratio));
            yield return null;
        }

        if (burst != null)
            Destroy(burst);
    }

    private void DestroyHealEffect()
    {
        if (healEffect != null)
            Destroy(healEffect);
        healEffect = null;
        healEffectRenderer = null;
    }

    private IEnumerator CoShieldBreakGroggy()
    {
        isShieldBlocking = false;
        PlayShieldBreakFeedback();
        if (IsSpawned && IsServer)
            StartShieldBreakGroggyVisualClientRpc();

        RestoreCastingArm();
        yield return new WaitForSeconds(0.12f);

        isGroggy = true;
        isCasting = true;
        moveDirection = 0f;
        PlayShieldGroggyAnimation();

        float timer = 0f;
        while (timer < shieldGroggyDuration)
        {
            timer += Time.deltaTime;
            float wobble = Mathf.Sin(timer * 22f) * 0.035f;
            ApplyJumpSquash(1f + wobble, 1f - Mathf.Abs(wobble));
            yield return null;
        }

        ApplyJumpSquash(1f, 1f);
        isGroggy = false;
        EndShieldBlock(true);
    }

    [ClientRpc]
    private void StartShieldBreakGroggyVisualClientRpc()
    {
        if (IsServer)
            return;

        // [Codex Boss Shield Network Visual] 서버에서 쉴드가 깨지면 클라이언트는 HP 판정 없이 파괴/그로기 연출만 따라갑니다.
        if (shieldRoutine != null)
            StopCoroutine(shieldRoutine);

        shieldRoutine = StartCoroutine(CoShieldBreakGroggyVisualOnly());
    }

    private IEnumerator CoShieldBreakGroggyVisualOnly()
    {
        isShieldBlocking = false;
        PlayShieldBreakFeedback();
        RestoreCastingArm();
        yield return new WaitForSeconds(0.12f);

        isGroggy = true;
        isCasting = true;
        moveDirection = 0f;
        PlayShieldGroggyAnimation();

        float timer = 0f;
        while (timer < shieldGroggyDuration)
        {
            timer += Time.deltaTime;
            float wobble = Mathf.Sin(timer * 22f) * 0.035f;
            ApplyJumpSquash(1f + wobble, 1f - Mathf.Abs(wobble));
            yield return null;
        }

        ApplyJumpSquash(1f, 1f);
        EndShieldBlock(true);
    }

    private void EndShieldBlock(bool broken)
    {
        isShieldBlocking = false;
        isCasting = false;
        isGroggy = false;
        shieldBreakDamage = 0;
        shieldRoutine = null;
        RestoreCastingArm();
        DestroyShieldEffect();
        ScheduleNextShield();

        if (!broken && animationManager != null)
            animationManager.SetState(CharacterState.Idle);
    }

    private void PlayShieldGroggyAnimation()
    {
        if (animationManager == null || animationManager.Animator == null)
            return;

        Animator animator = animationManager.Animator;

        // [Codex Boss Shield Groggy] HeroEditor uses the state name "Climb" on the Upper layer for the ClimbU clip.
        if (shieldGroggyStateName == "ClimbU")
        {
            int upperLayer = animator.GetLayerIndex("Upper");
            if (upperLayer >= 0)
                animator.CrossFade("Climb", 0.08f, upperLayer, 0f);

            animationManager.SetState(CharacterState.Climb);
            return;
        }

        int layer = animator.GetLayerIndex("Complex");
        if (layer < 0)
            layer = 0;
        animator.CrossFade(shieldGroggyStateName, 0.08f, layer, 0f);
    }

    private int RequiredShieldBreakDamage()
    {
        int configuredDamage = health != null && health.HpRatio <= 0.5f
            ? shieldPhaseTwoBreakDamage
            : shieldPhaseOneBreakDamage;
        return Mathf.Max(1, configuredDamage);
    }

    private void ScheduleNextShield()
    {
        if (!enablePowerShotShield)
        {
            nextShieldTime = float.PositiveInfinity;
            return;
        }

        Vector2 range = health != null && health.HpRatio <= 0.5f
            ? shieldPhaseTwoCooldownRange
            : shieldPhaseOneCooldownRange;
        float min = Mathf.Max(0.1f, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        nextShieldTime = Time.time + Random.Range(min, max);
    }

    private void CreateShieldEffect()
    {
        DestroyShieldEffect();
        shieldEffect = new GameObject("GoblinBoss_PowerShotShield");
        shieldEffectRenderer = shieldEffect.AddComponent<SpriteRenderer>();
        shieldEffectRenderer.sprite = CreateCircleSprite(72);
        shieldEffectRenderer.color = shieldPowerShotColor;
        shieldEffectRenderer.sortingOrder = 24;

        // [Codex Boss Shield Shape] A shield-shaped plate makes ShieldBlockU read as a real guard, not a round aura.
        GameObject plate = new GameObject("GoblinBoss_ShieldPlate");
        plate.transform.SetParent(shieldEffect.transform, false);
        shieldPlateRenderer = plate.AddComponent<SpriteRenderer>();
        shieldPlateRenderer.sprite = CreateShieldPlateSprite(96);
        shieldPlateRenderer.sortingOrder = 25;

        GameObject core = new GameObject("GoblinBoss_ShieldCore");
        core.transform.SetParent(shieldEffect.transform, false);
        shieldCoreRenderer = core.AddComponent<SpriteRenderer>();
        shieldCoreRenderer.sprite = CreateShieldCoreSprite(48);
        shieldCoreRenderer.sortingOrder = 26;

        UpdateShieldEffect(0f, 1f);
    }

    private void UpdateShieldEffect(float timer, float strength)
    {
        if (shieldEffect == null || shieldEffectRenderer == null)
            return;

        bool faceLeft = leftVisual != null && leftVisual.gameObject.activeSelf;
        float direction = faceLeft ? -1f : 1f;
        shieldEffect.transform.position = transform.position + new Vector3(direction * 1.62f, 1.02f, 0f);
        float pulse = 1f + Mathf.Sin(timer * 8f) * 0.05f + Mathf.Sin(timer * 17f) * 0.018f;
        shieldEffect.transform.localScale = Vector3.one * Mathf.Lerp(1.55f, 1.82f, strength) * pulse;
        float alpha = shieldBreakColor.a * Mathf.Lerp(0.58f, 0.82f, strength);
        shieldEffectRenderer.color = new Color(shieldBreakColor.r, shieldBreakColor.g, shieldBreakColor.b, alpha * 0f);

        if (shieldPlateRenderer != null)
        {
            shieldPlateRenderer.transform.localPosition = new Vector3(direction * 0.02f, -0.02f, 0f);
            shieldPlateRenderer.transform.localScale = new Vector3(0.94f, 1.2f, 1f);
            shieldPlateRenderer.color = new Color(shieldBreakColor.r, shieldBreakColor.g, shieldBreakColor.b, Mathf.Clamp01(alpha * 1.22f));
        }

        if (shieldCoreRenderer != null)
        {
            float corePulse = 0.82f + Mathf.Sin(timer * 12f) * 0.12f;
            shieldCoreRenderer.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            shieldCoreRenderer.transform.localScale = new Vector3(0.44f, 0.8f + corePulse * 0.06f, 1f);
            shieldCoreRenderer.color = new Color(1f, 0.86f, 0.36f, Mathf.Clamp01(alpha * corePulse * 1.3f));
        }
    }

    private void PlayShieldBlockedFeedback(float hitDir)
    {
        if (shieldEffect == null)
            return;

        float direction = Mathf.Approximately(hitDir, 0f) ? 1f : Mathf.Sign(hitDir);
        shieldEffect.transform.position += new Vector3(direction * 0.08f, 0f, 0f);
        UpdateShieldEffect(Time.time, 0.62f);
    }

    private void PlayShieldCrackFeedback(int currentBreakDamage)
    {
        bool willBreak = currentBreakDamage >= RequiredShieldBreakDamage();
        UpdateShieldEffect(Time.time, willBreak ? 1f : 0.84f);
        int shardCount = willBreak ? 8 : 5;
        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = new GameObject("GoblinBoss_ShieldCrack");
            SpriteRenderer renderer = shard.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateCircleSprite(16);
            renderer.color = new Color(shieldBreakColor.r, shieldBreakColor.g, shieldBreakColor.b, 0.82f);
            renderer.sortingOrder = 26;
            float angle = ((360f / shardCount) * i + currentBreakDamage * 23f) * Mathf.Deg2Rad;
            shard.transform.position = shieldEffect != null
                ? shieldEffect.transform.position + new Vector3(Mathf.Cos(angle) * 0.42f, Mathf.Sin(angle) * 0.42f, 0f)
                : transform.position;
            shard.transform.localScale = Vector3.one * 0.16f;
            Destroy(shard, 0.18f);
        }
    }

    private void PlayShieldBreakFeedback()
    {
        if (shieldEffectRenderer != null)
            shieldEffectRenderer.color = new Color(1f, 0.86f, 0.36f, 0.95f);

        for (int i = 0; i < 12; i++)
        {
            GameObject shard = new GameObject("GoblinBoss_ShieldBreakShard");
            SpriteRenderer renderer = shard.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateCircleSprite(14);
            renderer.color = shieldBreakColor;
            renderer.sortingOrder = 28;
            float angle = (360f / 12f * i) * Mathf.Deg2Rad;
            shard.transform.position = shieldEffect != null
                ? shieldEffect.transform.position + new Vector3(Mathf.Cos(angle) * 0.62f, Mathf.Sin(angle) * 0.62f, 0f)
                : transform.position;
            shard.transform.localScale = Vector3.one * 0.2f;
            Destroy(shard, 0.32f);
        }

        DestroyShieldEffect();
    }

    private void DestroyShieldEffect()
    {
        if (shieldEffect != null)
            Destroy(shieldEffect);
        shieldEffect = null;
        shieldEffectRenderer = null;
        shieldPlateRenderer = null;
        shieldCoreRenderer = null;
    }

    private void RestoreCastingArm()
    {
        if (castingArm != null)
        {
            castingArm.localPosition = castingArmBasePosition;
            castingArm.localRotation = castingArmBaseRotation;
        }
        if (weaponArm != null)
        {
            weaponArm.localPosition = weaponArmBasePosition;
            weaponArm.localRotation = weaponArmBaseRotation;
        }
        castingArm = null;
        weaponArm = null;
    }

    public void BeginFallingCast(float duration)
    {
        NotifyBossAttackStarted();
        ReportBossSkillVisualStart("MeteorCast", duration);

        if (castRoutine != null)
            StopCoroutine(castRoutine);

        castRoutine = StartCoroutine(CoFallingCast(Mathf.Max(0.2f, duration), castEffectColor));
    }

    public void BeginIceCast(float duration)
    {
        NotifyBossAttackStarted();
        ReportBossSkillVisualStart("IceWaveCast", duration);

        if (castRoutine != null)
            StopCoroutine(castRoutine);

        // [얼음 파도 시전색] 무기 앞 주문 빛을 청백색으로 바꿔 메테오 시전과 즉시 구분되게 합니다.
        Color iceCastColor = new Color(0.2f, 0.88f, 1f, 0.68f);
        castRoutine = StartCoroutine(CoFallingCast(Mathf.Max(0.2f, duration), iceCastColor));
    }

    private IEnumerator CoJumpMove()
    {
        LogBossSyncDiagnostic("JumpStart", true);
        ReportBossSkillVisualStart("JumpMove", jumpMoveDuration);

        // [Codex Boss Jump Move] Locks follow and other boss attacks while this standalone jump move runs.
        isCasting = true;
        isJumpMoving = true;
        moveDirection = 0f;
        SetMoving(false);
        FacePlayer();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
        if (animationManager != null)
            animationManager.SetState(CharacterState.Idle);

        yield return CoJumpCrouch();

        Transform targetPlayer = PickPriorityJumpTarget();
        if (targetPlayer == null)
            targetPlayer = player;

        Vector2 startPosition = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 landingPosition = CalculateJumpLandingPosition(targetPlayer, startPosition.y);
        FacePosition(landingPosition.x);

        if (IsSpawned && IsServer)
            PlayJumpMoveClientRpc(jumpMoveDuration, NetworkManager.Singleton.ServerTime.Time, landingPosition.x < transform.position.x);

        yield return CoVelocityJumpTo(startPosition, landingPosition, jumpMoveDuration, jumpMoveHeight);

        LogBossSyncDiagnostic("JumpLanding", true);

        yield return new WaitForSeconds(Mathf.Max(0f, jumpLandingDelay));

        nextJumpMoveTime = Time.time + Mathf.Max(0.1f, jumpMoveCooldown);
        isJumpMoving = false;
        isCasting = false;
        FacePlayer();
    }

    private IEnumerator CoJumpCrouch()
    {
        // [Codex Boss Jump Move] Squashes only the left/right visuals so the root collider stays stable.
        float timer = 0f;
        float duration = Mathf.Max(0.01f, jumpCrouchDuration);
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float ratio = Mathf.Clamp01(timer / duration);
            float squash = Mathf.Lerp(1f, 0.86f, Mathf.Sin(ratio * Mathf.PI));
            float stretch = Mathf.Lerp(1f, 1.08f, Mathf.Sin(ratio * Mathf.PI));
            ApplyJumpSquash(stretch, squash);
            yield return null;
        }

        ApplyJumpSquash(1f, 1f);
    }

    private Transform PickPriorityJumpTarget()
    {
        // [Codex Boss Jump Target Priority] 점프 타겟은 랜덤이 아니라 아처를 먼저 노리고, 아처가 없을 때 워리어를 노립니다.
        Transform networkTarget = PickPriorityNetworkJumpTarget();
        if (networkTarget != null)
            return networkTarget;

        return PickPriorityTaggedJumpTarget();
    }

    private Transform PickPriorityNetworkJumpTarget()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
            return null;

        Transform warriorTarget = null;
        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            if (client == null || client.PlayerObject == null || !client.PlayerObject.IsSpawned)
                continue;

            Transform candidate = client.PlayerObject.transform;
            NetworkPlayerOwner owner = candidate.GetComponent<NetworkPlayerOwner>();
            if (owner == null)
                owner = candidate.GetComponentInParent<NetworkPlayerOwner>();

            if (owner != null && owner.CharacterType == PlayerCharacterType.Archer)
                return candidate;

            if (warriorTarget == null && owner != null && owner.CharacterType == PlayerCharacterType.Warrior)
                warriorTarget = candidate;
        }

        return warriorTarget;
    }

    private Transform PickPriorityTaggedJumpTarget()
    {
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        if (playerObjects == null || playerObjects.Length == 0)
            return null;

        Transform warriorTarget = null;
        for (int i = 0; i < playerObjects.Length; i++)
        {
            if (playerObjects[i] == null)
                continue;

            Transform candidate = playerObjects[i].transform;
            NetworkPlayerOwner owner = candidate.GetComponentInParent<NetworkPlayerOwner>();
            if (owner != null && owner.CharacterType == PlayerCharacterType.Archer)
                return candidate;

            if (warriorTarget == null && owner != null && owner.CharacterType == PlayerCharacterType.Warrior)
                warriorTarget = candidate;
        }

        return warriorTarget != null ? warriorTarget : playerObjects[0].transform;
    }

    private Vector2 CalculateJumpLandingPosition(Transform targetPlayer, float landingY)
    {
        if (targetPlayer == null)
            return new Vector2(transform.position.x, landingY);

        // [Codex Boss Back Landing] 선택된 플레이어가 바라보는 방향의 뒤쪽으로 떨어져 백어택처럼 읽히게 합니다.
        float playerFacing = GetTargetHorizontalFacing(targetPlayer);
        float targetX = targetPlayer.position.x - playerFacing * jumpLandingDistanceFromPlayer;
        targetX = Mathf.Clamp(targetX, arenaMinX, arenaMaxX);
        return new Vector2(targetX, landingY);
    }

    private float GetTargetHorizontalFacing(Transform targetPlayer)
    {
        PlayerController2D controller = targetPlayer.GetComponentInParent<PlayerController2D>();
        if (controller != null)
            return controller.GetHorizontalFacingDir();

        PlayerDirection2D direction = targetPlayer.GetComponentInParent<PlayerDirection2D>();
        if (direction != null)
            return direction.GetHorizontalFacingDir();

        // [Codex Boss Back Landing Fallback] 방향 컴포넌트를 못 찾으면 기존 보스-플레이어 위치 관계로 안전하게 방향을 추정합니다.
        float bossToPlayer = targetPlayer.position.x - transform.position.x;
        return Mathf.Approximately(bossToPlayer, 0f) ? 1f : Mathf.Sign(bossToPlayer);
    }

    private void ApplyJumpSquash(float scaleX, float scaleY)
    {
        if (leftVisual != null)
            leftVisual.localScale = new Vector3(leftVisualBaseScale.x * scaleX, leftVisualBaseScale.y * scaleY, leftVisualBaseScale.z);
        if (rightVisual != null)
            rightVisual.localScale = new Vector3(rightVisualBaseScale.x * scaleX, rightVisualBaseScale.y * scaleY, rightVisualBaseScale.z);
    }

    private IEnumerator CoVelocityJumpTo(Vector2 startPosition, Vector2 landingPosition, float desiredDuration, float desiredHeight)
    {
        if (rb == null)
        {
            // [Codex Boss Server Authority Move] Rigidbody2D가 없을 때만 서버 Spawn 위치 보정용 Transform 쓰기를 최소 허용합니다.
            transform.position = new Vector3(landingPosition.x, landingPosition.y, transform.position.z);
            LogBossSyncDiagnostic("JumpNoRbCommit", true);
            yield break;
        }

        // [Codex Boss Velocity Jump] 착지점은 시작 순간 한 번만 정하고, 이동은 Rigidbody 속도와 중력으로 처리해 네트워크 위치 보정 흔들림을 줄입니다.
        float duration = Mathf.Max(0.08f, desiredDuration);
        float height = Mathf.Max(0.1f, desiredHeight);
        float originalGravityScale = rb.gravityScale;
        float gravityY = Mathf.Abs(Physics2D.gravity.y) < 0.01f ? -9.81f : Physics2D.gravity.y;
        float jumpGravityScale = Mathf.Max(0.01f, (8f * height) / (Mathf.Abs(gravityY) * duration * duration));
        float horizontalVelocity = (landingPosition.x - startPosition.x) / duration;
        float verticalVelocity = (landingPosition.y - startPosition.y) / duration + (4f * height) / duration;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = jumpGravityScale;
        rb.position = startPosition;
        // [Codex Boss Server Authority Move] NetworkTransform이 읽는 루트 Transform을 서버 Rigidbody 시작점과 한 번만 맞춥니다.
        transform.position = new Vector3(startPosition.x, startPosition.y, transform.position.z);
        rb.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);
        LogBossSyncDiagnostic("JumpVelocityApplied", true);

        float timer = 0f;
        while (timer < duration)
        {
            LogBossSyncDiagnostic("JumpInAir");
            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.gravityScale = originalGravityScale;
        rb.position = landingPosition;
        // [Codex Boss Server Authority Move] 착지 오차만 서버 Rigidbody 결과로 확정하고 클라이언트 전파는 NetworkTransform에 맡깁니다.
        transform.position = new Vector3(landingPosition.x, landingPosition.y, transform.position.z);
        rb.linearVelocity = Vector2.zero;
        LogBossSyncDiagnostic("JumpPositionCommitted", true);

    }

    [ClientRpc]
    private void PlayJumpMoveClientRpc(
        float duration,
        double serverStartTime,
        bool faceLeft)
    {
        if (IsServer)
            return;

        // [Codex Boss Jump Network Authority] 클라이언트는 보스 루트 위치를 직접 쓰지 않고 서버 NetworkTransform 동기화만 따릅니다.
        if (clientJumpVisualRoutine != null)
            StopCoroutine(clientJumpVisualRoutine);

        clientJumpVisualRoutine = StartCoroutine(CoClientJumpVisual(
            duration,
            serverStartTime,
            faceLeft));
    }

    private IEnumerator CoClientJumpVisual(
        float duration,
        double serverStartTime,
        bool faceLeft)
    {
        isCasting = true;
        isJumpMoving = true;
        moveDirection = 0f;
        SetMoving(false);
        FaceDirection(faceLeft);
        LogBossSyncDiagnostic("ClientJumpVisualStart", true);

        float safeDuration = Mathf.Max(0.08f, duration);
        float timer = 0f;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            timer = Mathf.Clamp((float)(NetworkManager.Singleton.ServerTime.Time - serverStartTime), 0f, safeDuration);

        while (timer < safeDuration)
        {
            LogBossSyncDiagnostic("ClientJumpVisualLoop");
            float ratio = Mathf.Clamp01(timer / safeDuration);
            // [Codex Boss Jump Client Visual Only] 실제 위치는 NetworkTransform만 따르고, 클라이언트는 몸 눌림 연출만 재생합니다.
            float squash = Mathf.Lerp(1f, 0.9f, Mathf.Sin(ratio * Mathf.PI));
            float stretch = Mathf.Lerp(1f, 1.06f, Mathf.Sin(ratio * Mathf.PI));
            ApplyJumpSquash(stretch, squash);

            timer += Time.deltaTime;
            yield return null;
        }

        ApplyJumpSquash(1f, 1f);
        isJumpMoving = false;
        isCasting = false;
        clientJumpVisualRoutine = null;
        LogBossSyncDiagnostic("ClientJumpVisualEnd", true);
    }

    private void LogBossSyncDiagnostic(string phase, bool force = false)
    {
        if (!enableBossSyncDiagnosticLog)
            return;

        if (!force && !isJumpMoving)
            return;

        float interval = Mathf.Max(0.02f, bossSyncDiagnosticInterval);
        if (!force && Time.unscaledTime < nextBossSyncDiagnosticTime)
            return;

        nextBossSyncDiagnosticTime = Time.unscaledTime + interval;

        NetworkManager manager = NetworkManager.Singleton;
        bool hasNetwork = manager != null && manager.IsListening;
        string role = hasNetwork && manager.IsServer ? "SERVER" : "CLIENT";
        double networkTime = hasNetwork ? manager.ServerTime.Time : Time.timeAsDouble;
        string rbState = rb == null
            ? "RigidbodyY=null VelocityY=null BodyType=null Simulated=null"
            : $"RigidbodyY={rb.position.y:F4} VelocityY={rb.linearVelocity.y:F4} BodyType={rb.bodyType} Simulated={rb.simulated}";

        // [Codex Boss Sync Diagnostic] 서버/클라이언트 점프 Y 차이 원인을 확인하기 위한 임시 진단 로그입니다.
        Debug.Log(
            $"[BossSync][{role}] phase={phase} netTime={networkTime:F4} localTime={Time.time:F4} " +
            $"TransformY={transform.position.y:F4} {rbState}");
    }

    private void LogBossSyncNetworkSettingsOnce(string phase)
    {
        if (!enableBossSyncDiagnosticLog || loggedBossSyncNetworkSettings)
            return;

        loggedBossSyncNetworkSettings = true;

        Component networkTransformComponent = GetComponent("NetworkTransform");

        string transformSettings = networkTransformComponent == null
            ? "NetworkTransformComponent=null"
            : BuildNetworkTransformSettings(networkTransformComponent);

        NetworkManager manager = NetworkManager.Singleton;
        bool hasNetwork = manager != null && manager.IsListening;
        string role = hasNetwork && manager.IsServer ? "SERVER" : "CLIENT";
        string rbState = rb == null
            ? "Rigidbody2D=null"
            : $"Rigidbody2D BodyType={rb.bodyType} Simulated={rb.simulated} GravityScale={rb.gravityScale} Constraints={rb.constraints}";

        // [Codex Boss Sync Diagnostic] 런타임 네트워크/물리 설정을 Host와 Client에서 각각 확인하기 위한 임시 진단 로그입니다.
        Debug.Log($"[BossSync][{role}] phase={phase} {rbState} {transformSettings}");
    }

    private static string BuildNetworkTransformSettings(Component component)
    {
        return BuildBehaviourFieldSettings(
            component,
            "AuthorityMode",
            "SyncPositionX",
            "SyncPositionY",
            "Interpolate",
            "UseRigidBodyForMotion",
            "PositionInterpolationType",
            "PositionMaxInterpolationTime",
            "StaleDataHandling");
    }

    private static string BuildBehaviourFieldSettings(Component component, params string[] memberNames)
    {
        string result = BuildComponentFieldSettings(component, memberNames);
        if (component is Behaviour behaviour)
            result += $" Enabled={behaviour.enabled}";

        return result;
    }

    private static string BuildComponentFieldSettings(Component component, params string[] memberNames)
    {
        System.Type type = component.GetType();
        string result = $"{type.Name}";

        for (int i = 0; i < memberNames.Length; i++)
        {
            object value = ReadComponentMemberValue(component, type, memberNames[i]);
            result += $" {memberNames[i]}={(value != null ? value : "unavailable")}";
        }

        return result;
    }

    private static object ReadComponentMemberValue(Component component, System.Type type, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.GetIndexParameters().Length == 0)
            return property.GetValue(component);

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
            return field.GetValue(component);

        return null;
    }

    private IEnumerator CoFallingCast(float duration, Color effectColor)
    {
        // [보스 시전 모션] 이동 정지 후 기존 강공격 모션을 스킬 발동 제스처로 재사용합니다.
        isCasting = true;
        moveDirection = 0f;
        SetMoving(false);
        FacePlayer();

        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animationManager != null)
        {
            animationManager.SetState(CharacterState.Idle);
            animationManager.BossCast();
        }

        GameObject castEffect = CreateCastEffect(effectColor);
        SpriteRenderer effectRenderer = castEffect.GetComponent<SpriteRenderer>();
        SpriteRenderer weaponRenderer = FindWeaponRenderer();
        Transform weaponTransform = weaponRenderer != null ? weaponRenderer.transform : null;
        weaponArm = FindCastingArm(weaponTransform);
        // [보스 주문 애니메이션] 주문을 시전할 때 무기를 든 팔을 직접 들어 올립니다.
        castingArm = weaponArm;
        castingArmBasePosition = castingArm != null ? castingArm.localPosition : Vector3.zero;
        castingArmBaseRotation = castingArm != null ? castingArm.localRotation : Quaternion.identity;
        weaponArmBasePosition = weaponArm != null ? weaponArm.localPosition : Vector3.zero;
        weaponArmBaseRotation = weaponArm != null ? weaponArm.localRotation : Quaternion.identity;
        castingFaceLeft = leftVisual != null && leftVisual.gameObject.activeSelf;
        castingMotionTimer = 0f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            castingMotionTimer = timer;
            float normalized = Mathf.Clamp01(timer / duration);
            float pulse = 1f + Mathf.Sin(timer * 14f) * 0.12f;

            UpdateCastEffectPosition(castEffect.transform, weaponRenderer);
            castEffect.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 2.4f, normalized) * pulse;
            effectRenderer.color = new Color(
                effectColor.r,
                effectColor.g,
                effectColor.b,
                effectColor.a * (1f - normalized * 0.65f));
            yield return null;
        }

        if (castingArm != null)
        {
            castingArm.localPosition = castingArmBasePosition;
            castingArm.localRotation = castingArmBaseRotation;
        }
        if (weaponArm != null)
        {
            weaponArm.localPosition = weaponArmBasePosition;
            weaponArm.localRotation = weaponArmBaseRotation;
        }

        Destroy(castEffect);
        isCasting = false;
        castingArm = null;
        weaponArm = null;
        castRoutine = null;
    }

    private void ReportBossSkillVisualStart(string eventName, float expectedDuration)
    {
        bool isNetworkActive = IsNetworkDebugActive();
        if (!isNetworkActive || !NetworkManager.Singleton.IsServer)
            return;

        double serverStartTime = NetworkManager.Singleton.ServerTime.Time;
        Vector3 serverPosition = transform.position;

        BroadcastBossSkillStartClientRpc(
            eventName,
            serverStartTime,
            expectedDuration,
            serverPosition);
    }

    private void NotifyBossAttackStarted()
    {
        // [Codex CaptureShieldBot] 실제 보스 공격 시전 시작 순간만 촬영 보조 스크립트에 알립니다.
        BossAttackStarted?.Invoke(this);
    }

    [ClientRpc]
    private void BroadcastBossSkillStartClientRpc(
        string eventName,
        double serverStartTime,
        float expectedDuration,
        Vector3 serverPosition)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (NetworkManager.Singleton.IsServer)
            return;

        // [Codex Boss Skill Sync] 서버 스킬 수신 시 클라이언트에서도 판정 없이 보스 시전 모션만 재생합니다.
        if (eventName == "MeteorCast")
        {
            NotifyBossAttackStarted();

            if (castRoutine != null)
                StopCoroutine(castRoutine);

            castRoutine = StartCoroutine(CoFallingCast(Mathf.Max(0.2f, expectedDuration), castEffectColor));
        }
        else if (eventName == "IceWaveCast")
        {
            NotifyBossAttackStarted();

            if (castRoutine != null)
                StopCoroutine(castRoutine);

            Color iceCastColor = new Color(0.2f, 0.88f, 1f, 0.68f);
            castRoutine = StartCoroutine(CoFallingCast(Mathf.Max(0.2f, expectedDuration), iceCastColor));
        }
    }

    [ClientRpc]
    private void StartShieldVisualClientRpc()
    {
        if (IsServer)
            return;

        // [Codex Boss Shield Network Visual] 서버에서 쉴드가 시작된 순간을 받아 클라이언트는 판정 없이 모션/이펙트만 재생합니다.
        if (shieldRoutine != null)
            StopCoroutine(shieldRoutine);

        shieldRoutine = StartCoroutine(CoShieldVisualOnly());
    }

    private IEnumerator CoShieldVisualOnly()
    {
        isShieldBlocking = true;
        isCasting = true;
        shieldBreakDamage = 0;
        moveDirection = 0f;
        SetMoving(false);

        if (animationManager != null && animationManager.Animator != null)
            animationManager.Animator.CrossFade(shieldBlockStateName, 0.08f, 0);

        PrepareShieldVisual();

        float timer = 0f;
        while (isShieldBlocking)
        {
            timer += Time.deltaTime;
            castingMotionTimer = timer;
            UpdateShieldEffect(timer, 0.92f + Mathf.Sin(timer * 5f) * 0.08f);
            yield return null;
        }
    }

    private void PrepareShieldVisual()
    {
        // [Codex Boss Shield Network Visual] 서버/클라이언트가 같은 함수로 방패 팔 자세와 쉴드 이펙트를 준비합니다.
        SpriteRenderer weaponRenderer = FindWeaponRenderer();
        Transform weaponTransform = weaponRenderer != null ? weaponRenderer.transform : null;
        weaponArm = FindCastingArm(weaponTransform);
        castingArm = weaponArm;
        castingArmBasePosition = castingArm != null ? castingArm.localPosition : Vector3.zero;
        castingArmBaseRotation = castingArm != null ? castingArm.localRotation : Quaternion.identity;
        weaponArmBasePosition = weaponArm != null ? weaponArm.localPosition : Vector3.zero;
        weaponArmBaseRotation = weaponArm != null ? weaponArm.localRotation : Quaternion.identity;
        castingFaceLeft = leftVisual != null && leftVisual.gameObject.activeSelf;
        castingMotionTimer = 0f;

        CreateShieldEffect();
    }

    private bool IsNetworkDebugActive()
    {
        return NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            IsSpawned;
    }

    private bool IsNetworkClientOnly()
    {
        return NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer;
    }

    private bool IsJumpReadyForAttackGate()
    {
        // [Codex Boss Jump Priority] 점프 시간이 지났을 때 메테오/아이스 코루틴이 먼저 캐스팅을 선점하지 않도록 잠깐 대기시킵니다.
        if (player == null || health == null || health.IsDead)
            return false;

        return Time.time >= nextJumpMoveTime;
    }

    private GameObject CreateCastEffect(Color effectColor)
    {
        GameObject effect = new GameObject("GoblinBoss_CastEffect");

        SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateCircleSprite(48);
        renderer.color = effectColor;
        renderer.sortingOrder = 20;
        return effect;
    }

    private SpriteRenderer FindWeaponRenderer()
    {
        // [보스 주문 무기 팔] 이름이 같은 방패 Handle을 피하고 Character4D에 등록된 실제 주 무기를 사용합니다.
        if (animationManager != null && animationManager.Character != null)
        {
            bool faceLeft = leftVisual != null && leftVisual.gameObject.activeSelf;
            Character activeCharacter = faceLeft
                ? animationManager.Character.Left
                : animationManager.Character.Right;

            if (activeCharacter != null && activeCharacter.PrimaryWeaponRenderer != null)
                return activeCharacter.PrimaryWeaponRenderer;
        }

        return null;
    }

    private static Transform FindCastingArm(Transform weaponTransform)
    {
        // [보스 전용 주문 모션] Handle에서 부모를 거슬러 올라가 실제 Arm 파츠를 찾아 손과 무기가 함께 움직이게 합니다.
        Transform current = weaponTransform;
        while (current != null)
        {
            if (current.name.StartsWith("Arm") && !current.name.Contains("Anchor"))
                return current;

            current = current.parent;
        }

        return weaponTransform;
    }

    private void UpdateCastEffectPosition(Transform effect, SpriteRenderer weaponRenderer)
    {
        // [보스 시전 효과] 무기 Sprite의 플레이어 방향 끝점을 따라가며 빛이 손이 아닌 무기 앞에서 보이게 합니다.
        bool faceLeft = leftVisual != null && leftVisual.gameObject.activeSelf;
        if (weaponRenderer != null && weaponRenderer.enabled)
        {
            // [보스 주문 빛 위치] 투명 여백이 포함된 sprite bounds 대신 손에 연결된 무기 피벗을 기준으로 배치합니다.
            float weaponDirection = faceLeft ? -1f : 1f;
            Vector3 weaponPivot = weaponRenderer.transform.position;
            effect.position = new Vector3(
                weaponPivot.x + weaponDirection * 0.32f,
                weaponPivot.y + 0.08f,
                transform.position.z);
            return;
        }

        float direction = faceLeft ? -1f : 1f;
        effect.position = transform.position + new Vector3(direction * 1.45f, 1.05f, 0f);
    }

    private void FacePlayer()
    {
        if (player == null)
            return;

        bool faceLeft = player.position.x < transform.position.x;
        FaceDirection(faceLeft);
    }

    private void FacePosition(float positionX)
    {
        FaceDirection(positionX < transform.position.x);
    }

    private void FaceDirection(bool faceLeft)
    {
        if (IsSpawned && IsServer)
            syncedFaceLeft.Value = faceLeft;

        ApplySyncedFaceLeft(faceLeft);
    }

    private void OnSyncedFaceLeftChanged(bool previousValue, bool newValue)
    {
        if (IsServer)
            return;

        ApplySyncedFaceLeft(newValue);
    }

    private void ApplySyncedFaceLeft(bool faceLeft)
    {
        // [Codex Boss Direction Sync] 서버가 바라보는 방향을 클라이언트의 Left/Right 비주얼에도 적용합니다.
        if (leftVisual != null)
            leftVisual.gameObject.SetActive(faceLeft);
        if (rightVisual != null)
            rightVisual.gameObject.SetActive(!faceLeft);
    }

    private void SetMoving(bool moving)
    {
        CharacterState desiredState = moving ? CharacterState.Run : CharacterState.Idle;
        bool animatorAlreadySynced = animationManager != null &&
            animationManager.Animator != null &&
            animationManager.Animator.GetInteger("State") == (int)desiredState;

        // [보스 첫 접근 애니메이션] Animator 초기화가 첫 Run 값을 되돌려도 실제 파라미터가 다르면 다시 동기화합니다.
        if (wasMoving == moving && animatorAlreadySynced)
            return;

        wasMoving = moving;
        if (IsSpawned && IsServer)
            syncedMoveState.Value = (int)desiredState;

        if (animationManager != null)
            animationManager.SetState(desiredState);
    }

    private void OnSyncedMoveStateChanged(int previousValue, int newValue)
    {
        if (IsServer)
            return;

        ApplySyncedMoveState(newValue);
    }

    private void ApplySyncedMoveState(int stateValue)
    {
        CharacterState state = (CharacterState)stateValue;
        wasMoving = state == CharacterState.Run;

        // [Codex Boss Move Sync] 서버에서 결정한 보스 이동 애니메이션 상태를 클라이언트 보스에도 적용합니다.
        if (animationManager != null)
            animationManager.SetState(state);
    }

    private void FindPlayer()
    {
        // [Codex Boss Network Target Refresh] 네트워크 씬에서는 PlayerObject가 나중에 Spawn되거나 대상이 바뀔 수 있어 매번 가장 가까운 플레이어를 다시 잡습니다.
        Transform nearestNetworkPlayer = FindNearestNetworkPlayer();
        if (nearestNetworkPlayer != null)
        {
            player = nearestNetworkPlayer;
            FacePlayer();
            return;
        }

        if (player != null && player.gameObject.activeInHierarchy)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            FacePlayer();
        }
    }

    private void FaceNearestPlayer()
    {
        Transform nearestNetworkPlayer = FindNearestNetworkPlayer();
        if (nearestNetworkPlayer != null)
        {
            player = nearestNetworkPlayer;
            FacePlayer();
            return;
        }

        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        if (playerObjects == null || playerObjects.Length == 0)
            return;

        Transform nearestPlayer = null;
        float nearestSqrDistance = float.PositiveInfinity;
        Vector3 bossPosition = transform.position;

        for (int i = 0; i < playerObjects.Length; i++)
        {
            if (playerObjects[i] == null)
                continue;

            Transform candidate = playerObjects[i].transform;
            float sqrDistance = (candidate.position - bossPosition).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            nearestPlayer = candidate;
        }

        if (nearestPlayer == null)
            return;

        player = nearestPlayer;
        FacePlayer();
    }

    private Transform FindNearestNetworkPlayer()
    {
        // [Codex Boss Network Follow] 네트워크 캐릭터 프리팹 루트가 Untagged여도 PlayerObject 기준으로 추적 대상을 찾습니다.
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
            return null;

        Transform nearestPlayer = null;
        float nearestSqrDistance = float.PositiveInfinity;
        Vector3 bossPosition = transform.position;

        foreach (NetworkClient client in manager.ConnectedClientsList)
        {
            if (client == null || client.PlayerObject == null || !client.PlayerObject.IsSpawned)
                continue;

            Transform candidate = client.PlayerObject.transform;
            float sqrDistance = (candidate.position - bossPosition).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            nearestPlayer = candidate;
        }

        return nearestPlayer;
    }

    private bool DisableDuplicateControllerIfNeeded()
    {
        // [Codex Boss Duplicate AI Guard] CombatController 중복 실행은 서로 속도/스킬 상태를 덮어 제자리걸음처럼 보일 수 있어 하나만 남깁니다.
        GoblinBossCombatController2D[] controllers = GetComponents<GoblinBossCombatController2D>();
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] == null || controllers[i] == this)
                continue;

            if (!controllers[i].enabled)
                continue;

            disabledAsDuplicateController = true;
            enabled = false;
            return true;
        }

        return false;
    }

    private void ClearLegacyShieldDisableFlag()
    {
        // [Codex Boss Shield Toggle] 예전 네트워크 테스트용 비활성화 값이 남아 있어도 새 enablePowerShotShield 토글 동작을 막지 않게 런타임에서만 해제합니다.
        if (disablePowerShotShieldForNetworkTest)
            disablePowerShotShieldForNetworkTest = false;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GoblinBossCast_RuntimeSprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / 3f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateShieldPlateSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GoblinBossShieldPlate_RuntimeSprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float ny = 1f - (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size - 0.5f) * 2f;
                float topFade = Mathf.Clamp01((ny - 0.08f) * 34f);
                float bottomFade = Mathf.Clamp01((0.98f - ny) * 28f);
                float halfWidth;

                if (ny < 0.18f)
                {
                    halfWidth = Mathf.Lerp(0.08f, 0.5f, Mathf.SmoothStep(0f, 1f, ny / 0.18f));
                }
                else if (ny < 0.42f)
                {
                    float t = (ny - 0.18f) / 0.24f;
                    halfWidth = Mathf.Lerp(0.5f, 0.62f, Mathf.SmoothStep(0f, 1f, t));
                }
                else if (ny < 0.78f)
                {
                    float t = (ny - 0.42f) / 0.36f;
                    halfWidth = Mathf.Lerp(0.62f, 0.56f, Mathf.SmoothStep(0f, 1f, t));
                }
                else
                {
                    float t = (ny - 0.78f) / 0.2f;
                    halfWidth = Mathf.Lerp(0.56f, 0.46f, Mathf.SmoothStep(0f, 1f, t));
                }

                float shoulderRound = Mathf.Clamp01((0.92f - ny) * 12f + Mathf.Clamp01((0.64f - Mathf.Abs(nx)) * 8f));
                float fill = Mathf.Clamp01((halfWidth - Mathf.Abs(nx)) * 38f) * shoulderRound;
                float borderDistance = Mathf.Abs(Mathf.Abs(nx) - halfWidth);
                float rim = Mathf.Clamp01((0.05f - borderDistance) * 32f);
                float crownRim = Mathf.Clamp01((0.12f - Mathf.Abs(ny - 0.86f)) * 20f) * Mathf.Clamp01((0.5f - Mathf.Abs(nx)) * 12f);
                float centerRidge = Mathf.Clamp01((0.05f - Mathf.Abs(nx)) * 22f) * Mathf.Clamp01((ny - 0.12f) * 2.4f);
                float innerGlow = Mathf.Clamp01((halfWidth * 0.72f - Mathf.Abs(nx)) * 7f) * Mathf.Clamp01((0.78f - Mathf.Abs(ny - 0.52f)) * 2.7f);
                float alpha = fill * topFade * bottomFade;
                float detail = 0.48f + rim * 0.4f + crownRim * 0.28f + centerRidge * 0.3f + innerGlow * 0.18f;

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha * detail));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite CreateShieldCoreSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "GoblinBossShieldCore_RuntimeSprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            float ny = Mathf.Abs(((y + 0.5f) / size - 0.5f) * 2f);
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs(((x + 0.5f) / size - 0.5f) * 2f);
                float vertical = Mathf.Clamp01((0.18f - nx) * 12f);
                float cap = Mathf.Clamp01((0.92f - ny) * 8f);
                float cross = Mathf.Clamp01((0.11f - Mathf.Abs(ny - 0.18f)) * 16f) * Mathf.Clamp01((0.58f - nx) * 6f);
                float alpha = Mathf.Clamp01(vertical * cap + cross * 0.7f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
