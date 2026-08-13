using Unity.Netcode;
using UnityEngine;

public class NetworkStartUI : MonoBehaviour
{
    private void OnGUI()
    {
        if (NetworkManager.Singleton == null)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 200, 200));

        if (!NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host 시작"))
            {
                NetworkManager.Singleton.StartHost();
            }

            if (GUILayout.Button("Client 시작"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }

        GUILayout.EndArea();
    }
}