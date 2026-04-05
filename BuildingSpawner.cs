using UnityEngine;

/// <summary>
/// BuildingSpawner v7.
/// Places HomeSite and Barracks at the player's spawn point, facing map center.
/// Automatically sets the Building layer on spawned objects so clicking works.
/// </summary>
public class BuildingSpawner : MonoBehaviour
{
    [Header("Building Prefabs")]
    [SerializeField] private GameObject homeSitePrefab;
    [SerializeField] private GameObject barracksPrefab;

    [Header("Offsets from spawn point")]
    [SerializeField] private float homeSiteOffset = -8f;
    [SerializeField] private float barracksOffset =  8f;

    [Header("Player")]
    [SerializeField] public int playerIndex = 0;

    [Header("Layer")]
    [Tooltip("Must match the 'Building' layer you created in Unity.")]
    [SerializeField] private string buildingLayerName = "Building";

    private void Start()
    {
        SpawnAreaManager area = FindObjectOfType<SpawnAreaManager>();
        if (area == null)
        {
            Debug.LogError("[BuildingSpawner] No SpawnAreaManager found!");
            return;
        }

        Vector3 spawnPos  = area.GetSpawnPosition(playerIndex);
        Vector3 toCenter  = (Vector3.zero - spawnPos).normalized;
        Vector3 side      = Vector3.Cross(toCenter, Vector3.up).normalized;
        Quaternion rot    = Quaternion.LookRotation(toCenter);

        int buildingLayer = LayerMask.NameToLayer(buildingLayerName);

        SpawnBuilding(homeSitePrefab,  spawnPos + side * homeSiteOffset,  rot, buildingLayer);
        SpawnBuilding(barracksPrefab,  spawnPos + side * barracksOffset,  rot, buildingLayer);
    }

    private void SpawnBuilding(GameObject prefab, Vector3 pos, Quaternion rot, int layer)
    {
        if (prefab == null) return;

        pos.y = 0.5f;
        GameObject go = Instantiate(prefab, pos, rot);

        // Set the layer on the root AND all children so raycasts work
        SetLayerRecursive(go, layer);

        // Make sure it has a collider for clicking
        if (go.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size        = new Vector3(2f, 2f, 2f);
        }
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return; // layer not found — don't apply
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
