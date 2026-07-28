using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAttack2D : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private Animator animator;              // 활 발사 애니메이션 제어용
    [SerializeField] private GameObject arrowPrefab;         // 발사할 화살 프리팹
    [SerializeField] private Transform firePoint;            // 화살 생성 위치
    [SerializeField] private float arrowSpeed = 12f;         // 화살 속도
    [SerializeField] private PlayerController2D playerController; // 바라보는 방향 참조용

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

    private bool isAttacking; // 연타 방지 (공격 중 추가 입력 차단 플래그)
    private bool isPowerShotCharging;
    private bool hasPendingPowerShot;
    private float powerShotChargeStartedAt;
    private int pendingPowerShotDamage;
    private float pendingPowerShotSpeed;
    private float pendingPowerShotScale;
    private float pendingPowerShotRatio;
    private PowerShotChargeGauge powerShotChargeGauge;
    private PowerShotVisualFeedback powerShotVisualFeedback;
    private PowerShotScreenFeedback powerShotScreenFeedback;
    private PowerShotLimbMotion powerShotLimbMotion;
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
    private int rapidVolleyFiredMask;
    private RapidVolleyVisualFeedback rapidVolleyVisualFeedback;
    private RapidVolleyScreenFeedback rapidVolleyScreenFeedback;
    private float rapidVolleyDirection = 1f;

    /// <summary>
    /// 초기 참조 설정.
    /// - PlayerController2D가 비어 있을 경우 자동으로 같은 오브젝트에서 가져옴
    /// - 공격 방향 계산(GetHorizontalFacingDir)에 사용됨
    /// </summary>
    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();

        // [파워 샷 게이지 추가] 씬 참조 없이 플레이어에 게이지를 자동 구성합니다.
        powerShotChargeGauge = GetComponent<PowerShotChargeGauge>();
        if (powerShotChargeGauge == null)
            powerShotChargeGauge = gameObject.AddComponent<PowerShotChargeGauge>();
        powerShotChargeGauge.Initialize();

        // [파워 샷 연출 추가] 차징 오라와 발사 섬광을 런타임으로 구성합니다.
        powerShotVisualFeedback = GetComponent<PowerShotVisualFeedback>();
        if (powerShotVisualFeedback == null)
            powerShotVisualFeedback = gameObject.AddComponent<PowerShotVisualFeedback>();
        powerShotVisualFeedback.Initialize(firePoint, playerController);
        cameraShake = FindFirstObjectByType<CameraShake2D>();
        if (cameraShake == null && Camera.main != null)
            cameraShake = Camera.main.gameObject.AddComponent<CameraShake2D>();

        // [파워 샷 화면 후처리] 별도 패키지 없이 카메라 줌·플래시·비네트를 자동 연결합니다.
        if (Camera.main != null)
        {
            powerShotScreenFeedback = Camera.main.GetComponent<PowerShotScreenFeedback>();
            if (powerShotScreenFeedback == null)
                powerShotScreenFeedback = Camera.main.gameObject.AddComponent<PowerShotScreenFeedback>();
            powerShotScreenFeedback.Initialize(Camera.main);

            // [래피드 볼리 후처리] 별도 패키지 없이 카메라에 3연사 플래시와 줌 연출을 연결합니다.
            rapidVolleyScreenFeedback = Camera.main.GetComponent<RapidVolleyScreenFeedback>();
            if (rapidVolleyScreenFeedback == null)
                rapidVolleyScreenFeedback = Camera.main.gameObject.AddComponent<RapidVolleyScreenFeedback>();
            rapidVolleyScreenFeedback.Initialize(Camera.main);
        }

        // [파워 샷 팔다리 애니메이션] 원본 클립을 보존하고 관절 보조 동작을 자동 연결합니다.
        powerShotLimbMotion = GetComponent<PowerShotLimbMotion>();
        if (powerShotLimbMotion == null)
            powerShotLimbMotion = gameObject.AddComponent<PowerShotLimbMotion>();
        powerShotLimbMotion.Initialize(playerController);

        // [파워 샷 사운드 연결] 다른 플레이어 사운드와 섞이지 않는 전용 AudioSource를 사용합니다.
        powerShotAudioSource = gameObject.AddComponent<AudioSource>();
        powerShotAudioSource.playOnAwake = false;
        powerShotAudioSource.spatialBlend = 0f;

        // [래피드 볼리 이펙트] 씬 참조 없이 캐릭터 주변 연출을 자동 구성합니다.
        rapidVolleyVisualFeedback = GetComponent<RapidVolleyVisualFeedback>();
        if (rapidVolleyVisualFeedback == null)
            rapidVolleyVisualFeedback = gameObject.AddComponent<RapidVolleyVisualFeedback>();
        rapidVolleyVisualFeedback.Initialize();
    }

    /// <summary>
    /// 입력 처리 루프.
    /// - LeftControl 입력 시 공격 코루틴 실행
    /// - isAttacking을 통해 연속 입력(스팸 공격) 방지
    /// </summary>
    private void Update()
    {
        if (isPowerShotCharging)
        {
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
        // [래피드 볼리 전용 애니메이션] 화살은 Animation Event 세 곳에서 생성합니다.
        isRapidVolleyAttacking = true;
        isAttacking = true;
        rapidVolleyFiredMask = 0;
        rapidVolleyDirection = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;
        // [래피드 볼리 방향 고정] 시작 방향을 저장하고 세 발이 끝날 때까지 캐릭터 전환을 잠급니다.
        playerController?.LockHorizontalFacing(rapidVolleyDirection);
        rapidVolleyVisualFeedback?.PlayCastEffect(rapidVolleyDirection);
        yield return null;
        animator.speed = 1f;

        // [래피드 볼리 연결 안정화] Trigger와 함께 실제 Lower State 진입을 보장합니다.
        // [래피드 볼리 수정] 전신 공격용 Complex 레이어의 상태를 직접 재생합니다.
        int complexLayer = animator.GetLayerIndex("Complex");
        int rapidVolleyStateHash = Animator.StringToHash("Complex.RapidVolley");
        if (complexLayer >= 0 && animator.HasState(complexLayer, rapidVolleyStateHash))
        {
            animator.ResetTrigger("RapidVolley");
            animator.CrossFade(rapidVolleyStateHash, 0.02f, complexLayer, 0f);
        }
        else
        {
            animator.SetTrigger("RapidVolley");
        }

        // [래피드 볼리 수정] 팔을 올린 자세를 유지하는 동안 세 발을 발사합니다.
        float[] eventTimes = { 0.25f, 0.43f, 0.61f };
        float elapsed = 0f;
        while (elapsed < 0.86f)
        {
            elapsed += Time.deltaTime;

            // [래피드 볼리 발사 안전장치] 이벤트가 누락된 발만 개별적으로 복구합니다.
            for (int shotIndex = 0; shotIndex < eventTimes.Length; shotIndex++)
            {
                int shotBit = 1 << shotIndex;
                if (elapsed >= eventTimes[shotIndex] + 0.07f &&
                    (rapidVolleyFiredMask & shotBit) == 0)
                {
                    FireRapidVolleyAnimationEvent(shotIndex);
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

        // [래피드 볼리 전용 애니메이션] 활시위가 풀리는 실제 키프레임에서 발사합니다.
        StartCoroutine(PlayRapidVolleyBodyKick(shotIndex));
        SpawnRapidVolleyArrow(shotIndex);
        PlayRapidVolleyShotSound();
        float shotDirection = rapidVolleyDirection;
        rapidVolleyVisualFeedback?.PlayShotEffect(shotIndex, shotDirection);
        rapidVolleyScreenFeedback?.PlayShot(shotIndex);

        // [래피드 볼리 후처리] 매 발의 반동을 보여 주되 세 번째 발만 확실하게 강해집니다.
        float shakeStrength = shotIndex == 2 ? 0.055f : 0.022f + shotIndex * 0.008f;
        cameraShake?.Shake(shotIndex == 2 ? 0.1f : 0.055f, shakeStrength);
    }

    private void PlayRapidVolleyShotSound()
    {
        // [래피드 볼리 사운드] 화살이 생성될 때 같은 클립을 한 번씩, 총 세 번 재생합니다.
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

        // [래피드 볼리 리듬 애니메이션] 물리 루트가 아닌 현재 방향 캐릭터만 움직입니다.
        Vector3 originalPosition = visualRoot.localPosition;
        Vector3 originalScale = visualRoot.localScale;
        rapidVolleyMovingVisual = visualRoot;
        rapidVolleyVisualOriginalPosition = originalPosition;
        rapidVolleyVisualOriginalScale = originalScale;
        float dir = rapidVolleyDirection;
        bool isFinalShot = shotIndex == 2;

        float windupDistance = isFinalShot ? 0.08f : 0.045f;
        float snapDistance = isFinalShot ? 0.14f : 0.085f;
        float windupDuration = isFinalShot ? 0.055f : 0.035f;
        float snapDuration = 0.045f;
        float recoverDuration = isFinalShot ? 0.1f : 0.065f;

        yield return MoveRapidVolleyVisual(
            visualRoot,
            originalPosition,
            originalPosition + Vector3.right * -dir * windupDistance,
            originalScale,
            isFinalShot ? new Vector3(originalScale.x * 0.97f, originalScale.y * 1.03f, originalScale.z) : originalScale,
            windupDuration);

        yield return MoveRapidVolleyVisual(
            visualRoot,
            visualRoot.localPosition,
            originalPosition + Vector3.right * dir * snapDistance,
            visualRoot.localScale,
            isFinalShot ? new Vector3(originalScale.x * 1.06f, originalScale.y * 0.95f, originalScale.z) : originalScale,
            snapDuration);

        yield return MoveRapidVolleyVisual(
            visualRoot,
            visualRoot.localPosition,
            originalPosition,
            visualRoot.localScale,
            originalScale,
            recoverDuration);

        visualRoot.localPosition = originalPosition;
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
            // 부드러운 가감속으로 짧은 움직임도 끊겨 보이지 않게 합니다.
            float easedRatio = ratio * ratio * (3f - 2f * ratio);
            target.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, easedRatio);
            target.localScale = Vector3.LerpUnclamped(fromScale, toScale, easedRatio);
            yield return null;
        }
    }

    private Transform GetActiveDirectionVisual()
    {
        // [래피드 볼리 리듬 애니메이션] 현재 화면에 표시되는 좌우 캐릭터만 선택합니다.
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

        // [파워 샷 추가] 키를 누른 시점부터 차징 시간을 측정합니다.
        isPowerShotCharging = true;
        powerShotChargeStartedAt = Time.time;
        powerShotChargeGauge.Show();
        powerShotVisualFeedback.BeginCharge();
        powerShotLimbMotion?.BeginCharge();
        PlayPowerShotChargeSound();

        // [파워 샷 활 당기기] 기존 ShotBow 클립을 발사 직전 자세에서 정지시킵니다.
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
        // [파워 샷 전신 연출] 차징 미세 동작을 정리한 뒤 발사 애니메이션을 이어갑니다.
        StopPowerShotChargeMotion();
        powerShotLimbMotion?.EndCharge();

        // [파워 샷 활 당기기] 멈춰 둔 애니메이션을 재개하면 기존 FireArrow 이벤트가 실행됩니다.
        animator.speed = 1f;
        powerShotVisualFeedback.Release(chargeRatio);
        PlayPowerShotReleaseSound();
        StartCoroutine(FinishPowerShotRelease());
        StartCoroutine(EnsurePowerShotFired());
    }

    private IEnumerator EnsurePowerShotFired()
    {
        // [방향 전환 후 첫 발 안정화] Animation Event가 유실된 경우에만 한 번 보장 발사합니다.
        yield return new WaitForSeconds(0.2f);
        if (hasPendingPowerShot)
            FireArrow();
    }

    private IEnumerator HoldPowerShotPose()
    {
        // [방향 전환 후 첫 애니메이션 수정]
        // PlayerController가 Left/Right 표시를 전환한 다음 프레임에 ShotBow를 시작합니다.
        yield return null;
        if (!isPowerShotCharging)
            yield break;

        animator.speed = 1f;
        int shotBowHash = Animator.StringToHash("ShotBow");

        // [방향 전환 후 첫 애니메이션 수정]
        // 기존 Animator의 Action/SoloState 흐름을 유지하고, 실제 진입할 때까지 조건을 유지합니다.
        animator.SetBool("ShotBow", true);

        // 실제 ShotBow 진행도를 확인한 뒤 발사 이벤트 전에 고정합니다.
        float elapsed = 0f;
        while (isPowerShotCharging && elapsed < 0.45f)
        {
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                if (state.shortNameHash == shotBowHash && state.normalizedTime >= 0.42f)
                {
                    animator.SetBool("ShotBow", false);
                    animator.speed = 0f;
                    powerShotChargeMotionCoroutine =
                        StartCoroutine(AnimatePowerShotChargePose());
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        animator.SetBool("ShotBow", false);
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

            // [파워 샷 전신 연출] 호흡과 장력 떨림으로 차징 중 완전 정지를 방지합니다.
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

    private IEnumerator PlayPowerShotFullBodyRecoil(float direction, float power)
    {
        Transform visualRoot = GetActiveDirectionVisual();
        if (visualRoot == null)
            yield break;

        Vector3 originalPosition = visualRoot.localPosition;
        Vector3 originalScale = visualRoot.localScale;
        Quaternion originalRotation = visualRoot.localRotation;
        float recoilDistance = Mathf.Lerp(0.16f, 0.38f, power);

        // [파워 샷 전신 강화] 아래·뒤로 강하게 압축되며 발사 충격을 전신으로 받습니다.
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

        // [파워 샷 전신 강화] 위·앞으로 튕기며 실루엣이 길어지는 스냅 동작입니다.
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
    }

    private void OnDisable()
    {
        // [파워 샷 게이지 추가] 비활성화 중 게이지와 차징 상태가 남지 않게 정리합니다.
        isPowerShotCharging = false;
        isRapidVolleyAttacking = false;
        isAttacking = false;
        playerController?.UnlockHorizontalFacing();
        animator.speed = 1f;
        StopPowerShotChargeMotion();
        if (rapidVolleyMovingVisual != null)
        {
            rapidVolleyMovingVisual.localPosition = rapidVolleyVisualOriginalPosition;
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
    /// 활 발사 애니메이션 시퀀스.
    /// - ShotBow Bool을 1프레임만 true로 설정하여 애니메이션 트리거 역할 수행
    /// - 일정 시간 동안 입력을 잠가 중복 공격 방지
    /// - 실제 화살 생성은 Animation Event에서 FireArrow()로 처리하는 구조
    /// </summary>
    private IEnumerator DoBowShot()
    {
        isAttacking = true; // 공격 시작 → 입력 잠금

        animator.SetBool("ShotBow", true); // 발사 애니 시작
        yield return null;                 // 1프레임 유지
        animator.SetBool("ShotBow", false); // 애니 트리거 OFF

        float currentAttackDelay =
            baseAttackDelay / attackSpeedMultiplier;

        yield return new WaitForSeconds(currentAttackDelay);

        isAttacking = false; // 공격 종료 → 입력 해제
    }

    /// <summary>
    /// 화살 생성 및 발사 처리.
    /// - 플레이어 바라보는 방향(dir)에 따라 위치/회전 결정
    /// - 플레이어와 화살의 충돌을 무시하여 자기 자신과 부딪히는 문제 방지
    /// - Rigidbody2D velocity를 이용해 직선 발사
    /// </summary>
    private void SpawnRapidVolleyArrow(int shotIndex)
    {
        if (arrowPrefab == null || firePoint == null)
            return;

        // [래피드 볼리 방향 고정] 세 화살 모두 스킬 시작 순간의 방향을 사용합니다.
        float dir = rapidVolleyDirection;
        Vector3 spawnPos = firePoint.position + new Vector3(dir * 0.3f, 0f, 0f);
        Quaternion rotation = dir > 0f
            ? Quaternion.Euler(0f, 0f, -90f)
            : Quaternion.Euler(0f, 0f, 90f);

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, rotation);
        bool isFinalShot = shotIndex == 2;
        arrow.transform.localScale *= rapidVolleyArrowScale * (isFinalShot ? 1.15f : 1f);
        ApplyRapidVolleyArrowVisual(arrow, isFinalShot);

        Collider2D arrowCollider = arrow.GetComponent<Collider2D>();
        foreach (Collider2D playerCollider in GetComponentsInChildren<Collider2D>())
        {
            if (arrowCollider != null && playerCollider != null)
                Physics2D.IgnoreCollision(arrowCollider, playerCollider, true);
        }

        ArrowProjectile2D projectile = arrow.GetComponent<ArrowProjectile2D>();
        if (projectile != null)
            projectile.Configure(rapidVolleyDamage, dir, false);

        Rigidbody2D rigidbody = arrow.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
        {
            float finalSpeedMultiplier = isFinalShot ? 1.12f : 1f;
            rigidbody.velocity = new Vector2(
                dir * rapidVolleyArrowSpeed * finalSpeedMultiplier,
                0f);
        }

    }

    private void ApplyRapidVolleyArrowVisual(GameObject arrow, bool isFinalShot)
    {
        // [래피드 볼리 전용 연출] 청록색 화살과 보라색 꼬리로 아이콘 색감을 이어갑니다.
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
        // [래피드 볼리 중복 방지] ShotBow Animation Event는 무시하고 코루틴이 정확히 3발을 생성합니다.
        if (isRapidVolleyAttacking)
            return;

        // 필수 참조 체크
        if (arrowPrefab == null || firePoint == null)
        {
            Debug.LogWarning("arrowPrefab 또는 firePoint가 연결되지 않았습니다.");
            return;
        }

        // 플레이어 방향 계산 (오른쪽: +1 / 왼쪽: -1)
        float dir = playerController != null ? playerController.GetHorizontalFacingDir() : 1f;

        // 발사 위치 (플레이어 앞쪽으로 약간 이동)
        Vector3 spawnPos = firePoint.position + new Vector3(dir * 0.3f, 0f, 0f);

        // 방향에 따른 회전 (스프라이트 기준 보정)
        Quaternion rot = dir > 0f
            ? Quaternion.Euler(0f, 0f, -90f) // 오른쪽
            : Quaternion.Euler(0f, 0f, 90f); // 왼쪽

        // 화살 생성
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, rot);

        float launchSpeed = arrowSpeed;
        if (hasPendingPowerShot)
        {
            // [파워 샷 추가] 차징 결과를 이번에 생성된 화살에만 적용합니다.
            arrow.transform.localScale *= pendingPowerShotScale;
            launchSpeed = pendingPowerShotSpeed;
            ApplyPowerShotArrowVisual(arrow, pendingPowerShotRatio);

            // [파워 샷 발사 진동] 완충도에 따라 약한 카메라 흔들림을 적용합니다.
            if (cameraShake == null && Camera.main != null)
                cameraShake = Camera.main.GetComponent<CameraShake2D>();
            cameraShake?.Shake(
                0.09f + pendingPowerShotRatio * 0.07f,
                0.035f + pendingPowerShotRatio * 0.04f);
            powerShotScreenFeedback?.PlayRelease(pendingPowerShotRatio);
            StartCoroutine(PlayPowerShotFullBodyRecoil(dir, pendingPowerShotRatio));
            powerShotLimbMotion?.PlayRelease(pendingPowerShotRatio);
        }

        // 화살 콜라이더 가져오기
        Collider2D arrowCol = arrow.GetComponent<Collider2D>();

        // 플레이어의 모든 콜라이더 가져오기 (자식 포함)
        Collider2D[] playerCols = GetComponentsInChildren<Collider2D>();

        // 화살 스크립트 참조 (방향 설정용)
        ArrowProjectile2D arrowProjectile = arrow.GetComponent<ArrowProjectile2D>();

        // 방향 정보 전달 (화살 자체 로직에서 사용)
        if (arrowProjectile != null)
        {
            if (hasPendingPowerShot)
            {
                arrowProjectile.Configure(
                    pendingPowerShotDamage,
                    dir,
                    false);
            }
            else
            {
                arrowProjectile.SetDirection(dir);
            }
        }

        // 플레이어와 화살 충돌 무시 처리
        foreach (Collider2D col in playerCols)
        {
            if (arrowCol != null && col != null)
                Physics2D.IgnoreCollision(arrowCol, col, true);
        }

        // Rigidbody2D 기반 발사 (속도 직접 지정)
        Rigidbody2D rb = arrow.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = new Vector2(dir * launchSpeed, 0f);
        }


        hasPendingPowerShot = false;
    }

    private void ApplyPowerShotArrowVisual(GameObject arrow, float power)
    {
        // [파워 샷 화살 변화] 원본 프리팹은 보존하고 파워 샷 인스턴스만 강화합니다.
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
        // [파워 샷 사운드 연결] 비어 있으면 조용히 건너뛰므로 Inspector에서 자유롭게 교체할 수 있습니다.
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
    /// 공격속도 배율 적용.
    /// 1.5 입력 시 공격 대기시간이 약 33% 감소합니다.
    /// </summary>
    public void SetAttackSpeedMultiplier(float multiplier)
    {
        attackSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    /// <summary>
    /// 공격속도를 기본 상태로 복구합니다.
    /// </summary>
    public void ResetAttackSpeedMultiplier()
    {
        attackSpeedMultiplier = 1f;
    }
}
