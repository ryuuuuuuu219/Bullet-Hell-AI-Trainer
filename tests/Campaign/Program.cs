using System;
using System.Linq;
using System.Collections.Generic;

class Program
{
 static int checks;
 static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 static void Main()
 {
  var p=new CampaignProgress();
  Check(p.StageUnlocked(0)&&!p.StageUnlocked(1),"initial stage");
  Check(!p.Complete("9-1"),"no invented stage9 goals");
  foreach(var m in SettingMissionCatalog.All){Check(p.Complete(m.Id),"first reward "+m.Id);Check(!p.Complete(m.Id),"duplicate reward "+m.Id);}
  Check(p.completed.Count==31&&p.SpeedRewards==11,"31 unique rewards, 11 speeds");
  for(int i=0;i<=9;i++)Check(p.StageUnlocked(i),"unlock chain "+i);
  Check(p.CanEdit("grass.cycle")&&p.CanEdit("8-2.戦艦型.count")&&p.CanEdit("ship.sections"),"feature unlock");
  p.Set("grass.cycle",60);p.Set("grass.cycle",30);Check(p.Get("grass.cycle",15)==30&&p.settings.Count==1,"settings update");
  p.Set("grass.cycle",float.NaN);Check(p.Get("grass.cycle",15)==30,"reject NaN");
  for(int s=0;s<9;s++)
  {
   var run=new CampaignMissionRun(s,true,true,true);
   Check(run.Goals.Count==SettingMissionCatalog.All.Count(m=>m.Stage==s),"goal coverage "+s);
   run.Tick(900,id=>10,id=>5,55);
   Check(run.Goals.All(g=>g.Achieved),"goals can trigger "+s);
   run.Tick(1,id=>0,id=>0,0);Check(run.Goals.All(g=>g.Achieved),"latching "+s);
  }
  var and=new CampaignMissionRun(2,false,false,false);
  and.Tick(449,id=>1,id=>0,0);Check(!and.Goals[0].Achieved,"below threshold");
  and.Tick(1,id=>id=="carnivore"?0:1,id=>0,0);Check(and.Goals[0].Value==0,"AND reset");
  and.Tick(450,id=>1,id=>0,0);Check(and.Goals[0].Achieved,"continuous threshold");
  and.Tick(900,id=>1,id=>0,0);Check(!and.Goals[2].Achieved&&!and.Goals[3].Achieved,"start requirements");
  var pop=new CampaignMissionRun(7,false,false,false);pop.Tick(0,id=>5,id=>0,0);Check(pop.Goals[0].Achieved&&!pop.Goals[1].Achieved,"population arrival");
  var sections=new CampaignMissionRun(8,false,false,false);sections.Tick(0,id=>1,id=>0,48);Check(sections.Goals[1].Achieved&&!sections.Goals[2].Achieved,"requested sections boundary");
  var traits=new AnimalTraits{populationScalingEnabled=true,reproductionPopulationBaseline=0};
  Check(traits.BirthSurvivalProbability(1)==0&&!traits.ValidReproduction,"zero baseline");
  traits.populationScalingEnabled=false;Check(traits.BirthSurvivalProbability(10000)==1&&traits.ValidReproduction,"toggle disabled");
  traits.populationScalingEnabled=true;traits.reproductionPopulationBaseline=50;Check(traits.BirthSurvivalProbability(25)==.5,"population formula");
  BattleSimulationSpeed.UserSpeedAllowed = speed => speed == 1;
  BattleSimulationSpeed.SetUserSpeed(1);
  BattleSimulationSpeed.SetUserSpeed(20);
  Check(UnityEngine.Time.timeScale==1,"locked multiplier rejected");
  BattleSimulationSpeed.ReportExtinction("snake");
  Check(UnityEngine.Time.timeScale==.1f,"extinction slowdown remains available");
  BattleSimulationSpeed.SetUserSpeed(20);
  Check(BattleSimulationSpeed.ExtinctionOverride,"locked multiplier cannot clear override");
  BattleSimulationSpeed.SetUserSpeed(1);
  Check(UnityEngine.Time.timeScale==1&&!BattleSimulationSpeed.ExtinctionOverride,"allowed multiplier resumes");
  BattleSimulationSpeed.UserSpeedAllowed=null;
  BattleSimulationSpeed.SetUserSpeed(8);
  Check(UnityEngine.Time.timeScale==8,"experiment speed remains supported");
  var upkeep = new CampaignProgress();
  Check(!upkeep.SetMaintenance("snake", 0), "locked upkeep rejected");
  Check(upkeep.GetMaintenance("snake", .05)==.05, "locked upkeep fallback");
  upkeep.Complete("5-2");
  Check(upkeep.SpeedRewards==1 && upkeep.CanEdit(CampaignProgress.MaintenanceKey("snake")), "dual reward");
  Check(upkeep.SetMaintenance("snake", 0) && Math.Abs(upkeep.GetMaintenance("snake", .05)-1e-5)<1e-10, "legacy zero clamps to logarithmic minimum");
  Check(!upkeep.SetMaintenance("dog", 0) && upkeep.GetMaintenance("dog", .05)==.05, "species isolation");
  upkeep.SetMaintenance("snake", 2); Check(upkeep.GetMaintenance("snake", .05)==1, "upper clamp");
  upkeep.SetMaintenance("snake", -1); Check(Math.Abs(upkeep.GetMaintenance("snake", .05)-1e-5)<1e-10, "lower clamp");
  Check(!upkeep.SetMaintenance("snake", float.NaN), "invalid upkeep rejected");
  foreach(double rate in new[]{0d,.05d,1d}) {
   var resource=new AnimalResources(10); resource.Advance(5,false,rate);
   Check(Math.Abs(resource.Amount-(10-5*rate))<1e-9,"upkeep amount "+rate);
   Check(Math.Abs(resource.Stamina-(50+(10-rate*2.5)*5*.2))<1e-9,"recovery integration "+rate);
   var sleeping=new AnimalResources(10); sleeping.Advance(5,true,rate);
   Check(sleeping.Amount==10,"sleep upkeep "+rate);
  }
  var exhausted=new AnimalResources(10); exhausted.Advance(20,false,1);
  Check(exhausted.Amount==0 && exhausted.Stamina==60,"recovery stops at starvation");
  var legacy=new AnimalResources(10); legacy.Advance(5,false);
  Check(Math.Abs(legacy.Amount-9.75)<1e-9,"legacy default upkeep");
  var configured=new AnimalTraits{maintenancePerSecond=1};
  var child=configured.Copy(); configured.maintenancePerSecond=0;
  var body=new AnimalResources(10); new AnimalActivity().Step(2,1,false,child,body);
  Check(body.Amount==8 && configured.maintenancePerSecond==0,"activity uses copied species upkeep");
  var json=System.Text.Json.JsonSerializer.Serialize(upkeep,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
  var restored=System.Text.Json.JsonSerializer.Deserialize<CampaignProgress>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
  Check(Math.Abs(restored.GetMaintenance("snake",.05)-1e-5)<1e-10 && restored.CanEdit(CampaignProgress.MaintenanceKey("snake")),"upkeep save round trip");
  for(int step=0;step<=15;step++) {
   float value=CampaignProgress.MaintenanceValue(step);
   Check(CampaignProgress.MaintenanceStep(value)==step,"log round trip "+step);
   upkeep.SetMaintenance("snake",value);
   Check(upkeep.GetMaintenance("snake",.05)==value,"log storage "+step);
   if(step<=12) Check(Math.Abs(CampaignProgress.MaintenanceValue(step+3)/value-10)<1e-5,"three steps multiply by ten");
  }
  Check(CampaignProgress.MaintenanceValue(0)==1e-5f && CampaignProgress.MaintenanceValue(15)==1,"log endpoints");
  Check(CampaignProgress.MaintenanceStep(.05)==11,"default snaps to nearest step");
  {
var s4=new CampaignMissionRun(4,true,false,false);
 Check(s4.Goals.Select(g=>g.Target).SequenceEqual(new double[]{150,300,300}),"stage4 specification targets 150/300/300");
 s4.Tick(150,id=>1,id=>0,0);Check(s4.Goals[0].Achieved,"4-1 completes at 150s");
 s4.Tick(150,id=>1,id=>0,0);Check(s4.Goals[1].Achieved,"4-2 completes at 300s");Check(s4.Goals[2].Achieved,"4-3 completes at 300s with cycle60");
 var reset=new CampaignMissionRun(4,true,false,false);reset.Tick(100,id=>1,id=>0,0);reset.Tick(1,id=>id=="dog"?0:1,id=>0,0);
 Check(reset.Goals.All(g=>g.Value==0),"4 simultaneous survival resets on dog extinction");
 var locked=new CampaignMissionRun(4,false,false,false);locked.Tick(1000,id=>1,id=>0,0);Check(!locked.Goals[2].Achieved,"4-3 requires cycle60");
 var s5=new CampaignMissionRun(5,false,false,false);
 s5.Tick(449,id=>1,id=>0,0);Check(!s5.Goals.Any(g=>g.Achieved),"5 no kill and 449s below all thresholds");
 s5.Tick(0,id=>1,id=>id=="snake"?1:0,0);Check(s5.Goals[0].Achieved&&!s5.Goals[1].Achieved,"5-1 one snake kill");
 s5.Tick(0,id=>1,id=>4,0);Check(!s5.Goals[1].Achieved,"5-2 four kills insufficient");
 s5.Tick(1,id=>1,id=>5,0);Check(s5.Goals.All(g=>g.Achieved),"5 five kills and 450s complete all");
 s5.Tick(1,id=>0,id=>0,0);Check(s5.Goals.All(g=>g.Achieved),"5 completed goals remain complete");
 var interrupted=new CampaignMissionRun(5,false,false,false);interrupted.Tick(449,id=>1,id=>0,0);interrupted.Tick(1,id=>0,id=>0,0);Check(interrupted.Goals[2].Value==0,"5-3 resets at extinction");
 var stageProgress=new CampaignProgress();stageProgress.Complete("4-1");Check(stageProgress.StageUnlocked(5),"4-1 unlocks stage5");
 stageProgress.Complete("4-2");Check(new[]{"犬型","ヨッシー型","蛇型","甲虫型"}.All(id=>stageProgress.CanEdit("4-2."+id+".count")),"4-2 four species controls");
 Check(!stageProgress.CanEdit(CampaignProgress.MaintenanceKey("snake")),"upkeep locked before5-2");stageProgress.Complete("5-1");Check(stageProgress.StageUnlocked(6),"5-1 unlocks stage6");
 stageProgress.Complete("5-2");Check(stageProgress.CanEdit(CampaignProgress.MaintenanceKey("snake"))&&stageProgress.SpeedRewards==1,"5-2 upkeep and speed rewards");
 Check(!stageProgress.Complete("5-2")&&stageProgress.SpeedRewards==1,"5-2 no duplicate reward");
 foreach(int step in new[]{0,15}){stageProgress.SetMaintenance("snake",CampaignProgress.MaintenanceValue(step));var resource=new AnimalResources(10);resource.Advance(5,false,stageProgress.GetMaintenance("snake",.05));Check(Math.Abs(resource.Amount-(10-5d*CampaignProgress.MaintenanceValue(step)))<1e-7,"upkeep endpoint applies "+step);}

  }
  Console.WriteLine($"PASS: {checks} assertions");
 }
}
