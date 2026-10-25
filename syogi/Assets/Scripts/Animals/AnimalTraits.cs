using System;

/// <summary>Species settings. Unspecified gameplay values are tunable defaults, not balance guarantees.</summary>
[Serializable]
public sealed class AnimalTraits
{
    public double reproductionThreshold = 40;
    public double maturitySeconds = 30;
    public int offspringCount = 1;
    public double offspringResources = 10;
    // Parent consumption / total resources given to offspring.
    public double reproductionCostRatio = 1;
    public float activeLightMinimum = 0;
    public float activeLightMaximum = 1;
    public float searchDistance = 1000;
    public float nightSearchMultiplier = 0.5f;
    public float maximumGrassConcealment = 0.5f;
    public float grassOcclusionPerUnit = 0.1f;
    public float walkingSpeed = 105;
    public float runningSpeed = 150;
    public double runningCostPerSecond = 0.1;
    public float escapeHoldSeconds = 2;

    public static AnimalTraits HerbivoreDefaults() => new AnimalTraits { searchDistance = 500, walkingSpeed = 80 };
    public AnimalTraits Copy() => (AnimalTraits)MemberwiseClone();
    public bool IsActive(double light) => light >= activeLightMinimum && light <= activeLightMaximum;
    public double SearchRadius(double light) => Math.Max(0, searchDistance) *
        (Math.Max(0, nightSearchMultiplier) + (1 - Math.Max(0, nightSearchMultiplier)) * Math.Max(0, Math.Min(1, light)));
    public double DetectionRadius(double light, double grassCoverage, double targetConcealment) => SearchRadius(light) *
        (1 - Math.Max(0, Math.Min(1, grassCoverage)) * Math.Max(0, Math.Min(1, targetConcealment)));
    public double DetectionRadius(double light, double grassCoverage, double targetConcealment, double interveningGrass)
        => DetectionRadius(light, grassCoverage, targetConcealment) /
           (1 + Math.Max(0, grassOcclusionPerUnit) * Math.Max(0, interveningGrass));
    public double BirthCost => offspringCount * offspringResources * reproductionCostRatio;
    public bool ValidReproduction => offspringCount > 0 && Finite(reproductionThreshold) && reproductionThreshold >= 0 &&
        Finite(maturitySeconds) && maturitySeconds >= 0 && Finite(offspringResources) && offspringResources > 0 &&
        offspringResources <= AnimalResources.Maximum && Finite(reproductionCostRatio) && reproductionCostRatio >= 0 &&
        Finite(BirthCost);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

/// <summary>Activity time and escape memory advance only while awake.</summary>
public sealed class AnimalActivity
{
    public double ActiveAge { get; private set; }
    public double EscapeRemaining { get; private set; }
    public bool IsSleeping { get; private set; }
    private bool threatDetected;
    public bool IsRunning => !IsSleeping && (threatDetected || EscapeRemaining > 0);

    public void Step(double seconds, double light, bool threat, AnimalTraits traits, AnimalResources resources)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        IsSleeping = !traits.IsActive(light);
        if (IsSleeping || resources.IsDead || seconds == 0) return;
        threatDetected = threat;
        ActiveAge += seconds;
        double runningSeconds = threat ? seconds : Math.Min(seconds, EscapeRemaining);
        EscapeRemaining = threat ? Math.Max(0, traits.escapeHoldSeconds) : Math.Max(0, EscapeRemaining - seconds);
        resources.Maintain(seconds);
        resources.Spend(Math.Max(0, traits.runningCostPerSecond) * runningSeconds);
    }
}
