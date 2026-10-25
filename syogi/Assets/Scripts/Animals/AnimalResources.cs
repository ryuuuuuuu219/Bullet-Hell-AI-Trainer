using System;

// Shared economy; HP remains independent of stored food. MVP animal armor is 0.
public class AnimalResources
{
    public const double Initial = 10;
    public const double Maximum = 50;
    public const double ReproductionThreshold = 40;
    public const double ReproductionCost = Initial;
    public const double MaintenancePerSecond = 0.2;
    public double Health { get; private set; }
    public double HealthLimit { get; }
    public double Amount { get; private set; } = Initial;
    public bool IsDead => Amount <= 0 || Health <= 0;

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
        if (IsDead || double.IsNaN(actualConsumed) || double.IsInfinity(actualConsumed) || actualConsumed <= 0) return;
        Amount = Math.Min(Maximum, Amount + actualConsumed);
        Health = Math.Min(HealthLimit, Health + actualConsumed);
    }

    // Both subtractions use the values BEFORE impact.
    public double Hit(double power, out double stolenResources)
        => HitWithDamageMultiplier(power, 1, out stolenResources);

    public double Hit(double power, int projectileSpeciesId, int targetSpeciesId, out double stolenResources)
        => HitWithDamageMultiplier(power, projectileSpeciesId == targetSpeciesId ? 0.1 : 1,
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
        if (IsDead || Amount < ReproductionThreshold) return false;
        Amount -= ReproductionCost;
        return true;
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
        Amount >= traits.reproductionThreshold && Amount >= traits.BirthCost;

    public bool TryReproduce(AnimalTraits traits, double activeAge)
    {
        if (!CanReproduce(traits, activeAge)) return false;
        Spend(traits.BirthCost);
        return true;
    }
}
