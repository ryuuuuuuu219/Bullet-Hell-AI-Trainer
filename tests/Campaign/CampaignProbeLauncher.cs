using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class CampaignProbeLauncher
{
 static CampaignProbeLauncher(){EditorApplication.update+=()=>{if(SessionState.GetBool("campaign-probe",false)&&EditorApplication.isPlaying){EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();}};}
 public static void Run()
 {
  PlayerSettings.companyName="CodexValidation";PlayerSettings.productName="AquariumCampaignIsolated";
  File.WriteAllText(Path.GetFullPath("campaign-tuned.txt"),"Runtime probe, configured rewards, delta 0.2 seconds\n");
  SessionState.SetBool("campaign-validation",false);SessionState.SetBool("campaign-probe",true);
  CampaignPlayProbe.Prepare(4);
  EditorSettings.enterPlayModeOptionsEnabled=false;
  EditorSceneManager.OpenScene("Assets/Scenes/battle4.unity");EditorApplication.EnterPlaymode();
 }
}
