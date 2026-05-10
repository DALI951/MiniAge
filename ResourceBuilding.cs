using UnityEngine;

/// <summary>
/// Attach to Farm, Market, LumberMill.
/// Generates resources over time automatically.
/// </summary>
public class ResourceBuilding : MonoBehaviour
{
    [Header("Resource Generation")]
    [SerializeField] private ResourceType resourceType = ResourceType.Food;
    [SerializeField] private int          amountPerTick = 5;
    [SerializeField] private float        tickInterval  = 5f;

    private float timer = 0f;

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= tickInterval)
        {
            timer = 0f;
            Debug.Log($"[{name}] Generating {amountPerTick} {resourceType}");
            switch (resourceType)
            {
                case ResourceType.Food:
                    ResourceManager.Instance?.AddResources(amountPerTick, 0, 0);
                    break;
                case ResourceType.Wood:
                    ResourceManager.Instance?.AddResources(0, amountPerTick, 0);
                    break;
                case ResourceType.Gold:
                    ResourceManager.Instance?.AddResources(0, 0, amountPerTick);
                    break;
            }
        }
    }
}