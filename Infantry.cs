using UnityEngine;

public class Infantry : Unit
{
    protected override void Awake()
    {
        unitName        = "Infantry";
        unitType        = "Infantry";   // distinct — double-click selects only Infantry
        unitDescription = "Frontline soldier. Strong and reliable.";
        maxHealth       = 120;
        baseSpeed       = 3.5f;
        attackDamage    = 20;
        attackRange     = 1f;
        attackCooldown  = 1.2f;
        base.Awake();
    }
}
