using UnityEngine;

public sealed class HerbivoreManager : MonoBehaviour
{
    [SerializeField] private GameObject herbivorePrefab;
    [SerializeField, Min(0)] private int initialCount = 50;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed = 1;
    public int ActualSeed { get; private set; }
    private int spawnedCount;

    public int LivingCount
    {
        get
        {
            int count = 0;
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeInHierarchy
                    && child.TryGetComponent(out HerbivoreFeeding animal)
                    && animal.ResourceAmount > 0)
                    count++;
            }
            return count;
        }
    }

    public bool TryReproduce(Vector3 position, HerbivoreResources resources, Vector2 parentDirection)
    {
        if (herbivorePrefab == null || resources == null || !resources.TryReproduce()) return false;
        GameObject child = Spawn(position);
        child.GetComponent<HerbivoreMovement>().SetInitialDirection(-parentDirection);
        return true;
    }

    private GameObject Spawn(Vector3 position)
    {
        GameObject animal = Instantiate(herbivorePrefab, position, Quaternion.identity, transform);
        animal.name = "Herbivore " + (++spawnedCount);
        return animal;
    }

    private void Start()
    {
        if (herbivorePrefab == null)
        {
            Debug.LogError("HerbivoreManager requires a herbivore prefab.", this);
            return;
        }

        ActualSeed = randomizeSeed ? System.Environment.TickCount : seed;
        var random = new System.Random(ActualSeed);
        float halfSize = GrassField.FieldSize * 0.5f;
        for (int i = 0; i < initialCount; i++)
        {
            // Continuous world XY coordinates, independent of grass cell centers.
            float x = (float)((random.NextDouble() * 2.0 - 1.0) * halfSize);
            float y = (float)((random.NextDouble() * 2.0 - 1.0) * halfSize);
            // Keep the upper edge exclusive even after conversion to float.
            x = Mathf.Min(x, halfSize - 0.001f);
            y = Mathf.Min(y, halfSize - 0.001f);
            Spawn(new Vector3(x, y, 0f));
        }
    }
}
