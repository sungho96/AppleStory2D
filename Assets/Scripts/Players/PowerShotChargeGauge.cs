using UnityEngine;

public class PowerShotChargeGauge : MonoBehaviour
{
    private const float GaugeWidth = 1.15f;
    private const float GaugeHeight = 0.12f;

    private Transform gaugeRoot;
    private SpriteRenderer fillRenderer;
    private Sprite runtimeSprite;
    private Texture2D runtimeTexture;

    public void Initialize()
    {
        if (gaugeRoot != null)
            return;

        // [파워 샷 게이지 추가] 외부 에셋 없이 플레이어 위에 월드 게이지를 생성합니다.
        runtimeTexture = new Texture2D(1, 1);
        runtimeTexture.name = "PowerShotGaugeTexture";
        runtimeTexture.SetPixel(0, 0, Color.white);
        runtimeTexture.Apply();
        runtimeSprite = Sprite.Create(runtimeTexture, new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f), 1f);

        gaugeRoot = new GameObject("PowerShotChargeGauge").transform;
        gaugeRoot.SetParent(transform, false);
        // [파워 샷 게이지 위치 조정] 캐릭터와 겹치지 않도록 높이를 2.2로 올립니다.
        gaugeRoot.localPosition = new Vector3(0f, 2.2f, 0f);

        SpriteRenderer referenceRenderer = GetComponentInChildren<SpriteRenderer>(true);
        int sortingLayerId = referenceRenderer != null ? referenceRenderer.sortingLayerID : 0;
        int sortingOrder = FindHighestSortingOrder(sortingLayerId) + 10;
        CreateBar("Border", new Vector2(GaugeWidth + 0.06f, GaugeHeight + 0.06f),
            new Color(0.28f, 0.16f, 0.06f, 0.95f), sortingLayerId, sortingOrder);
        CreateBar("Background", new Vector2(GaugeWidth, GaugeHeight),
            new Color(0.10f, 0.07f, 0.09f, 0.92f), sortingLayerId, sortingOrder + 1);
        fillRenderer = CreateBar("Fill", new Vector2(0f, GaugeHeight * 0.72f),
            new Color(1f, 0.72f, 0.16f, 1f), sortingLayerId, sortingOrder + 2);

        gaugeRoot.gameObject.SetActive(false);
    }

    public void Show()
    {
        Initialize();
        gaugeRoot.gameObject.SetActive(true);
        SetProgress(0f);
    }

    public void SetProgress(float progress)
    {
        if (fillRenderer == null)
            return;

        float ratio = Mathf.Clamp01(progress);
        fillRenderer.size = new Vector2(GaugeWidth * ratio, GaugeHeight * 0.72f);
        fillRenderer.transform.localPosition = new Vector3(
            -GaugeWidth * (1f - ratio) * 0.5f, 0f, -0.02f);

        // [파워 샷 게이지 추가] 완충에 가까워질수록 금색에서 밝은 주황색으로 변합니다.
        fillRenderer.color = Color.Lerp(
            new Color(1f, 0.72f, 0.16f, 1f),
            new Color(1f, 0.32f, 0.08f, 1f), ratio);
    }

    public void Hide()
    {
        if (gaugeRoot != null)
            gaugeRoot.gameObject.SetActive(false);
    }

    private SpriteRenderer CreateBar(
        string objectName,
        Vector2 size,
        Color color,
        int sortingLayerId,
        int sortingOrder)
    {
        GameObject barObject = new GameObject(objectName);
        barObject.transform.SetParent(gaugeRoot, false);

        SpriteRenderer renderer = barObject.AddComponent<SpriteRenderer>();
        renderer.sprite = runtimeSprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private int FindHighestSortingOrder(int sortingLayerId)
    {
        int highestOrder = 0;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.sortingLayerID == sortingLayerId)
                highestOrder = Mathf.Max(highestOrder, renderer.sortingOrder);
        }
        return highestOrder;
    }

    private void OnDestroy()
    {
        if (runtimeSprite != null)
            Destroy(runtimeSprite);
        if (runtimeTexture != null)
            Destroy(runtimeTexture);
    }
}
