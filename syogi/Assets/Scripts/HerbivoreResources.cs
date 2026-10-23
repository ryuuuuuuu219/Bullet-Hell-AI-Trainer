using System;

public sealed class HerbivoreResources
{
    public const double Initial = 10;
    public const double Maximum = 50;
    public const double ReproductionThreshold = 40;
    public const double ReproductionCost = 30;
    public const double MaintenancePerSecond = 0.2;
    public double Amount { get; private set; } = Initial;
    public bool IsDead => Amount <= 0;

    public void Maintain(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        Amount = Math.Max(0, Amount - MaintenancePerSecond * seconds);
        if (Amount < 1e-10) Amount = 0;
    }

    public void Feed(float actualConsumed)
    {
        if (IsDead || float.IsNaN(actualConsumed) || float.IsInfinity(actualConsumed) || actualConsumed <= 0) return;
        Amount = Math.Min(Maximum, Amount + actualConsumed);
    }

    public bool TryReproduce()
    {
        if (Amount < ReproductionThreshold) return false;
        Amount -= ReproductionCost;
        return true;
    }
}
