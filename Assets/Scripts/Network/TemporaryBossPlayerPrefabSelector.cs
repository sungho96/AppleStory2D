using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class TemporaryBossPlayerPrefabSelector : MonoBehaviour
{
    [Header("Temporary Boss Scene Player Prefabs")]
    [SerializeField] private NetworkObject hostArcherPrefab;
    [SerializeField] private NetworkObject clientWarriorPrefab;

    [Header("Spawn Positions")]
    [SerializeField] private Vector3 hostArcherSpawnPosition = new Vector3(-2f, 1f, 0f);
    [SerializeField] private Vector3 clientWarriorSpawnPosition = new Vector3(2f, 1f, 0f);

    private NetworkManager networkManager;
    private const string BossNetworkSceneName = "GoblinBoss_Network";

    private void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            return;

        // Codex: Temporary step 1 rule. Host spawns as Archer, remote clients spawn as Warrior.
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback += ApproveConnection;
        SceneManager.sceneLoaded += OnSceneLoaded;
        networkManager.SceneManager.OnLoadEventCompleted += OnNetworkSceneLoadCompleted;
    }

    private void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.ConnectionApprovalCallback -= ApproveConnection;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (networkManager.SceneManager != null)
            networkManager.SceneManager.OnLoadEventCompleted -= OnNetworkSceneLoadCompleted;
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        NetworkObject selectedPrefab = request.ClientNetworkId == NetworkManager.ServerClientId
            ? hostArcherPrefab
            : clientWarriorPrefab;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = selectedPrefab != null ? selectedPrefab.PrefabIdHash : null;

        // Codex: These temporary spawn points are exposed so the boss-map placement can be tuned in the Inspector.
        response.Position = request.ClientNetworkId == NetworkManager.ServerClientId
            ? hostArcherSpawnPosition
            : clientWarriorSpawnPosition;
        response.Rotation = Quaternion.identity;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (networkManager == null || !networkManager.IsServer || scene.name != BossNetworkSceneName)
            return;

        StartCoroutine(EnsureBossPlayersAfterSceneLoad());
    }

    private void OnNetworkSceneLoadCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (networkManager == null || !networkManager.IsServer || sceneName != BossNetworkSceneName)
            return;

        StartCoroutine(EnsureBossPlayersAfterSceneLoad());
    }

    private IEnumerator EnsureBossPlayersAfterSceneLoad()
    {
        yield return null;
        yield return null;

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
            EnsureBossPlayer(clientId);
    }

    private void EnsureBossPlayer(ulong clientId)
    {
        if (!networkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
            return;

        Vector3 spawnPosition = clientId == NetworkManager.ServerClientId
            ? hostArcherSpawnPosition
            : clientWarriorSpawnPosition;

        NetworkObject existingPlayer = client.PlayerObject;
        if (existingPlayer != null && existingPlayer.IsSpawned)
        {
            // [Codex Boss Scene Respawn] 씬 전환 뒤 기존 PlayerObject가 있으면 보스맵 시작 위치로 옮깁니다.
            existingPlayer.transform.position = spawnPosition;
            existingPlayer.transform.rotation = Quaternion.identity;
            Debug.Log($"[TemporaryBossPlayerPrefabSelector] Moved existing player clientId={clientId} to {spawnPosition}.");
            return;
        }

        NetworkObject prefab = clientId == NetworkManager.ServerClientId
            ? hostArcherPrefab
            : clientWarriorPrefab;

        if (prefab == null)
        {
            Debug.LogWarning($"[TemporaryBossPlayerPrefabSelector] Player prefab is missing for clientId={clientId}.");
            return;
        }

        // [Codex Boss Scene Respawn] 씬 전환 후 PlayerObject가 없으면 역할에 맞는 프리팹을 다시 스폰합니다.
        NetworkObject player = Instantiate(prefab, spawnPosition, Quaternion.identity);
        player.SpawnAsPlayerObject(clientId, true);
        Debug.Log($"[TemporaryBossPlayerPrefabSelector] Spawned boss player clientId={clientId}, prefab={prefab.name}, position={spawnPosition}.");
    }
}
