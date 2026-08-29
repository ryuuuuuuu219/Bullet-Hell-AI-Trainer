using UnityEngine;

public enum BulletHellAttackType
{
    Projectile,
    Laser,
}

public enum BulletAimType
{
    PlayerAimed,
    FixedDown,
}

public sealed class LaserStructure
{
    public LaserStructure(
        int threatLevel,
        float warningDuration,
        float activeDuration,
        float length,
        float warningWidth,
        float activeWidth)
    {
        ThreatLevel = Mathf.Max(0, threatLevel);
        WarningDuration = Mathf.Max(0f, warningDuration);
        ActiveDuration = Mathf.Max(0f, activeDuration);
        Length = Mathf.Max(0.01f, length);
        WarningWidth = Mathf.Max(0.01f, warningWidth);
        ActiveWidth = Mathf.Max(0.01f, activeWidth);
    }

    public int ThreatLevel { get; }
    public float WarningDuration { get; }
    public float ActiveDuration { get; }
    public float Length { get; }
    public float WarningWidth { get; }
    public float ActiveWidth { get; }
}

public sealed class BulletHellShotDefinition
{
    private BulletHellShotDefinition(
        BulletHellAttackType attackType,
        BulletAimType aimType,
        BulletStructure bullet,
        LaserStructure laser,
        int projectileCount,
        float angleIntervalDegrees,
        float repeatIntervalSeconds)
    {
        AttackType = attackType;
        AimType = aimType;
        Bullet = bullet;
        Laser = laser;
        ProjectileCount = Mathf.Max(1, projectileCount);
        AngleIntervalDegrees = angleIntervalDegrees;
        RepeatIntervalSeconds = Mathf.Max(0f, repeatIntervalSeconds);
    }

    public BulletHellAttackType AttackType { get; }
    public BulletAimType AimType { get; }
    public BulletStructure Bullet { get; }
    public LaserStructure Laser { get; }
    public int ProjectileCount { get; }
    public float AngleIntervalDegrees { get; }
    public float RepeatIntervalSeconds { get; }

    public float GetProjectileAngleOffset(int projectileIndex)
    {
        float centerIndex = (ProjectileCount - 1) * 0.5f;
        return (projectileIndex - centerIndex) * AngleIntervalDegrees;
    }

    public static BulletHellShotDefinition CreateProjectile(
        BulletStructure bullet,
        int projectileCount = 1,
        float angleIntervalDegrees = 0f,
        float repeatIntervalSeconds = 0f,
        BulletAimType aimType = BulletAimType.PlayerAimed)
    {
        return new BulletHellShotDefinition(
            BulletHellAttackType.Projectile,
            aimType,
            bullet,
            null,
            projectileCount,
            angleIntervalDegrees,
            repeatIntervalSeconds);
    }

    public static BulletHellShotDefinition CreateLaser(
        LaserStructure laser,
        float repeatIntervalSeconds = 0f,
        BulletAimType aimType = BulletAimType.PlayerAimed)
    {
        return new BulletHellShotDefinition(
            BulletHellAttackType.Laser,
            aimType,
            null,
            laser,
            projectileCount: 1,
            angleIntervalDegrees: 0f,
            repeatIntervalSeconds);
    }
}

public static class BulletHellStageAttackDefinitions
{
    public static readonly BulletHellShotDefinition Stage1 =
        BulletHellShotDefinition.CreateProjectile(
            BulletStructure.Straight(speed: 40f, threatLevel: 1),
            repeatIntervalSeconds: 2f);

    public static readonly BulletHellShotDefinition Stage13 =
        BulletHellShotDefinition.CreateLaser(new LaserStructure(
            threatLevel: 4,
            warningDuration: 2f,
            activeDuration: 0.5f,
            length: 1000f,
            warningWidth: 2f,
            activeWidth: 12f),
            repeatIntervalSeconds: 4f);
}
