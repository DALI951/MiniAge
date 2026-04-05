using UnityEngine;

/// <summary>
/// HomeSite building — pre-placed in the scene at game start.
/// Can spawn Villager units.
/// Inherits all spawning logic from Building.
/// Add HomeSite-specific logic here (e.g., population cap, upgrades).
/// </summary>
public class HomeSite : Building
{
    protected override void Start()
    {
        buildingName = "Home Site";
        base.Start();
        Debug.Log("[HomeSite] Ready.");
    }

    // Example hook: override Select() to add HomeSite-specific UI or sounds
    public override void Select()
    {
        base.Select();
        // e.g., play a selection sound, highlight the building, etc.
    }
}
