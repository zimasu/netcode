using System.Collections;
using System.Text.RegularExpressions;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LanBootstrap : MonoBehaviour
{
    [SerializeField] private TMP_InputField _ipInputField;
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _disconnectButton;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private float _connectionTimeoutSeconds = 10f;

    private const ushort Port = 7777;
    private static readonly Regex IpPattern = new Regex(
        @"^(\d{1,3}\.){3}\d{1,3}$", RegexOptions.Compiled);

    private SessionManager _session;
    private bool _subscribedToSession;
    private bool _isConnecting;
    private Coroutine _timeoutCoroutine;

    private void Awake()
    {
        if (_hostButton == null || _joinButton == null || _ipInputField == null ||
            _statusText == null || _disconnectButton == null)
        {
            Debug.LogError("[LanBootstrap] One or more UI references are not assigned in the Inspector!");
            enabled = false; // stop further execution cleanly
            return;
        }

        _hostButton.onClick.AddListener(StartHost);
        _joinButton.onClick.AddListener(StartClient);
        _disconnectButton.onClick.AddListener(Disconnect);

        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
    }

    private void OnDestroy()
    {
        // guard: NetworkManager may already be gone during scene unload
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        UnsubscribeFromConnectionEvents();
    }

    private UnityTransport Transport =>
        (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

    public void StartHost()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Already running a session.");
            return;
        }

        Transport.SetConnectionData("0.0.0.0", Port);
        SubscribeToConnectionEvents();

        bool started = NetworkManager.Singleton.StartHost();
        if (!started)
        {
            // StartHost returns false if NGO rejects startup (e.g. already shutting down)
            SetStatus("Failed to start host. Try again.");
            UnsubscribeFromConnectionEvents();
            return;
        }

        SetInteractable(false);
        SetStatus($"Hosting on port {Port}");
    }

    public void StartClient()
    {
        if (NetworkManager.Singleton.IsListening)
        {
            SetStatus("Already running a session.");
            return;
        }

        string rawInput = _ipInputField.text.Trim();
        string ip;

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            ip = "127.0.0.1";
            SetStatus("No IP entered — defaulting to localhost.");
        }
        else if (!IpPattern.IsMatch(rawInput))
        {
            SetStatus($"'{rawInput}' is not a valid IP (e.g. 192.168.1.5). Cancelled.");
            return;
        }
        else
        {
            ip = rawInput;
        }

        Transport.SetConnectionData(ip, Port);
        SubscribeToConnectionEvents();

        bool started = NetworkManager.Singleton.StartClient();
        if (!started)
        {
            SetStatus("Failed to start client. Try again.");
            UnsubscribeFromConnectionEvents();
            return;
        }

        _isConnecting = true;
        SetInteractable(false);
        SetStatus($"Connecting to {ip}:{Port}...");
        _timeoutCoroutine = StartCoroutine(ConnectionTimeout());
    }

    public void Disconnect()
    {
        if (!NetworkManager.Singleton.IsListening)
        {
            SetStatus("Not currently connected.");
            return;
        }

        bool wasHost = NetworkManager.Singleton.IsHost;
        CancelTimeout();
        UnsubscribeFromConnectionEvents();
        NetworkManager.Singleton.Shutdown();
        ResetState();

        SetStatus(wasHost
            ? "Stopped hosting. Session ended for all clients."
            : "Left the session.");
    }

    private void SubscribeToConnectionEvents()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnsubscribeFromConnectionEvents()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        // only react to our own connection (local client id)
        if (!NetworkManager.Singleton.IsHost &&
            clientId != NetworkManager.Singleton.LocalClientId) return;

        CancelTimeout();
        _isConnecting = false;

        if (_subscribedToSession) return;

        _session = FindFirstObjectByType<SessionManager>();
        if (_session != null)
        {
            _session.ConnectedClientCount.OnValueChanged += OnClientCountChanged;
            SetStatus($"Connected. Clients: {_session.ConnectedClientCount.Value}");
            _subscribedToSession = true;
        }
        else
        {
            // SessionManager not spawned yet — wait a frame and retry
            StartCoroutine(WaitForSessionManager());
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        // if we're the host, other clients disconnecting is normal — just refresh count
        if (NetworkManager.Singleton.IsHost)
        {
            if (_session != null)
                SetStatus($"Client {clientId} left. Clients: {_session.ConnectedClientCount.Value}");
            return;
        }

        // we're a client — this means we got kicked or failed to connect
        CancelTimeout();

        string reason = NetworkManager.Singleton.DisconnectReason;
        string msg = string.IsNullOrEmpty(reason)
            ? "Disconnected from host."
            : $"Disconnected: {reason}";

        SetStatus(msg);
        UnsubscribeFromConnectionEvents();
        ResetState();
    }

    private IEnumerator WaitForSessionManager()
    {
        yield return null; // wait one frame for NGO to spawn network objects
        _session = FindFirstObjectByType<SessionManager>();
        if (_session != null)
        {
            _session.ConnectedClientCount.OnValueChanged += OnClientCountChanged;
            SetStatus($"Connected. Clients: {_session.ConnectedClientCount.Value}");
            _subscribedToSession = true;
        }
        else
        {
            Debug.LogWarning("[LanBootstrap] SessionManager not found after connection. Is it in the scene?");
            SetStatus("Connected but session data unavailable.");
        }
    }

    private IEnumerator ConnectionTimeout()
    {
        yield return new WaitForSeconds(_connectionTimeoutSeconds);

        if (_isConnecting)
        {
            SetStatus($"Connection timed out after {_connectionTimeoutSeconds}s. Host may be unreachable.");
            UnsubscribeFromConnectionEvents();
            NetworkManager.Singleton.Shutdown();
            ResetState();
        }
    }

    private void OnClientCountChanged(int oldValue, int newValue)
        => SetStatus($"Connected clients: {newValue}");

    private void CancelTimeout()
    {
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }
    }

    private void ResetState()
    {
        _isConnecting = false;
        _subscribedToSession = false;
        _session = null;
        SetInteractable(true);
    }

    private void SetInteractable(bool value)
    {
        _hostButton.interactable = value;
        _joinButton.interactable = value;
    }

    private void SetStatus(string message)
    {
        if (_statusText != null) _statusText.text = message;
        Debug.Log($"[LanBootstrap] {message}");
    }

    private void OnTransportFailure()
    {
        CancelTimeout();
        UnsubscribeFromConnectionEvents();
        SetStatus($"Transport failed. Port {Port} may already be in use.");
        ResetState();
    }
}