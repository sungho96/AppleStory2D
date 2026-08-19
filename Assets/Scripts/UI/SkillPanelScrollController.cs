using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SkillPanelScrollController : MonoBehaviour
{
    // =========================================================
    // Inspector - Skill Icons
    // =========================================================

    [Header("Skill Icons")]
    [SerializeField] private Sprite powerShotIcon;
    [SerializeField] private Sprite rapidVolleyIcon;

    [SerializeField] private Sprite downStrikeIcon;
    [SerializeField] private Sprite shieldBlockIcon;


    // =========================================================
    // Inspector - Skill Text
    // =========================================================

    [Header("Skill Text")]

    [SerializeField, TextArea(2, 4)]
    private string powerShotText =
        "파워 샷\n최대 1.5초 차징\n피해·속도·크기 증가";

    [SerializeField, TextArea(2, 4)]
    private string rapidVolleyText =
        "래피드 볼리\n전방으로 화살 3발을\n빠르게 연속 발사";

    [SerializeField, TextArea(2, 4)]
    private string downStrikeText =
        "내려찍기\n검을 크게 내려쳐\n전방 적에게 피해";

    [SerializeField, TextArea(2, 4)]
    private string shieldBlockText =
        "방패막기\n짧은 시간 동안\n받는 피해 감소";


    // =========================================================
    // Skill Names
    // =========================================================

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


    // =========================================================
    // Runtime
    // =========================================================

    private const float RowSpacing = 160f;

    private Sprite runtimeRowSprite;
    private Texture2D runtimeRowTexture;

    private string[] activeIconNames;
    private string[] activeTextNames;

    private bool lastKnownWarriorState;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        /*
         * 먼저 모든 스킬 항목을 준비합니다.
         *
         * Archer / Warrior 둘 다 만들어 놓고
         * 실제 표시 여부만 현재 네트워크 역할에 따라 결정합니다.
         */

        EnsureAllSkillEntries();

        RefreshVisibleJobSkills();

        BuildScrollList();

        NormalizeExistingSkillIcons();

        lastKnownWarriorState = IsLocalWarriorPlayer();
    }


    private void OnEnable()
    {
        /*
         * '.' 키로 다시 열 때마다 현재 네트워크 역할을
         * 다시 확인합니다.
         *
         * Host  -> Archer
         * Client -> Warrior
         */

        RefreshPanelForCurrentPlayer();
    }


    // =========================================================
    // 외부 Refresh
    // =========================================================

    public void RefreshPanelForCurrentPlayer()
    {
        bool isWarrior = IsLocalWarriorPlayer();

        Debug.Log(
            $"[스킬창] 현재 직업 판정: " +
            $"{(isWarrior ? "Warrior" : "Archer")}");


        // -----------------------------------------------------
        // Inspector Sprite를 기존 아이콘에 다시 강제 반영
        // -----------------------------------------------------

        RefreshConfiguredSkillIcons();


        // -----------------------------------------------------
        // 직업별 표시 갱신
        // -----------------------------------------------------

        RefreshVisibleJobSkills();


        /*
         * 만약 이전에 Archer 기준으로 Scroll Row가 만들어졌는데
         * 지금 Warrior로 바뀌었거나 그 반대라면
         * Row 구성을 다시 만듭니다.
         */

        if (lastKnownWarriorState != isWarrior)
        {
            RebuildSkillRows();

            lastKnownWarriorState = isWarrior;
        }
        else
        {
            RefreshExistingRows();
        }


        NormalizeExistingSkillIcons();
    }


    // =========================================================
    // 모든 스킬 Entry 보장
    // =========================================================

    private void EnsureAllSkillEntries()
    {
        CreatePowerShotEntry();
        CreateRapidVolleyEntry();

        CreateDownStrikeEntry();
        CreateShieldBlockEntry();

        RefreshConfiguredSkillIcons();
    }


    // =========================================================
    // Power Shot
    // =========================================================

    private void CreatePowerShotEntry()
    {
        RectTransform existing =
            FindDescendantRect("PowerShotSkillIcon");

        if (existing != null)
        {
            ApplySkillIcon(
                "PowerShotSkillIcon",
                powerShotIcon,
                KeySettingSkillType.PowerShot);

            return;
        }


        Transform sourceIcon =
            FindDescendantRect("MoveSpeedSkillIcon");

        Transform sourceText =
            FindDescendantRect("MoveSpeedSkillText");


        if (sourceIcon == null ||
            sourceText == null)
        {
            Debug.LogWarning(
                "[스킬창] PowerShot 생성 실패: " +
                "MoveSpeedSkillIcon 또는 Text가 없습니다.");

            return;
        }


        if (powerShotIcon == null)
        {
            Debug.LogWarning(
                "[스킬창] Power Shot Icon이 Inspector에 없습니다.");

            return;
        }


        GameObject iconObject =
            Instantiate(
                sourceIcon.gameObject,
                transform);

        iconObject.name =
            "PowerShotSkillIcon";


        Image image =
            iconObject.GetComponent<Image>();

        if (image != null)
        {
            image.sprite = powerShotIcon;
        }


        SkillIconDragHandler handler =
            iconObject.GetComponent<SkillIconDragHandler>();

        if (handler != null)
        {
            handler.ConfigureSkillType(
                KeySettingSkillType.PowerShot);
        }


        GameObject textObject =
            Instantiate(
                sourceText.gameObject,
                transform);

        textObject.name =
            "PowerShotSkillText";


        TMP_Text text =
            textObject.GetComponent<TMP_Text>();

        if (text != null)
        {
            text.text = powerShotText;
        }
    }


    // =========================================================
    // Rapid Volley
    // =========================================================

    private void CreateRapidVolleyEntry()
    {
        RectTransform existing =
            FindDescendantRect("RapidVolleySkillIcon");

        if (existing != null)
        {
            ApplySkillIcon(
                "RapidVolleySkillIcon",
                rapidVolleyIcon,
                KeySettingSkillType.RapidVolley);

            return;
        }


        Transform sourceIcon =
            FindDescendantRect("MoveSpeedSkillIcon");

        Transform sourceText =
            FindDescendantRect("MoveSpeedSkillText");


        if (sourceIcon == null ||
            sourceText == null)
        {
            Debug.LogWarning(
                "[스킬창] RapidVolley 생성 실패: " +
                "MoveSpeedSkillIcon 또는 Text가 없습니다.");

            return;
        }


        if (rapidVolleyIcon == null)
        {
            Debug.LogWarning(
                "[스킬창] Rapid Volley Icon이 Inspector에 없습니다.");

            return;
        }


        GameObject iconObject =
            Instantiate(
                sourceIcon.gameObject,
                transform);

        iconObject.name =
            "RapidVolleySkillIcon";


        Image image =
            iconObject.GetComponent<Image>();

        if (image != null)
        {
            image.sprite = rapidVolleyIcon;
        }


        SkillIconDragHandler handler =
            iconObject.GetComponent<SkillIconDragHandler>();

        if (handler != null)
        {
            handler.ConfigureSkillType(
                KeySettingSkillType.RapidVolley);
        }


        GameObject textObject =
            Instantiate(
                sourceText.gameObject,
                transform);

        textObject.name =
            "RapidVolleySkillText";


        TMP_Text text =
            textObject.GetComponent<TMP_Text>();

        if (text != null)
        {
            text.text = rapidVolleyText;
        }
    }


    // =========================================================
    // Warrior Down Strike
    // =========================================================

    private void CreateDownStrikeEntry()
    {
        RectTransform existing =
            FindDescendantRect("DownStrikeSkillIcon");

        if (existing != null)
        {
            ApplySkillIcon(
                "DownStrikeSkillIcon",
                downStrikeIcon,
                KeySettingSkillType.WarriorDownStrike);

            return;
        }


        Transform sourceIcon =
            FindDescendantRect("MoveSpeedSkillIcon");

        Transform sourceText =
            FindDescendantRect("MoveSpeedSkillText");


        if (sourceIcon == null ||
            sourceText == null)
        {
            Debug.LogWarning(
                "[스킬창] DownStrike 생성 실패: " +
                "MoveSpeedSkillIcon 또는 Text가 없습니다.");

            return;
        }


        /*
         * 중요:
         *
         * DownStrike Icon이 없다고 MoveSpeed 같은
         * 다른 이미지를 대신 사용하지 않습니다.
         */

        if (downStrikeIcon == null)
        {
            Debug.LogWarning(
                "[스킬창] Down Strike Icon이 Inspector에 없습니다.");

            return;
        }


        GameObject iconObject =
            Instantiate(
                sourceIcon.gameObject,
                transform);

        iconObject.name =
            "DownStrikeSkillIcon";


        Image image =
            iconObject.GetComponent<Image>();

        if (image != null)
        {
            image.sprite = downStrikeIcon;
        }


        SkillIconDragHandler handler =
            iconObject.GetComponent<SkillIconDragHandler>();

        if (handler != null)
        {
            handler.ConfigureSkillType(
                KeySettingSkillType.WarriorDownStrike);
        }


        GameObject textObject =
            Instantiate(
                sourceText.gameObject,
                transform);

        textObject.name =
            "DownStrikeSkillText";


        TMP_Text text =
            textObject.GetComponent<TMP_Text>();

        if (text != null)
        {
            text.text = downStrikeText;
        }
    }


    // =========================================================
    // Warrior Shield Block
    // =========================================================

    private void CreateShieldBlockEntry()
    {
        RectTransform existing =
            FindDescendantRect("ShieldBlockSkillIcon");

        if (existing != null)
        {
            ApplySkillIcon(
                "ShieldBlockSkillIcon",
                shieldBlockIcon,
                KeySettingSkillType.WarriorShieldBlock);

            return;
        }


        Transform sourceIcon =
            FindDescendantRect("MoveSpeedSkillIcon");

        Transform sourceText =
            FindDescendantRect("MoveSpeedSkillText");


        if (sourceIcon == null ||
            sourceText == null)
        {
            Debug.LogWarning(
                "[스킬창] ShieldBlock 생성 실패: " +
                "MoveSpeedSkillIcon 또는 Text가 없습니다.");

            return;
        }


        /*
         * 중요:
         *
         * 기존 코드에서는 shieldBlockIcon이 없으면
         * rapidVolleyIcon을 사용했습니다.
         *
         * 이제 그런 대체를 절대 하지 않습니다.
         */

        if (shieldBlockIcon == null)
        {
            Debug.LogWarning(
                "[스킬창] Shield Block Icon이 Inspector에 없습니다.");

            return;
        }


        GameObject iconObject =
            Instantiate(
                sourceIcon.gameObject,
                transform);

        iconObject.name =
            "ShieldBlockSkillIcon";


        Image image =
            iconObject.GetComponent<Image>();

        if (image != null)
        {
            image.sprite = shieldBlockIcon;
        }


        SkillIconDragHandler handler =
            iconObject.GetComponent<SkillIconDragHandler>();

        if (handler != null)
        {
            handler.ConfigureSkillType(
                KeySettingSkillType.WarriorShieldBlock);
        }


        GameObject textObject =
            Instantiate(
                sourceText.gameObject,
                transform);

        textObject.name =
            "ShieldBlockSkillText";


        TMP_Text text =
            textObject.GetComponent<TMP_Text>();

        if (text != null)
        {
            text.text = shieldBlockText;
        }
    }


    // =========================================================
    // Inspector Sprite 강제 적용
    // =========================================================

    private void RefreshConfiguredSkillIcons()
    {
        ApplySkillIcon(
            "PowerShotSkillIcon",
            powerShotIcon,
            KeySettingSkillType.PowerShot);

        ApplySkillIcon(
            "RapidVolleySkillIcon",
            rapidVolleyIcon,
            KeySettingSkillType.RapidVolley);

        ApplySkillIcon(
            "DownStrikeSkillIcon",
            downStrikeIcon,
            KeySettingSkillType.WarriorDownStrike);

        ApplySkillIcon(
            "ShieldBlockSkillIcon",
            shieldBlockIcon,
            KeySettingSkillType.WarriorShieldBlock);
    }


    private void ApplySkillIcon(
        string iconName,
        Sprite configuredIcon,
        KeySettingSkillType skillType)
    {
        RectTransform icon =
            FindDescendantRect(iconName);

        if (icon == null)
        {
            return;
        }


        Image image =
            icon.GetComponent<Image>();


        /*
         * configuredIcon이 있을 때만 적용.
         *
         * 다른 스킬 이미지를 대신 넣는 동작은 없음.
         */

        if (image != null &&
            configuredIcon != null)
        {
            image.sprite =
                configuredIcon;
        }


        SkillIconDragHandler dragHandler =
            icon.GetComponent<SkillIconDragHandler>();

        if (dragHandler != null)
        {
            dragHandler.ConfigureSkillType(
                skillType);
        }
    }


    // =========================================================
    // 직업별 표시
    // =========================================================

    private void RefreshVisibleJobSkills()
    {
        bool isWarrior =
            IsLocalWarriorPlayer();


        activeIconNames =
            CombineNames(
                CommonIconNames,
                isWarrior
                    ? WarriorIconNames
                    : ArcherIconNames);


        activeTextNames =
            CombineNames(
                CommonTextNames,
                isWarrior
                    ? WarriorTextNames
                    : ArcherTextNames);


        // Archer
        SetSkillEntriesActive(
            ArcherIconNames,
            ArcherTextNames,
            !isWarrior);


        // Warrior
        SetSkillEntriesActive(
            WarriorIconNames,
            WarriorTextNames,
            isWarrior);
    }


    // =========================================================
    // ★ 직업 판정 핵심
    // =========================================================

    private bool IsLocalWarriorPlayer()
    {
        NetworkManager manager =
            NetworkManager.Singleton;


        /*
         * 현재 게임 규칙:
         *
         * Host   = Archer
         * Client = Warrior
         *
         * 따라서 WarriorDownStrike2D 같은 컴포넌트 존재 여부로
         * 직업을 판단하지 않습니다.
         *
         * 네트워크 역할만 봅니다.
         */


        if (manager != null &&
            manager.IsListening)
        {
            // 순수 Client만 Warrior
            if (manager.IsClient &&
                !manager.IsServer)
            {
                return true;
            }


            // Host / Server는 Archer
            if (manager.IsServer)
            {
                return false;
            }
        }


        /*
         * 네트워크가 아직 Listening 전인 경우.
         *
         * GameEntry에서는 KeyBindingManager에서 이미
         * HostArcher / ClientWarrior 프로필을 지정하는 구조입니다.
         *
         * 하지만 여기에서는 NetworkManager 연결 전에는
         * 안전하게 Archer를 기본값으로 사용합니다.
         *
         * 실제 ReadyPanel은 네트워크 접속 후 열리므로
         * 정상 플로우에서는 위 IsListening 분기를 타야 합니다.
         */

        return false;
    }


    // =========================================================
    // Scroll List 생성
    // =========================================================

    private void BuildScrollList()
    {
        RectTransform existingViewport =
            FindDescendantRect("SkillListViewport");


        if (existingViewport != null)
        {
            RefreshExistingRows();
            return;
        }


        CreateScrollObjects();
    }


    private void CreateScrollObjects()
    {
        RefreshVisibleJobSkills();


        RectTransform viewport =
            CreateRect(
                "SkillListViewport",
                transform);


        viewport.anchoredPosition =
            new Vector2(0f, -30f);

        viewport.sizeDelta =
            new Vector2(360f, 480f);


        Image viewportBackground =
            viewport.gameObject
                .AddComponent<Image>();


        viewportBackground.color =
            new Color(
                1f,
                0.965f,
                0.84f,
                1f);

        viewportBackground.raycastTarget =
            false;


        viewport.gameObject
            .AddComponent<RectMask2D>();


        RectTransform content =
            CreateRect(
                "SkillListContent",
                viewport);


        content.anchorMin =
            new Vector2(0.5f, 1f);

        content.anchorMax =
            new Vector2(0.5f, 1f);

        content.pivot =
            new Vector2(0.5f, 1f);

        content.anchoredPosition =
            Vector2.zero;


        content.sizeDelta =
            new Vector2(
                360f,
                RowSpacing * activeIconNames.Length);


        for (int i = 0;
             i < activeIconNames.Length;
             i++)
        {
            CreateSkillRow(
                content,
                i);
        }


        ScrollRect scrollRect =
            GetComponent<ScrollRect>();


        if (scrollRect == null)
        {
            scrollRect =
                gameObject.AddComponent<ScrollRect>();
        }


        scrollRect.viewport =
            viewport;

        scrollRect.content =
            content;

        scrollRect.horizontal =
            false;

        scrollRect.vertical =
            true;

        scrollRect.movementType =
            ScrollRect.MovementType.Clamped;

        scrollRect.inertia =
            true;

        scrollRect.decelerationRate =
            0.14f;

        scrollRect.scrollSensitivity =
            35f;

        scrollRect.verticalNormalizedPosition =
            1f;
    }


    // =========================================================
    // 직업이 바뀌었을 때 Row 다시 구성
    // =========================================================

    private void RebuildSkillRows()
    {
        RectTransform viewport =
            FindDescendantRect(
                "SkillListViewport");

        RectTransform content =
            FindDescendantRect(
                "SkillListContent");


        if (viewport == null ||
            content == null)
        {
            BuildScrollList();
            return;
        }


        /*
         * 기존 SkillRow에 들어있는 Skill Icon/Text를
         * 다시 Controller 바로 아래로 빼냅니다.
         */

        List<Transform> entriesToMove =
            new List<Transform>();


        foreach (string iconName in GetAllSkillIconNames())
        {
            RectTransform entry =
                FindDescendantRect(iconName);

            if (entry != null)
            {
                entriesToMove.Add(entry);
            }
        }


        foreach (string textName in GetAllSkillTextNames())
        {
            RectTransform entry =
                FindDescendantRect(textName);

            if (entry != null)
            {
                entriesToMove.Add(entry);
            }
        }


        for (int i = 0;
             i < entriesToMove.Count;
             i++)
        {
            entriesToMove[i]
                .SetParent(
                    transform,
                    false);
        }


        // 기존 Row 제거
        for (int i =
                 content.childCount - 1;
             i >= 0;
             i--)
        {
            Destroy(
                content.GetChild(i)
                    .gameObject);
        }


        RefreshVisibleJobSkills();


        content.sizeDelta =
            new Vector2(
                360f,
                RowSpacing *
                activeIconNames.Length);


        for (int i = 0;
             i < activeIconNames.Length;
             i++)
        {
            CreateSkillRow(
                content,
                i);
        }


        ScrollRect scrollRect =
            GetComponent<ScrollRect>();


        if (scrollRect != null)
        {
            scrollRect.content =
                content;

            scrollRect.verticalNormalizedPosition =
                1f;
        }
    }


    // =========================================================
    // 기존 Row Refresh
    // =========================================================

    private void RefreshExistingRows()
    {
        RefreshVisibleJobSkills();

        RefreshConfiguredSkillIcons();

        NormalizeExistingSkillIcons();
    }


    // =========================================================
    // Row 생성
    // =========================================================

    private void CreateSkillRow(
        RectTransform content,
        int rowIndex)
    {
        RectTransform row =
            CreateRect(
                "SkillRow_" + rowIndex,
                content);


        row.anchorMin =
            new Vector2(0.5f, 1f);

        row.anchorMax =
            new Vector2(0.5f, 1f);

        row.pivot =
            new Vector2(0.5f, 0.5f);


        row.anchoredPosition =
            new Vector2(
                0f,
                -80f -
                RowSpacing * rowIndex);


        row.sizeDelta =
            new Vector2(
                340f,
                148f);


        Image rowBackground =
            row.gameObject
                .AddComponent<Image>();


        rowBackground.sprite =
            GetOrCreateRowSprite();

        rowBackground.type =
            Image.Type.Sliced;

        rowBackground.raycastTarget =
            false;


        MoveEntryIntoRow(
            activeIconNames[rowIndex],
            row,
            -127f);


        MoveEntryIntoRow(
            activeTextNames[rowIndex],
            row,
            47.126f);
    }


    private void MoveEntryIntoRow(
        string objectName,
        RectTransform row,
        float positionX)
    {
        RectTransform entry =
            FindDescendantRect(
                objectName);


        if (entry == null)
        {
            Debug.LogWarning(
                $"[스킬창] {objectName} 항목을 찾지 못했습니다.");

            return;
        }


        entry.SetParent(
            row,
            false);


        entry.anchorMin =
            new Vector2(0.5f, 0.5f);

        entry.anchorMax =
            new Vector2(0.5f, 0.5f);

        entry.pivot =
            new Vector2(0.5f, 0.5f);


        entry.anchoredPosition3D =
            new Vector3(
                positionX,
                0f,
                0f);


        entry.localRotation =
            Quaternion.identity;

        entry.localScale =
            Vector3.one;

        entry.SetAsLastSibling();
    }


    // =========================================================
    // Icon 위치 정렬
    // =========================================================

    private void NormalizeExistingSkillIcons()
    {
        foreach (
            string iconName
            in GetAllSkillIconNames())
        {
            RectTransform iconRect =
                FindDescendantRect(
                    iconName);


            if (iconRect == null)
            {
                continue;
            }


            iconRect.anchorMin =
                new Vector2(0.5f, 0.5f);

            iconRect.anchorMax =
                new Vector2(0.5f, 0.5f);

            iconRect.pivot =
                new Vector2(0.5f, 0.5f);


            /*
             * Row 아래에 있을 때만
             * Row 기준 X축 위치를 적용합니다.
             */

            if (iconRect.parent != null &&
                iconRect.parent.name
                    .StartsWith("SkillRow_"))
            {
                iconRect.anchoredPosition3D =
                    new Vector3(
                        -127f,
                        0f,
                        0f);
            }


            iconRect.localRotation =
                Quaternion.identity;

            iconRect.localScale =
                Vector3.one;
        }
    }


    // =========================================================
    // Active
    // =========================================================

    private void SetSkillEntriesActive(
        string[] iconNames,
        string[] textNames,
        bool active)
    {
        for (int i = 0;
             i < iconNames.Length;
             i++)
        {
            RectTransform icon =
                FindDescendantRect(
                    iconNames[i]);


            if (icon != null)
            {
                icon.gameObject
                    .SetActive(active);
            }


            RectTransform text =
                FindDescendantRect(
                    textNames[i]);


            if (text != null)
            {
                text.gameObject
                    .SetActive(active);
            }
        }
    }


    // =========================================================
    // Find
    // =========================================================

    private RectTransform FindDescendantRect(
        string objectName)
    {
        RectTransform[] children =
            GetComponentsInChildren<RectTransform>(
                true);


        for (int i = 0;
             i < children.Length;
             i++)
        {
            if (children[i].name ==
                objectName)
            {
                return children[i];
            }
        }


        return null;
    }


    // =========================================================
    // Name Utils
    // =========================================================

    private string[] CombineNames(
        string[] first,
        string[] second)
    {
        string[] combined =
            new string[
                first.Length +
                second.Length];


        first.CopyTo(
            combined,
            0);

        second.CopyTo(
            combined,
            first.Length);


        return combined;
    }


    private string[] GetAllSkillIconNames()
    {
        string[] first =
            CombineNames(
                CommonIconNames,
                ArcherIconNames);


        return CombineNames(
            first,
            WarriorIconNames);
    }


    private string[] GetAllSkillTextNames()
    {
        string[] first =
            CombineNames(
                CommonTextNames,
                ArcherTextNames);


        return CombineNames(
            first,
            WarriorTextNames);
    }


    // =========================================================
    // Row Sprite
    // =========================================================

    private Sprite GetOrCreateRowSprite()
    {
        if (runtimeRowSprite != null)
        {
            return runtimeRowSprite;
        }


        const int width = 256;
        const int height = 112;
        const float cornerRadius = 12f;


        runtimeRowTexture =
            new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false);


        runtimeRowTexture.name =
            "SkillRowRuntimeTexture";


        Color fill =
            new Color(
                1f,
                0.965f,
                0.84f,
                1f);


        Color innerBorder =
            new Color(
                0.92f,
                0.73f,
                0.36f,
                1f);


        Color outerBorder =
            new Color(
                0.62f,
                0.46f,
                0.22f,
                1f);


        for (int y = 0;
             y < height;
             y++)
        {
            for (int x = 0;
                 x < width;
                 x++)
            {
                float edgeX =
                    Mathf.Min(
                        x,
                        width - 1 - x);


                float edgeY =
                    Mathf.Min(
                        y,
                        height - 1 - y);


                bool roundedCorner =
                    edgeX < cornerRadius &&
                    edgeY < cornerRadius;


                float cornerX =
                    cornerRadius - edgeX;

                float cornerY =
                    cornerRadius - edgeY;


                bool outside =
                    roundedCorner &&
                    cornerX * cornerX +
                    cornerY * cornerY >
                    cornerRadius *
                    cornerRadius;


                Color pixel =
                    outside
                        ? Color.clear
                        : edgeX < 3f ||
                          edgeY < 3f
                            ? outerBorder
                            : edgeX < 6f ||
                              edgeY < 6f
                                ? innerBorder
                                : fill;


                runtimeRowTexture
                    .SetPixel(
                        x,
                        y,
                        pixel);
            }
        }


        runtimeRowTexture.Apply();


        runtimeRowSprite =
            Sprite.Create(
                runtimeRowTexture,
                new Rect(
                    0f,
                    0f,
                    width,
                    height),
                new Vector2(
                    0.5f,
                    0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(
                    14f,
                    14f,
                    14f,
                    14f));


        runtimeRowSprite.name =
            "SkillRowRuntimeSprite";


        return runtimeRowSprite;
    }


    // =========================================================
    // Rect
    // =========================================================

    private RectTransform CreateRect(
        string objectName,
        Transform parent)
    {
        GameObject child =
            new GameObject(
                objectName,
                typeof(RectTransform));


        child.layer =
            gameObject.layer;


        RectTransform childRect =
            child.GetComponent<RectTransform>();


        childRect.SetParent(
            parent,
            false);


        childRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        childRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        childRect.pivot =
            new Vector2(0.5f, 0.5f);


        return childRect;
    }


    // =========================================================
    // Destroy
    // =========================================================

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
}