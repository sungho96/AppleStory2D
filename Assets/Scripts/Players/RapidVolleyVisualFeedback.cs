using System.Collections;
using UnityEngine;

public class RapidVolleyVisualFeedback : MonoBehaviour
{
    private Sprite softCircleSprite;
    private Texture2D softCircleTexture;
    private int sortingLayerId;
    private int characterSortingOrder;

    public static Color GetShotColor(int shotIndex)
    {
        // [래피드 볼리 컬러 연출] 그라데이션 대신 발마다 의도적으로 구분한 고정 팔레트를 사용합니다.
        switch (shotIndex)
        {
            case 0:
                return new Color(0.05f, 0.95f, 1f, 1f);   // 전기 청록
            case 1:
                return new Color(1f, 0.12f, 0.72f, 1f);   // 네온 마젠타
            default:
                return new Color(0.9f, 0.72f, 1f, 1f);    // 백색에 가까운 보라
        }
    }

    public void Initialize()
    {
        if (softCircleSprite != null)
            return;

        SpriteRenderer reference = GetComponentInChildren<SpriteRenderer>(true);
        sortingLayerId = reference != null ? reference.sortingLayerID : 0;
        characterSortingOrder = reference != null ? reference.sortingOrder : 0;
        CreateSoftCircleSprite();
    }

    public void PlayCastEffect(float direction)
    {
        if (softCircleSprite == null)
            Initialize();

        StartCoroutine(PlayAura(direction));
    }

    public void PlayShotEffect(int shotIndex, float direction)
    {
        if (softCircleSprite == null)
            Initialize();

        StartCoroutine(PlayShotBurst(shotIndex, direction));
    }

    private IEnumerator PlayAura(float direction)
    {
        Vector3 center = transform.position + Vector3.up * 0.72f;
        GameObject aura = CreateEffect("RapidVolleyCastAura", center, characterSortingOrder - 2);
        GameObject crossSlashA = CreateEffect("RapidVolleyCrossSlashA", center, characterSortingOrder - 1);
        GameObject crossSlashB = CreateEffect("RapidVolleyCrossSlashB", center, characterSortingOrder - 1);
        crossSlashA.transform.rotation = Quaternion.Euler(0f, 0f, 28f * direction);
        crossSlashB.transform.rotation = Quaternion.Euler(0f, 0f, -28f * direction);

        // [래피드 볼리 강화] 작은 불꽃 대신 전신을 감싸는 회전 바람 궤도를 구성합니다.
        const int orbitCount = 20;
        GameObject[] orbits = new GameObject[orbitCount];
        for (int i = 0; i < orbitCount; i++)
        {
            orbits[i] = CreateEffect(
                "RapidVolleyWindOrbit",
                center,
                characterSortingOrder - 1);
        }

        float elapsed = 0f;
        const float duration = 0.82f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f + Mathf.Sin(ratio * Mathf.PI * 8f) * 0.1f;
            float alpha = Mathf.Sin(ratio * Mathf.PI);
            center = transform.position + Vector3.up * 0.76f;

            aura.transform.position = center;
            // [래피드 볼리 조정] 중심 오라만 25% 줄이고 바깥 회전 궤도 크기는 유지합니다.
            aura.transform.localScale = new Vector3(2.48f, 2.81f, 1f) * pulse;
            aura.GetComponent<SpriteRenderer>().color =
                new Color(0.48f, 0.25f, 1f, alpha * 0.27f);

            float slashLength = Mathf.Lerp(0.6f, 4.4f, Mathf.SmoothStep(0f, 1f, ratio));
            crossSlashA.transform.position = center;
            crossSlashB.transform.position = center;
            crossSlashA.transform.localScale = new Vector3(slashLength, 0.14f, 1f);
            crossSlashB.transform.localScale = new Vector3(slashLength, 0.11f, 1f);
            crossSlashA.GetComponent<SpriteRenderer>().color =
                new Color(0.74f, 0.52f, 1f, alpha * 0.88f);
            crossSlashB.GetComponent<SpriteRenderer>().color =
                new Color(0.38f, 0.92f, 1f, alpha * 0.78f);

            for (int i = 0; i < orbitCount; i++)
            {
                float angle = ratio * Mathf.PI * 5f * direction +
                              Mathf.PI * 2f * i / orbitCount;
                float radiusPulse = 1f + Mathf.Sin(angle * 2f) * 0.12f;
                Vector3 orbitPosition = new Vector3(
                    Mathf.Cos(angle) * 1.9f * radiusPulse,
                    Mathf.Sin(angle) * 1.48f,
                    0f);
                orbits[i].transform.position = center + orbitPosition;
                orbits[i].transform.rotation = Quaternion.Euler(
                    0f, 0f, angle * Mathf.Rad2Deg + 90f);
                orbits[i].transform.localScale = new Vector3(
                    i % 2 == 0 ? 0.72f : 0.5f,
                    i % 2 == 0 ? 0.105f : 0.07f,
                    1f);
                orbits[i].GetComponent<SpriteRenderer>().color = i % 2 == 0
                    ? new Color(0.66f, 0.42f, 1f, alpha * 0.94f)
                    : new Color(0.32f, 0.94f, 1f, alpha * 0.86f);
            }

            yield return null;
        }

        Destroy(aura);
        Destroy(crossSlashA);
        Destroy(crossSlashB);
        foreach (GameObject orbit in orbits)
            Destroy(orbit);
    }

    private IEnumerator PlayShotBurst(int shotIndex, float direction)
    {
        bool finalShot = shotIndex == 2;
        float shotStrength = 1f + shotIndex * 0.22f;
        Color shotColor = GetShotColor(shotIndex);
        Vector3 center = transform.position + new Vector3(direction * 0.18f, 0.78f, 0f);
        GameObject flash = CreateEffect(
            "RapidVolleyBodyFlash", center, characterSortingOrder - 1);
        GameObject impactLineA = CreateEffect(
            "RapidVolleyImpactLineA", center, characterSortingOrder - 1);
        GameObject impactLineB = CreateEffect(
            "RapidVolleyImpactLineB", center, characterSortingOrder - 1);
        impactLineA.transform.rotation = Quaternion.Euler(0f, 0f, 38f);
        impactLineB.transform.rotation = Quaternion.Euler(0f, 0f, -38f);

        int sparkCount = finalShot ? 22 : 13;
        GameObject[] sparks = new GameObject[sparkCount];
        Vector3[] sparkDirections = new Vector3[sparkCount];
        for (int i = 0; i < sparkCount; i++)
        {
            float angle = Mathf.Lerp(-1.8f, 1.8f, i / Mathf.Max(1f, sparkCount - 1f));
            sparkDirections[i] = new Vector3(
                direction * Mathf.Cos(angle) * 2.15f,
                Mathf.Sin(angle) * 1.45f,
                0f);
            sparks[i] = CreateEffect(
                "RapidVolleyBodySpark", center, characterSortingOrder - 1);
        }

        float elapsed = 0f;
        float duration = finalShot ? 0.34f : 0.22f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - ratio;

            flash.transform.localScale = Vector3.one *
                Mathf.Lerp(0.55f, finalShot ? 4.4f : 2.5f, ratio);
            flash.GetComponent<SpriteRenderer>().color =
                new Color(shotColor.r, shotColor.g, shotColor.b,
                    alpha * (finalShot ? 0.78f : 0.58f));

            // [래피드 볼리 후처리] 발사할수록 커지는 교차 충격선으로 3연사의 누적감을 만듭니다.
            float impactLength = Mathf.Lerp(0.4f, finalShot ? 5.4f : 3.3f, ratio) * shotStrength;
            impactLineA.transform.position = center;
            impactLineB.transform.position = center;
            impactLineA.transform.localScale = new Vector3(impactLength, 0.1f, 1f);
            impactLineB.transform.localScale = new Vector3(impactLength * 0.82f, 0.075f, 1f);
            impactLineA.GetComponent<SpriteRenderer>().color =
                new Color(shotColor.r, shotColor.g, shotColor.b, alpha * 0.82f);
            impactLineB.GetComponent<SpriteRenderer>().color =
                new Color(0.5f, 0.9f, 1f, alpha * 0.66f);

            for (int i = 0; i < sparkCount; i++)
            {
                sparks[i].transform.position = center + sparkDirections[i] *
                    Mathf.Lerp(0.15f, finalShot ? 2.5f : 1.6f, ratio);
                sparks[i].transform.localScale = new Vector3(
                    Mathf.Lerp(finalShot ? 0.68f : 0.46f, 0.05f, ratio),
                    Mathf.Lerp(finalShot ? 0.17f : 0.12f, 0.02f, ratio),
                    1f);
                sparks[i].GetComponent<SpriteRenderer>().color = Color.Lerp(
                    new Color(0.48f, 0.9f, 1f, alpha),
                    new Color(0.68f, 0.38f, 1f, alpha),
                    ratio);
            }

            yield return null;
        }

        Destroy(flash);
        Destroy(impactLineA);
        Destroy(impactLineB);
        foreach (GameObject spark in sparks)
            Destroy(spark);
    }

    private GameObject CreateEffect(string objectName, Vector3 position, int sortingOrder)
    {
        GameObject effectObject = new GameObject(objectName);
        effectObject.transform.position = position;
        SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
        renderer.sprite = softCircleSprite;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return effectObject;
    }

    private void CreateSoftCircleSprite()
    {
        const int size = 64;
        softCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        softCircleTexture.name = "RapidVolleySoftCircleTexture";
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y) / (size - 1f);
                float distance = Vector2.Distance(point, Vector2.one * 0.5f) * 2f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2f);
                softCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        softCircleTexture.Apply();
        softCircleSprite = Sprite.Create(
            softCircleTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
    }

    private void OnDestroy()
    {
        if (softCircleSprite != null)
            Destroy(softCircleSprite);
        if (softCircleTexture != null)
            Destroy(softCircleTexture);
    }
}
