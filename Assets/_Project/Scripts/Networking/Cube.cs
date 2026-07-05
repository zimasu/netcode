using Unity.Netcode;
using UnityEngine;

public class Cube : NetworkBehaviour
{
    private NetworkVariable<Vector3> _position = new NetworkVariable<Vector3>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        _position.OnValueChanged += HandlePositionChanged;
        transform.position = _position.Value;
    }

    public override void OnNetworkDespawn()
    {
        _position.OnValueChanged -= HandlePositionChanged;
    }

    private void HandlePositionChanged(Vector3 previous, Vector3 current)
    {
        transform.position = current;
    }

    [Rpc(SendTo.Server)]
    public void RequestMoveRpc(Vector3 newPosition)
    {
        _position.Value = newPosition;
    }
}