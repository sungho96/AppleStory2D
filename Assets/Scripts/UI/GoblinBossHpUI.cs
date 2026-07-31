using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레퍼런스처럼 화면 상단에 초상, 긴 HP바, 남은 체력 퍼센트를 표시합니다.
/// </summary>
public class GoblinBossHpUI : MonoBehaviour
{
    [Header("Boss Target")]
    [SerializeField] private GoblinHealth2D bossHealth;
    [SerializeField] private string bossObjectName = "Goblin (3)";
    [SerializeField] private Sprite bossIcon;

    [Header("Layout")]
    [SerializeField] private Vector2 barSize = new Vector2(900f, 28f);
    [SerializeField] private float topMargin = 16f;

    [Header("Color")]
    [SerializeField] private Color frameColor = new Color(0.78f, 0.82f, 0.80f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.07f, 0.07f, 0.96f);
    [SerializeField] private Color hpColor = new Color(0.86f, 0.08f, 0.04f, 1f);

    private Image hpFill;
    private RectTransform hpFillRect;
    private Image portrait;
    private TextMeshProUGUI percentText;
    private GameObject uiRoot;
    private Sprite roundedUiSprite;

    private void Awake()
    {
        // [보스 UI 개선] Spring 씬에서 보스로 사용할 가장 오른쪽 고블린을 연결합니다.
        FindBossIfNeeded();
        CreateBossHpUI();
        RefreshPortrait();
        Refresh();
    }

    private void LateUpdate()
    {
        if (bossHealth == null)
        {
            FindBossIfNeeded();
            SetVisible(false);
            return;
        }

        SetVisible(!bossHealth.IsDead);
        Refresh();
    }

    private void FindBossIfNeeded()
    {
        if (bossHealth != null)
            return;

        GameObject bossObject = GameObject.Find(bossObjectName);
        if (bossObject != null)
            bossHealth = bossObject.GetComponent<GoblinHealth2D>();
    }

    private void CreateBossHpUI()
    {
        // [보스 UI 개선] 씬 변경량을 줄이면서 레퍼런스형 UI를 실행 시 구성합니다.
        // [보스 UI 오류 수정] Unity 버전에 따라 없는 내장 UISprite 경로 대신
        // 둥근 9-slice 스프라이트를 런타임에 직접 만들어 사용합니다.
        roundedUiSprite = CreateRoundedUiSprite();

        uiRoot = new GameObject("GoblinBossHpUI", typeof(RectTransform));
        uiRoot.transform.SetParent(transform, false);

        RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -topMargin);
        rootRect.sizeDelta = new Vector2(barSize.x + 82f, 68f);

        Image portraitFrame = CreateImage("PortraitFrame", uiRoot.transform, frameColor);
        SetRect(portraitFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), Vector2.zero, new Vector2(58f, 58f));

        portrait = CreateImage("Portrait", portraitFrame.transform, new Color(0.18f, 0.24f, 0.20f, 1f));
        portrait.preserveAspect = true;
        Stretch(portrait.rectTransform, 4f);

        Image barShadow = CreateImage("BarShadow", uiRoot.transform, new Color(0.03f, 0.02f, 0.01f, 0.72f));
        SetRect(barShadow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(69f, -6f), new Vector2(barSize.x, barSize.y + 2f));

        Image barFrame = CreateImage("BarFrame", uiRoot.transform, frameColor);
        SetRect(barFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(66f, -3f), new Vector2(barSize.x, barSize.y + 2f));

        Image barBackground = CreateImage("BarBackground", barFrame.transform, backgroundColor);
        Stretch(barBackground.rectTransform, 3f);

        hpFill = CreateImage("HpFill", barBackground.transform, hpColor);
        hpFillRect = hpFill.rectTransform;
        hpFillRect.anchorMin = Vector2.zero;
        hpFillRect.anchorMax = Vector2.one;
        hpFillRect.offsetMin = new Vector2(2f, 2f);
        hpFillRect.offsetMax = new Vector2(-2f, -2f);

        Image shine = CreateImage("HpShine", hpFill.transform, new Color(1f, 0.72f, 0.48f, 0.28f));
        RectTransform shineRect = shine.rectTransform;
        shineRect.anchorMin = new Vector2(0f, 0.56f);
        shineRect.anchorMax = Vector2.one;
        shineRect.offsetMin = new Vector2(5f, 0f);
        shineRect.offsetMax = new Vector2(-5f, -2f);

        TextMeshProUGUI bossNameText = CreateText("BossName", barBackground.transform, 16f, TextAlignmentOptions.MidlineLeft);
        bossNameText.text = "GOBLIN BOSS";
        bossNameText.margin = new Vector4(10f, 0f, 0f, 0f);
        Stretch(bossNameText.rectTransform, 0f);

        percentText = CreateText("Percent", uiRoot.transform, 17f, TextAlignmentOptions.MidlineLeft);
        percentText.color = new Color(1f, 0.86f, 0.60f, 1f);
        percentText.fontStyle = FontStyles.Bold;
        percentText.margin = new Vector4(12f, 0f, 0f, 0f);
        SetRect(percentText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(66f, -37f), new Vector2(barSize.x, 23f));
    }

    private void RefreshPortrait()
    {
        if (portrait == null || bossHealth == null)
            return;

        // [고블린 보스 아이콘] 프리팹 얼굴 파츠를 참고해 제작한 전용 아이콘을 우선 표시합니다.
        if (bossIcon != null)
        {
            portrait.sprite = bossIcon;
            portrait.type = Image.Type.Simple;
            portrait.color = Color.white;
            return;
        }

        // [보스 UI 개선] 별도 외부 에셋 없이 고블린의 Head 스프라이트를 초상으로 재사용합니다.
        SpriteRenderer[] renderers = bossHealth.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null && spriteRenderer.gameObject.name == "Head" &&
                spriteRenderer.gameObject.activeInHierarchy && spriteRenderer.sprite != null)
            {
                portrait.sprite = spriteRenderer.sprite;
                portrait.type = Image.Type.Simple;
                portrait.color = Color.white;
                return;
            }
        }

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null && spriteRenderer.gameObject.name == "Head" && spriteRenderer.sprite != null)
            {
                portrait.sprite = spriteRenderer.sprite;
                portrait.type = Image.Type.Simple;
                portrait.color = Color.white;
                return;
            }
        }
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        if (roundedUiSprite != null)
        {
            image.sprite = roundedUiSprite;
            image.type = Image.Type.Sliced;
        }
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void Refresh()
    {
        if (bossHealth == null)
            return;

        float ratio = bossHealth.MaxHp > 0
            ? Mathf.Clamp01(bossHealth.CurrentHp / (float)bossHealth.MaxHp)
            : 0f;

        if (hpFillRect != null)
        {
            hpFillRect.anchorMax = new Vector2(ratio, 1f);
            hpFillRect.offsetMax = new Vector2(ratio > 0f ? -2f : 0f, -2f);
        }

        if (percentText != null)
            percentText.text = $"{ratio * 100f:0.0}%";
    }

    private void SetVisible(bool visible)
    {
        if (uiRoot != null && uiRoot.activeSelf != visible)
            uiRoot.SetActive(visible);
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Sprite CreateRoundedUiSprite()
    {
        const int textureSize = 32;
        const float cornerRadius = 8f;

        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "RuntimeRoundedUISpriteTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[textureSize * textureSize];
        Vector2 bottomLeftCenter = new Vector2(cornerRadius, cornerRadius);
        Vector2 bottomRightCenter = new Vector2(textureSize - cornerRadius, cornerRadius);
        Vector2 topLeftCenter = new Vector2(cornerRadius, textureSize - cornerRadius);
        Vector2 topRightCenter = new Vector2(textureSize - cornerRadius, textureSize - cornerRadius);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 sample = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = true;

                if (sample.x < cornerRadius && sample.y < cornerRadius)
                    inside = Vector2.Distance(sample, bottomLeftCenter) <= cornerRadius;
                else if (sample.x > textureSize - cornerRadius && sample.y < cornerRadius)
                    inside = Vector2.Distance(sample, bottomRightCenter) <= cornerRadius;
                else if (sample.x < cornerRadius && sample.y > textureSize - cornerRadius)
                    inside = Vector2.Distance(sample, topLeftCenter) <= cornerRadius;
                else if (sample.x > textureSize - cornerRadius && sample.y > textureSize - cornerRadius)
                    inside = Vector2.Distance(sample, topRightCenter) <= cornerRadius;

                pixels[y * textureSize + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));

        sprite.name = "RuntimeRoundedUISprite";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
