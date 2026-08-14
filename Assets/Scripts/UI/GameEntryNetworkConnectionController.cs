using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

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

    private GameEntryLoadingOverlay loadingOverlay;
    private NetworkManager networkManager;
    private bool callbacksRegistered;
    private bool waitingAsHost;
    private bool waitingAsClient;
    private bool readyPanelSequenceStarted;

    private const string NetworkManagerResourcePath = "NetworkManager";
    private const string LoadingSwirlResourcePath = "UI/GameEntry/GameEntry_LoadingSwirl_Cartoon";

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

        if (readyPanel == null)
            readyPanel = BuildReadyPanel();

        if (readyPanel != null)
            readyPanel.SetActive(true);

        Debug.Log("[GameEntryNetwork] ReadyPanel shown after both players connected.");
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
        Transform existing = transform.Find("ReadyPanel");
        if (existing != null)
            return existing.gameObject;

        GameObject panel = new GameObject("ReadyPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = gameObject.layer;
        panel.transform.SetParent(transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        SetCentered(panelRect, new Vector2(860f, 320f), new Vector2(0f, -110f));

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.03f, 0.04f, 0.08f, 0.82f);
        panelImage.raycastTarget = true;

        TextMeshProUGUI title = CreateReadyText("Title", panel.transform, "출전 준비", 44f, new Vector2(0f, 95f));
        title.color = new Color(1f, 0.92f, 0.62f, 1f);

        TextMeshProUGUI info = CreateReadyText("Info", panel.transform, "캐릭터와 조작키를 확인하세요", 30f, new Vector2(0f, 30f));
        info.color = Color.white;

        TextMeshProUGUI pending = CreateReadyText("ReadyStatus", panel.transform, "READY 동기화는 다음 단계에서 연결", 26f, new Vector2(0f, -55f));
        pending.color = new Color(0.78f, 0.88f, 1f, 1f);

        panel.SetActive(false);
        return panel;
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
