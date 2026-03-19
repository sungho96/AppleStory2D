using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 반응 전용 처리.
/// - 넉백
/// - 피격 무적(쿨다운)
/// - 반투명 시각 처리
/// - 적과 충돌 무시 / 복구
/// </summary>
public class PlayerHitReaction2D : MonoBehaviour
{
    [Header("KnockBack")]
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Hit")]
    [SerializeField] private float hitCooldown = 1f;

    [Header("Layer")]
    [SerializeField] private string enemyLayerName = "Enemy";

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCol;
    private PlayerLadder2D ladder;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private bool isKnockback;
    private bool isHitCooldown;

    /// <summary>
    /// 현재 넉백 상태 여부.
    /// </summary>
    public bool IsKnockback => isKnockback;

    /// <summary>
    /// 현재 피격 무적 상태 여부.
    /// </summary>
    public bool IsHitCooldown => isHitCooldown;

    /// <summary>
    /// 외부 초기화.
    /// </summary>
    public void Initialize(Rigidbody2D targetRb, CapsuleCollider2D targetCol, PlayerLadder2D targetLadder)
    {
        rb = targetRb;
        playerCol = targetCol;
        ladder = targetLadder;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalColors[i] = renderers[i].color;
        }
    }

    /// <summary>
    /// 넉백 적용 요청.
    /// - 이미 넉백 중이거나 피격 쿨다운 중이면 무시
    /// </summary>
    public void ApplyKnockback(Vector2 force)
    {
        if (isKnockback || isHitCooldown)
            return;

        StartCoroutine(CoKnockback(force));
    }

    /// <summary>
    /// 넉백 코루틴.
    /// - 사다리 중이면 종료
    /// - 반투명 처리
    /// - 적 충돌 무시
    /// - 일정 시간 후 넉백 종료
    /// - hitCooldown 후 시각/충돌 복구
    /// </summary>
    private IEnumerator CoKnockback(Vector2 force)
    {
        isKnockback = true;
        isHitCooldown = true;

        if (ladder != null && ladder.IsClimbing)
            ladder.StopClimbing();

        SetDamageCooldownVisual(true);
        SetEnemyCollisionEnabled(false);

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.AddForce(force, ForceMode2D.Impulse);
        }

        yield return new WaitForSeconds(knockbackDuration);
        isKnockback = false;

        yield return new WaitForSeconds(hitCooldown);

        isHitCooldown = false;
        SetDamageCooldownVisual(false);
        SetEnemyCollisionEnabled(true);
    }

    /// <summary>
    /// 피격 무적 시 반투명 처리 / 해제.
    /// </summary>
    private void SetDamageCooldownVisual(bool active)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (active)
            {
                Color baseColor = originalColors[i];
                renderers[i].color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f);
            }
            else
            {
                renderers[i].color = originalColors[i];
            }
        }
    }

    /// <summary>
    /// 적과의 충돌 무시 / 복구 처리.
    /// - 플레이어 하위 Collider와
    /// - 씬의 Enemy 레이어 Collider 사이 IgnoreCollision 처리
    /// </summary>
    private void SetEnemyCollisionEnabled(bool enabled)
    {
        Collider2D[] playerCols = GetComponentsInChildren<Collider2D>(true);

        int enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        if (enemyLayer == -1)
        {
            Debug.LogWarning($"{enemyLayerName} 레이어가 없습니다.");
            return;
        }

        Collider2D[] allCols = Object.FindObjectsByType<Collider2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Collider2D myCol in playerCols)
        {
            if (myCol == null)
                continue;

            foreach (Collider2D otherCol in allCols)
            {
                if (otherCol == null)
                    continue;

                if (otherCol.gameObject.layer != enemyLayer)
                    continue;

                Physics2D.IgnoreCollision(myCol, otherCol, !enabled);
            }
        }
    }
}