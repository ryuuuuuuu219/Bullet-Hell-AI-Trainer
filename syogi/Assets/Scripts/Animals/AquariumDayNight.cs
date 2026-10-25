using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class AquariumDayNight : MonoBehaviour
{
    [Min(0.01f)] public float periodSeconds = 120;
    public float phaseDegrees;
    public float LightLevel { get; private set; } = 0.5f;
    public static float CurrentLight => current == null ? 1f : current.LightLevel;
    private static AquariumDayNight current;
    private Camera mainCamera;
    private double elapsed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCurrent() => current = null;
    private void OnEnable() { current = this; Apply(); }
    private void OnDisable() { if (current == this) current = null; }
    private void Update() { elapsed += Time.deltaTime; Apply(); }
    private void Apply()
    {
        double phase = elapsed * System.Math.PI * 2 / Mathf.Max(0.01f, periodSeconds) + phaseDegrees * Mathf.Deg2Rad;
        LightLevel = (float)(System.Math.Sin(phase) * 0.5 + 0.5);
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.Lerp(Color.black, new Color(176f / 255, 109f / 255, 109f / 255), LightLevel);
    }
}
