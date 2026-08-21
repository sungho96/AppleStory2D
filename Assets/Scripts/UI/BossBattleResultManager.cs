using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using System.Reflection;

public class BossBattleResultManager : MonoBehaviour
{
    private const string ResultMessageName = "BossBattleResult";
    private const string RestartMessageName = "BossBattleRestart";
    private const string RestartTargetSceneName = "GameEntry";
    private static BossBattleResultManager instance;

    [Header("Timing")]
    [SerializeField] private float finishHitStopDuration = 0.16f;
    [SerializeField] private float finishTimeScale = 0.08f;
    [SerializeField] private float cameraZoomDuration = 0.28f;
    [SerializeField] private float resultDelay = 1.9f;

    [Header("Camera")]
    [SerializeField] private float zoomSizeMultiplier = 0.82f;
    [SerializeField] private float playerDeathShakeDuration = 0.16f;
    [SerializeField] private float playerDeathShakeMagnitude = 0.06f;
    [SerializeField] private float bossDeathShakeDuration = 0.42f;
    [SerializeField] private float bossDeathShakeMagnitude = 0.32f;

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.55f;
    [SerializeField] private float fadeAlpha = 0.58f;

    [Header("Death Animation")]
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private string[] deathStateNames = { "Death", "Die", "Dead" };

    [Header("Result Image")]
    [SerializeField] private string victoryImagePath = "Art/Resource/Cartoon/victory.png";
    [SerializeField] private string defeatImagePath = "Art/Resource/Cartoon/Defeat.png";
    [SerializeField] private Vector2 resultImageSize = new Vector2(800f, 492f);
    [SerializeField] private Vector2 resultImageAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 resultTextAnchoredPosition = new Vector2(0f, -120f);
    [SerializeField] private Vector2 resultTextSize = new Vector2(560f, 90f);
    [SerializeField] private float resultTextFontSize = 46f;
    [SerializeField] private Vector2 restartButtonAnchoredPosition = new Vector2(0f, -210f);
    [SerializeField] private Vector2 restartButtonSize = new Vector2(220f, 54f);

    private float battleStartTime;
    private bool hasResult;
    private bool messageRegistered;
    private Image fadeImage;
    private GameObject resultImageObject;
    private GameObject victoryResultObject;
    private GameObject defeatResultObject;
    private Image resultImage;
    private TextMeshProUGUI clearTimeText;
    private Camera mainCamera;
    private float originalCameraSize;
    private bool currentResultVictory;
    private Transform currentResultFocusTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForLoadedScene()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreateInCurrentScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateInCurrentScene();
    }

    private static void TryCreateInCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!sceneName.Contains("GoblinBoss"))
            return;

        if (instance != null)
            Destroy(instance.gameObject);

        GameObject managerObject = new GameObject("BossBattleResultManager");
        instance = managerObject.AddComponent<BossBattleResultManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        battleStartTime = Time.time;
        mainCamera = Camera.main;

        if (mainCamera != null)
            originalCameraSize = mainCamera.orthographicSize;

        CreateResultUI();
    }

    private void OnDisable()
    {
        if (messageRegistered && NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
        {
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(ResultMessageName);
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(RestartMessageName);
        }

        messageRegistered = false;
    }

    private void Update()
    {
        RegisterNetworkMessage();

        if (hasResult)
            return;

        if (IsNetworkActive() && !NetworkManager.Singleton.IsServer)
            return;

        if (TryGetResult(out bool isVictory, out Transform focusTarget))
            FinishBattle(isVictory, focusTarget);
    }

    private bool TryGetResult(out bool isVictory, out Transform focusTarget)
    {
        isVictory = false;
        focusTarget = null;

        GoblinHealth2D boss = FindFirstObjectByType<GoblinHealth2D>();
        if (boss != null && boss.IsDead)
        {
            isVictory = true;
            focusTarget = boss.transform;
            return true;
        }

        PlayerHealth2D[] players = FindObjectsByType<PlayerHealth2D>(FindObjectsSortMode.None);
        if (players.Length <= 0)
            return false;

        PlayerHealth2D deadPlayer = null;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && !players[i].IsDead)
                return false;

            if (players[i] != null && deadPlayer == null)
                deadPlayer = players[i];
        }

        isVictory = false;
        focusTarget = deadPlayer != null ? deadPlayer.transform : null;
        return true;
    }

    private void FinishBattle(bool isVictory, Transform focusTarget)
    {
        hasResult = true;
        float elapsedTime = Mathf.Max(0f, Time.time - battleStartTime);

        PlayDeathAnimations();
        StopBattleObjects();
        SendResultToClients(isVictory, elapsedTime);
        currentResultVictory = isVictory;
        currentResultFocusTarget = focusTarget;
        StartCoroutine(ResultRoutine(elapsedTime));
    }

    private void SendResultToClients(bool isVictory, float elapsedTime)
    {
        if (!IsNetworkActive() || !NetworkManager.Singleton.IsServer)
            return;

        using FastBufferWriter writer = new FastBufferWriter(sizeof(bool) + sizeof(float), Allocator.Temp);
        writer.WriteValueSafe(isVictory);
        writer.WriteValueSafe(elapsedTime);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(ResultMessageName, writer);
    }

    private void RegisterNetworkMessage()
    {
        if (messageRegistered || !IsNetworkActive() || NetworkManager.Singleton.CustomMessagingManager == null)
            return;

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(ResultMessageName, ReceiveResultMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(RestartMessageName, ReceiveRestartMessage);
        messageRegistered = true;
    }

    private void ReceiveResultMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (hasResult)
            return;

        reader.ReadValueSafe(out bool isVictory);
        reader.ReadValueSafe(out float elapsedTime);

        hasResult = true;
        PlayDeathAnimations();
        StopBattleObjects();
        currentResultVictory = isVictory;
        currentResultFocusTarget = FindResultFocusTarget(isVictory);
        StartCoroutine(ResultRoutine(elapsedTime));
    }

    private void ReceiveRestartMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsNetworkActive() || !NetworkManager.Singleton.IsServer)
            return;

        RestartSceneFromHost();
    }

    private IEnumerator ResultRoutine(float elapsedTime)
    {
        yield return StartCoroutine(ZoomAndHitStop());
        yield return new WaitForSecondsRealtime(resultDelay);
        yield return StartCoroutine(FadeIn());

        ShowResultUI(elapsedTime);
    }

    private IEnumerator ZoomAndHitStop()
    {
        float previousTimeScale = Time.timeScale;
        Time.timeScale = finishTimeScale;

        if (mainCamera == null)
        {
            yield return new WaitForSecondsRealtime(finishHitStopDuration);
            Time.timeScale = previousTimeScale;
            yield break;
        }

        CameraFollow2D follow = mainCamera.GetComponent<CameraFollow2D>();
        if (follow != null)
            follow.enabled = false;

        Vector3 startPosition = mainCamera.transform.position;
        Vector3 targetPosition = GetCameraFocusPosition(startPosition);
        float shakeDuration = currentResultVictory ? bossDeathShakeDuration : playerDeathShakeDuration;
        float shakeMagnitude = currentResultVictory ? bossDeathShakeMagnitude : playerDeathShakeMagnitude;
        float startSize = mainCamera.orthographicSize;
        float targetSize = originalCameraSize * zoomSizeMultiplier;
        float elapsed = 0f;

        while (elapsed < cameraZoomDuration)
        {
            float t = Mathf.Clamp01(elapsed / cameraZoomDuration);
            Vector3 shakeOffset = GetResultShakeOffset(elapsed, shakeDuration, shakeMagnitude);
            mainCamera.orthographicSize = Mathf.Lerp(startSize, targetSize, Smooth01(t));
            mainCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, Smooth01(t)) + shakeOffset;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        mainCamera.orthographicSize = targetSize;

        float holdElapsed = 0f;
        while (holdElapsed < finishHitStopDuration)
        {
            Vector3 shakeOffset = GetResultShakeOffset(cameraZoomDuration + holdElapsed, shakeDuration, shakeMagnitude);
            mainCamera.transform.position = targetPosition + shakeOffset;
            holdElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        mainCamera.transform.position = targetPosition;
        Time.timeScale = previousTimeScale;
    }

    private IEnumerator FadeIn()
    {
        if (fadeImage == null)
            yield break;

        float elapsed = 0f;
        Color startColor = fadeImage.color;
        Color endColor = startColor;
        endColor.a = fadeAlpha;

        while (elapsed < fadeDuration)
        {
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            fadeImage.color = Color.Lerp(startColor, endColor, Smooth01(t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        fadeImage.color = endColor;
    }

    private Transform FindResultFocusTarget(bool isVictory)
    {
        if (isVictory)
        {
            GoblinHealth2D boss = FindFirstObjectByType<GoblinHealth2D>();
            return boss != null ? boss.transform : null;
        }

        PlayerHealth2D[] players = FindObjectsByType<PlayerHealth2D>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsDead)
                return players[i].transform;
        }

        return null;
    }

    private Vector3 GetCameraFocusPosition(Vector3 fallbackPosition)
    {
        if (currentResultFocusTarget == null)
            return fallbackPosition;

        Vector3 targetPosition = currentResultFocusTarget.position;
        targetPosition.z = fallbackPosition.z;
        return targetPosition;
    }

    private Vector3 GetResultShakeOffset(float elapsed, float duration, float magnitude)
    {
        if (duration <= 0f || magnitude <= 0f || elapsed >= duration)
            return Vector3.zero;

        float strength = 1f - Mathf.Clamp01(elapsed / duration);
        return new Vector3(
            Random.Range(-1f, 1f) * magnitude * strength,
            Random.Range(-1f, 1f) * magnitude * strength,
            0f);
    }

    private void StopBattleObjects()
    {
        PlayerController2D[] playerControllers = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None);
        for (int i = 0; i < playerControllers.Length; i++)
            playerControllers[i].enabled = false;

        PlayerAttack2D[] archerAttacks = FindObjectsByType<PlayerAttack2D>(FindObjectsSortMode.None);
        for (int i = 0; i < archerAttacks.Length; i++)
            archerAttacks[i].enabled = false;

        WarriorAttack2D[] warriorAttacks = FindObjectsByType<WarriorAttack2D>(FindObjectsSortMode.None);
        for (int i = 0; i < warriorAttacks.Length; i++)
            warriorAttacks[i].enabled = false;

        PlayerHealth2D[] players = FindObjectsByType<PlayerHealth2D>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            Rigidbody2D rb = players[i].GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            Animator animator = players[i].GetComponent<Animator>();
            if (animator != null && !players[i].IsDead)
                animator.speed = 0f;
        }

        GoblinBossCombatController2D[] bossCombats = FindObjectsByType<GoblinBossCombatController2D>(FindObjectsSortMode.None);
        for (int i = 0; i < bossCombats.Length; i++)
            bossCombats[i].enabled = false;

        GoblinBossFallingAttack2D[] fallingAttacks = FindObjectsByType<GoblinBossFallingAttack2D>(FindObjectsSortMode.None);
        for (int i = 0; i < fallingAttacks.Length; i++)
            fallingAttacks[i].enabled = false;

        GoblinBossIceWaveAttack2D[] iceAttacks = FindObjectsByType<GoblinBossIceWaveAttack2D>(FindObjectsSortMode.None);
        for (int i = 0; i < iceAttacks.Length; i++)
            iceAttacks[i].enabled = false;
    }

    private void PlayDeathAnimations()
    {
        if (string.IsNullOrEmpty(deathTriggerName))
            return;

        // [Codex Death] Boss death is already played once by GoblinHealth2D.CoDie().
        // Replaying it here makes the boss fall twice on victory.
        PlayerHealth2D[] players = FindObjectsByType<PlayerHealth2D>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsDead)
                PlayDeathAnimation(players[i].gameObject);
        }
    }

    private void PlayDeathAnimation(GameObject target)
    {
        if (target == null)
            return;

        ApplyHeroEditorDeadExpression(target);
        PlayDeathTrigger(target.GetComponentInChildren<Animator>(true));
    }

    private void ApplyHeroEditorDeadExpression(GameObject target)
    {
        MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

        TrySetStringMember(behaviour, "Expression", "Dead");
        TrySetStringMember(behaviour, "Emotion", "Dead");
        TrySetStringMember(behaviour, "Action", "Death");
        TrySetStringMember(behaviour, "SoloState", "Death");
        TrySetStringMember(behaviour, "State", "Death");
        TrySetEnumMember(behaviour, "Action", "Death");
        TrySetEnumMember(behaviour, "SoloState", "Death");
        TrySetEnumMember(behaviour, "State", "Death");
        TryInvokeStringMethod(behaviour, "SetExpression", "Dead");
        TryInvokeStringMethod(behaviour, "SetEmotion", "Dead");
        TryInvokeStringMethod(behaviour, "SetExpressionByName", "Dead");
        TryInvokeStringMethod(behaviour, "SetAction", "Death");
        TryInvokeStringMethod(behaviour, "SetSoloState", "Death");
        TryInvokeStringMethod(behaviour, "SetState", "Death");
        TryInvokeStringMethod(behaviour, "SetActionByName", "Death");
        TryInvokeEnumMethod(behaviour, "SetAction", "Death");
        TryInvokeEnumMethod(behaviour, "SetSoloState", "Death");
        TryInvokeEnumMethod(behaviour, "SetState", "Death");
        }
    }

    private void PlayDeathTrigger(Animator animator)
    {
        if (animator == null)
            return;

        animator.speed = 1f;

        // [Codex Boss Result] Death playback is centralized so clips can be swapped later.
        if (HasTrigger(animator, deathTriggerName))
            animator.SetTrigger(deathTriggerName);

        for (int i = 0; i < deathStateNames.Length; i++)
        {
            if (string.IsNullOrEmpty(deathStateNames[i]))
                continue;

            if (CrossFadeStateIfExists(animator, deathStateNames[i]))
                return;
        }
    }
    private void ApplyDefeatUILayoutOffset()
    {
        if (defeatResultObject == null)
            return;

        // �ڽ� ��ü���� ClearTimeText ã��
        TextMeshProUGUI[] texts =
            defeatResultObject.GetComponentsInChildren<TextMeshProUGUI>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            if (texts[i].name.Contains("ClearTime"))
            {
                RectTransform rect =
                    texts[i].GetComponent<RectTransform>();

                if (rect != null)
                {
                    Vector2 pos =
                        rect.anchoredPosition;

                    pos.y += 25f;

                    rect.anchoredPosition =
                        pos;

                    Debug.Log(
                        $"[BossResult] Defeat ClearTime �̵� �Ϸ� / Y = {rect.anchoredPosition.y}"
                    );
                }
            }
        }

        // �ڽ� ��ü���� RestartButton ã��
        Button[] buttons =
            defeatResultObject.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            if (buttons[i].name.Contains("Restart"))
            {
                RectTransform rect =
                    buttons[i].GetComponent<RectTransform>();

                if (rect != null)
                {
                    Vector2 pos =
                        rect.anchoredPosition;

                    pos.y += 35f;

                    rect.anchoredPosition =
                        pos;

                    Debug.Log(
                        $"[BossResult] Defeat RestartButton �̵� �Ϸ� / Y = {rect.anchoredPosition.y}"
                    );
                }
            }
        }
    }
    private bool CrossFadeStateIfExists(Animator animator, string stateName)
    {
        int stateHash = Animator.StringToHash(stateName);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            if (animator.HasState(layer, stateHash))
            {
                animator.CrossFade(stateHash, 0.05f, layer);
                return true;
            }
        }

        return false;
    }

    private bool HasTrigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                return true;
        }

        return false;
    }

    private void CreateResultUI()
    {
        if (TryBindSceneResultUI())
            return;

        GameObject canvasObject = new GameObject("BossBattle_ResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject fadeObject = new GameObject("Result_Fade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        StretchToParent(fadeObject.GetComponent<RectTransform>());

        resultImageObject = new GameObject("Result_ImageRoot", typeof(RectTransform), typeof(Image));
        resultImageObject.transform.SetParent(canvasObject.transform, false);
        resultImage = resultImageObject.GetComponent<Image>();
        resultImage.color = Color.white;
        resultImage.preserveAspect = true;

        RectTransform imageRect = resultImageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.5f);
        imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = resultImageSize;
        imageRect.anchoredPosition = resultImageAnchoredPosition;

        clearTimeText = CreateText("ClearTimeText", resultImageObject.transform, resultTextFontSize);
        RectTransform timeRect = clearTimeText.rectTransform;
        timeRect.anchorMin = new Vector2(0.5f, 0.5f);
        timeRect.anchorMax = new Vector2(0.5f, 0.5f);
        timeRect.sizeDelta = resultTextSize;
        timeRect.anchoredPosition = resultTextAnchoredPosition;

        GameObject buttonObject = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(resultImageObject.transform, false);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        Button restartButton = buttonObject.GetComponent<Button>();
        restartButton.onClick.AddListener(RestartScene);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = restartButtonSize;
        buttonRect.anchoredPosition = restartButtonAnchoredPosition;

        TextMeshProUGUI buttonText = CreateText("RestartText", buttonObject.transform, 24f);
        buttonText.text = "Restart";
        StretchToParent(buttonText.rectTransform);

        resultImageObject.SetActive(false);
    }

    private void TrySetStringMember(object target, string memberName, string value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null && field.FieldType == typeof(string))
            TrySetValue(() => field.SetValue(target, value));

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite && property.PropertyType == typeof(string))
            TrySetValue(() => property.SetValue(target, value));
    }

    private void TryInvokeStringMethod(object target, string methodName, string value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo method = target.GetType().GetMethod(methodName, flags, null, new[] { typeof(string) }, null);
        if (method != null)
            TrySetValue(() => method.Invoke(target, new object[] { value }));
    }

    private void TrySetEnumMember(object target, string memberName, string enumName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null && field.FieldType.IsEnum && System.Enum.IsDefined(field.FieldType, enumName))
            TrySetValue(() => field.SetValue(target, System.Enum.Parse(field.FieldType, enumName)));

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum && System.Enum.IsDefined(property.PropertyType, enumName))
            TrySetValue(() => property.SetValue(target, System.Enum.Parse(property.PropertyType, enumName)));
    }

    private void TryInvokeEnumMethod(object target, string methodName, string enumName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        MethodInfo[] methods = target.GetType().GetMethods(flags);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name != methodName)
                continue;

            ParameterInfo[] parameters = methods[i].GetParameters();
            if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum)
                continue;

            System.Type enumType = parameters[0].ParameterType;
            if (!System.Enum.IsDefined(enumType, enumName))
                continue;

            object enumValue = System.Enum.Parse(enumType, enumName);
            TrySetValue(() => methods[i].Invoke(target, new[] { enumValue }));
        }
    }

    private void TrySetValue(System.Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch
        {
        }
    }

    private bool TryBindSceneResultUI()
    {
        GameObject canvasObject = GameObject.Find("BossBattle_ResultCanvas");
        if (canvasObject == null)
            return false;

        Transform fadeTransform = canvasObject.transform.Find("Result_Fade");
        if (fadeTransform != null)
            fadeImage = fadeTransform.GetComponent<Image>();

        Transform victoryTransform = canvasObject.transform.Find("Victory_Result_ImageRoot");
        Transform defeatTransform = canvasObject.transform.Find("Defeat_Result_ImageRoot");
        if (victoryTransform != null || defeatTransform != null)
        {
            victoryResultObject = victoryTransform != null ? victoryTransform.gameObject : null;
            defeatResultObject = defeatTransform != null ? defeatTransform.gameObject : null;

            BindRestartButtons(victoryResultObject);
            BindRestartButtons(defeatResultObject);

            if (Application.isPlaying)
            {
                if (fadeImage != null)
                    fadeImage.color = new Color(0f, 0f, 0f, 0f);

                if (victoryResultObject != null)
                    victoryResultObject.SetActive(false);
                if (defeatResultObject != null)
                    defeatResultObject.SetActive(false);
            }

            return true;
        }

        Transform imageTransform = canvasObject.transform.Find("Result_ImageRoot");
        if (imageTransform == null)
            return false;

        resultImageObject = imageTransform.gameObject;
        resultImage = resultImageObject.GetComponent<Image>();

        Transform textTransform = imageTransform.Find("ClearTimeText");
        if (textTransform != null)
            clearTimeText = textTransform.GetComponent<TextMeshProUGUI>();

        BindRestartButtons(resultImageObject);

        if (Application.isPlaying)
        {
            if (fadeImage != null)
                fadeImage.color = new Color(0f, 0f, 0f, 0f);

            resultImageObject.SetActive(false);
        }

        return true;
    }

    private void BindRestartButtons(GameObject root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            buttons[i].onClick.RemoveListener(RestartScene);
            buttons[i].onClick.AddListener(RestartScene);
        }
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = fontSize;
        return text;
    }

    private void ShowResultUI(float elapsedTime)
    {
        string timeText = $"Clear Time {FormatTime(elapsedTime)}";
        ApplyClearTimeText(timeText);

        if (victoryResultObject != null || defeatResultObject != null)
        {
            if (!currentResultVictory)
            {
                ApplyDefeatUILayoutOffset();
            }

            if (victoryResultObject != null)
                victoryResultObject.SetActive(currentResultVictory);

            if (defeatResultObject != null)
                defeatResultObject.SetActive(!currentResultVictory);

            return;
        }

        if (resultImage != null)
            resultImage.sprite = LoadResultSprite(currentResultVictory);

        if (resultImageObject != null)
            resultImageObject.SetActive(true);
    }

    private void ApplyClearTimeText(string timeText)
    {
        if (clearTimeText != null)
            clearTimeText.text = timeText;

        if (victoryResultObject != null)
            SetTextInChildren(victoryResultObject, timeText);
        if (defeatResultObject != null)
            SetTextInChildren(defeatResultObject, timeText);
    }

    private void SetTextInChildren(GameObject root, string timeText)
    {
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name.Contains("ClearTime"))
                texts[i].text = timeText;
        }
    }

    private Sprite LoadResultSprite(bool isVictory)
    {
        string relativePath = isVictory ? victoryImagePath : defeatImagePath;
        string fullPath = Path.Combine(Application.dataPath, relativePath);

        if (!File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
            return null;

        texture.name = isVictory ? "BossBattle_VictoryResult" : "BossBattle_DefeatResult";
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void RestartScene()
    {
        Time.timeScale = 1f;

        if (!IsNetworkActive())
        {
            // [Codex Result Restart] 결과창 Restart는 보스 재도전이 아니라 처음 Play 진입 화면으로 돌아갑니다.
            SceneManager.LoadScene(RestartTargetSceneName);
            return;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            RestartSceneFromHost();
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(sizeof(byte), Allocator.Temp);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(RestartMessageName, NetworkManager.ServerClientId, writer);
    }

    private void RestartSceneFromHost()
    {
        Time.timeScale = 1f;
        // [Codex Result Restart] 네트워크 플레이도 호스트가 GameEntry로 씬 전환을 동기화합니다.
        NetworkManager.Singleton.SceneManager.LoadScene(RestartTargetSceneName, LoadSceneMode.Single);
    }

    private bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainSeconds:00}";
    }

    private static float Smooth01(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
