using Unity.Netcode;
using UnityEngine;

public class SessionManager : NetworkBehaviour
{
    public NetworkVariable<int> ConnectedClientCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[SessionManager] NetworkManager missing on spawn.");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += RefreshCount;
        NetworkManager.Singleton.OnClientDisconnectCallback += RefreshCount;
        RefreshCount(0);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= RefreshCount;
            NetworkManager.Singleton.OnClientDisconnectCallback -= RefreshCount;
        }
    }

    private void RefreshCount(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !IsServer) return;
        ConnectedClientCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
    }
}