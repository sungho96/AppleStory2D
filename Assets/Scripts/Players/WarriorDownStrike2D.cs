using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorDownStrike2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerHealth2D playerHealth;

    [Header("Skill")]
    [SerializeField] private KeyCode fallbackKey = KeyCode.None;
    [SerializeField] private int damage = 35;
    [SerializeField] private float cooldown = 1.4f;
    [SerializeField] private Vector2 hitBoxSize = new Vector2(1.55f, 1f);
    [SerializeField] private Vector2 hitBoxOffset = new Vector2(0.85f, 0.05f);
    [SerializeField] private LayerMask enemyMask;

    [Header("Animator Blend")]
    [SerializeField] private string chargeStateName = "Charge2H";
    [SerializeField] private string slashStateName = "Slash2H";
    [SerializeField] private string slashTriggerName = "";
    [SerializeField] private float chargeFadeDuration = 0.05f;
    [SerializeField] private float chargeHoldDuration = 0.24f;
    [SerializeField] private float slashFadeDuration = 0.02f;
    [SerializeField, Range(0f, 1f)] private float slashStartNormalizedTime = 0.64f;
    [SerializeField] private float hitDelay = 0.06f;
    [SerializeField] private float finishDelay = 0.32f;

    private bool isUsingSkill;
    private float nextUseTime;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth2D>();
    }

    private void Update()
    {
        if (fallbackKey == KeyCode.None || !Input.GetKeyDown(fallbackKey))
            return;

        UseDownStrike();
    }

    public void UseDownStrike()
    {
        if (isUsingSkill || Time.time < nextUseTime)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        StartCoroutine(DownStrikeRoutine());
    }

    private IEnumerator DownStrikeRoutine()
    {
        isUsingSkill = true;

        // [Codex Warrior DownStrike 3.0] Use the asset-provided 2H charge and slash animations instead of manual transform posing.
        PlayAnimatorState(chargeStateName, chargeFadeDuration);
        yield return new WaitForSeconds(chargeHoldDuration);

        PlaySlash2H();
        yield return new WaitForSeconds(hitDelay);

        ApplyDownStrikeHit();
        yield return new WaitForSeconds(finishDelay);

        nextUseTime = Time.time + cooldown;
        isUsingSkill = false;
    }

    private void PlaySlash2H()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(slashTriggerName))
        {
            animator.ResetTrigger(slashTriggerName);
            animator.SetTrigger(slashTriggerName);
        }

        PlayAnimatorState(slashStateName, slashFadeDuration, slashStartNormalizedTime);
    }

    private void PlayAnimatorState(string stateName, float fadeDuration, float normalizedStartTime = 0f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        for (int layer = animator.layerCount - 1; layer >= 0; layer--)
        {
            if (!animator.HasState(layer, stateHash))
                continue;

            // [Codex Warrior DownStrike 3.1] Slash2H starts after its wind-up so Charge2H does not raise the sword twice.
            animator.CrossFade(stateHash, fadeDuration, layer, normalizedStartTime);
            return;
        }

        for (int layer = animator.layerCount - 1; layer >= 0; layer--)
        {
            string qualifiedName = animator.GetLayerName(layer) + "." + stateName;
            int qualifiedHash = Animator.StringToHash(qualifiedName);
            if (!animator.HasState(layer, qualifiedHash))
                continue;

            animator.CrossFade(qualifiedHash, fadeDuration, layer, normalizedStartTime);
            return;
        }
    }

    private void ApplyDownStrikeHit()
    {
        float dir = playerController != null
            ? playerController.GetHorizontalFacingDir()
            : 1f;

        Vector2 center = (Vector2)transform.position + new Vector2(
            hitBoxOffset.x * dir,
            hitBoxOffset.y);

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
            if (enemyHealth == null || !damagedEnemies.Add(enemyHealth))
                continue;

            enemyHealth.TakeDamage(damage, dir);
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

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.8f);
        Gizmos.DrawWireCube(center, hitBoxSize);
    }
}
