using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>이동속도 버프의 발동 플래시, HUD 맥동, 이동 잔상을 담당합니다.</summary>
public class SpeedBuffVisualFeedback : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color flashColor = new Color(0.35f, 0.95f, 1f, 1f);
    [Tooltip("잔상은 캐릭터보다 연하게 보여야 하므로 낮은 알파값을 사용합니다.")]
    // [잔상 가시성 조정] 배경에서도 잔상의 형태를 알아볼 수 있도록 투명도를 소폭 높였습니다.
    [SerializeField] private Color afterimageColor = new Color(0.55f, 0.9f, 1f, 0.38f);

    [Header("Afterimage")]
    [SerializeField, Min(0.02f)] private float interval = 0.08f;
    [SerializeField, Min(0.05f)] private float lifetime = 0.28f;
    [SerializeField, Min(0.01f)] private float minimumMoveDistance = 0.04f;

    private readonly List<SpriteRenderer> sources = new List<SpriteRenderer>();
    private readonly List<Color> originalColors = new List<Color>();
    private Transform player;
    private RectTransform icon;
    private Vector3 iconBaseScale = Vector3.one;
    private Vector3 previousPosition;
    private int afterimageSortingOrder;
    private float timer;
    private bool playing;
    private Coroutine iconRoutine;
    private Coroutine flashRoutine;

    public void Initialize(Transform playerTransform, GameObject buffIcon)
    {
        player = playerTransform;
        icon = buffIcon != null ? buffIcon.GetComponent<RectTransform>() : null;
        if (icon != null) iconBaseScale = icon.localScale;

        sources.Clear();
        originalColors.Clear();
        if (player == null) return;

        foreach (SpriteRenderer source in player.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sources.Add(source);
            originalColors.Add(source.color);
        }

        CalculateAfterimageSortingOrder();
    }

    public void PlayStart()
    {
        if (player == null) return;
        playing = true;
        previousPosition = player.position;
        timer = 0f;

        // [이동속도 버프 연출 추가] 재사용 시 기존 연출을 정리하고 발동 피드백을 처음부터 재생합니다.
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashPlayer());
        if (iconRoutine != null) StopCoroutine(iconRoutine);
        iconRoutine = StartCoroutine(AnimateIcon());
        CreateAfterimage(1.25f);
    }

    public void PlayEnd()
    {
        playing = false;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = null;
        if (iconRoutine != null) StopCoroutine(iconRoutine);
        iconRoutine = null;
        if (icon != null) icon.localScale = iconBaseScale;
        RestoreColors();
    }

    private void Update()
    {
        if (!playing || player == null) return;

        // [이동속도 버프 연출 추가] 정지 중에는 잔상을 만들지 않아 화면이 지저분해지는 것을 막습니다.
        timer -= Time.deltaTime;
        if (timer <= 0f && Vector3.Distance(player.position, previousPosition) >= minimumMoveDistance)
        {
            CreateAfterimage(1f);
            timer = interval;
            previousPosition = player.position;
        }
    }

    private IEnumerator FlashPlayer()
    {
        const float duration = 0.24f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float strength = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * 0.8f;
            for (int i = 0; i < sources.Count; i++)
                if (sources[i] != null) sources[i].color = Color.Lerp(originalColors[i], flashColor, strength);
            yield return null;
        }
        RestoreColors();
        flashRoutine = null;
    }

    private IEnumerator AnimateIcon()
    {
        if (icon == null) yield break;
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

    private void CreateAfterimage(float sizeMultiplier)
    {
        GameObject ghostRoot = new GameObject("SpeedBuffAfterimage");
        SortingGroup sortingGroup = ghostRoot.AddComponent<SortingGroup>();
        sortingGroup.sortingLayerID = sources.Count > 0 ? sources[0].sortingLayerID : 0;
        sortingGroup.sortingOrder = afterimageSortingOrder;

        List<SpriteRenderer> ghosts = new List<SpriteRenderer>();

        foreach (SpriteRenderer source in sources)
        {
            if (source == null || !source.enabled || source.sprite == null) continue;
            GameObject ghostObject = new GameObject(source.gameObject.name + "_Ghost");
            ghostObject.transform.SetParent(ghostRoot.transform);
            ghostObject.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            ghostObject.transform.localScale = source.transform.lossyScale * sizeMultiplier;

            SpriteRenderer ghost = ghostObject.AddComponent<SpriteRenderer>();
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.flipY = source.flipY;
            ghost.sharedMaterial = source.sharedMaterial;
            ghost.sortingLayerID = source.sortingLayerID;
            // [잔상 정렬 수정] SortingGroup 안에서는 원본 파츠 순서를 그대로 유지합니다.
            ghost.sortingOrder = source.sortingOrder;
            ghost.color = afterimageColor;
            ghosts.Add(ghost);
        }

        if (ghosts.Count == 0)
        {
            Destroy(ghostRoot);
            return;
        }

        StartCoroutine(FadeGhostGroup(ghostRoot, ghosts));
    }

    private void CalculateAfterimageSortingOrder()
    {
        if (sources.Count == 0)
        {
            afterimageSortingOrder = 0;
            return;
        }

        int minimumOrder = int.MaxValue;

        foreach (SpriteRenderer source in sources)
        {
            if (source == null) continue;
            minimumOrder = Mathf.Min(minimumOrder, source.sortingOrder);
        }

        // [잔상 정렬 수정] 잔상 묶음을 캐릭터의 가장 뒤쪽 파츠 바로 한 단계 뒤에 둡니다.
        afterimageSortingOrder = minimumOrder == int.MaxValue ? 0 : minimumOrder - 1;
    }

    private IEnumerator FadeGhostGroup(GameObject ghostRoot, List<SpriteRenderer> ghosts)
    {
        float elapsed = 0f;
        while (elapsed < lifetime && ghostRoot != null)
        {
            elapsed += Time.deltaTime;
            float alphaRatio = 1f - Mathf.Clamp01(elapsed / lifetime);

            foreach (SpriteRenderer ghost in ghosts)
            {
                if (ghost == null) continue;
                Color color = afterimageColor;
                color.a = afterimageColor.a * alphaRatio;
                ghost.color = color;
            }

            yield return null;
        }

        if (ghostRoot != null) Destroy(ghostRoot);
    }

    private void RestoreColors()
    {
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null) sources[i].color = originalColors[i];
    }

    private void OnDisable() => PlayEnd();
}
