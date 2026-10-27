using System;
using UnityEngine;

static class Program
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main()
    {
        TargetRules();
        var first = new SpeciesDefinition { speciesId = "carnivore", pursuitSpeciesIds = new[] { "herbivore", "snake" } };
        var second = new SpeciesDefinition { speciesId = "snake" };
        Check(first.Pursues("snake") && first.Pursues("herbivore"), "multiple explicit targets");
        Check(!first.Pursues("Snake") && !first.Pursues("carnivore") && !first.Pursues(null), "exact matching and exclusions");
        var identity = new SpeciesIdentity();
        identity.Initialize(second);
        Check(identity.SpeciesId == "snake" && identity.Definition == second, "identity derives from assigned species");
        var settings = new AquariumStartupSettings
        {
            layers = new[] {
                new AquariumStartupSettings.PopulationLayer { species = new[] { new AquariumStartupSettings.AnimalSettings { species = first, initialCount = 5 } } },
                new AquariumStartupSettings.PopulationLayer { species = new[] { new AquariumStartupSettings.AnimalSettings { species = second, initialCount = 2 } } }
            }
        };
        Check(settings.TryFindSpecies("snake", out var entry) && entry.initialCount == 2, "lookup across layers");
        Check(!settings.TryFindSpecies("missing", out _), "missing ID rejected");
        second.speciesId = "carnivore";
        Check(!settings.TryFindSpecies("carnivore", out _), "duplicate ID rejected");
        second.speciesId = "snake";
        var a = new SpeciesPopulation(first);
        var b = new SpeciesPopulation(second);
        Check(!a.CheckExtinction(), "zero initial population is not extinction");
        var parent = new AnimalBody(); var child = new AnimalBody();
        a.Register(parent); a.Register(parent); a.Register(child);
        b.Register(new AnimalBody());
        Check(a.LivingCount == 2 && b.LivingCount == 1 && !a.CheckExtinction(), "deduplicated independent populations");
        a.Unregister(parent);
        Check(!a.CheckExtinction(), "remaining child prevents extinction");
        child.IsAlive = false;
        Check(a.CheckExtinction() && !a.CheckExtinction(), "extinction fires once even before unregister");
        child.IsAlive = true;
        Check(!a.CheckExtinction(), "revival rearms extinction");
        a.Unregister(child);
        Check(a.CheckExtinction() && b.LivingCount == 1, "second extinction independent of other species");
        int events = 0;
        BattleSimulationSpeed.Extinction += _ => events++;
        BattleSimulationSpeed.SetUserSpeed(8);
        BattleSimulationSpeed.ReportExtinction("snake");
        Check(Time.timeScale == 0.1f && BattleSimulationSpeed.ExtinctionOverride && events == 1, "extinction overrides speed");
        BattleSimulationSpeed.SetUserSpeed(1);
        Check(Time.timeScale == 1 && !BattleSimulationSpeed.ExtinctionOverride, "user speed releases override");
        BattleSimulationSpeed.ReportExtinction("carnivore");
        Check(Time.timeScale == 0.1f && events == 2, "another extinction overrides again");
        Console.WriteLine("PASS string identity/targets, layered lookup, duplicate IDs, population lifecycle and extinction speed override");
    }

    static void TargetRules()
    {
        var herbivore = new SpeciesDefinition { speciesId = "herbivore" };
        var carnivore = new SpeciesDefinition { speciesId = "carnivore", pursuitSpeciesIds = new[] { "herbivore" }, fleeNonHerbivores = true };
        var dog = new SpeciesDefinition { speciesId = "dog", pursuitSpeciesIds = new[] { "herbivore" }, fleeSpeciesIds = new[] { "yoshi" }, fleeShips = true };
        var yoshi = new SpeciesDefinition { speciesId = "yoshi", pursuitSpeciesIds = new[] { "herbivore", "carnivore", "dog", "beetle" }, fleeSpeciesIds = new[] { "snake" }, fleeShips = true };
        var snake = new SpeciesDefinition { speciesId = "snake", pursuitSpeciesIds = new[] { "carnivore", "dog", "yoshi" }, fleeShips = true };
        var beetle = new SpeciesDefinition { speciesId = "beetle" };
        var species = new[] { herbivore, carnivore, dog, yoshi, snake, beetle };
        string[] attacks = { "", "herbivore", "herbivore", "herbivore carnivore dog beetle", "carnivore dog yoshi", "" };
        string[] fears = { "", "carnivore dog yoshi snake beetle", "yoshi", "snake", "", "" };
        for (int i = 0; i < species.Length; i++)
        for (int j = 0; j < species.Length; j++)
        {
            Check(species[i].PursuesSpecies(species[j]) == Array.Exists(attacks[i].Split(' '), id => id == species[j].speciesId), "attack matrix");
            Check(species[i].Fears(species[j]) == Array.Exists(fears[i].Split(' '), id => id == species[j].speciesId), "flee matrix");
        }
        var frigate = new SpeciesDefinition { speciesId = "frigate", isShip = true, shipRank = 1 };
        var destroyer = new SpeciesDefinition { speciesId = "destroyer", isShip = true, shipRank = 2 };
        var battleship = new SpeciesDefinition { speciesId = "battleship", isShip = true, shipRank = 3 };
        Check(destroyer.PursuesSpecies(frigate) && destroyer.PursuesSpecies(destroyer) && !destroyer.PursuesSpecies(battleship), "ship attack ranks including same class");
        Check(!destroyer.Fears(frigate) && destroyer.Fears(destroyer) && destroyer.Fears(battleship), "ship flee ranks including same class");
        foreach (var nonShip in species) Check(destroyer.PursuesSpecies(nonShip) && !destroyer.Fears(nonShip), "ships target non-ships");
        foreach (var hunter in new[] { carnivore, dog, yoshi, snake }) Check(hunter.Fears(frigate), "ship threat group");
        Check(!beetle.HasFleeTargets && !beetle.Fears(battleship), "beetle ignores threats");
        Console.WriteLine("PASS attack/flee matrix and ship rank rules");
    }
}
