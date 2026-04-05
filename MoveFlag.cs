using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages movement flags shown at destinations.
/// Attach to GameManager.
/// </summary>
public class MoveFlag : MonoBehaviour
{
    public static MoveFlag Instance { get; private set; }

    [SerializeField] private GameObject flagPrefab; // a simple flag prefab

    // Active flags dictionary: position -> flag object
    private Dictionary<Vector3, GameObject> activeFlags = new Dictionary<Vector3, GameObject>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Show a move flag. Clears previous ones unless shift held.</summary>
    public void ShowFlag(Vector3 position, bool addToPath = false)
    {
        if (flagPrefab == null) 
        {
            Debug.LogWarning("[MoveFlag] No flag prefab assigned!");
            return;
        }

        // Round position slightly to handle floating point precision
        Vector3 keyPos = RoundVector3(position, 2);

        if (!addToPath)
        {
            ClearAllFlags();
        }

        // Create flag if it doesn't exist at this position
        if (!activeFlags.ContainsKey(keyPos))
        {
            GameObject flag = Instantiate(flagPrefab, position + Vector3.up * 0.1f, Quaternion.identity);
            activeFlags[keyPos] = flag;
        }
    }

    /// <summary>Show a spawn point flag for a building.</summary>
    public void ShowSpawnFlag(Vector3 position)
    {
        ClearAllFlags();
        if (flagPrefab == null) 
        {
            Debug.LogWarning("[MoveFlag] No flag prefab assigned!");
            return;
        }
        
        Vector3 keyPos = RoundVector3(position, 2);
        
        if (!activeFlags.ContainsKey(keyPos))
        {
            GameObject flag = Instantiate(flagPrefab, position + Vector3.up * 0.1f, Quaternion.identity);
            activeFlags[keyPos] = flag;
        }
    }

    public void ClearAllFlags()
    {
        foreach (var kvp in activeFlags)
        {
            if (kvp.Value != null) Destroy(kvp.Value);
        }
        activeFlags.Clear();
    }

    public void ClearFlags()
    {
        ClearAllFlags();
    }

    private Vector3 RoundVector3(Vector3 vec, int decimals)
    {
        float multiplier = Mathf.Pow(10, decimals);
        return new Vector3(
            Mathf.Round(vec.x * multiplier) / multiplier,
            Mathf.Round(vec.y * multiplier) / multiplier,
            Mathf.Round(vec.z * multiplier) / multiplier
        );
    }
}
