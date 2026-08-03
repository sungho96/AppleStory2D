using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Warrior-only buff controller.
/// Anger increases melee damage and plays a red rage visual effect.
/// </summary>
public class WarriorBuffController : MonoBehaviour
{
    [Header("Warrior")]
    [SerializeField] private WarriorAttack2D warriorAttack;

    [Header("Anger Buff UI")]
    [SerializeField] private GameObject angerBuffIcon;
    [SerializeField] private Slider angerDurationSlider;
    [SerializeField] private Sprite angerBuffSprite;

    [Header("Anger Buff Settings")]
    [SerializeField] private float angerDamageMultiplier = 1.5f;
    [SerializeField] private float angerDuration = 10f;

    [Header("Visual Feedback")]
    [SerializeField] private AngerBuffVisualFeedback angerVisualFeedback;

    private Coroutine angerBuffCoroutine;

    private void Awake()
    {
        if (warriorAttack == null)
            warriorAttack = GetComponent<WarriorAttack2D>();

        AutoBindAngerBuffUI();

        if (angerBuffIcon != null)
            angerBuffIcon.SetActive(false);

        if (angerVisualFeedback == null)
            angerVisualFeedback = GetComponent<AngerBuffVisualFeedback>();
        if (angerVisualFeedback == null)
            angerVisualFeedback = gameObject.AddComponent<AngerBuffVisualFeedback>();

        // [Codex Warrior Buff] Keep the rage effect attached to the warrior, not to the archer/shared buff controller.
        Transform target = warriorAttack != null ? warriorAttack.transform : transform;
        angerVisualFeedback.Initialize(target, angerBuffIcon);
    }

    private void AutoBindAngerBuffUI()
    {
        if (angerBuffIcon == null)
            angerBuffIcon = GameObject.Find("AngerBuffIcon");

        if (angerDurationSlider == null && angerBuffIcon != null)
            angerDurationSlider = angerBuffIcon.GetComponentInChildren<Slider>(true);

        if (angerBuffIcon != null && angerDurationSlider != null)
            return;

        GameObject sourceIcon = GameObject.Find("AttackBuffIcon");
        if (sourceIcon == null)
            return;

        Transform buffPanel = GameObject.Find("BuffPanel")?.transform;
        Transform iconParent = buffPanel != null ? buffPanel : sourceIcon.transform.parent;

        // [Codex Warrior Buff UI] Clone into BuffPanel and copy the existing icon size so anger matches other buff icons.
        GameObject clonedIcon = Instantiate(sourceIcon, iconParent);
        clonedIcon.name = "AngerBuffIcon";
        RectTransform clonedRect = clonedIcon.transform as RectTransform;
        RectTransform sourceRect = sourceIcon.transform as RectTransform;
        if (clonedRect != null && sourceRect != null)
        {
            clonedRect.anchorMin = sourceRect.anchorMin;
            clonedRect.anchorMax = sourceRect.anchorMax;
            clonedRect.pivot = sourceRect.pivot;
            clonedRect.sizeDelta = sourceRect.sizeDelta;
            clonedRect.localScale = sourceRect.localScale;
            clonedRect.anchoredPosition = GetNextBuffIconPosition(sourceRect);
        }

        angerBuffIcon = clonedIcon;
        angerDurationSlider = clonedIcon.GetComponentInChildren<Slider>(true);

        Image clonedImage = clonedIcon.GetComponent<Image>();
        if (clonedImage != null && angerBuffSprite != null)
            clonedImage.sprite = angerBuffSprite;
    }

    private Vector2 GetNextBuffIconPosition(RectTransform sourceRect)
    {
        float spacing = Mathf.Max(48f, sourceRect.rect.width * 0.32f);
        float rightMostX = sourceRect.anchoredPosition.x;
        Transform parent = sourceRect.parent;

        if (parent != null)
        {
            foreach (RectTransform child in parent.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == sourceRect || child.parent != parent)
                    continue;

                if (child.name.EndsWith("BuffIcon"))
                    rightMostX = Mathf.Max(rightMostX, child.anchoredPosition.x);
            }
        }

        return new Vector2(rightMostX + spacing, sourceRect.anchoredPosition.y);
    }

    public void UseAngerBuff()
    {
        if (warriorAttack == null)
        {
            Debug.LogWarning("[AngerBuff] WarriorAttack2D is not connected.");
            return;
        }

        if (angerBuffCoroutine != null)
            StopCoroutine(angerBuffCoroutine);

        angerBuffCoroutine = StartCoroutine(AngerBuffRoutine());
    }

    private IEnumerator AngerBuffRoutine()
    {
        if (angerBuffIcon != null)
            angerBuffIcon.SetActive(true);

        warriorAttack.SetDamageMultiplier(angerDamageMultiplier);
        angerVisualFeedback?.PlayStart();

        float remainingTime = angerDuration;

        if (angerDurationSlider != null)
        {
            angerDurationSlider.minValue = 0f;
            angerDurationSlider.maxValue = 1f;
            angerDurationSlider.value = 1f;
        }

        while (remainingTime > 0f)
        {
            remainingTime -= Time.deltaTime;

            if (angerDurationSlider != null)
                angerDurationSlider.value = Mathf.Clamp01(remainingTime / angerDuration);

            yield return null;
        }

        EndAngerBuff();
    }

    private void EndAngerBuff()
    {
        if (warriorAttack != null)
            warriorAttack.ResetDamageMultiplier();

        angerVisualFeedback?.PlayEnd();

        if (angerDurationSlider != null)
            angerDurationSlider.value = 0f;

        if (angerBuffIcon != null)
            angerBuffIcon.SetActive(false);

        angerBuffCoroutine = null;
    }

    private void OnDisable()
    {
        // [Codex Warrior Buff] Prevent damage multiplier from staying active after disabling or scene changes.
        EndAngerBuff();
    }
}
