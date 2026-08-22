using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GameEntryNetworkConnectionController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private GameObject connectionPanel;

    [Header("Loading Overlay")]
    [SerializeField] private Sprite loadingSwirlSprite;
    [SerializeField] private TMP_FontAsset statusFont;

    [Header("Ready Panel")]
    [SerializeField] private GameObject readyPanel;
    [SerializeField] private Sprite readyPanelFrameSprite;
    [SerializeField] private Sprite skillKeySetupTitleSprite;
    [SerializeField] private Sprite readyCompleteButtonSprite;
    [SerializeField] private GameObject goblinBossKeySettingPrefab;

    [Header("Character Select Panel")]
    [SerializeField] private GameObject characterSelectPanel;
    [SerializeField] private Sprite characterSelectBackgroundSprite;
    [SerializeField] private Sprite characterSelectTitleSprite;
    [SerializeField] private Sprite archerCardSprite;
    [SerializeField] private Sprite warriorCardSprite;
    [SerializeField] private Sprite confirmButtonSprite;

    [Header("Ready Panel Skill Icon Overrides")]
    [SerializeField] private Sprite moveSpeedSkillIcon;
    [SerializeField] private Sprite attackSpeedSkillIcon;
    [SerializeField] private Sprite quickStepPassiveSkillIcon;
    [SerializeField] private Sprite powerShotSkillIcon;
    [SerializeField] private Sprite rapidVolleySkillIcon;
    [SerializeField] private Sprite warriorDownStrikeSkillIcon;
    [SerializeField] private Sprite warriorShieldBlockSkillIcon;

    private GameEntryLoadingOverlay loadingOverlay;
    private NetworkManager networkManager;
    private bool callbacksRegistered;
    private bool waitingAsHost;
    private bool waitingAsClient;
    private bool readyPanelSequenceStarted;

    private const string NetworkManagerResourcePath = "NetworkManager";
    private const string LoadingSwirlResourcePath = "UI/GameEntry/GameEntry_LoadingSwirl_Cartoon";
    private const string ReadyPanelFramePath = "Assets/Art/UI/ReadyPanel_Frame_Transparent.png";
    private const string SkillKeySetupTitlePath = "Assets/Art/UI/Skill_Key_Setup_Title.png";
    private const string ReadyCompleteButtonPath = "Assets/Art/UI/Ready_Complete_Button.png";
    private const string GoblinBossKeySettingPrefabPath = "Assets/Resources/UI/GoblinBoss_KeySettingUI.prefab";
    private const string GoblinBossKeySettingResourcePath = "UI/GoblinBoss_KeySettingUI";
    private const string WarriorDownStrikeIconPath = "Assets/Art/UI/KeySetting/Downstrike.png";
    private const string WarriorShieldBlockIconPath = "Assets/Art/UI/KeySetting/Shiled.png";
    private const string NotoSansKRSemiBoldSdfPath = "Assets/Fonts/static/NotoSansKR-SemiBold SDF.asset";
    private const string CharacterSelectBackgroundPath = "Assets/Art/UI/CharacterSelect/CharacterSelect_Background.png";
    private const string CharacterSelectTitlePath = "Assets/Art/UI/CharacterSelect/CharacterSelect_Title.png";
    private const string ArcherCardPath = "Assets/Art/UI/CharacterSelect/ArcherCard.png";
    private const string WarriorCardPath = "Assets/Art/UI/CharacterSelect/WarriorCard.png";
    private const string ConfirmButtonPath = "Assets/Art/UI/CharacterSelect/ConfirmButton.png";

    private void Awake()
    {
        if (createRoomButton == null)
            createRoomButton = GameObject.Find("CreateRoomButton")?.GetComponent<Button>();

        if (joinButton == null)
            joinButton = GameObject.Find("JoinButton")?.GetComponent<Button>();

        if (connectionPanel == null)
            connectionPanel = GameObject.Find("ConnectionPanel");

        loadingOverlay = BuildLoadingOverlay();
        readyPanel = BuildReadyPanel();
        characterSelectPanel = BuildCharacterSelectPanel();
    }

    private void OnValidate()
    {
        if (Application.isPlaying || !isActiveAndEnabled)
            return;

        loadingOverlay = BuildLoadingOverlay();
        ShowOverlayInEditMode();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(StartHost);

        if (joinButton != null)
            joinButton.onClick.AddListener(StartClient);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(StartHost);

        if (joinButton != null)
            joinButton.onClick.RemoveListener(StartClient);

        UnregisterCallbacks();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (loadingOverlay == null)
            loadingOverlay = BuildLoadingOverlay();

        if (loadingOverlay != null)
            loadingOverlay.HideImmediate();

        if (readyPanel != null)
            readyPanel.SetActive(false);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        ResetGameEntrySessionState();
        ResetNetworkSessionForFreshEntry();
    }

    private void ResetGameEntrySessionState()
    {
        // [Codex GameEntry Fresh Start] 결과창 Restart 후 캐릭터/키 설정 세션 데이터를 첫 Play 진입처럼 비웁니다.
        GameEntryCharacterSelectionStore.ResetSessionState();
        KeyBindingManager.ClearAllSessionBindings();
    }

    private void ResetNetworkSessionForFreshEntry()
    {
        NetworkManager existingManager = NetworkManager.Singleton;
        if (existingManager == null || (!existingManager.IsListening && !existingManager.ShutdownInProgress))
            return;

        // [Codex GameEntry Restart] 결과창에서 돌아온 뒤 남아 있는 Netcode 세션을 정리해야 다시 방 만들기가 가능합니다.
        existingManager.Shutdown();
        networkManager = existingManager;
        waitingAsHost = false;
        waitingAsClient = false;
        readyPanelSequenceStarted = false;
        GameEntryCharacterSelectionStore.SetLocalSelectedCharacter(PlayerCharacterType.None);
        SetButtonsInteractable(false);
        StartCoroutine(EnableButtonsAfterNetworkShutdown(existingManager));
    }

    private IEnumerator EnableButtonsAfterNetworkShutdown(NetworkManager shuttingDownManager)
    {
        while (shuttingDownManager != null && (shuttingDownManager.IsListening || shuttingDownManager.ShutdownInProgress))
            yield return null;

        SetButtonsInteractable(true);
    }

    private void StartHost()
    {
        if (!PrepareNetworkManager())
        {
            FailConnection("NetworkManager를 찾을 수 없습니다.");
            return;
        }

        waitingAsHost = true;
        waitingAsClient = false;
        readyPanelSequenceStarted = false;
        GameEntryCharacterSelectionStore.SetLocalSelectedCharacter(PlayerCharacterType.None);
        SetButtonsInteractable(false);
        loadingOverlay.Show("방을 만드는 중...");

        bool started = networkManager.StartHost();
        Debug.Log($"[GameEntryNetwork] StartHost returned {started}.");

        if (!started)
        {
            FailConnection("접속 실패");
            return;
        }

        loadingOverlay.SetStatus("방이 생성되었습니다. 상대를 기다리는 중...");
        CheckHostClientCount();
    }

    private void StartClient()
    {
        if (!PrepareNetworkManager())
        {
            FailConnection("NetworkManager를 찾을 수 없습니다.");
            return;
        }

        waitingAsHost = false;
        waitingAsClient = true;
        readyPanelSequenceStarted = false;
        GameEntryCharacterSelectionStore.SetLocalSelectedCharacter(PlayerCharacterType.None);
        SetButtonsInteractable(false);
        loadingOverlay.Show("접속하는 중...");

        bool started = networkManager.StartClient();
        Debug.Log($"[GameEntryNetwork] StartClient returned {started}.");

        if (!started)
            FailConnection("접속 실패");
    }

    private bool PrepareNetworkManager()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            GameObject prefab = Resources.Load<GameObject>(NetworkManagerResourcePath);

            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.name = prefab.name;
                DontDestroyOnLoad(instance);
                networkManager = instance.GetComponent<NetworkManager>();
                Debug.Log("[GameEntryNetwork] Loaded existing Resources/NetworkManager prefab for GameEntry.");
            }
        }

        if (networkManager == null)
            return false;

        RegisterCallbacks();
        return true;
    }

    private void RegisterCallbacks()
    {
        if (callbacksRegistered || networkManager == null)
            return;

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        callbacksRegistered = true;
    }

    private void UnregisterCallbacks()
    {
        if (!callbacksRegistered || networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        callbacksRegistered = false;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[GameEntryNetwork] OnClientConnected clientId={clientId}, local={networkManager.LocalClientId}.");

        if (waitingAsHost)
            CheckHostClientCount();

        if (waitingAsClient && clientId == networkManager.LocalClientId)
            CompleteConnection();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[GameEntryNetwork] OnClientDisconnected clientId={clientId}, local={networkManager.LocalClientId}.");

        if (waitingAsClient && clientId == networkManager.LocalClientId)
            FailConnection("접속 실패");
    }

    private void CheckHostClientCount()
    {
        if (networkManager == null || !networkManager.IsServer)
            return;

        int connectedCount = networkManager.ConnectedClientsIds.Count;
        Debug.Log($"[GameEntryNetwork] Host connected client count={connectedCount}.");

        if (connectedCount >= 2)
            CompleteConnection();
    }

    private void CompleteConnection()
    {
        if (readyPanelSequenceStarted)
            return;

        readyPanelSequenceStarted = true;
        waitingAsHost = false;
        waitingAsClient = false;
        loadingOverlay.SetStatus("접속 완료");
        loadingOverlay.PlaySuccessPulse();
        Debug.Log("[GameEntryNetwork] Connection complete.");

        if (Application.isPlaying)
            StartCoroutine(ShowReadyPanelAfterConnection());
    }

    private IEnumerator ShowReadyPanelAfterConnection()
    {
        yield return new WaitForSecondsRealtime(0.8f);

        if (loadingOverlay != null)
            loadingOverlay.HideSmooth();

        if (connectionPanel != null)
            connectionPanel.SetActive(false);

        if (characterSelectPanel == null)
            characterSelectPanel = BuildCharacterSelectPanel();

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(true);

        Debug.Log("[GameEntryNetwork] CharacterSelectPanel shown after both players connected.");
    }

    private void ShowReadyPanelAfterCharacterConfirmed()
    {
        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(false);

        if (readyPanel == null)
            readyPanel = BuildReadyPanel();

        if (readyPanel != null)
        {
            ApplyReadyPanelLocalPlayerSkillSet();
            readyPanel.SetActive(true);
        }

        Debug.Log("[GameEntryNetwork] ReadyPanel shown after character selection.");
    }

    private void FailConnection(string message)
    {
        waitingAsHost = false;
        waitingAsClient = false;
        readyPanelSequenceStarted = false;

        if (loadingOverlay != null)
        {
            loadingOverlay.SetStatus(message);
            loadingOverlay.HideImmediate();
        }

        SetButtonsInteractable(true);
        Debug.LogWarning($"[GameEntryNetwork] {message}");
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (createRoomButton != null)
            createRoomButton.interactable = interactable;

        if (joinButton != null)
            joinButton.interactable = interactable;
    }

    private GameEntryLoadingOverlay BuildLoadingOverlay()
    {
        Transform existing = transform.Find("LoadingOverlay");
        if (existing != null)
        {
            GameEntryLoadingOverlay existingOverlay = existing.GetComponent<GameEntryLoadingOverlay>();

            if (Application.isPlaying && existingOverlay != null)
                existingOverlay.HideImmediate();

            return existingOverlay;
        }

        if (loadingSwirlSprite == null)
            loadingSwirlSprite = Resources.Load<Sprite>(LoadingSwirlResourcePath);

        GameObject overlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(GameEntryLoadingOverlay));
        overlay.layer = gameObject.layer;
        overlay.transform.SetParent(transform, false);

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        StretchToParent(overlayRect);

        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        GameObject dim = CreateImage("DimBackground", overlay.transform, null, new Color(0.02f, 0.05f, 0.14f, 0.82f));
        StretchToParent(dim.GetComponent<RectTransform>());

        GameObject swirlRoot = new GameObject("LoadingSwirlRoot", typeof(RectTransform));
        swirlRoot.layer = gameObject.layer;
        swirlRoot.transform.SetParent(overlay.transform, false);
        RectTransform swirlRootRect = swirlRoot.GetComponent<RectTransform>();
        Vector2 swirlNativeSize = GetSpriteNativeSize(loadingSwirlSprite) * 2f;
        SetCentered(swirlRootRect, swirlNativeSize, Vector2.zero);

        GameObject swirl = CreateImage("SwirlImage", swirlRoot.transform, loadingSwirlSprite, Color.white);
        RectTransform swirlRect = swirl.GetComponent<RectTransform>();
        SetCentered(swirlRect, swirlNativeSize, Vector2.zero);
        swirl.GetComponent<Image>().SetNativeSize();

        GameObject status = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        status.layer = gameObject.layer;
        status.transform.SetParent(overlay.transform, false);
        RectTransform statusRect = status.GetComponent<RectTransform>();
        SetCentered(statusRect, new Vector2(900f, 80f), new Vector2(0f, -285f));

        TextMeshProUGUI statusText = status.GetComponent<TextMeshProUGUI>();
        statusText.text = "";
        statusText.font = statusFont;
        statusText.fontSize = 38f;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = new Color(1f, 0.92f, 0.62f, 1f);
        statusText.raycastTarget = false;

        GameEntryLoadingOverlay component = overlay.GetComponent<GameEntryLoadingOverlay>();
        component.Initialize(group, swirlRootRect, swirlRect, statusText);
        return component;
    }

    private GameObject BuildReadyPanel()
    {
        LoadReadySpritesInEditor();

        Transform existing = transform.Find("ReadyPanel");
        if (existing != null)
        {
            EnsureReadyPanelContents(existing.gameObject, false);
            return existing.gameObject;
        }

        GameObject panel = new GameObject("ReadyPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetCentered(panelRect, new Vector2(1480f, 805f), new Vector2(0f, -55f));

        EnsureReadyPanelContents(panel, true);
        panel.SetActive(false);
        return panel;
    }

    private GameObject BuildCharacterSelectPanel()
    {
        LoadCharacterSelectSpritesInEditor();

        Transform existing = transform.Find("CharacterSelectPanel");
        if (existing != null)
        {
            EnsureCharacterSelectPanelContents(existing.gameObject);
            existing.gameObject.SetActive(false);
            return existing.gameObject;
        }

        GameObject panel = new GameObject("CharacterSelectPanel", typeof(RectTransform), typeof(CanvasGroup));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(transform, false);
        StretchToParent(panel.GetComponent<RectTransform>());
        EnsureCharacterSelectPanelContents(panel);
        panel.SetActive(false);
        return panel;
    }

    private void EnsureCharacterSelectPanelContents(GameObject panel)
    {
        Image background = panel.transform.Find("Background")?.GetComponent<Image>();
        if (background == null)
        {
            GameObject backgroundObject = CreateImage("Background", panel.transform, characterSelectBackgroundSprite, Color.white);
            StretchToParent(backgroundObject.GetComponent<RectTransform>());
            background = backgroundObject.GetComponent<Image>();
        }
        background.sprite = characterSelectBackgroundSprite;
        background.color = Color.white;
        background.raycastTarget = true;

        if (panel.transform.Find("TitleImage") == null)
        {
            GameObject title = CreateImage("TitleImage", panel.transform, characterSelectTitleSprite, Color.white);
            SetCentered(title.GetComponent<RectTransform>(), new Vector2(720f, 150f), new Vector2(0f, 325f));
        }

        Button archerButton = EnsureCharacterCardButton(panel.transform, "ArcherButton", "ArcherCardImage", archerCardSprite, new Vector2(-360f, -20f));
        Button warriorButton = EnsureCharacterCardButton(panel.transform, "WarriorButton", "WarriorCardImage", warriorCardSprite, new Vector2(360f, -20f));

        Button confirmButton = panel.transform.Find("ConfirmButton")?.GetComponent<Button>();
        Image confirmImage = panel.transform.Find("ConfirmButton/ConfirmImage")?.GetComponent<Image>();
        if (confirmButton == null)
        {
            GameObject buttonObject = new GameObject("ConfirmButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = gameObject.layer;
            buttonObject.transform.SetParent(panel.transform, false);
            SetCentered(buttonObject.GetComponent<RectTransform>(), new Vector2(310f, 120f), new Vector2(0f, -390f));
            Image targetImage = buttonObject.GetComponent<Image>();
            targetImage.color = Color.clear;
            targetImage.raycastTarget = true;
            confirmButton = buttonObject.GetComponent<Button>();

            GameObject imageObject = CreateImage("ConfirmImage", buttonObject.transform, confirmButtonSprite, Color.white);
            StretchToParent(imageObject.GetComponent<RectTransform>());
            confirmImage = imageObject.GetComponent<Image>();
        }
        confirmImage.sprite = confirmButtonSprite;
        confirmImage.raycastTarget = false;
        confirmButton.targetGraphic = confirmImage;

        TextMeshProUGUI statusText = panel.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
        if (statusText == null)
        {
            statusText = CreateReadyText("StatusText", panel.transform, "캐릭터를 선택하세요.", 28f, new Vector2(0f, -305f));
            statusText.color = new Color(1f, 0.92f, 0.62f, 1f);
        }

        GameEntryCharacterSelectPanelController controller = panel.GetComponent<GameEntryCharacterSelectPanelController>();
        if (controller == null)
            controller = panel.AddComponent<GameEntryCharacterSelectPanelController>();

        controller.Confirmed -= ShowReadyPanelAfterCharacterConfirmed;
        controller.Confirmed += ShowReadyPanelAfterCharacterConfirmed;
        controller.Initialize(
            archerButton,
            archerButton.transform.Find("ArcherCardImage")?.GetComponent<Image>(),
            warriorButton,
            warriorButton.transform.Find("WarriorCardImage")?.GetComponent<Image>(),
            confirmButton,
            statusText);
    }

    private Button EnsureCharacterCardButton(Transform parent, string buttonName, string imageName, Sprite sprite, Vector2 position)
    {
        Button button = parent.Find(buttonName)?.GetComponent<Button>();
        Image cardImage = parent.Find($"{buttonName}/{imageName}")?.GetComponent<Image>();

        if (button == null)
        {
            GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = gameObject.layer;
            buttonObject.transform.SetParent(parent, false);
            SetCentered(buttonObject.GetComponent<RectTransform>(), new Vector2(455f, 590f), position);
            Image targetImage = buttonObject.GetComponent<Image>();
            targetImage.color = Color.clear;
            targetImage.raycastTarget = true;
            button = buttonObject.GetComponent<Button>();

            GameObject cardObject = CreateImage(imageName, buttonObject.transform, sprite, Color.white);
            StretchToParent(cardObject.GetComponent<RectTransform>());
            cardImage = cardObject.GetComponent<Image>();
        }

        if (cardImage != null)
        {
            cardImage.sprite = sprite;
            cardImage.raycastTarget = true;
            button.targetGraphic = cardImage;
        }

        return button;
    }

    private void EnsureReadyPanelContents(GameObject panel, bool createdByBuilder)
    {
        if (createdByBuilder)
        {
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            SetCentered(panelRect, new Vector2(1480f, 805f), new Vector2(0f, -55f));
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.sprite = readyPanelFrameSprite;
            panelImage.color = Color.white;
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = true;
        }

        if (panel.transform.Find("ReadyTitleImage") == null)
        {
            GameObject title = CreateImage("ReadyTitleImage", panel.transform, skillKeySetupTitleSprite, Color.white);
            SetCentered(title.GetComponent<RectTransform>(), new Vector2(680f, 135f), new Vector2(0f, 282f));
        }

        GameObject keySettingRoot = null;
        Transform existingPreview = panel.transform.Find("KeySettingPreview");
        if (existingPreview != null)
        {
            keySettingRoot = existingPreview.gameObject;
        }
        else
        {
            keySettingRoot = CreateKeySettingPreview(panel.transform);
            SetCentered(keySettingRoot.GetComponent<RectTransform>(), new Vector2(1220f, 500f), new Vector2(0f, 8f));
        }

        ApplyReadySkillIconOverrides(keySettingRoot);

        TextMeshProUGUI readyStatus = panel.transform.Find("ReadyStatus")?.GetComponent<TextMeshProUGUI>();
        if (readyStatus == null)
        {
            readyStatus = CreateReadyText("ReadyStatus", panel.transform, "", 25f, new Vector2(0f, -315f));
            readyStatus.color = new Color(1f, 0.92f, 0.62f, 1f);
        }

        TextMeshProUGUI skillSelectionStatus =
            EnsureSkillSelectionStatusText(panel.transform, keySettingRoot.transform);

        Button readyButton = panel.transform.Find("ReadyCompleteButton")?.GetComponent<Button>();
        if (readyButton == null)
            readyButton = CreateReadyButton(panel.transform);

        GameEntryReadyNetworkController readyController = panel.GetComponent<GameEntryReadyNetworkController>();
        if (readyController == null)
            readyController = panel.AddComponent<GameEntryReadyNetworkController>();
        readyController.Initialize(readyButton, readyStatus, skillSelectionStatus);
    }

    private TextMeshProUGUI EnsureSkillSelectionStatusText(
        Transform panelRoot,
        Transform keySettingRoot)
    {
        Transform keyboardPanel =
            FindChildRecursive(keySettingRoot, "KeyboardPanel");

        Transform parent =
            keyboardPanel != null ? keyboardPanel : keySettingRoot;

        TextMeshProUGUI skillSelectionStatus =
            FindChildRecursive(parent, "SkillSelectionStatus")?.GetComponent<TextMeshProUGUI>();

        bool createdStatusText = false;

        if (skillSelectionStatus == null)
        {
            skillSelectionStatus =
                panelRoot.Find("SkillSelectionStatus")?.GetComponent<TextMeshProUGUI>();
        }

        if (skillSelectionStatus == null)
        {
            // [Codex Skill Select Required Count] 키보드 아래에서 실제 선택 완료 수를 보여줍니다.
            GameObject textObject = new GameObject(
                "SkillSelectionStatus",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            textObject.layer = gameObject.layer;
            textObject.transform.SetParent(parent, false);

            skillSelectionStatus =
                textObject.GetComponent<TextMeshProUGUI>();

            createdStatusText =
                true;
        }
        else if (skillSelectionStatus.transform.parent != parent)
        {
            skillSelectionStatus.transform.SetParent(parent, false);
            createdStatusText =
                true;
        }

        if (createdStatusText)
        {
            // [Codex Scene Editable UI] 새로 만든 경우에만 기본 배치를 잡고, 이후 씬 수정값은 덮어쓰지 않습니다.
            RectTransform textRect =
                skillSelectionStatus.GetComponent<RectTransform>();

            SetCentered(
                textRect,
                new Vector2(520f, 42f),
                new Vector2(0f, -275f));

            skillSelectionStatus.text =
                "버프 스킬 0/1   공격스킬 0/1";
            skillSelectionStatus.font =
                statusFont;
            skillSelectionStatus.fontSize =
                21f;
            skillSelectionStatus.alignment =
                TextAlignmentOptions.Center;
            skillSelectionStatus.raycastTarget =
                false;
            skillSelectionStatus.color =
                new Color(1f, 0.92f, 0.62f, 1f);
        }

        return skillSelectionStatus;
    }

    private TextMeshProUGUI CreateReadyText(string objectName, Transform parent, string text, float fontSize, Vector2 position)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        SetCentered(textRect, new Vector2(760f, 70f), position);

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = statusFont;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private GameObject CreateKeySettingPreview(Transform parent)
    {
        // [Codex GoblinBoss KeySetting Reuse] 새로 배치하지 않고 GoblinBoss 씬에서 맞춰둔 KeySettingUI 프리팹을 그대로 가져옵니다.
        GameObject prefab = goblinBossKeySettingPrefab != null
            ? goblinBossKeySettingPrefab
            : Resources.Load<GameObject>(GoblinBossKeySettingResourcePath);

#if UNITY_EDITOR
        if (prefab == null)
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinBossKeySettingPrefabPath);
#endif

        if (prefab != null)
        {
            GameObject keySetting = Instantiate(prefab, parent, false);
            keySetting.name = "KeySettingPreview";
            keySetting.SetActive(true);
            ConfigureImportedKeySettingPreviewOnce(keySetting);
            return keySetting;
        }

        TextMeshProUGUI missing = CreateReadyText("KeySettingPreview", parent, "GoblinBoss 키 설정 프리팹을 생성해주세요", 24f, Vector2.zero);
        missing.color = new Color(1f, 0.72f, 0.52f, 1f);
        return missing.gameObject;
    }

    private void ConfigureImportedKeySettingPreviewOnce(GameObject keySetting)
    {
        // [Codex ReadyPanel 최초 배치] 씬에서 수정한 KeySettingPreview 위치/스케일은 이후 자동으로 덮어쓰지 않습니다.
        RectTransform rootRect = keySetting.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        Transform dimmedBackground = keySetting.transform.Find("DimmedBackground");
        if (dimmedBackground != null)
            dimmedBackground.gameObject.SetActive(false);

        Transform closeBottomButton = keySetting.transform.Find("CloseBottomButton");
        if (closeBottomButton != null)
            closeBottomButton.gameObject.SetActive(false);

        Transform guideText = keySetting.transform.Find("GuideText");
        if (guideText != null)
            guideText.gameObject.SetActive(false);

        RectTransform content = keySetting.transform.Find("Content") as RectTransform;
        if (content == null)
            return;

        content.localScale = Vector3.one * 0.74f;
        content.anchoredPosition = new Vector2(135f, -12f);
    }

    private void ApplyReadySkillIconOverrides(GameObject keySettingRoot)
    {
        if (keySettingRoot == null)
            return;

        ApplySkillIconOverride(keySettingRoot.transform, "MoveSpeedSkillIcon", moveSpeedSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "AttackSpeedSkillIcon", attackSpeedSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "QuickStepPassiveSkillIcon", quickStepPassiveSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "PowerShotSkillIcon", powerShotSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "RapidVolleySkillIcon", rapidVolleySkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "WarriorDownStrikeSkillIcon", warriorDownStrikeSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "DownStrikeSkillIcon", warriorDownStrikeSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "DownstrikeSkillIcon", warriorDownStrikeSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "WarriorShieldBlockSkillIcon", warriorShieldBlockSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "ShieldBlockSkillIcon", warriorShieldBlockSkillIcon);
        ApplySkillIconOverride(keySettingRoot.transform, "ShiledSkillIcon", warriorShieldBlockSkillIcon);
    }

    private void ApplyReadyPanelLocalPlayerSkillSet()
    {
        if (readyPanel == null)
            return;

        Transform keySettingPreview = readyPanel.transform.Find("KeySettingPreview");
        if (keySettingPreview == null)
            return;

        if (GameEntryCharacterSelectionStore.LocalSelectedCharacter == PlayerCharacterType.Warrior)
        {
            KeyBindingManager.SetBindingProfileForCharacter(PlayerCharacterType.Warrior);
            // [Codex Client Warrior ReadyPanel] 참가하기(Client)는 워리어라서 아처 공격 스킬 슬롯을 워리어 스킬로 교체합니다.
            ApplySkillSlotOverride(
                keySettingPreview,
                "PowerShotSkillIcon",
                warriorDownStrikeSkillIcon,
                KeySettingSkillType.WarriorDownStrike);
            ApplySkillSlotOverride(
                keySettingPreview,
                "RapidVolleySkillIcon",
                warriorShieldBlockSkillIcon,
                KeySettingSkillType.WarriorShieldBlock);
            ApplySkillTextOverride(keySettingPreview, "PowerShotSkillText", "내려찍기\n공중에서 강하게 내려찍어\n범위 피해");
            ApplySkillTextOverride(keySettingPreview, "RapidVolleySkillText", "방패막기\n잠시 전방 공격을 막고\n피해 감소");
            return;
        }

        KeyBindingManager.SetBindingProfileForCharacter(PlayerCharacterType.Archer);
        ApplyReadySkillIconOverrides(keySettingPreview.gameObject);
    }

    private bool IsLocalClientWarrior()
    {
        return networkManager != null &&
            networkManager.IsListening &&
            networkManager.IsClient &&
            !networkManager.IsServer;
    }

    private void ApplySkillSlotOverride(
        Transform root,
        string iconObjectName,
        Sprite iconSprite,
        KeySettingSkillType skillType)
    {
        Transform iconTransform = FindChildRecursive(root, iconObjectName);
        if (iconTransform == null)
            return;

        Image iconImage = iconTransform.GetComponent<Image>();
        if (iconImage != null && iconSprite != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.preserveAspect = true;
        }

        SkillIconDragHandler dragHandler = iconTransform.GetComponent<SkillIconDragHandler>();
        if (dragHandler != null)
            dragHandler.ConfigureSkillType(skillType);
    }

    private void ApplySkillTextOverride(Transform root, string textObjectName, string text)
    {
        Transform textTransform = FindChildRecursive(root, textObjectName);
        TextMeshProUGUI textComponent = textTransform != null ? textTransform.GetComponent<TextMeshProUGUI>() : null;
        if (textComponent != null)
            textComponent.text = text;
    }

    private void ApplySkillIconOverride(Transform root, string iconObjectName, Sprite overrideSprite)
    {
        if (overrideSprite == null)
            return;

        Transform iconTransform = FindChildRecursive(root, iconObjectName);
        Image iconImage = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        if (iconImage == null)
            return;

        // [Codex ReadyPanel Skill Icon Override] Inspector에서 지정한 Sprite를 ReadyPanel 프리뷰 아이콘에만 덮어씁니다.
        iconImage.sprite = overrideSprite;
        iconImage.preserveAspect = true;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Button CreateReadyButton(Transform parent)
    {
        GameObject buttonObject = CreateImage("ReadyCompleteButton", parent, readyCompleteButtonSprite, Color.white);
        SetCentered(buttonObject.GetComponent<RectTransform>(), new Vector2(310f, 120f), new Vector2(0f, -390f));
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.raycastTarget = true;
        return buttonObject.AddComponent<Button>();
    }

    private void ClearGeneratedReadyChildren(Transform panel)
    {
        string[] generatedNames =
        {
            "Title",
            "Info",
            "ReadyTitleImage",
            "KeySettingPreview",
            "ReadyStatus",
            "ReadyCompleteButton"
        };

        foreach (string generatedName in generatedNames)
        {
            Transform child = panel.Find(generatedName);
            if (child == null)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void ShowOverlayInEditMode()
    {
        if (loadingOverlay == null)
            return;

        loadingOverlay.gameObject.SetActive(true);

        CanvasGroup group = loadingOverlay.GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 1f;
    }

    private GameObject CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = gameObject.layer;
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return imageObject;
    }

    private void LoadReadySpritesInEditor()
    {
#if UNITY_EDITOR
        // [Codex Skill Select Font] Ready/Skill Select 안내 글씨는 NotoSansKR-SemiBold SDF를 우선 사용합니다.
        statusFont ??= AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NotoSansKRSemiBoldSdfPath);
        readyPanelFrameSprite ??= LoadSpriteInEditor(ReadyPanelFramePath);
        skillKeySetupTitleSprite ??= LoadSpriteInEditor(SkillKeySetupTitlePath);
        readyCompleteButtonSprite ??= LoadSpriteInEditor(ReadyCompleteButtonPath);
        goblinBossKeySettingPrefab ??= AssetDatabase.LoadAssetAtPath<GameObject>(GoblinBossKeySettingPrefabPath);
        warriorDownStrikeSkillIcon ??= LoadSpriteInEditor(WarriorDownStrikeIconPath);
        warriorShieldBlockSkillIcon ??= LoadSpriteInEditor(WarriorShieldBlockIconPath);
#endif
    }

    private void LoadCharacterSelectSpritesInEditor()
    {
#if UNITY_EDITOR
        characterSelectBackgroundSprite ??= LoadSpriteInEditor(CharacterSelectBackgroundPath);
        characterSelectTitleSprite ??= LoadSpriteInEditor(CharacterSelectTitlePath);
        archerCardSprite ??= LoadSpriteInEditor(ArcherCardPath);
        warriorCardSprite ??= LoadSpriteInEditor(WarriorCardPath);
        confirmButtonSprite ??= LoadSpriteInEditor(ConfirmButtonPath);
#endif
    }

    private static Sprite LoadSpriteInEditor(string assetPath)
    {
#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite largestSprite = null;
        float largestArea = 0f;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                float area = sprite.rect.width * sprite.rect.height;
                if (area > largestArea)
                {
                    largestArea = area;
                    largestSprite = sprite;
                }
            }
        }

        return largestSprite;
#else
        return null;
#endif
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

    private static Vector2 GetSpriteNativeSize(Sprite sprite)
    {
        if (sprite == null)
            return new Vector2(420f, 420f);

        return new Vector2(sprite.rect.width, sprite.rect.height);
    }
}
