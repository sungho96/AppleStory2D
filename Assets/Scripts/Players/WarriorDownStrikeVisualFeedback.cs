using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WarriorDownStrikeVisualFeedback : MonoBehaviour
{
    private const string SlashSpriteEditorPath =
        "Assets/Art/VFX/Warrior/Warrior_DownStrike_Slash.png";
    private const string DustSpriteEditorPath =
        "Assets/Art/VFX/Warrior/Warrior_DownStrike_Dust.png";

    [Header("Refs")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private Transform downStrikeSlashVfxPoint;
    [SerializeField] private Transform downStrikeGroundVfxPoint;

    [Header("Slash VFX")]
    [SerializeField] private Sprite slashSprite;
    [SerializeField] private GameObject slashPrefab;
    [SerializeField] private Vector2 slashOffset = new Vector2(0.72f, 0.72f);
    [SerializeField] private Vector3 slashScale = new Vector3(0.3f, 0.3f, 1f);
    [SerializeField] private float slashDuration = 0.16f;
    [SerializeField] private bool flipSlashByDirection = true;

    [Header("Dust VFX")]
    [SerializeField] private Sprite dustSprite;
    [SerializeField] private GameObject dustPrefab;
    [SerializeField] private Vector2 dustOffset = new Vector2(0.08f, 0.12f);
    [SerializeField] private Vector3 dustScale = new Vector3(0.3f, 0.3f, 1f);
    [SerializeField] private float dustDuration = 0.45f;
    [SerializeField] private bool flipDustByDirection;

    [Header("Sorting")]
    [SerializeField] private int slashSortingOrderOffset = 6;
    [SerializeField] private int dustSortingOrderOffset = -1;

    private int sortingLayerId;
    private int baseSortingOrder;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController2D>();

#if UNITY_EDITOR
        LoadDefaultSpritesInEditor();
#endif

        FindSortingReference();
    }

    public void PlayDownStrikeSlashVfx()
    {
        if (slashSprite == null && slashPrefab == null)
            return;

        StartCoroutine(PlaySlashRoutine());
    }

    public void PlayDownStrikeDustVfx()
    {
        if (dustSprite == null && dustPrefab == null)
            return;

        StartCoroutine(PlayDustRoutine());
    }

    private IEnumerator PlaySlashRoutine()
    {
        float direction = GetDirection();
        Transform parent = downStrikeSlashVfxPoint != null
            ? downStrikeSlashVfxPoint
            : transform;
        GameObject effect = CreateEffect(
            "Warrior_DownStrike_SlashVFX",
            slashPrefab,
            slashSprite,
            parent,
            Vector3.zero,
            baseSortingOrder + slashSortingOrderOffset);

        if (effect == null)
            yield break;

        SpriteRenderer renderer = effect.GetComponentInChildren<SpriteRenderer>(true);
        float duration = Mathf.Max(0.01f, slashDuration);
        float elapsed = 0f;

        // [Codex DownStrike VFX] 데미지 판정과 분리된 로컬 검기만 짧게 켜고 캐릭터 Transform을 따라가게 합니다.
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            Vector3 localPosition = new Vector3(slashOffset.x * direction, slashOffset.y, 0f);
            effect.transform.localPosition = localPosition;
            effect.transform.localRotation = Quaternion.identity;
            effect.transform.localScale = GetDirectedScale(slashScale, direction, flipSlashByDirection);

            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = 1f - Mathf.SmoothStep(0f, 1f, ratio);
                renderer.color = color;
            }

            yield return null;
        }

        Destroy(effect);
    }

    private IEnumerator PlayDustRoutine()
    {
        float direction = GetDirection();
        Vector3 origin = downStrikeGroundVfxPoint != null
            ? downStrikeGroundVfxPoint.position
            : transform.position;
        Vector3 position = origin + new Vector3(dustOffset.x * direction, dustOffset.y, 0f);
        GameObject effect = CreateEffect(
            "Warrior_DownStrike_DustVFX",
            dustPrefab,
            dustSprite,
            null,
            position,
            baseSortingOrder + dustSortingOrderOffset);

        if (effect == null)
            yield break;

        SpriteRenderer renderer = effect.GetComponentInChildren<SpriteRenderer>(true);
        Vector3 directedScale = GetDirectedScale(dustScale, direction, flipDustByDirection);
        float duration = Mathf.Max(0.01f, dustDuration);
        float elapsed = 0f;

        // [Codex DownStrike VFX] 착지 먼지는 NetworkObject 없이 각 클라이언트에서 잠깐 커졌다가 알파로 사라집니다.
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float scalePulse = Mathf.Lerp(0.72f, 1.18f, Mathf.SmoothStep(0f, 1f, ratio));
            effect.transform.localScale = directedScale * scalePulse;

            if (renderer != null)
            {
                Color color = renderer.color;
                color.a = 1f - Mathf.SmoothStep(0.18f, 1f, ratio);
                renderer.color = color;
            }

            yield return null;
        }

        Destroy(effect);
    }

    private GameObject CreateEffect(
        string objectName,
        GameObject prefab,
        Sprite sprite,
        Transform parent,
        Vector3 position,
        int sortingOrder)
    {
        GameObject effect = prefab != null
            ? Instantiate(prefab, parent)
            : new GameObject(objectName);

        effect.name = objectName;
        if (parent != null)
            effect.transform.SetParent(parent, false);
        else
            effect.transform.position = position;

        SpriteRenderer renderer = effect.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer == null)
            renderer = effect.AddComponent<SpriteRenderer>();

        if (sprite != null)
            renderer.sprite = sprite;

        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, 1f);
        effect.SetActive(true);
        return effect;
    }

    private Vector3 GetDirectedScale(Vector3 baseScale, float direction, bool flipByDirection)
    {
        if (!flipByDirection)
            return baseScale;

        return new Vector3(
            Mathf.Abs(baseScale.x) * (direction < 0f ? -1f : 1f),
            baseScale.y,
            baseScale.z);
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        LoadDefaultSpritesInEditor();
    }

    private void LoadDefaultSpritesInEditor()
    {
        if (slashSprite == null)
            slashSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlashSpriteEditorPath);
        if (dustSprite == null)
            dustSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DustSpriteEditorPath);
    }
#endif
}
