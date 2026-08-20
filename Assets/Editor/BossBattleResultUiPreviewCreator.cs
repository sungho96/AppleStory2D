using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BossBattleResultUiPreviewCreator
{
    private const string VictorySpritePath =
        "Assets/Art/Resource/Cartoon/victory.png";

    private const string DefeatSpritePath =
        "Assets/Art/Resource/Cartoon/Defeat.png";

    [MenuItem("AppleStory/Boss/Create Result UI Preview")]
    public static void CreateResultUiPreview()
    {
        // =========================================================
        // 기존 Canvas가 있으면 삭제하고 새로 생성
        // =========================================================

        GameObject existing =
            GameObject.Find("BossBattle_ResultCanvas");

        if (existing != null)
        {
            bool delete =
                EditorUtility.DisplayDialog(
                    "Boss Result UI",
                    "기존 BossBattle_ResultCanvas가 있습니다.\n삭제하고 새로 생성할까요?",
                    "새로 생성",
                    "취소"
                );

            if (!delete)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            Undo.DestroyObjectImmediate(existing);
        }

        // =========================================================
        // Canvas
        // =========================================================

        GameObject canvasObject =
            new GameObject(
                "BossBattle_ResultCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Create Boss Result UI Preview"
        );

        Canvas canvas =
            canvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder =
            500;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(1920f, 1080f);

        // =========================================================
        // Fade
        // =========================================================

        GameObject fadeObject =
            CreateChild(
                canvasObject.transform,
                "Result_Fade",
                typeof(Image)
            );

        Image fadeImage =
            fadeObject.GetComponent<Image>();

        fadeImage.color =
            new Color(
                0f,
                0f,
                0f,
                0.58f
            );

        fadeImage.raycastTarget =
            false;

        Stretch(
            fadeObject.GetComponent<RectTransform>()
        );

        // =========================================================
        // Victory UI
        // =========================================================

        GameObject victoryRoot =
            CreateResultRoot(
                canvasObject.transform,
                "Victory_Result_ImageRoot",
                VictorySpritePath
            );

        CreateClearTimeText(
            victoryRoot.transform,
            new Vector2(0f, -120f)
        );

        CreateRestartButton(
            victoryRoot.transform,
            new Vector2(0f, -210f)
        );

        // =========================================================
        // Defeat UI
        // =========================================================

        GameObject defeatRoot =
            CreateResultRoot(
                canvasObject.transform,
                "Defeat_Result_ImageRoot",
                DefeatSpritePath
            );

        // Defeat는 처음부터 조금 위로
        // 이후 Inspector에서 자유롭게 수정 가능
        CreateClearTimeText(
            defeatRoot.transform,
            new Vector2(0f, -95f)
        );

        CreateRestartButton(
            defeatRoot.transform,
            new Vector2(0f, -175f)
        );

        // =========================================================
        // Preview에서는 Victory만 표시
        // =========================================================

        victoryRoot.SetActive(true);
        defeatRoot.SetActive(false);

        // =========================================================
        // 선택
        // =========================================================

        Selection.activeGameObject =
            victoryRoot;

        EditorGUIUtility.PingObject(
            victoryRoot
        );

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
        );

        Debug.Log(
            "[BossResultPreview] Victory / Defeat UI 생성 완료"
        );
    }

    // =============================================================
    // Result Root 생성
    // =============================================================

    private static GameObject CreateResultRoot(
        Transform parent,
        string objectName,
        string spritePath
    )
    {
        GameObject imageObject =
            CreateChild(
                parent,
                objectName,
                typeof(Image)
            );

        Image resultImage =
            imageObject.GetComponent<Image>();

        resultImage.sprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(
                spritePath
            );

        resultImage.preserveAspect =
            true;

        resultImage.color =
            Color.white;

        RectTransform imageRect =
            imageObject.GetComponent<RectTransform>();

        imageRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        imageRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        imageRect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        imageRect.sizeDelta =
            new Vector2(
                800f,
                492f
            );

        imageRect.anchoredPosition =
            Vector2.zero;

        return imageObject;
    }

    // =============================================================
    // Clear Time
    // =============================================================

    private static void CreateClearTimeText(
        Transform parent,
        Vector2 anchoredPosition
    )
    {
        GameObject textObject =
            CreateChild(
                parent,
                "ClearTimeText",
                typeof(TextMeshProUGUI)
            );

        TextMeshProUGUI timeText =
            textObject.GetComponent<TextMeshProUGUI>();

        timeText.text =
            "Clear Time 00:31";

        timeText.alignment =
            TextAlignmentOptions.Center;

        timeText.color =
            Color.white;

        timeText.fontSize =
            46f;

        timeText.enableAutoSizing =
            true;

        timeText.fontSizeMin =
            16f;

        timeText.fontSizeMax =
            46f;

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        textRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        textRect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        textRect.sizeDelta =
            new Vector2(
                560f,
                90f
            );

        textRect.anchoredPosition =
            anchoredPosition;
    }

    // =============================================================
    // Restart Button
    // =============================================================

    private static void CreateRestartButton(
        Transform parent,
        Vector2 anchoredPosition
    )
    {
        GameObject buttonObject =
            CreateChild(
                parent,
                "RestartButton",
                typeof(Image),
                typeof(Button)
            );

        Image buttonImage =
            buttonObject.GetComponent<Image>();

        buttonImage.color =
            new Color(
                0.18f,
                0.22f,
                0.28f,
                0.95f
            );

        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();

        buttonRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );

        buttonRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );

        buttonRect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );

        buttonRect.sizeDelta =
            new Vector2(
                220f,
                54f
            );

        buttonRect.anchoredPosition =
            anchoredPosition;

        // =========================================================
        // Restart Text
        // =========================================================

        GameObject buttonTextObject =
            CreateChild(
                buttonObject.transform,
                "RestartText",
                typeof(TextMeshProUGUI)
            );

        TextMeshProUGUI buttonText =
            buttonTextObject
                .GetComponent<TextMeshProUGUI>();

        buttonText.text =
            "Restart";

        buttonText.alignment =
            TextAlignmentOptions.Center;

        buttonText.color =
            Color.white;

        buttonText.fontSize =
            24f;

        Stretch(
            buttonTextObject
                .GetComponent<RectTransform>()
        );
    }

    // =============================================================
    // Child 생성
    // =============================================================

    private static GameObject CreateChild(
        Transform parent,
        string objectName,
        params System.Type[] components
    )
    {
        GameObject child =
            new GameObject(
                objectName,
                components
            );

        Undo.RegisterCreatedObjectUndo(
            child,
            "Create Boss Result UI Preview Child"
        );

        child.transform.SetParent(
            parent,
            false
        );

        return child;
    }

    // =============================================================
    // Stretch
    // =============================================================

    private static void Stretch(
        RectTransform rect
    )
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }
}