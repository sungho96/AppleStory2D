using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillIconDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas rootCanvas;
    private Image iconImage;
    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPosition;
    private bool dropHandled;

    // 키 슬롯은 원본 오브젝트를 옮기지 않고 이 스프라이트만 복사해 표시합니다.
    public Sprite SkillIcon => iconImage != null ? iconImage.sprite : null;
    public KeySettingSkillType SkillType => skillType;

    [SerializeField] private KeySettingSkillType skillType;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        iconImage = GetComponent<Image>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rectTransform == null || rootCanvas == null)
        {
            return;
        }

        // 드래그가 끝나면 스킬 목록의 정확한 원래 위치로 돌아갈 수 있도록 저장합니다.
        originalParent = rectTransform.parent;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        dropHandled = false;

        // 다른 UI보다 위에 보이게 최상위 Canvas로 잠시 이동합니다.
        rectTransform.SetParent(rootCanvas.transform, true);
        rectTransform.SetAsLastSibling();

        // 다음 단계에서 키 슬롯이 아이콘 아래의 포인터를 받을 수 있도록 막지 않습니다.
        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
        }

        MoveToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveToPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (originalParent == null)
        {
            return;
        }

        // 일부 UI 계층에서 OnDrop 전달이 누락되어도 포인터 아래 슬롯을 직접 찾아 배치합니다.
        if (!dropHandled)
        {
            TryDropOnSlot(eventData);
        }

        // 키에는 복사본을 표시하고 스킬 목록의 원본 아이콘은 항상 제자리로 복귀합니다.
        rectTransform.SetParent(originalParent, false);
        rectTransform.SetSiblingIndex(originalSiblingIndex);
        rectTransform.anchoredPosition = originalAnchoredPosition;

        if (iconImage != null)
        {
            iconImage.raycastTarget = true;
        }
    }

    public void NotifySuccessfulDrop()
    {
        // 정상 OnDrop이 처리됐음을 기록해 보조 Raycast의 중복 배치를 막습니다.
        dropHandled = true;
    }

    public void ConfigureSkillType(KeySettingSkillType configuredSkillType)
    {
        // [런타임 스킬 항목 지원] 복제한 아이콘의 실행 스킬만 안전하게 변경합니다.
        skillType = configuredSkillType;
    }

    private void TryDropOnSlot(PointerEventData eventData)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);

        foreach (RaycastResult result in raycastResults)
        {
            KeyDropSlot dropSlot = result.gameObject.GetComponentInParent<KeyDropSlot>();
            if (dropSlot != null && dropSlot.TryAssign(this))
            {
                return;
            }
        }
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        if (rectTransform == null || rootCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector3 worldPosition))
        {
            rectTransform.position = worldPosition;
        }
    }
}
