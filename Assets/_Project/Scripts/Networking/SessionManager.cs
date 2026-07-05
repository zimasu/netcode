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
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += RefreshCount;
            NetworkManager.Singleton.OnClientDisconnectCallback += RefreshCount;
            RefreshCount(0);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= RefreshCount;
            NetworkManager.Singleton.OnClientDisconnectCallback -= RefreshCount;
        }
    }

    private void RefreshCount(ulong clientId)
    {
        ConnectedClientCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
    }
}