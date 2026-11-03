using System;
using System.IO;
using UnityEngine;

public static class CampaignStore
{
    private static CampaignProgress data;
    public static string SaveError { get; private set; }
    public static CampaignProgress Data => data ?? (data = Load());
    public static string SavePath => Path.Combine(Application.persistentDataPath, "aquarium-progress-v1.json");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { data = null; SaveError = null; }
    private static CampaignProgress Load()
    {
        foreach (string path in new[] { SavePath, SavePath + ".bak" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var result = JsonUtility.FromJson<CampaignProgress>(File.ReadAllText(path));
                if (result == null || result.version != 1 || result.completed == null || result.settings == null)
                    throw new InvalidDataException("Invalid progress format");
                result.completed = result.completed.FindAll(id => Array.Exists(SettingMissionCatalog.All, m => m.Id == id));
                result.completed = new System.Collections.Generic.List<string>(new System.Collections.Generic.HashSet<string>(result.completed));
                result.settings.RemoveAll(v => v == null || v.key == null);
                result.maximumSections = Mathf.Clamp(result.maximumSections, 0, 128);
                return result;
            }
            catch (Exception e) { SaveError = "保存データの読込失敗: " + e.Message; Debug.LogWarning(SaveError); }
        }
        return new CampaignProgress();
    }
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temp = SavePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(Data, true));
            if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".bak");
            else File.Move(temp, SavePath);
            SaveError = null;
        }
        catch (Exception e) { SaveError = "保存に失敗しました: " + e.Message; Debug.LogError(SaveError); }
    }
    public static void Set(string key,float value)
    {
        if (!Data.CanEdit(key) || CampaignRuntime.Current != null) return;
        Data.Set(key,value); Save();
    }
}
