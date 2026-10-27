// Only configuration and lifecycle policy are tested here, not Unity callbacks or serialization.
namespace UnityEngine
{
    public class ScriptableObject { }
    public class MonoBehaviour { }
    public class GameObject { }
    public struct Color { public static Color gray => new Color(); }
    public sealed class CreateAssetMenuAttribute : System.Attribute { public string fileName; public string menuName; }
    public sealed class SerializeField : System.Attribute { }
    public sealed class DisallowMultipleComponent : System.Attribute { }
    public sealed class MinAttribute : System.Attribute { public MinAttribute(float value) { } }
    public sealed class RangeAttribute : System.Attribute { public RangeAttribute(float a, float b) { } }
    public sealed class HeaderAttribute : System.Attribute { public HeaderAttribute(string value) { } }
    public sealed class TooltipAttribute : System.Attribute { public TooltipAttribute(string value) { } }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : System.Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public static class Time { public static float timeScale = 1; }
}
public sealed class AnimalBody { public bool IsAlive = true; }
