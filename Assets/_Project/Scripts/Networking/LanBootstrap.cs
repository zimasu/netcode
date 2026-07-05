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

    private const ushort Port = 7777;
    private static readonly Regex IpPattern = new Regex(
        @"^(\d{1,3}\.){3}\d{1,3}$", RegexOptions.Compiled);

    private SessionManager _session;
    private bool _subscribedToSession;

    private void Awake()
    {
        if (_hostButton == null || _joinButton == null || _ipInputField == null ||
            _statusText == null || _disconnectButton == null)
        {
            Debug.LogError("[LanBootstrap] One or more UI references are not assigned in the Inspector!");
            return;
        }

        _hostButton.onClick.AddListener(StartHost);
        _joinButton.onClick.AddListener(StartClient);
        _disconnectButton.onClick.AddListener(Disconnect);

        NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
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
        NetworkManager.Singleton.StartHost();
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
            SetStatus("No IP entered. Defaulting to localhost (127.0.0.1).");
        }
        else if (!IpPattern.IsMatch(rawInput))
        {
            SetStatus($"'{rawInput}' is not a valid IP address (example: 192.168.1.5). Connection cancelled.");
            return;
        }
        else
        {
            ip = rawInput;
        }

        Transport.SetConnectionData(ip, Port);
        SubscribeToConnectionEvents();
        NetworkManager.Singleton.StartClient();
        SetInteractable(false);
        SetStatus($"Connecting to {ip}:{Port} ...");
    }

    public void Disconnect()
    {
        if (!NetworkManager.Singleton.IsListening)
        {
            SetStatus("Not currently connected.");
            return;
        }

        bool wasHost = NetworkManager.Singleton.IsHost;

        UnsubscribeFromConnectionEvents();
        NetworkManager.Singleton.Shutdown();
        SetInteractable(true);
        _subscribedToSession = false;

        SetStatus(wasHost
            ? "Stopped hosting. Session ended for all clients."
            : "Left the session.");
    }

    /// <summary>
    /// Wires up connection/disconnection callbacks for the session about to start.
    /// Always paired with <see cref="UnsubscribeFromConnectionEvents"/> in <see cref="Disconnect"/>
    /// so callbacks never stack across repeated host/join cycles.
    /// </summary>
    private void SubscribeToConnectionEvents()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnsubscribeFromConnectionEvents()
    {
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (_subscribedToSession) return;

        _session = FindFirstObjectByType<SessionManager>();
        if (_session != null)
        {
            _session.ConnectedClientCount.OnValueChanged += (oldValue, newValue) =>
                SetStatus($"Connected clients: {newValue}");
            SetStatus($"Connected clients: {_session.ConnectedClientCount.Value}");
            _subscribedToSession = true;
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        SetStatus("Failed to connect. Check the IP address and make sure the host is running.");
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
        SetStatus($"Could not start on port {Port}. It may already be in use by another process. Close other instances and try again.");
        SetInteractable(true);
    }
}