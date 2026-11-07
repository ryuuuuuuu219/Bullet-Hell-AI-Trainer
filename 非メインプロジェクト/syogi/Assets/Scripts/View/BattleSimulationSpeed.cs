using UnityEngine;

public static class BattleSimulationSpeed
{
    public static bool ExtinctionOverride { get; private set; }
    public static string ExtinctSpeciesId { get; private set; }
    public static event System.Action<string> Extinction;
    public static System.Func<float, bool> UserSpeedAllowed;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        Extinction = null;
        UserSpeedAllowed = null;
        SetUserSpeed(1);
    }
    public static void SetUserSpeed(float speed)
    {
        if (UserSpeedAllowed != null && !UserSpeedAllowed(speed)) return;
        ExtinctionOverride = false;
        ExtinctSpeciesId = null;
        Time.timeScale = speed;
    }
    public static void ReportExtinction(string speciesId)
    {
        ExtinctionOverride = true;
        ExtinctSpeciesId = speciesId;
        Time.timeScale = 0.1f;
        Extinction?.Invoke(speciesId);
    }
}
