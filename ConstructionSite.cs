using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Placed on the ground when a villager orders construction.
/// Villager walks here and builds over time.
/// When complete, spawns the real building prefab.
/// </summary>
public class ConstructionSite : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float buildTime      = 10f;
    [SerializeField] private GameObject completedPrefab;

    private float    progress       = 0f;
    private bool     isComplete     = false;
    private int      pendingOwnerId = -1;
    private List<Villager> builders = new List<Villager>();
    public void Initialize(GameObject prefab, float time)
    {
        completedPrefab = prefab;
        buildTime       = time;
    }

    public void AssignBuilder(Villager v)
    {
        if (!builders.Contains(v))
            builders.Add(v);
        if (pendingOwnerId < 0 && v != null)
            pendingOwnerId = v.OwnerPlayerId;
    }

    private void Update()
    {
        if (isComplete) return;
        builders.RemoveAll(b => b == null);
        if (builders.Count == 0) return;

        // Count only builders that are close enough
        int activeBuilders = 0;
        foreach (Villager b in builders)
        {
            float dist = Vector3.Distance(b.transform.position, transform.position);
            if (dist <= 3f) activeBuilders++;
        }

        if (activeBuilders == 0) return;

        // More builders = faster construction
        progress += Time.deltaTime * activeBuilders;

        if (progress >= buildTime)
            Complete();
    }

    private void Complete()
    {
        isComplete = true;
        if (completedPrefab != null)
        {
            GameObject built = Instantiate(completedPrefab,
                transform.position, transform.rotation);
            int layer = LayerMask.NameToLayer("Building");
            SetLayerRecursive(built, layer);
            if (pendingOwnerId >= 0 && built.TryGetComponent(out Building b))
                b.SetOwner(pendingOwnerId);
        }
        foreach (Villager b in builders)
            if (b != null) b.OnBuildingComplete();
        builders.Clear();
        Destroy(gameObject);
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursive(t.gameObject, layer);
    }

    private void OnMouseDown()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        BuildingInfoUI.Instance?.ShowConstructionSite(this);
        // Show progress in resource info panel as a reuse
        float pct = buildTime > 0 ? (progress / buildTime) * 100f : 0f;
        Debug.Log($"[{name}] Build progress: {pct:F0}%");
        // We'll wire this to UI below
    }

    public float Progress   => progress;
    public float BuildTime  => buildTime;
    public bool  IsComplete => isComplete;
}