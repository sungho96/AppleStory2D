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
        keySettingUI.SetActive(!keySettingUI.activeSelf);
    }

    public void Open()
    {
        keySettingUI.SetActive(true);
    }

    public void Close()
    {
        keySettingUI.SetActive(false);
    }
}
