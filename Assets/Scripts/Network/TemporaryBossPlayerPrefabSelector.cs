using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TemporaryBossPlayerPrefabSelector : MonoBehaviour
{
    [Header("Boss Scene Player Prefabs")]
    [SerializeField] private NetworkObject hostArcherPrefab;
    [SerializeField] private NetworkObject clientWarriorPrefab;

    [Header("Spawn Positions")]
    [SerializeField]
    private Vector3 hostArcherSpawnPosition =
        new Vector3(-7f, 7.67f, 0f);

    [SerializeField]
    private Vector3 clientWarriorSpawnPosition =
        new Vector3(-4f, 7.67f, 0f);

    private NetworkManager networkManager;

    private const string BossNetworkSceneName = "GoblinBoss_Network";

    // 중복 생성 방지
    private bool bossPlayersSpawned;

    // NetworkSceneManager 이벤트 중복 등록 방지
    private bool networkSceneEventRegistered;


    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError(
                "[BossPlayerSelector] NetworkManager를 찾지 못했습니다."
            );
            return;
        }

        /*
         * GameEntry에서는 접속만 하고
         * 캐릭터(PlayerObject)는 생성하지 않습니다.
         */
        networkManager.NetworkConfig.ConnectionApproval = true;

        networkManager.ConnectionApprovalCallback += ApproveConnection;

        /*
         * StartHost가 완료된 뒤 NetworkSceneManager 이벤트를
         * 확실하게 등록하기 위해 사용합니다.
         */
        networkManager.OnServerStarted += OnServerStarted;

        /*
         * 혹시 NetworkSceneManager 이벤트를 놓쳤을 때를 위한
         * 보조 감지입니다.
         */
        SceneManager.sceneLoaded += OnUnitySceneLoaded;


        /*
         * 만약 이 스크립트가 서버 시작 이후 활성화된 경우도 대응합니다.
         */
        if (networkManager.IsListening && networkManager.IsServer)
        {
            RegisterNetworkSceneEvent();
        }

        Debug.Log(
            "[BossPlayerSelector] Awake 완료. " +
            "GameEntry에서는 PlayerObject를 생성하지 않습니다."
        );
    }


    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;

        if (networkManager == null)
            return;

        networkManager.ConnectionApprovalCallback -= ApproveConnection;
        networkManager.OnServerStarted -= OnServerStarted;

        if (networkSceneEventRegistered &&
            networkManager.SceneManager != null)
        {
            networkManager.SceneManager.OnLoadEventCompleted -=
                OnNetworkSceneLoadCompleted;
        }

        networkSceneEventRegistered = false;
    }


    // =========================================================
    // GameEntry 접속 승인
    // =========================================================

    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = true;

        /*
         * 핵심:
         *
         * GameEntry에서는 PlayerObject를 만들지 않습니다.
         * GoblinBoss_Network에 도착했을 때 직접 생성합니다.
         */
        response.CreatePlayerObject = false;

        response.PlayerPrefabHash = null;
        response.Position = null;
        response.Rotation = null;
        response.Pending = false;

        Debug.Log(
            $"[BossPlayerSelector] Connection Approved. " +
            $"clientId={request.ClientNetworkId} / " +
            $"PlayerObject 생성 대기"
        );
    }


    // =========================================================
    // Host 시작 완료
    // =========================================================

    private void OnServerStarted()
    {
        Debug.Log(
            "[BossPlayerSelector] Server Started. " +
            "NetworkSceneManager 이벤트 등록을 시도합니다."
        );

        RegisterNetworkSceneEvent();
    }


    private void RegisterNetworkSceneEvent()
    {
        if (networkManager == null)
            return;

        if (!networkManager.IsServer)
            return;

        if (networkSceneEventRegistered)
            return;

        if (networkManager.SceneManager == null)
        {
            Debug.LogWarning(
                "[BossPlayerSelector] NetworkSceneManager가 아직 준비되지 않았습니다."
            );
            return;
        }

        networkManager.SceneManager.OnLoadEventCompleted +=
            OnNetworkSceneLoadCompleted;

        networkSceneEventRegistered = true;

        Debug.Log(
            "[BossPlayerSelector] NetworkSceneManager " +
            "OnLoadEventCompleted 등록 성공."
        );
    }


    // =========================================================
    // NGO 씬 로딩 완료
    // =========================================================

    private void OnNetworkSceneLoadCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (networkManager == null)
            return;

        if (!networkManager.IsServer)
            return;

        if (sceneName != BossNetworkSceneName)
            return;

        Debug.Log(
            $"[BossPlayerSelector] NGO 씬 로딩 완료: {sceneName} / " +
            $"완료 Client={clientsCompleted.Count} / " +
            $"Timeout={clientsTimedOut.Count}"
        );

        StartCoroutine(
            SpawnBossPlayersAfterSceneLoaded(
                "NGO OnLoadEventCompleted"
            )
        );
    }


    // =========================================================
    // Unity 기본 SceneLoaded
    // =========================================================

    private void OnUnitySceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (scene.name != BossNetworkSceneName)
            return;

        Debug.Log(
            $"[BossPlayerSelector] Unity sceneLoaded 감지: {scene.name}"
        );

        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError(
                "[BossPlayerSelector] 보스 씬에서 NetworkManager를 찾지 못했습니다."
            );
            return;
        }

        if (!networkManager.IsServer)
            return;


        /*
         * 혹시 아직 NetworkSceneManager 이벤트가
         * 등록되지 않았다면 여기서 다시 한번 등록합니다.
         */
        if (!networkSceneEventRegistered)
        {
            RegisterNetworkSceneEvent();
        }


        /*
         * NetworkSceneManager 이벤트 등록에 성공했다면
         * NGO의 OnLoadEventCompleted를 기다립니다.
         *
         * 그래야 Client까지 로딩이 끝난 뒤 생성할 수 있습니다.
         */
        if (networkSceneEventRegistered)
        {
            Debug.Log(
                "[BossPlayerSelector] NGO 씬 완료 이벤트를 기다립니다."
            );
            return;
        }


        /*
         * NGO 이벤트 등록에 실패했을 경우에만
         * Unity sceneLoaded를 fallback으로 사용합니다.
         */
        StartCoroutine(
            SpawnBossPlayersAfterSceneLoaded(
                "Unity sceneLoaded fallback"
            )
        );
    }


    // =========================================================
    // Boss Player Spawn
    // =========================================================

    private IEnumerator SpawnBossPlayersAfterSceneLoaded(
        string source)
    {
        /*
         * 씬 오브젝트들이 초기화될 시간을 조금 줍니다.
         */
        yield return null;
        yield return null;

        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError(
                "[BossPlayerSelector] Spawn 실패: NetworkManager 없음."
            );
            yield break;
        }

        if (!networkManager.IsServer)
        {
            yield break;
        }

        if (!networkManager.IsListening)
        {
            Debug.LogError(
                "[BossPlayerSelector] Spawn 실패: NetworkManager가 Listening 상태가 아닙니다."
            );
            yield break;
        }

        if (bossPlayersSpawned)
        {
            Debug.Log(
                "[BossPlayerSelector] 이미 Boss Player 생성이 완료되어 " +
                "중복 Spawn을 건너뜁니다."
            );
            yield break;
        }


        int connectedCount =
            networkManager.ConnectedClientsIds.Count;

        Debug.Log(
            $"[BossPlayerSelector] Boss Spawn 시작. " +
            $"source={source}, " +
            $"ConnectedClients={connectedCount}"
        );


        if (connectedCount == 0)
        {
            Debug.LogError(
                "[BossPlayerSelector] 접속된 Client가 없습니다."
            );
            yield break;
        }


        /*
         * 먼저 true로 설정해
         * sceneLoaded / OnLoadEventCompleted가 동시에 들어와도
         * 중복 생성하지 않게 합니다.
         */
        bossPlayersSpawned = true;


        /*
         * ConnectedClientsIds를 직접 순회하는 도중
         * 네트워크 상태 변화가 생기는 것을 피하기 위해
         * 복사해서 사용합니다.
         */
        List<ulong> connectedClientIds =
            new List<ulong>(
                networkManager.ConnectedClientsIds
            );


        foreach (ulong clientId in connectedClientIds)
        {
            SpawnBossPlayer(clientId);
        }


        Debug.Log(
            "[BossPlayerSelector] Boss Player Spawn 처리 완료."
        );
    }


    private void SpawnBossPlayer(ulong clientId)
    {
        if (networkManager == null)
            return;


        if (!networkManager.ConnectedClients.TryGetValue(
                clientId,
                out NetworkClient client))
        {
            Debug.LogError(
                $"[BossPlayerSelector] " +
                $"clientId={clientId}의 NetworkClient를 찾지 못했습니다."
            );
            return;
        }


        /*
         * 원래는 GameEntry에서 PlayerObject 생성이 금지되어 있으므로
         * 여기서는 null이어야 정상입니다.
         */
        if (client.PlayerObject != null &&
            client.PlayerObject.IsSpawned)
        {
            Debug.LogError(
                $"[BossPlayerSelector] " +
                $"clientId={clientId}에게 이미 PlayerObject가 있습니다. " +
                $"name={client.PlayerObject.name}, " +
                $"NetworkObjectId={client.PlayerObject.NetworkObjectId}"
            );

            return;
        }


        if (!GameEntryCharacterSelectionStore.TryGetConfirmedSelection(
                clientId,
                out PlayerCharacterType selectedCharacter))
        {
            selectedCharacter =
                GameEntryCharacterSelectionStore.GetFallbackCharacterForClient(
                    clientId);

            Debug.LogWarning(
                $"[BossPlayerSelector] " +
                $"clientId={clientId}의 선택 정보가 없어 기존 역할 기준 fallback을 사용합니다. " +
                $"character={selectedCharacter}"
            );
        }


        NetworkObject selectedPrefab =
            selectedCharacter == PlayerCharacterType.Warrior
                ? clientWarriorPrefab
                : hostArcherPrefab;


        Vector3 spawnPosition =
            selectedCharacter == PlayerCharacterType.Warrior
                ? clientWarriorSpawnPosition
                : hostArcherSpawnPosition;


        string role =
            selectedCharacter.ToString();


        if (selectedPrefab == null)
        {
            Debug.LogError(
                $"[BossPlayerSelector] " +
                $"{role} Prefab이 Inspector에 연결되어 있지 않습니다. " +
                $"clientId={clientId}"
            );

            return;
        }


        Debug.Log(
            $"[BossPlayerSelector] Spawn 시도. " +
            $"clientId={clientId}, " +
            $"role={role}, " +
            $"prefab={selectedPrefab.name}, " +
            $"position={spawnPosition}"
        );


        NetworkObject player =
            Instantiate(
                selectedPrefab,
                spawnPosition,
                Quaternion.identity
            );


        if (player == null)
        {
            Debug.LogError(
                $"[BossPlayerSelector] " +
                $"{role} Instantiate 실패."
            );
            return;
        }


        /*
         * 핵심:
         *
         * 단순 Spawn()이 아니라 SpawnAsPlayerObject()를 사용해서
         * 해당 Client가 이 캐릭터를 소유하도록 만듭니다.
         */
        player.SpawnAsPlayerObject(
            clientId,
            true
        );


        Debug.Log(
            $"[BossPlayerSelector] PLAYER SPAWN SUCCESS\n" +
            $"clientId={clientId}\n" +
            $"role={role}\n" +
            $"prefab={selectedPrefab.name}\n" +
            $"IsSpawned={player.IsSpawned}\n" +
            $"OwnerClientId={player.OwnerClientId}\n" +
            $"NetworkObjectId={player.NetworkObjectId}\n" +
            $"position={player.transform.position}"
        );
    }
}
