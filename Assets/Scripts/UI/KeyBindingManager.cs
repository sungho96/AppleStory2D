using System.Collections.Generic;
using UnityEngine;

public enum KeySettingSkillType
{
    None,
    MoveSpeedBuff,
    AttackSpeedBuff,
    PowerShot
}

public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }

    [SerializeField] private SpeedBuffController speedBuffController;
    [SerializeField] private PlayerAttack2D playerAttack;
    [SerializeField] private KeySettingUIController keySettingUIController;
    private readonly Dictionary<KeyCode, KeySettingSkillType> keyBindings = new();

    private void Awake()
    {
        Instance = this;

        if (speedBuffController == null)
        {
            speedBuffController = GetComponent<SpeedBuffController>();
        }

        if (keySettingUIController == null)
        {
            keySettingUIController = FindFirstObjectByType<KeySettingUIController>();
        }

        if (playerAttack == null)
        {
            // [파워 샷 연결] 매니저와 플레이어가 다른 오브젝트이므로 활성 플레이어 공격기를 찾습니다.
            playerAttack = FindFirstObjectByType<PlayerAttack2D>();
        }
    }

    private void Update()
    {
        // 키 설정 창에서 편집 중일 때는 배치된 스킬이 실수로 발동하지 않게 막습니다.
        if (keySettingUIController != null && keySettingUIController.IsOpen)
        {
            return;
        }

        foreach (KeyValuePair<KeyCode, KeySettingSkillType> binding in keyBindings)
        {
            if (binding.Value == KeySettingSkillType.PowerShot)
            {
                // [파워 샷 추가] 누르는 동안 차징하고 키를 떼는 순간 발사합니다.
                if (Input.GetKeyDown(binding.Key))
                {
                    playerAttack?.BeginPowerShotCharge();
                }

                if (Input.GetKeyUp(binding.Key))
                {
                    playerAttack?.ReleasePowerShot();
                }

                continue;
            }

            if (Input.GetKeyDown(binding.Key))
            {
                ExecuteSkill(binding.Value);
            }
        }
    }

    public void Assign(string keyName, KeySettingSkillType skillType)
    {
        if (!TryConvertKeyCode(keyName, out KeyCode keyCode))
        {
            Debug.LogWarning($"[키 설정] 지원하지 않는 키 이름입니다: {keyName}");
            return;
        }

        keyBindings[keyCode] = skillType;
        Debug.Log($"[키 설정] {keyName} 실행 스킬이 {skillType}(으)로 변경됐습니다.");
    }

    private void ExecuteSkill(KeySettingSkillType skillType)
    {
        if (speedBuffController == null)
        {
            Debug.LogWarning("[키 설정] SpeedBuffController가 연결되지 않았습니다.");
            return;
        }

        switch (skillType)
        {
            case KeySettingSkillType.MoveSpeedBuff:
                speedBuffController.UseSpeedBuff();
                break;

            case KeySettingSkillType.AttackSpeedBuff:
                speedBuffController.UseAttackSpeedBuff();
                break;

            case KeySettingSkillType.PowerShot:
                break;
        }
    }

    private bool TryConvertKeyCode(string keyName, out KeyCode keyCode)
    {
        // 화면 표기와 Unity KeyCode 이름이 다른 키들을 먼저 변환합니다.
        switch (keyName)
        {
            case "`":
                keyCode = KeyCode.BackQuote;
                return true;
            case "-":
                keyCode = KeyCode.Minus;
                return true;
            case "=":
                keyCode = KeyCode.Equals;
                return true;
            case "Ins":
                keyCode = KeyCode.Insert;
                return true;
            case "Del":
                keyCode = KeyCode.Delete;
                return true;
            case "PgUp":
                keyCode = KeyCode.PageUp;
                return true;
            case "PgDn":
                keyCode = KeyCode.PageDown;
                return true;
        }

        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            return System.Enum.TryParse("Alpha" + keyName, out keyCode);
        }

        return System.Enum.TryParse(keyName, true, out keyCode);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
