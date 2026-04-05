using UnityEngine;
using Mirror;
using TMPro;

public class LobbyPlayer : NetworkBehaviour
{
    // Synced to all clients automatically
    [SyncVar(hook = nameof(OnNameChanged))]
    public string playerName = "Player";

    [SyncVar(hook = nameof(OnColorChanged))]
    public Color playerColor = Color.white;

    [SyncVar]
    public int playerIndex = 0;

    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady = false;
    [SyncVar(hook = nameof(OnTeamChanged))]
    public int teamIndex = 0; // 0 = Team 1, 1 = Team 2, etc.

    [SyncVar(hook = nameof(OnNameChanged2))]
    public string displayName = "";

    // Available colors players can pick
    public static readonly Color[] AvailableColors = new Color[]
    {
        Color.cyan,
        Color.red,
        new Color(1f, 0.5f, 0f), // Orange
        Color.green,
        Color.magenta,
        Color.yellow,
        new Color(0.5f, 0f, 1f), // Purple
        Color.white,
    };

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Register with lobby UI whenever this player object is created on any client
        LobbyUI.Instance?.RegisterPlayer(this);
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        LobbyUI.Instance?.UnregisterPlayer(this);
    }

    // ── Called by host to assign index and default color ─────────────────
    [Server]
    public void ServerSetup(int index)
    {
        playerIndex = index;
        playerColor = AvailableColors[index % AvailableColors.Length];
        playerName  = $"Player {index + 1}";
        teamIndex   = index % 2; // alternate teams by default
    }

    // ── Commands — called by owning client, run on server ─────────────────
    [Command]
    public void CmdSetColor(Color color)
    {
        playerColor = color;
    }

    [Command]
    public void CmdSetReady(bool ready)
    {
        isReady = ready;
        RTSNetworkManager.Instance?.CheckAllReady();
    }
    [Command]
    public void CmdSetName(string name)
    {
        playerName = name.Length > 0 ? name : $"Player {playerIndex + 1}";
    }

    [Command]
    public void CmdSetTeam(int team)
    {
        teamIndex = team;
    }

    // ── Hooks — run on all clients when SyncVar changes ───────────────────
    void OnNameChanged(string oldVal, string newVal)  => LobbyUI.Instance?.RefreshPlayerList();
    void OnColorChanged(Color oldVal, Color newVal)   => LobbyUI.Instance?.RefreshPlayerList();
    void OnReadyChanged(bool oldVal, bool newVal)     => LobbyUI.Instance?.RefreshPlayerList();
    void OnTeamChanged(int oldVal, int newVal)  => LobbyUI.Instance?.RefreshPlayerList();
    void OnNameChanged2(string oldVal, string newVal) => LobbyUI.Instance?.RefreshPlayerList();
}