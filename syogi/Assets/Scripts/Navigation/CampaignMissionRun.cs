using System;
using System.Collections.Generic;

// One uninterrupted observation. A missing species resets its continuous timer.
public sealed class CampaignMissionRun
{
    public readonly int Stage;
    public readonly List<Goal> Goals = new List<Goal>();
    public sealed class Goal
    {
        public string Id;
        public string[] Species;
        public double Target;
        public double Value;
        public bool Eligible = true;
        public bool Achieved;
        public int Kind; // 0 continuous survival, 1 kills, 2 population, 3 requested sections
    }
    public CampaignMissionRun(int stage, bool cycle60, bool herbivore50, bool herbivore60)
    {
        Stage = stage;
        switch (stage)
        {
            case 0: Add(300,"grass"); Add(600,"herbivore"); Add(900,"grass"); Add(900,"grass", eligible:cycle60); break;
            case 1: Add(450,"herbivore"); Add(600,"carnivore"); Add(900,"carnivore"); Add(900,"carnivore", eligible:herbivore50); break;
            case 2: Add(450,"herbivore,carnivore"); Add(600,"herbivore,carnivore"); Add(900,"carnivore",eligible:herbivore60); Add(900,"carnivore",eligible:cycle60); break;
            case 3: Add(450,"dog"); Add(450,"dog,carnivore"); Add(900,"dog,carnivore"); break;
            case 4: Add(450,"herbivore,dog"); Add(900,"herbivore,dog"); Add(900,"herbivore,dog",eligible:cycle60); break;
            case 5: Add(1,"snake",1); Add(5,"snake",1); Add(450,"snake"); break;
            case 6: Add(450,"herbivore"); Add(5,"battleship-frigate",1); Add(450,"battleship-destroyer"); break;
            case 7: Add(5,"battleship-destroyer",2); Add(10,"battleship-destroyer",2); Add(900,"herbivore,battleship-frigate"); break;
            case 8: Add(450,"battleship-battleship"); Add(48,"battleship-battleship",3); Add(50,"battleship-battleship",3); Add(55,"battleship-battleship",3); break;
        }
    }
    private void Add(double target,string species,int kind=0,bool eligible=true) => Goals.Add(new Goal {
        Id=Stage+"-"+(Goals.Count+1),Target=target,Species=species.Split(','),Kind=kind,Eligible=eligible });
    public void Tick(double seconds, Func<string,int> count, Func<string,int> kills, int maximumSections)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        foreach (var goal in Goals)
        {
            if (goal.Achieved || !goal.Eligible) continue;
            switch (goal.Kind)
            {
                case 0: goal.Value = Array.TrueForAll(goal.Species, s => count(s)>0) ? goal.Value+seconds : 0; break;
                case 1: goal.Value = kills(goal.Species[0]); break;
                case 2: goal.Value = Math.Max(goal.Value,count(goal.Species[0])); break;
                case 3: goal.Value = Math.Max(goal.Value,maximumSections); break;
            }
            goal.Achieved = goal.Value >= goal.Target;
        }
    }
}
