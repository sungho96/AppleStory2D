using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerOwner : NetworkBehaviour
{
    [Header("Character")]
    [SerializeField] private PlayerCharacterType characterType = PlayerCharacterType.None;

    public PlayerCharacterType CharacterType => characterType;

    [Header("Owner Only Components")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerAttack2D playerAttack;
    [SerializeField] private WarriorAttack2D warriorAttack;

    [Header("Local HUD")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Network Delay Debug")]
    [SerializeField] private bool enableDelayDebugLog = false;
    [SerializeField] private float delayDebugInterval = 1f;

    private int delayDebugSequence;
    private float nextDelayDebugTime;

    public override void OnNetworkSpawn()
    {
        bool isLocalOwner = IsOwner;

        if (playerController != null)
        {
            playerController.enabled = isLocalOwner;
        }

        if (playerAttack != null)
        {
            playerAttack.enabled = isLocalOwner;
        }

        // Codex: Warrior uses a separate attack script, so remote warrior input must be disabled too.
        if (warriorAttack != null)
        {
            warriorAttack.enabled = isLocalOwner;
        }

        if (isLocalOwner)
        {
            BindLocalHud();
        }
    }

    private void Update()
    {
        if (!enableDelayDebugLog || !IsOwner || !IsSpawned)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        if (Time.unscaledTime < nextDelayDebugTime)
            return;

        nextDelayDebugTime = Time.unscaledTime + Mathf.Max(0.1f, delayDebugInterval);

        // [Codex Network Delay Debug] 로컬 입력 시점과 서버 수신 시점을 함께 보내 왕복 지연과 서버 시간 차이를 Console에서 확인합니다.
        double clientSendLocalTime = NetworkManager.Singleton.LocalTime.Time;
        double clientSendServerEstimate = NetworkManager.Singleton.ServerTime.Time;
        RequestDelayDebugServerRpc(
            delayDebugSequence++,
            clientSendLocalTime,
            clientSendServerEstimate);
    }

    [ServerRpc]
    private void RequestDelayDebugServerRpc(
        int sequence,
        double clientSendLocalTime,
        double clientSendServerEstimate,
        ServerRpcParams serverRpcParams = default)
    {
        if (NetworkManager.Singleton == null)
            return;

        double serverReceiveTime = NetworkManager.Singleton.ServerTime.Time;
        double serverGapFromClientEstimate = serverReceiveTime - clientSendServerEstimate;
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;

        Debug.Log(
            $"[NetworkDelayDebug][Server] seq={sequence} " +
            $"fromClient={senderClientId} " +
            $"clientServerEstimate={clientSendServerEstimate:F4}s " +
            $"serverReceive={serverReceiveTime:F4}s " +
            $"serverGap={serverGapFromClientEstimate * 1000d:F1}ms");

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { senderClientId }
            }
        };

        RespondDelayDebugClientRpc(
            sequence,
            clientSendLocalTime,
            clientSendServerEstimate,
            serverReceiveTime,
            clientRpcParams);
    }

    [ClientRpc]
    private void RespondDelayDebugClientRpc(
        int sequence,
        double clientSendLocalTime,
        double clientSendServerEstimate,
        double serverReceiveTime,
        ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner || NetworkManager.Singleton == null)
            return;

        double clientReceiveLocalTime = NetworkManager.Singleton.LocalTime.Time;
        double clientReceiveServerEstimate = NetworkManager.Singleton.ServerTime.Time;
        double roundTripTime = clientReceiveLocalTime - clientSendLocalTime;
        double serverGapFromSendEstimate = serverReceiveTime - clientSendServerEstimate;
        double clientServerEstimateGap = clientReceiveServerEstimate - serverReceiveTime;

        Debug.Log(
            $"[NetworkDelayDebug][Client] seq={sequence} " +
            $"rtt={roundTripTime * 1000d:F1}ms " +
            $"oneWay~={roundTripTime * 500d:F1}ms " +
            $"serverGapAtReceive={serverGapFromSendEstimate * 1000d:F1}ms " +
            $"clientEstimateAfterServer={clientServerEstimateGap * 1000d:F1}ms");
    }

    private void Awake()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
    }

    private void BindLocalHud()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogWarning($"[NetworkPlayerOwner] 로컬 HUD 연결 실패: PlayerStats 없음. name={name}");
            return;
        }

        HUDStatusUI[] huds = FindObjectsByType<HUDStatusUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] == null)
                continue;

            bool isCommonHud = huds[i].HudCharacterType == PlayerCharacterType.None;
            bool isMatchingCharacterHud = huds[i].HudCharacterType == characterType;
            bool shouldUseHud = isCommonHud || isMatchingCharacterHud;

            // [Codex Local HP HUD] 로컬 소유 캐릭터 타입과 맞는 HUD만 켜고, 다른 캐릭터 HUD는 끕니다.
            huds[i].gameObject.SetActive(shouldUseHud);

            if (shouldUseHud)
                huds[i].Bind(playerStats);
        }
    }
}
