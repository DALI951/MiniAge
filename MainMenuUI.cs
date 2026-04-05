using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// MainMenuUI v10.
/// IP address shown immediately when Host Game panel opens.
/// No need to press Start Hosting to see the IP.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    public static MainMenuUI Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject joinPanel;

    [Header("Host Panel")]
    [SerializeField] private TMP_Dropdown maxPlayersDropdown;
    [SerializeField] private TMP_Text       hostStatusText;
    [SerializeField] private TMP_Text       ipDisplayText;   // NEW — shows IP immediately
    [SerializeField] private Button         startHostButton;

    [Header("Join Panel")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private TMP_Text       joinStatusText;
    [SerializeField] private Button         joinButton;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => ShowMain();

    // ─── Panel navigation ─────────────────────────────────────────────────

    public void ShowMain()
    {
        mainPanel?.SetActive(true);
        hostPanel?.SetActive(false);
        joinPanel?.SetActive(false);
    }

    public void ShowHostPanel()
    {
        mainPanel?.SetActive(false);
        hostPanel?.SetActive(true);
        joinPanel?.SetActive(false);

        // Show IP immediately as soon as panel opens
        string ip = GetLocalIP();
        if (ipDisplayText != null)
            ipDisplayText.text = $"Your IP:  {ip}";

        SetHostStatus("Configure and press Start Hosting.");
    }

    public void ShowJoinPanel()
    {
        mainPanel?.SetActive(false);
        hostPanel?.SetActive(false);
        joinPanel?.SetActive(true);
    }

    // ─── Host ─────────────────────────────────────────────────────────────

    public void OnCreateLobby()
    {
        int max = 2;
        if (maxPlayersDropdown != null)
            max = maxPlayersDropdown.value + 2;

        if (RTSNetworkManager.Instance == null)
        { SetHostStatus("NetworkManager missing!"); return; }

        RTSNetworkManager.Instance.maxConnections = max;
        RTSNetworkManager.Instance.requiredPlayers = max;
        RTSNetworkManager.Instance.StartHost();

        if (startHostButton != null) startHostButton.interactable = false;
        SetHostStatus("Creating lobby...");
    }

    /// <summary>Called by RTSNetworkManager after host confirms running.</summary>
    public void OnHostStarted(string ip)
    {
        Debug.Log("[MainMenuUI] OnHostStarted called");
        if (ipDisplayText != null)
            ipDisplayText.text = $"Your IP:  {ip}";
        SetHostStatus("✅ Server running. Share your IP above.");
        
        // Show lobby after hosting starts
        LobbyUI.Instance?.ShowLobby();
    }

    public void OnBackFromHost()
    {
        if (Mirror.NetworkServer.active)
            RTSNetworkManager.Instance?.StopHost();
        if (startHostButton != null) startHostButton.interactable = true;
        ShowMain();
    }

    // ─── Join ─────────────────────────────────────────────────────────────

    public void OnJoin()
    {
        string ip = ipAddressInput != null ? ipAddressInput.text.Trim() : "localhost";
        if (string.IsNullOrEmpty(ip)) ip = "localhost";

        if (RTSNetworkManager.Instance == null)
        { SetJoinStatus("NetworkManager missing!"); return; }

        RTSNetworkManager.Instance.networkAddress = ip;
        RTSNetworkManager.Instance.StartClient();

        SetJoinStatus($"Connecting to {ip}...");
        if (joinButton != null) joinButton.interactable = false;
        // Hide join panel — lobby will show when connection confirmed
        joinPanel?.SetActive(false);
    }

    public void OnBackFromJoin()
    {
        if (Mirror.NetworkClient.active)
            RTSNetworkManager.Instance?.StopClient();
        if (joinButton != null) joinButton.interactable = true;
        ShowMain();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private void SetHostStatus(string msg) { if (hostStatusText) hostStatusText.text = msg; }
    private void SetJoinStatus(string msg) { if (joinStatusText) joinStatusText.text = msg; }

    private string GetLocalIP()
    {
        try
        {
            foreach (var ip in System.Net.Dns.GetHostEntry(
                System.Net.Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
    public void HideAllPanels()
    {
        mainPanel?.SetActive(false);
        hostPanel?.SetActive(false);
        joinPanel?.SetActive(false);
    }
    public void OnStartSinglePlayer()
    {
        GameModeManager.Instance?.StartSinglePlayer();
    }

    public void ShowSinglePlayerPanel()
    {
        mainPanel?.SetActive(false);
        // For single player, we can directly start the game
        GameModeManager.Instance?.StartSinglePlayer();
    }

}
