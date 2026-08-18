using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GameEntryReadyNetworkController : MonoBehaviour
{
    private const string ReadyMessageName = "GameEntryReadyState";
    private const byte ClientReadyRequest = 0;
    private const byte ServerReadySnapshot = 1;

    [SerializeField] private Button readyButton;
    [SerializeField] private TextMeshProUGUI readyStatusText;

    private readonly HashSet<ulong> readyClients = new();
    private NetworkManager networkManager;
    private bool isLocalReady;
    private bool messageRegistered;

    private void OnEnable()
    {
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

    public void Initialize(Button button, TextMeshProUGUI statusText)
    {
        readyButton = button;
        readyStatusText = statusText;
        RefreshStatus();
    }

    private void ToggleLocalReady()
    {
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
            return;
        }

        string countText = readyCount >= 0 && totalCount > 0
            ? $" ({readyCount}/{totalCount})"
            : "";
        readyStatusText.text = isLocalReady ? $"내 준비 완료 - 상대를 기다리는 중{countText}" : $"준비 완료 버튼을 눌러주세요{countText}";
        readyStatusText.color = isLocalReady
            ? new Color(0.78f, 0.92f, 1f, 1f)
            : new Color(1f, 0.92f, 0.62f, 1f);
    }
}
