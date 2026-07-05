using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LanBootstrap : MonoBehaviour
{
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_Text statusText;

    private const ushort Port = 7777;

    private void Awake()
    {
        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);
    }

    private UnityTransport Transport =>
        (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

    public void StartHost()
    {
        Transport.SetConnectionData("0.0.0.0", Port);
        NetworkManager.Singleton.StartHost();
        SetStatus($"Hosting on port {Port}");
    }

    public void StartClient()
    {
        string ip = string.IsNullOrWhiteSpace(ipInputField.text) ? "127.0.0.1" : ipInputField.text.Trim();
        Transport.SetConnectionData(ip, Port);
        NetworkManager.Singleton.StartClient();
        SetStatus($"Connecting to {ip}:{Port} ...");
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log($"[LanBootstrap] {message}");
    }
}