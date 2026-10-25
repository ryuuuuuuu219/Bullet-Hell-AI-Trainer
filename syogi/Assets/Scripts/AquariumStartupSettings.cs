using UnityEngine;

/// <summary>Population settings are read at startup; traits are copied on each spawn. Edit before play for reproducible runs.</summary>
[CreateAssetMenu(fileName = "AquariumStartupSettings", menuName = "Aquarium/Startup Settings")]
public sealed class AquariumStartupSettings : ScriptableObject
{
    [Header("草")]
    public GrassSettings grass = new GrassSettings();
    [Header("草食動物")]
    public AnimalSettings herbivores = new AnimalSettings { initialCount = 50, traits = AnimalTraits.HerbivoreDefaults() };
    [Header("肉食動物")]
    public AnimalSettings carnivores = new AnimalSettings { initialCount = 15 };
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
        public GameObject prefab;
        [Min(0)] public int initialCount;
        public bool randomizeSeed = true;
        public int seed = 1;
        public AnimalTraits traits = new AnimalTraits();
    }
}

