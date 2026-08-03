using System.Collections.Generic;
using UnityEngine;

public enum KeySettingSkillType
{
    None,
    MoveSpeedBuff,
    AttackSpeedBuff,
    AngerBuff,
    DownStrike,
    PowerShot,
    RapidVolley
}

public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }

    [SerializeField] private SpeedBuffController speedBuffController;
    [SerializeField] private WarriorBuffController warriorBuffController;
    [SerializeField] private WarriorDownStrike2D warriorDownStrike;
    [SerializeField] private PlayerAttack2D playerAttack;
    [SerializeField] private KeySettingUIController keySettingUIController;

    private readonly Dictionary<KeyCode, KeySettingSkillType> keyBindings = new();

    private void Awake()
    {
        Instance = this;

        if (speedBuffController == null)
            speedBuffController = GetComponent<SpeedBuffController>();

        if (warriorBuffController == null)
        {
            // [Codex Warrior Buff] Anger is warrior-only, so it is routed through WarriorBuffController.
            warriorBuffController = GetComponent<WarriorBuffController>();
        }
        if (warriorBuffController == null)
            warriorBuffController = FindFirstObjectByType<WarriorBuffController>();

        if (warriorDownStrike == null)
        {
            // [Codex Warrior Skill] DownStrike is warrior-only and uses its own warrior skill component.
            warriorDownStrike = GetComponent<WarriorDownStrike2D>();
        }
        if (warriorDownStrike == null)
            warriorDownStrike = FindFirstObjectByType<WarriorDownStrike2D>();

        if (keySettingUIController == null)
            keySettingUIController = FindFirstObjectByType<KeySettingUIController>();

        if (playerAttack == null)
        {
            // [Codex Skill Mapping] PowerShot/RapidVolley still use the existing archer attack script.
            playerAttack = FindFirstObjectByType<PlayerAttack2D>();
        }
    }

    private void Update()
    {
        if (keySettingUIController != null && keySettingUIController.IsOpen)
            return;

        foreach (KeyValuePair<KeyCode, KeySettingSkillType> binding in keyBindings)
        {
            if (binding.Value == KeySettingSkillType.PowerShot)
            {
                if (Input.GetKeyDown(binding.Key))
                    playerAttack?.BeginPowerShotCharge();

                if (Input.GetKeyUp(binding.Key))
                    playerAttack?.ReleasePowerShot();

                continue;
            }

            if (Input.GetKeyDown(binding.Key))
                ExecuteSkill(binding.Value);
        }
    }

    public void Assign(string keyName, KeySettingSkillType skillType)
    {
        if (!TryConvertKeyCode(keyName, out KeyCode keyCode))
        {
            Debug.LogWarning($"[KeySetting] Unsupported key name: {keyName}");
            return;
        }

        keyBindings[keyCode] = skillType;
        Debug.Log($"[KeySetting] {keyName} mapped to {skillType}.");
    }

    public void Unassign(string keyName)
    {
        if (!TryConvertKeyCode(keyName, out KeyCode keyCode))
            return;

        keyBindings.Remove(keyCode);
    }

    private void ExecuteSkill(KeySettingSkillType skillType)
    {
        if (skillType == KeySettingSkillType.RapidVolley)
        {
            if (playerAttack != null)
                playerAttack.UseRapidVolley();
            else
                Debug.LogWarning("[KeySetting] PlayerAttack2D is not connected.");

            return;
        }

        if (skillType == KeySettingSkillType.PowerShot)
            return;

        if (skillType == KeySettingSkillType.AngerBuff)
        {
            if (warriorBuffController != null)
                warriorBuffController.UseAngerBuff();
            else
                Debug.LogWarning("[KeySetting] WarriorBuffController is not connected.");

            return;
        }

        if (skillType == KeySettingSkillType.DownStrike)
        {
            if (warriorDownStrike != null)
                warriorDownStrike.UseDownStrike();
            else
                Debug.LogWarning("[KeySetting] WarriorDownStrike2D is not connected.");

            return;
        }

        if (speedBuffController == null)
        {
            Debug.LogWarning("[KeySetting] SpeedBuffController is not connected.");
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
        }
    }

    private bool TryConvertKeyCode(string keyName, out KeyCode keyCode)
    {
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
            return System.Enum.TryParse("Alpha" + keyName, out keyCode);

        return System.Enum.TryParse(keyName, true, out keyCode);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
