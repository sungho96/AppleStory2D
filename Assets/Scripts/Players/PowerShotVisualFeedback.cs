using System.Collections;
using UnityEngine;

public class PowerShotVisualFeedback : MonoBehaviour
{
    private Transform firePoint;
    private PlayerController2D playerController;
    private SpriteRenderer chargeAura;
    private Sprite softCircleSprite;
    private Texture2D softCircleTexture;
    private int sortingLayerId;
    private int sortingOrder;
    private float chargeProgress;
    private bool playedFullChargeSparkle;

    public void Initialize(Transform assignedFirePoint, PlayerController2D assignedController)
    {
        if (chargeAura != null)
            return;

        firePoint = assignedFirePoint;
        playerController = assignedController;
        FindSortingReference();
        CreateSoftCircleSprite();

        // [파워 샷 연출 추가] 캐릭터 뒤에서 보이는 은은한 보라·금색 차징 오라입니다.
        GameObject auraObject = new GameObject("PowerShotChargeAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        chargeAura = auraObject.AddComponent<SpriteRenderer>();
        chargeAura.sprite = softCircleSprite;
        chargeAura.sortingLayerID = sortingLayerId;
        chargeAura.sortingOrder = sortingOrder - 1;
        chargeAura.gameObject.SetActive(false);
    }

    public void BeginCharge()
    {
        chargeProgress = 0f;
        playedFullChargeSparkle = false;
        chargeAura.gameObject.SetActive(true);
        UpdateChargeAura();
    }

    public void SetChargeProgress(float progress)
    {
        chargeProgress = Mathf.Clamp01(progress);
        UpdateChargeAura();

        if (chargeProgress >= 1f && !playedFullChargeSparkle)
        {
            // [파워 샷 완충 반짝임] 완충에 도달한 순간 한 번만 재생합니다.
            playedFullChargeSparkle = true;
            StartCoroutine(PlayFullChargeSparkle());
        }
    }

    public void Release(float power)
    {
        chargeAura.gameObject.SetActive(false);
        StartCoroutine(PlayReleaseBurst(Mathf.Clamp01(power)));
    }

    public void CancelCharge()
    {
        if (chargeAura != null)
            chargeAura.gameObject.SetActive(false);
    }

    private void UpdateChargeAura()
    {
        if (chargeAura == null || !chargeAura.gameObject.activeSelf)
            return;

        float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.07f;
        float size = Mathf.Lerp(0.55f, 1.2f, chargeProgress) * pulse;
        chargeAura.transform.localScale = Vector3.one * size;
        chargeAura.color = Color.Lerp(
            new Color(0.52f, 0.36f, 0.88f, 0.18f),
            new Color(1f, 0.68f, 0.22f, 0.48f),
            chargeProgress);
    }

    private IEnumerator PlayReleaseBurst(float power)
    {
        float direction = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;
        Vector3 origin = firePoint != null
            ? firePoint.position
            : transform.position + Vector3.up * 0.7f;

        GameObject flash = CreateEffectSprite("PowerShotReleaseFlash", origin, sortingOrder + 12);
        SpriteRenderer flashRenderer = flash.GetComponent<SpriteRenderer>();

        const int streakCount = 5;
        GameObject[] streaks = new GameObject[streakCount];
        for (int i = 0; i < streakCount; i++)
        {
            float yOffset = (i - 2) * 0.11f;
            streaks[i] = CreateEffectSprite(
                "PowerShotSpeedStreak",
                origin + new Vector3(0f, yOffset, 0f),
                sortingOrder + 11);
            streaks[i].transform.localScale = new Vector3(0.6f + power * 0.45f, 0.055f, 1f);
        }

        float duration = 0.16f + power * 0.05f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - ratio;

            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.05f + power * 0.35f, ratio);
            flashRenderer.color = new Color(1f, 0.82f, 0.34f, alpha * 0.8f);

            for (int i = 0; i < streakCount; i++)
            {
                streaks[i].transform.position += Vector3.right * direction *
                                                 (5.5f + i * 0.35f) * Time.deltaTime;
                SpriteRenderer renderer = streaks[i].GetComponent<SpriteRenderer>();
                renderer.color = new Color(0.72f, 0.52f, 1f, alpha * 0.72f);
            }

            yield return null;
        }

        Destroy(flash);
        foreach (GameObject streak in streaks)
            Destroy(streak);
    }

    private IEnumerator PlayFullChargeSparkle()
    {
        Vector3 center = transform.position + Vector3.up * 0.8f;
        GameObject flash = CreateEffectSprite(
            "PowerShotFullChargeFlash",
            center,
            sortingOrder + 13);

        const int sparkleCount = 8;
        GameObject[] sparkles = new GameObject[sparkleCount];
        Vector3[] directions = new Vector3[sparkleCount];
        for (int i = 0; i < sparkleCount; i++)
        {
            float angle = Mathf.PI * 2f * i / sparkleCount;
            directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            sparkles[i] = CreateEffectSprite(
                "PowerShotFullChargeSparkle",
                center,
                sortingOrder + 14);
            sparkles[i].transform.localScale = Vector3.one * 0.12f;
        }

        float elapsed = 0f;
        const float duration = 0.28f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - ratio;

            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.25f, 1.45f, ratio);
            flash.GetComponent<SpriteRenderer>().color =
                new Color(1f, 0.82f, 0.32f, alpha * 0.65f);

            for (int i = 0; i < sparkleCount; i++)
            {
                sparkles[i].transform.position = center +
                    directions[i] * Mathf.Lerp(0.15f, 0.85f, ratio);
                sparkles[i].transform.localScale = Vector3.one *
                    Mathf.Lerp(0.15f, 0.035f, ratio);
                sparkles[i].GetComponent<SpriteRenderer>().color = Color.Lerp(
                    new Color(1f, 0.9f, 0.42f, alpha),
                    new Color(0.7f, 0.48f, 1f, alpha),
                    ratio);
            }

            yield return null;
        }

        Destroy(flash);
        foreach (GameObject sparkle in sparkles)
            Destroy(sparkle);
    }

    private GameObject CreateEffectSprite(string objectName, Vector3 position, int order)
    {
        GameObject effectObject = new GameObject(objectName);
        effectObject.transform.position = position;
        SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
        renderer.sprite = softCircleSprite;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = order;
        return effectObject;
    }

    private void FindSortingReference()
    {
        SpriteRenderer reference = GetComponentInChildren<SpriteRenderer>(true);
        sortingLayerId = reference != null ? reference.sortingLayerID : 0;
        sortingOrder = reference != null ? reference.sortingOrder : 0;

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sortingLayerID == sortingLayerId)
                sortingOrder = Mathf.Max(sortingOrder, renderer.sortingOrder);
        }
    }

    private void CreateSoftCircleSprite()
    {
        const int textureSize = 64;
        softCircleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        softCircleTexture.name = "PowerShotSoftCircleTexture";

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 point = new Vector2(x, y) / (textureSize - 1f);
                float distance = Vector2.Distance(point, Vector2.one * 0.5f) * 2f;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;
                softCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        softCircleTexture.Apply();
        softCircleSprite = Sprite.Create(
            softCircleTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            textureSize);
    }

    private void OnDestroy()
    {
        if (softCircleSprite != null)
            Destroy(softCircleSprite);
        if (softCircleTexture != null)
            Destroy(softCircleTexture);
    }
}
