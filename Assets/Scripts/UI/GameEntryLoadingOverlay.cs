using TMPro;
using UnityEngine;

public class GameEntryLoadingOverlay : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup overlayGroup;
    [SerializeField] private RectTransform loadingSwirlRoot;
    [SerializeField] private RectTransform swirlImage;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Intro")]
    [SerializeField] private float introDuration = 0.3f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = -110f;

    private float introElapsed;
    private float hideElapsed;
    private bool showing;
    private bool hiding;
    private bool pulseRequested;
    private float pulseElapsed;
    private const float PulseDuration = 0.35f;
    private const float HideDuration = 0.35f;

    public void Initialize(CanvasGroup group, RectTransform swirlRoot, RectTransform swirl, TextMeshProUGUI text)
    {
        overlayGroup = group;
        loadingSwirlRoot = swirlRoot;
        swirlImage = swirl;
        statusText = text;
        HideImmediate();
    }

    public void Show(string message)
    {
        gameObject.SetActive(true);
        showing = true;
        hiding = false;
        pulseRequested = false;
        pulseElapsed = 0f;
        introElapsed = 0f;
        SetStatus(message);

        if (overlayGroup != null)
            overlayGroup.alpha = 0f;

        if (loadingSwirlRoot != null)
            loadingSwirlRoot.localScale = Vector3.one * 0.8f;
    }

    public void HideImmediate()
    {
        showing = false;
        hiding = false;
        pulseRequested = false;

        if (overlayGroup != null)
            overlayGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    public void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    public void HideSmooth()
    {
        if (!gameObject.activeSelf)
            return;

        showing = false;
        hiding = true;
        hideElapsed = 0f;
    }

    public void PlaySuccessPulse()
    {
        pulseRequested = true;
        pulseElapsed = 0f;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        RotateSwirls(deltaTime);

        if (showing)
            UpdateIntro(deltaTime);

        if (hiding)
            UpdateHide(deltaTime);

        if (pulseRequested)
            UpdatePulse(deltaTime);
    }

    private void RotateSwirls(float deltaTime)
    {
        if (swirlImage != null)
            swirlImage.Rotate(0f, 0f, rotationSpeed * deltaTime);
    }

    private void UpdateIntro(float deltaTime)
    {
        introElapsed += deltaTime;
        float t = introDuration <= 0f ? 1f : Mathf.Clamp01(introElapsed / introDuration);
        float eased = SmoothEaseInOut(t);

        if (overlayGroup != null)
            overlayGroup.alpha = eased;

        if (loadingSwirlRoot != null && !pulseRequested)
            loadingSwirlRoot.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, eased);

        if (t >= 1f)
            showing = false;
    }

    private void UpdateHide(float deltaTime)
    {
        hideElapsed += deltaTime;
        float t = Mathf.Clamp01(hideElapsed / HideDuration);
        float eased = SmoothEaseInOut(t);

        if (overlayGroup != null)
            overlayGroup.alpha = 1f - eased;

        if (loadingSwirlRoot != null)
            loadingSwirlRoot.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, eased);

        if (t >= 1f)
            HideImmediate();
    }

    private void UpdatePulse(float deltaTime)
    {
        pulseElapsed += deltaTime;
        float t = Mathf.Clamp01(pulseElapsed / PulseDuration);
        float scale = t < 0.5f
            ? Mathf.Lerp(1f, 1.08f, SmoothEaseInOut(t / 0.5f))
            : Mathf.Lerp(1.08f, 1f, SmoothEaseInOut((t - 0.5f) / 0.5f));

        if (loadingSwirlRoot != null)
            loadingSwirlRoot.localScale = Vector3.one * scale;

        if (t >= 1f)
            pulseRequested = false;
    }

    private static float SmoothEaseInOut(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }
}
