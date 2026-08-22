using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameEntryReadyNetworkController : MonoBehaviour
{
    private const string ReadyMessageName = "GameEntryReadyState";
    private const byte ClientReadyRequest = 0;
    private const byte ServerReadySnapshot = 1;

    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyStatusText;
    [SerializeField] private TextMeshProUGUI skillSelectionStatusText;
    [SerializeField] private float fadeOutDuration = 0.75f;
    [SerializeField] private string bossNetworkSceneName = "GoblinBoss_Network";

    private readonly HashSet<ulong> readyClients = new();
    private NetworkManager networkManager;
    private bool isLocalReady;
    private bool messageRegistered;
    private bool transitionStarted;

    private void Update()
    {
        RefreshSkillSelectionStatus();
    }

    private void OnEnable()
    {
        // [Codex GameEntry Fresh Start] Restart 후 Ready 패널은 이전 준비 상태와 전환 플래그를 이어받지 않습니다.
        readyClients.Clear();
        isLocalReady = false;
        transitionStarted = false;

        if (readyButton != null)
            readyButton.onClick.AddListener(ToggleLocalReady);

        RegisterNetworkMessage();
        RefreshStatus();
    }

    private void OnDisable()
    {
        if (readyButton != null)
            readyButton.onClick.RemoveListener(ToggleLocalReady);

        UnregisterNetworkMessage();
    }

    public void Initialize(
        Button button,
        TextMeshProUGUI statusText,
        TextMeshProUGUI selectionStatusText = null)
    {
        readyButton = button;
        readyStatusText = statusText;
        skillSelectionStatusText = selectionStatusText;
        RefreshStatus();
        RefreshSkillSelectionStatus();
    }

    private void ToggleLocalReady()
    {
        if (!isLocalReady &&
            !KeyBindingManager.HasRequiredSkillSelection())
        {
            // [Codex Ready Skill Requirement] Active와 Buff가 각각 1개일 때만 다음 단계로 진행합니다.
            RefreshSkillSelectionStatus();

            if (readyStatusText != null)
            {
                readyStatusText.text =
                    "버프 스킬 1개와 공격 스킬 1개를 모두 키에 배치해야 준비할 수 있습니다.";
                readyStatusText.color =
                    new Color(1f, 0.55f, 0.42f, 1f);
            }

            return;
        }

        isLocalReady = !isLocalReady;
        RegisterNetworkMessage();

        if (networkManager == null || !networkManager.IsListening)
        {
            // [Codex Ready Network] 에디터 단독 확인 중에도 버튼 상태를 눈으로 확인할 수 있게 로컬 표시만 갱신합니다.
            RefreshStatus();
            return;
        }

        if (networkManager.IsServer)
            ApplyReadyState(networkManager.LocalClientId, isLocalReady);
        else
            SendReadyStateToServer(isLocalReady);
    }

    private void RegisterNetworkMessage()
    {
        if (messageRegistered)
            return;

        networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.CustomMessagingManager == null)
            return;

        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
            ReadyMessageName,
            OnReadyMessageReceived);
        messageRegistered = true;
    }

    private void UnregisterNetworkMessage()
    {
        if (!messageRegistered || networkManager == null || networkManager.CustomMessagingManager == null)
            return;

        networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(ReadyMessageName);
        messageRegistered = false;
    }

    private void SendReadyStateToServer(bool ready)
    {
        using FastBufferWriter writer = new FastBufferWriter(sizeof(byte) + sizeof(bool), Unity.Collections.Allocator.Temp);
        writer.WriteValueSafe(ClientReadyRequest);
        writer.WriteValueSafe(ready);
        networkManager.CustomMessagingManager.SendNamedMessage(
            ReadyMessageName,
            NetworkManager.ServerClientId,
            writer);
    }

    private void OnReadyMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte messageType);

        if (messageType == ServerReadySnapshot)
        {
            reader.ReadValueSafe(out int readyCount);
            reader.ReadValueSafe(out int totalCount);
            reader.ReadValueSafe(out bool allReady);
            RefreshStatus(allReady, readyCount, totalCount);
            return;
        }

        if (networkManager == null || !networkManager.IsServer)
            return;

        reader.ReadValueSafe(out bool ready);
        ApplyReadyState(senderClientId, ready);
    }

    private void ApplyReadyState(ulong clientId, bool ready)
    {
        if (ready)
            readyClients.Add(clientId);
        else
            readyClients.Remove(clientId);

        // [Codex Boss Intro Hook] 두 명 모두 준비된 시점에 보스씬 입장/인트로 연출을 이어 붙일 수 있는 분기점입니다.
        bool allReady = networkManager != null &&
            networkManager.ConnectedClientsIds.Count >= 2 &&
            readyClients.Count >= networkManager.ConnectedClientsIds.Count;

        BroadcastReadySnapshot(allReady);
        RefreshStatus(allReady, readyClients.Count, networkManager.ConnectedClientsIds.Count);
        Debug.Log($"[GameEntryReady] clientId={clientId}, ready={ready}, allReady={allReady}");
    }

    private void BroadcastReadySnapshot(bool allReady)
    {
        if (networkManager == null || !networkManager.IsServer)
            return;

        using FastBufferWriter writer = new FastBufferWriter(sizeof(byte) + sizeof(int) + sizeof(int) + sizeof(bool), Unity.Collections.Allocator.Temp);
        writer.WriteValueSafe(ServerReadySnapshot);
        writer.WriteValueSafe(readyClients.Count);
        writer.WriteValueSafe(networkManager.ConnectedClientsIds.Count);
        writer.WriteValueSafe(allReady);
        networkManager.CustomMessagingManager.SendNamedMessageToAll(ReadyMessageName, writer);
    }

    private void RefreshStatus(bool allReady = false, int readyCount = -1, int totalCount = -1)
    {
        if (readyStatusText == null)
            return;

        if (allReady)
        {
            readyStatusText.text = "두 플레이어 준비 완료";
            readyStatusText.color = new Color(0.7f, 1f, 0.55f, 1f);
            BeginBossSceneTransition();
            return;
        }

        string countText = readyCount >= 0 && totalCount > 0
            ? $" ({readyCount}/{totalCount})"
            : "";
        readyStatusText.text = isLocalReady ? $"내 준비 완료 - 상대를 기다리는 중{countText}" : "";
        readyStatusText.color = isLocalReady
            ? new Color32(65, 75, 173, 255)
            : new Color(1f, 0.92f, 0.62f, 1f);
    }

    private void RefreshSkillSelectionStatus()
    {
        if (skillSelectionStatusText == null)
        {
            return;
        }

        int buffCount =
            KeyBindingManager.GetSelectedBuffSkillCount();

        int activeCount =
            KeyBindingManager.GetSelectedActiveSkillCount();

        skillSelectionStatusText.text =
            $"버프 스킬 {buffCount}/1   공격스킬 {activeCount}/1";

    }

    private void BeginBossSceneTransition()
    {
        if (transitionStarted)
            return;

        transitionStarted = true;
        KeyBindingManager.SaveCurrentBindings();
        StartCoroutine(FadeOutAndLoadBossScene());
    }

    private System.Collections.IEnumerator FadeOutAndLoadBossScene()
    {
        if (readyButton != null)
            readyButton.interactable = false;

        CanvasGroup fadeGroup = BuildFadeOverlay();
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeOutDuration));
            yield return null;
        }

        fadeGroup.alpha = 1f;

        if (networkManager != null && networkManager.IsServer)
        {
            // [Codex Ready Scene Transition] 서버만 Netcode 씬 전환을 요청하고, 클라이언트는 같은 전환을 따라갑니다.
            networkManager.SceneManager.LoadScene(bossNetworkSceneName, LoadSceneMode.Single);
        }
    }

    private CanvasGroup BuildFadeOverlay()
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        Transform parent = rootCanvas != null ? rootCanvas.transform : transform;

        GameObject overlay = new GameObject("ReadyBossSceneFade", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        overlay.transform.SetParent(parent, false);
        overlay.transform.SetAsLastSibling();

        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        Image image = overlay.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true;
        group.interactable = true;
        return group;
    }
}
