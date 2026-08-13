using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TemporaryBossNetworkSetup
{
    private const string NetworkManagerPath = "Assets/Resources/NetworkManager.prefab";
    private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
    private const string ArcherPath = "Assets/Art/Prefabs/Player/Archer.prefab";
    private const string WarriorPath = "Assets/Art/Prefabs/Player/warrior.prefab";
    private const string NetworkScenePath = "Assets/Scenes/GoblinBoss_Network.unity";

    [MenuItem("AppleStory/Network/Apply Temporary Boss Network Setup")]
    public static void Apply()
    {
        GameObject networkManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPath);
        GameObject archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArcherPath);
        GameObject warriorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPath);

        if (networkManagerPrefab == null || archerPrefab == null || warriorPrefab == null)
        {
            Debug.LogError("[TemporaryBossNetworkSetup] Check the NetworkManager, Archer, and warrior prefab paths.");
            return;
        }

        NetworkObject archerNetworkObject = EnsureNetworkPlayerPrefab(archerPrefab);
        NetworkObject warriorNetworkObject = EnsureNetworkPlayerPrefab(warriorPrefab);
        ConfigureNetworkManager(networkManagerPrefab, archerNetworkObject, warriorNetworkObject);
        EnsureNetworkScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TemporaryBossNetworkSetup] Done: temporary boss network setup applied. Host=Archer, Client=Warrior.");
    }

    private static NetworkObject EnsureNetworkPlayerPrefab(GameObject prefabAsset)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            NetworkObject networkObject = prefabRoot.GetComponent<NetworkObject>();
            if (networkObject == null)
                networkObject = prefabRoot.AddComponent<NetworkObject>();

            // Codex: Use Netcode's built-in transform sync for this temporary movement test.
            if (prefabRoot.GetComponent<NetworkTransform>() == null)
                prefabRoot.AddComponent<NetworkTransform>();

            NetworkPlayerOwner owner = prefabRoot.GetComponent<NetworkPlayerOwner>();
            if (owner == null)
                owner = prefabRoot.AddComponent<NetworkPlayerOwner>();

            if (prefabRoot.GetComponent<NetworkPlayerVisualSync>() == null)
                prefabRoot.AddComponent<NetworkPlayerVisualSync>();

            SerializedObject ownerObject = new SerializedObject(owner);
            ownerObject.FindProperty("playerController").objectReferenceValue = prefabRoot.GetComponent<PlayerController2D>();
            ownerObject.FindProperty("playerAttack").objectReferenceValue = prefabRoot.GetComponent<PlayerAttack2D>();
            ownerObject.FindProperty("warriorAttack").objectReferenceValue = prefabRoot.GetComponent<WarriorAttack2D>();
            ownerObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<NetworkObject>();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ConfigureNetworkManager(GameObject networkManagerPrefab, NetworkObject archerPrefab, NetworkObject warriorPrefab)
    {
        string prefabPath = AssetDatabase.GetAssetPath(networkManagerPrefab);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            NetworkManager networkManager = prefabRoot.GetComponent<NetworkManager>();
            TemporaryBossPlayerPrefabSelector selector = prefabRoot.GetComponent<TemporaryBossPlayerPrefabSelector>();

            if (selector == null)
                selector = prefabRoot.AddComponent<TemporaryBossPlayerPrefabSelector>();

            SerializedObject selectorObject = new SerializedObject(selector);
            selectorObject.FindProperty("hostArcherPrefab").objectReferenceValue = archerPrefab;
            selectorObject.FindProperty("clientWarriorPrefab").objectReferenceValue = warriorPrefab;
            selectorObject.FindProperty("hostArcherSpawnPosition").vector3Value = new Vector3(-2f, 1f, 0f);
            selectorObject.FindProperty("clientWarriorSpawnPosition").vector3Value = new Vector3(2f, 1f, 0f);
            selectorObject.ApplyModifiedPropertiesWithoutUndo();

            if (networkManager != null)
            {
                networkManager.NetworkConfig.ConnectionApproval = true;
                AddNetworkPrefab(archerPrefab.gameObject);
                AddNetworkPrefab(warriorPrefab.gameObject);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void AddNetworkPrefab(GameObject prefab)
    {
        NetworkPrefabsList prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);

        if (prefabsList == null)
        {
            Debug.LogError("[TemporaryBossNetworkSetup] DefaultNetworkPrefabs.asset was not found.");
            return;
        }

        if (prefabsList.Contains(prefab))
            return;

        // Codex: Persist Archer/Warrior in the shared Netcode prefab list used by NetworkManager.
        prefabsList.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(prefabsList);
    }

    private static void EnsureNetworkScene()
    {
        if (!System.IO.File.Exists(NetworkScenePath))
            AssetDatabase.CopyAsset("Assets/Scenes/GoblinBoss.unity", NetworkScenePath);

        Scene scene = EditorSceneManager.OpenScene(NetworkScenePath, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene);
    }
}
