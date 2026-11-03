using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using Aquarium.Tiling;

public sealed class CampaignPlayProbe : MonoBehaviour
{
 int stage = 4;
 float elapsed;
 float nextLog;
 bool entered;
 string Output=>System.IO.Path.GetFullPath("campaign-tuned.txt");
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
 static void Boot()
 {
  if(!SessionState.GetBool("campaign-probe",false))return;
  var go=new GameObject("Validation Probe");DontDestroyOnLoad(go);go.AddComponent<CampaignPlayProbe>();
 }
 public static void Prepare(int stage)
 {
  var d=CampaignStore.Data;
  d.Set("grass.initialCount",150);d.Set("grass.maxAmount",10);
  d.Set("grass.cycle",stage==0?60:15);
  d.Set("1-2.草食動物.enabled",1);d.Set("1-2.草食動物.count",stage==1?50:stage==2?60:600);
  d.Set("2-2.肉食動物.enabled",stage>=3?1:0);d.Set("2-2.肉食動物.count",60);
  CampaignStore.Save();
 }
 void Update()
 {
  var run=CampaignRuntime.Current;
  if(run==null||run.Run==null||run.stageIndex!=stage)return;
  if(!entered){entered=true;
   if(stage==5) {
    var grass=UnityEngine.Object.FindAnyObjectByType<GrassField>();
    File.AppendAllText(Output,"NO-GRASS disabled="+(!grass.enabled)+"\n");
   }File.AppendAllText(Output,"START stage="+stage+" species="+string.Join(",",SpeciesManager.Active.Select(s=>s.SpeciesId+":"+s.LivingCount))+" ships="+BattleshipAnimalInstance.Active.Count+"\n");}
  Time.captureDeltaTime=.2f;Time.maximumDeltaTime=.2f;Time.timeScale=1;Application.runInBackground=true;
  elapsed+=Time.deltaTime;
  if(elapsed>=nextLog){nextLog+=300;File.AppendAllText(Output,"TICK stage="+stage+" seconds="+elapsed+" counts="+string.Join(",",SpeciesManager.Active.Select(s=>s.SpeciesId+":"+s.LivingCount))+" ships="+BattleshipAnimalInstance.Active.Count+"\n");}
  if(elapsed<905)return;
  File.AppendAllText(Output,"END stage="+stage+"\n"+run.Summary+"\n");
  stage = stage==4?6:stage==8?0:stage==0?9:stage+1;elapsed=0;nextLog=0;entered=false;
  if(stage<9) { Prepare(stage); SceneManager.LoadScene("battle"+stage); }
  else {CampaignStore.Save();File.AppendAllText(Output,"SAVE "+CampaignStore.SavePath+"\nDONE\n");SessionState.SetBool("campaign-probe",false);EditorApplication.Exit(0);}
 }
}
