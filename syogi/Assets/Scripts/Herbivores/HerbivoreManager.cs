using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class HerbivoreManager : SpeciesManager
{
    protected override string DefaultSpeciesId => "herbivore";
    private GameObject herbivorePrefab;
    private int initialCount = 50;
    private bool randomizeSeed = false;
    private int seed = 1;
    private int spawnedCount;
    private System.Random reproductionRandom;
    public bool TryReproduce(AnimalBody parent, Vector2 parentDirection)
    {
        if (herbivorePrefab == null || parent == null || parent.SpeciesId != SpeciesId || !parent.IsAlive || parent.IsSleeping || parent.IsRunning ||
            !parent.Resources.CanReproduce(parent.Traits, parent.Activity.ActiveAge)) return false;
        if (reproductionRandom == null) reproductionRandom = new System.Random(seed);
        double probability = parent.Traits.BirthSurvivalProbability(AnimalBody.LivingSpeciesCount(parent.SpeciesId));
        if (!parent.Resources.TryReproduce(parent.Traits, parent.Activity.ActiveAge, probability,
            reproductionRandom, out int livingOffspring, out double stillbornCarbon)) return false;
        for (int i = 0; i < livingOffspring; i++)
        {
            GameObject child = Spawn(parent.transform.position);
            child.GetComponent<AnimalBody>().Resources.SetBirthResources(parent.Traits.offspringResources);
            child.GetComponent<HerbivoreMovement>().SetInitialDirection(-parentDirection);
        }
        parent.ScatterCarbon(stillbornCarbon);
        if (parent.Resources.IsDead) parent.Die();
        return true;
    }

    private GameObject Spawn(Vector3 position)
    {
        GameObject animal = Instantiate(herbivorePrefab, position, Quaternion.identity, transform);
        animal.name = SpeciesId + " " + (++spawnedCount);
        Population.CheckExtinction();
        return animal;
    }

    private void Start()
    {
        if (Population == null) return;
        var settings = Settings;
        herbivorePrefab = startupSettings.herbivorePrefab;
        initialCount = settings.initialCount;
        randomizeSeed = settings.randomizeSeed;
        seed = settings.seed;

        if (herbivorePrefab == null)
        {
            Debug.LogError("HerbivoreManager requires a herbivore prefab.", this);
            return;
        }

        ActualSeed = randomizeSeed ? System.Environment.TickCount : seed;
        reproductionRandom = new System.Random(ActualSeed);
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
