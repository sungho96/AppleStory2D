using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BossBattleResultUiPreviewCreator
{
    private const string VictorySpritePath = "Assets/Art/Resource/Cartoon/victory.png";

    [MenuItem("AppleStory/Boss/Create Result UI Preview")]
    public static void CreateResultUiPreview()
    {
        GameObject existing = GameObject.Find("BossBattle_ResultCanvas");
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        GameObject canvasObject = new GameObject("BossBattle_ResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Boss Result UI Preview");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject fadeObject = CreateChild(canvasObject.transform, "Result_Fade", typeof(Image));
        Image fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0.58f);
        fadeImage.raycastTarget = false;
        Stretch(fadeObject.GetComponent<RectTransform>());

        GameObject imageObject = CreateChild(canvasObject.transform, "Result_ImageRoot", typeof(Image));
        Image resultImage = imageObject.GetComponent<Image>();
        resultImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(VictorySpritePath);
        resultImage.preserveAspect = true;
        resultImage.color = Color.white;

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(800f, 492f);
        imageRect.anchoredPosition = Vector2.zero;

        GameObject textObject = CreateChild(imageObject.transform, "ClearTimeText", typeof(TextMeshProUGUI));
        TextMeshProUGUI timeText = textObject.GetComponent<TextMeshProUGUI>();
        timeText.text = "Clear Time 00:31";
        timeText.alignment = TextAlignmentOptions.Center;
        timeText.color = Color.white;
        timeText.fontSize = 46f;
        timeText.enableAutoSizing = true;
        timeText.fontSizeMin = 16f;
        timeText.fontSizeMax = 46f;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(560f, 90f);
        textRect.anchoredPosition = new Vector2(0f, -120f);

        GameObject buttonObject = CreateChild(imageObject.transform, "RestartButton", typeof(Image), typeof(Button));
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(220f, 54f);
        buttonRect.anchoredPosition = new Vector2(0f, -210f);

        GameObject buttonTextObject = CreateChild(buttonObject.transform, "RestartText", typeof(TextMeshProUGUI));
        TextMeshProUGUI buttonText = buttonTextObject.GetComponent<TextMeshProUGUI>();
        buttonText.text = "Restart";
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = Color.white;
        buttonText.fontSize = 24f;
        Stretch(buttonTextObject.GetComponent<RectTransform>());

        Selection.activeGameObject = imageObject;
        EditorGUIUtility.PingObject(imageObject);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static GameObject CreateChild(Transform parent, string objectName, params System.Type[] components)
    {
        GameObject child = new GameObject(objectName, components);
        Undo.RegisterCreatedObjectUndo(child, "Create Boss Result UI Preview Child");
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
