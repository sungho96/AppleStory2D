using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격속도 버프의 발동 플래시와 상단 아이콘 모션을 담당합니다.
/// </summary>
public class AttackSpeedBuffVisualFeedback : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color flashColor = new Color(1f, 0.62f, 0.18f, 1f);

    [Header("Ambient Speed Motes")]
    [SerializeField] private Color moteColor = new Color(1f, 0.72f, 0.2f, 0.38f);
    [SerializeField, Min(3)] private int moteCount = 5;
    [SerializeField, Min(0.05f)] private float moteSpawnInterval = 0.18f;
    [SerializeField] private Vector2 moteLifetimeRange = new Vector2(0.28f, 0.42f);
    [SerializeField] private Vector2 moteHeightRange = new Vector2(0.15f, 1.15f);
    [SerializeField, Min(0.05f)] private float moteTravelDistance = 0.65f;

    private sealed class SpeedMote
    {
        public GameObject gameObject;
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 startPosition;
        public Vector3 endPosition;
        public float elapsed;
        public float lifetime;
        public bool active;
    }

    private readonly List<SpriteRenderer> sources = new List<SpriteRenderer>();
    private readonly List<Color> originalColors = new List<Color>();
    private readonly List<SpeedMote> motes = new List<SpeedMote>();
    private Transform player;
    private RectTransform icon;
    private Vector3 iconBaseScale = Vector3.one;
    private bool playing;
    private float moteSpawnTimer;
    private int nextMoteIndex;
    private Sprite moteSprite;
    private Coroutine iconRoutine;
    private Coroutine flashRoutine;

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

        // [공격속도 유지 이펙트 추가] 씬 수정 없이 재사용 가능한 속도 파편 풀을 준비합니다.
        CreateMotePool();
    }

    public void PlayStart()
    {
        playing = true;
        moteSpawnTimer = 0f;
        nextMoteIndex = 0;

        // [공격속도 버프 연출 추가] 재사용해도 발동 플래시와 아이콘 모션을 처음부터 다시 재생합니다.
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashPlayer());

        if (iconRoutine != null)
            StopCoroutine(iconRoutine);
        iconRoutine = StartCoroutine(AnimateIcon());
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

        // [공격속도 유지 이펙트 추가] 버프가 끝나면 남은 파편도 즉시 정리합니다.
        HideAllMotes();
        RestoreColors();
    }

    private void Update()
    {
        if (!playing || player == null)
            return;

        moteSpawnTimer -= Time.deltaTime;
        if (moteSpawnTimer <= 0f)
        {
            SpawnMote();
            moteSpawnTimer = moteSpawnInterval * Random.Range(0.8f, 1.2f);
        }

        UpdateMotes();
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

        // [상단 아이콘 모션 통일] 이동속도 버프와 같은 등장 펀치 값을 사용합니다.
        float elapsed = 0f;
        const float punchDuration = 0.35f;

        while (elapsed < punchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            icon.localScale = iconBaseScale * (1f + Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.65f);
            yield return null;
        }

        // [상단 아이콘 모션 통일] 버프 유지 중에도 이동속도 아이콘과 같은 약한 맥동을 적용합니다.
        while (playing)
        {
            icon.localScale = iconBaseScale * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f);
            yield return null;
        }

        icon.localScale = iconBaseScale;
        iconRoutine = null;
    }

    private void CreateMotePool()
    {
        if (player == null || motes.Count > 0)
            return;

        // [공격속도 유지 이펙트 추가] 외부 에셋 없이 SpriteRenderer 기본 재질로 얇은 빛 파편을 만듭니다.
        moteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        moteSprite.name = "AttackSpeedMoteSprite";

        int sortingLayerId = sources.Count > 0 && sources[0] != null
            ? sources[0].sortingLayerID
            : 0;
        int sortingOrder = 1;

        foreach (SpriteRenderer source in sources)
        {
            if (source != null)
                sortingOrder = Mathf.Max(sortingOrder, source.sortingOrder + 1);
        }

        for (int i = 0; i < moteCount; i++)
        {
            GameObject moteObject = new GameObject("AttackSpeedMote");
            moteObject.transform.SetParent(player, false);

            SpriteRenderer moteRenderer = moteObject.AddComponent<SpriteRenderer>();
            moteRenderer.sprite = moteSprite;
            moteRenderer.color = Color.clear;
            moteRenderer.sortingLayerID = sortingLayerId;
            moteRenderer.sortingOrder = sortingOrder;

            // 흰색 기본 스프라이트의 실제 크기를 보정해 짧고 가는 속도선으로 맞춥니다.
            Vector2 spriteSize = moteSprite.bounds.size;
            moteObject.transform.localScale = new Vector3(
                0.28f / spriteSize.x,
                0.025f / spriteSize.y,
                1f);
            moteObject.SetActive(false);

            motes.Add(new SpeedMote
            {
                gameObject = moteObject,
                transform = moteObject.transform,
                renderer = moteRenderer
            });
        }
    }

    private void SpawnMote()
    {
        if (motes.Count == 0)
            return;

        SpeedMote mote = motes[nextMoteIndex];
        nextMoteIndex = (nextMoteIndex + 1) % motes.Count;

        float direction = Random.value < 0.5f ? -1f : 1f;
        float startX = -direction * Random.Range(0.18f, 0.42f);
        float height = Random.Range(moteHeightRange.x, moteHeightRange.y);

        mote.startPosition = new Vector3(startX, height, 0f);
        mote.endPosition = mote.startPosition + Vector3.right * direction * moteTravelDistance;
        mote.elapsed = 0f;
        mote.lifetime = Random.Range(moteLifetimeRange.x, moteLifetimeRange.y);
        mote.active = true;
        mote.transform.localPosition = mote.startPosition;
        mote.gameObject.SetActive(true);
    }

    private void UpdateMotes()
    {
        foreach (SpeedMote mote in motes)
        {
            if (!mote.active)
                continue;

            mote.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(mote.elapsed / mote.lifetime);
            float easedT = 1f - (1f - t) * (1f - t);

            mote.transform.localPosition = Vector3.Lerp(mote.startPosition, mote.endPosition, easedT);

            // [공격속도 유지 이펙트 추가] 처음과 끝을 부드럽게 숨겨 화면에 선이 갑자기 튀는 느낌을 줄입니다.
            Color color = moteColor;
            color.a = moteColor.a * Mathf.Sin(t * Mathf.PI);
            mote.renderer.color = color;

            if (t >= 1f)
            {
                mote.active = false;
                mote.gameObject.SetActive(false);
            }
        }
    }

    private void HideAllMotes()
    {
        foreach (SpeedMote mote in motes)
        {
            mote.active = false;
            if (mote.gameObject != null)
                mote.gameObject.SetActive(false);
        }
    }

    private void RestoreColors()
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] != null)
                sources[i].color = originalColors[i];
        }
    }

    private void OnDisable()
    {
        PlayEnd();
    }

    private void OnDestroy()
    {
        // 런타임에 만든 Sprite만 정리하며 Unity 공용 흰색 Texture는 제거하지 않습니다.
        if (moteSprite != null)
            Destroy(moteSprite);
    }
}
