using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum KeySettingSkillType
{
    None,
    MoveSpeedBuff,
    AttackSpeedBuff,
    PowerShot,
    RapidVolley,
    WarriorDownStrike,
    WarriorShieldBlock
}

public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }

    [SerializeField] private SpeedBuffController speedBuffController;
    [SerializeField] private PlayerAttack2D playerAttack;
    [SerializeField] private WarriorDownStrike2D warriorDownStrike;
    [SerializeField] private WarriorShieldBlock2D warriorShieldBlock;
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
            playerAttack = FindLocalPlayerComponent<PlayerAttack2D>();
        }

        if (warriorDownStrike == null)
        {
            // [Codex Warrior Skill Binding] 네트워크 방에서는 내 소유 전사 스킬을 우선 연결합니다.
            warriorDownStrike = FindLocalPlayerComponent<WarriorDownStrike2D>();
        }

        if (warriorShieldBlock == null)
        {
            // [Codex Warrior Skill Binding] 방패막기도 키 매니저에서 직접 실행할 수 있게 찾습니다.
            warriorShieldBlock = FindLocalPlayerComponent<WarriorShieldBlock2D>();
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

    public void Unassign(string keyName)
    {
        // [키 중복 배치 수정] 스킬이 다른 키로 이동하면 이전 키 바인딩을 제거합니다.
        if (!TryConvertKeyCode(keyName, out KeyCode keyCode))
            return;

        keyBindings.Remove(keyCode);
    }

    private void ExecuteSkill(KeySettingSkillType skillType)
    {
        // [래피드 볼리 키매핑 복구] 공격 스킬은 버프 컨트롤러 검사와 분리해 직접 실행합니다.
        if (skillType == KeySettingSkillType.RapidVolley)
        {
            EnsurePlayerAttack();
            if (playerAttack != null)
                playerAttack.UseRapidVolley();
            else
                Debug.LogWarning("[키 설정] PlayerAttack2D가 연결되지 않았습니다.");
            return;
        }

        // 파워샷은 Update에서 KeyDown/KeyUp을 따로 처리하므로 여기서는 실행하지 않습니다.
        if (skillType == KeySettingSkillType.PowerShot)
            return;

        if (skillType == KeySettingSkillType.WarriorDownStrike)
        {
            EnsureWarriorSkills();
            if (warriorDownStrike != null)
                warriorDownStrike.UseDownStrike();
            else
                Debug.LogWarning("[키 설정] WarriorDownStrike2D가 연결되지 않았습니다.");
            return;
        }

        if (skillType == KeySettingSkillType.WarriorShieldBlock)
        {
            EnsureWarriorSkills();
            if (warriorShieldBlock != null)
                warriorShieldBlock.UseShieldBlock();
            else
                Debug.LogWarning("[키 설정] WarriorShieldBlock2D가 연결되지 않았습니다.");
            return;
        }

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
            case KeySettingSkillType.RapidVolley:
            case KeySettingSkillType.WarriorDownStrike:
            case KeySettingSkillType.WarriorShieldBlock:
                break;
        }
    }

    private void EnsurePlayerAttack()
    {
        if (playerAttack == null)
            playerAttack = FindLocalPlayerComponent<PlayerAttack2D>();
    }

    private void EnsureWarriorSkills()
    {
        if (warriorDownStrike == null)
            warriorDownStrike = FindLocalPlayerComponent<WarriorDownStrike2D>();
        if (warriorShieldBlock == null)
            warriorShieldBlock = FindLocalPlayerComponent<WarriorShieldBlock2D>();
        if (warriorShieldBlock == null && warriorDownStrike != null)
        {
            // [Codex Warrior ShieldBlock] 프리팹 연결 전에도 워리어 스킬 테스트가 가능하도록 같은 플레이어에 보장합니다.
            warriorShieldBlock = warriorDownStrike.gameObject.AddComponent<WarriorShieldBlock2D>();
        }
    }

    private T FindLocalPlayerComponent<T>() where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            NetworkObject networkObject = components[i].GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned && networkObject.IsOwner)
                return components[i];
        }

        return components.Length > 0 ? components[0] : null;
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
