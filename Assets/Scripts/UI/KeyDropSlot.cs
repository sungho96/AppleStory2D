using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KeyDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    // [키 슬롯 아이콘 크기 통일] 모든 스킬을 동일한 정사각형 크기로 표시합니다.
    private const float AssignedIconSize = 62f;

    [SerializeField] private string keyName = "Q";
    [SerializeField] private Image assignedSkillIcon;
    private Image dropAreaImage;
    private KeySettingSkillType assignedSkillType = KeySettingSkillType.None;

    private void Start()
    {
        // [Free Aspect 중앙 정렬] 키 스킨 중심과 드롭 슬롯 사이의 공통 4.5px 오차를 보정합니다.
        RectTransform slotRect = transform as RectTransform;
        if (slotRect != null)
            slotRect.anchoredPosition += Vector2.right * 4.5f;

        ApplyAssignedIconLayout();
    }

    private void OnEnable()
    {
        // [매핑 아이콘 재정렬] 키세팅 창을 다시 열 때 모든 슬롯의 이미지를 정중앙으로 복구합니다.
        if (assignedSkillIcon != null)
            ApplyAssignedIconLayout();
    }

    private void Awake()
    {
        dropAreaImage = GetComponent<Image>();
        EnsureAssignedSkillIcon();
    }

    public void Configure(string configuredKeyName)
    {
        // 배치 도구가 생성한 슬롯에도 Console과 저장용 키 이름을 정확히 전달합니다.
        keyName = configuredKeyName;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
        {
            return;
        }

        SkillIconDragHandler draggedSkill =
            eventData.pointerDrag.GetComponent<SkillIconDragHandler>();

        TryAssign(draggedSkill);
    }

    public bool TryAssign(SkillIconDragHandler draggedSkill)
    {
        EnsureAssignedSkillIcon();

        if (assignedSkillIcon == null || draggedSkill == null || draggedSkill.SkillIcon == null)
        {
            return false;
        }

        // 스킬 목록의 원본은 유지하고, 키 위에는 아이콘 스프라이트만 복사합니다.
        // [키 중복 배치 수정] 같은 스킬의 이전 키를 비워 한 스킬당 한 자리만 유지합니다.
        foreach (KeyDropSlot slot in FindObjectsByType<KeyDropSlot>(FindObjectsSortMode.None))
        {
            if (slot != this && slot.assignedSkillType == draggedSkill.SkillType)
                slot.ClearAssignedSkill();
        }

        assignedSkillIcon.sprite = draggedSkill.SkillIcon;
        assignedSkillIcon.preserveAspect = true;
        assignedSkillIcon.gameObject.SetActive(true);
        assignedSkillType = draggedSkill.SkillType;
        draggedSkill.NotifySuccessfulDrop();

        // 화면에 배치한 키 이름과 스킬 종류를 실제 입력 관리자에 함께 전달합니다.
        if (KeyBindingManager.Instance != null)
        {
            KeyBindingManager.Instance.Assign(keyName, draggedSkill.SkillType);
        }

        Debug.Log($"[키 설정] {keyName} 키에 스킬을 임시 배치했습니다.");
        SetDropAreaColor(0f);
        return true;
    }

    private void ClearAssignedSkill()
    {
        if (assignedSkillType == KeySettingSkillType.None)
            return;

        // [키 중복 배치 수정] 이전 아이콘과 실제 키 입력을 함께 제거합니다.
        if (assignedSkillIcon != null)
        {
            assignedSkillIcon.sprite = null;
            assignedSkillIcon.gameObject.SetActive(false);
        }

        KeyBindingManager.Instance?.Unassign(keyName);
        assignedSkillType = KeySettingSkillType.None;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 드래그 중 Q 키의 실제 판정 범위를 눈으로 확인할 수 있게 표시합니다.
        if (eventData.pointerDrag != null &&
            eventData.pointerDrag.GetComponent<SkillIconDragHandler>() != null)
        {
            SetDropAreaColor(0.22f);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetDropAreaColor(0f);
    }

    private void SetDropAreaColor(float alpha)
    {
        if (dropAreaImage != null)
        {
            dropAreaImage.color = new Color(0.45f, 1f, 0.35f, alpha);
        }
    }

    private void EnsureAssignedSkillIcon()
    {
        if (assignedSkillIcon != null)
        {
            ApplyAssignedIconLayout();
            return;
        }

        // 반복되는 키 슬롯은 표시용 아이콘을 런타임에 생성해 씬 구조를 단순하게 유지합니다.
        GameObject iconObject = new GameObject(
            "AssignedSkillIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(transform, false);
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        // 키 위에서 스킬이 확실히 보이도록 기존 60px보다 30% 크게 표시합니다.
        assignedSkillIcon = iconObject.GetComponent<Image>();
        assignedSkillIcon.raycastTarget = false;
        ApplyAssignedIconLayout();
        iconObject.SetActive(false);
    }

    private void ApplyAssignedIconLayout()
    {
        // [키 슬롯 아이콘 크기 통일] 기존 씬 참조와 런타임 생성 아이콘 모두 같은 규격을 사용합니다.
        RectTransform iconRect = assignedSkillIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition3D = Vector3.zero;
        iconRect.localRotation = Quaternion.identity;
        iconRect.localScale = Vector3.one;
        iconRect.sizeDelta = Vector2.one * AssignedIconSize;
        assignedSkillIcon.preserveAspect = true;
    }
}
