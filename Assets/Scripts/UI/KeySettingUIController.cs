using System.Collections;
using UnityEngine;

public class KeySettingUIController : MonoBehaviour
{
    [SerializeField] private GameObject keySettingUI;

    // 마침표 키
    [SerializeField] private KeyCode toggleKey = KeyCode.Period;

    [SerializeField] private bool startClosed = true;

    public bool IsOpen =>
        keySettingUI != null &&
        keySettingUI.activeSelf;


    private void Awake()
    {
        if (startClosed &&
            keySettingUI != null)
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

        if (keySettingUI.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }


    // =========================================================
    // Toggle
    // =========================================================

    public void Toggle()
    {
        if (keySettingUI == null)
        {
            return;
        }

        bool willOpen =
            !keySettingUI.activeSelf;

        if (!willOpen)
        {
            Close();
            return;
        }

        Open();
    }


    // =========================================================
    // Open
    // =========================================================

    public void Open()
    {
        if (keySettingUI == null)
        {
            return;
        }

        // UI 먼저 활성화
        keySettingUI.SetActive(true);

        /*
         * 중요:
         *
         * GoblinBoss_Network 씬이 막 로드된 순간에는
         * NetworkManager / KeyBindingManager / KeyDropSlot
         * Awake 실행 순서가 뒤섞일 수 있습니다.
         *
         * 그래서 한 프레임 뒤에 현재 Host/Client 프로필을
         * 다시 결정하고 저장값을 복원합니다.
         */
        StopAllCoroutines();
        StartCoroutine(
            RefreshBindingsNextFrame());
    }


    private IEnumerator RefreshBindingsNextFrame()
    {
        // 씬의 모든 Awake / OnEnable 처리가 끝나도록 한 프레임 기다림
        yield return null;


        // =====================================================
        // 1. 현재 Host / Client 프로필 다시 확정
        // =====================================================

        if (KeyBindingManager.Instance != null)
        {
            KeyBindingManager.Instance
                .RefreshProfileAndBindings();

            Debug.Log(
                "[키 설정] 보스씬 현재 프로필/바인딩 재확인 완료");
        }
        else
        {
            Debug.LogWarning(
                "[키 설정] KeyBindingManager.Instance가 없습니다.");
        }


        // 한 프레임 더 기다려 UI 생성 완료 보장
        yield return null;


        // =====================================================
        // 2. 모든 KeyDropSlot 저장값 다시 읽기
        // =====================================================

        RefreshAllKeySlots();


        // =====================================================
        // 3. UI 위치 보정
        // =====================================================

        FitContentToCanvas();
    }


    // =========================================================
    // Slot Refresh
    // =========================================================

    private void RefreshAllKeySlots()
    {
        if (keySettingUI == null)
        {
            return;
        }

        KeyDropSlot[] slots =
            keySettingUI
                .GetComponentsInChildren<KeyDropSlot>(
                    true);


        for (int i = 0;
             i < slots.Length;
             i++)
        {
            if (slots[i] == null)
            {
                continue;
            }

            slots[i]
                .RefreshFromSavedBinding();
        }


        Debug.Log(
            $"[키 설정] 저장된 키매핑 UI 복원 완료: " +
            $"{slots.Length} 슬롯");
    }


    // =========================================================
    // Close
    // =========================================================

    public void Close()
    {
        StopAllCoroutines();

        if (keySettingUI != null)
        {
            keySettingUI.SetActive(false);
        }
    }


    // =========================================================
    // Resize
    // =========================================================

    private void OnRectTransformDimensionsChange()
    {
        if (keySettingUI != null &&
            keySettingUI.activeSelf)
        {
            FitContentToCanvas();
        }
    }


    private void FitContentToCanvas()
    {
        if (keySettingUI == null)
        {
            return;
        }

        RectTransform content =
            keySettingUI.transform.Find("Content")
            as RectTransform;


        RectTransform contentParent =
            content != null
                ? content.parent as RectTransform
                : null;


        if (content == null ||
            contentParent == null)
        {
            return;
        }


        Canvas.ForceUpdateCanvases();


        content.localScale =
            Vector3.one;

        content.anchoredPosition =
            Vector2.zero;


        Bounds naturalBounds =
            RectTransformUtility
                .CalculateRelativeRectTransformBounds(
                    contentParent,
                    content);


        const float screenMargin =
            0.96f;


        float widthScale =
            contentParent.rect.width *
            screenMargin /
            Mathf.Max(
                1f,
                naturalBounds.size.x);


        float heightScale =
            contentParent.rect.height *
            screenMargin /
            Mathf.Max(
                1f,
                naturalBounds.size.y);


        float fitScale =
            Mathf.Min(
                1f,
                widthScale,
                heightScale);


        content.localScale =
            Vector3.one * fitScale;


        Canvas.ForceUpdateCanvases();


        Bounds fittedBounds =
            RectTransformUtility
                .CalculateRelativeRectTransformBounds(
                    contentParent,
                    content);


        Vector2 centerOffset =
            (Vector2)contentParent.rect.center -
            (Vector2)fittedBounds.center;


        content.anchoredPosition +=
            centerOffset;
    }
}