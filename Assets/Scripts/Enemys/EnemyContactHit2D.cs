using UnityEngine;

public class EnemyContactHit2D : MonoBehaviour
{
    [SerializeField] private float knockbackX = 11f;
    [SerializeField] private float knockbackY = 4.5f;

    private EnemyDamageSource2D damageSource;

    private void Awake()
    {
        damageSource = GetComponent<EnemyDamageSource2D>();
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        TryHit(col.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void TryHit(Collider2D other)
    {
        // [Codex Warrior Hit] Apply the calculated contact damage and knockback to the player.
        var health = other.GetComponentInParent<PlayerHealth2D>();
        if (health == null) return;

        int rawDamage = (damageSource != null) ? damageSource.ContactDamage : 1;

        float dir = (other.transform.position.x >= transform.position.x) ? 1f : -1f;
        Vector2 force = new Vector2(dir * knockbackX, knockbackY);

        // 최종 데미지 계산/무적 체크는 PlayerHealth2D가 한다
        health.TakeDamage(rawDamage, force);
    }
}