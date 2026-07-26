using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillPanelScrollController : MonoBehaviour
{
    [SerializeField] private Sprite powerShotIcon;

    private static readonly string[] IconNames =
    {
        "MoveSpeedSkillIcon",
        "AttackSpeedSkillIcon",
        "QuickStepPassiveSkillIcon",
        "PowerShotSkillIcon"
    };

    private static readonly string[] TextNames =
    {
        "MoveSpeedSkillText",
        "AttackSpeedSkillText",
        "QuickStepPassiveSkillText",
        "PowerShotSkillText"
    };

    private const float RowSpacing = 160f;

    private void Awake()
    {
        BuildScrollList();
    }

    private void BuildScrollList()
    {
        if (transform.Find("SkillListViewport") != null)
        {
            return;
        }

        CreatePowerShotEntry();

        // [스킬창 스크롤 추가] 기존 스킬 스킨은 유지하고 목록만 클리핑합니다.
        RectTransform viewport = CreateRect("SkillListViewport", transform);
        viewport.anchoredPosition = new Vector2(0f, -30f);
        viewport.sizeDelta = new Vector2(360f, 480f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect("SkillListContent", viewport);
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(360f, RowSpacing * IconNames.Length);

        for (int i = 0; i < IconNames.Length; i++)
        {
            MoveEntry(IconNames[i], content, -127f, i);
            MoveEntry(TextNames[i], content, 47.126f, i);
        }

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
