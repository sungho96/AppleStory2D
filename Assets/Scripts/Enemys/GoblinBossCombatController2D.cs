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

    [Header("Close Counter Attack")]
    [SerializeField, Range(0f, 1f)] private float closeCounterChance = 0.3f;
    [SerializeField] private float closeCounterRange = 3.2f;
    [SerializeField] private float closeCounterWindup = 0.22f;
    [SerializeField] private float closeCounterRecovery = 0.28f;
    [SerializeField] private float closeCounterRetryInterval = 1.2f;
    [SerializeField] private Vector2 closeCounterKnockback = new Vector2(8f, 2.5f);

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
    private Transform castingArm;
    private Transform weaponArm;
    private Vector3 castingArmBasePosition;
    private Quaternion castingArmBaseRotation;
    private Vector3 weaponArmBasePosition;
    private Quaternion weaponArmBaseRotation;
    private float castingMotionTimer;
    private bool castingFaceLeft;
    private float nextCloseCounterAttemptTime;

    public bool IsCasting => isCasting;

    /// <summary>
    /// 플레이어가 공격 범위에 들어왔을 때 일반공격 발동을 시도합니다.
    /// </summary>
    public void TryCloseCounterAttack(Transform attacker)
    {
        // [보스 근접 일반공격 추가] 거리와 Inspector 확률을 통과하면 보스가 직접 일반공격을 시작합니다.
        if (attacker == null || isCasting || (health != null && health.IsDead))
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

        yield return new WaitForSeconds(Mathf.Max(0f, closeCounterWindup));

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

        // [보스 이동] 보스 인스턴스에서는 일반 고블린의 랜덤 순찰을 사용하지 않습니다.
        GoblinController2D normalController = GetComponent<GoblinController2D>();
        if (normalController != null)
            normalController.enabled = false;
    }

    private void Update()
    {
        FindPlayer();

        if (health != null && health.IsDead)
        {
            moveDirection = 0f;
            return;
        }

        if (player == null)
            return;

        FacePlayer();

        if (isCasting)
        {
            SetMoving(false);
            moveDirection = 0f;
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

        float horizontalVelocity = isCasting ? 0f : moveDirection * approachSpeed;
        rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
    }

    private void LateUpdate()
    {
        if (!isCasting || castingArm == null)
            return;

        // [보스 주문 애니메이션] Animator의 공격 자세를 먼저 초기화한 뒤 무기를 든 팔에 전용 주문 자세를 적용합니다.
        if (weaponArm != null)
        {
            weaponArm.localPosition = weaponArmBasePosition;
            weaponArm.localRotation = weaponArmBaseRotation;
        }

        // [보스 주문 애니메이션] Animator가 파츠 자세를 계산한 뒤 팔 전체를 들어 올려 주문 동작이 덮어써지지 않게 합니다.
        float liftRatio = Mathf.Clamp01(castingMotionTimer / 0.18f);
        float shake = Mathf.Sin(Mathf.Min(castingMotionTimer, 1f) * 18f) * 7f;
        float raisedAngle = castingFaceLeft ? -55f : 55f;
        castingArm.localPosition = castingArmBasePosition + Vector3.up * (0.22f * liftRatio);
        castingArm.localRotation = castingArmBaseRotation * Quaternion.Euler(0f, 0f, (raisedAngle + shake) * liftRatio);
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
            player = playerObject.transform;
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
}
