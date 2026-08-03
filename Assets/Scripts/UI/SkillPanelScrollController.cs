using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillPanelScrollController : MonoBehaviour
{
    private enum SkillPanelJob
    {
        Auto,
        Archer,
        Warrior
    }

    [SerializeField] private SkillPanelJob panelJob = SkillPanelJob.Auto;
    [SerializeField] private Sprite angerIcon;
    [SerializeField] private Sprite downStrikeIcon;
    [SerializeField] private Sprite powerShotIcon;
    [SerializeField] private Sprite rapidVolleyIcon;

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
        "AngerSkillIcon",
        "DownStrikeSkillIcon"
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
        "AngerSkillText",
        "DownStrikeSkillText"
    };

    private const float RowSpacing = 160f;
    private Sprite runtimeRowSprite;
    private Texture2D runtimeRowTexture;

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
        foreach (string iconName in GetAllIconNames())
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
            return;
        }

        SkillPanelJob resolvedJob = ResolvePanelJob();

        if (resolvedJob == SkillPanelJob.Archer)
        {
            CreatePowerShotEntry();
            CreateRapidVolleyEntry();
        }
        else if (resolvedJob == SkillPanelJob.Warrior)
        {
            CreateAngerEntry();
            CreateDownStrikeEntry();
        }

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
        string[] iconNames = GetVisibleIconNames(resolvedJob);
        string[] textNames = GetVisibleTextNames(resolvedJob);
        content.sizeDelta = new Vector2(360f, RowSpacing * iconNames.Length);

        for (int i = 0; i < iconNames.Length; i++)
        {
            CreateSkillRow(content, i, iconNames[i], textNames[i]);
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
        textObject.GetComponent<TMP_Text>().text =
            "파워 샷\n최대 1.5초 차징\n피해·속도·크기 증가";
    }

    private void CreateRapidVolleyEntry()
    {
        if (transform.Find("RapidVolleySkillIcon") != null)
        {
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
        textObject.GetComponent<TMP_Text>().text =
            "래피드 볼리\n전방으로 화살 3발을\n빠르게 연속 발사";
    }

    private void CreateAngerEntry()
    {
        if (transform.Find("AngerSkillIcon") != null)
        {
            return;
        }

        Transform sourceIcon = transform.Find("MoveSpeedSkillIcon");
        Transform sourceText = transform.Find("MoveSpeedSkillText");
        if (sourceIcon == null || sourceText == null)
        {
            Debug.LogWarning("[SkillPanel] Anger skill source UI is missing.");
            return;
        }

        GameObject iconObject = Instantiate(sourceIcon.gameObject, transform);
        iconObject.name = "AngerSkillIcon";
        Image iconImage = iconObject.GetComponent<Image>();
        if (iconImage != null && angerIcon != null)
            iconImage.sprite = angerIcon;
        iconObject.GetComponent<SkillIconDragHandler>()
            .ConfigureSkillType(KeySettingSkillType.AngerBuff);

        GameObject textObject = Instantiate(sourceText.gameObject, transform);
        textObject.name = "AngerSkillText";
        textObject.GetComponent<TMP_Text>().text =
            "분노\n일정 시간 동안\n공격력이 증가";
    }

    private void CreateDownStrikeEntry()
    {
        if (transform.Find("DownStrikeSkillIcon") != null)
        {
            return;
        }

        Transform sourceIcon = transform.Find("MoveSpeedSkillIcon");
        Transform sourceText = transform.Find("MoveSpeedSkillText");
        if (sourceIcon == null || sourceText == null)
        {
            Debug.LogWarning("[SkillPanel] DownStrike skill source UI is missing.");
            return;
        }

        // [Codex Warrior DownStrike] Clone the existing skill row pieces so the warrior panel keeps the same drag mapping behavior.
        GameObject iconObject = Instantiate(sourceIcon.gameObject, transform);
        iconObject.name = "DownStrikeSkillIcon";
        Image iconImage = iconObject.GetComponent<Image>();
        if (iconImage != null && downStrikeIcon != null)
            iconImage.sprite = downStrikeIcon;
        iconObject.GetComponent<SkillIconDragHandler>()
            .ConfigureSkillType(KeySettingSkillType.DownStrike);

        GameObject textObject = Instantiate(sourceText.gameObject, transform);
        textObject.name = "DownStrikeSkillText";
        textObject.GetComponent<TMP_Text>().text =
            "Down Strike\nHop, then slam\nfront enemies";
    }

    private void CreateSkillRow(RectTransform content, int rowIndex, string iconName, string textName)
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
        MoveEntryIntoRow(iconName, row, -127f);
        MoveEntryIntoRow(textName, row, 47.126f);
    }

    private SkillPanelJob ResolvePanelJob()
    {
        if (panelJob != SkillPanelJob.Auto)
            return panelJob;

        // [Codex Job Skill Panel] If the active player has WarriorAttack2D, open the warrior skill list; otherwise keep the archer list.
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null && playerObject.activeInHierarchy &&
            playerObject.GetComponent<WarriorAttack2D>() != null)
        {
            return SkillPanelJob.Warrior;
        }

        foreach (WarriorAttack2D warrior in FindObjectsByType<WarriorAttack2D>(FindObjectsSortMode.None))
        {
            if (warrior != null && warrior.gameObject.activeInHierarchy)
                return SkillPanelJob.Warrior;
        }

        return SkillPanelJob.Archer;
    }

    private string[] GetVisibleIconNames(SkillPanelJob resolvedJob)
    {
        return resolvedJob == SkillPanelJob.Warrior
            ? Combine(CommonIconNames, WarriorIconNames)
            : Combine(CommonIconNames, ArcherIconNames);
    }

    private string[] GetVisibleTextNames(SkillPanelJob resolvedJob)
    {
        return resolvedJob == SkillPanelJob.Warrior
            ? Combine(CommonTextNames, WarriorTextNames)
            : Combine(CommonTextNames, ArcherTextNames);
    }

    private string[] GetAllIconNames()
    {
        return Combine(Combine(CommonIconNames, ArcherIconNames), WarriorIconNames);
    }

    private static string[] Combine(string[] first, string[] second)
    {
        string[] combined = new string[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);
        return combined;
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
