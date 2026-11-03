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
  Console.WriteLine($"PASS: {checks} assertions");
 }
}
