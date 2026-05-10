using UnityEngine;
using Mirror;

/// <summary>
/// BuildingSpawner v9.
/// Places HomeSite and Barracks at the player's spawn point, facing map center.
/// FIX: Defers NetworkClient.identity read to avoid race condition on slow connections.
/// </summary>
public class BuildingSpawner : MonoBehaviour
{
    [Header("Building Prefabs")]
    [SerializeField] private GameObject homeSitePrefab;
    [SerializeField] private GameObject barracksPrefab;

    [Header("Offsets from spawn point")]
    [SerializeField] private float homeSiteOffset = -8f;
    [SerializeField] private float barracksOffset =  8f;
    [SerializeField] private float inwardFromEdge = 4f;

    [Header("Player")]
    [SerializeField] public int playerIndex = 0;

    [Header("Layer")]
    [SerializeField] private string buildingLayerName = "Building";

    [Header("Placement")]
    [SerializeField] private float buildingYOffset = 1f;

    private void Start()
    {
        // If networked, wait for identity to be ready
        if (NetworkClient.active)
        {
            StartCoroutine(DeferredSpawn());
        }
        else
        {
            DoSpawn(playerIndex);
        }
    }

    private System.Collections.IEnumerator DeferredSpawn()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (NetworkClient.connection?.identity != null &&
                NetworkClient.connection.identity.TryGetComponent(out LobbyPlayer lp))
            {
                DoSpawn(lp.playerIndex);
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.LogWarning($"[BuildingSpawner] Network identity timeout. Using fallback playerIndex={playerIndex}.");
        DoSpawn(playerIndex);
    }

    private void DoSpawn(int idx)
    {
        SpawnAreaManager area = FindObjectOfType<SpawnAreaManager>();
        if (area == null)
        {
            Debug.LogError("[BuildingSpawner] No SpawnAreaManager found!");
            return;
        }

        Vector3 spawnPos  = area.GetSpawnPosition(idx);
        Vector3 toCenter  = (Vector3.zero - spawnPos).normalized;
        if (toCenter.sqrMagnitude > 0.001f)
            spawnPos += toCenter * Mathf.Max(0f, inwardFromEdge);
        Vector3 side      = Vector3.Cross(toCenter, Vector3.up).normalized;
        Quaternion rot    = Quaternion.LookRotation(toCenter);

        int buildingLayer = LayerMask.NameToLayer(buildingLayerName);

        SpawnBuilding(homeSitePrefab,  spawnPos + side * homeSiteOffset,  rot, buildingLayer, idx);
        SpawnBuilding(barracksPrefab,  spawnPos + side * barracksOffset,  rot, buildingLayer, idx);
    }

    private void SpawnBuilding(GameObject prefab, Vector3 pos, Quaternion rot, int layer, int ownerIndex)
    {
        if (prefab == null) return;

        if (MapBoundary.Instance != null)
            pos = MapBoundary.Instance.Clamp(pos);

        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            pos = hit.position;
        else
            pos.y = 0f;

        if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 20f))
            pos.y = groundHit.point.y;

        pos.y += buildingYOffset;
        GameObject go = Instantiate(prefab, pos, rot);

        SetLayerRecursive(go, layer);

        if (go.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(2f, 2f, 2f);
        }

        if (go.TryGetComponent(out Building building))
            building.SetOwner(ownerIndex);
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}