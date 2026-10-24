using UnityEngine;

public sealed class CarnivoreManager : MonoBehaviour
{
    [SerializeField] private GameObject carnivorePrefab;
    [SerializeField, Min(0)] private int initialCount = 15;
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
                if (child.TryGetComponent(out AnimalBody body) && body.IsAlive) count++;
            return count;
        }
    }

    private void Start()
    {
        if (carnivorePrefab == null)
        {
            Debug.LogError("CarnivoreManager requires a carnivore prefab.", this);
            return;
        }
        ActualSeed = randomizeSeed ? System.Environment.TickCount : seed;
        var random = new System.Random(ActualSeed);
        float half = GrassField.FieldSize * 0.5f;
        for (int i = 0; i < initialCount; i++)
        {
            float x = Mathf.Min((float)((random.NextDouble() * 2 - 1) * half), half - 0.001f);
            float y = Mathf.Min((float)((random.NextDouble() * 2 - 1) * half), half - 0.001f);
            Spawn(new Vector3(x, y, 0), Vector2.right);
        }
    }

    private void Spawn(Vector3 position, Vector2 direction)
    {
        GameObject child = Instantiate(carnivorePrefab, position, Quaternion.identity, transform);
        child.name = "Carnivore " + (++spawnedCount);
        child.GetComponent<CarnivoreAnimal>().SetInitialDirection(direction);
    }

    public void TryReproduce(CarnivoreAnimal parent)
    {
        if (carnivorePrefab == null || !parent.Resources.TryReproduce()) return;
        Spawn(parent.transform.position, -parent.MovementDirection);
    }
}
