using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class Cube : NetworkBehaviour
{
    [SerializeField] private float _planeHalfExtent = 4.5f;

    private NetworkVariable<Vector3> _position = new NetworkVariable<Vector3>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // Seed the authoritative value from wherever the cube currently is --
        // whether that's its original editor placement, or wherever it got
        // dragged during local-only play before hosting started. Either way,
        // the host's current position becomes the new shared truth.
        if (IsServer)
        {
            _position.Value = transform.position;
        }

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
        // Server validates and clamps regardless of what the client asked
        // for -- a client can request anything, only the server's version
        // of the truth actually counts.
        _position.Value = ClampToPlane(newPosition);
    }

    private Vector3 ClampToPlane(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, -_planeHalfExtent, _planeHalfExtent);
        position.z = Mathf.Clamp(position.z, -_planeHalfExtent, _planeHalfExtent);
        return position;
    }

    // New Input System replacement for the legacy OnMouseDown callback.
    // Works in two modes:
    //  - Before the NetworkManager has started (IsSpawned == false):
    //    moves the cube locally only, no networking involved at all.
    //  - After spawning (host or client): sends a networked move request,
    //    same as before.
    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider.gameObject != gameObject)
        {
            return;
        }

        Vector3 randomOffset = new Vector3(
            Random.Range(-2f, 2f),
            0f,
            Random.Range(-2f, 2f));

        Vector3 targetPosition = ClampToPlane(transform.position + randomOffset);

        if (IsSpawned)
        {
            RequestMoveRpc(targetPosition);
        }
        else
        {
            transform.position = targetPosition;
        }
    }
}