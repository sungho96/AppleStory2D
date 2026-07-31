using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerOwner : NetworkBehaviour
{
    [Header("Owner Only Components")]
    [SerializeField] private PlayerController2D playerController;
    [SerializeField] private PlayerAttack2D playerAttack;

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
    }
}