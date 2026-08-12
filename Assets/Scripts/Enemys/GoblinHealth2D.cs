using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GoblinHealth2D : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 100;     // 고블린의 최대 체력
    [SerializeField] private int currentHp;       // 현재 체력
    [SerializeField] private int startHpOverride = -1;

    [Header("Hit")]
    [SerializeField] private Color hitColor = Color.red;   // 피격 시 잠시 변경할 색상
    [SerializeField] private float hitColorDuration = 0.2f; // 피격 색상 유지 시간
    [SerializeField] private float hitStunDuration = 0.25f; // 피격 경직 관련 시간값 (현재 이 스크립트에서는 직접 사용하지 않음)
    [SerializeField] private Slider hpSlider;

    [Header("Death Effect")]
    [SerializeField] private float deathDuration = 0.5f;
    [SerializeField] private float deathRotateZ = 25f;
    [SerializeField] private float deathFloatY = 0.2f;

    private GoblinController2D goblinController; // 이동/넉백/경직 처리를 맡는 컨트롤러 참조
    private SpriteRenderer[] renderers;          // 자신 및 자식에 있는 모든 SpriteRenderer
    private Color[] originalColors;              // 각 SpriteRenderer의 원래 색상 저장용
    private bool isHitStun;                      // 피격 경직 상태용 변수 (현재 이 스크립트에서는 직접 사용하지 않음)

    [SerializeField] private int expReward = 3;   // 처치 시 지급 EXP(메이플식: 몬스터가 보상값 소유)
    private GoblinBossCombatController2D bossCombat;
    private bool isDead;                          // 중복 처치/중복 EXP 지급 방지

    // Codex recovery compatibility: GoblinBoss scripts need read-only HP/death state.
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public float HpRatio => maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
    public bool IsDead => isDead;

    /// <summary>
    /// 초기 참조 캐싱.
    /// - GoblinController2D를 연결해 피격 시 이동/넉백 로직을 호출할 준비를 함
    /// - 자식 포함 SpriteRenderer를 모두 수집
    /// - 피격 후 원래 색으로 복구하기 위해 시작 색상을 배열에 저장
    /// </summary>
    private void Awake()
    {
        goblinController = GetComponent<GoblinController2D>();
        bossCombat = GetComponent<GoblinBossCombatController2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalColors[i] = renderers[i].color;
        }

        if (hpSlider == null) hpSlider = GetComponentInChildren<Slider>(true);
    }

    void Start()
    {
        // [Codex Boss HP Test] -1이면 최대 체력, 0 이상이면 인스펙터 값으로 시작해서 2페이즈 테스트를 쉽게 합니다.
        currentHp = startHpOverride >= 0 ? Mathf.Clamp(startHpOverride, 0, maxHp) : maxHp;
        SyncHpUI();
    }

    public void TakeDamage(
        int damage,
        float hitDir,
        bool applyHitReaction = true)
    {
        TakeDamage(damage, hitDir, applyHitReaction, 0f);
    }

    public void TakeDamage(
        int damage,
        float hitDir,
        bool applyHitReaction,
        float powerShotChargeRatio)
    {
        if (isDead) return;

        // [Codex Boss Shield Break] ShieldBlockU 중에는 50% 이상 차징 파워샷만 방어 파괴 판정으로 받습니다.
        if (bossCombat != null && bossCombat.TryHandleShieldDamage(powerShotChargeRatio, hitDir))
            return;

        // [Codex Boss Shield Groggy] 방어 파괴 후 Dance 그로기 동안 받는 피해를 강화합니다.
        int finalDamage = bossCombat != null
            ? Mathf.Max(1, Mathf.RoundToInt(damage * bossCombat.CurrentDamageMultiplier))
            : damage;

        currentHp -= finalDamage;

        SyncHpUI();

        // [Codex Boss Heal Cast] 회복 캐스팅은 취소하지 않지만, 맞았다는 피드백은 보스 흔들림으로 보여줍니다.
        bossCombat?.NotifyHealCastHit();

        if (currentHp <= 0)
        {
            isDead = true;

            StartCoroutine(CoDie());

            // 1) 슬라이더 정리(시각적으로 0 고정)
            currentHp = 0;
            if (hpSlider != null)
                hpSlider.value = 0f;

            // 2) EXP 지급(정석: 몬스터가 죽을 때 지급)
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                PlayerStats stats = player.GetComponent<PlayerStats>();
                if (stats != null)
                    stats.AddEXP(expReward);
            }


            return;
        }

        StartCoroutine(CoHitColor());

        if (applyHitReaction && goblinController != null)
        {
            goblinController.PlayHitStun();
            goblinController.PlayKnockback(hitDir);
        }
    }

    /// <summary>
    /// 사망 연출 코루틴.
    /// - 컨트롤러/충돌/물리 동작을 정지
    /// - 살짝 기울이며 위로 이동
    /// - 알파값을 줄여 사라지게 처리
    /// - 연출 종료 후 오브젝트 제거
    /// </summary>
    private IEnumerator CoDie()
    {
        if (goblinController != null)
            goblinController.enabled = false;

        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D col in cols)
        {
            if (col != null)
                col.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 endPos = startPos + Vector3.up * deathFloatY;
        Quaternion endRot = Quaternion.Euler(0f, 0f, deathRotateZ);

        float elapsed = 0f;

        while (elapsed < deathDuration)
        {
            float t = elapsed / deathDuration;

            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.rotation = Quaternion.Lerp(startRot, endRot, t);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color c = renderers[i].color;
                c.a = Mathf.Lerp(originalColors[i].a, 0f, t);
                renderers[i].color = c;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
    private void SyncHpUI()
    {
        if (hpSlider == null) return;

        float ratio = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
        hpSlider.value = ratio;
    }

    public void HealBossHp(int amount)
    {
        // [Codex Boss Heal Cast] 보스 회복 패턴 전용 HP 회복입니다. 최대 HP를 넘지 않도록 제한합니다.
        if (isDead || amount <= 0)
            return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        SyncHpUI();
    }
    /// <summary>
    /// 피격 색상 연출 코루틴.
    /// - 모든 SpriteRenderer를 hitColor로 잠시 변경
    /// - 일정 시간 후 Awake에서 저장한 원래 색상으로 복구
    /// </summary>
    private IEnumerator CoHitColor()
    {
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != null)
                sr.color = hitColor;
        }

        yield return new WaitForSeconds(hitColorDuration);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }
    }
}
