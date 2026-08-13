using Unity.Netcode;
using UnityEngine;

public class TemporaryBossPlayerPrefabSelector : MonoBehaviour
{
    [Header("Temporary Boss Scene Player Prefabs")]
    [SerializeField] private NetworkObject hostArcherPrefab;
    [SerializeField] private NetworkObject clientWarriorPrefab;

    [Header("Spawn Positions")]
    [SerializeField] private Vector3 hostArcherSpawnPosition = new Vector3(-2f, 1f, 0f);
    [SerializeField] private Vector3 clientWarriorSpawnPosition = new Vector3(2f, 1f, 0f);

    private NetworkManager networkManager;

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
    }

    private void OnDestroy()
    {
        if (networkManager == null)
            return;

        networkManager.ConnectionApprovalCallback -= ApproveConnection;
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
}
