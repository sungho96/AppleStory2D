using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스가 바라보는 방향으로 지면 얼음 가시를 순차 생성합니다.
/// </summary>
public class GoblinBossIceWaveAttack2D : MonoBehaviour
{
    [Header("Attack Timing")]
    [SerializeField] private float firstAttackDelay = 5.5f;
    [SerializeField] private float warningDuration = 0.65f;
    [SerializeField] private float spikeInterval = 0.0425f;
    [SerializeField] private float secondPhaseRepeatDelay = 0.5f;
    [SerializeField] private float recoveryDuration = 0.45f;
    [SerializeField] private Vector2 cooldownRange = new Vector2(6.5f, 8f);

    [Header("Wave")]
    [SerializeField, Range(0.05f, 0.95f)] private float secondPhaseHpRatio = 0.5f;
    [SerializeField] private int spikeCount = 7;
    [SerializeField] private float firstSpikeDistance = 1.35f;
    [SerializeField] private float spikeSpacing = 0.65f;
    [SerializeField] private float spikeScale = 0.17f;
    [SerializeField] private float spikeGroundOffset = -0.13f;
    [SerializeField] private float spikeHoldDuration = 0.58f;
    [SerializeField] private float arenaWaveMinX = -13.6f;
    [SerializeField] private float arenaWaveMaxX = 13.6f;
    [SerializeField] private float surfaceExtraWidth = 4.4f;

    [Header("Damage")]
    [SerializeField] private int damage = 18;
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1.35f, 0.7f);
    [SerializeField] private float hitBoxGroundOffset = 0.34f;
    [SerializeField] private float knockbackX = 6f;
    [SerializeField] private float knockbackY = 4.5f;
    [SerializeField] private float slowMultiplier = 0.75f;
    [SerializeField] private float slowDuration = 2f;

    private Transform player;
    private GoblinHealth2D bossHealth;
    private GoblinBossCombatController2D bossCombat;
    private Sprite iceSpikeSprite;
    private Sprite warningSprite;
    private Sprite impactDustSprite;

    private void Awake()
    {
        bossHealth = GetComponent<GoblinHealth2D>();
        bossCombat = GetComponent<GoblinBossCombatController2D>();
        iceSpikeSprite = Resources.Load<Sprite>("Boss/GoblinBoss_IceSpikes");
        impactDustSprite = Resources.Load<Sprite>("Boss/GoblinBoss_ImpactDust");
        warningSprite = CreateCircleSprite(64);
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(firstAttackDelay);

        while (bossHealth == null || !bossHealth.IsDead)
        {
            FindPlayer();
            yield return WaitForOtherAttack();

            if (player != null && iceSpikeSprite != null)
                yield return PerformIceWave();

            yield return new WaitForSeconds(Random.Range(cooldownRange.x, cooldownRange.y));
        }
    }

    private IEnumerator PerformIceWave()
    {
        float direction = player.position.x < transform.position.x ? -1f : 1f;
        bool isSecondPhase = IsSecondPhase();
        List<Vector2> points = CreateWavePoints();
        if (points.Count == 0)
            yield break;

        List<GameObject> warnings = new List<GameObject>(points.Count);

        for (int i = 0; i < points.Count; i++)
            warnings.Add(CreateWarning(points[i], i));

        // [얼음 파도 추가] 메테오와 같은 시전 자세를 사용하되 푸른 바닥 경고로 공격 종류를 구분합니다.
        if (bossCombat != null)
        {
            // [Codex IceWave Repeat] 2페이즈는 전체 바닥 웨이브를 한 번 더 깔아서 점프-착지-점프 리듬을 요구합니다.
            float singleWaveDuration = spikeInterval * Mathf.Max(0, points.Count - 1) + spikeHoldDuration;
            float repeatDuration = isSecondPhase ? Mathf.Max(0f, secondPhaseRepeatDelay) + singleWaveDuration : 0f;
            float attackLockDuration = warningDuration + singleWaveDuration + repeatDuration + recoveryDuration;
            bossCombat.BeginIceCast(attackLockDuration);
        }

        float timer = 0f;
        while (timer < warningDuration)
        {
            timer += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(timer * 16f) * 0.1f;
            for (int i = 0; i < warnings.Count; i++)
            {
                if (warnings[i] != null)
                    warnings[i].transform.localScale = new Vector3(1.25f * pulse, 0.18f * pulse, 1f);
            }
            yield return null;
        }

        yield return SpawnWave(points, direction, warnings);

        if (isSecondPhase)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, secondPhaseRepeatDelay));
            yield return SpawnWave(points, direction, null);
        }

        yield return new WaitForSeconds(spikeHoldDuration + recoveryDuration);
    }

    private IEnumerator SpawnWave(List<Vector2> points, float direction, List<GameObject> warnings)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (warnings != null && warnings[i] != null)
                Destroy(warnings[i]);

            StartCoroutine(ShowSpike(points[i], direction));
            yield return new WaitForSeconds(spikeInterval);
        }
    }

    private List<Vector2> CreateWavePoints()
    {
        List<Vector2> points = new List<Vector2>(spikeCount);
        bool isSecondPhase = IsSecondPhase();

        // [Codex IceWave Phase Floor] 1페이즈는 메인 바닥 전체, 2페이즈는 메인 바닥과 모든 공중 발판 전체를 공격합니다.
        AddFloorWavePoints(points, "BossArena_MainFloor");
        if (isSecondPhase)
            AddSidePlatformWavePoints(points);

        return points;
    }

    private bool IsSecondPhase()
    {
        return bossHealth != null && bossHealth.HpRatio <= secondPhaseHpRatio;
    }

    private void AddSidePlatformWavePoints(List<Vector2> points)
    {
        GameObject platformRoot = GameObject.Find("BossArena_SidePlatforms");
        if (platformRoot == null)
            return;

        Collider2D[] platformColliders = platformRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < platformColliders.Length; i++)
        {
            if (platformColliders[i] == null || !platformColliders[i].enabled)
                continue;

            // [Codex IceWave Phase 2 Platform] 공중 발판은 발판 위치에 딱 맞게 생성해서 시각적으로 어긋나 보이지 않게 합니다.
            AddColliderSurfacePoints(points, platformColliders[i], 0f);
        }
    }

    private void AddFloorWavePoints(List<Vector2> points, string floorName)
    {
        GameObject floor = GameObject.Find(floorName);
        Collider2D floorCollider = floor != null ? floor.GetComponent<Collider2D>() : null;
        if (floorCollider != null && floorCollider.enabled)
            AddColliderSurfacePoints(points, floorCollider, surfaceExtraWidth);
    }

    private void AddColliderSurfacePoints(List<Vector2> points, Collider2D surfaceCollider, float extraWidth)
    {
        Bounds bounds = surfaceCollider.bounds;
        // [Codex IceWave Range] 카메라 밖까지 길게 깔리도록 각 바닥의 좌우 생성 범위를 추가로 넓힙니다.
        float minX = Mathf.Max(bounds.min.x - extraWidth, arenaWaveMinX);
        float maxX = Mathf.Min(bounds.max.x + extraWidth, arenaWaveMaxX);
        if (maxX < minX)
            return;

        float startX = minX + firstSpikeDistance;
        for (float x = startX; x <= maxX; x += Mathf.Max(0.1f, spikeSpacing))
            points.Add(new Vector2(x, bounds.max.y));
    }

    private bool TryFindPlayerGroundY(out float groundY)
    {
        int groundLayer = LayerMask.GetMask("Ground");
        Vector2 origin = new Vector2(player.position.x, player.position.y + 0.35f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 4f, groundLayer);
        groundY = hit.collider != null ? hit.point.y : 0f;
        return hit.collider != null;
    }

    private bool TryFindGroundPoint(float targetX, float playerFloorY, out Vector2 groundPoint)
    {
        int groundLayer = LayerMask.GetMask("Ground");
        // [층 오인식 수정] 플레이어 발밑 층 바로 위에서 아래로만 검사해 2·3층 플랫폼을 먼저 맞히지 않게 합니다.
        Vector2 origin = new Vector2(targetX, playerFloorY + 0.65f);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 2.2f, groundLayer);
        // [공중 생성 수정] 실제 Ground 충돌이 없는 낭떠러지와 빈 공간에는 가시 위치를 만들지 않습니다.
        groundPoint = hit.collider != null ? hit.point : Vector2.zero;
        return hit.collider != null;
    }

    private GameObject CreateWarning(Vector2 point, int index)
    {
        GameObject warning = new GameObject("BossIce_Warning");
        warning.transform.position = new Vector3(point.x, point.y + 0.04f, 0f);
        warning.transform.localScale = new Vector3(1.25f, 0.18f, 1f);

        SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
        renderer.sprite = warningSprite;
        renderer.color = new Color(0.2f, 0.86f, 1f, Mathf.Lerp(0.75f, 0.42f, index / Mathf.Max(1f, spikeCount - 1f)));
        renderer.sortingOrder = 25;
        return warning;
    }

    private IEnumerator ShowSpike(Vector2 point, float direction)
    {
        GameObject spike = new GameObject("BossIce_Spike");
        // [가시 높이 보정] 이미지 하단 투명 여백만큼 내려 실제 그림의 밑면을 Ground에 붙입니다.
        spike.transform.position = new Vector3(point.x, point.y + spikeGroundOffset, 0f);
        spike.transform.localScale = new Vector3(spikeScale * direction, 0.01f, 1f);

        SpriteRenderer renderer = spike.AddComponent<SpriteRenderer>();
        renderer.sprite = iceSpikeSprite;
        renderer.sortingOrder = 30;

        const float riseDuration = 0.12f;
        float timer = 0f;
        while (timer < riseDuration)
        {
            timer += Time.deltaTime;
            float ratio = Mathf.Clamp01(timer / riseDuration);
            float eased = 1f - (1f - ratio) * (1f - ratio);
            spike.transform.localScale = new Vector3(spikeScale * direction, spikeScale * eased, 1f);
            yield return null;
        }

        // [얼음 충격 이펙트] 가시가 완전히 솟는 순간 냉기 먼지와 작은 얼음 파편을 함께 흩뿌립니다.
        StartCoroutine(ShowEruptionEffect(point));
        ApplyDamage(point);
        yield return new WaitForSeconds(spikeHoldDuration);

        timer = 0f;
        const float disappearDuration = 0.2f;
        while (timer < disappearDuration)
        {
            timer += Time.deltaTime;
            float ratio = Mathf.Clamp01(timer / disappearDuration);
            spike.transform.localScale = new Vector3(spikeScale * direction, spikeScale * (1f - ratio), 1f);
            Color color = renderer.color;
            color.a = 1f - ratio;
            renderer.color = color;
            yield return null;
        }

        Destroy(spike);
    }

    private IEnumerator ShowEruptionEffect(Vector2 point)
    {
        // [얼음 지면 충격광] 땅을 가르며 퍼지는 청백색 섬광을 가장 아래 레이어에 표시합니다.
        GameObject groundFlash = new GameObject("BossIce_GroundFlash");
        groundFlash.transform.position = point + Vector2.up * 0.035f;
        groundFlash.transform.localScale = new Vector3(0.35f, 0.08f, 1f);

        SpriteRenderer flashRenderer = groundFlash.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = warningSprite;
        flashRenderer.color = new Color(0.72f, 0.97f, 1f, 0.9f);
        flashRenderer.sortingOrder = 31;

        // [얼음 폭발 오라] 순간적으로 밝은 코어와 푸른 외곽광이 커지며 시선이 가시 중심에 모이게 합니다.
        GameObject burstGlow = new GameObject("BossIce_BurstGlow");
        burstGlow.transform.position = point + Vector2.up * 0.42f;
        burstGlow.transform.localScale = Vector3.one * 0.16f;

        SpriteRenderer glowRenderer = burstGlow.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = warningSprite;
        glowRenderer.color = new Color(0.22f, 0.78f, 1f, 0.82f);
        glowRenderer.sortingOrder = 34;

        GameObject coreFlash = new GameObject("BossIce_CoreFlash");
        coreFlash.transform.position = point + Vector2.up * 0.38f;
        coreFlash.transform.localScale = new Vector3(0.14f, 0.22f, 1f);

        SpriteRenderer coreRenderer = coreFlash.AddComponent<SpriteRenderer>();
        coreRenderer.sprite = warningSprite;
        coreRenderer.color = new Color(0.94f, 1f, 1f, 1f);
        coreRenderer.sortingOrder = 35;

        List<GameObject> fragments = new List<GameObject>();
        List<Vector2> velocities = new List<Vector2>();

        for (int i = 0; i < 8; i++)
        {
            GameObject fragment = new GameObject("BossIce_Fragment");
            fragment.transform.position = point + Vector2.up * 0.12f;
            fragment.transform.localScale = new Vector3(0.085f, 0.19f, 1f) * Random.Range(0.7f, 1.25f);
            fragment.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-45f, 45f));

            SpriteRenderer fragmentRenderer = fragment.AddComponent<SpriteRenderer>();
            fragmentRenderer.sprite = warningSprite;
            fragmentRenderer.color = i % 2 == 0
                ? new Color(0.72f, 0.96f, 1f, 0.95f)
                : new Color(0.12f, 0.62f, 1f, 0.95f);
            fragmentRenderer.sortingOrder = 33;

            fragments.Add(fragment);
            velocities.Add(new Vector2(Random.Range(-5.2f, 5.2f), Random.Range(3.1f, 6.5f)));
        }

        List<GameObject> dustPuffs = new List<GameObject>();
        if (impactDustSprite != null)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject dust = new GameObject("BossIce_FrostDust");
                float side = i - 1f;
                dust.transform.position = point + new Vector2(side * 0.24f, 0.07f + (i == 1 ? 0.08f : 0f));
                dust.transform.localScale = Vector3.one * (i == 1 ? 0.13f : 0.1f);

                SpriteRenderer dustRenderer = dust.AddComponent<SpriteRenderer>();
                dustRenderer.sprite = impactDustSprite;
                dustRenderer.color = i == 1
                    ? new Color(0.78f, 0.97f, 1f, 0.72f)
                    : new Color(0.44f, 0.82f, 1f, 0.64f);
                dustRenderer.sortingOrder = 32;
                dustPuffs.Add(dust);
            }
        }

        float timer = 0f;
        const float duration = 0.52f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float ratio = Mathf.Clamp01(timer / duration);

            groundFlash.transform.localScale = Vector3.Lerp(
                new Vector3(0.35f, 0.08f, 1f),
                new Vector3(2.15f, 0.42f, 1f),
                ratio);
            flashRenderer.color = new Color(0.72f, 0.97f, 1f, 1f * (1f - ratio));

            float glowFade = 1f - Mathf.SmoothStep(0f, 1f, ratio);
            burstGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.16f, 1.05f, ratio);
            glowRenderer.color = new Color(0.22f, 0.78f, 1f, 0.82f * glowFade);
            coreFlash.transform.localScale = Vector3.Lerp(
                new Vector3(0.14f, 0.22f, 1f),
                new Vector3(0.56f, 0.76f, 1f),
                Mathf.Clamp01(ratio * 2.4f));
            coreRenderer.color = new Color(0.94f, 1f, 1f, Mathf.Clamp01(1f - ratio * 2.2f));

            for (int i = 0; i < fragments.Count; i++)
            {
                Vector2 velocity = velocities[i];
                fragments[i].transform.position += (Vector3)(velocity * Time.deltaTime);
                velocity.y -= 10f * Time.deltaTime;
                velocities[i] = velocity;
                fragments[i].transform.Rotate(0f, 0f, 520f * Time.deltaTime * (i % 2 == 0 ? 1f : -1f));

                SpriteRenderer fragmentRenderer = fragments[i].GetComponent<SpriteRenderer>();
                Color color = fragmentRenderer.color;
                color.a = 0.95f * (1f - ratio);
                fragmentRenderer.color = color;
            }

            for (int i = 0; i < dustPuffs.Count; i++)
            {
                float side = i - 1f;
                dustPuffs[i].transform.position += new Vector3(side * 0.62f * Time.deltaTime, 0.46f * Time.deltaTime, 0f);
                float startScale = i == 1 ? 0.13f : 0.1f;
                float endScale = i == 1 ? 0.46f : 0.38f;
                dustPuffs[i].transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, ratio);

                SpriteRenderer dustRenderer = dustPuffs[i].GetComponent<SpriteRenderer>();
                Color color = dustRenderer.color;
                float baseAlpha = i == 1 ? 0.72f : 0.64f;
                color.a = baseAlpha * (1f - ratio);
                dustRenderer.color = color;
            }

            yield return null;
        }

        Destroy(groundFlash);
        Destroy(burstGlow);
        Destroy(coreFlash);
        for (int i = 0; i < fragments.Count; i++)
            Destroy(fragments[i]);
        for (int i = 0; i < dustPuffs.Count; i++)
            Destroy(dustPuffs[i]);
    }

    private void ApplyDamage(Vector2 point)
    {
        // [Codex IceWave Phase 1] 낮은 가로 판정만 사용해서 정답 회피가 확실히 점프가 되도록 합니다.
        Vector2 hitCenter = point + Vector2.up * hitBoxGroundOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitCenter, hitBoxSize, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth2D playerHealth = hits[i].GetComponentInParent<PlayerHealth2D>();
            if (playerHealth == null)
                continue;

            float direction = playerHealth.transform.position.x >= point.x ? 1f : -1f;
            int hpBeforeDamage = playerHealth.CurrentHp;
            playerHealth.TakeDamage(damage, new Vector2(direction * knockbackX, knockbackY));

            // [얼음 둔화 적용] 무적 상태로 피해가 무시된 경우에는 둔화도 걸리지 않습니다.
            if (playerHealth.CurrentHp < hpBeforeDamage && !playerHealth.IsDead)
            {
                PlayerIceSlow2D iceSlow = playerHealth.GetComponent<PlayerIceSlow2D>();
                if (iceSlow == null)
                    iceSlow = playerHealth.gameObject.AddComponent<PlayerIceSlow2D>();
                iceSlow.ApplySlow(slowMultiplier, slowDuration);
            }
            break;
        }
    }

    private IEnumerator WaitForOtherAttack()
    {
        while (bossCombat != null && bossCombat.IsCasting)
            yield return null;
    }

    private void FindPlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "BossIceWarning_RuntimeSprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.43f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / 2f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
