using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Aquarium.Tiling;

// Awake precedes grass initialization and all population managers.
[DefaultExecutionOrder(-10000)]
public sealed class CampaignRuntime : MonoBehaviour
{
    public int stageIndex;
    public static CampaignRuntime Current { get; private set; }
    public static int SelectedStage;
    public static string LastSummary;
    public static string LastEcosystemLog { get; private set; }
    public CampaignMissionRun Run { get; private set; }
    private readonly Dictionary<AquariumStartupSettings,AquariumStartupSettings> snapshots = new Dictionary<AquariumStartupSettings,AquariumStartupSettings>();
    private readonly List<Object> owned = new List<Object>();
    private readonly Dictionary<string,int> kills = new Dictionary<string,int>();
    private readonly List<string> newRewards = new List<string>();
    private readonly HashSet<string> observedGoals = new HashSet<string>();
    private int runMaximum;
    private GrassField grass;
    private bool ended;
    private int lastEvaluatedFrame = -1;
    private CampaignProgress startSettings;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Current=null; SelectedStage=0; LastSummary=null; LastEcosystemLog=null; }
    private void Awake() { LastEcosystemLog=null; Current=this; SelectedStage=stageIndex; startSettings=JsonUtility.FromJson<CampaignProgress>(JsonUtility.ToJson(CampaignStore.Data));
        BattleSimulationSpeed.UserSpeedAllowed = speed => {
            int index = SettingMissionCatalog.TimeScaleUnlockOrder.IndexOf(speed);
            return index >= 0 && index <= CampaignStore.Data.SpeedRewards;
        };
    }
    public static AquariumStartupSettings Resolve(AquariumStartupSettings source)
    {
        if (Current==null || source==null) return source;
        if (Current.snapshots.TryGetValue(source,out var existing)) return existing;
        var copy=Instantiate(source); Current.owned.Add(copy);
        // Unity clones serialized nested configuration; species assets need explicit clones.
        foreach (var layer in copy.layers)
        foreach (var entry in layer.species)
        {
            entry.species=Instantiate(entry.species); Current.owned.Add(entry.species);
            entry.species.traits=entry.species.traits.Copy();
            ApplyTraits(entry.species.speciesId,entry.species.traits);
            // Stage-specific initial populations; shared species assets stay unchanged.
            string id = entry.species.speciesId;
            if (Current.stageIndex == 4)
            {
                if (id == "herbivore") entry.initialCount = 150;
                else if (id == "dog") entry.initialCount = 10;
                else if (id == "yoshi" || id == "snake" || id == "beetle") entry.initialCount = 1;
            }
            if (Current.stageIndex >= 6)
            {
                if (id == "herbivore") entry.initialCount = 150;
                else if (id == "carnivore") entry.initialCount = 30;
            }
        }
        var g=copy.grass;
        g.initialSpawnCount=Read("grass.initialCount",20,20,150);
        g.maxGrass=Read("grass.maxAmount",2,2,10);
        g.updateInterval=Read("grass.cycle",15,15,60);
        g.birthNeighborMinimum=Read("grass.birthNeighborMinimum",1,1,3);
        g.birthNeighborMaximum=Read("grass.birthNeighborMaximum",1,g.birthNeighborMinimum,3);
        if (Settings.Has("2-4")) copy.dayNightPeriodSeconds=new[]{30,60,90}[Read("dayNight",0,0,2)];
        if (Current.stageIndex==2) g.deathAmount=-5;
        if (Current.stageIndex==5) { g.initialSpawnCount=0; g.growthEnabled=false; copy.permanentNight=true; }
        Current.snapshots.Add(source,copy);
        return copy;
    }
    private static CampaignProgress Settings => Current != null ? Current.startSettings : CampaignStore.Data;
    public static int Read(string key,int fallback,int min,int max) => Mathf.Clamp(Mathf.RoundToInt(
        Settings.CanEdit(key) ? Settings.Get(key,fallback) : fallback),min,max);
    public static string PopulationKey(string id)
    {
        switch(id)
        {
            case "herbivore": return "1-2.草食動物";
            case "carnivore": return "2-2.肉食動物";
            case "dog": return "4-2.犬型";
            case "yoshi": return "4-2.ヨッシー型";
            case "snake": return "4-2.蛇型";
            case "beetle": return "4-2.甲虫型";
            case "battleship-frigate": return "6-2.フリゲート艦型";
            case "battleship-destroyer": return "7-2.駆逐艦型";
            case "battleship-cruiser": return "8-2.巡洋艦型";
            case "battleship-battleship": return "8-2.戦艦型";
            default: return "";
        }
    }
    public static void ApplyTraits(string id,AnimalTraits traits)
    {
        traits.maintenancePerSecond = Settings.GetMaintenance(id, traits.maintenancePerSecond);
        var key=PopulationKey(id);
        traits.populationScalingEnabled=Read(key+".enabled",0,0,1)!=0;
        int min=id=="herbivore"?50:id=="carnivore"?20:0;
        int max=id=="herbivore"?600:id=="carnivore"?60:id=="battleship-destroyer"?10:id=="battleship-cruiser"?5:id=="battleship-battleship"?3:20;
        traits.reproductionPopulationBaseline=Read(key+".count",min,min,max);
    }
    public static double ShipProbability(BattleshipAnimalClass rank,int count,double legacy)
    {
        if (Current==null) return legacy;
        var traits=new AnimalTraits(); ApplyTraits("battleship-"+rank.ToString().ToLowerInvariant(),traits);
        return traits.BirthSurvivalProbability(count);
    }
    private void Start()
    {
        grass=FindAnyObjectByType<GrassField>();
        string h=PopulationKey("herbivore");
        bool herbEnabled=Read(h+".enabled",0,0,1)==1;
        Run=new CampaignMissionRun(stageIndex, CampaignStore.Data.Has("1-4") && Read("grass.cycle",15,15,60)==60,
            herbEnabled && Read(h+".count",50,50,600)==50,herbEnabled && Read(h+".count",50,50,600)==60);
    }
    public static void RecordKill(string id)
    {
        if (Current==null || Current.ended) return;
        Current.kills[id]=Current.Kills(id)+1;
    }
    public static void RecordShip(BattleshipAnimalClass rank,int requested)
    {
        if (Current==null || rank!=BattleshipAnimalClass.Battleship) return;
        Current.runMaximum=Mathf.Max(Current.runMaximum,requested);
        if (requested<=CampaignStore.Data.maximumSections) return;
        CampaignStore.Data.maximumSections=requested; CampaignStore.Save();
    }
    private int Kills(string id) => kills.TryGetValue(id,out int value)?value:0;
    private int Count(string id)
    {
        if (id=="grass")
        {
            if (grass==null || grass.Grid==null) return 0;
            for(int y=0;y<grass.Grid.Height;y++) for(int x=0;x<grass.Grid.Width;x++) if(grass.Grid[x,y]>0) return 1;
            return 0;
        }
        if (id.StartsWith("battleship-")) return BattleshipAnimalInstance.Active.Count(s=>s!=null && s.IsAlive && "battleship-"+s.ShipClass.ToString().ToLowerInvariant()==id);
        return AnimalBody.LivingSpeciesCount(id);
    }
    // LateUpdate runs after simulation Updates, even while the time scale is zero.
    private void LateUpdate()
    {
        if (Run==null || ended || lastEvaluatedFrame == Time.frameCount) return;
        lastEvaluatedFrame = Time.frameCount;
        Run.Tick(Time.deltaTime,Count,Kills,runMaximum);
        bool changed=false;
        foreach(var goal in Run.Goals)
            if(goal.Achieved && observedGoals.Add(goal.Id) && CampaignStore.Data.Complete(goal.Id))
            { newRewards.Add(goal.Id); changed=true; }
        if(changed) CampaignStore.Save();
    }
    public string Summary
    {
        get
        {
            if(Run==null) return "準備中";
            var text=new StringBuilder("ステージ "+stageIndex);
            foreach(var goal in Run.Goals)
            {
                var mission=System.Array.Find(SettingMissionCatalog.All,m=>m.Id==goal.Id);
                text.Append("\n"+goal.Id+" "+(goal.Achieved?"達成":!goal.Eligible?"開始時の設定条件が不一致":$"{goal.Value:0}/{goal.Target:0}")+"  "+mission.Condition);
            }
            if(CampaignStore.SaveError!=null) text.Append("\n"+CampaignStore.SaveError);
            return text.ToString();
        }
    }
    public void Finish()
    {
        if(ended) return;
        LateUpdate(); ended=true;
        var observation = FindAnyObjectByType<BattleViewControls>();
        LastEcosystemLog = observation != null ? observation.eventlog : null;
        LastSummary=Summary+"\n\n今回獲得した解放";
        if(newRewards.Count==0) LastSummary+="\nなし（取得済み報酬は重複しません）";
        foreach(var id in newRewards) LastSummary+="\n"+id+"  "+System.Array.Find(SettingMissionCatalog.All,m=>m.Id==id).Reward;
        CampaignStore.Save(); Time.timeScale=1;
        UnityEngine.SceneManagement.SceneManager.LoadScene("debriefing0");
    }
    private void OnDestroy()
    {
        if(Current==this) { Current=null; BattleSimulationSpeed.UserSpeedAllowed=null; }
        foreach(var asset in owned) if(asset!=null) Destroy(asset);
    }
}
