using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class KeyboardDropSlotLayout : MonoBehaviour
{
    private static readonly Vector2 SlotSize =
        new Vector2(66f, 67f);

    private readonly struct SlotDefinition
    {
        public readonly string KeyName;
        public readonly string ObjectName;
        public readonly Vector2 Position;

        public SlotDefinition(
            string keyName,
            string objectName,
            float x,
            float y)
        {
            KeyName = keyName;
            ObjectName = objectName;
            Position = new Vector2(x, y);
        }
    }


    private static readonly SlotDefinition[] Slots =
    {
        // =====================================================
        // F1 ~ F12
        // =====================================================

        new SlotDefinition("F1", "F1", -459.6f, 212.8f),
        new SlotDefinition("F2", "F2", -389.1f, 212.8f),
        new SlotDefinition("F3", "F3", -319.3f, 212.8f),
        new SlotDefinition("F4", "F4", -248.8f, 212.8f),

        new SlotDefinition("F5", "F5", -139.9f, 212.8f),
        new SlotDefinition("F6", "F6", -70.1f, 212.8f),
        new SlotDefinition("F7", "F7", 0.4f, 212.8f),
        new SlotDefinition("F8", "F8", 70.9f, 212.8f),

        new SlotDefinition("F9", "F9", 175.2f, 212.8f),
        new SlotDefinition("F10", "F10", 245.7f, 212.8f),
        new SlotDefinition("F11", "F11", 315.4f, 212.8f),
        new SlotDefinition("F12", "F12", 385.2f, 212.8f),


        // =====================================================
        // ` ~ =
        // =====================================================

        new SlotDefinition("`", "BackQuote", -580.3f, 123f),

        new SlotDefinition("1", "1", -512f, 123f),
        new SlotDefinition("2", "2", -442.3f, 123f),
        new SlotDefinition("3", "3", -373.4f, 123f),
        new SlotDefinition("4", "4", -304.4f, 123f),
        new SlotDefinition("5", "5", -235.5f, 123f),
        new SlotDefinition("6", "6", -166.5f, 123f),
        new SlotDefinition("7", "7", -97.6f, 123f),
        new SlotDefinition("8", "8", -28.6f, 123f),
        new SlotDefinition("9", "9", 40.4f, 123f),
        new SlotDefinition("0", "0", 109.3f, 123f),

        new SlotDefinition("-", "Minus", 178.3f, 123f),
        new SlotDefinition("=", "Equals", 245.7f, 123f),


        // =====================================================
        // Q ~ P
        // =====================================================

        new SlotDefinition("Q", "Q", -476.0f, 50.5f),
        new SlotDefinition("W", "W", -407.0f, 50.5f),
        new SlotDefinition("E", "E", -338.0f, 50.5f),
        new SlotDefinition("R", "R", -269.0f, 50.5f),

        new SlotDefinition("T", "T", -200.2f, 50.5f),
        new SlotDefinition("Y", "Y", -131.3f, 50.5f),
        new SlotDefinition("U", "U", -62.3f, 50.5f),
        new SlotDefinition("I", "I", 6.7f, 50.5f),
        new SlotDefinition("O", "O", 75.6f, 50.5f),
        new SlotDefinition("P", "P", 144.6f, 50.5f),


        // =====================================================
        // A ~ L
        // =====================================================

        new SlotDefinition("A", "A", -461.8f, -22.3f),
        new SlotDefinition("S", "S", -392.2f, -22.3f),
        new SlotDefinition("D", "D", -323.3f, -22.3f),
        new SlotDefinition("F", "F", -254.3f, -22.3f),
        new SlotDefinition("G", "G", -184.6f, -22.3f),
        new SlotDefinition("H", "H", -115.6f, -22.3f),
        new SlotDefinition("J", "J", -46.6f, -22.3f),
        new SlotDefinition("K", "K", 22.3f, -22.3f),
        new SlotDefinition("L", "L", 91.3f, -22.3f),


        // =====================================================
        // Z ~ M
        // =====================================================

        new SlotDefinition("Z", "Z", -427.5f, -96f),
        new SlotDefinition("X", "X", -357.8f, -96f),
        new SlotDefinition("C", "C", -288.8f, -96f),
        new SlotDefinition("V", "V", -219.8f, -96f),
        new SlotDefinition("B", "B", -150.9f, -96f),
        new SlotDefinition("N", "N", -81.9f, -96f),
        new SlotDefinition("M", "M", -12.9f, -96f)
    };


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        foreach (SlotDefinition slot in Slots)
        {
            CreateOrRefreshDropSlot(slot);
        }
    }


    // =========================================================
    // 생성 또는 기존 슬롯 재사용
    // =========================================================

    private void CreateOrRefreshDropSlot(
        SlotDefinition slot)
    {
        string slotObjectName =
            slot.ObjectName + "KeyDropSlot";


        // =====================================================
        // 이미 존재하는 슬롯
        // =====================================================

        Transform existing =
            transform.Find(slotObjectName);


        if (existing != null)
        {
            SetupExistingSlot(
                existing.gameObject,
                slot);

            return;
        }


        // =====================================================
        // 새 슬롯 생성
        // =====================================================

        GameObject slotObject =
            new GameObject(
                slotObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(KeyDropSlot));


        SetupRect(
            slotObject,
            slot);


        SetupDropAreaImage(
            slotObject);


        KeyDropSlot keyDropSlot =
            slotObject.GetComponent<KeyDropSlot>();


        // 실제 키 이름 전달 후 저장값 복원
        keyDropSlot.Configure(
            slot.KeyName);
    }


    // =========================================================
    // 기존 슬롯 처리
    // =========================================================

    private void SetupExistingSlot(
        GameObject slotObject,
        SlotDefinition slot)
    {
        RectTransform slotRect =
            slotObject.GetComponent<RectTransform>();


        if (slotRect == null)
        {
            Debug.LogWarning(
                $"[키 설정] {slotObject.name}에 " +
                "RectTransform이 없습니다.");

            return;
        }


        // 위치와 크기도 현재 설정으로 다시 맞춤
        slotRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        slotRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        slotRect.pivot =
            new Vector2(0.5f, 0.5f);

        slotRect.anchoredPosition =
            slot.Position;

        slotRect.sizeDelta =
            SlotSize;


        // Image 보장
        Image dropAreaImage =
            slotObject.GetComponent<Image>();


        if (dropAreaImage == null)
        {
            dropAreaImage =
                slotObject.AddComponent<Image>();
        }


        dropAreaImage.color =
            new Color(
                1f,
                1f,
                1f,
                0f);

        dropAreaImage.raycastTarget =
            true;


        // KeyDropSlot 보장
        KeyDropSlot keyDropSlot =
            slotObject.GetComponent<KeyDropSlot>();


        if (keyDropSlot == null)
        {
            keyDropSlot =
                slotObject.AddComponent<KeyDropSlot>();
        }


        /*
         * ★ 중요
         *
         * 예전 코드는 기존 슬롯을 발견하면
         * 그냥 return 했습니다.
         *
         * 그러면 이전 Play에서 씬/프리팹에 남아 있던
         * AssignedSkillIcon 상태가 그대로 살아 있을 수 있습니다.
         *
         * 이제 기존 슬롯도 반드시 Configure합니다.
         */

        keyDropSlot.Configure(
            slot.KeyName);


        Debug.Log(
            $"[키 설정] 기존 슬롯 재설정: " +
            $"{slot.KeyName}");
    }


    // =========================================================
    // Rect
    // =========================================================

    private void SetupRect(
        GameObject slotObject,
        SlotDefinition slot)
    {
        RectTransform slotRect =
            slotObject.GetComponent<RectTransform>();


        slotRect.SetParent(
            transform,
            false);


        slotRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        slotRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        slotRect.pivot =
            new Vector2(0.5f, 0.5f);


        slotRect.anchoredPosition =
            slot.Position;


        slotRect.sizeDelta =
            SlotSize;
    }


    // =========================================================
    // 투명 드롭 영역
    // =========================================================

    private void SetupDropAreaImage(
        GameObject slotObject)
    {
        Image dropAreaImage =
            slotObject.GetComponent<Image>();


        if (dropAreaImage == null)
        {
            dropAreaImage =
                slotObject.AddComponent<Image>();
        }


        dropAreaImage.color =
            new Color(
                1f,
                1f,
                1f,
                0f);


        dropAreaImage.raycastTarget =
            true;
    }
}