using System.Collections;
using UnityEngine;

/// <summary>
/// 얼음 공격으로 받은 이동속도 둔화의 지속시간과 간단한 시각 효과를 관리합니다.
/// </summary>
public class PlayerIceSlow2D : MonoBehaviour
{
    private PlayerMovement2D playerMovement;
    private Coroutine slowRoutine;
    private GameObject frostEffect;
    private Sprite frostSprite;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement2D>();
        frostSprite = Resources.Load<Sprite>("Boss/PlayerIceSlow_AnkleFrost");
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (playerMovement == null)
            return;

        // [얼음 둔화 갱신] 재피격 시 강도는 중첩하지 않고 기존 코루틴을 멈춰 지속시간만 처음부터 시작합니다.
        if (slowRoutine != null)
            StopCoroutine(slowRoutine);

        slowRoutine = StartCoroutine(SlowRoutine(Mathf.Clamp01(multiplier), Mathf.Max(0.1f, duration)));
    }

    private IEnumerator SlowRoutine(float multiplier, float duration)
    {
        playerMovement.SetSlowMultiplier(multiplier);
        CreateFrostEffect();

        float timer = 0f;
        SpriteRenderer frostRenderer = frostEffect != null ? frostEffect.GetComponent<SpriteRenderer>() : null;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (frostEffect != null)
            {
                float pulse = 1f + Mathf.Sin(timer * 8f) * 0.035f;
                float remaining = duration - timer;
                float endFade = Mathf.Clamp01(remaining / 0.25f);
                // [발목 빙결 표시] 종료 직전 얼음이 흔들리며 투명해져 깨지는 느낌을 냅니다.
                float shake = remaining < 0.25f ? Mathf.Sin(timer * 65f) * 0.025f : 0f;
                frostEffect.transform.localPosition = new Vector3(shake, -0.05f, 0f);
                frostEffect.transform.localScale = Vector3.one * (0.12f * pulse * Mathf.Lerp(0.82f, 1f, endFade));
                if (frostRenderer != null)
                    frostRenderer.color = new Color(1f, 1f, 1f, endFade);
            }
            yield return null;
        }

        playerMovement.ResetSlowMultiplier();
        if (frostEffect != null)
            Destroy(frostEffect);
        frostEffect = null;
        slowRoutine = null;
    }

    private void CreateFrostEffect()
    {
        if (frostEffect != null)
            Destroy(frostEffect);

        frostEffect = new GameObject("Player_IceSlowEffect");
        frostEffect.transform.SetParent(transform, false);
        // [발목 얼음 위치 보정] 이미지 중심의 투명 여백을 고려해 플레이어 발목 높이까지 올립니다.
        frostEffect.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        frostEffect.transform.localScale = Vector3.one * 0.12f;

        SpriteRenderer renderer = frostEffect.AddComponent<SpriteRenderer>();
        renderer.sprite = frostSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 45;
    }

    private void OnDisable()
    {
        if (playerMovement != null)
            playerMovement.ResetSlowMultiplier();
    }

}
