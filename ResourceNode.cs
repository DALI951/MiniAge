using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ResourceNode — base class for Tree, Mine, Animal.
///
/// Fix Bug 5: OnDepleted now uses Destroy(gameObject) immediately after
///            hiding visuals, with a short delay for a smooth fade.
///            The node marks itself IsEmpty first so no new gatherers attach.
/// </summary>
public class ResourceNode : MonoBehaviour
{
    [Header("=== RESOURCE TYPE ===")]
    [SerializeField] private ResourceType resourceType = ResourceType.Wood;

    [Header("=== KILL REQUIRED? (Animals only — tick this) ===")]
    [SerializeField] public  bool requiresKill = false;
    [SerializeField] private int  maxHealth    = 50;

    [Header("=== AMOUNTS ===")]
    [SerializeField] private int totalAmount  = 200;
    [SerializeField] private int maxGatherers = 8;

    [Tooltip("Drag the visible mesh here. It hides when the node is depleted.")]
    [SerializeField] private GameObject visualRoot;

    [Tooltip("Seconds before the GameObject is destroyed after depletion.")]
    [SerializeField] private float destroyDelay = 0.5f;

    // ── Runtime ───────────────────────────────────────────────────────────
    private int            remainingAmount;
    private int            currentHealth;
    private bool           isDead    = false;
    private bool           depleted  = false;      // prevents double-depletion
    private List<Villager> gatherers = new List<Villager>();

    protected virtual void Awake()
    {
        remainingAmount = totalAmount;
        currentHealth   = requiresKill ? maxHealth : 1;

        if (GetComponent<Collider>() == null &&
            GetComponentInChildren<Collider>() == null)
        {
            CapsuleCollider cc = gameObject.AddComponent<CapsuleCollider>();
            cc.height = 1f;
            cc.radius = 0.25f;
            cc.center = new Vector3(0, 0.5f, 0); // lift above ground
        }
    }

    // ── Click to inspect ──────────────────────────────────────────────────
    private void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;
        ResourceInfoUI.Instance?.Show(this);
    }

    // ── Gatherer management ───────────────────────────────────────────────

    public bool RequestGather(Villager villager)
    {
        if (depleted) return false;
        gatherers.RemoveAll(v => v == null);
        if (gatherers.Contains(villager)) return true;

        if (gatherers.Count < maxGatherers)
        {
            // Don't add to list yet — villager adds itself when it actually starts gathering
            return true;
        }

        ResourceNode next = FindNearest(villager.transform.position, resourceType, this);
        if (next != null) villager.GatherFrom(next);
        return false;
}

// Called by Villager when it physically starts gathering
    public void ConfirmGathering(Villager villager)
    {
        if (!gatherers.Contains(villager))
            gatherers.Add(villager);
    }

    public void ReleaseGatherer(Villager v) => gatherers.Remove(v);

    // ── Gather ────────────────────────────────────────────────────────────

    public int Gather(int amount)
    {
        if (depleted) return 0;
        if (requiresKill && !isDead) return 0;

        int got = Mathf.Min(amount, remainingAmount);
        remainingAmount -= got;

        if (remainingAmount <= 0 && !depleted)
            OnDepleted();

        return got;
    }

    // ── Animal combat ─────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (!requiresKill || isDead) return;
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead        = true;
            OnKilled();
        }
    }

    protected virtual void OnKilled()
    {
        Debug.Log($"[{name}] Killed — ready to gather.");
        if (this is AnimalNode a) a.StopMoving();
    }

    protected virtual void OnDepleted()
    {
        if (depleted) return;
        depleted = true;

        // Disable the entire GameObject immediately
        gameObject.SetActive(false);

        ResourceInfoUI.Instance?.HideIfShowing(this);

        foreach (Villager v in gatherers)
        {
            if (v == null) continue;
            ResourceNode next = FindNearest(v.transform.position, resourceType, this);
            if (next != null) v.GatherFrom(next);
        }
        gatherers.Clear();

        Destroy(gameObject, 0.3f);
    }

    // ── Static finder ─────────────────────────────────────────────────────

    public static ResourceNode FindNearest(
        Vector3 pos, ResourceType type, ResourceNode exclude = null)
    {
        ResourceNode best    = null;
        float        bestDist = float.MaxValue;

        foreach (ResourceNode n in FindObjectsOfType<ResourceNode>())
        {
            if (n == exclude || n.depleted) continue;
            if (n.resourceType != type) continue;
            float d = Vector3.Distance(pos, n.transform.position);
            if (d < bestDist) { bestDist = d; best = n; }
        }
        return best;
    }

    // ── Properties ────────────────────────────────────────────────────────
    public ResourceType ResourceType    => resourceType;
    public int          RemainingAmount => remainingAmount;
    public int          TotalAmount     => totalAmount;
    public int          MaxHealth       => maxHealth;
    public int          CurrentHealth   => currentHealth;
    public bool         RequiresKill    => requiresKill;
    public bool         IsKilled        => isDead || !requiresKill;
    public bool         IsEmpty         => depleted || remainingAmount <= 0;
    public int          GathererCount   => gatherers.Count;
    public int          MaxGatherers    => maxGatherers;
}
