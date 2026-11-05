using System;
using System.Collections.Generic;

// Serializable progress and settings. Rewards are derived from unique mission IDs.
[Serializable]
public sealed class CampaignProgress
{
    public int version = 1;
    public List<string> completed = new List<string>();
    public List<SettingValue> settings = new List<SettingValue>();
    public int maximumSections;
    [Serializable] public sealed class SettingValue { public string key; public float value; }
    public bool Has(string id) => completed.Contains(id);
    public bool Complete(string id)
    {
        if (!Array.Exists(SettingMissionCatalog.All, m => m.Id == id) || Has(id)) return false;
        completed.Add(id); return true;
    }
    public bool StageUnlocked(int stage) => stage == 0 || stage > 0 && stage <= 9 && Has((stage - 1) + "-1");
    public int SpeedRewards => completed.FindAll(id => Array.Exists(SettingMissionCatalog.All,
        m => m.Id == id && IsSpeedReward(m))).Count;
    public static bool IsSpeedReward(SettingMissionCatalog.Mission m) => m.Number == 3 || m.Id == "3-2" || m.Id == "5-2";
    public float Get(string key, float fallback)
    {
        var entry = settings.Find(s => s.key == key);
        return entry == null || float.IsNaN(entry.value) || float.IsInfinity(entry.value) ? fallback : entry.value;
    }
    public void Set(string key, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        if (key != null && key.StartsWith("maintenance.")) value = MaintenanceValue(MaintenanceStep(value));
        var entry = settings.Find(s => s.key == key);
        if (entry == null) { entry = new SettingValue { key = key }; settings.Add(entry); }
        entry.value = value;
    }
    // Stable species IDs keep settings independent of localized UI labels.
    public const int MaintenanceMaximumStep = 15;
    // Integer positions 0..15 represent exponents -5..0 in exact thirds.
    public static float MaintenanceValue(int step) => (float)Math.Pow(10, -5 + Math.Max(0, Math.Min(15, step)) / 3.0);
    public static int MaintenanceStep(double value)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        if (value >= 1) return MaintenanceMaximumStep;
        return Math.Max(0, Math.Min(15, (int)Math.Round((Math.Log10(Math.Max(1e-5, value)) + 5) * 3)));
    }
    public static string MaintenanceKey(string speciesId) => "maintenance." + speciesId;
    public double GetMaintenance(string speciesId, double fallback)
    {
        string key = MaintenanceKey(speciesId);
        return CanEdit(key) ? MaintenanceValue(MaintenanceStep(Get(key, (float)fallback))) : fallback;
    }
    public bool SetMaintenance(string speciesId, float value)
    {
        string key = MaintenanceKey(speciesId);
        if (!CanEdit(key) || float.IsNaN(value) || float.IsInfinity(value)) return false;
        Set(key, value);
        return true;
    }
    public static string RequiredReward(string key)
    {
        if (key == MaintenanceKey("snake")) return "5-2";
        if (key == "grass.initialCount" || key == "grass.maxAmount") return "0-2";
        if (key.StartsWith("grass.birthNeighbor")) return "0-4";
        if (key == "grass.cycle") return "1-4";
        if (key == "dayNight") return "2-4";
        if (key == "ship.sections") return "8-4";
        return key.Length > 3 ? key.Substring(0, 3) : "";
    }
    public bool CanEdit(string key) => key != null && Has(RequiredReward(key));
}
