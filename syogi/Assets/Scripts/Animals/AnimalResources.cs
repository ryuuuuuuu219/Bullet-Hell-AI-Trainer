using System;

// Carbon fuels passive recovery; stamina pays for actions. MVP animal armor is 0.
public class AnimalResources
{
    public const double Initial = 10;
    public const double Maximum = 50;
    public const double ReproductionThreshold = 40;
    public const double ReproductionCost = Initial;
    public const double MaintenancePerSecond = 0.05;
    public const double InitialStamina = 50;
    public const double MaximumStamina = 100;
    public const double RecoveryPerCarbonPerSecond = 0.2;
    public const double SleepingRecoveryMultiplier = 3;
    public double Stamina { get; private set; } = InitialStamina;
    public double Health { get; private set; }
    public double HealthLimit { get; }
    public double Amount { get; private set; } = Initial;
    public bool IsDead => Amount <= 0 || Health <= 0 || Stamina <= 0;

    public AnimalResources(double initialHealth)
    {
        Health = initialHealth;
        HealthLimit = initialHealth * 2;
    }

    public void Maintain(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        Amount = Math.Max(0, Amount - MaintenancePerSecond * seconds);
        if (Amount < 1e-10) Amount = 0;
    }

    public void Feed(double actualConsumed)
    {
        AbsorbCarbon(actualConsumed);
    }

    public void Advance(double seconds, bool sleeping)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (IsDead || seconds == 0) return;
        // Sleeping preserves carbon; awake recovery integrates its gradual depletion.
        double maintenance = sleeping ? 0 : MaintenancePerSecond;
        double aliveSeconds = sleeping ? seconds : Math.Min(seconds, Amount / maintenance);
        double recovery = (Amount - maintenance * aliveSeconds / 2) * aliveSeconds *
            RecoveryPerCarbonPerSecond * (sleeping ? SleepingRecoveryMultiplier : 1);
        Health = Math.Min(HealthLimit, Health + recovery);
        Stamina = Math.Min(MaximumStamina, Stamina + recovery);
        if (!sleeping) Maintain(seconds);
    }

    public void SpendStamina(double amount)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Stamina = Math.Max(0, Stamina - amount);
        if (Stamina < 1e-10) Stamina = 0;
    }

    // Carbon pickup does not heal. Return the amount accepted so overflow stays in the particle.
    public double AbsorbCarbon(double carbon)
    {
        if (IsDead || double.IsNaN(carbon) || double.IsInfinity(carbon) || carbon <= 0) return 0;
        double accepted = Math.Min(carbon, Math.Max(0, Maximum - Amount));
        Amount += accepted;
        return accepted;
    }

    public double ReleaseCarbon()
    {
        double carbon = Amount;
        Amount = 0;
        return carbon;
    }

    // Both subtractions use the values BEFORE impact.
    public double Hit(double power, out double stolenResources)
        => HitWithDamageMultiplier(power, 1, out stolenResources);

    public double Hit(double power, string projectileSpeciesId, string targetSpeciesId, out double stolenResources)
        => HitWithDamageMultiplier(power, !string.IsNullOrEmpty(projectileSpeciesId) && projectileSpeciesId == targetSpeciesId ? 0.1 : 1,
            out stolenResources);

    private double HitWithDamageMultiplier(double power, double damageMultiplier, out double stolenResources)
    {
        stolenResources = 0;
        if (IsDead || double.IsNaN(power) || double.IsInfinity(power) || power <= 0) return power;
        double previousHealth = Health;
        Health = Math.Max(0, previousHealth - power * damageMultiplier);
        if (Health <= 0)
        {
            stolenResources = Amount;
            Amount = 0;
        }
        // Damage reduction does not scale projectile power. Preserve the existing
        // consumption rule: subtract the target's HP before impact.
        return Math.Max(0, power - previousHealth);
    }

    public bool TryReproduce()
    {
        return TryReproduce(new AnimalTraits(), double.MaxValue);
    }

    public void SetBirthResources(double amount)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0 || amount > Maximum)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
    }

    public void Spend(double amount)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = Math.Max(0, Amount - amount);
    }

    public bool CanReproduce(AnimalTraits traits, double activeAge) => !IsDead && traits != null &&
        traits.ValidReproduction && activeAge >= traits.maturitySeconds &&
        Amount >= traits.reproductionThreshold && Amount >= traits.BirthCost && Stamina >= traits.reproductionStaminaCost;

    public bool TryReproduce(AnimalTraits traits, double activeAge)
    {
        if (!CanReproduce(traits, activeAge)) return false;
        Spend(traits.BirthCost);
        SpendStamina(traits.reproductionStaminaCost);
        return true;
    }

    // A paid attempt can produce no living offspring. Carbon is allocated either
    // to living children or to drops; SP is charged once for the entire litter.
    public bool TryReproduce(AnimalTraits traits, double activeAge, double survivalProbability,
        Random random, out int livingOffspring, out double stillbornCarbon)
    {
        livingOffspring = 0;
        stillbornCarbon = 0;
        if (random == null || double.IsNaN(survivalProbability) ||
            survivalProbability < 0 || survivalProbability > 1) return false;
        if (!TryReproduce(traits, activeAge)) return false;
        for (int i = 0; i < traits.offspringCount; i++)
            if (random.NextDouble() < survivalProbability) livingOffspring++;
        stillbornCarbon = (traits.offspringCount - livingOffspring) * traits.offspringResources;
        return true;
    }
}
