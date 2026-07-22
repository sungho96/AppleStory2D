using System.Collections;
using UnityEngine;

/// <summary>
/// 2D 카메라 흔들림 전용 스크립트입니다.
/// - 피격, 공격, 폭발 등 순간적인 연출에서 호출합니다.
/// - 흔들림이 끝나면 원래 위치로 복구합니다.
/// </summary>
public class CameraShake2D : MonoBehaviour
{
    [Header("Shake Default")]
    [SerializeField] private float defaultDuration = 0.12f;
    [SerializeField] private float defaultMagnitude = 0.08f;

    private Vector3 originalPos;
    private Coroutine shakeCoroutine;

    /// <summary>
    /// 기본 값으로 카메라 흔들림을 실행합니다.
    /// </summary>
    public void Shake()
    {
        Shake(defaultDuration, defaultMagnitude);
    }

    /// <summary>
    /// 지정한 시간과 강도로 카메라 흔들림을 실행합니다.
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    /// <summary>
    /// 짧은 시간 동안 카메라 위치를 랜덤하게 흔든 뒤 원래 위치로 복구합니다.
    /// </summary>
    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        originalPos = transform.localPosition;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float offsetX = Random.Range(-1f, 1f) * magnitude;
            float offsetY = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originalPos;
        shakeCoroutine = null;
    }
}