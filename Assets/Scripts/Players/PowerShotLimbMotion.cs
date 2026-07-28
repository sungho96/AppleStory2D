using System.Collections;
using UnityEngine;

public class PowerShotLimbMotion : MonoBehaviour
{
    private PlayerController2D playerController;
    private Transform upperBody;
    private Transform lowerBody;
    private Transform headAnchor;
    private Transform armLAnchor;
    private Transform armRAnchor;
    private Transform legL;
    private Transform legR;
    private Quaternion upperBase;
    private Quaternion lowerBase;
    private Quaternion headBase;
    private Quaternion armLBase;
    private Quaternion armRBase;
    private Quaternion legLBase;
    private Quaternion legRBase;
    private Vector3 lowerBasePosition;
    private bool charging;
    private float chargeProgress;
    private Coroutine releaseRoutine;

    public void Initialize(PlayerController2D controller)
    {
        playerController = controller;
    }

    public void BeginCharge()
    {
        if (!CacheActiveRig())
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
        RestoreRig();
    }

    public void PlayRelease(float power)
    {
        charging = false;
        if (!CacheActiveRig())
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
        float tension = Mathf.Sin(Time.time * Mathf.Lerp(10f, 20f, chargeProgress));
        float brace = Mathf.SmoothStep(0f, 1f, chargeProgress);

        // [파워 샷 팔다리 애니메이션] 팔을 벌려 활 장력을 만들고 상체는 뒤로 버팁니다.
        upperBody.localRotation = upperBase * Quaternion.Euler(
            0f, 0f, -direction * (3f + brace * 5f + tension * 0.7f));
        // [파워 샷 머리 안정화] 상체 기울기의 일부를 반대로 보정해 머리 흔들림을 줄입니다.
        headAnchor.localRotation = headBase * Quaternion.Euler(
            0f, 0f, direction * (2f + brace * 3.2f + tension * 0.2f));

        // [파워 샷 팔 각도 조정] 기존 활 자세보다 양팔이 조금 위를 향하도록 보정합니다.
        armLAnchor.localRotation = armLBase * Quaternion.Euler(
            0f, 0f, direction * (-1f - brace * 3f - tension * 0.8f));
        armRAnchor.localRotation = armRBase * Quaternion.Euler(
            0f, 0f, direction * (7f + brace * 7f + tension * 1.1f));

        // [파워 샷 팔다리 애니메이션] 양다리를 반대로 회전해 무게를 버티는 자세를 만듭니다.
        lowerBody.localPosition = lowerBasePosition + Vector3.down * brace * 0.035f;
        lowerBody.localRotation = lowerBase * Quaternion.Euler(0f, 0f, direction * brace * 1.5f);
        legL.localRotation = legLBase * Quaternion.Euler(0f, 0f, direction * (-5f - brace * 8f));
        legR.localRotation = legRBase * Quaternion.Euler(0f, 0f, direction * (4f + brace * 7f));
    }

    private IEnumerator ReleaseRoutine(float power)
    {
        float direction = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        yield return AnimateRigPose(
            direction,
            Mathf.Lerp(8f, 15f, power),
            Mathf.Lerp(14f, 24f, power),
            Mathf.Lerp(-10f, -18f, power),
            Mathf.Lerp(-14f, -22f, power),
            Mathf.Lerp(12f, 20f, power),
            -0.065f,
            0.065f);

        // [파워 샷 팔다리 애니메이션] 발사 후 팔과 다리가 반대 방향으로 튕기며 오버슈트합니다.
        yield return AnimateRigPose(
            direction,
            -4f,
            -7f,
            6f,
            5f,
            -5f,
            0.025f,
            0.085f);

        yield return AnimateRigPose(direction, 0f, 0f, 0f, 0f, 0f, 0f, 0.14f);
        RestoreRig();
        releaseRoutine = null;
    }

    private IEnumerator AnimateRigPose(
        float direction,
        float upperAngle,
        float armLAngle,
        float armRAngle,
        float legLAngle,
        float legRAngle,
        float lowerYOffset,
        float duration)
    {
        Quaternion upperFrom = upperBody.localRotation;
        Quaternion armLFrom = armLAnchor.localRotation;
        Quaternion armRFrom = armRAnchor.localRotation;
        Quaternion legLFrom = legL.localRotation;
        Quaternion legRFrom = legR.localRotation;
        Vector3 lowerFrom = lowerBody.localPosition;
        float elapsed = 0f;

        while (elapsed < duration && upperBody != null)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - ratio, 3f);
            upperBody.localRotation = Quaternion.SlerpUnclamped(
                upperFrom, upperBase * Quaternion.Euler(0f, 0f, direction * upperAngle), eased);
            armLAnchor.localRotation = Quaternion.SlerpUnclamped(
                armLFrom, armLBase * Quaternion.Euler(0f, 0f, direction * armLAngle), eased);
            armRAnchor.localRotation = Quaternion.SlerpUnclamped(
                armRFrom, armRBase * Quaternion.Euler(0f, 0f, direction * armRAngle), eased);
            legL.localRotation = Quaternion.SlerpUnclamped(
                legLFrom, legLBase * Quaternion.Euler(0f, 0f, direction * legLAngle), eased);
            legR.localRotation = Quaternion.SlerpUnclamped(
                legRFrom, legRBase * Quaternion.Euler(0f, 0f, direction * legRAngle), eased);
            lowerBody.localPosition = Vector3.LerpUnclamped(
                lowerFrom, lowerBasePosition + Vector3.up * lowerYOffset, eased);
            yield return null;
        }
    }

    private bool CacheActiveRig()
    {
        Transform visualRoot = FindActiveVisual();
        if (visualRoot == null)
            return false;

        upperBody = visualRoot.Find("UpperBody");
        lowerBody = visualRoot.Find("LowerBody");
        headAnchor = visualRoot.Find("UpperBody/HeadAnchor");
        armLAnchor = visualRoot.Find("UpperBody/ArmLAnchor");
        armRAnchor = visualRoot.Find("UpperBody/ArmRAnchor");
        legL = visualRoot.Find("LowerBody/LegL");
        legR = visualRoot.Find("LowerBody/LegR");
        if (upperBody == null || lowerBody == null || headAnchor == null || armLAnchor == null ||
            armRAnchor == null || legL == null || legR == null)
            return false;

        upperBase = upperBody.localRotation;
        lowerBase = lowerBody.localRotation;
        headBase = headAnchor.localRotation;
        armLBase = armLAnchor.localRotation;
        armRBase = armRAnchor.localRotation;
        legLBase = legL.localRotation;
        legRBase = legR.localRotation;
        lowerBasePosition = lowerBody.localPosition;
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

    private void RestoreRig()
    {
        if (upperBody != null) upperBody.localRotation = upperBase;
        if (headAnchor != null) headAnchor.localRotation = headBase;
        if (lowerBody != null)
        {
            lowerBody.localRotation = lowerBase;
            lowerBody.localPosition = lowerBasePosition;
        }
        if (armLAnchor != null) armLAnchor.localRotation = armLBase;
        if (armRAnchor != null) armRAnchor.localRotation = armRBase;
        if (legL != null) legL.localRotation = legLBase;
        if (legR != null) legR.localRotation = legRBase;
    }

    private void OnDisable()
    {
        charging = false;
        RestoreRig();
    }
}
