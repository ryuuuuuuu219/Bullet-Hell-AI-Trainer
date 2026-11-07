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
        float childSpawnIntervalSeconds = float.PositiveInfinity,
        float[] splitAngleOffsetsDegrees = null,
        LaserStructure childLaserStructure = null,
        int maximumChildSpawnEvents = 0,
        BulletStructure[] splitProjectileStructures = null,
        bool childFlightWarningEnabled = true,
        float linearJerk = 0f,
        float linearJerkDurationSeconds = 0f,
        float angularJerkDegreesPerSecondCubed = 0f,
        float angularJerkDurationSeconds = 0f,
        bool releaseParentAfterFinalChildSpawn = true,
        float cameraReleaseDistanceMultiplier =
            ProjectilePool.DefaultReleaseDistanceMultiplier,
        bool useTimeTimeForMotion = false)
    {
        Speed = speed;
        UseTimeTimeForMotion = useTimeTimeForMotion;
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
        LinearJerk = linearJerk;
        LinearJerkDurationSeconds = UnityEngine.Mathf.Max(
            0f,
            linearJerkDurationSeconds);
        AngularAccelerationDegreesPerSecondSquared =
            angularAccelerationDegreesPerSecondSquared;
        AngularAccelerationDurationSeconds = UnityEngine.Mathf.Max(
            0f,
            angularAccelerationDurationSeconds);
        AngularJerkDegreesPerSecondCubed =
            angularJerkDegreesPerSecondCubed;
        AngularJerkDurationSeconds = UnityEngine.Mathf.Max(
            0f,
            angularJerkDurationSeconds);
        ReleaseParentAfterFinalChildSpawn =
            releaseParentAfterFinalChildSpawn;
        GuidanceCommandIntervalSeconds = UnityEngine.Mathf.Max(
            0f,
            guidanceCommandIntervalSeconds);
        SplitAngleOffsetsDegrees = splitAngleOffsetsDegrees ??
            System.Array.Empty<float>();
        ChildLaserStructure = childLaserStructure;
        MaximumChildSpawnEvents = UnityEngine.Mathf.Max(
            0,
            maximumChildSpawnEvents);
        SplitProjectileStructures = splitProjectileStructures ??
            System.Array.Empty<BulletStructure>();
        ChildFlightWarningEnabled = childFlightWarningEnabled;
        CameraReleaseDistanceMultiplier = float.IsNaN(
            cameraReleaseDistanceMultiplier)
            ? ProjectilePool.DefaultReleaseDistanceMultiplier
            : UnityEngine.Mathf.Max(0f, cameraReleaseDistanceMultiplier);
    }

    public float Speed { get; }
    public bool UseTimeTimeForMotion { get; }
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
    public float LinearJerk { get; }
    public float LinearJerkDurationSeconds { get; }
    public float AngularAccelerationDegreesPerSecondSquared { get; }
    public float AngularAccelerationDurationSeconds { get; }
    public float AngularJerkDegreesPerSecondCubed { get; }
    public float AngularJerkDurationSeconds { get; }
    public bool ReleaseParentAfterFinalChildSpawn { get; }
    public float GuidanceCommandIntervalSeconds { get; }
    public System.Collections.Generic.IReadOnlyList<float>
        SplitAngleOffsetsDegrees { get; }
    public LaserStructure ChildLaserStructure { get; }
    public int MaximumChildSpawnEvents { get; }
    public System.Collections.Generic.IReadOnlyList<BulletStructure>
        SplitProjectileStructures { get; }
    public bool ChildFlightWarningEnabled { get; }
    public float CameraReleaseDistanceMultiplier { get; }
    public bool HasSplit => SplitProjectileCount > 0 &&
                            (ChildStructure != null ||
                             SplitProjectileStructures.Count > 0 ||
                             ChildLaserStructure != null);

    public float GetSplitAngleOffset(int index)
    {
        if (index >= 0 && index < SplitAngleOffsetsDegrees.Count)
        {
            return SplitAngleOffsetsDegrees[index];
        }

        float centerIndex = (SplitProjectileCount - 1) * 0.5f;
        return (index - centerIndex) * SplitAngleIntervalDegrees;
    }

    public BulletStructure GetSplitProjectileStructure(int index)
    {
        return index >= 0 &&
               index < SplitProjectileStructures.Count &&
               SplitProjectileStructures[index] != null
            ? SplitProjectileStructures[index]
            : ChildStructure;
    }

    public static BulletStructure Straight(
        float speed,
        int threatLevel,
        float linearAcceleration = 0f,
        float linearAccelerationDurationSeconds = 0f,
        float linearJerk = 0f,
        float linearJerkDurationSeconds = 0f,
        float cameraReleaseDistanceMultiplier =
            ProjectilePool.DefaultReleaseDistanceMultiplier)
    {
        return new BulletStructure(
            speed,
            threatLevel,
            linearAcceleration: linearAcceleration,
            linearAccelerationDurationSeconds:
                linearAccelerationDurationSeconds,
            linearJerk: linearJerk,
            linearJerkDurationSeconds: linearJerkDurationSeconds,
            cameraReleaseDistanceMultiplier:
                cameraReleaseDistanceMultiplier);
    }
}
