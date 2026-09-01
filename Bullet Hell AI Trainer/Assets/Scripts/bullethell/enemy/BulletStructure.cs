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
        float childSpawnFirstDelaySeconds = 0f,
        int splitProjectileCount = 0,
        float splitAngleIntervalDegrees = 0f,
        BulletSplitAimType splitAimType = BulletSplitAimType.Forward,
        BulletStructure childStructure = null,
        float splitWarningDurationSeconds = 0f,
        float totalTurnAngleDegrees = float.PositiveInfinity,
        float linearAcceleration = 0f,
        float linearAccelerationDurationSeconds = 0f,
        float angularAccelerationDegreesPerSecondSquared = 0f,
        float angularAccelerationDurationSeconds = 0f,
        float guidanceCommandIntervalSeconds = 0f,
        float childSpawnIntervalSeconds = float.PositiveInfinity)
    {
        Speed = UnityEngine.Mathf.Max(0f, speed);
        ThreatLevel = UnityEngine.Mathf.Max(0, threatLevel);
        MotionType = motionType;
        TurnRateDegreesPerSecond = turnRateDegreesPerSecond;
        NavigationConstant = UnityEngine.Mathf.Max(0f, navigationConstant);
        ChildSpawnFirstDelaySeconds = UnityEngine.Mathf.Max(
            0f,
            childSpawnFirstDelaySeconds);
        ChildSpawnIntervalSeconds = float.IsNaN(childSpawnIntervalSeconds)
            ? float.PositiveInfinity
            : UnityEngine.Mathf.Max(0f, childSpawnIntervalSeconds);
        SplitProjectileCount = UnityEngine.Mathf.Max(0, splitProjectileCount);
        SplitAngleIntervalDegrees = splitAngleIntervalDegrees;
        SplitAimType = splitAimType;
        ChildStructure = childStructure;
        SplitWarningDurationSeconds = UnityEngine.Mathf.Max(
            0f,
            splitWarningDurationSeconds);
        TotalTurnAngleDegrees = UnityEngine.Mathf.Max(0f, totalTurnAngleDegrees);
        LinearAcceleration = linearAcceleration;
        LinearAccelerationDurationSeconds = UnityEngine.Mathf.Max(
            0f,
            linearAccelerationDurationSeconds);
        AngularAccelerationDegreesPerSecondSquared =
            angularAccelerationDegreesPerSecondSquared;
        AngularAccelerationDurationSeconds = UnityEngine.Mathf.Max(
            0f,
            angularAccelerationDurationSeconds);
        GuidanceCommandIntervalSeconds = UnityEngine.Mathf.Max(
            0f,
            guidanceCommandIntervalSeconds);
    }

    public float Speed { get; }
    public int ThreatLevel { get; }
    public BulletMotionType MotionType { get; }
    public float TurnRateDegreesPerSecond { get; }
    public float NavigationConstant { get; }
    public float ChildSpawnFirstDelaySeconds { get; }
    public float ChildSpawnIntervalSeconds { get; }
    public int SplitProjectileCount { get; }
    public float SplitAngleIntervalDegrees { get; }
    public BulletSplitAimType SplitAimType { get; }
    public BulletStructure ChildStructure { get; }
    public float SplitWarningDurationSeconds { get; }
    public float TotalTurnAngleDegrees { get; }
    public float LinearAcceleration { get; }
    public float LinearAccelerationDurationSeconds { get; }
    public float AngularAccelerationDegreesPerSecondSquared { get; }
    public float AngularAccelerationDurationSeconds { get; }
    public float GuidanceCommandIntervalSeconds { get; }
    public bool HasSplit => SplitProjectileCount > 0 &&
                            ChildStructure != null;

    public static BulletStructure Straight(float speed, int threatLevel)
    {
        return new BulletStructure(speed, threatLevel);
    }
}
