using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WarriorShieldBlockVisualFeedback : MonoBehaviour
{
    private const string BarrierSpriteEditorPath =
        "Assets/Art/VFX/Warrior/Warrior_ShieldBlock_BarrierImpact.png";

    [Header("Refs")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private Transform shieldBlockVfxPoint;

    [Header("Barrier VFX")]
    [SerializeField] private Sprite barrierSprite;
    [SerializeField] private GameObject barrierPrefab;
    [SerializeField] private Vector2 barrierOffset = new Vector2(0.34f, 1.59f);
    [SerializeField] private Vector3 barrierScale = new Vector3(0.3f, 0.3f, 1f);
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float fadeOutDuration = 0.1f;
    [SerializeField] private bool flipBarrierByDirection = true;

    [Header("Sorting")]
    [SerializeField] private int sortingOrderOffset = 8;

    private GameObject barrierObject;
    private SpriteRenderer barrierRenderer;
    private Coroutine fadeRoutine;
    private int sortingLayerId;
    private int baseSortingOrder;

    private void Awake()
    {
        Initialize();
    }

    private void LateUpdate()
    {
        if (barrierObject == null || !barrierObject.activeSelf)
            return;

        UpdateBarrierTransform();
    }

    public void Initialize()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();

#if UNITY_EDITOR
        LoadDefaultSpriteInEditor();
#endif

        FindSortingReference();
        EnsureBarrierObject();
    }

    public void ShowShieldBlockBarrier()
    {
        if (barrierSprite == null && barrierPrefab == null)
            return;

        EnsureBarrierObject();
        if (barrierObject == null || barrierRenderer == null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        barrierObject.SetActive(true);
        UpdateBarrierTransform();
        fadeRoutine = StartCoroutine(FadeBarrier(true));
    }

    public void HideShieldBlockBarrier()
    {
        if (barrierObject == null || barrierRenderer == null)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeBarrier(false));
    }

    private IEnumerator FadeBarrier(bool fadeIn)
    {
        float duration = Mathf.Max(0.01f, fadeIn ? fadeInDuration : fadeOutDuration);
        float elapsed = 0f;
        float startAlpha = fadeIn ? 0f : barrierRenderer.color.a;
        float endAlpha = fadeIn ? 1f : 0f;

        // [Codex ShieldBlock VFX] 방어 판정과 분리된 로컬 Barrier만 짧게 페이드합니다.
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float smooth = Mathf.SmoothStep(0f, 1f, ratio);
            UpdateBarrierTransform(Mathf.Lerp(0.7f, 1f, fadeIn ? smooth : 1f));
            SetBarrierAlpha(Mathf.Lerp(startAlpha, endAlpha, smooth));
            yield return null;
        }

        UpdateBarrierTransform(fadeIn ? 1f : 1f);
        SetBarrierAlpha(endAlpha);

        if (!fadeIn)
            barrierObject.SetActive(false);

        fadeRoutine = null;
    }

    private void EnsureBarrierObject()
    {
        if (barrierObject != null)
            return;

        Transform parent = shieldBlockVfxPoint != null
            ? shieldBlockVfxPoint
            : transform;

        barrierObject = barrierPrefab != null
            ? Instantiate(barrierPrefab, parent)
            : new GameObject("ShieldBlockBarrierVfx");

        barrierObject.name = "ShieldBlockBarrierVfx";
        barrierObject.transform.SetParent(parent, false);
        barrierRenderer = barrierObject.GetComponentInChildren<SpriteRenderer>(true);
        if (barrierRenderer == null)
            barrierRenderer = barrierObject.AddComponent<SpriteRenderer>();

        if (barrierSprite != null)
            barrierRenderer.sprite = barrierSprite;

        barrierRenderer.sortingLayerID = sortingLayerId;
        barrierRenderer.sortingOrder = baseSortingOrder + sortingOrderOffset;
        SetBarrierAlpha(0f);
        barrierObject.SetActive(false);
    }

    private void UpdateBarrierTransform(float scaleMultiplier = 1f)
    {
        if (barrierObject == null)
            return;

        float direction = GetDirection();
        barrierObject.transform.localPosition =
            new Vector3(barrierOffset.x * direction, barrierOffset.y, 0f);
        barrierObject.transform.localRotation = Quaternion.identity;
        barrierObject.transform.localScale =
            GetDirectedScale(barrierScale * scaleMultiplier, direction);
    }

    private Vector3 GetDirectedScale(Vector3 baseScale, float direction)
    {
        if (!flipBarrierByDirection)
            return baseScale;

        return new Vector3(
            Mathf.Abs(baseScale.x) * (direction < 0f ? -1f : 1f),
            baseScale.y,
            baseScale.z);
    }

    private void SetBarrierAlpha(float alpha)
    {
        if (barrierRenderer == null)
            return;

        Color color = barrierRenderer.color;
        color.a = Mathf.Clamp01(alpha);
        barrierRenderer.color = color;
    }

    private float GetDirection()
    {
        return playerController != null && playerController.GetHorizontalFacingDir() < 0f
            ? -1f
            : 1f;
    }

    private void FindSortingReference()
    {
        SpriteRenderer reference = GetComponentInChildren<SpriteRenderer>(true);
        sortingLayerId = reference != null ? reference.sortingLayerID : 0;
        baseSortingOrder = reference != null ? reference.sortingOrder : 0;

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sortingLayerID != sortingLayerId)
                continue;

            baseSortingOrder = Mathf.Max(baseSortingOrder, renderer.sortingOrder);
        }
    }

    private void OnDisable()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = null;
        if (barrierObject != null)
            barrierObject.SetActive(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        LoadDefaultSpriteInEditor();
    }

    private void LoadDefaultSpriteInEditor()
    {
        if (barrierSprite == null)
            barrierSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BarrierSpriteEditorPath);
    }
#endif
}
