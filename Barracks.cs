using UnityEngine;

/// <summary>
/// Barracks building — pre-placed in the scene at game start.
/// Can spawn Infantry and Cavalry units.
/// Inherits all spawning logic from Building.
/// </summary>
public class Barracks : Building
{
    public override int TrainingPriority => 10;
    protected override void Start()
    {
        buildingName = "Barracks";
        base.Start();
    }

}
