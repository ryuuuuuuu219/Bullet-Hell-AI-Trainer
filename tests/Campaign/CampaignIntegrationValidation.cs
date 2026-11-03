using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CampaignIntegrationValidation
{
 static int phase;
 static double deadline;
 static string PathName=>Path.GetFullPath("campaign-integration.txt");
 static CampaignIntegrationValidation(){EditorApplication.update+=Update;}
 public static void Run()
 {
  PlayerSettings.companyName="CodexValidation";PlayerSettings.productName="AquariumCampaignIsolated";
  File.WriteAllText(PathName,"Unity campaign integration\n");
  SessionState.SetBool("campaign-integration",true);
  EditorSceneManager.OpenScene("Assets/Scenes/main menu.unity");EditorApplication.EnterPlaymode();
 }
 static void Check(bool condition,string label)
 {
  File.AppendAllText(PathName,(condition?"PASS ":"FAIL ")+label+"\n");
  if(!condition)throw new Exception(label);
 }
 static void Click(string caption)
 {
  var button=UnityEngine.Object.FindObjectsByType<Button>().First(b=>b.GetComponentInChildren<Text>().text==caption);
  Check(button.interactable,"button enabled: "+caption);button.onClick.Invoke();
 }
 static void Update()
 {
  if(!SessionState.GetBool("campaign-integration",false)||!EditorApplication.isPlaying)return;
  EditorApplication.isPaused=false; EditorApplication.QueuePlayerLoopUpdate(); Application.runInBackground=true; if(deadline==0)deadline=EditorApplication.timeSinceStartup+90;
  try
  {
   if(EditorApplication.timeSinceStartup>deadline)throw new Exception("timeout phase "+phase);
   string scene=SceneManager.GetActiveScene().name;
   switch(phase)
   {
    case 0:
     typeof(CampaignStore).GetField("data",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new CampaignProgress());
     CampaignStore.Save();phase++;SceneManager.LoadScene("main menu");break;
    case 1:
     if(scene!="main menu"||UnityEngine.Object.FindObjectsByType<Button>().Length<10)return;
     var locked=UnityEngine.Object.FindObjectsByType<Button>().First(b=>b.GetComponentInChildren<Text>().text.Contains("ステージ 1"));
     Check(!locked.interactable,"stage1 locked on fresh profile");
     phase++;Click("ステージ 0  0目標達成");break;
    case 2:
     if(scene!="nobel0"||!UnityEngine.Object.FindObjectsByType<Text>().Any(t=>t.text=="観察を開始"))return;
     phase++;Click("観察を開始");break;
    case 3:
     if(scene!="battle0"||CampaignRuntime.Current?.Run==null)return;
     Check(SpeciesManager.Active.Count==1&&SpeciesManager.Active[0].SpeciesId=="herbivore","stage0 enabled species");
     Check(CampaignRuntime.Read("grass.cycle",15,15,60)==15,"locked cycle uses default");
     CampaignStore.Data.Complete("1-4");CampaignStore.Data.Set("grass.cycle",60);
     Check(CampaignRuntime.Read("grass.cycle",15,15,60)==15,"unlock does not change observation snapshot");
     var asset=AssetDatabase.LoadAssetAtPath<AquariumStartupSettings>("Assets/Settings/AquariumStartupSettings.asset");
     Check(asset.grass.initialSpawnCount==150&&asset.grass.maxGrass==3,"shared startup asset unchanged");
     CampaignRuntime.Current.Run.Tick(900,id=>100,id=>0,0);
     phase++;break;
    case 4:
     if(!CampaignStore.Data.Has("0-1"))return;
     Check(CampaignStore.Data.StageUnlocked(1),"first mission unlocks stage1");
     Check(!CampaignStore.Data.Has("0-4"),"locked prerequisite cannot earn conditional goal");
     CampaignRuntime.Current.Finish();phase++;break;
    case 5:
     if(scene!="debriefing0"||!UnityEngine.Object.FindObjectsByType<Text>().Any(t=>t.text=="再挑戦"))return;
     Check(CampaignRuntime.LastSummary.Contains("0-1")&&CampaignRuntime.LastSummary.Contains("今回獲得"),"debrief results");
     phase++;Click("再挑戦");break;
    case 6:
     if(scene!="battle0"||CampaignRuntime.Current?.Run==null)return;
     Check(CampaignRuntime.Read("grass.cycle",15,15,60)==60,"new observation applies saved selection");
     CampaignRuntime.Current.Run.Tick(900,id=>100,id=>0,0);phase++;break;
    case 7:
     if(!CampaignStore.Data.Has("0-4"))return;
     Check(CampaignStore.Data.SpeedRewards==1,"replay does not duplicate speed rewards");
     CampaignRuntime.Current.Finish();phase++;break;
    case 8:
     if(scene!="debriefing0"||!UnityEngine.Object.FindObjectsByType<Text>().Any(t=>t.text=="メニュー"))return;
     Check(!CampaignRuntime.LastSummary.Substring(CampaignRuntime.LastSummary.IndexOf("今回獲得")).Contains("0-1"),"replay excludes old rewards");
     phase++;Click("メニュー");break;
    case 9:
     if(scene!="main menu"||!UnityEngine.Object.FindObjectsByType<Text>().Any(t=>t.text=="生態系の設定"))return;
     CampaignStore.Data.Complete("8-4");CampaignStore.Data.maximumSections=57;
     phase++;Click("生態系の設定");break;
    case 10:
     if(scene!="setting"||UnityEngine.Object.FindObjectsByType<Slider>().Length==0)return;
     Check(UnityEngine.Object.FindObjectsByType<Slider>().Any(s=>s.interactable && s.minValue==40 && s.maxValue==57),"recorded section limit applied to slider");
     Check(UnityEngine.Object.FindObjectsByType<Slider>().Any(s=>s.interactable),"unlocked setting enabled");
     Check(UnityEngine.Object.FindObjectsByType<Slider>().Any(s=>!s.interactable),"locked setting disabled");
     CampaignStore.Set("grass.cycle",30);CampaignStore.Data.maximumSections=57;CampaignStore.Save();
     typeof(CampaignStore).GetField("data",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
     Check(CampaignStore.Data.Get("grass.cycle",15)==30&&CampaignStore.Data.maximumSections==57&&CampaignStore.Data.Has("0-1"),"disk reload restores settings records and missions");
     Check(File.Exists(CampaignStore.SavePath+".bak"),"backup exists");
     Check(CampaignStore.SaveError==null,"no save error");
     CampaignStore.Data.Complete("8-1");CampaignStore.Save();phase++;SceneManager.LoadScene("main menu");break;
    case 11:
     if(scene!="main menu"||!UnityEngine.Object.FindObjectsByType<Text>().Any(t=>t.text=="ステージ9のノベル入口"))return;
     phase++;Click("ステージ9のノベル入口");break;
    case 12:
     if(scene!="nobel0"||!UnityEngine.Object.FindObjectsByType<Text>().Any(t=>t.text.Contains("ステージ 9")))return;
     Check(!UnityEngine.Object.FindObjectsByType<Button>().First(b=>b.GetComponentInChildren<Text>().text=="観察を開始").interactable,"undefined stage9 battle cannot start");
     File.AppendAllText(PathName,"DONE\n");SessionState.SetBool("campaign-integration",false);EditorApplication.Exit(0);break;
   }
  }
  catch(Exception e){File.AppendAllText(PathName,"ERROR "+e+"\n");SessionState.SetBool("campaign-integration",false);EditorApplication.Exit(1);}
 }
}

