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
    private CameraShake2D cameraShake;
    private AudioSource powerShotAudioSource;

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

        // [파워 샷 사운드 연결] 다른 플레이어 사운드와 섞이지 않는 전용 AudioSource를 사용합니다.
        powerShotAudioSource = gameObject.AddComponent<AudioSource>();
        powerShotAudioSource.playOnAwake = false;
        powerShotAudioSource.spatialBlend = 0f;
    }

    /// <summary>
    /// 입력 처리 루프.
    /// - LeftControl 입력 시 공격 코루틴 실행
    /// - isAttacking을 통해 연속 입력(스팸 공격) 방지
    /// </summary>
    private void Update()
    {
        // [임시 테스트 Q키 - 파워 샷 완성 후 삭제]
        // 키 세팅 UI에서 매번 배치하지 않아도 Q를 누르고 떼어 파워 샷을 테스트할 수 있습니다.
        if (Input.GetKeyDown(KeyCode.Q))
            BeginPowerShotCharge();

        if (Input.GetKeyUp(KeyCode.Q))
            ReleasePowerShot();

        if (isPowerShotCharging)
        {
            float chargeRatio = (Time.time - powerShotChargeStartedAt) /
                                Mathf.Max(0.01f, powerShotMaxChargeDuration);
            powerShotChargeGauge.SetProgress(chargeRatio);
            powerShotVisualFeedback.SetChargeProgress(chargeRatio);
        }

        if (Input.GetKeyDown(KeyCode.LeftControl) &&
            !isAttacking &&
            !isPowerShotCharging)
        {
            StartCoroutine(DoBowShot());
        }
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
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        animator.SetBool("ShotBow", false);
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
        animator.speed = 1f;
        if (powerShotChargeGauge != null)
            powerShotChargeGauge.Hide();
        if (powerShotVisualFeedback != null)
            powerShotVisualFeedback.CancelCharge();
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
    public void FireArrow()
    {
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
                0.07f + pendingPowerShotRatio * 0.04f,
                0.025f + pendingPowerShotRatio * 0.025f);
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
