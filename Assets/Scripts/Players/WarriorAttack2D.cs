using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전사 기본 공격 전용 처리.
/// - 궁수 PlayerAttack2D를 수정하지 않고 전사용 평타만 별도로 담당합니다.
/// - 스킬/버프는 다음 단계에서 이 스크립트에 안전하게 확장할 예정입니다.
/// </summary>
public class WarriorAttack2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerHealth2D playerHealth;

    [Header("Basic Attack")]
    [SerializeField] private KeyCode attackKey = KeyCode.LeftControl;
    [SerializeField] private int damage = 18;
    [SerializeField] private float attackDelay = 0.45f;
    [SerializeField] private float hitStartDelay = 0.12f;
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1.25f, 1.0f);
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0.75f, 0.15f);
    [SerializeField] private LayerMask enemyMask;

    [Header("Animator")]
    [SerializeField] private string attackTriggerName = "Slash1H";

    [Header("Weapon Scale")]
    [SerializeField] private Vector3 attackWeaponScale = new Vector3(1f, 1.5f, 1f);
    [SerializeField] private string[] weaponScaleLockPaths =
    {
        "Left/UpperBody/ArmRAnchor/ArmR/HandR/PrimaryWeapon",
        "Right/UpperBody/ArmRAnchor/ArmR/HandR/PrimaryWeapon"
    };

    private bool isAttacking;
    private bool hasAppliedHitThisAttack;
    private float attackSpeedMultiplier = 1f;
    private float damageMultiplier = 1f;
    private Transform[] weaponScaleTargets;

    public bool IsAttacking => isAttacking;
    public bool CanUseBasicAttack => !isAttacking &&
        (playerHealth == null || !playerHealth.IsDead);

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth2D>();

        CacheWeaponScaleTargets();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (!Input.GetKeyDown(attackKey))
            return;

        if (!CanUseBasicAttack)
            return;

        StartBasicAttack();
    }

    private void LateUpdate()
    {
        if (!isAttacking)
            return;

        ApplyAttackWeaponScale();
    }

    private IEnumerator BasicAttackRoutine()
    {
        isAttacking = true;
        hasAppliedHitThisAttack = false;

        // [Codex Warrior Basic] 전사 프리팹의 기존 Animator 상태를 우선 활용하기 위해 Trigger 이름만 Inspector에서 교체 가능하게 둡니다.
        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            animator.ResetTrigger(attackTriggerName);
            animator.SetTrigger(attackTriggerName);
        }

        yield return new WaitForSeconds(hitStartDelay);
        TryApplyBasicHitOnce();

        float remainDelay = Mathf.Max(0f, GetCurrentAttackDelay() - hitStartDelay);
        yield return new WaitForSeconds(remainDelay);

        ApplyAttackWeaponScale();
        isAttacking = false;
    }

    public void TriggerBasicAttackForCapture()
    {
        // [Codex CaptureShieldBot] 촬영용 자동 평타도 실제 입력과 같은 기본공격 시작 경로만 호출합니다.
        if (!CanUseBasicAttack)
            return;

        StartBasicAttack();
    }

    private void StartBasicAttack()
    {
        StartCoroutine(BasicAttackRoutine());
    }

    public void FireArrow()
    {
        // [Codex Warrior Basic] 기존 활 애니메이션 이벤트가 남아 있어도 전사는 화살 대신 근접 판정으로 처리합니다.
        TryApplyBasicHitOnce();
    }

    private void TryApplyBasicHitOnce()
    {
        if (hasAppliedHitThisAttack)
            return;

        hasAppliedHitThisAttack = true;
        ApplyBasicHit();
    }

    private void ApplyBasicHit()
    {
        float dir = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        Vector2 center = (Vector2)transform.position + new Vector2(
            hitBoxOffset.x * dir,
            hitBoxOffset.y);

        // [Codex Warrior Basic] Unity Physics2D 박스 판정으로 전방의 적 체력 컴포넌트만 찾아 데미지를 줍니다.
        Collider2D[] hits = Physics2D.OverlapBoxAll(
            center,
            hitBoxSize,
            0f,
            enemyMask);

        HashSet<GoblinHealth2D> damagedEnemies = new HashSet<GoblinHealth2D>();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            GoblinHealth2D enemyHealth = hits[i].GetComponentInParent<GoblinHealth2D>();
            if (enemyHealth == null)
                continue;

            if (!damagedEnemies.Add(enemyHealth))
                continue;

            int finalDamage = Mathf.RoundToInt(damage * damageMultiplier);
            enemyHealth.TakeDamage(finalDamage, dir);
        }
    }

    private float GetCurrentAttackDelay()
    {
        return attackDelay / Mathf.Max(0.1f, attackSpeedMultiplier);
    }

    public void SetAttackSpeedMultiplier(float multiplier)
    {
        attackSpeedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ResetAttackSpeedMultiplier()
    {
        attackSpeedMultiplier = 1f;
    }

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ResetDamageMultiplier()
    {
        damageMultiplier = 1f;
    }

    private void CacheWeaponScaleTargets()
    {
        if (weaponScaleLockPaths == null || weaponScaleLockPaths.Length == 0)
        {
            weaponScaleTargets = System.Array.Empty<Transform>();
            return;
        }

        weaponScaleTargets = new Transform[weaponScaleLockPaths.Length];

        for (int i = 0; i < weaponScaleLockPaths.Length; i++)
        {
            if (string.IsNullOrEmpty(weaponScaleLockPaths[i]))
                continue;

            weaponScaleTargets[i] = transform.Find(weaponScaleLockPaths[i]);
        }
    }

    private void ApplyAttackWeaponScale()
    {
        if (weaponScaleTargets == null || weaponScaleTargets.Length == 0)
            return;

        for (int i = 0; i < weaponScaleTargets.Length; i++)
        {
            if (weaponScaleTargets[i] == null)
                continue;

            // [Codex Warrior Weapon Scale] Slash animation overwrites weapon scale, so restore only the requested attack scale.
            weaponScaleTargets[i].localScale = attackWeaponScale;
        }
    }

    private void OnDrawGizmosSelected()
    {
        float dir = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        Vector2 center = (Vector2)transform.position + new Vector2(
            hitBoxOffset.x * dir,
            hitBoxOffset.y);

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(center, hitBoxSize);
    }
}
