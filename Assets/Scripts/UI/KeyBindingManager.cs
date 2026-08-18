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
    private const string SavedBindingPrefix = "AppleStory.KeyBinding.";
    public const string HostArcherProfile = "HostArcher";
    public const string ClientWarriorProfile = "ClientWarrior";

    private static string bindingProfile = HostArcherProfile;

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

        ResolveProfileFromNetwork();
        LoadSavedBindings();
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
        SaveBinding(keyCode, skillType);
        Debug.Log($"[키 설정] {keyName} 실행 스킬이 {skillType}(으)로 변경됐습니다.");
    }

    public void Unassign(string keyName)
    {
        // [키 중복 배치 수정] 스킬이 다른 키로 이동하면 이전 키 바인딩을 제거합니다.
        if (!TryConvertKeyCode(keyName, out KeyCode keyCode))
            return;

        keyBindings.Remove(keyCode);
        RemoveSavedBinding(keyCode);
    }

    public static void SaveBinding(string keyName, KeySettingSkillType skillType)
    {
        if (!TryConvertKeyCodeStatic(keyName, out KeyCode keyCode))
            return;

        SaveBinding(keyCode, skillType);
    }

    public static void SetBindingProfile(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
            return;

        // [Codex KeyBinding Profile] Host 아처와 Client 워리어 키 설정이 같은 PC PlayerPrefs에서 섞이지 않게 분리합니다.
        bindingProfile = profile;
        Instance?.LoadSavedBindings();
    }

    public static void RemoveSavedBinding(string keyName)
    {
        if (!TryConvertKeyCodeStatic(keyName, out KeyCode keyCode))
            return;

        RemoveSavedBinding(keyCode);
    }

    public static void SaveCurrentBindings()
    {
        if (Instance == null)
        {
            PlayerPrefs.Save();
            return;
        }

        foreach (KeyValuePair<KeyCode, KeySettingSkillType> binding in Instance.keyBindings)
            SaveBinding(binding.Key, binding.Value);

        PlayerPrefs.Save();
    }

    public static bool TryGetSavedBinding(string keyName, out KeySettingSkillType skillType)
    {
        skillType = KeySettingSkillType.None;

        if (!TryConvertKeyCodeStatic(keyName, out KeyCode keyCode))
            return false;

        Dictionary<KeyCode, KeySettingSkillType> savedBindings = LoadSavedBindingDictionary();
        return savedBindings.TryGetValue(keyCode, out skillType) &&
            skillType != KeySettingSkillType.None;
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
        return TryConvertKeyCodeStatic(keyName, out keyCode);
    }

    private static bool TryConvertKeyCodeStatic(string keyName, out KeyCode keyCode)
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

    private void LoadSavedBindings()
    {
        keyBindings.Clear();

        int count = PlayerPrefs.GetInt(GetSavedBindingCountKey(), 0);
        for (int i = 0; i < count; i++)
        {
            string keyName = PlayerPrefs.GetString(GetSavedBindingKey(i), "");
            string skillName = PlayerPrefs.GetString(GetSavedBindingSkillKey(i), "");

            if (!System.Enum.TryParse(keyName, out KeyCode keyCode) ||
                !System.Enum.TryParse(skillName, out KeySettingSkillType skillType) ||
                skillType == KeySettingSkillType.None)
            {
                continue;
            }

            keyBindings[keyCode] = skillType;
        }
    }

    private static void SaveBinding(KeyCode keyCode, KeySettingSkillType skillType)
    {
        Dictionary<KeyCode, KeySettingSkillType> savedBindings = LoadSavedBindingDictionary();
        savedBindings[keyCode] = skillType;
        SaveBindingDictionary(savedBindings);
    }

    private static void RemoveSavedBinding(KeyCode keyCode)
    {
        Dictionary<KeyCode, KeySettingSkillType> savedBindings = LoadSavedBindingDictionary();
        savedBindings.Remove(keyCode);
        SaveBindingDictionary(savedBindings);
    }

    private static Dictionary<KeyCode, KeySettingSkillType> LoadSavedBindingDictionary()
    {
        Dictionary<KeyCode, KeySettingSkillType> savedBindings = new();
        int count = PlayerPrefs.GetInt(GetSavedBindingCountKey(), 0);
        for (int i = 0; i < count; i++)
        {
            string keyName = PlayerPrefs.GetString(GetSavedBindingKey(i), "");
            string skillName = PlayerPrefs.GetString(GetSavedBindingSkillKey(i), "");

            if (System.Enum.TryParse(keyName, out KeyCode keyCode) &&
                System.Enum.TryParse(skillName, out KeySettingSkillType skillType) &&
                skillType != KeySettingSkillType.None)
            {
                savedBindings[keyCode] = skillType;
            }
        }

        return savedBindings;
    }

    private static void SaveBindingDictionary(Dictionary<KeyCode, KeySettingSkillType> bindings)
    {
        int previousCount = PlayerPrefs.GetInt(GetSavedBindingCountKey(), 0);
        for (int i = 0; i < previousCount; i++)
        {
            PlayerPrefs.DeleteKey(GetSavedBindingKey(i));
            PlayerPrefs.DeleteKey(GetSavedBindingSkillKey(i));
        }

        int index = 0;
        foreach (KeyValuePair<KeyCode, KeySettingSkillType> binding in bindings)
        {
            PlayerPrefs.SetString(GetSavedBindingKey(index), binding.Key.ToString());
            PlayerPrefs.SetString(GetSavedBindingSkillKey(index), binding.Value.ToString());
            index++;
        }

        PlayerPrefs.SetInt(GetSavedBindingCountKey(), index);
        PlayerPrefs.Save();
    }

    private static string GetProfilePrefix()
    {
        return SavedBindingPrefix + bindingProfile + ".";
    }

    private static string GetSavedBindingCountKey()
    {
        return GetProfilePrefix() + "Count";
    }

    private static string GetSavedBindingKey(int index)
    {
        return $"{GetProfilePrefix()}Key{index}";
    }

    private static string GetSavedBindingSkillKey(int index)
    {
        return $"{GetProfilePrefix()}Skill{index}";
    }

    private static void ResolveProfileFromNetwork()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
            return;

        bindingProfile = manager.IsClient && !manager.IsServer
            ? ClientWarriorProfile
            : HostArcherProfile;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
