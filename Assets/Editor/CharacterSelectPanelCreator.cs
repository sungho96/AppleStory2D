#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterSelectPanelCreator
{
    private const string BackgroundPath = "Assets/Art/UI/CharacterSelect/CharacterSelect_Background.png";
    private const string TitlePath = "Assets/Art/UI/CharacterSelect/CharacterSelect_Title.png";
    private const string ArcherCardPath = "Assets/Art/UI/CharacterSelect/ArcherCard.png";
    private const string WarriorCardPath = "Assets/Art/UI/CharacterSelect/WarriorCard.png";
    private const string ConfirmButtonPath = "Assets/Art/UI/CharacterSelect/ConfirmButton.png";

    [MenuItem("AppleStory/UI/Create Character Select Panel")]
    public static void CreateCharacterSelectPanel()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null || canvas.name != "GameEntryCanvas")
        {
            GameObject canvasObject = GameObject.Find("GameEntryCanvas");
            canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
        }

        if (canvas == null)
        {
            Debug.LogError("[CharacterSelectPanelCreator] GameEntryCanvas를 찾지 못했습니다.");
            return;
        }

        GameObject panel = EnsurePanel(canvas.transform);
        EnsureContents(panel.transform);
        panel.SetActive(false);

        GameEntryCharacterSelectPanelController controller =
            panel.GetComponent<GameEntryCharacterSelectPanelController>() ??
            panel.AddComponent<GameEntryCharacterSelectPanelController>();

        controller.Initialize(
            panel.transform.Find("ArcherButton")?.GetComponent<Button>(),
            panel.transform.Find("ArcherButton/ArcherCardImage")?.GetComponent<Image>(),
            panel.transform.Find("WarriorButton")?.GetComponent<Button>(),
            panel.transform.Find("WarriorButton/WarriorCardImage")?.GetComponent<Image>(),
            panel.transform.Find("ConfirmButton")?.GetComponent<Button>(),
            panel.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>());

        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Debug.Log("[CharacterSelectPanelCreator] CharacterSelectPanel 생성/갱신 완료.");
    }

    private static GameObject EnsurePanel(Transform canvasTransform)
    {
        Transform existing = canvasTransform.Find("CharacterSelectPanel");
        if (existing != null)
            return existing.gameObject;

        GameObject panel = new GameObject("CharacterSelectPanel", typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(canvasTransform, false);
        StretchToParent(panel.GetComponent<RectTransform>());
        return panel;
    }

    private static void EnsureContents(Transform panel)
    {
        GameObject background = EnsureImage(panel, "Background", LoadSprite(BackgroundPath), Color.white);
        StretchToParent(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().raycastTarget = true;

        GameObject title = EnsureImage(panel, "TitleImage", LoadSprite(TitlePath), Color.white);
        SetCentered(title.GetComponent<RectTransform>(), new Vector2(720f, 150f), new Vector2(0f, 325f));

        EnsureCard(panel, "ArcherButton", "ArcherCardImage", LoadSprite(ArcherCardPath), new Vector2(-360f, -20f));
        EnsureCard(panel, "WarriorButton", "WarriorCardImage", LoadSprite(WarriorCardPath), new Vector2(360f, -20f));
        EnsureConfirmButton(panel);
        EnsureStatusText(panel);
    }

    private static void EnsureCard(Transform parent, string buttonName, string imageName, Sprite sprite, Vector2 position)
    {
        Transform existing = parent.Find(buttonName);
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

        buttonObject.transform.SetParent(parent, false);
        SetCentered(buttonObject.GetComponent<RectTransform>(), new Vector2(455f, 590f), position);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.clear;
        buttonImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        GameObject card = EnsureImage(buttonObject.transform, imageName, sprite, Color.white);
        StretchToParent(card.GetComponent<RectTransform>());
        card.GetComponent<Image>().raycastTarget = true;
        button.targetGraphic = card.GetComponent<Image>();
    }

    private static void EnsureConfirmButton(Transform parent)
    {
        Transform existing = parent.Find("ConfirmButton");
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : new GameObject("ConfirmButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

        buttonObject.transform.SetParent(parent, false);
        SetCentered(buttonObject.GetComponent<RectTransform>(), new Vector2(310f, 120f), new Vector2(0f, -390f));

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.clear;
        buttonImage.raycastTarget = true;

        GameObject confirmImage = EnsureImage(buttonObject.transform, "ConfirmImage", LoadSprite(ConfirmButtonPath), Color.white);
        StretchToParent(confirmImage.GetComponent<RectTransform>());
        confirmImage.GetComponent<Image>().raycastTarget = false;
        buttonObject.GetComponent<Button>().targetGraphic = confirmImage.GetComponent<Image>();
    }

    private static void EnsureStatusText(Transform parent)
    {
        if (parent.Find("StatusText") != null)
            return;

        GameObject textObject = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        SetCentered(textObject.GetComponent<RectTransform>(), new Vector2(760f, 70f), new Vector2(0f, -305f));

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = "캐릭터를 선택하세요.";
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.92f, 0.62f, 1f);
        text.raycastTarget = false;
    }

    private static GameObject EnsureImage(Transform parent, string name, Sprite sprite, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject imageObject = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return imageObject;
    }

    private static Sprite LoadSprite(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite largestSprite = null;
        float largestArea = 0f;

        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
                continue;

            float area = sprite.rect.width * sprite.rect.height;
            if (area > largestArea)
            {
                largestArea = area;
                largestSprite = sprite;
            }
        }

        return largestSprite;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetCentered(RectTransform rectTransform, Vector2 size, Vector2 position)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
    }
}
#endif
