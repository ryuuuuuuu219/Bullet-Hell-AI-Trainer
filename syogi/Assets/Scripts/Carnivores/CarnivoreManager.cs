using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class CarnivoreManager : SpeciesManager
{
    protected override string DefaultSpeciesId => "carnivore";
    private GameObject carnivorePrefab;
    private int initialCount = 15;
    private bool randomizeSeed = false;
    private int seed = 1;
    private int spawnedCount;
    private System.Random birthRandom;
    private System.Random reproductionRandom;
    private void Start()
    {
        if (Population == null) return;
        var settings = Settings;
        carnivorePrefab = startupSettings.carnivorePrefab;
        initialCount = settings.initialCount;
        randomizeSeed = settings.randomizeSeed;
        seed = settings.seed;

        if (carnivorePrefab == null)
        {
            Debug.LogError("CarnivoreManager requires a carnivore prefab.", this);
            return;
        }
        ActualSeed = randomizeSeed ? System.Environment.TickCount : seed;
        birthRandom = new System.Random(ActualSeed);
        reproductionRandom = new System.Random(ActualSeed);
        var random = new System.Random(ActualSeed);
        float half = GrassField.FieldSize * 0.5f;
        for (int i = 0; i < initialCount; i++)
        {
            float x = Mathf.Min((float)((random.NextDouble() * 2 - 1) * half), half - 0.001f);
            float y = Mathf.Min((float)((random.NextDouble() * 2 - 1) * half), half - 0.001f);
            Spawn(new Vector3(x, y, 0), Vector2.right);
        }
    }

    private void Spawn(Vector3 position, Vector2 direction, double birthResources = AnimalResources.Initial)
    {
        GameObject child = Instantiate(carnivorePrefab, position, Quaternion.identity, transform);
        child.name = SpeciesId + " " + (++spawnedCount);
        child.GetComponent<CarnivoreAnimal>().SetInitialDirection(direction);
        child.GetComponent<AnimalBody>().Resources.SetBirthResources(birthResources);
        Population.CheckExtinction();
    }

    public void TryReproduce(CarnivoreAnimal parent)
    {
        if (carnivorePrefab == null || parent == null || parent.Body.SpeciesId != SpeciesId || !parent.Body.IsAlive || parent.Body.IsSleeping ||
            !parent.Resources.CanReproduce(parent.Body.Traits, parent.Body.Activity.ActiveAge)) return;
        if (birthRandom == null) birthRandom = new System.Random(seed);
        Vector3 position = parent.transform.position;
        Vector2 direction = parent.MovementDirection;
        if (!CarnivoreSpacing.TryBirthPosition(position.x, position.y, direction.x, direction.y,
            parent.Body.Radius, birthRandom.NextDouble(), birthRandom.Next(2) == 0,
            GrassField.FieldSize * 0.5, out double x, out double y)) return;
        // Do not charge food if neither lateral position is inside the field.
        if (reproductionRandom == null) reproductionRandom = new System.Random(seed);
        double probability = parent.Body.Traits.BirthSurvivalProbability(AnimalBody.LivingSpeciesCount(parent.Body.SpeciesId));
        if (!parent.Resources.TryReproduce(parent.Body.Traits, parent.Body.Activity.ActiveAge, probability,
            reproductionRandom, out int livingOffspring, out double stillbornCarbon)) return;
        for (int i = 0; i < livingOffspring; i++)
            Spawn(new Vector3((float)x, (float)y, position.z), -direction, parent.Body.Traits.offspringResources);
        parent.Body.ScatterCarbon(stillbornCarbon);
        if (parent.Resources.IsDead) parent.Body.Die();
    }
}
