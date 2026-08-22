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

public enum KeySettingSkillCategory
{
    None,
    Buff,
    Active
}

public class KeyBindingManager : MonoBehaviour
{
    public static KeyBindingManager Instance { get; private set; }


    // =========================================================
    // Profile
    // =========================================================

    public const string HostArcherProfile =
        "HostArcher";

    public const string ClientWarriorProfile =
        "ClientWarrior";


    private static string bindingProfile =
        HostArcherProfile;


    // =========================================================
    // ★ 이번 Play 동안만 유지되는 키 설정
    // =========================================================

    /*
     * PlayerPrefs를 더 이상 사용하지 않습니다.
     *
     * static Dictionary이므로
     *
     * GameEntry
     *     ↓
     * GoblinBoss_Network
     *
     * 씬이 바뀌어도 유지됩니다.
     *
     * 하지만 Play를 종료하면 자동으로 사라집니다.
     */

    private static readonly
        Dictionary<KeyCode, KeySettingSkillType>
        hostArcherBindings =
            new Dictionary<KeyCode, KeySettingSkillType>();


    private static readonly
        Dictionary<KeyCode, KeySettingSkillType>
        clientWarriorBindings =
            new Dictionary<KeyCode, KeySettingSkillType>();


    // 현재 씬의 KeyBindingManager가 사용하는 복사본
    private readonly
        Dictionary<KeyCode, KeySettingSkillType>
        keyBindings =
            new Dictionary<KeyCode, KeySettingSkillType>();


    // =========================================================
    // References
    // =========================================================

    [Header("Refs")]

    [SerializeField]
    private SpeedBuffController speedBuffController;


    [SerializeField]
    private PlayerAttack2D playerAttack;


    [SerializeField]
    private WarriorDownStrike2D warriorDownStrike;


    [SerializeField]
    private WarriorShieldBlock2D warriorShieldBlock;


    [SerializeField]
    private KeySettingUIController keySettingUIController;


    // =========================================================
    // ★ 새로운 Play가 시작될 때 static 메모리 초기화
    // =========================================================

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlaySession()
    {
        /*
         * Reload Domain이 꺼져 있어도
         * 새 Play 시작 시 실행됩니다.
         */

        hostArcherBindings.Clear();
        clientWarriorBindings.Clear();

        bindingProfile =
            HostArcherProfile;

        Instance =
            null;


        Debug.Log(
            "[키 설정] 새 Play 세션 시작 - " +
            "Host/Client 키매핑 메모리 초기화");
    }


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        Instance =
            this;


        if (speedBuffController == null)
        {
            speedBuffController =
                GetComponent<SpeedBuffController>();
        }


        if (keySettingUIController == null)
        {
            keySettingUIController =
                FindFirstObjectByType<KeySettingUIController>();
        }


        RefreshProfileAndBindings();


        Debug.Log(
            $"[키 설정] KeyBindingManager Awake - " +
            $"Profile={bindingProfile}, " +
            $"Count={keyBindings.Count}");
    }


    private void Update()
    {
        // 키설정 UI가 열려 있을 때 실제 스킬 사용 방지
        if (keySettingUIController != null &&
            keySettingUIController.IsOpen)
        {
            return;
        }


        foreach (
            KeyValuePair<KeyCode, KeySettingSkillType> binding
            in keyBindings)
        {
            // =================================================
            // PowerShot은 누름 / 뗌을 따로 처리
            // =================================================

            if (binding.Value ==
                KeySettingSkillType.PowerShot)
            {
                EnsurePlayerAttack();


                if (Input.GetKeyDown(
                        binding.Key))
                {
                    playerAttack?
                        .BeginPowerShotCharge();
                }


                if (Input.GetKeyUp(
                        binding.Key))
                {
                    playerAttack?
                        .ReleasePowerShot();
                }


                continue;
            }


            // =================================================
            // 일반 스킬
            // =================================================

            if (Input.GetKeyDown(
                    binding.Key))
            {
                ExecuteSkill(
                    binding.Value);
            }
        }
    }


    // =========================================================
    // 현재 프로필 Dictionary
    // =========================================================

    private static
        Dictionary<KeyCode, KeySettingSkillType>
        GetCurrentProfileBindings()
    {
        if (bindingProfile ==
            ClientWarriorProfile)
        {
            return clientWarriorBindings;
        }


        return hostArcherBindings;
    }


    // =========================================================
    // Profile Refresh
    // =========================================================

    public void RefreshProfileAndBindings()
    {
        ResolveProfileFromNetwork();


        keyBindings.Clear();


        Dictionary<KeyCode, KeySettingSkillType>
            source =
                GetCurrentProfileBindings();


        foreach (
            KeyValuePair<KeyCode, KeySettingSkillType> pair
            in source)
        {
            keyBindings[pair.Key] =
                pair.Value;
        }


        Debug.Log(
            $"[키 설정] 프로필 재로드: " +
            $"{bindingProfile}, " +
            $"Count={keyBindings.Count}");
    }


    // =========================================================
    // 프로필 명시 변경
    // =========================================================

    public static void SetBindingProfile(
        string profile)
    {
        if (string.IsNullOrWhiteSpace(
                profile))
        {
            return;
        }


        bindingProfile =
            profile;


        Debug.Log(
            $"[키 설정] 프로필 변경: " +
            $"{bindingProfile}");


        if (Instance != null)
        {
            Instance.LoadCurrentProfileBindings();
        }
    }


    public static void SetBindingProfileForCharacter(
        PlayerCharacterType characterType)
    {
        switch (characterType)
        {
            case PlayerCharacterType.Warrior:
                SetBindingProfile(ClientWarriorProfile);
                break;

            case PlayerCharacterType.Archer:
                SetBindingProfile(HostArcherProfile);
                break;
        }
    }


    private void LoadCurrentProfileBindings()
    {
        keyBindings.Clear();


        Dictionary<KeyCode, KeySettingSkillType>
            source =
                GetCurrentProfileBindings();


        foreach (
            KeyValuePair<KeyCode, KeySettingSkillType> pair
            in source)
        {
            keyBindings[pair.Key] =
                pair.Value;
        }


        Debug.Log(
            $"[키 설정] 현재 세션 바인딩 로드: " +
            $"Profile={bindingProfile}, " +
            $"Count={keyBindings.Count}");
    }


    // =========================================================
    // Assign
    // =========================================================

    public void Assign(
        string keyName,
        KeySettingSkillType skillType)
    {
        if (skillType ==
            KeySettingSkillType.None)
        {
            return;
        }


        if (!TryConvertKeyCodeStatic(
                keyName,
                out KeyCode keyCode))
        {
            Debug.LogWarning(
                $"[키 설정] 지원하지 않는 키: " +
                $"{keyName}");

            return;
        }


        Dictionary<KeyCode, KeySettingSkillType>
            profileBindings =
                GetCurrentProfileBindings();


        // =====================================================
        // [Codex Skill Select Category Limit]
        // 같은 카테고리는 1개만 남기고 기존 선택을 자동 해제합니다.
        // =====================================================

        List<KeyCode> duplicateKeys =
            new List<KeyCode>();


        foreach (
            KeyValuePair<KeyCode, KeySettingSkillType> pair
            in profileBindings)
        {
            if ((pair.Value ==
                    skillType ||
                 IsSameSkillCategory(pair.Value, skillType)) &&
                pair.Key !=
                    keyCode)
            {
                duplicateKeys.Add(
                    pair.Key);
            }
        }


        for (int i = 0;
             i < duplicateKeys.Count;
             i++)
        {
            profileBindings.Remove(
                duplicateKeys[i]);


            keyBindings.Remove(
                duplicateKeys[i]);
        }


        // =====================================================
        // 저장
        // =====================================================

        profileBindings[keyCode] =
            skillType;


        keyBindings[keyCode] =
            skillType;


        Debug.Log(
            $"[키 설정] 세션 저장: " +
            $"Profile={bindingProfile}, " +
            $"{keyName} = {skillType}, " +
            $"Count={profileBindings.Count}");
    }


    // =========================================================
    // Unassign
    // =========================================================

    public void Unassign(
        string keyName)
    {
        if (!TryConvertKeyCodeStatic(
                keyName,
                out KeyCode keyCode))
        {
            return;
        }


        Dictionary<KeyCode, KeySettingSkillType>
            profileBindings =
                GetCurrentProfileBindings();


        profileBindings.Remove(
            keyCode);


        keyBindings.Remove(
            keyCode);


        Debug.Log(
            $"[키 설정] 세션 제거: " +
            $"Profile={bindingProfile}, " +
            $"Key={keyName}");
    }


    // =========================================================
    // KeyDropSlot fallback용 static Save
    // =========================================================

    public static void SaveBinding(
        string keyName,
        KeySettingSkillType skillType)
    {
        if (skillType ==
            KeySettingSkillType.None)
        {
            return;
        }


        if (!TryConvertKeyCodeStatic(
                keyName,
                out KeyCode keyCode))
        {
            return;
        }


        Dictionary<KeyCode, KeySettingSkillType>
            profileBindings =
                GetCurrentProfileBindings();


        // [Codex Skill Select Category Limit] 같은 스킬 또는 같은 카테고리의 기존 키 제거
        List<KeyCode> duplicateKeys =
            new List<KeyCode>();


        foreach (
            KeyValuePair<KeyCode, KeySettingSkillType> pair
            in profileBindings)
        {
            if ((pair.Value ==
                    skillType ||
                 IsSameSkillCategory(pair.Value, skillType)) &&
                pair.Key !=
                    keyCode)
            {
                duplicateKeys.Add(
                    pair.Key);
            }
        }


        for (int i = 0;
             i < duplicateKeys.Count;
             i++)
        {
            profileBindings.Remove(
                duplicateKeys[i]);
        }


        profileBindings[keyCode] =
            skillType;


        if (Instance != null)
        {
            Instance.keyBindings.Clear();


            foreach (
                KeyValuePair<KeyCode, KeySettingSkillType> pair
                in profileBindings)
            {
                Instance.keyBindings[pair.Key] =
                    pair.Value;
            }
        }


        Debug.Log(
            $"[키 설정] static 세션 저장: " +
            $"Profile={bindingProfile}, " +
            $"{keyName}={skillType}");
    }


    // =========================================================
    // Saved Binding Remove
    // =========================================================

    public static void RemoveSavedBinding(
        string keyName)
    {
        if (!TryConvertKeyCodeStatic(
                keyName,
                out KeyCode keyCode))
        {
            return;
        }


        Dictionary<KeyCode, KeySettingSkillType>
            profileBindings =
                GetCurrentProfileBindings();


        profileBindings.Remove(
            keyCode);


        Instance?.keyBindings.Remove(
            keyCode);
    }


    // =========================================================
    // ★ KeyDropSlot에서 저장값 읽기
    // =========================================================

    public static bool TryGetSavedBinding(
        string keyName,
        out KeySettingSkillType skillType)
    {
        skillType =
            KeySettingSkillType.None;


        if (!TryConvertKeyCodeStatic(
                keyName,
                out KeyCode keyCode))
        {
            return false;
        }


        Dictionary<KeyCode, KeySettingSkillType>
            profileBindings =
                GetCurrentProfileBindings();


        if (!profileBindings.TryGetValue(
                keyCode,
                out skillType))
        {
            return false;
        }


        return skillType !=
               KeySettingSkillType.None;
    }


    // =========================================================
    // [Codex Skill Select Required Count]
    // Skill Select는 Buff 1개, Active 1개만 유효한 완료 상태입니다.
    // =========================================================

    public static bool HasRequiredSkillSelection()
    {
        return GetSelectedBuffSkillCount() == 1 &&
               GetSelectedActiveSkillCount() == 1;
    }

    public static int GetSelectedBuffSkillCount()
    {
        return GetSelectedSkillCount(
            KeySettingSkillCategory.Buff);
    }

    public static int GetSelectedActiveSkillCount()
    {
        return GetSelectedSkillCount(
            KeySettingSkillCategory.Active);
    }

    public static KeySettingSkillCategory GetSkillCategory(
        KeySettingSkillType skillType)
    {
        switch (skillType)
        {
            case KeySettingSkillType.MoveSpeedBuff:
            case KeySettingSkillType.AttackSpeedBuff:
                return KeySettingSkillCategory.Buff;

            case KeySettingSkillType.PowerShot:
            case KeySettingSkillType.RapidVolley:
            case KeySettingSkillType.WarriorDownStrike:
            case KeySettingSkillType.WarriorShieldBlock:
                return KeySettingSkillCategory.Active;

            default:
                return KeySettingSkillCategory.None;
        }
    }

    public static bool IsSameSkillCategory(
        KeySettingSkillType left,
        KeySettingSkillType right)
    {
        KeySettingSkillCategory leftCategory =
            GetSkillCategory(left);

        return leftCategory != KeySettingSkillCategory.None &&
               leftCategory == GetSkillCategory(right);
    }

    private static int GetSelectedSkillCount(
        KeySettingSkillCategory category)
    {
        if (category == KeySettingSkillCategory.None)
        {
            return 0;
        }

        Dictionary<KeyCode, KeySettingSkillType>
            profileBindings =
                GetCurrentProfileBindings();

        int count = 0;

        foreach (
            KeyValuePair<KeyCode, KeySettingSkillType> pair
            in profileBindings)
        {
            if (GetSkillCategory(pair.Value) ==
                category)
            {
                count++;
            }
        }

        return count;
    }


    // =========================================================
    // ★ Ready 버튼에서 호출되는 기존 함수
    // =========================================================

    public static void SaveCurrentBindings()
    {
        /*
         * 이제 PlayerPrefs.Save()는 필요 없습니다.
         *
         * 이미 static Dictionary에 저장되어 있으므로
         * 이 함수는 현재 상태 확인용 역할만 합니다.
         *
         * 기존 GameEntryReadyNetworkController에서
         * 이 함수를 호출하고 있으므로 삭제하면 안 됩니다.
         */


        Dictionary<KeyCode, KeySettingSkillType>
            profileBindings =
                GetCurrentProfileBindings();


        Debug.Log(
            $"[키 설정] 현재 바인딩 세션 유지: " +
            $"Profile={bindingProfile}, " +
            $"Count={profileBindings.Count}");
    }


    // =========================================================
    // 필요할 경우 수동으로 세션 전체 초기화
    // =========================================================

    public static void ClearAllSessionBindings()
    {
        hostArcherBindings.Clear();
        clientWarriorBindings.Clear();
        // [Codex GameEntry Fresh Start] Restart 후에는 새 Play 시작과 같은 기본 프로필에서 다시 시작합니다.
        bindingProfile =
            HostArcherProfile;


        if (Instance != null)
        {
            Instance.keyBindings.Clear();
        }


        Debug.Log(
            "[키 설정] Host/Client 세션 키설정 수동 초기화");
    }


    // =========================================================
    // Network Profile
    // =========================================================

    private static void ResolveProfileFromNetwork()
    {
        PlayerCharacterType localCharacter =
            ResolveLocalCharacterType();

        // [Codex Character Skill Sync] Host/Client 역할이 아니라 실제 선택한 캐릭터 기준으로 키 프로필을 고릅니다.
        if (localCharacter !=
            PlayerCharacterType.None)
        {
            SetBindingProfileForCharacter(
                localCharacter);

            return;
        }

        if (GameEntryCharacterSelectionStore.LocalSelectedCharacter !=
            PlayerCharacterType.None)
        {
            SetBindingProfileForCharacter(
                GameEntryCharacterSelectionStore.LocalSelectedCharacter);

            return;
        }

        NetworkManager manager =
            NetworkManager.Singleton;


        if (manager == null)
        {
            return;
        }


        // =====================================================
        // Pure Client = Warrior
        // =====================================================

        if (manager.IsClient &&
            !manager.IsServer)
        {
            bindingProfile =
                ClientWarriorProfile;

            return;
        }


        // =====================================================
        // Host / Server = Archer
        // =====================================================

        if (manager.IsServer)
        {
            bindingProfile =
                HostArcherProfile;
        }
    }


    private static PlayerCharacterType ResolveLocalCharacterType()
    {
        if (GameEntryCharacterSelectionStore.LocalSelectedCharacter !=
            PlayerCharacterType.None)
        {
            return GameEntryCharacterSelectionStore.LocalSelectedCharacter;
        }

        NetworkManager manager =
            NetworkManager.Singleton;

        if (manager == null ||
            manager.LocalClient == null ||
            manager.LocalClient.PlayerObject == null)
        {
            return PlayerCharacterType.None;
        }

        NetworkObject localPlayer =
            manager.LocalClient.PlayerObject;

        // [Codex Character Skill Sync] 보스씬에서는 스폰된 내 PlayerObject의 컴포넌트로 선택 캐릭터를 다시 확인합니다.
        if (localPlayer.GetComponentInChildren<WarriorAttack2D>(true) != null ||
            localPlayer.GetComponentInChildren<WarriorDownStrike2D>(true) != null ||
            localPlayer.GetComponentInChildren<WarriorShieldBlock2D>(true) != null)
        {
            return PlayerCharacterType.Warrior;
        }

        if (localPlayer.GetComponentInChildren<PlayerAttack2D>(true) != null)
        {
            return PlayerCharacterType.Archer;
        }

        return PlayerCharacterType.None;
    }


    // =========================================================
    // Skill Execute
    // =========================================================

    private void ExecuteSkill(
        KeySettingSkillType skillType)
    {
        switch (skillType)
        {
            // =================================================
            // Rapid Volley
            // =================================================

            case KeySettingSkillType.RapidVolley:

                EnsurePlayerAttack();


                if (playerAttack != null)
                {
                    playerAttack.UseRapidVolley();
                }

                break;


            // =================================================
            // Power Shot
            // Update에서 처리
            // =================================================

            case KeySettingSkillType.PowerShot:

                break;


            // =================================================
            // Warrior DownStrike
            // =================================================

            case KeySettingSkillType.WarriorDownStrike:

                EnsureWarriorSkills();


                if (warriorDownStrike != null)
                {
                    warriorDownStrike
                        .UseDownStrike();
                }
                else
                {
                    Debug.LogWarning(
                        "[키 설정] 로컬 WarriorDownStrike2D를 " +
                        "찾지 못했습니다.");
                }

                break;


            // =================================================
            // Warrior ShieldBlock
            // =================================================

            case KeySettingSkillType.WarriorShieldBlock:

                EnsureWarriorSkills();


                if (warriorShieldBlock != null)
                {
                    warriorShieldBlock
                        .UseShieldBlock();
                }
                else
                {
                    Debug.LogWarning(
                        "[키 설정] 로컬 WarriorShieldBlock2D를 " +
                        "찾지 못했습니다.");
                }

                break;


            // =================================================
            // 이동속도 버프
            // =================================================

            case KeySettingSkillType.MoveSpeedBuff:

                EnsureSpeedBuffController();


                if (speedBuffController != null)
                {
                    speedBuffController
                        .UseSpeedBuff();
                }

                break;


            // =================================================
            // 공격속도 버프
            // =================================================

            case KeySettingSkillType.AttackSpeedBuff:

                EnsureSpeedBuffController();


                if (speedBuffController != null)
                {
                    speedBuffController
                        .UseAttackSpeedBuff();
                }

                break;
        }
    }


    // =========================================================
    // Reference Refresh
    // =========================================================

    private void EnsurePlayerAttack()
    {
        if (IsLocalPlayerComponent(playerAttack))
        {
            return;
        }


        playerAttack =
            FindLocalPlayerComponent<PlayerAttack2D>();
    }


    private void EnsureWarriorSkills()
    {
        if (!IsLocalPlayerComponent(warriorDownStrike))
        {
            warriorDownStrike =
                FindLocalPlayerComponent<
                    WarriorDownStrike2D>();
        }


        if (!IsLocalPlayerComponent(warriorShieldBlock))
        {
            warriorShieldBlock =
                FindLocalPlayerComponent<
                    WarriorShieldBlock2D>();
        }
    }


    private void EnsureSpeedBuffController()
    {
        PlayerMovement2D localMovement =
            FindLocalPlayerComponent<
                PlayerMovement2D>();

        PlayerAttack2D localAttack =
            FindLocalPlayerComponent<
                PlayerAttack2D>();

        WarriorAttack2D localWarriorAttack =
            FindLocalPlayerComponent<
                WarriorAttack2D>();

        if (IsLocalPlayerComponent(speedBuffController))
        {
            speedBuffController.BindPlayerTargets(
                localMovement,
                localAttack,
                localWarriorAttack);

            return;
        }


        speedBuffController =
            FindLocalPlayerComponent<
                SpeedBuffController>();

        if (speedBuffController != null)
        {
            speedBuffController.BindPlayerTargets(
                localMovement,
                localAttack,
                localWarriorAttack);

            return;
        }

        if (speedBuffController == null)
        {
            if (localMovement != null)
            {
                // [Codex 캐릭터 선택 대응] 워리어처럼 프리팹에 버프 컨트롤러가 없는 캐릭터도 로컬 플레이어에만 런타임 연결합니다.
                speedBuffController =
                    localMovement.gameObject
                        .AddComponent<SpeedBuffController>();

                speedBuffController.BindPlayerTargets(
                    localMovement,
                    localAttack,
                    localWarriorAttack);
            }
        }
    }


    // =========================================================
    // 로컬 플레이어 찾기
    // =========================================================

    private T FindLocalPlayerComponent<T>()
        where T : Component
    {
        T[] components =
            FindObjectsByType<T>(
                FindObjectsSortMode.None);


        // =====================================================
        // 네트워크 Spawn + Owner 우선
        // =====================================================

        for (int i = 0;
             i < components.Length;
             i++)
        {
            T component =
                components[i];


            if (component == null)
            {
                continue;
            }


            NetworkObject networkObject =
                component.GetComponent<NetworkObject>();


            if (networkObject == null)
            {
                networkObject =
                    component.GetComponentInParent<
                        NetworkObject>();
            }


            if (networkObject != null &&
                networkObject.IsSpawned &&
                networkObject.IsOwner)
            {
                return component;
            }
        }


        // =====================================================
        // 스폰 전 / 단일 테스트 씬 fallback
        // =====================================================

        for (int i = 0;
             i < components.Length;
             i++)
        {
            T component =
                components[i];

            if (component == null)
            {
                continue;
            }

            NetworkObject networkObject =
                component.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                networkObject =
                    component.GetComponentInParent<
                        NetworkObject>();
            }

            // [Codex 공속 버프 대상 수정] 네트워크 보스씬에서 소유자가 아닌 아처/워리어를 fallback으로 잡지 않습니다.
            if (networkObject == null)
            {
                return component;
            }
        }


        return null;
    }

    private bool IsLocalPlayerComponent<T>(
        T component)
        where T : Component
    {
        if (component == null)
        {
            return false;
        }

        NetworkObject networkObject =
            component.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            networkObject =
                component.GetComponentInParent<
                    NetworkObject>();
        }

        if (networkObject == null)
        {
            // [Codex 캐릭터 선택 대응] 네트워크 오브젝트가 없는 단일 테스트 씬 참조는 기존처럼 허용합니다.
            return true;
        }

        return networkObject.IsSpawned &&
               networkObject.IsOwner;
    }


    // =========================================================
    // KeyCode Convert
    // =========================================================

    private static bool TryConvertKeyCodeStatic(
        string keyName,
        out KeyCode keyCode)
    {
        keyCode =
            KeyCode.None;


        if (string.IsNullOrWhiteSpace(
                keyName))
        {
            return false;
        }


        switch (keyName)
        {
            case "`":

                keyCode =
                    KeyCode.BackQuote;

                return true;


            case "-":

                keyCode =
                    KeyCode.Minus;

                return true;


            case "=":

                keyCode =
                    KeyCode.Equals;

                return true;


            case "Ins":

                keyCode =
                    KeyCode.Insert;

                return true;


            case "Del":

                keyCode =
                    KeyCode.Delete;

                return true;


            case "PgUp":

                keyCode =
                    KeyCode.PageUp;

                return true;


            case "PgDn":

                keyCode =
                    KeyCode.PageDown;

                return true;
        }


        // =====================================================
        // 숫자키
        // "1" -> Alpha1
        // =====================================================

        if (keyName.Length == 1 &&
            char.IsDigit(keyName[0]))
        {
            return System.Enum.TryParse(
                "Alpha" + keyName,
                out keyCode);
        }


        // =====================================================
        // Q, W, E, F1 등
        // =====================================================

        return System.Enum.TryParse(
            keyName,
            true,
            out keyCode);
    }


    // =========================================================
    // Destroy
    // =========================================================

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance =
                null;
        }
    }
}
