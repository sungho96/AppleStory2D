using Unity.Netcode;
using UnityEngine;

public class Arrow : NetworkBehaviour
{
    [SerializeField] private float lifeTime = 3f;

    private void Start()
    {
        if (IsNetworkActive() && !IsServer)
            return;

        Invoke(nameof(Expire), lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsNetworkActive() && !IsServer)
            return;

        if (other.CompareTag("Player"))
            return;

        if (other.CompareTag("Arrow"))
            return;

        Debug.Log("Arrow hit target: " + other.name);
        DespawnOrDestroy();
    }

    private void Expire()
    {
        DespawnOrDestroy();
    }

    private void DespawnOrDestroy()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            networkObject.Despawn(true);
            return;
        }

        Destroy(gameObject);
    }

    private bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
