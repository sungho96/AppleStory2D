using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PlayerOneWayPlatformCollision2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private CapsuleCollider2D playerCollider;

    [Header("One Way Landing")]
    [SerializeField] private LayerMask platformSearchMask = ~0;
    [SerializeField] private float landingFeetTolerance = 0.08f;
    [SerializeField] private float horizontalEdgeInset = 0.05f;
    [SerializeField] private float upwardVelocityThreshold = 0.01f;
    [SerializeField] private Vector2 searchPadding = new Vector2(0.15f, 0.35f);

    private readonly Collider2D[] platformHits = new Collider2D[16];
    private readonly HashSet<Collider2D> ignoredPlatforms = new HashSet<Collider2D>();
    private readonly List<Collider2D> ignoredSnapshot = new List<Collider2D>();

    private void Awake()
    {
        CacheRefs();
    }

    private void FixedUpdate()
    {
        if (playerRigidbody == null || playerCollider == null)
            return;

        Bounds playerBounds = playerCollider.bounds;
        Vector2 searchCenter = playerBounds.center;
        Vector2 searchSize = new Vector2(
            playerBounds.size.x + searchPadding.x * 2f,
            playerBounds.size.y + searchPadding.y * 2f);

        int hitCount = Physics2D.OverlapBoxNonAlloc(
            searchCenter,
            searchSize,
            0f,
            platformHits,
            platformSearchMask);

        ignoredSnapshot.Clear();
        foreach (Collider2D ignored in ignoredPlatforms)
            ignoredSnapshot.Add(ignored);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D platformCollider = platformHits[i];
            if (!IsOneWayPlatformCollider(platformCollider))
                continue;

            bool canLand = CanLandOnPlatform(playerBounds, platformCollider.bounds);
            Physics2D.IgnoreCollision(playerCollider, platformCollider, !canLand);

            if (canLand)
                ignoredPlatforms.Remove(platformCollider);
            else
                ignoredPlatforms.Add(platformCollider);

            ignoredSnapshot.Remove(platformCollider);
        }

        for (int i = 0; i < ignoredSnapshot.Count; i++)
        {
            Collider2D platformCollider = ignoredSnapshot[i];
            if (platformCollider == null)
            {
                ignoredPlatforms.Remove(platformCollider);
                continue;
            }

            if (CanLandOnPlatform(playerBounds, platformCollider.bounds))
            {
                Physics2D.IgnoreCollision(playerCollider, platformCollider, false);
                ignoredPlatforms.Remove(platformCollider);
            }
        }
    }

    private void OnDisable()
    {
        RestoreIgnoredPlatforms();
    }

    private void OnDestroy()
    {
        RestoreIgnoredPlatforms();
    }

    private void CacheRefs()
    {
        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody2D>();
        if (playerCollider == null)
            playerCollider = GetComponent<CapsuleCollider2D>();
    }

    private bool CanLandOnPlatform(Bounds playerBounds, Bounds platformBounds)
    {
        if (playerRigidbody.linearVelocity.y > upwardVelocityThreshold)
            return false;

        float feetY = playerBounds.min.y;
        float platformTopY = platformBounds.max.y;
        if (feetY < platformTopY - Mathf.Max(0f, landingFeetTolerance))
            return false;

        float playerCenterX = playerBounds.center.x;
        float minX = platformBounds.min.x + Mathf.Max(0f, horizontalEdgeInset);
        float maxX = platformBounds.max.x - Mathf.Max(0f, horizontalEdgeInset);
        return playerCenterX >= minX && playerCenterX <= maxX;
    }

    private static bool IsOneWayPlatformCollider(Collider2D platformCollider)
    {
        if (platformCollider == null || platformCollider.isTrigger || !platformCollider.enabled)
            return false;

        PlatformEffector2D effector = platformCollider.GetComponent<PlatformEffector2D>();
        return effector != null && effector.enabled && effector.useOneWay;
    }

    private void RestoreIgnoredPlatforms()
    {
        if (playerCollider == null)
            return;

        ignoredSnapshot.Clear();
        foreach (Collider2D ignored in ignoredPlatforms)
            ignoredSnapshot.Add(ignored);

        for (int i = 0; i < ignoredSnapshot.Count; i++)
        {
            if (ignoredSnapshot[i] != null)
                Physics2D.IgnoreCollision(playerCollider, ignoredSnapshot[i], false);
        }

        ignoredPlatforms.Clear();
    }
}
