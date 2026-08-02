using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerAttack2D : NetworkBehaviour
{
    [Header("Attack")]
    [SerializeField] private Animator animator;              // ??諛쒖궗 ?좊땲硫붿씠???쒖뼱??
    [SerializeField] private GameObject arrowPrefab;         // 諛쒖궗???붿궡 ?꾨━??
    [SerializeField] private Transform firePoint;            // ?붿궡 ?앹꽦 ?꾩튂
    [SerializeField] private float arrowSpeed = 12f;         // ?붿궡 ?띾룄
    [SerializeField] private PlayerController2D playerController; // 諛붾씪蹂대뒗 諛⑺뼢 李몄“??

    [Header("Attack Speed")]
    [SerializeField] private float baseAttackDelay = 0.4f;

    [Header("Power Shot")]
    [SerializeField] private float powerShotMaxChargeDuration = 1.5f;
    [SerializeField] private int powerShotMinDamage = 18;
    [SerializeField] private int powerShotMaxDamage = 45;
    [SerializeField] private float powerShotMinSpeed = 14f;
    [SerializeField] private float powerShotMaxSpeed = 20f;
    [SerializeField] private float powerShotMinScale = 0.95f;
    [SerializeField] private float powerShotMaxScale = 1.45f;

    [Header("Power Shot Audio")]
    [SerializeField] private AudioClip powerShotChargeSound;
    [SerializeField] private AudioClip powerShotReleaseSound;
    [SerializeField, Range(0f, 1f)] private float powerShotChargeVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float powerShotReleaseVolume = 0.85f;

    [Header("Rapid Volley")]
    [SerializeField] private int rapidVolleyDamage = 8;
    [SerializeField] private float rapidVolleyArrowSpeed = 16f;
    [SerializeField] private float rapidVolleyArrowScale = 0.9f;

    [Header("Rapid Volley Audio")]
    [SerializeField] private AudioClip rapidVolleyShotSound;
    [SerializeField, Range(0f, 1f)] private float rapidVolleyShotVolume = 0.8f;

    private float attackSpeedMultiplier = 1f;

    private bool isAttacking; // ?고? 諛⑹? (怨듦꺽 以?異붽? ?낅젰 李⑤떒 ?뚮옒洹?
    private bool isPowerShotCharging;
    private bool hasPendingPowerShot;
    private float powerShotChargeStartedAt;
    private int pendingPowerShotDamage;
    private float pendingPowerShotSpeed;
    private float pendingPowerShotScale;
    private float pendingPowerShotRatio;
    private bool ignoreNormalShotBowEvent;
    private PowerShotChargeGauge powerShotChargeGauge;
    private PowerShotVisualFeedback powerShotVisualFeedback;
    private PowerShotScreenFeedback powerShotScreenFeedback;
    private PowerShotLimbMotion powerShotLimbMotion;
    private PlayerLadder2D ladder;
    private CameraShake2D cameraShake;
    private Coroutine powerShotChargeMotionCoroutine;
    private Transform powerShotMovingVisual;
    private Vector3 powerShotVisualOriginalPosition;
    private Vector3 powerShotVisualOriginalScale;
    private Quaternion powerShotVisualOriginalRotation;
    private AudioSource powerShotAudioSource;
    private bool isRapidVolleyAttacking;
    private Transform rapidVolleyMovingVisual;
    private Vector3 rapidVolleyVisualOriginalPosition;
    private Vector3 rapidVolleyVisualOriginalScale;
    private Vector3 rapidVolleyVisualBasePosition;
    private Vector3 rapidVolleyVisualLocalOffset;
    private int rapidVolleyFiredMask;
    private RapidVolleyVisualFeedback rapidVolleyVisualFeedback;
    private RapidVolleyScreenFeedback rapidVolleyScreenFeedback;
    private float rapidVolleyDirection = 1f;

    /// <summary>
    /// 珥덇린 李몄“ ?ㅼ젙.
    /// - PlayerController2D媛 鍮꾩뼱 ?덉쓣 寃쎌슦 ?먮룞?쇰줈 媛숈? ?ㅻ툕?앺듃?먯꽌 媛?몄샂
    /// - 怨듦꺽 諛⑺뼢 怨꾩궛(GetHorizontalFacingDir)???ъ슜??
    /// </summary>
    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
        ladder = GetComponent<PlayerLadder2D>();

        // [?뚯썙 ??寃뚯씠吏 異붽?] ??李몄“ ?놁씠 ?뚮젅?댁뼱??寃뚯씠吏瑜??먮룞 援ъ꽦?⑸땲??
        powerShotChargeGauge = GetComponent<PowerShotChargeGauge>();
        if (powerShotChargeGauge == null)
            powerShotChargeGauge = gameObject.AddComponent<PowerShotChargeGauge>();
        powerShotChargeGauge.Initialize();

        // [?뚯썙 ???곗텧 異붽?] 李⑥쭠 ?ㅻ씪? 諛쒖궗 ?ш킅???고??꾩쑝濡?援ъ꽦?⑸땲??
        powerShotVisualFeedback = GetComponent<PowerShotVisualFeedback>();
        if (powerShotVisualFeedback == null)
            powerShotVisualFeedback = gameObject.AddComponent<PowerShotVisualFeedback>();
        powerShotVisualFeedback.Initialize(firePoint, playerController);
        cameraShake = FindFirstObjectByType<CameraShake2D>();
        if (cameraShake == null && Camera.main != null)
            cameraShake = Camera.main.gameObject.AddComponent<CameraShake2D>();

        // [?뚯썙 ???붾㈃ ?꾩쿂由? 蹂꾨룄 ?⑦궎吏 ?놁씠 移대찓??以뙿룻뵆?섏떆쨌鍮꾨꽕?몃? ?먮룞 ?곌껐?⑸땲??
        if (Camera.main != null)
        {
            powerShotScreenFeedback = Camera.main.GetComponent<PowerShotScreenFeedback>();
            if (powerShotScreenFeedback == null)
                powerShotScreenFeedback = Camera.main.gameObject.AddComponent<PowerShotScreenFeedback>();
            powerShotScreenFeedback.Initialize(Camera.main);

            // [?섑뵾??蹂쇰━ ?꾩쿂由? 蹂꾨룄 ?⑦궎吏 ?놁씠 移대찓?쇱뿉 3?곗궗 ?뚮옒?쒖? 以??곗텧???곌껐?⑸땲??
            rapidVolleyScreenFeedback = Camera.main.GetComponent<RapidVolleyScreenFeedback>();
            if (rapidVolleyScreenFeedback == null)
                rapidVolleyScreenFeedback = Camera.main.gameObject.AddComponent<RapidVolleyScreenFeedback>();
            rapidVolleyScreenFeedback.Initialize(Camera.main);
        }

        // [?뚯썙 ???붾떎由??좊땲硫붿씠?? ?먮낯 ?대┰??蹂댁〈?섍퀬 愿??蹂댁“ ?숈옉???먮룞 ?곌껐?⑸땲??
        powerShotLimbMotion = GetComponent<PowerShotLimbMotion>();
        if (powerShotLimbMotion == null)
            powerShotLimbMotion = gameObject.AddComponent<PowerShotLimbMotion>();
        powerShotLimbMotion.Initialize(playerController);

        // [?뚯썙 ???ъ슫???곌껐] ?ㅻⅨ ?뚮젅?댁뼱 ?ъ슫?쒖? ?욎씠吏 ?딅뒗 ?꾩슜 AudioSource瑜??ъ슜?⑸땲??
        powerShotAudioSource = gameObject.AddComponent<AudioSource>();
        powerShotAudioSource.playOnAwake = false;
        powerShotAudioSource.spatialBlend = 0f;

        // [?섑뵾??蹂쇰━ ?댄럺?? ??李몄“ ?놁씠 罹먮┃??二쇰? ?곗텧???먮룞 援ъ꽦?⑸땲??
        rapidVolleyVisualFeedback = GetComponent<RapidVolleyVisualFeedback>();
        if (rapidVolleyVisualFeedback == null)
            rapidVolleyVisualFeedback = gameObject.AddComponent<RapidVolleyVisualFeedback>();
        rapidVolleyVisualFeedback.Initialize();
    }

    /// <summary>
    /// ?낅젰 泥섎━ 猷⑦봽.
    /// - LeftControl ?낅젰 ??怨듦꺽 肄붾（???ㅽ뻾
    /// - isAttacking???듯빐 ?곗냽 ?낅젰(?ㅽ뙵 怨듦꺽) 諛⑹?
    /// </summary>
    private void Update()
    {
        if (isPowerShotCharging)
        {
            bool jumpStarted = Input.GetButtonDown("Jump");
            bool airborne = ladder != null && !ladder.IsGrounded;
            if (jumpStarted || airborne)
            {
                CancelPowerShotCharge();
                return;
            }

            float chargeRatio = (Time.time - powerShotChargeStartedAt) /
                                Mathf.Max(0.01f, powerShotMaxChargeDuration);
            powerShotChargeGauge.SetProgress(chargeRatio);
            powerShotVisualFeedback.SetChargeProgress(chargeRatio);
            powerShotLimbMotion?.SetChargeProgress(chargeRatio);
        }

        if (Input.GetKeyDown(KeyCode.LeftControl) &&
            !isAttacking &&
            !isPowerShotCharging)
        {
            StartCoroutine(DoBowShot());
        }
    }

    public void UseRapidVolley()
    {
        if (isAttacking || isPowerShotCharging || isRapidVolleyAttacking)
            return;

        StartCoroutine(RapidVolleyRoutine());
    }

    private IEnumerator RapidVolleyRoutine()
    {
        // [?섑뵾??蹂쇰━ ?꾩슜 ?좊땲硫붿씠?? ?붿궡? Animation Event ??怨녹뿉???앹꽦?⑸땲??
        isRapidVolleyAttacking = true;
        isAttacking = true;
        rapidVolleyFiredMask = 0;
        Transform rapidVolleyStartVisual = GetActiveDirectionVisual();
        rapidVolleyVisualBasePosition = rapidVolleyStartVisual != null
            ? rapidVolleyStartVisual.localPosition
            : Vector3.zero;
        rapidVolleyVisualLocalOffset = Vector3.zero;
        rapidVolleyVisualFeedback?.SetRapidVolleyWorldOffset(Vector3.zero);
        rapidVolleyDirection = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;
        // [?섑뵾??蹂쇰━ 諛⑺뼢 怨좎젙] ?쒖옉 諛⑺뼢????ν븯怨???諛쒖씠 ?앸궇 ?뚭퉴吏 罹먮┃???꾪솚???좉툒?덈떎.
        playerController?.LockHorizontalFacing(rapidVolleyDirection);
        rapidVolleyVisualFeedback?.PlayCastEffect(
            rapidVolleyDirection,
            GetRapidVolleyVisualWorldOffset());
        yield return null;
        animator.speed = 1f;

        // [?섑뵾??蹂쇰━ ?곌껐 ?덉젙?? Trigger? ?④퍡 ?ㅼ젣 Lower State 吏꾩엯??蹂댁옣?⑸땲??
        // [?섑뵾??蹂쇰━ ?섏젙] ?꾩떊 怨듦꺽??Complex ?덉씠?댁쓽 ?곹깭瑜?吏곸젒 ?ъ깮?⑸땲??
        // [Codex RapidVolley 마지막 복구] BowShot 기반 상체 애니메이션은 Upper 레이어에서 직접 재생합니다.
        int upperLayer = animator.GetLayerIndex("Upper");
        int rapidVolleyStateHash = Animator.StringToHash("Upper.RapidVolley");
        bool canForceUpperRapidVolley =
            upperLayer >= 0 && animator.HasState(upperLayer, rapidVolleyStateHash);
        if (!canForceUpperRapidVolley && upperLayer >= 0)
        {
            rapidVolleyStateHash = Animator.StringToHash("RapidVolley");
            canForceUpperRapidVolley = animator.HasState(upperLayer, rapidVolleyStateHash);
        }
        if (canForceUpperRapidVolley)
        {
            animator.CrossFade(rapidVolleyStateHash, 0.01f, upperLayer, 0f);
        }

        // [?섑뵾??蹂쇰━ ?섏젙] ?붿쓣 ?щ┛ ?먯꽭瑜??좎??섎뒗 ?숈븞 ??諛쒖쓣 諛쒖궗?⑸땲??
        float[] eventTimes = { 0.22f, 0.34f, 0.46f };
        const float rapidVolleyHoldStartTime = 0.14f;
        const float rapidVolleyReleaseStartTime = 0.5f;
        const float rapidVolleyEndDelay = 0.72f;
        bool rapidVolleyReleaseStarted = false;
        float elapsed = 0f;
        while (elapsed < rapidVolleyEndDelay)
        {
            elapsed += Time.deltaTime;

            // [?섑뵾??蹂쇰━ 諛쒖궗 ?덉쟾?μ튂] ?대깽?멸? ?꾨씫??諛쒕쭔 媛쒕퀎?곸쑝濡?蹂듦뎄?⑸땲??
            for (int shotIndex = 0; shotIndex < eventTimes.Length; shotIndex++)
            {
                int shotBit = 1 << shotIndex;
                if (elapsed >= eventTimes[shotIndex] &&
                    (rapidVolleyFiredMask & shotBit) == 0)
                {
                    FireRapidVolleyAnimationEvent(shotIndex);
                }
            }

            if (canForceUpperRapidVolley)
            {
                if (elapsed >= rapidVolleyHoldStartTime && elapsed < rapidVolleyReleaseStartTime)
                {
                    // [Codex RapidVolley 팔 유지 복구] 첫 발이 나가기 전부터 BowShot의 당긴 프레임을 고정해 끝나기 전까지 팔을 내리지 않습니다.
                    animator.Play(rapidVolleyStateHash, upperLayer, 0.21f);
                }
                else if (!rapidVolleyReleaseStarted && elapsed >= rapidVolleyReleaseStartTime)
                {
                    // [Codex RapidVolley 애니메이션 복구] 세 번째 발 이후에는 활 내리는 구간으로 넘겨 마무리합니다.
                    animator.Play(rapidVolleyStateHash, upperLayer, 0.76f);
                    rapidVolleyReleaseStarted = true;
                }
            }

            yield return null;
        }

        isRapidVolleyAttacking = false;
        isAttacking = false;
        playerController?.UnlockHorizontalFacing();
    }

    public void FireRapidVolleyAnimationEvent(int shotIndex)
    {
        if (!isRapidVolleyAttacking || shotIndex < 0 || shotIndex > 2)
            return;

        int shotBit = 1 << shotIndex;
        if ((rapidVolleyFiredMask & shotBit) != 0)
            return;

        rapidVolleyFiredMask |= shotBit;

        // [?섑뵾??蹂쇰━ ?꾩슜 ?좊땲硫붿씠?? ?쒖떆?꾧? ?由щ뒗 ?ㅼ젣 ?ㅽ봽?덉엫?먯꽌 諛쒖궗?⑸땲??
        // [Codex RapidVolley 마지막 복구] 3발마다 흔들지 않고 첫 발과 마지막 발에만 몸 반동을 줍니다.
        if (shotIndex == 0 || shotIndex == 2)
            StartCoroutine(PlayRapidVolleyBodyKick(shotIndex));
        SpawnRapidVolleyArrow(shotIndex);
        PlayRapidVolleyShotSound();
        float shotDirection = rapidVolleyDirection;
        rapidVolleyVisualFeedback?.PlayShotEffect(
            shotIndex,
            shotDirection,
            GetRapidVolleyVisualWorldOffset());
        rapidVolleyScreenFeedback?.PlayShot(shotIndex);

        // [?섑뵾??蹂쇰━ ?꾩쿂由? 留?諛쒖쓽 諛섎룞??蹂댁뿬 二쇰릺 ??踰덉㎏ 諛쒕쭔 ?뺤떎?섍쾶 媛뺥빐吏묐땲??
        float shakeStrength = shotIndex == 2 ? 0.055f : 0.022f + shotIndex * 0.008f;
        cameraShake?.Shake(shotIndex == 2 ? 0.1f : 0.055f, shakeStrength);
    }

    private void PlayRapidVolleyShotSound()
    {
        // [?섑뵾??蹂쇰━ ?ъ슫?? ?붿궡???앹꽦????媛숈? ?대┰????踰덉뵫, 珥???踰??ъ깮?⑸땲??
        if (powerShotAudioSource == null || rapidVolleyShotSound == null)
            return;

        powerShotAudioSource.PlayOneShot(
            rapidVolleyShotSound,
            rapidVolleyShotVolume);
    }

    private IEnumerator PlayRapidVolleyBodyKick(int shotIndex)
    {
        Transform visualRoot = GetActiveDirectionVisual();
        if (visualRoot == null)
            yield break;

        // [?섑뵾??蹂쇰━ 由щ벉 ?좊땲硫붿씠?? 臾쇰━ 猷⑦듃媛 ?꾨땶 ?꾩옱 諛⑺뼢 罹먮┃?곕쭔 ?吏곸엯?덈떎.
        Vector3 originalPosition = visualRoot.localPosition;
        Vector3 originalScale = visualRoot.localScale;
        rapidVolleyMovingVisual = visualRoot;
        rapidVolleyVisualOriginalPosition = originalPosition;
        rapidVolleyVisualOriginalScale = originalScale;
        rapidVolleyVisualBasePosition = originalPosition;
        float dir = rapidVolleyDirection;
        bool isFirstShot = shotIndex == 0;
        bool isFinalShot = shotIndex == 2;
        if (!isFirstShot && !isFinalShot)
            yield break;

        float windupDistance = isFinalShot ? 0.08f : 0.045f;
        float snapDistance = isFinalShot ? 0.14f : 0.085f;
        float windupDuration = isFinalShot ? 0.055f : 0.035f;
        float snapDuration = 0.045f;
        float recoverDuration = isFinalShot ? 0.1f : 0.065f;
        // [Codex RapidVolley 마지막 복구] 반동으로 밀린 위치를 유지하고 Left/Right 양쪽 비주얼 위치를 동기화합니다.
        Vector3 recoilHoldPosition = originalPosition + Vector3.right * -dir * windupDistance;

        if (isFirstShot)
        {
            yield return MoveRapidVolleyVisual(
                visualRoot,
                originalPosition,
                recoilHoldPosition,
                originalScale,
                originalScale,
                windupDuration);

            SetRapidVolleyVisualPosition(recoilHoldPosition);
            visualRoot.localScale = originalScale;
            if (rapidVolleyMovingVisual == visualRoot)
                rapidVolleyMovingVisual = null;
            yield break;
        }

        yield return MoveRapidVolleyVisual(
            visualRoot,
            visualRoot.localPosition,
            originalPosition + Vector3.right * dir * snapDistance,
            visualRoot.localScale,
            originalScale,
            snapDuration);

        yield return MoveRapidVolleyVisual(
            visualRoot,
            visualRoot.localPosition,
            recoilHoldPosition,
            visualRoot.localScale,
            originalScale,
            recoverDuration);

        SetRapidVolleyVisualPosition(recoilHoldPosition);
        visualRoot.localScale = originalScale;
        if (rapidVolleyMovingVisual == visualRoot)
            rapidVolleyMovingVisual = null;
    }

    private IEnumerator MoveRapidVolleyVisual(
        Transform target,
        Vector3 fromPosition,
        Vector3 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            // 遺?쒕윭??媛媛먯냽?쇰줈 吏㏃? ?吏곸엫???딄꺼 蹂댁씠吏 ?딄쾶 ?⑸땲??
            float easedRatio = ratio * ratio * (3f - 2f * ratio);
            Vector3 syncedPosition = Vector3.LerpUnclamped(fromPosition, toPosition, easedRatio);
            SetRapidVolleyVisualPosition(syncedPosition);
            target.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedRatio);
            yield return null;
        }
    }

    private void SetRapidVolleyVisualPosition(Vector3 position)
    {
        // [Codex RapidVolley 마지막 복구] 반동 이동 위치를 Left/Right 양쪽 비주얼 루트에 함께 적용합니다.
        rapidVolleyVisualLocalOffset = position - rapidVolleyVisualBasePosition;
        rapidVolleyVisualFeedback?.SetRapidVolleyWorldOffset(GetRapidVolleyVisualWorldOffset());
        Transform left = transform.Find("Left");
        if (left != null)
            left.localPosition = position;

        Transform right = transform.Find("Right");
        if (right != null)
            right.localPosition = position;
    }

    private Vector3 GetRapidVolleyVisualWorldOffset()
    {
        // [Codex RapidVolley 위치 보정] 비주얼 반동값을 화살과 이펙트 생성 기준에도 똑같이 반영합니다.
        return transform.TransformVector(rapidVolleyVisualLocalOffset);
    }

    private Transform GetActiveDirectionVisual()
    {
        // [?섑뵾??蹂쇰━ 由щ벉 ?좊땲硫붿씠?? ?꾩옱 ?붾㈃???쒖떆?섎뒗 醫뚯슦 罹먮┃?곕쭔 ?좏깮?⑸땲??
        Transform left = transform.Find("Left");
        if (left != null && left.gameObject.activeSelf)
            return left;

        Transform right = transform.Find("Right");
        if (right != null && right.gameObject.activeSelf)
            return right;

        return null;
    }

    public void BeginPowerShotCharge()
    {
        if (isAttacking || isPowerShotCharging)
        {
            return;
        }

        // [?뚯썙 ??異붽?] ?ㅻ? ?꾨Ⅸ ?쒖젏遺??李⑥쭠 ?쒓컙??痢≪젙?⑸땲??
        isPowerShotCharging = true;
        powerShotChargeStartedAt = Time.time;
        powerShotChargeGauge.Show();
        powerShotVisualFeedback.BeginCharge();
        powerShotLimbMotion?.BeginCharge();
        PlayPowerShotChargeSound();

        // [?뚯썙 ?????밴린湲? 湲곗〈 ShotBow ?대┰??諛쒖궗 吏곸쟾 ?먯꽭?먯꽌 ?뺤??쒗궢?덈떎.
        StartCoroutine(HoldPowerShotPose());
    }

    public void ReleasePowerShot()
    {
        if (!isPowerShotCharging || isAttacking)
        {
            return;
        }

        float chargedDuration = Time.time - powerShotChargeStartedAt;
        float chargeRatio = Mathf.Clamp01(
            chargedDuration / Mathf.Max(0.01f, powerShotMaxChargeDuration));

        isPowerShotCharging = false;
        powerShotChargeGauge.Hide();
        StopPowerShotChargeSound();
        hasPendingPowerShot = true;
        pendingPowerShotDamage = Mathf.RoundToInt(
            Mathf.Lerp(powerShotMinDamage, powerShotMaxDamage, chargeRatio));
        pendingPowerShotSpeed = Mathf.Lerp(
            powerShotMinSpeed, powerShotMaxSpeed, chargeRatio);
        pendingPowerShotScale = Mathf.Lerp(
            powerShotMinScale, powerShotMaxScale, chargeRatio);
        pendingPowerShotRatio = chargeRatio;
        // [?뚯썙 ???꾩떊 ?곗텧] 李⑥쭠 誘몄꽭 ?숈옉???뺣━????諛쒖궗 ?좊땲硫붿씠?섏쓣 ?댁뼱媛묐땲??
        StopPowerShotChargeMotion();
        powerShotLimbMotion?.EndCharge();

        // [?뚯썙 ?????밴린湲? 硫덉떠 ???좊땲硫붿씠?섏쓣 ?ш컻?섎㈃ 湲곗〈 FireArrow ?대깽?멸? ?ㅽ뻾?⑸땲??
        animator.speed = 1f;
        powerShotVisualFeedback.Release(chargeRatio);
        PlayPowerShotReleaseSound();
        StartCoroutine(FinishPowerShotRelease());
        StartCoroutine(EnsurePowerShotFired());
    }

    private IEnumerator EnsurePowerShotFired()
    {
        // [Codex PowerShot 1.1] Animation Event가 유실된 경우에만 한 번 보장 발사합니다.
        yield return new WaitForSeconds(0.2f);
        if (hasPendingPowerShot)
        {
            FireArrow();
            ignoreNormalShotBowEvent = true;
        }
    }
    private IEnumerator HoldPowerShotPose()
    {
        // [諛⑺뼢 ?꾪솚 ??泥??좊땲硫붿씠???섏젙]
        // PlayerController媛 Left/Right ?쒖떆瑜??꾪솚???ㅼ쓬 ?꾨젅?꾩뿉 ShotBow瑜??쒖옉?⑸땲??
        yield return null;
        if (!isPowerShotCharging)
            yield break;

        animator.speed = 1f;
        int shotBowHash = Animator.StringToHash("ShotBow");

        // [諛⑺뼢 ?꾪솚 ??泥??좊땲硫붿씠???섏젙]
        // 湲곗〈 Animator??Action/SoloState ?먮쫫???좎??섍퀬, ?ㅼ젣 吏꾩엯???뚭퉴吏 議곌굔???좎??⑸땲??
        // [Codex PowerShot 1.1] ShotBow??Trigger?대?濡?李⑥쭠 ?쒖옉 ????踰덈쭔 吏꾩엯?쒗궢?덈떎.
        animator.ResetTrigger("ShotBow");
        animator.SetTrigger("ShotBow");

        // ?ㅼ젣 ShotBow 吏꾪뻾?꾨? ?뺤씤????諛쒖궗 ?대깽???꾩뿉 怨좎젙?⑸땲??
        float elapsed = 0f;
        while (isPowerShotCharging && elapsed < 0.45f)
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                if (state.shortNameHash == shotBowHash && state.normalizedTime >= 0.42f)
                {
                    animator.speed = 0f;
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

    }

    private IEnumerator AnimatePowerShotChargePose()
    {
        Transform visualRoot = GetActiveDirectionVisual();
        if (visualRoot == null)
            yield break;

        powerShotMovingVisual = visualRoot;
        powerShotVisualOriginalPosition = visualRoot.localPosition;
        powerShotVisualOriginalScale = visualRoot.localScale;
        powerShotVisualOriginalRotation = visualRoot.localRotation;

        while (isPowerShotCharging && visualRoot != null)
        {
            float chargeRatio = Mathf.Clamp01(
                (Time.time - powerShotChargeStartedAt) /
                Mathf.Max(0.01f, powerShotMaxChargeDuration));
            float direction = playerController != null
                ? playerController.GetHorizontalFacingDir()
                : 1f;

            // [?뚯썙 ???꾩떊 ?곗텧] ?명씉怨??λ젰 ?⑤┝?쇰줈 李⑥쭠 以??꾩쟾 ?뺤?瑜?諛⑹??⑸땲??
            float breath = Mathf.Sin(Time.time * 7f);
            float tension = Mathf.Sin(Time.time * Mathf.Lerp(13f, 24f, chargeRatio));
            float drawPulse = Mathf.Sin(Time.time * 4.2f) * chargeRatio;
            float pullBack = Mathf.Lerp(0.055f, 0.24f, chargeRatio) + drawPulse * 0.032f;
            visualRoot.localPosition = powerShotVisualOriginalPosition + new Vector3(
                -direction * (pullBack + tension * 0.008f * chargeRatio),
                -chargeRatio * 0.045f + breath * 0.03f + tension * 0.01f * chargeRatio,
                0f);
            visualRoot.localScale = new Vector3(
                powerShotVisualOriginalScale.x * (1f - chargeRatio * 0.05f + tension * 0.012f),
                powerShotVisualOriginalScale.y * (1f + breath * 0.04f - drawPulse * 0.018f),
                powerShotVisualOriginalScale.z);
            visualRoot.localRotation = powerShotVisualOriginalRotation *
                Quaternion.Euler(0f, 0f, direction * (-4.5f * chargeRatio + breath * 1.1f));

            yield return null;
        }

        RestorePowerShotVisual();
        powerShotChargeMotionCoroutine = null;
    }

    private void StopPowerShotChargeMotion()
    {
        if (powerShotChargeMotionCoroutine != null)
        {
            StopCoroutine(powerShotChargeMotionCoroutine);
            powerShotChargeMotionCoroutine = null;
        }

        RestorePowerShotVisual();
    }

    private void RestorePowerShotVisual()
    {
        if (powerShotMovingVisual == null)
            return;

        powerShotMovingVisual.localPosition = powerShotVisualOriginalPosition;
        powerShotMovingVisual.localScale = powerShotVisualOriginalScale;
        powerShotMovingVisual.localRotation = powerShotVisualOriginalRotation;
        powerShotMovingVisual = null;
    }

    private void CancelPowerShotCharge()
    {
        if (!isPowerShotCharging)
            return;

        // [Codex PowerShot 1.4] ?먰봽?섎㈃ ?뚯썙?룹쓣 諛쒖궗?섏? ?딄퀬 李⑥쭠 ?곹깭留?源⑤걮?섍쾶 ?댁젣?⑸땲??
        isPowerShotCharging = false;
        hasPendingPowerShot = false;
        ignoreNormalShotBowEvent = false;
        animator.speed = 1f;
        animator.ResetTrigger("ShotBow");
        StopPowerShotChargeMotion();
        powerShotChargeGauge.Hide();
        powerShotVisualFeedback.CancelCharge();
        powerShotLimbMotion?.EndCharge();
        StopPowerShotChargeSound();
    }

    private IEnumerator PlayPowerShotFullBodyRecoil(float direction, float power)
    {
        Transform visualRoot = GetActiveDirectionVisual();
        if (visualRoot == null)
            yield break;

        Vector3 originalPosition = visualRoot.localPosition;
        Vector3 originalScale = visualRoot.localScale;
        Quaternion originalRotation = visualRoot.localRotation;
        float recoilDistance = Mathf.Lerp(0.16f, 0.38f, power);

        // [?뚯썙 ???꾩떊 媛뺥솕] ?꾨옒쨌?ㅻ줈 媛뺥븯寃??뺤텞?섎ŉ 諛쒖궗 異⑷꺽???꾩떊?쇰줈 諛쏆뒿?덈떎.
        yield return MovePowerShotVisual(
            visualRoot,
            originalPosition,
            originalPosition - Vector3.right * direction * recoilDistance +
                Vector3.down * Mathf.Lerp(0.075f, 0.13f, power),
            originalScale,
            new Vector3(originalScale.x * 1.14f, originalScale.y * 0.8f, originalScale.z),
            originalRotation,
            originalRotation * Quaternion.Euler(0f, 0f, -direction * Mathf.Lerp(5f, 10f, power)),
            Mathf.Lerp(0.05f, 0.075f, power));

        // [?뚯썙 ???꾩떊 媛뺥솕] ?꽷룹븵?쇰줈 ?뺢린硫??ㅻ（?ｌ씠 湲몄뼱吏???ㅻ깄 ?숈옉?낅땲??
        yield return MovePowerShotVisual(
            visualRoot,
            visualRoot.localPosition,
            originalPosition + Vector3.right * direction * Mathf.Lerp(0.09f, 0.17f, power) +
                Vector3.up * Mathf.Lerp(0.055f, 0.1f, power),
            visualRoot.localScale,
            new Vector3(originalScale.x * 0.91f, originalScale.y * 1.14f, originalScale.z),
            visualRoot.localRotation,
            originalRotation * Quaternion.Euler(0f, 0f, direction * 4.5f),
            0.075f);

        yield return MovePowerShotVisual(
            visualRoot,
            visualRoot.localPosition,
            originalPosition,
            visualRoot.localScale,
            originalScale,
            visualRoot.localRotation,
            originalRotation,
            0.15f);

        visualRoot.localPosition = originalPosition;
        visualRoot.localScale = originalScale;
        visualRoot.localRotation = originalRotation;
    }

    private IEnumerator MovePowerShotVisual(
        Transform target,
        Vector3 fromPosition,
        Vector3 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        Quaternion fromRotation,
        Quaternion toRotation,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float easedRatio = 1f - Mathf.Pow(1f - ratio, 3f);
            target.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, easedRatio);
            target.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedRatio);
            target.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, easedRatio);
            yield return null;
        }
    }

    private IEnumerator FinishPowerShotRelease()
    {
        isAttacking = true;
        float currentAttackDelay = baseAttackDelay / attackSpeedMultiplier;
        yield return new WaitForSeconds(currentAttackDelay);
        isAttacking = false;
        ignoreNormalShotBowEvent = false;
    }

    private void OnDisable()
    {
        // [?뚯썙 ??寃뚯씠吏 異붽?] 鍮꾪솢?깊솕 以?寃뚯씠吏? 李⑥쭠 ?곹깭媛 ?⑥? ?딄쾶 ?뺣━?⑸땲??
        isPowerShotCharging = false;
        isRapidVolleyAttacking = false;
        isAttacking = false;
        ignoreNormalShotBowEvent = false;
        playerController?.UnlockHorizontalFacing();
        animator.speed = 1f;
        StopPowerShotChargeMotion();
        if (rapidVolleyMovingVisual != null)
        {
            SetRapidVolleyVisualPosition(rapidVolleyVisualOriginalPosition);
            rapidVolleyMovingVisual.localScale = rapidVolleyVisualOriginalScale;
            rapidVolleyMovingVisual = null;
        }
        if (powerShotChargeGauge != null)
            powerShotChargeGauge.Hide();
        if (powerShotVisualFeedback != null)
            powerShotVisualFeedback.CancelCharge();
        powerShotLimbMotion?.EndCharge();
        StopPowerShotChargeSound();
    }

    /// <summary>
    /// ??諛쒖궗 ?좊땲硫붿씠???쒗??
    /// - ShotBow Bool??1?꾨젅?꾨쭔 true濡??ㅼ젙?섏뿬 ?좊땲硫붿씠???몃━嫄???븷 ?섑뻾
    /// - ?쇱젙 ?쒓컙 ?숈븞 ?낅젰???좉? 以묐났 怨듦꺽 諛⑹?
    /// - ?ㅼ젣 ?붿궡 ?앹꽦? Animation Event?먯꽌 FireArrow()濡?泥섎━?섎뒗 援ъ“
    /// </summary>
    private IEnumerator DoBowShot()
    {
        isAttacking = true; // 공격 시작 → 입력 잠금

        // [Codex PowerShot 1.1] ShotBow는 Bool이 아니라 Trigger로 한 번만 요청합니다.
        animator.ResetTrigger("ShotBow");
        animator.SetTrigger("ShotBow"); // 발사 애니 시작

        float currentAttackDelay =
            baseAttackDelay / attackSpeedMultiplier;

        yield return new WaitForSeconds(currentAttackDelay);

        isAttacking = false; // 공격 종료 → 입력 해제
    }
    /// <summary>
    /// ?붿궡 ?앹꽦 諛?諛쒖궗 泥섎━.
    /// - ?뚮젅?댁뼱 諛붾씪蹂대뒗 諛⑺뼢(dir)???곕씪 ?꾩튂/?뚯쟾 寃곗젙
    /// - ?뚮젅?댁뼱? ?붿궡??異⑸룎??臾댁떆?섏뿬 ?먭린 ?먯떊怨?遺?ろ엳??臾몄젣 諛⑹?
    /// - Rigidbody2D velocity瑜??댁슜??吏곸꽑 諛쒖궗
    /// </summary>
    private void SpawnRapidVolleyArrow(int shotIndex)
    {
        if (arrowPrefab == null || firePoint == null)
            return;

        // [?섑뵾??蹂쇰━ 諛⑺뼢 怨좎젙] ???붿궡 紐⑤몢 ?ㅽ궗 ?쒖옉 ?쒓컙??諛⑺뼢???ъ슜?⑸땲??
        float dir = rapidVolleyDirection;
        Vector3 spawnPos = firePoint.position +
            GetRapidVolleyVisualWorldOffset() +
            new Vector3(dir * 0.3f, 0f, 0f);
        Quaternion rotation = dir > 0f
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.Euler(0f, 0f, 90f);

        bool isFinalShot = shotIndex == 2;
        float finalSpeedMultiplier = isFinalShot ? 1.12f : 1f;
        float scaleMultiplier = rapidVolleyArrowScale * (isFinalShot ? 1.15f : 1f);
        RequestArrowSpawn(
            spawnPos,
            rotation,
            dir,
            rapidVolleyArrowSpeed * finalSpeedMultiplier,
            rapidVolleyDamage,
            scaleMultiplier,
            false,
            isFinalShot ? 2 : 1);

    }

    private void ApplyRapidVolleyArrowVisual(GameObject arrow, bool isFinalShot)
    {
        // [?섑뵾??蹂쇰━ ?꾩슜 ?곗텧] 泥?줉???붿궡怨?蹂대씪??瑗щ━濡??꾩씠肄??됯컧???댁뼱媛묐땲??
        SpriteRenderer arrowRenderer = arrow.GetComponent<SpriteRenderer>();
        if (arrowRenderer != null)
        {
            arrowRenderer.color = isFinalShot
                ? new Color(0.78f, 0.65f, 1f, 1f)
                : new Color(0.48f, 0.95f, 1f, 1f);
        }

        TrailRenderer trail = arrow.GetComponentInChildren<TrailRenderer>(true);
        if (trail == null)
            return;

        trail.time = isFinalShot ? 0.3f : 0.22f;
        trail.widthMultiplier *= isFinalShot ? 1.5f : 1.2f;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.35f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.62f, 0.36f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
    }

    public void FireArrow()
    {
        if (IsNetworkActive() && !IsOwner)
            return;

        if (isRapidVolleyAttacking)
            return;

        if (isPowerShotCharging || ignoreNormalShotBowEvent)
            return;

        if (arrowPrefab == null || firePoint == null)
        {
            Debug.LogWarning("arrowPrefab or firePoint is not assigned.");
            return;
        }

        float dir = playerController != null ? playerController.GetHorizontalFacingDir() : 1f;
        Vector3 spawnPos = firePoint.position + new Vector3(dir * 0.3f, 0f, 0f);
        Quaternion rot = dir > 0f
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.Euler(0f, 0f, 90f);

        float launchSpeed = arrowSpeed;
        int arrowDamage = 10;
        float arrowScale = 1f;
        bool useHitReaction = true;
        int visualStyle = 0;
        float visualPower = 0f;

        if (hasPendingPowerShot)
        {
            ignoreNormalShotBowEvent = true;
            launchSpeed = pendingPowerShotSpeed;
            arrowDamage = pendingPowerShotDamage;
            arrowScale = pendingPowerShotScale;
            useHitReaction = false;
            visualStyle = 3;
            visualPower = pendingPowerShotRatio;

            if (cameraShake == null && Camera.main != null)
                cameraShake = Camera.main.GetComponent<CameraShake2D>();
            cameraShake?.Shake(
                0.09f + pendingPowerShotRatio * 0.07f,
                0.035f + pendingPowerShotRatio * 0.04f);
            powerShotScreenFeedback?.PlayRelease(pendingPowerShotRatio);
            StartCoroutine(PlayPowerShotFullBodyRecoil(dir, pendingPowerShotRatio));
            powerShotLimbMotion?.PlayRelease(pendingPowerShotRatio);
        }

        // Codex: Route projectile creation through Netcode so host and clients see the same arrow.
        RequestArrowSpawn(
            spawnPos,
            rot,
            dir,
            launchSpeed,
            arrowDamage,
            arrowScale,
            useHitReaction,
            visualStyle,
            visualPower);

        hasPendingPowerShot = false;
    }
    private void RequestArrowSpawn(
        Vector3 spawnPos,
        Quaternion rotation,
        float dir,
        float speed,
        int damage,
        float scaleMultiplier,
        bool useHitReaction,
        int visualStyle,
        float visualPower = 0f)
    {
        if (IsNetworkActive())
        {
            if (IsServer)
            {
                SpawnArrowOnServer(
                    spawnPos,
                    rotation,
                    dir,
                    speed,
                    damage,
                    scaleMultiplier,
                    useHitReaction,
                    visualStyle,
                    visualPower);
            }
            else
            {
                SpawnArrowServerRpc(
                    spawnPos,
                    rotation,
                    dir,
                    speed,
                    damage,
                    scaleMultiplier,
                    useHitReaction,
                    visualStyle,
                    visualPower);
            }

            return;
        }

        SpawnArrowLocal(
            spawnPos,
            rotation,
            dir,
            speed,
            damage,
            scaleMultiplier,
            useHitReaction,
            visualStyle,
            visualPower);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnArrowServerRpc(
        Vector3 spawnPos,
        Quaternion rotation,
        float dir,
        float speed,
        int damage,
        float scaleMultiplier,
        bool useHitReaction,
        int visualStyle,
        float visualPower)
    {
        SpawnArrowOnServer(
            spawnPos,
            rotation,
            dir,
            speed,
            damage,
            scaleMultiplier,
            useHitReaction,
            visualStyle,
            visualPower);
    }

    private void SpawnArrowOnServer(
        Vector3 spawnPos,
        Quaternion rotation,
        float dir,
        float speed,
        int damage,
        float scaleMultiplier,
        bool useHitReaction,
        int visualStyle,
        float visualPower)
    {
        GameObject arrow = SpawnArrowLocal(
            spawnPos,
            rotation,
            dir,
            speed,
            damage,
            scaleMultiplier,
            useHitReaction,
            visualStyle,
            visualPower);

        NetworkObject networkObject = arrow.GetComponent<NetworkObject>();
        if (networkObject != null && !networkObject.IsSpawned)
            networkObject.Spawn(true);

        ArrowProjectile2D projectile = arrow.GetComponent<ArrowProjectile2D>();
        if (projectile != null)
            projectile.ApplyVelocityClientRpc(dir, speed);
    }

    private GameObject SpawnArrowLocal(
        Vector3 spawnPos,
        Quaternion rotation,
        float dir,
        float speed,
        int damage,
        float scaleMultiplier,
        bool useHitReaction,
        int visualStyle,
        float visualPower)
    {
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, rotation);
        arrow.transform.localScale *= scaleMultiplier;
        ApplyArrowVisualStyle(arrow, visualStyle, visualPower);
        IgnoreArrowOwnerCollision(arrow);

        ArrowProjectile2D projectile = arrow.GetComponent<ArrowProjectile2D>();
        if (projectile != null)
            projectile.Configure(damage, dir, useHitReaction, speed);

        Rigidbody2D rigidbody = arrow.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
            rigidbody.linearVelocity = new Vector2(dir * speed, 0f);

        return arrow;
    }

    private void ApplyArrowVisualStyle(GameObject arrow, int visualStyle, float visualPower)
    {
        if (visualStyle == 1 || visualStyle == 2)
        {
            ApplyRapidVolleyArrowVisual(arrow, visualStyle == 2);
            return;
        }

        if (visualStyle == 3)
            ApplyPowerShotArrowVisual(arrow, visualPower);
    }

    private void IgnoreArrowOwnerCollision(GameObject arrow)
    {
        Collider2D arrowCollider = arrow.GetComponent<Collider2D>();
        foreach (Collider2D playerCollider in GetComponentsInChildren<Collider2D>())
        {
            if (arrowCollider != null && playerCollider != null)
                Physics2D.IgnoreCollision(arrowCollider, playerCollider, true);
        }
    }

    private bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;
    }

    private void ApplyPowerShotArrowVisual(GameObject arrow, float power)
    {
        // [?뚯썙 ???붿궡 蹂?? ?먮낯 ?꾨━?뱀? 蹂댁〈?섍퀬 ?뚯썙 ???몄뒪?댁뒪留?媛뺥솕?⑸땲??
        SpriteRenderer arrowRenderer = arrow.GetComponent<SpriteRenderer>();
        if (arrowRenderer != null)
        {
            arrowRenderer.color = Color.Lerp(
                new Color(1f, 0.9f, 0.55f, 1f),
                new Color(0.9f, 0.72f, 1f, 1f),
                power);
        }

        TrailRenderer trail = arrow.GetComponentInChildren<TrailRenderer>(true);
        if (trail == null)
            return;

        trail.time = Mathf.Lerp(0.22f, 0.34f, power);
        trail.widthMultiplier *= Mathf.Lerp(1.25f, 1.65f, power);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.82f, 0.3f), 0f),
                new GradientColorKey(new Color(0.62f, 0.38f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;
    }

    private void PlayPowerShotChargeSound()
    {
        // [?뚯썙 ???ъ슫???곌껐] 鍮꾩뼱 ?덉쑝硫?議곗슜??嫄대꼫?곕?濡?Inspector?먯꽌 ?먯쑀濡?쾶 援먯껜?????덉뒿?덈떎.
        if (powerShotAudioSource == null || powerShotChargeSound == null)
            return;

        powerShotAudioSource.Stop();
        powerShotAudioSource.clip = powerShotChargeSound;
        powerShotAudioSource.volume = powerShotChargeVolume;
        powerShotAudioSource.loop = true;
        powerShotAudioSource.Play();
    }

    private void StopPowerShotChargeSound()
    {
        if (powerShotAudioSource == null)
            return;

        powerShotAudioSource.Stop();
        powerShotAudioSource.clip = null;
        powerShotAudioSource.loop = false;
    }

    private void PlayPowerShotReleaseSound()
    {
        if (powerShotAudioSource == null || powerShotReleaseSound == null)
            return;

        powerShotAudioSource.PlayOneShot(
            powerShotReleaseSound,
            powerShotReleaseVolume);
    }
    /// <summary>
    /// 怨듦꺽?띾룄 諛곗쑉 ?곸슜.
    /// 1.5 ?낅젰 ??怨듦꺽 ?湲곗떆媛꾩씠 ??33% 媛먯냼?⑸땲??
    /// </summary>
    public void SetAttackSpeedMultiplier(float multiplier)
    {
        attackSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    /// <summary>
    /// 怨듦꺽?띾룄瑜?湲곕낯 ?곹깭濡?蹂듦뎄?⑸땲??
    /// </summary>
    public void ResetAttackSpeedMultiplier()
    {
        attackSpeedMultiplier = 1f;
    }
}


