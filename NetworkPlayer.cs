using UnityEngine;
using Mirror;

/// <summary>
/// NetworkPlayer — one instance per connected player, owned by that player's client.
/// Stores the player's index and color. Synced across all clients.
///
/// This must be the prefab assigned to NetworkManager → Player Prefab.
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    // SyncVar = automatically synced from server to all clients
    [SyncVar] public int   playerIndex = 0;
    [SyncVar] public Color playerColor = Color.white;

    // ─── Server sets the player index when they join ──────────────────────
    [Server]
    public void SetPlayerIndex(int index)
    {
        playerIndex  = index;
        playerColor  = PlayerColorManager.Instance != null
            ? PlayerColorManager.Instance.GetColor(index)
            : Color.white;
    }

    // ─── Called on the owning client when the object is ready ────────────
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // Tell the local color manager who we are
        PlayerColorManager.LocalPlayerIndex = playerIndex;

        Debug.Log($"[NetworkPlayer] Local player index: {playerIndex}, color: {playerColor}");
    }
}
