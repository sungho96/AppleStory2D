using System.Collections;
using UnityEngine;

public class PowerShotLimbMotion : MonoBehaviour
{
    private PlayerController2D playerController;
    private Transform upperBody;
    private Transform headAnchor;
    private Transform armLAnchor;
    private Transform armRAnchor;
    private Quaternion upperBase;
    private Quaternion headBase;
    private Quaternion armLBase;
    private Quaternion armRBase;
    private Vector3 upperBasePosition;
    private bool charging;
    private float chargeProgress;
    private Coroutine releaseRoutine;

    public void Initialize(PlayerController2D controller)
    {
        playerController = controller;
    }

    public void BeginCharge()
    {
        if (!CacheActiveUpperRig())
            return;

        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        chargeProgress = 0f;
        charging = true;
    }

    public void SetChargeProgress(float progress)
    {
        chargeProgress = Mathf.Clamp01(progress);
    }

    public void EndCharge()
    {
        charging = false;
        RestoreUpperRig();
    }

    public void PlayRelease(float power)
    {
        charging = false;
        if (!CacheActiveUpperRig())
            return;

        if (releaseRoutine != null)
            StopCoroutine(releaseRoutine);

        releaseRoutine = StartCoroutine(ReleaseRoutine(Mathf.Clamp01(power)));
    }

    private void LateUpdate()
    {
        if (!charging || upperBody == null)
            return;

        float direction = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;
        float brace = Mathf.SmoothStep(0f, 1f, chargeProgress);
        float pulse = Mathf.Sin(Time.time * Mathf.Lerp(16f, 30f, brace));
        float heavyPulse = Mathf.Sin(Time.time * Mathf.Lerp(5f, 9f, brace));

        // [Codex PowerShot 1.0] 하체는 점프/Idle 상태를 그대로 두고 상체만 활을 당기는 자세로 흔듭니다.
        upperBody.localPosition = upperBasePosition + new Vector3(
            -direction * (Mathf.Lerp(0.025f, 0.09f, brace) + pulse * Mathf.Lerp(0.004f, 0.018f, brace)),
            heavyPulse * Mathf.Lerp(0.004f, 0.018f, brace),
            0f);

        upperBody.localRotation = upperBase * Quaternion.Euler(
            0f,
            0f,
            -direction * (Mathf.Lerp(4f, 13f, brace) + pulse * Mathf.Lerp(0.8f, 2.2f, brace)));

        headAnchor.localRotation = headBase * Quaternion.Euler(
            0f,
            0f,
            direction * (Mathf.Lerp(2f, 6f, brace) + heavyPulse * Mathf.Lerp(0.2f, 0.8f, brace)));

        // [Codex PowerShot 1.1] ShotBow 자세 위에 살짝만 보태서 팔이 아래로 처지지 않게 받칩니다.
        armLAnchor.localRotation = armLBase * Quaternion.Euler(
            0f,
            0f,
            direction * (Mathf.Lerp(3f, 8f, brace) + pulse * Mathf.Lerp(0.4f, 1.1f, brace)));
        armRAnchor.localRotation = armRBase * Quaternion.Euler(
            0f,
            0f,
            direction * (Mathf.Lerp(6f, 14f, brace) + pulse * Mathf.Lerp(0.6f, 1.6f, brace)));
    }

    private IEnumerator ReleaseRoutine(float power)
    {
        float direction = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        yield return AnimateUpperPose(
            direction,
            Mathf.Lerp(12f, 24f, power),
            Mathf.Lerp(18f, 32f, power),
            Mathf.Lerp(-14f, -26f, power),
            0.055f);

        yield return AnimateUpperPose(
            direction,
            Mathf.Lerp(-7f, -12f, power),
            Mathf.Lerp(-8f, -14f, power),
            Mathf.Lerp(7f, 13f, power),
            0.075f);

        yield return AnimateUpperPose(direction, 0f, 0f, 0f, 0.13f);
        RestoreUpperRig();
        releaseRoutine = null;
    }

    private IEnumerator AnimateUpperPose(
        float direction,
        float upperAngle,
        float armLAngle,
        float armRAngle,
        float duration)
    {
        Quaternion upperFrom = upperBody.localRotation;
        Quaternion armLFrom = armLAnchor.localRotation;
        Quaternion armRFrom = armRAnchor.localRotation;
        Vector3 upperFromPosition = upperBody.localPosition;
        float elapsed = 0f;

        while (elapsed < duration && upperBody != null)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - ratio, 3f);
            float recoilOffset = upperAngle == 0f
                ? 0f
                : -Mathf.Sign(upperAngle) * Mathf.Abs(upperAngle) * 0.005f;

            upperBody.localPosition = Vector3.LerpUnclamped(
                upperFromPosition,
                upperBasePosition + Vector3.right * direction * recoilOffset,
                eased);
            upperBody.localRotation = Quaternion.SlerpUnclamped(
                upperFrom,
                upperBase * Quaternion.Euler(0f, 0f, direction * upperAngle),
                eased);
            armLAnchor.localRotation = Quaternion.SlerpUnclamped(
                armLFrom,
                armLBase * Quaternion.Euler(0f, 0f, direction * armLAngle),
                eased);
            armRAnchor.localRotation = Quaternion.SlerpUnclamped(
                armRFrom,
                armRBase * Quaternion.Euler(0f, 0f, direction * armRAngle),
                eased);
            yield return null;
        }
    }

    private bool CacheActiveUpperRig()
    {
        Transform visualRoot = FindActiveVisual();
        if (visualRoot == null)
            return false;

        upperBody = visualRoot.Find("UpperBody");
        headAnchor = visualRoot.Find("UpperBody/HeadAnchor");
        armLAnchor = visualRoot.Find("UpperBody/ArmLAnchor");
        armRAnchor = visualRoot.Find("UpperBody/ArmRAnchor");
        if (upperBody == null || headAnchor == null || armLAnchor == null || armRAnchor == null)
            return false;

        upperBase = upperBody.localRotation;
        headBase = headAnchor.localRotation;
        armLBase = armLAnchor.localRotation;
        armRBase = armRAnchor.localRotation;
        upperBasePosition = upperBody.localPosition;
        return true;
    }

    private Transform FindActiveVisual()
    {
        Transform left = transform.Find("Left");
        if (left != null && left.gameObject.activeSelf)
            return left;

        Transform right = transform.Find("Right");
        return right != null && right.gameObject.activeSelf ? right : null;
    }

    private void RestoreUpperRig()
    {
        if (upperBody != null)
        {
            upperBody.localRotation = upperBase;
            upperBody.localPosition = upperBasePosition;
        }

        if (headAnchor != null) headAnchor.localRotation = headBase;
        if (armLAnchor != null) armLAnchor.localRotation = armLBase;
        if (armRAnchor != null) armRAnchor.localRotation = armRBase;
    }

    private void OnDisable()
    {
        charging = false;
        RestoreUpperRig();
    }
}
