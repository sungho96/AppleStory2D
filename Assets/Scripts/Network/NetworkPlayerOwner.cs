using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerOwner : NetworkBehaviour
{
    [Header("Character")]
    [SerializeField] private PlayerCharacterType characterType = PlayerCharacterType.None;

    [Header("Owner Only Components")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerAttack2D playerAttack;
    [SerializeField] private WarriorAttack2D warriorAttack;

    [Header("Local HUD")]
    [SerializeField] private PlayerStats playerStats;

    public override void OnNetworkSpawn()
    {
        bool isLocalOwner = IsOwner;

        if (playerController != null)
        {
            playerController.enabled = isLocalOwner;
        }

        if (playerAttack != null)
        {
            playerAttack.enabled = isLocalOwner;
        }

        // Codex: Warrior uses a separate attack script, so remote warrior input must be disabled too.
        if (warriorAttack != null)
        {
            warriorAttack.enabled = isLocalOwner;
        }

        if (isLocalOwner)
        {
            BindLocalHud();
        }
    }

    private void Awake()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
    }

    private void BindLocalHud()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogWarning($"[NetworkPlayerOwner] 로컬 HUD 연결 실패: PlayerStats 없음. name={name}");
            return;
        }

        HUDStatusUI[] huds = FindObjectsByType<HUDStatusUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < huds.Length; i++)
        {
            if (huds[i] == null)
                continue;

            bool isCommonHud = huds[i].HudCharacterType == PlayerCharacterType.None;
            bool isMatchingCharacterHud = huds[i].HudCharacterType == characterType;
            bool shouldUseHud = isCommonHud || isMatchingCharacterHud;

            // [Codex Local HP HUD] 로컬 소유 캐릭터 타입과 맞는 HUD만 켜고, 다른 캐릭터 HUD는 끕니다.
            huds[i].gameObject.SetActive(shouldUseHud);

            if (shouldUseHud)
                huds[i].Bind(playerStats);
        }
    }
}
