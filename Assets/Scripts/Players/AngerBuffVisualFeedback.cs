using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AngerBuffVisualFeedback : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color flashColor = new Color(1f, 0.18f, 0.06f, 1f);
    [SerializeField] private Color auraColor = new Color(1f, 0.08f, 0.02f, 0.22f);
    [SerializeField] private Color flameColor = new Color(1f, 0.24f, 0.03f, 0.68f);

    [Header("Rage Aura")]
    [SerializeField, Min(4)] private int flameCount = 12;
    [SerializeField, Min(0.04f)] private float spawnInterval = 0.055f;
    [SerializeField] private Vector2 widthRange = new Vector2(-0.42f, 0.42f);
    [SerializeField] private Vector2 heightRange = new Vector2(0.05f, 1.35f);
    [SerializeField] private Vector2 lifetimeRange = new Vector2(0.38f, 0.62f);

    private sealed class Flame
    {
        public GameObject gameObject;
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float elapsed;
        public float lifetime;
        public float width;
        public bool active;
    }

    private readonly List<SpriteRenderer> sources = new List<SpriteRenderer>();
    private readonly List<Color> originalColors = new List<Color>();
    private readonly List<Flame> flames = new List<Flame>();

    private Transform player;
    private RectTransform icon;
    private Vector3 iconBaseScale = Vector3.one;
    private Sprite softSprite;
    private SpriteRenderer backAura;
    private SpriteRenderer frontAura;
    private bool playing;
    private float spawnTimer;
    private int nextFlameIndex;
    private Coroutine flashRoutine;
    private Coroutine iconRoutine;

    public void Initialize(Transform playerTransform, GameObject buffIcon)
    {
        player = playerTransform;
        icon = buffIcon != null ? buffIcon.GetComponent<RectTransform>() : null;
        if (icon != null)
            iconBaseScale = icon.localScale;

        sources.Clear();
        originalColors.Clear();

        if (playerTransform == null)
            return;

        foreach (SpriteRenderer source in playerTransform.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sources.Add(source);
            originalColors.Add(source.color);
        }

        CreateVisualPool();
    }

    public void PlayStart()
    {
        playing = true;
        spawnTimer = 0f;
        nextFlameIndex = 0;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashPlayer());

        if (iconRoutine != null)
            StopCoroutine(iconRoutine);
        iconRoutine = StartCoroutine(AnimateIcon());

        SetAuraVisible(true);
    }

    public void PlayEnd()
    {
        playing = false;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = null;

        if (iconRoutine != null)
            StopCoroutine(iconRoutine);
        iconRoutine = null;

        if (icon != null)
            icon.localScale = iconBaseScale;

        HideAllFlames();
        SetAuraVisible(false);
        RestoreColors();
    }

    private void Update()
    {
        if (!playing || player == null)
            return;

        UpdateAura();

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnFlame();
            spawnTimer = spawnInterval * Random.Range(0.85f, 1.25f);
        }

        UpdateFlames();
    }

    private IEnumerator FlashPlayer()
    {
        const float duration = 0.22f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * 0.65f;

            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null)
                    sources[i].color = Color.Lerp(originalColors[i], flashColor, strength);
            }

            yield return null;
        }

        RestoreColors();
        flashRoutine = null;
    }

    private IEnumerator AnimateIcon()
    {
        if (icon == null)
            yield break;

        // [Codex Anger Buff UI] Match the existing speed/attack buff icon punch and idle pulse exactly.
        float elapsed = 0f;
        const float punchDuration = 0.35f;

        while (elapsed < punchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            icon.localScale = iconBaseScale * (1f + Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.65f);
            yield return null;
        }

        while (playing)
        {
            icon.localScale = iconBaseScale * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f);
            yield return null;
        }

        icon.localScale = iconBaseScale;
        iconRoutine = null;
    }

    private void CreateVisualPool()
    {
        if (player == null || softSprite != null)
            return;

        softSprite = CreateSoftCircleSprite();

        int sortingLayerId = sources.Count > 0 && sources[0] != null ? sources[0].sortingLayerID : 0;
        int baseSortingOrder = 1;
        foreach (SpriteRenderer source in sources)
        {
            if (source != null)
                baseSortingOrder = Mathf.Max(baseSortingOrder, source.sortingOrder);
        }

        backAura = CreateEffectRenderer("AngerBuffBackAura", baseSortingOrder - 2, sortingLayerId);
        frontAura = CreateEffectRenderer("AngerBuffFrontAura", baseSortingOrder + 2, sortingLayerId);
        SetAuraVisible(false);

        for (int i = 0; i < flameCount; i++)
        {
            SpriteRenderer flameRenderer = CreateEffectRenderer("AngerBuffFlame", baseSortingOrder + 3, sortingLayerId);
            flameRenderer.gameObject.SetActive(false);
            flames.Add(new Flame
            {
                gameObject = flameRenderer.gameObject,
                transform = flameRenderer.transform,
                renderer = flameRenderer
            });
        }
    }

    private SpriteRenderer CreateEffectRenderer(string objectName, int sortingOrder, int sortingLayerId)
    {
        GameObject effectObject = new GameObject(objectName);
        effectObject.transform.SetParent(player, false);

        SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
        renderer.sprite = softSprite;
        renderer.color = Color.clear;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void UpdateAura()
    {
        if (backAura == null || frontAura == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.time * 5.5f) * 0.055f;
        backAura.transform.localPosition = new Vector3(0f, 0.72f, 0f);
        backAura.transform.localScale = new Vector3(1.55f * pulse, 2.05f * pulse, 1f);
        backAura.color = auraColor;

        float frontPulse = 1f + Mathf.Sin(Time.time * 7f + 1.2f) * 0.04f;
        frontAura.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        frontAura.transform.localScale = new Vector3(1.05f * frontPulse, 1.65f * frontPulse, 1f);
        Color frontColor = auraColor;
        frontColor.a *= 0.45f;
        frontAura.color = frontColor;
    }

    private void SpawnFlame()
    {
        if (flames.Count == 0)
            return;

        Flame flame = flames[nextFlameIndex];
        nextFlameIndex = (nextFlameIndex + 1) % flames.Count;

        float startX = Random.Range(widthRange.x, widthRange.y);
        float startY = Random.Range(heightRange.x, heightRange.y);
        float driftX = Random.Range(-0.08f, 0.08f);
        float rise = Random.Range(0.42f, 0.78f);

        flame.startPosition = new Vector3(startX, startY, 0f);
        flame.endPosition = flame.startPosition + new Vector3(driftX, rise, 0f);
        flame.elapsed = 0f;
        flame.lifetime = Random.Range(lifetimeRange.x, lifetimeRange.y);
        flame.width = Random.Range(0.07f, 0.12f);
        flame.active = true;
        flame.transform.localPosition = flame.startPosition;
        flame.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f));
        flame.gameObject.SetActive(true);
    }

    private void UpdateFlames()
    {
        foreach (Flame flame in flames)
        {
            if (!flame.active)
                continue;

            flame.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(flame.elapsed / flame.lifetime);
            flame.transform.localPosition = Vector3.Lerp(flame.startPosition, flame.endPosition, 1f - (1f - t) * (1f - t));
            flame.transform.localScale = new Vector3(flame.width * (1f - t * 0.35f), flame.width * 2.7f * (1f - t * 0.1f), 1f);

            Color color = flameColor;
            color.a = flameColor.a * Mathf.Sin(t * Mathf.PI) * 0.85f;
            flame.renderer.color = color;

            if (t >= 1f)
            {
                flame.active = false;
                flame.gameObject.SetActive(false);
            }
        }
    }

    private void HideAllFlames()
    {
        foreach (Flame flame in flames)
        {
            flame.active = false;
            if (flame.gameObject != null)
                flame.gameObject.SetActive(false);
        }
    }

    private void SetAuraVisible(bool visible)
    {
        if (backAura != null)
            backAura.gameObject.SetActive(visible);
        if (frontAura != null)
            frontAura.gameObject.SetActive(visible);
    }

    private void RestoreColors()
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] != null)
                sources[i].color = originalColors[i];
        }
    }

    private Sprite CreateSoftCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "AngerBuffSoftCircleTexture";

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    private void OnDisable()
    {
        PlayEnd();
    }

    private void OnDestroy()
    {
        if (softSprite != null)
            Destroy(softSprite.texture);
    }
}
