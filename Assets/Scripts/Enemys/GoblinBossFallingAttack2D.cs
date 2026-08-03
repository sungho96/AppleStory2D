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
    [SerializeField] private Vector2 cooldownRange = new Vector2(4.5f, 6f);

    [Header("Meteor Disappear")]
    [Tooltip("충돌 후 바위가 사라지기 시작하기 전 대기시간입니다.")]
    [SerializeField] private float rockDisappearDelay = 0.15f;

    [Tooltip("바위가 사라지기 시작한 뒤 완전히 사라질 때까지의 시간입니다.")]
    [SerializeField] private float rockDisappearDuration = 0.2f;

    [Tooltip("사라질 때 남길 가로 크기 비율입니다.")]
    [SerializeField, Range(0f, 1f)]
    private float rockDisappearWidthRatio = 0.9f;

    [Tooltip("사라질 때 남길 세로 크기 비율입니다.")]
    [SerializeField, Range(0f, 1f)]
    private float rockDisappearHeightRatio = 0.03f;

    [Tooltip("바위가 사라지면서 땅속으로 내려가는 거리입니다.")]
    [SerializeField] private float rockSinkDistance = 0.12f;

    [Tooltip("전체 소멸 시간 중 바위가 투명해지기 시작하는 시점입니다.")]
    [SerializeField, Range(0f, 0.95f)]
    private float rockFadeStartRatio = 0.6f;

    [Header("Impact Effect")]
    [Tooltip("충격 섬광, 파편, 먼지가 유지되는 전체 시간입니다.")]
    [SerializeField] private float impactEffectDuration = 0.85f;

    [Tooltip("먼지가 전체 지속시간 중 투명해지기 시작하는 시점입니다.")]
    [SerializeField, Range(0f, 0.95f)]
    private float dustFadeStartRatio = 0.78f;

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

        // 메테오 이미지 로드 실패 시 임시 원 이미지를 사용합니다.
        if (fallingSprite == null)
        {
            fallingSprite = CreateCircleSprite(48, 0.92f);
        }

        // 얼음 파도 공격이 없다면 런타임으로 추가합니다.
        if (GetComponent<GoblinBossIceWaveAttack2D>() == null)
        {
            gameObject.AddComponent<GoblinBossIceWaveAttack2D>();
        }
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(firstAttackDelay);

        while (bossHealth == null || !bossHealth.IsDead)
        {
            FindPlayer();

            if (player != null)
            {
                yield return PerformFallingAttack();
            }

            float cooldownMultiplier =
                IsPhaseTwo() ? phaseTwoCooldownMultiplier : 1f;

            float cooldown =
                Random.Range(cooldownRange.x, cooldownRange.y)
                * cooldownMultiplier;

            yield return new WaitForSeconds(cooldown);
        }
    }

    private IEnumerator PerformFallingAttack()
    {
        if (bossCombat == null)
        {
            bossCombat = GetComponent<GoblinBossCombatController2D>();
        }

        // 다른 보스 공격을 시전 중이라면 기다립니다.
        while (bossCombat != null && bossCombat.IsCasting)
        {
            yield return null;
        }

        bool phaseTwo = IsPhaseTwo();

        int rockCount = phaseTwo ? 3 : 2;
        int attackDamage = phaseTwo ? phaseTwoDamage : damage;

        float currentWarningDuration =
            phaseTwo ? phaseTwoWarningDuration : warningDuration;

        float currentStaggerDelay =
            phaseTwo ? phaseTwoRockStaggerDelay : rockStaggerDelay;

        List<Vector2> impactPoints =
            CreateImpactPoints(rockCount);

        List<GameObject> warnings =
            new List<GameObject>(rockCount);

        for (int i = 0; i < impactPoints.Count; i++)
        {
            warnings.Add(
                CreateWarning(
                    impactPoints[i],
                    phaseTwo));
        }

        if (bossCombat != null)
        {
            bossCombat.BeginFallingCast(currentWarningDuration);
        }

        float timer = 0f;

        while (timer < currentWarningDuration)
        {
            timer += Time.deltaTime;

            float pulse =
                1f + Mathf.Sin(timer * 12f) * 0.12f;

            for (int i = 0; i < warnings.Count; i++)
            {
                if (warnings[i] == null)
                {
                    continue;
                }

                warnings[i].transform.localScale =
                    new Vector3(
                        2.2f * pulse,
                        0.38f * pulse,
                        1f);
            }

            yield return null;
        }

        for (int i = 0; i < impactPoints.Count; i++)
        {
            StartCoroutine(
                DropRock(
                    impactPoints[i],
                    warnings[i],
                    attackDamage));

            yield return new WaitForSeconds(currentStaggerDelay);
        }

        yield return new WaitForSeconds(fallDuration + 0.2f);
    }

    private IEnumerator DropRock(
        Vector2 impactPoint,
        GameObject warning,
        int attackDamage)
    {
        GameObject fallingObject =
            CreateFallingObject(impactPoint);

        float meteorDirection =
            impactPoint.x < 0f ? 1f : -1f;

        Vector3 start = new Vector3(
            impactPoint.x - meteorDirection * spawnHeight,
            impactPoint.y + spawnHeight,
            0f);

        float meteorAngle =
            meteorDirection > 0f ? -45f : 45f;

        fallingObject.transform.SetPositionAndRotation(
            start,
            Quaternion.Euler(0f, 0f, meteorAngle));

        /*
         * 스프라이트 피벗을 메테오의 실제 아래쪽에 설정했으므로
         * impactPoint를 착지 위치로 그대로 사용합니다.
         */
        Vector3 end = new Vector3(
            impactPoint.x,
            impactPoint.y,
            0f);

        float timer = 0f;

        while (timer < fallDuration)
        {
            timer += Time.deltaTime;

            float normalized =
                Mathf.Clamp01(timer / fallDuration);

            float accelerated =
                normalized * normalized;

            if (fallingObject != null)
            {
                fallingObject.transform.position =
                    Vector3.Lerp(
                        start,
                        end,
                        accelerated);

                // 착지 순간 정방향으로 돌아오도록 회전합니다.
                float spinAngle =
                    normalized * 405f * meteorDirection;

                fallingObject.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        meteorAngle + spinAngle);
            }

            yield return null;
        }

        if (fallingObject != null)
        {
            float finalAngle =
                meteorAngle + 405f * meteorDirection;

            fallingObject.transform.SetPositionAndRotation(
                end,
                Quaternion.Euler(
                    0f,
                    0f,
                    finalAngle));
        }

        ApplyImpactDamage(
            impactPoint,
            attackDamage);

        if (warning != null)
        {
            Destroy(warning);
        }

        /*
         * 먼지와 파편은 충돌 즉시 시작합니다.
         * StartCoroutine으로 실행하기 때문에
         * 바위 소멸과 별개로 계속 유지됩니다.
         */
        StartCoroutine(
            ShowImpactFlash(impactPoint));

        /*
         * 바위는 충돌 후 설정한 시간만큼 그대로 남아 있습니다.
         */
        float disappearDelay =
            Mathf.Max(0f, rockDisappearDelay);

        if (disappearDelay > 0f)
        {
            yield return new WaitForSeconds(disappearDelay);
        }

        /*
         * 대기시간이 끝나면 바위가 땅으로 흡수되며 사라집니다.
         */
        if (fallingObject != null)
        {
            yield return FadeOutFallingObject(fallingObject);
        }
    }

    private IEnumerator FadeOutFallingObject(
        GameObject fallingObject)
    {
        SpriteRenderer renderer =
            fallingObject.GetComponent<SpriteRenderer>();

        Vector3 startScale =
            fallingObject.transform.localScale;

        Vector3 startPosition =
            fallingObject.transform.position;

        Color startColor =
            renderer != null
                ? renderer.color
                : Color.white;

        float disappearDuration =
            Mathf.Max(
                0.01f,
                rockDisappearDuration);

        float timer = 0f;

        while (timer < disappearDuration)
        {
            timer += Time.deltaTime;

            float ratio =
                Mathf.Clamp01(
                    timer / disappearDuration);

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    ratio);

            // 가로는 거의 유지하고 세로만 납작하게 줄입니다.
            float scaleX =
                Mathf.Lerp(
                    startScale.x,
                    startScale.x * rockDisappearWidthRatio,
                    eased);

            float scaleY =
                Mathf.Lerp(
                    startScale.y,
                    startScale.y * rockDisappearHeightRatio,
                    eased);

            fallingObject.transform.localScale =
                new Vector3(
                    scaleX,
                    scaleY,
                    startScale.z);

            // 바위를 조금 아래로 이동시켜 땅속으로 흡수되는 느낌을 줍니다.
            Vector3 sinkPosition =
                startPosition
                + Vector3.down * rockSinkDistance;

            fallingObject.transform.position =
                Vector3.Lerp(
                    startPosition,
                    sinkPosition,
                    eased);

            if (renderer != null)
            {
                Color color = startColor;

                float fadeRatio =
                    Mathf.InverseLerp(
                        rockFadeStartRatio,
                        1f,
                        ratio);

                color.a =
                    startColor.a * (1f - fadeRatio);

                renderer.color = color;
            }

            yield return null;
        }

        Destroy(fallingObject);
    }

    private List<Vector2> CreateImpactPoints(int count)
    {
        const float spacing = 2.8f;

        float primaryX =
            Mathf.Clamp(
                player.position.x,
                minTargetX,
                maxTargetX);

        float arenaCenter =
            (minTargetX + maxTargetX) * 0.5f;

        float direction =
            primaryX > arenaCenter
                ? -1f
                : 1f;

        List<Vector2> points =
            new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            float x =
                primaryX
                + direction * spacing * i;

            if (x < minTargetX || x > maxTargetX)
            {
                x =
                    primaryX
                    - direction * spacing * i;
            }

            points.Add(
                FindImpactPoint(
                    Mathf.Clamp(
                        x,
                        minTargetX,
                        maxTargetX)));
        }

        return points;
    }

    private Vector2 FindImpactPoint(float targetX)
    {
        Vector2 rayOrigin =
            new Vector2(
                targetX,
                player.position.y + 0.5f);

        int groundLayer =
            LayerMask.GetMask("Ground");

        RaycastHit2D hit =
            Physics2D.Raycast(
                rayOrigin,
                Vector2.down,
                20f,
                groundLayer);

        return hit.collider != null
            ? hit.point
            : new Vector2(
                targetX,
                -7.9f);
    }

    private GameObject CreateWarning(
        Vector2 impactPoint,
        bool phaseTwo)
    {
        GameObject warning =
            new GameObject("BossFall_Warning");

        warning.transform.position =
            new Vector3(
                impactPoint.x,
                impactPoint.y + 0.04f,
                0f);

        warning.transform.localScale =
            new Vector3(
                2.2f,
                0.38f,
                1f);

        SpriteRenderer renderer =
            warning.AddComponent<SpriteRenderer>();

        renderer.sprite = warningSprite;

        renderer.color = phaseTwo
            ? new Color(
                1f,
                0.32f,
                0.02f,
                0.76f)
            : new Color(
                1f,
                0.08f,
                0.03f,
                0.62f);

        renderer.sortingOrder = 25;

        return warning;
    }

    private GameObject CreateFallingObject(
        Vector2 impactPoint)
    {
        GameObject fallingObject =
            new GameObject("BossFall_Object");

        fallingObject.transform.position =
            new Vector3(
                impactPoint.x,
                impactPoint.y + spawnHeight,
                0f);

        fallingObject.transform.localScale =
            new Vector3(
                0.2f,
                0.2f,
                1f);

        SpriteRenderer renderer =
            fallingObject.AddComponent<SpriteRenderer>();

        renderer.sprite = fallingSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 30;

        return fallingObject;
    }

    private void ApplyImpactDamage(
        Vector2 impactPoint,
        int attackDamage)
    {
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                impactPoint,
                hitRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth2D playerHealth =
                hits[i].GetComponentInParent<PlayerHealth2D>();

            if (playerHealth == null)
            {
                continue;
            }

            float direction =
                playerHealth.transform.position.x
                >= impactPoint.x
                    ? 1f
                    : -1f;

            playerHealth.TakeDamage(
                attackDamage,
                new Vector2(
                    direction * knockbackX,
                    knockbackY));

            break;
        }
    }

    private IEnumerator ShowImpactFlash(
        Vector2 impactPoint)
    {
        GameObject flash =
            new GameObject("BossFall_Impact");

        flash.transform.position =
            new Vector3(
                impactPoint.x,
                impactPoint.y + 0.08f,
                0f);

        SpriteRenderer renderer =
            flash.AddComponent<SpriteRenderer>();

        renderer.sprite = warningSprite;

        renderer.color =
            new Color(
                1f,
                0.55f,
                0.08f,
                0.8f);

        renderer.sortingOrder = 31;

        List<GameObject> fragments =
            new List<GameObject>();

        List<Vector2> fragmentVelocities =
            new List<Vector2>();

        for (int i = 0; i < 7; i++)
        {
            GameObject fragment =
                CreateImpactParticle(
                    "BossFall_Fragment",
                    impactPoint + Vector2.up * 0.12f,
                    new Color(
                        0.3f,
                        0.2f,
                        0.1f,
                        1f),
                    Random.Range(
                        0.24f,
                        0.46f),
                    33);

            fragments.Add(fragment);

            fragmentVelocities.Add(
                new Vector2(
                    Random.Range(
                        -6.2f,
                        6.2f),
                    Random.Range(
                        4.2f,
                        8.2f)));
        }

        List<GameObject> dustPuffs =
            new List<GameObject>();

        for (int i = 0; i < 3; i++)
        {
            Vector2 dustPosition =
                impactPoint
                + new Vector2(
                    (i - 1) * 0.7f,
                    0.18f);

            dustPuffs.Add(
                CreateImpactParticle(
                    "BossFall_Dust",
                    dustPosition,
                    new Color(
                        0.72f,
                        0.62f,
                        0.5f,
                        0.98f),
                    0.42f,
                    32,
                    impactDustSprite));
        }

        float timer = 0f;

        float duration =
            Mathf.Max(
                0.01f,
                impactEffectDuration);

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float normalized =
                Mathf.Clamp01(
                    timer / duration);

            flash.transform.localScale =
                Vector3.Lerp(
                    new Vector3(
                        0.8f,
                        0.28f,
                        1f),
                    new Vector3(
                        4.8f,
                        1.15f,
                        1f),
                    normalized);

            renderer.color =
                new Color(
                    1f,
                    0.55f,
                    0.08f,
                    0.8f * (1f - normalized));

            for (int i = 0; i < fragments.Count; i++)
            {
                if (fragments[i] == null)
                {
                    continue;
                }

                Vector2 velocity =
                    fragmentVelocities[i];

                fragments[i].transform.position +=
                    (Vector3)(
                        velocity * Time.deltaTime);

                velocity.y -=
                    14f * Time.deltaTime;

                fragmentVelocities[i] = velocity;

                fragments[i].transform.Rotate(
                    0f,
                    0f,
                    720f * Time.deltaTime);

                SpriteRenderer fragmentRenderer =
                    fragments[i]
                        .GetComponent<SpriteRenderer>();

                if (fragmentRenderer != null)
                {
                    Color fragmentColor =
                        fragmentRenderer.color;

                    fragmentColor.a =
                        1f - normalized;

                    fragmentRenderer.color =
                        fragmentColor;
                }
            }

            for (int i = 0; i < dustPuffs.Count; i++)
            {
                if (dustPuffs[i] == null)
                {
                    continue;
                }

                float sideDirection =
                    i - 1;

                dustPuffs[i].transform.position +=
                    new Vector3(
                        sideDirection
                        * 0.9f
                        * Time.deltaTime,
                        0.7f * Time.deltaTime,
                        0f);

                dustPuffs[i].transform.localScale =
                    Vector3.one
                    * Mathf.Lerp(
                        0.42f,
                        0.95f,
                        normalized);

                SpriteRenderer dustRenderer =
                    dustPuffs[i]
                        .GetComponent<SpriteRenderer>();

                if (dustRenderer != null)
                {
                    Color dustColor =
                        dustRenderer.color;

                    float dustFade =
                        1f - Mathf.InverseLerp(
                            dustFadeStartRatio,
                            1f,
                            normalized);

                    dustColor.a =
                        0.98f * dustFade;

                    dustRenderer.color =
                        dustColor;
                }
            }

            yield return null;
        }

        Destroy(flash);

        for (int i = 0; i < fragments.Count; i++)
        {
            if (fragments[i] != null)
            {
                Destroy(fragments[i]);
            }
        }

        for (int i = 0; i < dustPuffs.Count; i++)
        {
            if (dustPuffs[i] != null)
            {
                Destroy(dustPuffs[i]);
            }
        }
    }

    private GameObject CreateImpactParticle(
        string objectName,
        Vector2 position,
        Color color,
        float scale,
        int sortingOrder,
        Sprite particleSprite = null)
    {
        GameObject particle =
            new GameObject(objectName);

        particle.transform.position =
            new Vector3(
                position.x,
                position.y,
                0f);

        particle.transform.localScale =
            Vector3.one * scale;

        SpriteRenderer renderer =
            particle.AddComponent<SpriteRenderer>();

        renderer.sprite =
            particleSprite != null
                ? particleSprite
                : warningSprite;

        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return particle;
    }

    private void FindPlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private bool IsPhaseTwo()
    {
        return bossHealth != null
            && bossHealth.CurrentHp
            <= bossHealth.MaxHp * 0.5f;
    }

    private static Sprite CreateCircleSprite(
        int size,
        float filledRadius)
    {
        Texture2D texture =
            new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false);

        texture.name =
            "BossFall_RuntimeSprite";

        texture.hideFlags =
            HideFlags.HideAndDontSave;

        texture.filterMode =
            FilterMode.Bilinear;

        Color[] pixels =
            new Color[size * size];

        Vector2 center =
            new Vector2(
                (size - 1) * 0.5f,
                (size - 1) * 0.5f);

        float radius =
            size * 0.5f * filledRadius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance =
                    Vector2.Distance(
                        new Vector2(x, y),
                        center);

                float alpha =
                    Mathf.Clamp01(
                        (radius - distance) / 2f);

                pixels[y * size + x] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(
                0f,
                0f,
                size,
                size),
            new Vector2(
                0.5f,
                0.5f),
            size);
    }
}