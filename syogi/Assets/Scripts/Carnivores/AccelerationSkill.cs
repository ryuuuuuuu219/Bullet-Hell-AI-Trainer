using System;

public static class AccelerationSkill
{
    public static float Speed(AnimalTraits traits, bool active) =>
        Math.Max(0, active ? Math.Max(traits.walkingSpeed, traits.runningSpeed) : traits.walkingSpeed);

    public static void Consume(AnimalTraits traits, AnimalResources resources, double seconds)
    {
        resources.Spend(Math.Max(0, traits.runningCostPerSecond) * seconds);
    }
}
