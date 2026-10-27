using UnityEngine;

/// <summary>Population settings are read at startup; traits are copied on each spawn. Edit before play for reproducible runs.</summary>
[CreateAssetMenu(fileName = "AquariumStartupSettings", menuName = "Aquarium/Startup Settings")]
public sealed class AquariumStartupSettings : ScriptableObject
{
    [Header("草")]
    public GrassSettings grass = new GrassSettings();
    [Header("動物の層別配置（草食、第一層肉食、第二層肉食…）")]
    public PopulationLayer[] layers = System.Array.Empty<PopulationLayer>();
    public GameObject herbivorePrefab;
    public GameObject carnivorePrefab;

    [System.Serializable]
    public sealed class PopulationLayer
    {
        public string name;
        public AnimalSettings[] species = System.Array.Empty<AnimalSettings>();
    }

    public bool TryFindSpecies(string id, out AnimalSettings result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(id) || layers == null) return false;
        foreach (var layer in layers)
        {
            if (layer == null || layer.species == null) continue;
            foreach (var entry in layer.species)
            {
                if (entry == null || entry.species == null || entry.species.speciesId != id) continue;
                if (result != null) { result = null; return false; }
                if (entry.species.traits == null) return false;
                result = entry;
            }
        }
        return result != null;
    }
    [Header("昼夜（0=夜、1=昼）")]
    [Min(0.01f)] public float dayNightPeriodSeconds = 120;
    public float dayNightPhaseDegrees;

    [System.Serializable]
    public sealed class GrassSettings
    {
        [Min(0)] public int initialSpawnCount = 150;
        public bool randomizeSeed = true;
        public int seed = 1;
        [Min(0.01f)] public float updateInterval = 15f;
        [Min(1)] public float maxGrass = 3f;
        [Tooltip("枯死後の草量（0以下）")] public float deathAmount = -1f;
        [Min(0)] public float increaseAmount = 1f;
        [Range(0, 8)] public int birthNeighborMinimum = 1;
        [Range(0, 8)] public int birthNeighborMaximum = 1;
        public bool growthEnabled = true;
        public bool wrapBoundary = false;
    }

    [System.Serializable]
    public sealed class AnimalSettings
    {
        public SpeciesDefinition species;
        [Min(0)] public int initialCount;
        public bool randomizeSeed = false;
        public int seed = 1;
    }
}

