using System.Collections;
using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;

/// <summary>
/// 고블린 보스 전용 추적, 방향 전환, 스킬 시전 연출을 담당합니다.
/// </summary>
public class GoblinBossCombatController2D : MonoBehaviour
{
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
    [SerializeField] private bool disablePowerShotShieldForNetworkTest = true;
    [SerializeField] private float shieldFirstDelay = 7f;
    [SerializeField] private Vector2 shieldPhaseOneCooldownRange = new Vector2(12f, 16f);
    [SerializeField] private Vector2 shieldPhaseTwoCooldownRange = new Vector2(16f, 22f);
    [SerializeField, Range(0f, 1f)] private float shieldRequiredPowerShotRatio = 0.5f;
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
    private int shieldBreakHits;
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

    public bool IsCasting => isIntroLocked || isCasting || isShieldBlocking || isGroggy || isHealCasting;
    public float CurrentDamageMultiplier => isGroggy ? Mathf.Max(1f, shieldGroggyDamageMultiplier) : 1f;

    public void SetIntroLocked(bool locked)
    {
        // [Codex Boss Intro Lock] 카메라 인트로 동안 보스 이동/공격 패턴만 잠시 멈추고 기존 패턴 구조는 그대로 둡니다.
        isIntroLocked = locked;
        if (!locked)
            return;

        moveDirection = 0f;
        SetMoving(false);

        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    /// <summary>
    /// 플레이어가 공격 범위에 들어왔을 때 일반공격 발동을 시도합니다.
    /// </summary>
    public void TryCloseCounterAttack(Transform attacker)
    {
        // [보스 근접 일반공격 추가] 거리와 Inspector 확률을 통과하면 보스가 직접 일반공격을 시작합니다.
        if (attacker == null || isIntroLocked || isCasting || isShieldBlocking || isGroggy || (health != null && health.IsDead))
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

        if (rb != null)
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
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<GoblinHealth2D>();
        animationManager = GetComponent<AnimationManager>();
        leftVisual = transform.Find("Left");
        rightVisual = transform.Find("Right");
        leftVisualBaseScale = leftVisual != null ? leftVisual.localScale : Vector3.one;
        rightVisualBaseScale = rightVisual != null ? rightVisual.localScale : Vector3.one;
        nextJumpMoveTime = Time.time + Mathf.Max(0f, jumpMoveFirstDelay);
        // Codex: Temporarily disable the defensive shield while testing the network boss room.
        nextShieldTime = disablePowerShotShieldForNetworkTest
            ? float.PositiveInfinity
            : Time.time + Mathf.Max(0f, shieldFirstDelay);

        // [보스 이동] 보스 인스턴스에서는 일반 고블린의 랜덤 순찰을 사용하지 않습니다.
        GoblinController2D normalController = GetComponent<GoblinController2D>();
        if (normalController != null)
            normalController.enabled = false;
    }

    private IEnumerator Start()
    {
        // [Codex Boss Start Facing] 씬 로드/네트워크 Spawn 직후 플레이어 위치가 잡힌 다음 기존 Left/Right 방식으로 시작 방향을 맞춥니다.
        yield return null;
        FaceNearestPlayer();
    }

    private void Update()
    {
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

        FacePlayer();

        if (isCasting || isShieldBlocking || isGroggy || isHealCasting)
        {
            // [Codex Boss Shield Groggy] During groggy, keep ClimbU instead of overwriting it with Idle every frame.
            if (!isGroggy)
                SetMoving(false);
            moveDirection = 0f;
            return;
        }

        if (ShouldStartHealCast())
        {
            StartCoroutine(CoHealCast());
            return;
        }

        if (!disablePowerShotShieldForNetworkTest && Time.time >= nextShieldTime)
        {
            shieldRoutine = StartCoroutine(CoShieldBlock());
            return;
        }

        if (Time.time >= nextJumpMoveTime)
        {
            StartCoroutine(CoJumpMove());
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
        if (rb == null)
            return;

        float horizontalVelocity = (isIntroLocked || isCasting || isShieldBlocking || isGroggy || isHealCasting) ? 0f : moveDirection * approachSpeed;
        rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
    }

    private void LateUpdate()
    {
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

    public bool TryHandleShieldDamage(float powerShotChargeRatio, float hitDir)
    {
        // Codex: When the temporary test flag is on, PowerShot Shield never blocks incoming damage.
        if (disablePowerShotShieldForNetworkTest)
            return false;

        if (!isShieldBlocking)
            return false;

        bool validPowerShot = powerShotChargeRatio >= shieldRequiredPowerShotRatio;
        if (!validPowerShot)
        {
            PlayShieldBlockedFeedback(hitDir);
            return true;
        }

        shieldBreakHits++;
        PlayShieldCrackFeedback(shieldBreakHits);
        if (shieldBreakHits >= RequiredShieldBreakHits())
        {
            if (shieldRoutine != null)
                StopCoroutine(shieldRoutine);
            shieldRoutine = StartCoroutine(CoShieldBreakGroggy());
        }

        return true;
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
        if (disablePowerShotShieldForNetworkTest)
            yield break;

        // [Codex Boss Shield Break] ShieldBlockU stays up until a valid 50%+ PowerShot breaks it.
        isShieldBlocking = true;
        isCasting = true;
        shieldBreakHits = 0;
        moveDirection = 0f;
        SetMoving(false);
        FacePlayer();

        if (rb != null)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (animationManager != null && animationManager.Animator != null)
            animationManager.Animator.CrossFade(shieldBlockStateName, 0.08f, 0);

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

        float jumpTimer = 0f;
        float jumpDuration = Mathf.Max(0.05f, jumpMoveDuration * 0.82f);
        while (jumpTimer < jumpDuration)
        {
            jumpTimer += Time.fixedDeltaTime;
            float ratio = Mathf.Clamp01(jumpTimer / jumpDuration);
            Vector2 nextPosition = Vector2.Lerp(startPosition, landingPosition, ratio);
            nextPosition.y += Mathf.Sin(ratio * Mathf.PI) * (jumpMoveHeight * 0.72f);

            if (rb != null)
                rb.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            yield return new WaitForFixedUpdate();
        }

        if (rb != null)
        {
            rb.MovePosition(landingPosition);
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = landingPosition;
        }

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

    private void EndShieldBlock(bool broken)
    {
        isShieldBlocking = false;
        isCasting = false;
        isGroggy = false;
        shieldBreakHits = 0;
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

    private int RequiredShieldBreakHits()
    {
        return health != null && health.HpRatio <= 0.5f ? 2 : 1;
    }

    private void ScheduleNextShield()
    {
        if (disablePowerShotShieldForNetworkTest)
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

    private void PlayShieldCrackFeedback(int hitCount)
    {
        UpdateShieldEffect(Time.time, hitCount >= RequiredShieldBreakHits() ? 1f : 0.84f);
        int shardCount = hitCount >= RequiredShieldBreakHits() ? 8 : 5;
        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = new GameObject("GoblinBoss_ShieldCrack");
            SpriteRenderer renderer = shard.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateCircleSprite(16);
            renderer.color = new Color(shieldBreakColor.r, shieldBreakColor.g, shieldBreakColor.b, 0.82f);
            renderer.sortingOrder = 26;
            float angle = ((360f / shardCount) * i + hitCount * 23f) * Mathf.Deg2Rad;
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
        if (castRoutine != null)
            StopCoroutine(castRoutine);

        castRoutine = StartCoroutine(CoFallingCast(Mathf.Max(0.2f, duration), castEffectColor));
    }

    public void BeginIceCast(float duration)
    {
        if (castRoutine != null)
            StopCoroutine(castRoutine);

        // [얼음 파도 시전색] 무기 앞 주문 빛을 청백색으로 바꿔 메테오 시전과 즉시 구분되게 합니다.
        Color iceCastColor = new Color(0.2f, 0.88f, 1f, 0.68f);
        castRoutine = StartCoroutine(CoFallingCast(Mathf.Max(0.2f, duration), iceCastColor));
    }

    private IEnumerator CoJumpMove()
    {
        // [Codex Boss Jump Move] Locks follow and other boss attacks while this standalone jump move runs.
        isCasting = true;
        moveDirection = 0f;
        SetMoving(false);
        FacePlayer();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
        if (animationManager != null)
            animationManager.SetState(CharacterState.Idle);

        yield return CoJumpCrouch();

        Transform targetPlayer = PickRandomPlayer();
        if (targetPlayer == null)
            targetPlayer = player;

        Vector2 startPosition = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 landingPosition = CalculateJumpLandingPosition(targetPlayer, startPosition.y);
        FacePosition(landingPosition.x);

        float timer = 0f;
        float duration = Mathf.Max(0.05f, jumpMoveDuration);
        while (timer < duration)
        {
            timer += Time.fixedDeltaTime;
            float ratio = Mathf.Clamp01(timer / duration);
            Vector2 nextPosition = Vector2.Lerp(startPosition, landingPosition, ratio);
            nextPosition.y += Mathf.Sin(ratio * Mathf.PI) * jumpMoveHeight;

            if (rb != null)
                rb.MovePosition(nextPosition);
            else
                transform.position = nextPosition;

            yield return new WaitForFixedUpdate();
        }

        if (rb != null)
        {
            rb.MovePosition(landingPosition);
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            transform.position = landingPosition;
        }

        FacePlayer();
        yield return new WaitForSeconds(Mathf.Max(0f, jumpLandingDelay));

        nextJumpMoveTime = Time.time + Mathf.Max(0.1f, jumpMoveCooldown);
        isCasting = false;
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

    private Transform PickRandomPlayer()
    {
        // [Codex Boss Jump Move] Supports two players by choosing one Player-tagged target at random.
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        if (playerObjects == null || playerObjects.Length == 0)
            return null;

        return playerObjects[Random.Range(0, playerObjects.Length)].transform;
    }

    private Vector2 CalculateJumpLandingPosition(Transform targetPlayer, float landingY)
    {
        if (targetPlayer == null)
            return new Vector2(transform.position.x, landingY);

        // [Codex Boss Jump Move] Land on the opposite side of the chosen player so the pattern reads clearly.
        float bossToPlayer = targetPlayer.position.x - transform.position.x;
        float side = Mathf.Approximately(bossToPlayer, 0f) ? 1f : Mathf.Sign(bossToPlayer);
        float targetX = targetPlayer.position.x + side * jumpLandingDistanceFromPlayer;
        targetX = Mathf.Clamp(targetX, arenaMinX, arenaMaxX);
        return new Vector2(targetX, landingY);
    }

    private void ApplyJumpSquash(float scaleX, float scaleY)
    {
        if (leftVisual != null)
            leftVisual.localScale = new Vector3(leftVisualBaseScale.x * scaleX, leftVisualBaseScale.y * scaleY, leftVisualBaseScale.z);
        if (rightVisual != null)
            rightVisual.localScale = new Vector3(rightVisualBaseScale.x * scaleX, rightVisualBaseScale.y * scaleY, rightVisualBaseScale.z);
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
        if (animationManager != null)
            animationManager.SetState(desiredState);
    }

    private void FindPlayer()
    {
        if (player != null)
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
