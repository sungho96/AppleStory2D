using UnityEngine;

public class GameEntryLogoIntroAnimator : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform logoRoot;

    [Header("Intro")]
    [SerializeField] private float introDuration = 1.4f;
    [SerializeField] private float startScale = 1.5f;
    [SerializeField] private float overshootScale = 3f;
    [SerializeField] private float targetScale = 2f;

    private float baseZScale = 1f;
    private float elapsed;
    private bool finished;
    private bool initialized;

    private void Awake()
    {
        InitializeIntro();
    }

    private void OnEnable()
    {
        InitializeIntro();
    }

    private void Start()
    {
        InitializeIntro();
    }

    private void InitializeIntro()
    {
        if (logoRoot == null)
            logoRoot = transform as RectTransform;

        if (logoRoot == null)
            return;

        if (!initialized)
            baseZScale = logoRoot.localScale.z;

        elapsed = 0f;
        finished = false;
        initialized = true;

        ApplyScale(startScale);
    }

    private void Update()
    {
        if (finished || logoRoot == null)
            return;

        elapsed += Time.unscaledDeltaTime;
        float normalizedTime = introDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / introDuration);

        // [Codex GameEntry Logo] Smoothly grows past the target, then eases back to the fixed logo size.
        if (normalizedTime < 0.65f)
        {
            float scaleT = SmoothEaseInOut(normalizedTime / 0.65f);
            ApplyScale(Mathf.LerpUnclamped(startScale, overshootScale, scaleT));
            return;
        }

        float settleT = SmoothEaseInOut((normalizedTime - 0.65f) / 0.35f);
        ApplyScale(Mathf.LerpUnclamped(overshootScale, targetScale, settleT));

        if (normalizedTime >= 1f)
        {
            ApplyScale(targetScale);
            finished = true;
        }
    }

    private void ApplyScale(float scale)
    {
        if (logoRoot != null)
            logoRoot.localScale = new Vector3(scale, scale, baseZScale);
    }

    private static float SmoothEaseInOut(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }
}
