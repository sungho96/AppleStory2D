using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class GoblinHealth2D : NetworkBehaviour
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
    [SerializeField] private float deathFadeDelay = 1.0f;
    [SerializeField] private float deathFreezeCrossFadeBuffer = 0.08f;

    private GoblinController2D goblinController; // 이동/넉백/경직 처리를 맡는 컨트롤러 참조
    private AnimationManager animationManager;
    private Animator animator;
    private SpriteRenderer[] renderers;          // 자신 및 자식에 있는 모든 SpriteRenderer
    private Color[] originalColors;              // 각 SpriteRenderer의 원래 색상 저장용
    private bool isHitStun;                      // 피격 경직 상태용 변수 (현재 이 스크립트에서는 직접 사용하지 않음)

    [SerializeField] private int expReward = 3;   // 처치 시 지급 EXP(메이플식: 몬스터가 보상값 소유)
    private GoblinBossCombatController2D bossCombat;
    private bool isDead;                          // 중복 처치/중복 EXP 지급 방지
    private bool deathAnimationStarted;
    private bool deathRootLocked;
    private Vector3 deathRootPosition;
    private bool hasInitializedHp;
    private readonly NetworkVariable<int> syncedHp = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
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
        animationManager = GetComponent<AnimationManager>();
        animator = GetComponentInChildren<Animator>(true);
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                originalColors[i] = renderers[i].color;
        }

        if (hpSlider == null) hpSlider = GetComponentInChildren<Slider>(true);

        InitializeHpIfNeeded();
    }

    public override void OnNetworkSpawn()
    {
        syncedHp.OnValueChanged += OnSyncedHpChanged;

        InitializeHpIfNeeded();

        if (IsServer)
            syncedHp.Value = currentHp;

        ApplySyncedHp(syncedHp.Value);
    }

    public override void OnNetworkDespawn()
    {
        syncedHp.OnValueChanged -= OnSyncedHpChanged;
    }

    void Start()
    {
        // [Codex Boss HP Test] -1이면 최대 체력, 0 이상이면 인스펙터 값으로 시작해서 2페이즈 테스트를 쉽게 합니다.
        InitializeHpIfNeeded();
        if (IsSpawned && IsServer)
            syncedHp.Value = currentHp;
        SyncHpUI();
    }

    private void LateUpdate()
    {
        if (!deathRootLocked)
            return;

        // [Codex Boss Death Root Lock] Death 애니메이션/패턴 잔여 프레임이 루트를 아래로 밀어도 사망 순간 위치를 유지합니다.
        transform.position = deathRootPosition;
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
        if (IsNetworkClientOnly())
        {
            // [Codex Boss Network Hit] 클라이언트의 근접/스킬 피격 판정은 서버 HP만 줄이도록 RPC로 위임합니다.
            RequestTakeDamageServerRpc(damage, hitDir, applyHitReaction, powerShotChargeRatio);
            return;
        }

        ApplyDamageOnAuthority(damage, hitDir, applyHitReaction, powerShotChargeRatio);
    }

    private void ApplyDamageOnAuthority(
        int damage,
        float hitDir,
        bool applyHitReaction,
        float powerShotChargeRatio)
    {
        if (isDead) return;

        // [Codex Boss Shield Break Damage] ShieldBlockU 중에는 공격 종류와 관계없이 들어온 데미지를 쉴드 파괴량으로 누적합니다.
        if (bossCombat != null && bossCombat.TryHandleShieldDamage(damage, hitDir))
            return;

        // [Codex Boss Shield Groggy] 방어 파괴 후 Dance 그로기 동안 받는 피해를 강화합니다.
        int finalDamage = bossCombat != null
            ? Mathf.Max(1, Mathf.RoundToInt(damage * bossCombat.CurrentDamageMultiplier))
            : damage;

        currentHp -= finalDamage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        if (IsSpawned && IsServer)
            syncedHp.Value = currentHp;

        SyncHpUI();

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

        // [Codex Boss Heal Cast] 살아있는 피격에서만 회복 캐스팅 피드백을 갱신한다.
        bossCombat?.NotifyHealCastHit();

        PlayHitFeedback();

        if (IsSpawned && IsServer)
            PlayHitFeedbackClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeDamageServerRpc(
        int damage,
        float hitDir,
        bool applyHitReaction,
        float powerShotChargeRatio)
    {
        // [Codex Boss Network Hit] 모든 클라이언트 공격은 최종적으로 서버에서만 HP를 계산합니다.
        ApplyDamageOnAuthority(damage, hitDir, applyHitReaction, powerShotChargeRatio);
    }

    [ClientRpc]
    private void PlayHitFeedbackClientRpc()
    {
        if (IsServer || isDead)
            return;

        PlayHitFeedback();
    }

    private void PlayHitFeedback()
    {
        // [Codex Boss Hit Effect Only] 보스 피격 시 위치 넉백 없이 색상 이펙트만 재생합니다.
        StartCoroutine(CoHitColor());
    }

    /// <summary>
    /// 사망 연출 코루틴.
    /// - 컨트롤러/충돌/물리 동작을 정지
    /// - HeroEditor Death 애니메이션만 1회 실행
    /// - 오브젝트 삭제/페이드 처리 없음
    /// </summary>
    private IEnumerator CoDie()
    {
        if (goblinController != null)
        {
            goblinController.StopAllCoroutines();
            goblinController.enabled = false;
        }
        if (bossCombat != null)
        {
            bossCombat.StopAllCoroutines();
            bossCombat.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // [Codex Boss Death Physics Stop] 사망 시 콜라이더는 끄지 않고 물리 이동만 멈춰 BossArena_MainFloor를 통과하지 않게 합니다.
            deathRootPosition = transform.position;
            deathRootLocked = true;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            rb.position = deathRootPosition;
        }
        else
        {
            // [Codex Boss Death Root Lock] Rigidbody2D가 없어도 루트 Transform이 Death 중 내려가지 않게 현재 위치를 잠급니다.
            deathRootPosition = transform.position;
            deathRootLocked = true;
        }

        // [Codex Boss Death Collider Keep] 죽은 뒤 바닥을 통과하지 않도록 Collider2D는 끄지 않습니다.
        // 공격/피격 중단은 isDead와 컨트롤러 비활성화로 처리합니다.

        // [Codex Boss Death Single Play] 사라지는 연출 없이 Death 애니메이션만 1회 실행한다.
        PlayDeathAnimation();
        yield break;
    }
    private void PlayDeathAnimation()
    {
        if (deathAnimationStarted)
            return;

        deathAnimationStarted = true;

        if (animator != null)
        {
            // [Codex Boss Death Single Play] 공격/캐스팅 Action이 Death 전환을 막지 않게 끄고 속도를 정상화한다.
            animator.speed = 1f;
            animator.SetBool("Action", false);
        }

        if (animationManager != null)
        {
            // [Codex Boss Death Single Play] HeroEditor 정식 경로로 State=Death를 넣어 빈 Death 상태를 직접 잡지 않는다.
            animationManager.enabled = true;
            animationManager.SetState(CharacterState.Death);
        }

        if (animator != null)
            StartCoroutine(CoFreezeCurrentDeathAfterOnePlay(animator));
    }

    private IEnumerator CoFreezeCurrentDeathAfterOnePlay(Animator targetAnimator)
    {
        // [Codex Boss Death Single Play] Death 전환이 일어난 뒤 현재 Death 상태가 1회 끝나면 마지막 자세에서 멈춘다.
        if (targetAnimator == null)
            yield break;

        if (deathFreezeCrossFadeBuffer > 0f)
            yield return new WaitForSeconds(deathFreezeCrossFadeBuffer);

        float elapsed = 0f;
        while (targetAnimator != null)
        {
            for (int layer = 0; layer < targetAnimator.layerCount; layer++)
            {
                AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(layer);
                if (stateInfo.IsName("Death") || stateInfo.IsName("Complex.Death") || stateInfo.shortNameHash == Animator.StringToHash("Death"))
                {
                    if (stateInfo.normalizedTime >= 1f)
                    {
                        targetAnimator.speed = 0f;
                        yield break;
                    }
                }
            }

            elapsed += Time.deltaTime;
            if (elapsed >= deathFadeDelay)
            {
                targetAnimator.speed = 0f;
                yield break;
            }

            yield return null;
        }
    }

    private void SyncHpUI()
    {
        if (hpSlider == null) return;

        float ratio = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;
        hpSlider.value = ratio;
    }

    private void InitializeHpIfNeeded()
    {
        if (hasInitializedHp)
            return;

        currentHp = startHpOverride >= 0 ? Mathf.Clamp(startHpOverride, 0, maxHp) : maxHp;
        hasInitializedHp = true;
    }

    private void OnSyncedHpChanged(int previousValue, int newValue)
    {
        ApplySyncedHp(newValue);
    }

    private void ApplySyncedHp(int hp)
    {
        // [Codex Boss Network HP] 서버 HP 변경을 모든 클라이언트의 보스 UI와 페이즈 판정에 반영합니다.
        currentHp = Mathf.Clamp(hp, 0, maxHp);
        SyncHpUI();

        if (currentHp <= 0 && !isDead)
        {
            isDead = true;
            StartCoroutine(CoDie());
        }
    }

    public void HealBossHp(int amount)
    {
        // [Codex Boss Heal Cast] 보스 회복 패턴 전용 HP 회복입니다. 최대 HP를 넘지 않도록 제한합니다.
        if (isDead || amount <= 0)
            return;

        currentHp = Mathf.Min(maxHp, currentHp + amount);
        if (IsSpawned && IsServer)
            syncedHp.Value = currentHp;
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

    private bool IsNetworkClientOnly()
    {
        return NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            IsSpawned &&
            !IsServer;
    }
}
