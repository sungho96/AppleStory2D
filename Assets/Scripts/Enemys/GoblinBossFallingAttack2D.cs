using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 고블린 보스의 낙하 공격입니다.
/// 플레이어 위치 예고 -> 다중 낙하물 -> 범위 피해 순서로 실행합니다.
/// </summary>
public class GoblinBossFallingAttack2D : NetworkBehaviour
{
    [Header("Attack Timing")]
    [SerializeField] private float firstAttackDelay = 2.5f;
    [SerializeField] private float warningDuration = 1.15f;
    [SerializeField] private float fallDuration = 0.55f;
    [SerializeField] private float rockDisappearDuration = 0.22f;
    [SerializeField] private Vector2 cooldownRange = new Vector2(4.5f, 6f);

    [Header("Phase 2 - HP 50%")]
    [SerializeField] private float phaseTwoWarningDuration = 0.85f;
    [SerializeField] private float phaseTwoCooldownMultiplier = 0.6f;
    [SerializeField] private int phaseTwoDamage = 30;

    [Header("Damage")]
    [SerializeField] private int damage = 20;
    [SerializeField] private float hitRadius = 0.9f;
    [SerializeField] private float knockbackX = 7f;
    [SerializeField] private float knockbackY = 5f;

    [Header("Arena")]
    [SerializeField] private float minTargetX = -9f;
    [SerializeField] private float maxTargetX = 9f;
    [SerializeField] private float spawnHeight = 7.5f;

    [Header("Meteor Safe Zone")]
    [SerializeField] private bool hardMode;
    [SerializeField] private float phaseOneSafeZoneWidth = 3.4f;
    [SerializeField] private float phaseTwoSafeZoneWidth = 2.15f;
    [SerializeField] private float hardSafeZoneWidth = 1.65f;
    [SerializeField] private float hardWarningDuration = 0.65f;
    [SerializeField] private int extraFloorMeteors = 3;
    [SerializeField] private int extraPlatformMeteors = 2;
    [SerializeField] private float meteorWarningWidth = 1.25f;
    [SerializeField] private int phaseOnePlayerNearbyMeteorCount = 4;
    [SerializeField] private int phaseTwoPlayerNearbyMeteorCount = 7;
    [SerializeField] private float phaseOnePlayerMeteorSpacing = 1.35f;
    [SerializeField] private float phaseTwoPlayerMeteorSpacing = 1.05f;
    [SerializeField] private float safeZoneMinDistanceFromPlayer = 4.2f;

    [Header("Network Skill Debug")]
    [SerializeField] private bool enableSkillVfxDebugLog = true;

    private Transform player;
    private GoblinHealth2D bossHealth;
    private GoblinBossCombatController2D bossCombat;
    private Sprite warningSprite;
    private Sprite fallingSprite;
    private Sprite impactDustSprite;

    private void Awake()
    {
        bossHealth = GetComponent<GoblinHealth2D>();
        bossCombat = GetComponent<GoblinBossCombatController2D>();
        warningSprite = CreateCircleSprite(64, 0.86f);
        fallingSprite = Resources.Load<Sprite>("Boss/GoblinBoss_FallingRock");
        impactDustSprite = Resources.Load<Sprite>("Boss/GoblinBoss_ImpactDust");

        // [보스 낙하 공격 이미지] 에셋 로드 실패 시에도 공격 기능은 확인할 수 있게 임시 원을 사용합니다.
        if (fallingSprite == null)
            fallingSprite = CreateCircleSprite(48, 0.92f);

        // [얼음 파도 추가] 씬과 프리팹 참조를 건드리지 않고 보스에 얼음 공격을 런타임으로 연결합니다.
        if (GetComponent<GoblinBossIceWaveAttack2D>() == null)
            gameObject.AddComponent<GoblinBossIceWaveAttack2D>();
    }

    private IEnumerator Start()
    {
        if (IsNetworkClientOnly())
            yield break;

        yield return new WaitForSeconds(firstAttackDelay);

        while (bossHealth == null || !bossHealth.IsDead)
        {
            FindPlayer();
            if (player != null)
                yield return PerformFallingAttack();

            float cooldownMultiplier = IsPhaseTwo() ? phaseTwoCooldownMultiplier : 1f;
            yield return new WaitForSeconds(Random.Range(cooldownRange.x, cooldownRange.y) * cooldownMultiplier);
        }
    }

    private IEnumerator PerformFallingAttack()
    {
        if (bossCombat == null)
            bossCombat = GetComponent<GoblinBossCombatController2D>();

        // [공격 겹침 방지] 얼음 파도 시전 중에는 메테오 경고가 동시에 시작되지 않게 기다립니다.
        while (bossCombat != null && bossCombat.IsCasting)
            yield return null;

        bool phaseTwo = IsPhaseTwo();
        int attackDamage = phaseTwo ? phaseTwoDamage : damage;
        float currentWarningDuration = hardMode ? hardWarningDuration : (phaseTwo ? phaseTwoWarningDuration : warningDuration);
        List<MeteorArea> meteorAreas = CreateMeteorAreas(phaseTwo);
        List<Vector2> impactPoints = new List<Vector2>();
        List<GameObject> dangerWarnings = new List<GameObject>(meteorAreas.Count);

        LogSkillVfxDebug(
            "MeteorStart",
            $"phaseTwo={phaseTwo} hardMode={hardMode} warningDuration={currentWarningDuration:F3}s damage={attackDamage}");

        for (int i = 0; i < meteorAreas.Count; i++)
        {
            List<Vector2> areaImpactPoints = new List<Vector2>();
            AddPlayerNearbyImpacts(areaImpactPoints, meteorAreas[i], phaseTwo);
            AddDistributedImpactPoints(areaImpactPoints, meteorAreas[i].SafeZones, meteorAreas[i].Floor);
            impactPoints.AddRange(areaImpactPoints);
        }

        for (int i = 0; i < impactPoints.Count; i++)
            dangerWarnings.Add(CreateMeteorWarning(impactPoints[i]));

        LogSkillVfxDebug(
            "MeteorWarningsCreated",
            $"count={dangerWarnings.Count} firstPoint={(impactPoints.Count > 0 ? impactPoints[0].ToString() : "none")}");

        if (IsServer && IsSpawned)
            PlayMeteorVisualClientRpc(impactPoints.ToArray(), currentWarningDuration);

        // [보스 시전 연결] 바닥 예고가 시작되는 순간 보스가 이동을 멈추고 스킬 동작을 재생합니다.
        if (bossCombat != null)
            bossCombat.BeginFallingCast(currentWarningDuration);

        // [보스 낙하 공격 페이즈] 모든 위치를 먼저 알려준 뒤 돌을 짧은 간격으로 순차 낙하시킵니다.
        float timer = 0f;
        while (timer < currentWarningDuration)
        {
            timer += Time.deltaTime;
            float warningAlphaPulse = 0.82f + Mathf.Sin(timer * 14f) * 0.18f;
            for (int i = 0; i < dangerWarnings.Count; i++)
            {
                if (dangerWarnings[i] != null)
                    SetWarningAlpha(dangerWarnings[i], (hardMode ? 0.72f : 0.58f) * warningAlphaPulse);
            }
            yield return null;
        }

        for (int i = 0; i < impactPoints.Count; i++)
        {
            // [Codex Meteor Safe Zone] 제한시간이 끝나면 위험지역 전체가 거의 동시에 떨어져 안전구역 선택이 정답이 되게 합니다.
            StartCoroutine(DropRock(impactPoints[i], null, attackDamage));
        }

        LogSkillVfxDebug("MeteorDropStart", $"count={impactPoints.Count}");

        yield return new WaitForSeconds(fallDuration);
        ApplyMeteorImpactDamage(impactPoints, attackDamage);
        for (int i = 0; i < dangerWarnings.Count; i++)
        {
            if (dangerWarnings[i] != null)
                Destroy(dangerWarnings[i]);
        }

        yield return new WaitForSeconds(fallDuration + 0.2f);
    }

    private IEnumerator DropRock(Vector2 impactPoint, GameObject warning, int attackDamage)
    {
        GameObject fallingObject = CreateFallingObject(impactPoint);
        float meteorDirection = impactPoint.x < 0f ? 1f : -1f;
        Vector3 start = new Vector3(
            impactPoint.x - meteorDirection * spawnHeight,
            impactPoint.y + spawnHeight,
            0f);
        float meteorAngle = meteorDirection > 0f ? -45f : 45f;
        fallingObject.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 0f, meteorAngle));
        SpriteRenderer fallingRenderer = fallingObject.GetComponent<SpriteRenderer>();
        float rockHalfHeight = fallingRenderer != null ? fallingRenderer.bounds.extents.y : 0.42f;
        // [보스 낙하 착지] 돌 전체 높이의 아래쪽 약 30%가 바닥에 깊게 박히도록 중심을 낮춥니다.
        Vector3 end = new Vector3(impactPoint.x, impactPoint.y + rockHalfHeight * 0.4f, 0f);

        float timer = 0f;
        while (timer < fallDuration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / fallDuration);
            float accelerated = normalized * normalized;
            if (fallingObject != null)
            {
                fallingObject.transform.position = Vector3.Lerp(start, end, accelerated);
                // [보스 메테오 낙하] 45도 대각선 궤도를 따라 내려오면서 한 바퀴 반 회전합니다.
                float spinAngle = normalized * 540f * meteorDirection;
                fallingObject.transform.rotation = Quaternion.Euler(0f, 0f, meteorAngle + spinAngle);
            }
            yield return null;
        }

        if (fallingObject != null)
        {
            float finalAngle = meteorAngle + 540f * meteorDirection;
            fallingObject.transform.SetPositionAndRotation(end, Quaternion.Euler(0f, 0f, finalAngle));
        }

        // [Codex Meteor Safe Zone] 피해는 개별 바위가 아니라 최종 안전구역 판정으로 1회만 처리합니다.
        yield return ShowImpactFlash(impactPoint);

        if (warning != null)
            Destroy(warning);
        if (fallingObject != null)
            yield return FadeOutFallingObject(fallingObject);
    }

    [ClientRpc]
    private void PlayMeteorVisualClientRpc(Vector2[] impactPoints, float currentWarningDuration)
    {
        if (IsServer)
            return;

        // [Codex Boss Skill Sync] 서버가 계산한 메테오 위치를 클라이언트에서 판정 없이 이펙트만 재생합니다.
        StartCoroutine(CoPlayMeteorVisualOnly(impactPoints, currentWarningDuration));
    }

    private IEnumerator CoPlayMeteorVisualOnly(Vector2[] impactPoints, float currentWarningDuration)
    {
        List<GameObject> dangerWarnings = new List<GameObject>(impactPoints.Length);
        for (int i = 0; i < impactPoints.Length; i++)
            dangerWarnings.Add(CreateMeteorWarning(impactPoints[i]));

        LogSkillVfxDebug("MeteorClientReplayStart", $"count={impactPoints.Length} warningDuration={currentWarningDuration:F3}s");

        float timer = 0f;
        while (timer < currentWarningDuration)
        {
            timer += Time.deltaTime;
            float warningAlphaPulse = 0.82f + Mathf.Sin(timer * 14f) * 0.18f;
            for (int i = 0; i < dangerWarnings.Count; i++)
            {
                if (dangerWarnings[i] != null)
                    SetWarningAlpha(dangerWarnings[i], (hardMode ? 0.72f : 0.58f) * warningAlphaPulse);
            }
            yield return null;
        }

        for (int i = 0; i < impactPoints.Length; i++)
            StartCoroutine(DropRockVisualOnly(impactPoints[i]));

        LogSkillVfxDebug("MeteorClientReplayDropStart", $"count={impactPoints.Length}");

        yield return new WaitForSeconds(fallDuration);
        for (int i = 0; i < dangerWarnings.Count; i++)
        {
            if (dangerWarnings[i] != null)
                Destroy(dangerWarnings[i]);
        }
    }

    private IEnumerator DropRockVisualOnly(Vector2 impactPoint)
    {
        GameObject fallingObject = CreateFallingObject(impactPoint);
        float meteorDirection = impactPoint.x < 0f ? 1f : -1f;
        Vector3 start = new Vector3(
            impactPoint.x - meteorDirection * spawnHeight,
            impactPoint.y + spawnHeight,
            0f);
        float meteorAngle = meteorDirection > 0f ? -45f : 45f;
        fallingObject.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 0f, meteorAngle));
        SpriteRenderer fallingRenderer = fallingObject.GetComponent<SpriteRenderer>();
        float rockHalfHeight = fallingRenderer != null ? fallingRenderer.bounds.extents.y : 0.42f;
        Vector3 end = new Vector3(impactPoint.x, impactPoint.y + rockHalfHeight * 0.4f, 0f);

        float timer = 0f;
        while (timer < fallDuration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / fallDuration);
            float accelerated = normalized * normalized;
            if (fallingObject != null)
            {
                fallingObject.transform.position = Vector3.Lerp(start, end, accelerated);
                float spinAngle = normalized * 540f * meteorDirection;
                fallingObject.transform.rotation = Quaternion.Euler(0f, 0f, meteorAngle + spinAngle);
            }
            yield return null;
        }

        if (fallingObject != null)
        {
            float finalAngle = meteorAngle + 540f * meteorDirection;
            fallingObject.transform.SetPositionAndRotation(end, Quaternion.Euler(0f, 0f, finalAngle));
        }

        yield return ShowImpactFlash(impactPoint);

        if (fallingObject != null)
            yield return FadeOutFallingObject(fallingObject);
    }

    private IEnumerator FadeOutFallingObject(GameObject fallingObject)
    {
        // [메테오 소멸 연출] 아이스웨이브처럼 바로 삭제하지 않고 작아지며 투명해지게 만듭니다.
        SpriteRenderer renderer = fallingObject.GetComponent<SpriteRenderer>();
        Vector3 startScale = fallingObject.transform.localScale;
        Color startColor = renderer != null ? renderer.color : Color.white;

        float disappearDuration = Mathf.Max(0.01f, rockDisappearDuration);
        float timer = 0f;
        while (timer < disappearDuration)
        {
            timer += Time.deltaTime;
            float ratio = Mathf.Clamp01(timer / disappearDuration);
            float eased = Mathf.SmoothStep(0f, 1f, ratio);

            fallingObject.transform.localScale = Vector3.Lerp(startScale, startScale * 0.45f, eased);
            if (renderer != null)
            {
                Color color = startColor;
                color.a = 1f - eased;
                renderer.color = color;
            }

            yield return null;
        }

        Destroy(fallingObject);
    }

    private List<MeteorArea> CreateMeteorAreas(bool phaseTwo)
    {
        List<MeteorArea> areas = new List<MeteorArea>();
        FloorArea playerFloor = FindTargetFloorArea();
        areas.Add(new MeteorArea(playerFloor, CreateSafeZones(phaseTwo, playerFloor)));

        if (!phaseTwo)
            return areas;

        GameObject platformRoot = GameObject.Find("BossArena_SidePlatforms");
        if (platformRoot == null)
            return areas;

        Collider2D[] platformColliders = platformRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < platformColliders.Length; i++)
        {
            if (platformColliders[i] == null || !platformColliders[i].enabled)
                continue;

            FloorArea platformArea = new FloorArea(platformColliders[i].bounds, true);
            if (ContainsSimilarFloor(areas, platformArea))
                continue;

            // [Codex Meteor Phase 2 Platforms] 2페이즈는 공중 발판에도 같은 규칙의 경고/안전지대를 만들어 위층 대피도 압박합니다.
            areas.Add(new MeteorArea(platformArea, CreateSafeZones(phaseTwo, platformArea)));
        }

        return areas;
    }

    private List<SafeZone> CreateSafeZones(bool phaseTwo, FloorArea targetFloor)
    {
        int safeZoneCount = hardMode ? 1 : 2;
        if (targetFloor.IsPlatform)
            safeZoneCount = 1;

        float width = hardMode ? hardSafeZoneWidth : (phaseTwo ? phaseTwoSafeZoneWidth : phaseOneSafeZoneWidth);
        float halfWidth = Mathf.Max(0.4f, width * 0.5f);
        halfWidth = Mathf.Min(halfWidth, targetFloor.Width * 0.45f);
        float playerX = Mathf.Clamp(player.position.x, targetFloor.MinX, targetFloor.MaxX);
        float leftCenter = targetFloor.MinX + halfWidth;
        float rightCenter = targetFloor.MaxX - halfWidth;
        List<SafeZone> safeZones = new List<SafeZone>(safeZoneCount);

        // [Codex Meteor Safe Zone] 플레이어 위치에서 떨어진 안전지대를 먼저 보여줘서 걷기보다 대시 판단이 중요해지게 합니다.
        if (safeZoneCount == 1)
        {
            float center = Mathf.Abs(playerX - leftCenter) > Mathf.Abs(playerX - rightCenter) ? leftCenter : rightCenter;
            if (Mathf.Abs(playerX - center) < safeZoneMinDistanceFromPlayer)
                center = center < playerX ? targetFloor.MinX + halfWidth : targetFloor.MaxX - halfWidth;

            safeZones.Add(new SafeZone(center, halfWidth));
            return safeZones;
        }

        safeZones.Add(new SafeZone(leftCenter, halfWidth));
        safeZones.Add(new SafeZone(rightCenter, halfWidth));
        return safeZones;
    }

    private void AddDistributedImpactPoints(List<Vector2> points, List<SafeZone> safeZones, FloorArea targetFloor)
    {
        int meteorCount = Mathf.Max(0, targetFloor.IsPlatform ? extraPlatformMeteors : extraFloorMeteors);
        if (meteorCount <= 0)
            return;

        float spacing = targetFloor.Width / (meteorCount + 1f);

        for (int i = 0; i < meteorCount; i++)
        {
            float x = targetFloor.MinX + spacing * (i + 1f);
            if (IsInsideAnySafeZone(x, safeZones))
            {
                x = FindNearestDangerX(x, safeZones, targetFloor);
                if (IsInsideAnySafeZone(x, safeZones))
                    continue;
            }

            if (!TryResolveImpactX(points, targetFloor, x, out x))
                continue;

            points.Add(new Vector2(Mathf.Clamp(x, targetFloor.MinX, targetFloor.MaxX), targetFloor.TopY));
        }
    }

    private void AddPlayerNearbyImpacts(List<Vector2> impactPoints, MeteorArea meteorArea, bool phaseTwo)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || !meteorArea.Floor.Contains(players[i].transform.position))
                continue;

            float playerX = Mathf.Clamp(players[i].transform.position.x, meteorArea.Floor.MinX, meteorArea.Floor.MaxX);
            float x = playerX;
            if (!TryResolveImpactX(impactPoints, meteorArea.Floor, x, out x))
                continue;

            // [Codex Meteor Player Start] 시전 시작 위치에는 항상 메테오를 찍어, 장판을 보고 이동해야 하는 압박을 만듭니다.
            impactPoints.Add(new Vector2(x, meteorArea.Floor.TopY));

            // [Codex Meteor Player Pressure] 1페이즈는 주변 4개, 2페이즈는 주변 7개로 압박 밀도를 올립니다.
            int nearbyCount = Mathf.Max(0, phaseTwo ? phaseTwoPlayerNearbyMeteorCount : phaseOnePlayerNearbyMeteorCount);
            float nearbySpacing = phaseTwo ? phaseTwoPlayerMeteorSpacing : phaseOnePlayerMeteorSpacing;
            for (int nearbyIndex = 0; nearbyIndex < nearbyCount; nearbyIndex++)
            {
                float side = nearbyIndex % 2 == 0 ? -1f : 1f;
                float ring = nearbyIndex / 2 + 1f;
                float preferredX = playerX + side * nearbySpacing * ring;
                x = Mathf.Clamp(preferredX, meteorArea.Floor.MinX, meteorArea.Floor.MaxX);
                if (!TryResolveImpactX(impactPoints, meteorArea.Floor, x, out x))
                    continue;

                impactPoints.Add(new Vector2(x, meteorArea.Floor.TopY));
            }
        }
    }

    private Vector2 FindImpactPoint(float targetX)
    {
        Vector2 rayOrigin = new Vector2(targetX, player.position.y + 0.5f);
        int groundLayer = LayerMask.GetMask("Ground");
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 20f, groundLayer);

        return hit.collider != null
            ? hit.point
            : new Vector2(targetX, -7.9f);
    }

    private FloorArea FindTargetFloorArea()
    {
        int groundLayer = LayerMask.GetMask("Ground");
        Vector2 rayOrigin = new Vector2(player.position.x, player.position.y + 0.45f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 4f, groundLayer);
        if (hit.collider != null)
            return new FloorArea(hit.collider.bounds, IsSidePlatformCollider(hit.collider));

        return new FloorArea(minTargetX, maxTargetX, FindImpactPoint(player.position.x).y, false);
    }

    private static bool ContainsSimilarFloor(List<MeteorArea> areas, FloorArea candidate)
    {
        for (int i = 0; i < areas.Count; i++)
        {
            FloorArea floor = areas[i].Floor;
            bool sameHeight = Mathf.Abs(floor.TopY - candidate.TopY) <= 0.08f;
            bool overlappingX = candidate.MinX <= floor.MaxX && candidate.MaxX >= floor.MinX;
            if (sameHeight && overlappingX)
                return true;
        }

        return false;
    }

    private bool TryResolveImpactX(List<Vector2> points, FloorArea targetFloor, float preferredX, out float resolvedX)
    {
        float minimumGap = Mathf.Max(0.35f, meteorWarningWidth * 0.9f);
        resolvedX = Mathf.Clamp(preferredX, targetFloor.MinX, targetFloor.MaxX);
        if (!ContainsNearbyImpact(points, targetFloor.TopY, resolvedX, minimumGap))
            return true;

        for (int step = 1; step <= 6; step++)
        {
            float offset = minimumGap * step;
            float leftX = Mathf.Clamp(preferredX - offset, targetFloor.MinX, targetFloor.MaxX);
            if (!ContainsNearbyImpact(points, targetFloor.TopY, leftX, minimumGap))
            {
                resolvedX = leftX;
                return true;
            }

            float rightX = Mathf.Clamp(preferredX + offset, targetFloor.MinX, targetFloor.MaxX);
            if (!ContainsNearbyImpact(points, targetFloor.TopY, rightX, minimumGap))
            {
                resolvedX = rightX;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNearbyImpact(List<Vector2> points, float floorY, float x, float minimumGap)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if (Mathf.Abs(points[i].y - floorY) <= 0.08f && Mathf.Abs(points[i].x - x) < minimumGap)
                return true;
        }

        return false;
    }

    private static float FindNearestDangerX(float preferredX, List<SafeZone> safeZones, FloorArea targetFloor)
    {
        float bestX = preferredX;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < safeZones.Count; i++)
        {
            float leftCandidate = Mathf.Clamp(safeZones[i].CenterX - safeZones[i].HalfWidth - 0.28f, targetFloor.MinX, targetFloor.MaxX);
            float rightCandidate = Mathf.Clamp(safeZones[i].CenterX + safeZones[i].HalfWidth + 0.28f, targetFloor.MinX, targetFloor.MaxX);
            float leftDistance = Mathf.Abs(preferredX - leftCandidate);
            float rightDistance = Mathf.Abs(preferredX - rightCandidate);

            if (leftDistance < bestDistance)
            {
                bestDistance = leftDistance;
                bestX = leftCandidate;
            }
            if (rightDistance < bestDistance)
            {
                bestDistance = rightDistance;
                bestX = rightCandidate;
            }
        }

        return bestX;
    }

    private static bool IsSidePlatformCollider(Collider2D collider)
    {
        Transform current = collider.transform;
        while (current != null)
        {
            if (current.name == "BossArena_SidePlatforms")
                return true;

            current = current.parent;
        }

        return false;
    }

    private GameObject CreateWarning(Vector2 impactPoint, bool phaseTwo)
    {
        GameObject warning = new GameObject("BossFall_Warning");
        warning.transform.position = new Vector3(impactPoint.x, impactPoint.y + 0.04f, 0f);
        warning.transform.localScale = new Vector3(2.2f, 0.38f, 1f);

        SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
        renderer.sprite = warningSprite;
        renderer.color = phaseTwo
            ? new Color(1f, 0.32f, 0.02f, 0.76f)
            : new Color(1f, 0.08f, 0.03f, 0.62f);
        renderer.sortingOrder = 25;
        return warning;
    }

    private GameObject CreateMeteorWarning(Vector2 impactPoint)
    {
        GameObject warning = new GameObject("BossFall_MeteorWarning");
        warning.transform.position = new Vector3(impactPoint.x, impactPoint.y + 0.03f, 0f);

        SpriteRenderer renderer = warning.AddComponent<SpriteRenderer>();
        renderer.sprite = warningSprite;
        renderer.color = hardMode
            ? new Color(1f, 0.03f, 0.01f, 0.72f)
            : new Color(1f, 0.12f, 0.03f, 0.58f);
        renderer.sortingOrder = 24;
        ApplyWarningSize(warning.transform, meteorWarningWidth, meteorWarningWidth * 0.28f);
        LogSkillVfxDebug("MeteorWarningCreated", $"pos={impactPoint}");
        return warning;
    }

    private GameObject CreateFallingObject(Vector2 impactPoint)
    {
        GameObject fallingObject = new GameObject("BossFall_Object");
        fallingObject.transform.position = new Vector3(impactPoint.x, impactPoint.y + spawnHeight, 0f);
        fallingObject.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

        SpriteRenderer renderer = fallingObject.AddComponent<SpriteRenderer>();
        renderer.sprite = fallingSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 30;
        LogSkillVfxDebug("MeteorObjectCreated", $"impact={impactPoint} spawn={fallingObject.transform.position}");
        return fallingObject;
    }

    private void ApplyImpactDamage(Vector2 impactPoint, int attackDamage)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPoint, hitRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth2D playerHealth = hits[i].GetComponentInParent<PlayerHealth2D>();
            if (playerHealth == null)
                continue;

            float direction = playerHealth.transform.position.x >= impactPoint.x ? 1f : -1f;
            playerHealth.TakeDamage(attackDamage, new Vector2(direction * knockbackX, knockbackY));
            break;
        }
    }

    private void ApplyDangerAreaDamage(List<MeteorArea> meteorAreas, int attackDamage)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            MeteorArea hitArea;
            if (!TryFindHitArea(players[i].transform.position, meteorAreas, out hitArea) ||
                IsInsideAnySafeZone(players[i].transform.position.x, hitArea.SafeZones))
                continue;

            PlayerHealth2D playerHealth = players[i].GetComponentInParent<PlayerHealth2D>();
            if (playerHealth == null)
                continue;

            float direction = players[i].transform.position.x >= hitArea.Floor.CenterX ? 1f : -1f;
            playerHealth.TakeDamage(attackDamage, new Vector2(direction * knockbackX, knockbackY));
        }
    }

    private void ApplyMeteorImpactDamage(List<Vector2> impactPoints, int attackDamage)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float halfWarningWidth = Mathf.Max(0.1f, meteorWarningWidth * 0.5f);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            Vector3 playerPosition = players[i].transform.position;
            for (int pointIndex = 0; pointIndex < impactPoints.Count; pointIndex++)
            {
                Vector2 impactPoint = impactPoints[pointIndex];
                bool sameFloor = Mathf.Abs(playerPosition.y - impactPoint.y) <= 2.2f;
                bool insideMarkedX = Mathf.Abs(playerPosition.x - impactPoint.x) <= halfWarningWidth;
                if (!sameFloor || !insideMarkedX)
                    continue;

                PlayerHealth2D playerHealth = players[i].GetComponentInParent<PlayerHealth2D>();
                if (playerHealth == null)
                    break;

                // [Codex Meteor Hit Match] 실제 피해 X 범위를 바닥 경고 표시 폭과 동일하게 맞춥니다.
                float direction = playerPosition.x >= impactPoint.x ? 1f : -1f;
                LogSkillVfxDebug(
                    "MeteorDamageApplied",
                    $"target={playerHealth.name} point={impactPoint} playerPos={playerPosition} damage={attackDamage}");
                playerHealth.TakeDamage(attackDamage, new Vector2(direction * knockbackX, knockbackY));
                break;
            }
        }
    }

    private void LogSkillVfxDebug(string eventName, string detail)
    {
        if (!enableSkillVfxDebugLog)
            return;

        NetworkManager manager = NetworkManager.Singleton;
        bool hasNetwork = manager != null && manager.IsListening;
        string role = hasNetwork
            ? (manager.IsServer ? "HostOrServer" : "Client")
            : "Offline";
        double serverTime = hasNetwork ? manager.ServerTime.Time : Time.timeAsDouble;

        // [Codex Boss Skill VFX Debug] 보스 스킬 이펙트가 어느 피어에서 언제 생성되는지 비교하기 위한 Console 로그입니다.
        Debug.Log(
            $"[BossSkillVfxDebug][Meteor][{eventName}] role={role} " +
            $"time={Time.time:F4}s serverTime={serverTime:F4}s bossPos={transform.position} {detail}");
    }

    private static bool TryFindHitArea(Vector3 playerPosition, List<MeteorArea> meteorAreas, out MeteorArea hitArea)
    {
        for (int i = 0; i < meteorAreas.Count; i++)
        {
            if (meteorAreas[i].Floor.Contains(playerPosition))
            {
                hitArea = meteorAreas[i];
                return true;
            }
        }

        hitArea = new MeteorArea();
        return false;
    }

    private static bool IsInsideAnySafeZone(float x, List<SafeZone> safeZones)
    {
        for (int i = 0; i < safeZones.Count; i++)
        {
            if (safeZones[i].Contains(x))
                return true;
        }

        return false;
    }

    private static void SetWarningAlpha(GameObject warning, float alpha)
    {
        SpriteRenderer renderer = warning.GetComponent<SpriteRenderer>();
        if (renderer == null)
            return;

        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }

    private void ApplyWarningSize(Transform warningTransform, float worldWidth, float worldHeight)
    {
        if (warningSprite == null)
            return;

        Vector2 spriteSize = warningSprite.bounds.size;
        warningTransform.localScale = new Vector3(
            worldWidth / Mathf.Max(0.01f, spriteSize.x),
            worldHeight / Mathf.Max(0.01f, spriteSize.y),
            1f);
    }

    private IEnumerator ShowImpactFlash(Vector2 impactPoint)
    {
        GameObject flash = new GameObject("BossFall_Impact");
        flash.transform.position = new Vector3(impactPoint.x, impactPoint.y + 0.08f, 0f);

        SpriteRenderer renderer = flash.AddComponent<SpriteRenderer>();
        renderer.sprite = warningSprite;
        renderer.color = new Color(1f, 0.55f, 0.08f, 0.8f);
        renderer.sortingOrder = 31;

        // [보스 낙하 충격] 작은 돌 파편과 먼지를 함께 튀겨 착지 순간을 더 역동적으로 보이게 합니다.
        List<GameObject> fragments = new List<GameObject>();
        List<Vector2> fragmentVelocities = new List<Vector2>();
        for (int i = 0; i < 7; i++)
        {
            GameObject fragment = CreateImpactParticle(
                "BossFall_Fragment",
                impactPoint + Vector2.up * 0.12f,
                new Color(0.3f, 0.2f, 0.1f, 1f),
                Random.Range(0.24f, 0.46f),
                33);
            fragments.Add(fragment);
            fragmentVelocities.Add(new Vector2(Random.Range(-6.2f, 6.2f), Random.Range(4.2f, 8.2f)));
        }

        List<GameObject> dustPuffs = new List<GameObject>();
        for (int i = 0; i < 3; i++)
        {
            Vector2 dustPosition = impactPoint + new Vector2((i - 1) * 0.7f, 0.18f);
            dustPuffs.Add(CreateImpactParticle(
                "BossFall_Dust",
                dustPosition,
                new Color(0.72f, 0.62f, 0.5f, 0.98f),
                0.42f,
                32,
                impactDustSprite));
        }

        float timer = 0f;
        const float duration = 0.58f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            flash.transform.localScale = Vector3.Lerp(new Vector3(0.8f, 0.28f, 1f), new Vector3(4.8f, 1.15f, 1f), normalized);
            renderer.color = new Color(1f, 0.55f, 0.08f, 0.8f * (1f - normalized));

            for (int i = 0; i < fragments.Count; i++)
            {
                Vector2 velocity = fragmentVelocities[i];
                fragments[i].transform.position += (Vector3)(velocity * Time.deltaTime);
                velocity.y -= 14f * Time.deltaTime;
                fragmentVelocities[i] = velocity;
                fragments[i].transform.Rotate(0f, 0f, 720f * Time.deltaTime);

                SpriteRenderer fragmentRenderer = fragments[i].GetComponent<SpriteRenderer>();
                Color fragmentColor = fragmentRenderer.color;
                fragmentColor.a = 1f - normalized;
                fragmentRenderer.color = fragmentColor;
            }

            for (int i = 0; i < dustPuffs.Count; i++)
            {
                float sideDirection = i - 1;
                dustPuffs[i].transform.position += new Vector3(sideDirection * 0.9f * Time.deltaTime, 0.7f * Time.deltaTime, 0f);
                dustPuffs[i].transform.localScale = Vector3.one * Mathf.Lerp(0.42f, 0.95f, normalized);

                SpriteRenderer dustRenderer = dustPuffs[i].GetComponent<SpriteRenderer>();
                Color dustColor = dustRenderer.color;
                // [보스 낙하 먼지] 대부분의 시간은 진하게 유지하고 마지막 구간에서만 자연스럽게 사라집니다.
                float dustFade = 1f - Mathf.InverseLerp(0.68f, 1f, normalized);
                dustColor.a = 0.98f * dustFade;
                dustRenderer.color = dustColor;
            }
            yield return null;
        }

        Destroy(flash);
        for (int i = 0; i < fragments.Count; i++)
            Destroy(fragments[i]);
        for (int i = 0; i < dustPuffs.Count; i++)
            Destroy(dustPuffs[i]);
    }

    private GameObject CreateImpactParticle(
        string objectName,
        Vector2 position,
        Color color,
        float scale,
        int sortingOrder,
        Sprite particleSprite = null)
    {
        GameObject particle = new GameObject(objectName);
        particle.transform.position = new Vector3(position.x, position.y, 0f);
        particle.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
        // [보스 낙하 먼지] 전용 카툰 먼지 이미지가 있으면 원형 임시 이미지 대신 사용합니다.
        renderer.sprite = particleSprite != null ? particleSprite : warningSprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return particle;
    }

    private void FindPlayer()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    private bool IsPhaseTwo()
    {
        return bossHealth != null && bossHealth.CurrentHp <= bossHealth.MaxHp * 0.5f;
    }

    private static bool IsNetworkClientOnly()
    {
        return NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsServer;
    }

    private static Sprite CreateCircleSprite(int size, float filledRadius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "BossFall_RuntimeSprite";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f * filledRadius;

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

    private struct SafeZone
    {
        public readonly float CenterX;
        public readonly float HalfWidth;
        public float Width => HalfWidth * 2f;

        public SafeZone(float centerX, float halfWidth)
        {
            CenterX = centerX;
            HalfWidth = halfWidth;
        }

        public bool Contains(float x)
        {
            return Mathf.Abs(x - CenterX) <= HalfWidth;
        }
    }

    private struct MeteorArea
    {
        public readonly FloorArea Floor;
        public readonly List<SafeZone> SafeZones;

        public MeteorArea(FloorArea floor, List<SafeZone> safeZones)
        {
            Floor = floor;
            SafeZones = safeZones;
        }
    }

    private struct FloorArea
    {
        public readonly float MinX;
        public readonly float MaxX;
        public readonly float TopY;
        public readonly bool IsPlatform;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float Width => Mathf.Max(0.1f, MaxX - MinX);

        public FloorArea(Bounds bounds, bool isPlatform)
        {
            MinX = bounds.min.x;
            MaxX = bounds.max.x;
            TopY = bounds.max.y;
            IsPlatform = isPlatform;
        }

        public FloorArea(float minX, float maxX, float topY, bool isPlatform)
        {
            MinX = minX;
            MaxX = maxX;
            TopY = topY;
            IsPlatform = isPlatform;
        }

        public bool Contains(Vector3 position)
        {
            return position.x >= MinX && position.x <= MaxX && Mathf.Abs(position.y - TopY) <= 2.2f;
        }
    }
}
