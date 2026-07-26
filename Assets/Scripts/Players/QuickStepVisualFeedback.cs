using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class QuickStepVisualFeedback : MonoBehaviour
{
    [Header("Afterimage")]
    [SerializeField] private Color afterimageColor = new Color(0.66f, 0.62f, 1f, 0.52f);
    [SerializeField] private float afterimageInterval = 0.04f;
    [SerializeField] private float afterimageLifetime = 0.26f;

    [Header("Stretch")]
    [SerializeField] private Vector2 stretchScale = new Vector2(1.16f, 0.88f);
    [SerializeField] private float stretchDuration = 0.22f;

    [Header("Speed Lines")]
    [SerializeField] private Color speedLineColor = new Color(0.78f, 0.72f, 1f, 0.82f);
    [SerializeField] private int speedLineCount = 5;
    [SerializeField] private float speedLineLifetime = 0.22f;

    [Header("Sound")]
    [SerializeField, Range(0f, 1f)] private float quickStepVolume = 0.68f;
    [SerializeField] private Vector2 quickStepPitchRange = new Vector2(0.96f, 1.04f);

    private readonly List<SpriteRenderer> sources = new List<SpriteRenderer>();
    private readonly List<GameObject> spawnedEffects = new List<GameObject>();

    private Transform player;
    private Transform stretchedVisual;
    private Vector3 stretchedVisualBaseScale;
    private Sprite lineSprite;
    private float afterimageTimer;
    private float stepDirection;
    private bool playing;
    private Coroutine stretchRoutine;
    private AudioSource quickStepAudioSource;
    private AudioClip quickStepClip;

    public void Initialize(
        Transform playerTransform,
        AudioClip assignedQuickStepClip)
    {
        player = playerTransform;
        sources.Clear();

        if (player == null)
        {
            return;
        }

        foreach (SpriteRenderer source in player.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sources.Add(source);
        }

        CalculateSorting(out int sortingLayerId, out int sortingOrder);
        CreateLineSprite();
        InitializeAudio(assignedQuickStepClip);

        // [퀵 스텝 정렬 수정] 배경보다 앞이면서 캐릭터 본체 바로 뒤에 표시합니다.
        lineSortingLayerId = sortingLayerId;
        lineSortingOrder = sortingOrder;
    }

    private int lineSortingLayerId;
    private int lineSortingOrder;

    public void PlayStart(float direction)
    {
        if (player == null)
        {
            return;
        }

        stepDirection = Mathf.Sign(direction);
        playing = true;
        afterimageTimer = 0f;

        // [퀵 스텝 연출 강화] 시작 잔상을 살짝 확대해 순간적인 폭발감을 줍니다.
        CreateAfterimage(1.08f);
        CreateSpeedLines();
        StartStretch();
        PlayQuickStepSound();
    }

    public void PlayEnd()
    {
        if (!playing)
        {
            return;
        }

        playing = false;
        CreateAfterimage(0.98f);
    }

    private void Update()
    {
        if (!playing || player == null)
        {
            return;
        }

        afterimageTimer -= Time.deltaTime;
        if (afterimageTimer <= 0f)
        {
            CreateAfterimage(1.04f);
            afterimageTimer = afterimageInterval;
        }
    }

    private void StartStretch()
    {
        Transform visual = FindActiveDirectionVisual();
        if (visual == null)
        {
            return;
        }

        if (stretchRoutine != null)
        {
            StopCoroutine(stretchRoutine);
            RestoreStretchedVisual();
        }

        stretchedVisual = visual;
        stretchedVisualBaseScale = visual.localScale;
        stretchRoutine = StartCoroutine(StretchRoutine());
    }

    private IEnumerator StretchRoutine()
    {
        float elapsed = 0f;

        while (elapsed < stretchDuration && stretchedVisual != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / stretchDuration);

            // 시작 시 빠르게 늘어나고 종료 시 약한 반동과 함께 원래 크기로 돌아옵니다.
            float strength = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.25f);
            Vector3 targetScale = new Vector3(
                stretchedVisualBaseScale.x * stretchScale.x,
                stretchedVisualBaseScale.y * stretchScale.y,
                stretchedVisualBaseScale.z);

            stretchedVisual.localScale =
                Vector3.Lerp(stretchedVisualBaseScale, targetScale, strength);

            yield return null;
        }

        RestoreStretchedVisual();
        stretchRoutine = null;
    }

    private Transform FindActiveDirectionVisual()
    {
        string[] directionNames = { "Left", "Right", "Front", "Back" };

        foreach (string directionName in directionNames)
        {
            Transform candidate = player.Find(directionName);
            if (candidate != null && candidate.gameObject.activeInHierarchy)
            {
                return candidate;
            }
        }

        return null;
    }

    private void CreateAfterimage(float sizeMultiplier)
    {
        GameObject ghostRoot = new GameObject("QuickStepAfterimage");
        spawnedEffects.Add(ghostRoot);

        SortingGroup sortingGroup = ghostRoot.AddComponent<SortingGroup>();
        sortingGroup.sortingLayerID = lineSortingLayerId;
        sortingGroup.sortingOrder = lineSortingOrder;

        List<SpriteRenderer> ghosts = new List<SpriteRenderer>();

        foreach (SpriteRenderer source in sources)
        {
            if (source == null ||
                !source.enabled ||
                !source.gameObject.activeInHierarchy ||
                source.sprite == null)
            {
                continue;
            }

            GameObject ghostObject = new GameObject(source.gameObject.name + "_QuickStepGhost");
            ghostObject.transform.SetParent(ghostRoot.transform);
            ghostObject.transform.SetPositionAndRotation(
                source.transform.position,
                source.transform.rotation);
            ghostObject.transform.localScale = source.transform.lossyScale * sizeMultiplier;

            SpriteRenderer ghost = ghostObject.AddComponent<SpriteRenderer>();
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.flipY = source.flipY;
            ghost.sharedMaterial = source.sharedMaterial;
            ghost.sortingLayerID = source.sortingLayerID;
            ghost.sortingOrder = source.sortingOrder;
            ghost.color = afterimageColor;
            ghosts.Add(ghost);
        }

        if (ghosts.Count == 0)
        {
            spawnedEffects.Remove(ghostRoot);
            Destroy(ghostRoot);
            return;
        }

        StartCoroutine(FadeAfterimage(ghostRoot, ghosts));
    }

    private IEnumerator FadeAfterimage(
        GameObject ghostRoot,
        List<SpriteRenderer> ghosts)
    {
        float elapsed = 0f;

        while (elapsed < afterimageLifetime && ghostRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / afterimageLifetime);

            foreach (SpriteRenderer ghost in ghosts)
            {
                if (ghost == null)
                {
                    continue;
                }

                Color color = afterimageColor;
                color.a = afterimageColor.a * (1f - t) * (1f - t);
                ghost.color = color;
            }

            yield return null;
        }

        DestroyTrackedEffect(ghostRoot);
    }

    private void CreateSpeedLines()
    {
        if (lineSprite == null || player == null)
        {
            return;
        }

        for (int i = 0; i < speedLineCount; i++)
        {
            GameObject lineObject = new GameObject("QuickStepSpeedLine");
            spawnedEffects.Add(lineObject);

            SpriteRenderer lineRenderer = lineObject.AddComponent<SpriteRenderer>();
            lineRenderer.sprite = lineSprite;
            lineRenderer.color = speedLineColor;
            lineRenderer.sortingLayerID = lineSortingLayerId;
            lineRenderer.sortingOrder = lineSortingOrder;

            float width = Random.Range(0.52f, 0.92f);
            float height = Random.Range(0.025f, 0.05f);
            Vector2 spriteSize = lineSprite.bounds.size;
            lineObject.transform.localScale = new Vector3(
                width / spriteSize.x,
                height / spriteSize.y,
                1f);

            lineObject.transform.position = player.position + new Vector3(
                -stepDirection * Random.Range(0.18f, 0.58f),
                Random.Range(0.12f, 1.22f),
                0f);

            StartCoroutine(AnimateSpeedLine(lineObject, lineRenderer));
        }
    }

    private void InitializeAudio(AudioClip assignedQuickStepClip)
    {
        // [퀵 스텝 사운드 교체 지원] Inspector 지정 파일을 우선하고 비어 있으면 기본음을 사용합니다.
        quickStepClip = assignedQuickStepClip != null
            ? assignedQuickStepClip
            : Resources.Load<AudioClip>("Audio/SFX/QuickStep_Whoosh");

        if (quickStepAudioSource == null)
        {
            // 다른 플레이어 사운드의 설정을 바꾸지 않도록 전용 AudioSource를 사용합니다.
            quickStepAudioSource = gameObject.AddComponent<AudioSource>();
        }

        quickStepAudioSource.playOnAwake = false;
        quickStepAudioSource.loop = false;
        quickStepAudioSource.spatialBlend = 0f;
    }

    private void PlayQuickStepSound()
    {
        if (quickStepAudioSource == null || quickStepClip == null)
        {
            return;
        }

        quickStepAudioSource.pitch = Random.Range(
            quickStepPitchRange.x,
            quickStepPitchRange.y);
        quickStepAudioSource.PlayOneShot(quickStepClip, quickStepVolume);
    }

    private IEnumerator AnimateSpeedLine(
        GameObject lineObject,
        SpriteRenderer lineRenderer)
    {
        Vector3 startPosition = lineObject.transform.position;
        Vector3 endPosition =
            startPosition + Vector3.left * stepDirection * Random.Range(0.48f, 0.82f);
        float elapsed = 0f;

        while (elapsed < speedLineLifetime && lineObject != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / speedLineLifetime);
            lineObject.transform.position = Vector3.Lerp(startPosition, endPosition, t);

            Color color = speedLineColor;
            color.a = speedLineColor.a * Mathf.Sin(t * Mathf.PI);
            lineRenderer.color = color;
            yield return null;
        }

        DestroyTrackedEffect(lineObject);
    }

    private void CalculateSorting(out int sortingLayerId, out int sortingOrder)
    {
        sortingLayerId = 0;
        sortingOrder = 0;

        SortingGroup playerSortingGroup = player.GetComponentInChildren<SortingGroup>(true);
        if (playerSortingGroup != null)
        {
            // 캐릭터 묶음 전체의 바로 뒤에 배치해야 파츠 위를 덮지 않습니다.
            sortingLayerId = playerSortingGroup.sortingLayerID;
            sortingOrder = playerSortingGroup.sortingOrder - 1;
            return;
        }

        int maximumOrder = int.MinValue;

        foreach (SpriteRenderer source in sources)
        {
            if (source == null)
            {
                continue;
            }

            sortingLayerId = source.sortingLayerID;
            maximumOrder = Mathf.Max(maximumOrder, source.sortingOrder);
        }

        if (maximumOrder != int.MinValue)
        {
            // SortingGroup이 없는 캐릭터를 위한 보조값입니다.
            sortingOrder = Mathf.Max(0, maximumOrder - 1);
        }
    }

    private void CreateLineSprite()
    {
        if (lineSprite != null)
        {
            return;
        }

        lineSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(
                0f,
                0f,
                Texture2D.whiteTexture.width,
                Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        lineSprite.name = "QuickStepLineSprite";
    }

    private void RestoreStretchedVisual()
    {
        if (stretchedVisual != null)
        {
            stretchedVisual.localScale = stretchedVisualBaseScale;
        }

        stretchedVisual = null;
    }

    private void DestroyTrackedEffect(GameObject effect)
    {
        if (effect == null)
        {
            return;
        }

        spawnedEffects.Remove(effect);
        Destroy(effect);
    }

    private void OnDisable()
    {
        playing = false;
        RestoreStretchedVisual();

        foreach (GameObject effect in spawnedEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }

        spawnedEffects.Clear();
    }

    private void OnDestroy()
    {
        if (lineSprite != null)
        {
            Destroy(lineSprite);
        }
    }
}
