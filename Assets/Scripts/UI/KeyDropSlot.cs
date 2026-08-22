using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KeyDropSlot : MonoBehaviour,
    IDropHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    private const float AssignedIconSize = 66f;

    [SerializeField] private string keyName = "Q";
    [SerializeField] private Image assignedSkillIcon;

    private Image dropAreaImage;

    private KeySettingSkillType assignedSkillType =
        KeySettingSkillType.None;

    private bool isConfigured;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        dropAreaImage = GetComponent<Image>();

        EnsureAssignedSkillIcon();
    }

    private void Start()
    {
        RectTransform slotRect =
            transform as RectTransform;

        if (slotRect != null)
        {
            slotRect.anchoredPosition +=
                Vector2.right * 4.5f;
        }

        ApplyAssignedIconLayout();
    }

    private void OnEnable()
    {
        /*
         * 여기서 RestoreSavedSkill() 호출 금지.
         *
         * 런타임 생성 슬롯은 Configure() 전에
         * keyName 기본값 Q를 가지고 있기 때문에
         * 다시 DownStrike 도배 문제가 발생할 수 있습니다.
         */

        EnsureAssignedSkillIcon();

        ApplyAssignedIconLayout();
    }


    // =========================================================
    // Configure
    // =========================================================

    public void Configure(
        string configuredKeyName)
    {
        if (string.IsNullOrWhiteSpace(
                configuredKeyName))
        {
            return;
        }

        keyName =
            configuredKeyName;

        isConfigured =
            true;

        RestoreSavedSkill();
    }


    // =========================================================
    // 외부 강제 Refresh
    // =========================================================

    public void RefreshFromSavedBinding()
    {
        /*
         * 이미 Configure된 슬롯만 복원합니다.
         *
         * 이를 통해 GoblinBoss_Network에서
         * . 키로 UI를 열 때 저장된 키 설정을
         * 다시 화면에 반영할 수 있습니다.
         */

        if (!isConfigured)
        {
            return;
        }

        RestoreSavedSkill();
    }


    // =========================================================
    // Drag
    // =========================================================

    public void OnDrop(
        PointerEventData eventData)
    {
        if (eventData.pointerDrag == null)
        {
            return;
        }

        SkillIconDragHandler draggedSkill =
            eventData.pointerDrag
                .GetComponent<SkillIconDragHandler>();

        TryAssign(draggedSkill);
    }

    public bool TryAssign(
        SkillIconDragHandler draggedSkill)
    {
        EnsureAssignedSkillIcon();

        if (!isConfigured)
        {
            return false;
        }

        if (draggedSkill == null ||
            draggedSkill.SkillIcon == null ||
            draggedSkill.SkillType ==
            KeySettingSkillType.None)
        {
            return false;
        }

        KeyDropSlot[] allSlots =
            FindObjectsByType<KeyDropSlot>(
                FindObjectsSortMode.None);

        for (int i = 0;
             i < allSlots.Length;
             i++)
        {
            KeyDropSlot slot =
                allSlots[i];

            if (slot == null ||
                slot == this)
            {
                continue;
            }

            // [Codex Skill Select Category Limit]
            // 같은 카테고리의 기존 선택은 화면에서도 바로 자동 해제합니다.
            if (slot.assignedSkillType ==
                    draggedSkill.SkillType ||
                KeyBindingManager.IsSameSkillCategory(
                    slot.assignedSkillType,
                    draggedSkill.SkillType))
            {
                slot.ClearAssignedSkill();
            }
        }

        assignedSkillType =
            draggedSkill.SkillType;

        ClearAssignedIconSprite();

        assignedSkillIcon.sprite =
            CreateAssignedIconSprite(
                draggedSkill.SkillIcon);

        assignedSkillIcon.preserveAspect =
            false;
        assignedSkillIcon.type =
            Image.Type.Simple;

        assignedSkillIcon.gameObject
            .SetActive(true);

        ApplyAssignedIconLayout();

        draggedSkill.NotifySuccessfulDrop();


        if (KeyBindingManager.Instance != null)
        {
            KeyBindingManager.Instance.Assign(
                keyName,
                assignedSkillType);
        }
        else
        {
            KeyBindingManager.SaveBinding(
                keyName,
                assignedSkillType);
        }

        Debug.Log(
            $"[키 설정] 배치: " +
            $"{keyName} = {assignedSkillType}");

        SetDropAreaColor(0f);

        return true;
    }


    // =========================================================
    // Restore
    // =========================================================

    private void RestoreSavedSkill()
    {
        if (!isConfigured)
        {
            return;
        }

        EnsureAssignedSkillIcon();

        // 먼저 이전 상태 완전 제거
        assignedSkillType =
            KeySettingSkillType.None;

        ClearVisualOnly();


        if (!KeyBindingManager.TryGetSavedBinding(
                keyName,
                out KeySettingSkillType savedSkillType))
        {
            return;
        }

        if (savedSkillType ==
            KeySettingSkillType.None)
        {
            return;
        }

        assignedSkillType =
            savedSkillType;

        RefreshAssignedSkillIcon();

        /*
         * 복원할 때 Assign()을 다시 호출하면 안 됩니다.
         * 여기서는 읽기만 합니다.
         */

        Debug.Log(
            $"[키 설정] UI 복원: " +
            $"{keyName} = {savedSkillType}");
    }


    // =========================================================
    // Icon
    // =========================================================

    private void RefreshAssignedSkillIcon()
    {
        EnsureAssignedSkillIcon();

        if (assignedSkillType ==
            KeySettingSkillType.None)
        {
            ClearVisualOnly();
            return;
        }

        SkillIconDragHandler[] skillIcons =
            FindObjectsByType<SkillIconDragHandler>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0;
             i < skillIcons.Length;
             i++)
        {
            SkillIconDragHandler skill =
                skillIcons[i];

            if (skill == null)
            {
                continue;
            }

            if (skill.SkillType !=
                assignedSkillType)
            {
                continue;
            }

            if (skill.SkillIcon == null)
            {
                continue;
            }

            ClearAssignedIconSprite();

            assignedSkillIcon.sprite =
                CreateAssignedIconSprite(
                    skill.SkillIcon);

            assignedSkillIcon.preserveAspect =
                false;
            assignedSkillIcon.type =
                Image.Type.Simple;

            assignedSkillIcon.gameObject
                .SetActive(true);

            ApplyAssignedIconLayout();

            return;
        }

        ClearVisualOnly();
    }


    // =========================================================
    // Clear
    // =========================================================

    private void ClearAssignedSkill()
    {
        ClearVisualOnly();

        if (isConfigured)
        {
            if (KeyBindingManager.Instance != null)
            {
                KeyBindingManager.Instance.Unassign(
                    keyName);
            }
            else
            {
                KeyBindingManager.RemoveSavedBinding(
                    keyName);
            }
        }

        assignedSkillType =
            KeySettingSkillType.None;
    }

    private void ClearVisualOnly()
    {
        if (assignedSkillIcon == null)
        {
            return;
        }

        ClearAssignedIconSprite();

        assignedSkillIcon.gameObject
            .SetActive(false);
    }

    private Sprite CreateAssignedIconSprite(
        Sprite sourceSprite)
    {
        if (sourceSprite == null)
        {
            return null;
        }

        // [Codex Assigned Icon Copy]
        // 키보드 슬롯 아이콘은 스킬 패널 원본 Image와 독립된 런타임 Sprite로 표시합니다.
        Sprite copiedSprite =
            Instantiate(sourceSprite);

        copiedSprite.name =
            sourceSprite.name + "_KeySlotCopy";

        return copiedSprite;
    }

    private void ClearAssignedIconSprite()
    {
        if (assignedSkillIcon == null ||
            assignedSkillIcon.sprite == null)
        {
            return;
        }

        Sprite previousSprite =
            assignedSkillIcon.sprite;

        assignedSkillIcon.sprite =
            null;

        if (previousSprite.name.EndsWith(
                "_KeySlotCopy",
                System.StringComparison.Ordinal))
        {
            Destroy(previousSprite);
        }
    }


    // =========================================================
    // Pointer
    // =========================================================

    public void OnPointerEnter(
        PointerEventData eventData)
    {
        if (eventData.pointerDrag != null &&
            eventData.pointerDrag
                .GetComponent<SkillIconDragHandler>() != null)
        {
            SetDropAreaColor(0.22f);
        }
    }

    public void OnPointerExit(
        PointerEventData eventData)
    {
        SetDropAreaColor(0f);
    }

    private void SetDropAreaColor(float alpha)
    {
        if (dropAreaImage != null)
        {
            dropAreaImage.color =
                new Color(
                    0.45f,
                    1f,
                    0.35f,
                    alpha);
        }
    }


    // =========================================================
    // Create Icon
    // =========================================================

    private void EnsureAssignedSkillIcon()
    {
        if (assignedSkillIcon != null)
        {
            ApplyAssignedIconLayout();
            return;
        }

        GameObject iconObject =
            new GameObject(
                "AssignedSkillIcon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform iconRect =
            iconObject.GetComponent<RectTransform>();

        iconRect.SetParent(
            transform,
            false);

        iconRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        iconRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        iconRect.pivot =
            new Vector2(0.5f, 0.5f);

        iconRect.anchoredPosition =
            Vector2.zero;

        assignedSkillIcon =
            iconObject.GetComponent<Image>();

        assignedSkillIcon.raycastTarget =
            false;

        assignedSkillIcon.preserveAspect =
            false;
        assignedSkillIcon.type =
            Image.Type.Simple;

        assignedSkillIcon.sprite =
            null;

        ApplyAssignedIconLayout();

        iconObject.SetActive(false);
    }


    private void ApplyAssignedIconLayout()
    {
        if (assignedSkillIcon == null)
        {
            return;
        }

        RectTransform iconRect =
            assignedSkillIcon.rectTransform;

        iconRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        iconRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        iconRect.pivot =
            new Vector2(0.5f, 0.5f);

        iconRect.anchoredPosition3D =
            Vector3.zero;

        iconRect.localRotation =
            Quaternion.identity;

        iconRect.localScale =
            Vector3.one;

        iconRect.sizeDelta =
            Vector2.one * AssignedIconSize;

        assignedSkillIcon.preserveAspect =
            false;
        assignedSkillIcon.type =
            Image.Type.Simple;
    }
}
