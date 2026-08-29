public enum BulletMotionType
{
    Straight,
    ConstantTurn,
    Homing,
    ProportionalNavigation,
}

public sealed class BulletStructure
{
    public BulletStructure(
        float speed,
        int threatLevel,
        BulletMotionType motionType = BulletMotionType.Straight,
        float turnRateDegreesPerSecond = 0f,
        float navigationConstant = 0f)
    {
        Speed = UnityEngine.Mathf.Max(0f, speed);
        ThreatLevel = UnityEngine.Mathf.Max(0, threatLevel);
        MotionType = motionType;
        TurnRateDegreesPerSecond = turnRateDegreesPerSecond;
        NavigationConstant = UnityEngine.Mathf.Max(0f, navigationConstant);
    }

    public float Speed { get; }
    public int ThreatLevel { get; }
    public BulletMotionType MotionType { get; }
    public float TurnRateDegreesPerSecond { get; }
    public float NavigationConstant { get; }

    public static BulletStructure Straight(float speed, int threatLevel)
    {
        return new BulletStructure(speed, threatLevel);
    }
}
