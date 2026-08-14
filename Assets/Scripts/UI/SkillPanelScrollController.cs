using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SkillPanelScrollController : MonoBehaviour
{
    [Header("Skill Icons")]
    [SerializeField] private Sprite powerShotIcon;
    [SerializeField] private Sprite rapidVolleyIcon;
    [SerializeField] private Sprite downStrikeIcon;
    [SerializeField] private Sprite shieldBlockIcon;

    [Header("Skill Text")]
    [SerializeField, TextArea(2, 4)] private string powerShotText =
        "파워 샷\n최대 1.5초 차징\n피해·속도·크기 증가";
    [SerializeField, TextArea(2, 4)] private string rapidVolleyText =
        "래피드 볼리\n전방으로 화살 3발을\n빠르게 연속 발사";
    [SerializeField, TextArea(2, 4)] private string downStrikeText =
        "내려찍기\n검을 크게 내려쳐\n전방 적에게 피해";
    [SerializeField, TextArea(2, 4)] private string shieldBlockText =
        "방패막기\n짧은 시간 동안\n받는 피해 감소";

    private static readonly string[] CommonIconNames =
    {
        "MoveSpeedSkillIcon",
        "AttackSpeedSkillIcon",
        "QuickStepPassiveSkillIcon"
    };

    private static readonly string[] ArcherIconNames =
    {
        "PowerShotSkillIcon",
        "RapidVolleySkillIcon"
    };

    private static readonly string[] WarriorIconNames =
    {
        "DownStrikeSkillIcon",
        "ShieldBlockSkillIcon"
    };

    private static readonly string[] CommonTextNames =
    {
        "MoveSpeedSkillText",
        "AttackSpeedSkillText",
        "QuickStepPassiveSkillText"
    };

    private static readonly string[] ArcherTextNames =
    {
        "PowerShotSkillText",
        "RapidVolleySkillText"
    };

    private static readonly string[] WarriorTextNames =
    {
        "DownStrikeSkillText",
        "ShieldBlockSkillText"
    };

    private const float RowSpacing = 160f;
    private Sprite runtimeRowSprite;
    private Texture2D runtimeRowTexture;
    private string[] activeIconNames;
    private string[] activeTextNames;

    private void Awake()
    {
        BuildScrollList();
    }

    private void OnEnable()
    {
        // [스킬 패널 재정렬] '.' 키로 다시 열 때 기존 Row가 좌표를 덮어쓴 상태여도 전부 같은 축으로 복구합니다.
        NormalizeExistingSkillIcons();
    }

    private void NormalizeExistingSkillIcons()
    {
        foreach (string iconName in GetAllSkillIconNames())
        {
            RectTransform iconRect = FindDescendantRect(iconName);
            if (iconRect == null)
                continue;

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition3D = new Vector3(-127f, 0f, 0f);
            iconRect.localRotation = Quaternion.identity;
            iconRect.localScale = Vector3.one;
        }
    }

    private RectTransform FindDescendantRect(string objectName)
    {
        foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
        {
            if (child.name == objectName)
                return child;
        }

        return null;
    }

    private void BuildScrollList()
    {
        if (transform.Find("SkillListViewport") != null)
        {
            RefreshVisibleJobSkills();
            return;
        }

        CreatePowerShotEntry();
        CreateRapidVolleyEntry();
        CreateDownStrikeEntry();
        CreateShieldBlockEntry();
        RefreshVisibleJobSkills();

        // [스킬창 스크롤 추가] 기존 스킬 스킨은 유지하고 목록만 클리핑합니다.
        RectTransform viewport = CreateRect("SkillListViewport", transform);
        viewport.anchoredPosition = new Vector2(0f, -30f);
        viewport.sizeDelta = new Vector2(360f, 480f);
        // [스킬 Row 스크롤 수정] 원본 스킨의 고정 슬롯을 가리고 움직이는 Row만 보이게 합니다.
        Image viewportBackground = viewport.gameObject.AddComponent<Image>();
        viewportBackground.color = new Color(1f, 0.965f, 0.84f, 1f);
        viewportBackground.raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect("SkillListContent", viewport);
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(360f, RowSpacing * activeIconNames.Length);

        for (int i = 0; i < activeIconNames.Length; i++)
        {
            CreateSkillRow(content, i);
        }

        NormalizeExistingSkillIcons();

        ScrollRect scrollRect = gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.decelerationRate = 0.14f;
        scrollRect.scrollSensitivity = 35f;
        scrollRect.verticalNormalizedPosition = 1f;
    }

    private void CreatePowerShotEntry()
    {
        if (transform.Find("PowerShotSkillIcon") != null)
        {
            ApplySkillIcon("PowerShotSkillIcon", powerShotIcon, KeySettingSkillType.PowerShot);
            return;
        }

        Transform sourceIcon = transform.Find("MoveSpeedSkillIcon");
        Transform sourceText = transform.Find("MoveSpeedSkillText");
        if (sourceIcon == null || sourceText == null || powerShotIcon == null)
        {
            Debug.LogWarning("[스킬창] 파워 샷 항목을 생성할 참조가 부족합니다.");
            return;
        }

        // [파워 샷 UI 추가] 기존 항목을 복제해 크기·폰트·드래그 구조를 동일하게 유지합니다.
        GameObject iconObject = Instantiate(sourceIcon.gameObject, transform);
        iconObject.name = "PowerShotSkillIcon";
        iconObject.GetComponent<Image>().sprite = powerShotIcon;
        iconObject.GetComponent<SkillIconDragHandler>()
            .ConfigureSkillType(KeySettingSkillType.PowerShot);

        GameObject textObject = Instantiate(sourceText.gameObject, transform);
        textObject.name = "PowerShotSkillText";
        textObject.GetComponent<TMP_Text>().text = powerShotText;
    }

    private void CreateRapidVolleyEntry()
    {
        if (transform.Find("RapidVolleySkillIcon") != null)
        {
            ApplySkillIcon("RapidVolleySkillIcon", rapidVolleyIcon, KeySettingSkillType.RapidVolley);
            return;
        }

        Transform sourceIcon = transform.Find("MoveSpeedSkillIcon");
        Transform sourceText = transform.Find("MoveSpeedSkillText");
        if (sourceIcon == null || sourceText == null || rapidVolleyIcon == null)
        {
            Debug.LogWarning("[스킬창] 래피드 볼리 항목 생성에 필요한 참조가 부족합니다.");
            return;
        }

        // [래피드 볼리 UI 추가] 기존 항목을 복제하여 크기·폰트·드래그 구조를 동일하게 유지합니다.
        GameObject iconObject = Instantiate(sourceIcon.gameObject, transform);
        iconObject.name = "RapidVolleySkillIcon";
        iconObject.GetComponent<Image>().sprite = rapidVolleyIcon;
        iconObject.GetComponent<SkillIconDragHandler>()
            .ConfigureSkillType(KeySettingSkillType.RapidVolley);

        GameObject textObject = Instantiate(sourceText.gameObject, transform);
        textObject.name = "RapidVolleySkillText";
        textObject.GetComponent<TMP_Text>().text = rapidVolleyText;
    }

    private void CreateDownStrikeEntry()
    {
        if (transform.Find("DownStrikeSkillIcon") != null)
        {
            ApplySkillIcon("DownStrikeSkillIcon", downStrikeIcon, KeySettingSkillType.WarriorDownStrike);
            return;
        }

        Transform sourceIcon = transform.Find("MoveSpeedSkillIcon");
        Transform sourceText = transform.Find("MoveSpeedSkillText");
        if (sourceIcon == null || sourceText == null)
        {
            Debug.LogWarning("[스킬창] 내려찍기 항목 생성에 필요한 참조가 부족합니다.");
            return;
        }

        // [Codex Warrior Skill Panel] 전사 전용 스킬도 기존 스킬 슬롯 스킨을 복제해서 같은 크기로 맞춥니다.
        GameObject iconObject = Instantiate(sourceIcon.gameObject, transform);
        iconObject.name = "DownStrikeSkillIcon";
        iconObject.GetComponent<Image>().sprite = downStrikeIcon != null
            ? downStrikeIcon
            : sourceIcon.GetComponent<Image>().sprite;
        iconObject.GetComponent<SkillIconDragHandler>()
            .ConfigureSkillType(KeySettingSkillType.WarriorDownStrike);

        GameObject textObject = Instantiate(sourceText.gameObject, transform);
        textObject.name = "DownStrikeSkillText";
        textObject.GetComponent<TMP_Text>().text = downStrikeText;
    }

    private void CreateShieldBlockEntry()
    {
        if (transform.Find("ShieldBlockSkillIcon") != null)
        {
            ApplySkillIcon("ShieldBlockSkillIcon", shieldBlockIcon, KeySettingSkillType.WarriorShieldBlock);
            return;
        }

        Transform sourceIcon = transform.Find("MoveSpeedSkillIcon");
        Transform sourceText = transform.Find("MoveSpeedSkillText");
        if (sourceIcon == null || sourceText == null)
        {
            Debug.LogWarning("[스킬창] 방패막기 항목 생성에 필요한 참조가 부족합니다.");
            return;
        }

        // [Codex Warrior Skill Panel] 방패막기는 워리어일 때만 Row에 들어가도록 별도 타입을 부여합니다.
        GameObject iconObject = Instantiate(sourceIcon.gameObject, transform);
        iconObject.name = "ShieldBlockSkillIcon";
        iconObject.GetComponent<Image>().sprite = shieldBlockIcon != null ? shieldBlockIcon : rapidVolleyIcon;
        iconObject.GetComponent<SkillIconDragHandler>()
            .ConfigureSkillType(KeySettingSkillType.WarriorShieldBlock);

        GameObject textObject = Instantiate(sourceText.gameObject, transform);
        textObject.name = "ShieldBlockSkillText";
        textObject.GetComponent<TMP_Text>().text = shieldBlockText;
    }

    private void ApplySkillIcon(
        string iconName,
        Sprite configuredIcon,
        KeySettingSkillType skillType)
    {
        Transform icon = FindDescendantRect(iconName);
        if (icon == null)
            return;

        Image image = icon.GetComponent<Image>();
        if (image != null && configuredIcon != null)
        {
            // [Codex Skill Icon Inspector] Inspector에 넣은 Sprite가 있으면 기존 씬 아이콘보다 우선 적용합니다.
            image.sprite = configuredIcon;
        }

        SkillIconDragHandler dragHandler = icon.GetComponent<SkillIconDragHandler>();
        if (dragHandler != null)
            dragHandler.ConfigureSkillType(skillType);
    }

    private void CreateSkillRow(RectTransform content, int rowIndex)
    {
        // [스킬 Row 스크롤 수정] 배경·아이콘·텍스트를 하나의 부모 아래 묶어 함께 이동시킵니다.
        RectTransform row = CreateRect("SkillRow_" + rowIndex, content);
        row.anchorMin = new Vector2(0.5f, 1f);
        row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, -80f - RowSpacing * rowIndex);
        row.sizeDelta = new Vector2(340f, 148f);

        Image rowBackground = row.gameObject.AddComponent<Image>();
        rowBackground.sprite = GetOrCreateRowSprite();
        rowBackground.type = Image.Type.Sliced;
        rowBackground.raycastTarget = false;

        // [Free Aspect 중앙 정렬] 원본 아이콘 중심값(-122)을 유지해 왼쪽 쏠림을 제거합니다.
        // [Free Aspect 중앙 정렬] 모든 스킬 아이콘을 첫 번째 아이콘과 같은 X축 기준으로 통일합니다.
        MoveEntryIntoRow(activeIconNames[rowIndex], row, -127f);
        MoveEntryIntoRow(activeTextNames[rowIndex], row, 47.126f);
    }

    private void RefreshVisibleJobSkills()
    {
        bool isWarrior = IsLocalWarriorPlayer();
        activeIconNames = CombineNames(CommonIconNames, isWarrior ? WarriorIconNames : ArcherIconNames);
        activeTextNames = CombineNames(CommonTextNames, isWarrior ? WarriorTextNames : ArcherTextNames);

        SetSkillEntriesActive(ArcherIconNames, ArcherTextNames, !isWarrior);
        SetSkillEntriesActive(WarriorIconNames, WarriorTextNames, isWarrior);
    }

    private bool IsLocalWarriorPlayer()
    {
        WarriorDownStrike2D[] warriorSkills = FindObjectsByType<WarriorDownStrike2D>(FindObjectsSortMode.None);
        for (int i = 0; i < warriorSkills.Length; i++)
        {
            NetworkObject networkObject = warriorSkills[i].GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                if (networkObject.IsOwner)
                    return true;
                continue;
            }

            return true;
        }

        return false;
    }

    private string[] CombineNames(string[] first, string[] second)
    {
        string[] combined = new string[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);
        return combined;
    }

    private string[] GetAllSkillIconNames()
    {
        string[] first = CombineNames(CommonIconNames, ArcherIconNames);
        return CombineNames(first, WarriorIconNames);
    }

    private void SetSkillEntriesActive(string[] iconNames, string[] textNames, bool active)
    {
        for (int i = 0; i < iconNames.Length; i++)
        {
            Transform icon = FindDescendantRect(iconNames[i]);
            if (icon != null)
                icon.gameObject.SetActive(active);

            Transform text = FindDescendantRect(textNames[i]);
            if (text != null)
                text.gameObject.SetActive(active);
        }
    }

    private void MoveEntryIntoRow(string objectName, RectTransform row, float positionX)
    {
        Transform entry = transform.Find(objectName);
        if (entry == null)
        {
            Debug.LogWarning($"[스킬창] {objectName} 항목을 찾지 못했습니다.");
            return;
        }

        RectTransform entryRect = entry as RectTransform;
        entryRect.SetParent(row, false);
        entryRect.anchorMin = new Vector2(0.5f, 0.5f);
        entryRect.anchorMax = new Vector2(0.5f, 0.5f);
        entryRect.pivot = new Vector2(0.5f, 0.5f);
        entryRect.anchoredPosition3D = new Vector3(positionX, 0f, 0f);
        entryRect.localRotation = Quaternion.identity;
        entryRect.localScale = Vector3.one;
        entryRect.SetAsLastSibling();
    }

    private Sprite GetOrCreateRowSprite()
    {
        if (runtimeRowSprite != null)
        {
            return runtimeRowSprite;
        }

        // [스킬 Row 스크롤 수정] 프로젝트 스킨과 어울리는 크림색·금색 Row 배경을 생성합니다.
        const int width = 256;
        const int height = 112;
        const float cornerRadius = 12f;
        runtimeRowTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        runtimeRowTexture.name = "SkillRowRuntimeTexture";

        Color fill = new Color(1f, 0.965f, 0.84f, 1f);
        Color innerBorder = new Color(0.92f, 0.73f, 0.36f, 1f);
        Color outerBorder = new Color(0.62f, 0.46f, 0.22f, 1f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float edgeX = Mathf.Min(x, width - 1 - x);
                float edgeY = Mathf.Min(y, height - 1 - y);
                bool roundedCorner = edgeX < cornerRadius && edgeY < cornerRadius;
                float cornerX = cornerRadius - edgeX;
                float cornerY = cornerRadius - edgeY;
                bool outside = roundedCorner &&
                    cornerX * cornerX + cornerY * cornerY > cornerRadius * cornerRadius;

                Color pixel = outside
                    ? Color.clear
                    : edgeX < 3f || edgeY < 3f
                        ? outerBorder
                        : edgeX < 6f || edgeY < 6f
                            ? innerBorder
                            : fill;
                runtimeRowTexture.SetPixel(x, y, pixel);
            }
        }

        runtimeRowTexture.Apply();
        runtimeRowSprite = Sprite.Create(
            runtimeRowTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(14f, 14f, 14f, 14f));
        runtimeRowSprite.name = "SkillRowRuntimeSprite";
        return runtimeRowSprite;
    }

    private void OnDestroy()
    {
        if (runtimeRowSprite != null)
        {
            Destroy(runtimeRowSprite);
        }

        if (runtimeRowTexture != null)
        {
            Destroy(runtimeRowTexture);
        }
    }

    private void MoveEntry(
        string objectName,
        RectTransform content,
        float positionX,
        int rowIndex)
    {
        Transform entry = transform.Find(objectName);
        if (entry == null)
        {
            Debug.LogWarning($"[스킬창] {objectName}을(를) 찾지 못했습니다.");
            return;
        }

        RectTransform entryRect = entry as RectTransform;
        entryRect.SetParent(content, false);
        entryRect.anchorMin = new Vector2(0.5f, 1f);
        entryRect.anchorMax = new Vector2(0.5f, 1f);
        entryRect.pivot = new Vector2(0.5f, 0.5f);
        entryRect.anchoredPosition = new Vector2(
            positionX,
            -80f - RowSpacing * rowIndex);
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(
            objectName,
            typeof(RectTransform));
        child.layer = gameObject.layer;

        RectTransform childRect = child.GetComponent<RectTransform>();
        childRect.SetParent(parent, false);
        childRect.anchorMin = new Vector2(0.5f, 0.5f);
        childRect.anchorMax = new Vector2(0.5f, 0.5f);
        childRect.pivot = new Vector2(0.5f, 0.5f);
        return childRect;
    }
}
