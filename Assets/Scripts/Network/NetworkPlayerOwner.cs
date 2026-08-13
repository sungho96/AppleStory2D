using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerOwner : NetworkBehaviour
{
    [Header("Owner Only Components")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerAttack2D playerAttack;
    [SerializeField] private WarriorAttack2D warriorAttack;

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
    }
}
