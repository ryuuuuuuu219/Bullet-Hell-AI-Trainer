using System.Collections.Generic;
using UnityEngine;

public abstract class SpeciesManager : MonoBehaviour
{
    [SerializeField] protected AquariumStartupSettings startupSettings;
    [SerializeField] protected string speciesId;
    public string SpeciesId => speciesId;
    public SpeciesPopulation Population { get; private set; }
    public SpeciesDefinition Definition => Population == null ? null : Population.Definition;
    public AnimalTraits Traits => Definition.traits;
    public int ActualSeed { get; protected set; }
    public int LivingCount => Population == null ? 0 : Population.LivingCount;
    private static readonly List<SpeciesManager> active = new List<SpeciesManager>();
    public static IReadOnlyList<SpeciesManager> Active => active;
    protected AquariumStartupSettings.AnimalSettings Settings { get; private set; }
    protected abstract string DefaultSpeciesId { get; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();

    protected virtual void Awake()
    {
        if (string.IsNullOrEmpty(speciesId)) speciesId = DefaultSpeciesId;
        if (startupSettings == null || !startupSettings.TryFindSpecies(speciesId, out var settings))
        {
            Debug.LogError($"SpeciesManager: missing or duplicate species configuration '{speciesId}'.", this);
            enabled = false;
            return;
        }
        foreach (var manager in active)
            if (manager.SpeciesId == speciesId)
            {
                Debug.LogError($"SpeciesManager: duplicate scene manager '{speciesId}'.", this);
                enabled = false;
                return;
            }
        Settings = settings;
        Population = new SpeciesPopulation(settings.species);
        active.Add(this);
    }

    protected virtual void LateUpdate()
    {
        if (Population != null && Population.CheckExtinction())
            BattleSimulationSpeed.ReportExtinction(speciesId);
    }
    protected virtual void OnDestroy() => active.Remove(this);
}
