public enum BulletMotionType
{
    Straight,
    ConstantTurn,
    Homing,
    ProportionalNavigation,
}

public enum BulletSplitAimType
{
    Forward,
    PlayerAimed,
}

public sealed class BulletStructure
{
    public BulletStructure(
        float speed,
        int threatLevel,
        BulletMotionType motionType = BulletMotionType.Straight,
        float turnRateDegreesPerSecond = 0f,
        float navigationConstant = 0f,
        float splitDelaySeconds = 0f,
        int splitProjectileCount = 0,
        float splitAngleIntervalDegrees = 0f,
        BulletSplitAimType splitAimType = BulletSplitAimType.Forward,
        BulletStructure childStructure = null,
        float splitWarningDurationSeconds = 0f,
        float totalTurnAngleDegrees = float.PositiveInfinity)
    {
        Speed = UnityEngine.Mathf.Max(0f, speed);
        ThreatLevel = UnityEngine.Mathf.Max(0, threatLevel);
        MotionType = motionType;
        TurnRateDegreesPerSecond = turnRateDegreesPerSecond;
        NavigationConstant = UnityEngine.Mathf.Max(0f, navigationConstant);
        SplitDelaySeconds = UnityEngine.Mathf.Max(0f, splitDelaySeconds);
        SplitProjectileCount = UnityEngine.Mathf.Max(0, splitProjectileCount);
        SplitAngleIntervalDegrees = splitAngleIntervalDegrees;
        SplitAimType = splitAimType;
        ChildStructure = childStructure;
        SplitWarningDurationSeconds = UnityEngine.Mathf.Max(
            0f,
            splitWarningDurationSeconds);
        TotalTurnAngleDegrees = UnityEngine.Mathf.Max(0f, totalTurnAngleDegrees);
    }

    public float Speed { get; }
    public int ThreatLevel { get; }
    public BulletMotionType MotionType { get; }
    public float TurnRateDegreesPerSecond { get; }
    public float NavigationConstant { get; }
    public float SplitDelaySeconds { get; }
    public int SplitProjectileCount { get; }
    public float SplitAngleIntervalDegrees { get; }
    public BulletSplitAimType SplitAimType { get; }
    public BulletStructure ChildStructure { get; }
    public float SplitWarningDurationSeconds { get; }
    public float TotalTurnAngleDegrees { get; }
    public bool HasSplit => SplitDelaySeconds > 0f &&
                            SplitProjectileCount > 0 &&
                            ChildStructure != null;

    public static BulletStructure Straight(float speed, int threatLevel)
    {
        return new BulletStructure(speed, threatLevel);
    }
}
