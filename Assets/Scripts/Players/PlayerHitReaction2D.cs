    using System.Collections;
using UnityEngine;

/// <summary>
/// �ǰ� ���� ���� ó��.
/// - �˹�
/// - �ǰ� ����(��ٿ�)
/// - ������ �ð� ó��
/// - ���� �浹 ���� / ����
/// </summary>
public class PlayerHitReaction2D : MonoBehaviour
{
    [Header("KnockBack")]
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Hit")]
    [SerializeField] private float hitCooldown = 1f;

    [Header("Layer")]
    [SerializeField] private string enemyLayerName = "Enemy";

    [Header("Camera Shake")]
    [SerializeField] private CameraShake2D cameraShake;

    private Rigidbody2D rb;
    private CapsuleCollider2D playerCol;
    private PlayerLadder2D ladder;

    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private bool isKnockback;
    private bool isHitCooldown;

    /// <summary>
    /// ���� �˹� ���� ����.
    /// </summary>
    public bool IsKnockback => isKnockback;

    /// <summary>
    /// ���� �ǰ� ���� ���� ����.
    /// </summary>
    public bool IsHitCooldown => isHitCooldown;

    /// <summary>
    /// �ܺ� �ʱ�ȭ.
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

        if (cameraShake == null)
            cameraShake = Camera.main.GetComponent<CameraShake2D>();
    }

    /// <summary>
    /// �˹� ���� ��û.
    /// - �̹� �˹� ���̰ų� �ǰ� ��ٿ� ���̸� ����
    /// </summary>
    public void ApplyKnockback(Vector2 force)
    {
        if (isKnockback || isHitCooldown)
            return;

        StartCoroutine(CoKnockback(force));
    }

    /// <summary>
    /// �˹� �ڷ�ƾ.
    /// - ��ٸ� ���̸� ����
    /// - ������ ó��
    /// - �� �浹 ����
    /// - ���� �ð� �� �˹� ����
    /// - hitCooldown �� �ð�/�浹 ����
    /// </summary>
    private IEnumerator CoKnockback(Vector2 force)
    {
        isKnockback = true;
        isHitCooldown = true;

        cameraShake?.Shake();

        if (ladder != null && ladder.IsClimbing)
            ladder.StopClimbing();

        SetDamageCooldownVisual(true);
        SetEnemyCollisionEnabled(false);

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
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
    /// �ǰ� ���� �� ������ ó�� / ����.
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
    /// ������ �浹 ���� / ���� ó��.
    /// - �÷��̾� ���� Collider��
    /// - ���� Enemy ���̾� Collider ���� IgnoreCollision ó��
    /// </summary>
    private void SetEnemyCollisionEnabled(bool enabled)
    {
        Collider2D[] playerCols = GetComponentsInChildren<Collider2D>(true);

        int enemyLayer = LayerMask.NameToLayer(enemyLayerName);
        if (enemyLayer == -1)
        {
            Debug.LogWarning($"{enemyLayerName} ���̾ �����ϴ�.");
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