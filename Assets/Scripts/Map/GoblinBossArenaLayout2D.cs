    using UnityEngine;

    /// <summary>
    /// 고블린 보스 전용 씬의 시작 배치를 구성합니다.
    /// Spring 원본 씬과 프리팹을 건드리지 않고, 보스 씬 안에서만 기존 지형을 재사용합니다.
    /// </summary>
    [ExecuteAlways]
    public class GoblinBossArenaLayout2D : MonoBehaviour
    {
        private static readonly Vector3 PlayerStart = new Vector3(-7f, -7.67f, 0f);
        private static readonly Vector3 BossStart = new Vector3(7f, -7.67f, 0.08854225f);

        private void OnEnable()
        {
            if (!IsGoblinBossScene(gameObject.scene.name))
                return;

            // [보스 맵 1단계] 기존 3층을 메인 바닥으로 쓰고 RPG식 아래층 구조는 숨깁니다.
            GameObject groundBase = FindSceneObject("Ground_Base");
            GameObject ground2F = FindSceneObject("Ground_2F");
            GameObject ground3F = FindSceneObject("Ground_3F");
            if (ground3F == null)
                ground3F = FindSceneObject("BossArena_MainFloor");

            CreateSidePlatforms(ground2F);
            CreateArenaBoundaries();

            if (groundBase != null)
                groundBase.SetActive(false);
            if (ground2F != null)
                ground2F.SetActive(false);
            if (ground3F != null)
            {
                ground3F.name = "BossArena_MainFloor";
                ConfigureMainFloorCollider(ground3F);
            }

            // [보스 맵 1단계] 일반 몬스터는 제외하고 보스 한 마리만 오른쪽에 배치합니다.
            SetActiveIfFound("Goblin (1)", false);
            SetActiveIfFound("Goblin (2)", false);

            GameObject boss = FindSceneObject("Goblin (3)");
            if (boss != null)
            {
                boss.transform.position = BossStart;

                // [보스 패턴 1단계] 일반 고블린은 유지하고 보스 인스턴스에만 낙하 공격을 연결합니다.
                if (boss.GetComponent<GoblinBossCombatController2D>() == null)
                    boss.AddComponent<GoblinBossCombatController2D>();
                if (boss.GetComponent<GoblinBossFallingAttack2D>() == null)
                    boss.AddComponent<GoblinBossFallingAttack2D>();

                IgnoreBossSidePlatformCollisions(boss);
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                player.transform.position = PlayerStart;

            // [보스 맵 1단계] 플레이어를 따라 흔들리지 않는 고정 투기장 구도로 설정합니다.
            CameraFollow2D follow = GetComponent<CameraFollow2D>();
            if (follow != null)
                follow.enabled = false;

            Camera arenaCamera = GetComponent<Camera>();
            if (arenaCamera != null)
                arenaCamera.orthographicSize = 6f;

            transform.position = new Vector3(0f, -5.35f, -10f);
        }

        private static void CreateArenaBoundaries()
        {
            // [보스 맵 경계] 플레이어와 보스가 카메라 밖으로 나가지 않도록 보이지 않는 좌우 벽을 만듭니다.
            GameObject boundaryRoot = FindSceneObject("BossArena_Boundaries");
            if (boundaryRoot == null)
                boundaryRoot = new GameObject("BossArena_Boundaries");

            CreateBoundary(boundaryRoot.transform, "LeftBoundary", -9.2f);
            CreateBoundary(boundaryRoot.transform, "RightBoundary", 9.2f);
        }

        private static void CreateBoundary(Transform parent, string objectName, float x)
        {
            Transform existing = parent.Find(objectName);
            bool isNewBoundary = existing == null;
            GameObject boundary = existing != null ? existing.gameObject : new GameObject(objectName);
            boundary.transform.SetParent(parent);

            // [보스 맵 경계 편집] 처음 만들 때만 기본 위치를 지정하여 Hierarchy에서 옮긴 투명벽 위치를 덮어쓰지 않습니다.
            if (isNewBoundary)
                boundary.transform.position = new Vector3(x, -1f, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                boundary.layer = groundLayer;

            EdgeCollider2D edge = boundary.GetComponent<EdgeCollider2D>();
            if (edge == null)
                edge = boundary.AddComponent<EdgeCollider2D>();

            edge.isTrigger = false;
            edge.points = new[] { new Vector2(0f, -10f), new Vector2(0f, 10f) };
        }

        private static void CreateSidePlatforms(GameObject sourceFloor)
        {
            if (sourceFloor == null)
                return;

            Transform source = sourceFloor.transform.Find("platform_05_0");
            if (source == null)
                return;

            GameObject platformRoot = FindSceneObject("BossArena_SidePlatforms");
            if (platformRoot == null)
                platformRoot = new GameObject("BossArena_SidePlatforms");

            // [보스 맵 동선 개선] 좌우 발판은 지상에서 여유 있게 닿도록 조금 낮춥니다.
            // [Codex Platform Scene Scale] 씬에 이미 있는 발판은 직접 맞춘 Transform 값을 유지하고, 새 발판만 기본값으로 만듭니다.
            Vector3 platformDefaultScale = new Vector3(0.3f, 0.3f, 1f);
            CreateOrUpdatePlatform(source.gameObject, platformRoot.transform, "LeftPlatform", new Vector3(-6.6f, -5.65f, 0f), platformDefaultScale);
            CreateOrUpdatePlatform(source.gameObject, platformRoot.transform, "RightPlatform", new Vector3(6.6f, -5.65f, 0f), platformDefaultScale);

            // [보스 맵 동선 개선] 기존의 작은 발판 조각을 재사용해 중앙에 두 번째 이동 단계를 만듭니다.
            Transform centerSource = sourceFloor.transform.Find("platform_10_0 (6)");
            if (centerSource != null)
            {
                CreateOrUpdatePlatform(
                    centerSource.gameObject,
                    platformRoot.transform,
                    "CenterPlatform",
                    new Vector3(0f, -3.65f, 0f),
                    platformDefaultScale);
            }
        }

        private static void ConfigureMainFloorCollider(GameObject mainFloor)
        {
            // [보스 맵 바닥 평탄화] 조각마다 높이가 다른 충돌체를 끄고 긴 충돌체 하나로 통일합니다.
            Collider2D[] pieceColliders = mainFloor.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < pieceColliders.Length; i++)
            {
                if (pieceColliders[i].gameObject != mainFloor)
                    pieceColliders[i].enabled = false;
            }

            BoxCollider2D flatCollider = mainFloor.GetComponent<BoxCollider2D>();
            if (flatCollider == null)
                flatCollider = mainFloor.AddComponent<BoxCollider2D>();

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                mainFloor.layer = groundLayer;

            flatCollider.enabled = true;
            flatCollider.offset = new Vector2(-11.14f, 5.35f);
            flatCollider.size = new Vector2(58f, 0.7f);
        }

        private static void CreateOrUpdatePlatform(
            GameObject source,
            Transform parent,
            string objectName,
            Vector3 position,
            Vector3 scale)
        {
            Transform existing = parent.Find(objectName);
            bool isNewPlatform = existing == null;
            GameObject platform = isNewPlatform
                ? Instantiate(source, position, Quaternion.identity, parent)
                : existing.gameObject;

            platform.name = objectName;
            if (isNewPlatform)
            {
                platform.transform.SetPositionAndRotation(position, Quaternion.identity);
                platform.transform.localScale = scale;
            }
            platform.SetActive(true);

            ConfigureLandingPlatform(platform, source);
        }

        private static void ConfigureLandingPlatform(GameObject platform, GameObject source)
        {
            // [보스 맵 발판 수정] 원본의 얇은 충돌면을 사용해 캐릭터가 확실히 착지하도록 구성합니다.
            PlatformEffector2D effector = platform.GetComponent<PlatformEffector2D>();
            if (effector == null)
                effector = platform.AddComponent<PlatformEffector2D>();

            // [보스 맵 발판 수정] 정확한 원본 충돌면에만 단방향 효과를 연결해 옆·아래는 통과시킵니다.
            effector.enabled = true;
            effector.useOneWay = true;
            effector.useOneWayGrouping = false;
            effector.surfaceArc = 180f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;

            BoxCollider2D landingCollider = null;
            Collider2D[] colliders = platform.GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (landingCollider == null && colliders[i] is BoxCollider2D box)
                    landingCollider = box;
                else
                    colliders[i].enabled = false;
            }

            bool needsGeneratedCollider = landingCollider == null;
            if (needsGeneratedCollider)
                landingCollider = platform.AddComponent<BoxCollider2D>();

            // [보스 맵 발판 수정] 원본 발판에 손으로 맞춰둔 충돌면 위치와 크기를 그대로 복사합니다.
            BoxCollider2D sourceCollider = null;
            BoxCollider2D[] sourceColliders = source.GetComponents<BoxCollider2D>();
            for (int i = 0; i < sourceColliders.Length; i++)
            {
                if (sourceColliders[i].enabled)
                {
                    sourceCollider = sourceColliders[i];
                    break;
                }
            }

            if (sourceCollider != null && needsGeneratedCollider)
            {
                // [Codex Manual Collider Keep] 씬에서 직접 맞춘 기존 BoxCollider2D는 덮어쓰지 않고, 새로 만든 콜리더에만 기본값을 복사합니다.
                landingCollider.size = sourceCollider.size;
                landingCollider.offset = sourceCollider.offset;
                landingCollider.edgeRadius = sourceCollider.edgeRadius;
            }

            SpriteRenderer spriteRenderer = platform.GetComponent<SpriteRenderer>();
            if (sourceCollider == null && needsGeneratedCollider && spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Bounds spriteBounds = spriteRenderer.sprite.bounds;
                landingCollider.size = new Vector2(spriteBounds.size.x * 0.94f, 0.22f);
                landingCollider.offset = new Vector2(spriteBounds.center.x, spriteBounds.max.y - 0.11f);
            }

            landingCollider.enabled = true;
            landingCollider.usedByEffector = true;

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                platform.layer = groundLayer;
        }

        private static void IgnoreBossSidePlatformCollisions(GameObject boss)
        {
            // [Codex Boss Platform Ignore] Only ignore the floating side/center platforms, not BossArena_MainFloor.
            if (boss == null)
                return;

            GameObject platformRoot = FindSceneObject("BossArena_SidePlatforms");
            if (platformRoot == null)
                return;

            Collider2D[] bossColliders = boss.GetComponentsInChildren<Collider2D>(true);
            Collider2D[] platformColliders = platformRoot.GetComponentsInChildren<Collider2D>(true);

            for (int bossIndex = 0; bossIndex < bossColliders.Length; bossIndex++)
            {
                Collider2D bossCollider = bossColliders[bossIndex];
                if (bossCollider == null)
                    continue;

                for (int platformIndex = 0; platformIndex < platformColliders.Length; platformIndex++)
                {
                    Collider2D platformCollider = platformColliders[platformIndex];
                    if (platformCollider == null || !platformCollider.enabled)
                        continue;

                    Physics2D.IgnoreCollision(bossCollider, platformCollider, true);
                }
            }
        }

        private static void SetActiveIfFound(string objectName, bool active)
        {
            GameObject target = FindSceneObject(objectName);
            if (target != null)
                target.SetActive(active);
        }

        private static GameObject FindSceneObject(string objectName)
        {
            // [보스 맵 1단계] 비활성화된 원본 지형도 편집 모드에서 찾아 재사용합니다.
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate.name == objectName && candidate.scene.IsValid() && IsGoblinBossScene(candidate.scene.name))
                    return candidate;
            }

            return null;
        }

        private static bool IsGoblinBossScene(string sceneName)
        {
            // [Codex Boss Network Scene] 일반/네트워크 보스 씬 모두 같은 발판 충돌 무시 설정을 사용합니다.
            return sceneName == "GoblinBoss" || sceneName == "GoblinBoss_Network";
        }
    }
