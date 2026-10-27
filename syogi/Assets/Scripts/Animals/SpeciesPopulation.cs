using System.Collections.Generic;

// Runtime state belongs to the scene, never to the shared ScriptableObject asset.
public sealed class SpeciesPopulation
{
    public SpeciesDefinition Definition { get; }
    private readonly HashSet<AnimalBody> individuals = new HashSet<AnimalBody>();
    public IReadOnlyCollection<AnimalBody> Individuals => individuals;
    private bool hadLivingIndividuals;
    public SpeciesPopulation(SpeciesDefinition definition) => Definition = definition;
    public void Register(AnimalBody body) => individuals.Add(body);
    public void Unregister(AnimalBody body) => individuals.Remove(body);
    public int LivingCount
    {
        get
        {
            int count = 0;
            foreach (var body in individuals) if (body != null && body.IsAlive) count++;
            return count;
        }
    }
    public bool CheckExtinction()
    {
        bool living = LivingCount > 0;
        bool extinct = hadLivingIndividuals && !living;
        hadLivingIndividuals = living;
        return extinct;
    }
}
