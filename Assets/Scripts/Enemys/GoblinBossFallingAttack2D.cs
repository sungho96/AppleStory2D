using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 고블린 보스의 낙하 공격입니다.
/// 플레이어 위치 예고 -> 다중 낙하물 -> 범위 피해 순서로 실행합니다.
/// </summary>
public class GoblinBossFallingAttack2D : MonoBehaviour
{
    [Header("Attack Timing")]
    [SerializeField] private float firstAttackDelay = 2.5f;
    [SerializeField] private float warningDuration = 1.5f;
    [SerializeField] private float fallDuration = 0.55f;
    [SerializeField] private float rockStaggerDelay = 0.34f;
    [SerializeField] private float rockDisappearDuration = 0.22f;
    [SerializeField] private Vector2 cooldownRange = new Vector2(4.5f, 6f);

    [Header("Phase 2 - HP 50%")]
    [SerializeField] private float phaseTwoWarningDuration = 1.15f;
    [SerializeField] private float phaseTwoRockStaggerDelay = 0.24f;
    [SerializeField] private float phaseTwoCooldownMultiplier = 0.75f;
    [SerializeField] private int phaseTwoDamage = 25;

    [Header("Damage")]
    [SerializeField] private int damage = 20;
    [SerializeField] private float hitRadius = 0.9f;
    [SerializeField] private float knockbackX = 7f;
    [SerializeField] private float knockbackY = 5f;

    [Header("Arena")]
    [SerializeField] private float minTargetX = -9f;
    [SerializeField] private float maxTargetX = 9f;
    [SerializeField] private float spawnHeight = 7.5f;

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
        int rockCount = phaseTwo ? 3 : 2;
        int attackDamage = phaseTwo ? phaseTwoDamage : damage;
        float currentWarningDuration = phaseTwo ? phaseTwoWarningDuration : warningDuration;
        float currentStaggerDelay = phaseTwo ? phaseTwoRockStaggerDelay : rockStaggerDelay;
        List<Vector2> impactPoints = CreateImpactPoints(rockCount);
        List<GameObject> warnings = new List<GameObject>(rockCount);

        for (int i = 0; i < impactPoints.Count; i++)
            warnings.Add(CreateWarning(impactPoints[i], phaseTwo));

        // [보스 시전 연결] 바닥 예고가 시작되는 순간 보스가 이동을 멈추고 스킬 동작을 재생합니다.
        if (bossCombat != null)
            bossCombat.BeginFallingCast(currentWarningDuration);

        // [보스 낙하 공격 페이즈] 모든 위치를 먼저 알려준 뒤 돌을 짧은 간격으로 순차 낙하시킵니다.
        float timer = 0f;
        while (timer < currentWarningDuration)
        {
            timer += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(timer * 12f) * 0.12f;
            for (int i = 0; i < warnings.Count; i++)
            {
                if (warnings[i] != null)
                    warnings[i].transform.localScale = new Vector3(2.2f * pulse, 0.38f * pulse, 1f);
            }
            yield return null;
        }

        for (int i = 0; i < impactPoints.Count; i++)
        {
            StartCoroutine(DropRock(impactPoints[i], warnings[i], attackDamage));
            yield return new WaitForSeconds(currentStaggerDelay);
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

        ApplyImpactDamage(impactPoint, attackDamage);
        yield return ShowImpactFlash(impactPoint);

        if (warning != null)
            Destroy(warning);
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

    private List<Vector2> CreateImpactPoints(int count)
    {
        const float spacing = 2.8f;
        float primaryX = Mathf.Clamp(player.position.x, minTargetX, maxTargetX);
        float arenaCenter = (minTargetX + maxTargetX) * 0.5f;
        float direction = primaryX > arenaCenter ? -1f : 1f;
        List<Vector2> points = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            float x = primaryX + direction * spacing * i;
            if (x < minTargetX || x > maxTargetX)
                x = primaryX - direction * spacing * i;

            points.Add(FindImpactPoint(Mathf.Clamp(x, minTargetX, maxTargetX)));
        }

        return points;
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

    private GameObject CreateFallingObject(Vector2 impactPoint)
    {
        GameObject fallingObject = new GameObject("BossFall_Object");
        fallingObject.transform.position = new Vector3(impactPoint.x, impactPoint.y + spawnHeight, 0f);
        fallingObject.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

        SpriteRenderer renderer = fallingObject.AddComponent<SpriteRenderer>();
        renderer.sprite = fallingSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 30;
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
}
