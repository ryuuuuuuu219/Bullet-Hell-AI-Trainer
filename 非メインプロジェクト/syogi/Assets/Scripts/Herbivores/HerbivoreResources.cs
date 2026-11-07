public sealed class HerbivoreResources : AnimalResources
{
    public const double InitialHealth = 30;
    public const double MaximumHealth = 60;
    public HerbivoreResources(double initialHealth = InitialHealth) : base(initialHealth) { }
}
