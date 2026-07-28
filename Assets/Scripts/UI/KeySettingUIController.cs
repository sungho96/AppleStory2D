using UnityEngine;

public class KeySettingUIController : MonoBehaviour
{
    [SerializeField] private GameObject keySettingUI;
    // 문자 스킬 키와 충돌하지 않는 마침표 키로 설정 창을 열고 닫습니다.
    [SerializeField] private KeyCode toggleKey = KeyCode.Period;
    [SerializeField] private bool startClosed = true;

    public bool IsOpen => keySettingUI != null && keySettingUI.activeSelf;

    private void Awake()
    {
        // 편집 중에는 UI 배치를 볼 수 있게 유지하고, 실제 플레이 시작 시에만 닫습니다.
        if (startClosed && keySettingUI != null)
        {
            keySettingUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (keySettingUI == null)
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
            return;
        }

        if (keySettingUI.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Toggle()
    {
        bool willOpen = !keySettingUI.activeSelf;
        keySettingUI.SetActive(willOpen);
        if (willOpen)
            FitContentToCanvas();
    }

    public void Open()
    {
        keySettingUI.SetActive(true);
        FitContentToCanvas();
    }

    public void Close()
    {
        keySettingUI.SetActive(false);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (keySettingUI != null && keySettingUI.activeSelf)
            FitContentToCanvas();
    }

    private void FitContentToCanvas()
    {
        if (keySettingUI == null)
            return;

        RectTransform content = keySettingUI.transform.Find("Content") as RectTransform;
        RectTransform contentParent = content != null
            ? content.parent as RectTransform
            : null;
        if (content == null || contentParent == null)
            return;

        Canvas.ForceUpdateCanvases();

        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
        Bounds naturalBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            contentParent,
            content);

        // [Free Aspect 실제 Bounds 맞춤] 현재 렌더링되는 두 패널의 크기로 축소 비율을 계산합니다.
        const float screenMargin = 0.96f;
        float widthScale = contentParent.rect.width * screenMargin /
            Mathf.Max(1f, naturalBounds.size.x);
        float heightScale = contentParent.rect.height * screenMargin /
            Mathf.Max(1f, naturalBounds.size.y);
        float fitScale = Mathf.Min(1f, widthScale, heightScale);
        content.localScale = Vector3.one * fitScale;

        Canvas.ForceUpdateCanvases();
        Bounds fittedBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            contentParent,
            content);

        // [Free Aspect 실제 중앙 정렬] 축소 후 보이는 Bounds 중심을 부모 화면 중심과 정확히 일치시킵니다.
        Vector2 centerOffset = (Vector2)contentParent.rect.center -
            (Vector2)fittedBounds.center;
        content.anchoredPosition += centerOffset;
    }
}
