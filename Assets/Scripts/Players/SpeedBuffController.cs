using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;

/// <summary>
/// Movement and archer attack-speed buff controller.
/// Warrior-only buffs are separated into WarriorBuffController.
/// </summary>
public class SpeedBuffController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerMovement2D playerMovement;
    [SerializeField] private PlayerAttack2D playerAttack;

    [Header("Move Speed Buff UI")]
    [SerializeField] private GameObject speedBuffIcon;
    [SerializeField] private Slider durationSlider;

    [Header("Attack Speed Buff UI")]
    [SerializeField] private GameObject attackSpeedBuffIcon;
    [SerializeField] private Slider attackDurationSlider;

    [Header("Move Speed Buff Settings")]
    [SerializeField] private float moveSpeedMultiplier = 1.5f;
    [SerializeField] private float duration = 10f;

    [Header("Attack Speed Buff Settings")]
    [SerializeField] private float attackSpeedMultiplier = 1.5f;
    [SerializeField] private float attackSpeedDuration = 10f;

    [Header("Animation")]
    [SerializeField] private AnimationManager animationManager;

    [Header("Visual Feedback")]
    // [Codex Buff Split] This controller keeps only common/archer buffs so warrior logic does not affect the archer prefab.
    [SerializeField] private SpeedBuffVisualFeedback moveSpeedVisualFeedback;
    [SerializeField] private AttackSpeedBuffVisualFeedback attackSpeedVisualFeedback;

    private Coroutine moveSpeedBuffCoroutine;
    private Coroutine attackSpeedBuffCoroutine;

    private void Awake()
    {
        if (speedBuffIcon != null)
            speedBuffIcon.SetActive(false);

        if (attackSpeedBuffIcon != null)
            attackSpeedBuffIcon.SetActive(false);

        if (moveSpeedVisualFeedback == null)
            moveSpeedVisualFeedback = GetComponent<SpeedBuffVisualFeedback>();
        if (moveSpeedVisualFeedback == null)
            moveSpeedVisualFeedback = gameObject.AddComponent<SpeedBuffVisualFeedback>();

        moveSpeedVisualFeedback.Initialize(
            playerMovement != null ? playerMovement.transform : null,
            speedBuffIcon);

        if (attackSpeedVisualFeedback == null)
            attackSpeedVisualFeedback = GetComponent<AttackSpeedBuffVisualFeedback>();
        if (attackSpeedVisualFeedback == null)
            attackSpeedVisualFeedback = gameObject.AddComponent<AttackSpeedBuffVisualFeedback>();

        attackSpeedVisualFeedback.Initialize(
            playerAttack != null ? playerAttack.transform : null,
            attackSpeedBuffIcon);
    }

    public void UseSpeedBuff()
    {
        if (playerMovement == null)
        {
            Debug.LogWarning("[SpeedBuff] PlayerMovement2D is not connected.");
            return;
        }

        if (moveSpeedBuffCoroutine != null)
            StopCoroutine(moveSpeedBuffCoroutine);

        moveSpeedBuffCoroutine = StartCoroutine(MoveSpeedBuffRoutine());
    }

    public void UseAttackSpeedBuff()
    {
        if (playerAttack == null)
        {
            Debug.LogWarning("[AttackSpeedBuff] PlayerAttack2D is not connected.");
            return;
        }

        if (attackSpeedBuffCoroutine != null)
            StopCoroutine(attackSpeedBuffCoroutine);

        attackSpeedBuffCoroutine = StartCoroutine(AttackSpeedBuffRoutine());
    }

    private IEnumerator MoveSpeedBuffRoutine()
    {
        if (speedBuffIcon != null)
            speedBuffIcon.SetActive(true);

        animationManager?.PlayMoveSpeedBuff();
        moveSpeedVisualFeedback?.PlayStart();

        playerMovement.SetMoveSpeedMultiplier(moveSpeedMultiplier);

        float remainingTime = duration;

        if (durationSlider != null)
        {
            durationSlider.minValue = 0f;
            durationSlider.maxValue = 1f;
            durationSlider.value = 1f;
        }

        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;

            if (durationSlider != null)
                durationSlider.value = Mathf.Clamp01(remainingTime / duration);

            yield return null;
        }

        playerMovement.ResetmoveSpeedMultiplier();
        moveSpeedVisualFeedback?.PlayEnd();

        if (durationSlider != null)
            durationSlider.value = 0f;

        if (speedBuffIcon != null)
            speedBuffIcon.SetActive(false);

        moveSpeedBuffCoroutine = null;
    }

    private IEnumerator AttackSpeedBuffRoutine()
    {
        if (attackSpeedBuffIcon != null)
            attackSpeedBuffIcon.SetActive(true);

        animationManager?.PlayAttackSpeedBuff();
        playerAttack.SetAttackSpeedMultiplier(attackSpeedMultiplier);
        attackSpeedVisualFeedback?.PlayStart();

        float remainingTime = attackSpeedDuration;

        if (attackDurationSlider != null)
        {
            attackDurationSlider.minValue = 0f;
            attackDurationSlider.maxValue = 1f;
            attackDurationSlider.value = 1f;
        }

        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;

            if (attackDurationSlider != null)
                attackDurationSlider.value = Mathf.Clamp01(remainingTime / attackSpeedDuration);

            yield return null;
        }

        playerAttack.ResetAttackSpeedMultiplier();
        attackSpeedVisualFeedback?.PlayEnd();

        if (attackDurationSlider != null)
            attackDurationSlider.value = 0f;

        if (attackSpeedBuffIcon != null)
            attackSpeedBuffIcon.SetActive(false);

        attackSpeedBuffCoroutine = null;
    }

    private void OnDisable()
    {
        // [Codex Buff Split] Restore common/archer buff values when this object is disabled.
        if (playerMovement != null)
            playerMovement.ResetmoveSpeedMultiplier();

        if (playerAttack != null)
            playerAttack.ResetAttackSpeedMultiplier();

        moveSpeedVisualFeedback?.PlayEnd();
        attackSpeedVisualFeedback?.PlayEnd();
    }
}
