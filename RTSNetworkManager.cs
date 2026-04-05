using System.Collections;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;

/// <summary>
/// RTSNetworkManager v5.
/// Requires Mirror + KCP Transport component on the same GameObject.
/// After importing Mirror, add Component → KCP Transport to this GameObject.
/// </summary>
public class RTSNetworkManager : NetworkManager
{
    public static RTSNetworkManager Instance => singleton as RTSNetworkManager;

    [Header("RTS Settings")]
    [SerializeField] private string gameSceneName   = "GameScene";
    [SerializeField] public  int    requiredPlayers = 2;
    public override void Awake()
    {
        base.Awake();
        Debug.Log($"[NetworkManager] Awake in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} on object: {gameObject.name}");
    }

    public override void OnStartHost()
    {
        base.OnStartHost();
        StartCoroutine(ShowLobbyAfterDelay());
    }

    private IEnumerator ShowLobbyAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        string ip = GetLocalIP();
        MainMenuUI.Instance?.OnHostStarted(ip);
        LobbyUI.Instance?.ShowLobby();
    }

    private IEnumerator ShowIPAfterDelay()
    {
        // Wait a few frames so Mirror finishes initializing
        yield return null;
        yield return null;
        string ip = GetLocalIP();
        Debug.Log($"[Network] Hosting on IP: {ip}");
        // Retry for up to 2 seconds in case MainMenuUI isn't ready
        for (int i = 0; i < 10; i++)
        {
            if (MainMenuUI.Instance != null)
            { MainMenuUI.Instance.OnHostStarted(ip); yield break; }
            yield return new WaitForSeconds(0.2f);
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log("[Network] Connecting...");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        // Only show lobby for joining clients, not the host
        // (host lobby is shown via ShowLobbyAfterDelay)
        if (!NetworkServer.active)
            LobbyUI.Instance?.ShowLobby();
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        
        // Only go back to menu if we didn't successfully enter the game
        if (!Mirror.NetworkClient.isConnected)
            SceneManager.LoadScene(offlineScene);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        if (conn.identity.TryGetComponent(out LobbyPlayer lp))
        {
            lp.ServerSetup(numPlayers - 1);
            LobbyUI.Instance?.RefreshPlayerList();
        }
    }
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnServerDisconnect(conn);
        LobbyUI.Instance?.RefreshPlayerList();
    }

    public string GetLocalIP()
    {
        try
        {
            foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch { }
        return "127.0.0.1";
    }
    [Server]
    public void KickPlayer(LobbyPlayer player)
    {
        if (player == null) return;
        player.connectionToClient.Disconnect();
    }

    [Server]
    public void CheckAllReady()
    {
        LobbyUI.Instance?.RefreshPlayerList();

        // Count ready players
        int readyCount = 0;
        int totalCount = 0;
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) continue;
            if (!conn.identity.TryGetComponent(out LobbyPlayer lp)) continue;
            totalCount++;
            if (lp.isReady) readyCount++;
        }

        // Auto-start if everyone is ready and minimum 2 players
        if (totalCount >= requiredPlayers && readyCount == totalCount)
            StartMatch();
    }


    private void OnRpcRefreshLobby()
    {
        LobbyUI.Instance?.RefreshPlayerList();
    }

    [Server]
    public void StartMatch()
    {
        ServerChangeScene(gameSceneName);
    }
}
