using UnityEngine;

/// <summary>
/// Barracks building — pre-placed in the scene at game start.
/// Can spawn Infantry and Cavalry units.
/// Inherits all spawning logic from Building.
/// </summary>
public class Barracks : Building
{
    protected override void Start()
    {
        buildingName = "Barracks";
        base.Start();
        Debug.Log("[Barracks] Ready.");
    }

    // Example hook: override Select() to add Barracks-specific UI or sounds
    public override void Select()
    {
        base.Select();
        // e.g., play a military drum sound, show rally-point UI, etc.
    }
}
