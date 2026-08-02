using Unity.Netcode;
using UnityEngine;

public class ArrowProjectile2D : NetworkBehaviour
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float lifeTime = 3f;

    private float moveDir;
    private bool applyHitReaction = true;

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

        GoblinHealth2D enemyHealth = other.GetComponentInParent<GoblinHealth2D>();
        if (enemyHealth == null)
            return;

        Debug.Log($"Arrow hit target: {other.name}");
        enemyHealth.TakeDamage(damage, moveDir, applyHitReaction);
        DespawnOrDestroy();
    }

    public void SetDirection(float dir)
    {
        moveDir = dir;
    }

    public void Configure(int configuredDamage, float dir, bool useHitReaction)
    {
        Configure(configuredDamage, dir, useHitReaction, 0f);
    }

    public void Configure(int configuredDamage, float dir, bool useHitReaction, float speed)
    {
        damage = Mathf.Max(1, configuredDamage);
        moveDir = Mathf.Sign(dir);
        applyHitReaction = useHitReaction;

        Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
        if (rigidbody == null)
            return;

        rigidbody.linearVelocity = new Vector2(moveDir * speed, 0f);
    }

    [ClientRpc]
    public void ApplyVelocityClientRpc(float dir, float speed)
    {
        // Codex: Apply movement after NetworkObject.Spawn so clients never write pre-spawn NetworkVariables.
        moveDir = Mathf.Sign(dir);

        Rigidbody2D rigidbody = GetComponent<Rigidbody2D>();
        if (rigidbody != null)
            rigidbody.linearVelocity = new Vector2(moveDir * speed, 0f);
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
