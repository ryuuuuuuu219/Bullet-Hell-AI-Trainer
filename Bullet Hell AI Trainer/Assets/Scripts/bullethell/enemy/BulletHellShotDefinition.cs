using System;
using System.Collections.Generic;
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
        float repeatIntervalSeconds,
        int burstCount,
        float burstIntervalSeconds,
        float[] burstAngleOffsets,
        float aimAngleOffsetDegrees,
        float leadMultiplier,
        float projectileWarningDuration,
        bool convergesOnAimPoint,
        BulletStructure[] projectileStructures)
    {
        AttackType = attackType;
        AimType = aimType;
        Bullet = bullet;
        Laser = laser;
        ProjectileCount = Mathf.Max(1, projectileCount);
        AngleIntervalDegrees = angleIntervalDegrees;
        RepeatIntervalSeconds = Mathf.Max(0f, repeatIntervalSeconds);
        BurstCount = Mathf.Max(1, burstCount);
        BurstIntervalSeconds = Mathf.Max(0f, burstIntervalSeconds);
        BurstAngleOffsets = burstAngleOffsets ?? Array.Empty<float>();
        AimAngleOffsetDegrees = aimAngleOffsetDegrees;
        LeadMultiplier = leadMultiplier;
        ProjectileWarningDuration = Mathf.Max(0f, projectileWarningDuration);
        ConvergesOnAimPoint = convergesOnAimPoint;
        ProjectileStructures = projectileStructures ?? Array.Empty<BulletStructure>();
    }

    public BulletHellAttackType AttackType { get; }
    public BulletAimType AimType { get; }
    public BulletStructure Bullet { get; }
    public LaserStructure Laser { get; }
    public int ProjectileCount { get; }
    public float AngleIntervalDegrees { get; }
    public float RepeatIntervalSeconds { get; }
    public int BurstCount { get; }
    public float BurstIntervalSeconds { get; }
    public IReadOnlyList<float> BurstAngleOffsets { get; }
    public float AimAngleOffsetDegrees { get; }
    public float LeadMultiplier { get; }
    public float ProjectileWarningDuration { get; }
    public bool ConvergesOnAimPoint { get; }
    public IReadOnlyList<BulletStructure> ProjectileStructures { get; }

    public float GetProjectileAngleOffset(int projectileIndex)
    {
        float centerIndex = (ProjectileCount - 1) * 0.5f;
        return (projectileIndex - centerIndex) * AngleIntervalDegrees;
    }

    public BulletStructure GetProjectileStructure(int projectileIndex)
    {
        return projectileIndex >= 0 &&
               projectileIndex < ProjectileStructures.Count &&
               ProjectileStructures[projectileIndex] != null
            ? ProjectileStructures[projectileIndex]
            : Bullet;
    }

    public float GetBurstAngleOffset(int burstIndex)
    {
        return burstIndex >= 0 && burstIndex < BurstAngleOffsets.Count
            ? BurstAngleOffsets[burstIndex]
            : 0f;
    }

    public static BulletHellShotDefinition CreateProjectile(
        BulletStructure bullet,
        int projectileCount = 1,
        float angleIntervalDegrees = 0f,
        float repeatIntervalSeconds = 0f,
        BulletAimType aimType = BulletAimType.PlayerAimed,
        int burstCount = 1,
        float burstIntervalSeconds = 0f,
        float[] burstAngleOffsets = null,
        float aimAngleOffsetDegrees = 0f,
        float leadMultiplier = 0f,
        float warningDuration = 0f,
        bool convergesOnAimPoint = false,
        BulletStructure[] projectileStructures = null)
    {
        return new BulletHellShotDefinition(
            BulletHellAttackType.Projectile,
            aimType,
            bullet,
            null,
            projectileCount,
            angleIntervalDegrees,
            repeatIntervalSeconds,
            burstCount,
            burstIntervalSeconds,
            burstAngleOffsets,
            aimAngleOffsetDegrees,
            leadMultiplier,
            warningDuration,
            convergesOnAimPoint,
            projectileStructures);
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
            1,
            0f,
            repeatIntervalSeconds,
            1,
            0f,
            null,
            0f,
            0f,
            0f,
            false,
            null);
    }
}

public sealed class BulletHellStagePattern
{
    public BulletHellStagePattern(
        BulletHellShotDefinition shot,
        float initialDelaySeconds = -1f)
    {
        Shot = shot;
        InitialDelaySeconds = initialDelaySeconds >= 0f
            ? initialDelaySeconds
            : shot?.RepeatIntervalSeconds ?? 0f;
    }

    public BulletHellShotDefinition Shot { get; }
    public float InitialDelaySeconds { get; }
}

public sealed class BulletHellStageDefinition
{
    public BulletHellStageDefinition(
        int id,
        string title,
        string description,
        params BulletHellStagePattern[] patterns)
        : this(id, title, description, Array.Empty<float>(), patterns)
    {
    }

    public BulletHellStageDefinition(
        int id,
        string title,
        string description,
        float[] threatArrivalTimes,
        params BulletHellStagePattern[] patterns)
    {
        Id = id;
        Title = title;
        Description = description;
        this.threatArrivalTimes = NormalizeThreatArrivalTimes(threatArrivalTimes);
        Patterns = patterns ?? Array.Empty<BulletHellStagePattern>();
    }

    public int Id { get; }
    public string Title { get; }
    public string Description { get; }
    public float[] threatArrivalTimes { get; }
    public IReadOnlyList<float> ThreatArrivalTimes => threatArrivalTimes;
    public IReadOnlyList<BulletHellStagePattern> Patterns { get; }

    private static float[] NormalizeThreatArrivalTimes(float[] values)
    {
        if (values == null || values.Length == 0)
        {
            return Array.Empty<float>();
        }

        float[] normalized = new float[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            normalized[index] = Mathf.Max(0f, values[index]);
        }

        Array.Sort(normalized);
        return normalized;
    }
}

public static class BulletHellStageAttackDefinitions
{
    private const float WarningLength = 1000f;
    private static readonly BulletHellStageDefinition[] Stages = BuildStages();

    public static int Count => Stages.Length;
    public static IReadOnlyList<BulletHellStageDefinition> All => Stages;

    public static BulletHellStageDefinition GetStage(int stageId)
    {
        return stageId >= 0 && stageId < Stages.Length
            ? Stages[stageId]
            : null;
    }

    private static BulletHellStageDefinition[] BuildStages()
    {
        BulletStructure[] changingConvergence = new BulletStructure[8];
        for (int index = 0; index < changingConvergence.Length; index++)
        {
            float ratio = index / (changingConvergence.Length - 1f);
            changingConvergence[index] = Motion(
                400f,
                2,
                BulletMotionType.ConstantTurn,
                Mathf.Lerp(-30f, 30f, ratio),
                angularAcceleration: Mathf.Lerp(-4f, 4f, ratio),
                angularAccelerationDuration: 15f);
        }

        BulletStructure timedForwardSplit = Split(
            200f,
            3,
            1.2f,
            5,
            16f,
            BulletSplitAimType.Forward,
            Straight(250f, 1));
        BulletStructure timedPlayerAimedSplit = Split(
            200f,
            3,
            1.2f,
            3,
            8f,
            BulletSplitAimType.PlayerAimed,
            Straight(250f, 1));

        return new[]
        {
            Stage(0, "自機狙い1way", "単独認識\n自機狙い1way\n自機を直接狙う単発弾の回避を学習する\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f))),
            Stage(1, "自機狙い5way", "強化\n自機狙い5way\n中央に自機を直接狙う弾を含む奇数way弾\n間隔：12deg\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f, 5, 12f))),
            Stage(2, "自機狙い2way", "単独認識\n自機狙い2way\n中央に自機を直接狙う弾を含まない偶数way弾\n間隔：12deg\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f, 2, 12f))),
            Stage(3, "自機狙い6way", "強化\n自機狙い6way\n弾数を増やした偶数way弾\n間隔：8deg\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f, 6, 8f))),
            Stage(4, "収束弾", "単独認識\n収束9way\n一点へ収束する直進弾\n間隔：4deg\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f, 9, 4f,
                    convergesOnAimPoint: true))),
            Stage(5, "一定曲率弾", "単独認識\n角速度が一定の曲がる弾\n角速度：30deg/s\n角加速度：0deg/s²\n発射周期：2秒\n弾速：400\n脅威度：2",
                Pattern(Projectile(Motion(400f, 2, BulletMotionType.ConstantTurn, 30f), 2f))),
            Stage(6, "曲率増加弾", "強化\n角速度が時間とともに増加する曲がる弾\n初期角速度：0deg/s\n角加速度：+5deg/s²\n変化時間：15秒\n発射周期：2秒\n弾速：400\n脅威度：2",
                Pattern(Projectile(Motion(400f, 2, BulletMotionType.ConstantTurn, 0f,
                    angularAcceleration: 5f, angularAccelerationDuration: 15f), 2f))),
            Stage(7, "軌道変化する収束弾", "強化\n8way収束弾\n収束する角速度と角加速度に差を持つ8発\n間隔：0deg\n角速度：-30～+30deg/s\n角加速度：-4～+4deg/s²\n変化時間：15秒\n発射周期：2秒\n弾速：400\n脅威度：2",
                Pattern(Projectile(changingConvergence[0], 2f, 8, 0f,
                    convergesOnAimPoint: true,
                    projectileStructures: changingConvergence))),
            Stage(8, "N=1比例航法誘導弾・1way", "単独認識\n航法定数N=1の比例航法誘導弾、1way\n角速度制限：12deg/s\ntotalΔθ：90deg\n発射周期：2秒\n弾速：500\n脅威度：3",
                Pattern(Projectile(Motion(500f, 3, BulletMotionType.ProportionalNavigation,
                    12f, 1f, 90f), 2f))),
            Stage(9, "N=1比例航法誘導弾・4way", "強化\n航法定数N=1の比例航法誘導弾、4way\n角速度制限：12deg/s\ntotalΔθ：90deg\n発射周期：2秒\n弾速：500\n脅威度：3",
                Pattern(Projectile(Motion(500f, 3, BulletMotionType.ProportionalNavigation,
                    12f, 1f, 90f), 2f, 4, 12f,
                    convergesOnAimPoint: true))),
            Stage(10, "減速弾", "単独認識\n減速する直進弾\n初速：500Unit/s\n加速度：-250Unit/s²\n加減速時間：1.5秒\n発射周期：2秒\n脅威度：1",
                Pattern(Projectile(Straight(500f, 1, -250f, 1.5f), 2f))),
            Stage(11, "加速弾", "単独認識\n加速する直進弾\n初速：150Unit/s\n加速度：+200Unit/s²\n加減速時間：2秒\n発射周期：2秒\n脅威度：1",
                Pattern(Projectile(Straight(150f, 1, 200f, 2f), 2f))),
            Stage(12, "移動方向への偏差射撃", "強化\n自機の移動方向を参照する偏差射撃\nリード量：LOS角速度×3\n発射周期：1.2秒\n弾速：500\n脅威度：1",
                Pattern(Projectile(500f, 1, 1.2f, leadMultiplier: 3f))),
            Stage(13, "N=3誘導弾", "強化\n航法定数N=3の比例航法誘導弾\n角速度制限：18deg/s\ntotalΔθ：120deg\n発射周期：2秒\n弾速：400\n脅威度：3",
                Pattern(Projectile(Motion(400f, 3, BulletMotionType.ProportionalNavigation,
                    18f, 3f, 120f), 2f))),
            Stage(14, "固定方向への連射", "単独認識\n固定された射線への連射弾\n連射数：5\n連射間隔：0.2秒\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f, aimType: BulletAimType.FixedDown,
                    burstCount: 5, burstInterval: 0.2f))),
            Stage(15, "薙ぎ払い連射", "強化\n射線を変化させる連射弾\n連射数：10\n連射間隔：0.1秒\n偏差角：-18→+18\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f, aimType: BulletAimType.FixedDown,
                    burstCount: 10, burstInterval: 0.1f,
                    burstOffsets: Sweep(10, -18f, 18f)))),
            Stage(16, "予告線付き高速弾", "単独認識\n予告線付き高速直進弾\n予告時間：0.5秒\n発射周期：2秒\n弾速：800\n脅威度：4",
                Pattern(Projectile(800f, 4, 2f, warning: 0.5f))),
            Stage(17, "レーザー", "強化\n予告線付きレーザー\n予告時間：1秒\n照射時間：2秒\n発射周期：2秒\n脅威度：5",
                Pattern(BulletHellShotDefinition.CreateLaser(new LaserStructure(
                    5, 1f, 2f, WarningLength, 2f, 12f), 2f))),
            Stage(18, "時限分裂弾", "単独認識\n1.2秒後にベクトル基準5wayへ分裂\n子弾：基礎的飛翔体5way\n間隔：16deg\n発射周期：2秒\n弾速：200→250\n脅威度：3→1",
                Pattern(Projectile(timedForwardSplit, 2f))),
            Stage(19, "飛翔中の弾を起点とする自機狙い拡散弾", "強化\n飛翔中の親弾を起点とする対プレイヤー3way\n分裂時間：1.2秒\n間隔：8deg\n発射周期：2秒\n弾速：200→250\n脅威度：3→1",
                Pattern(Projectile(timedPlayerAimedSplit, 2f))),
        };
    }

    private static BulletHellStageDefinition Stage(
        int id,
        string title,
        string details,
        params BulletHellStagePattern[] patterns)
    {
        return new BulletHellStageDefinition(
            id,
            title,
            $"課題A-{id + 1}\n{title}\n{details}",
            patterns);
    }

    private static BulletHellStagePattern Pattern(
        BulletHellShotDefinition shot,
        float initialDelay = -1f)
    {
        return new BulletHellStagePattern(shot, initialDelay);
    }

    private static BulletHellShotDefinition Projectile(
        float speed,
        int threat,
        float repeat,
        int count = 1,
        float interval = 0f,
        BulletAimType aimType = BulletAimType.PlayerAimed,
        int burstCount = 1,
        float burstInterval = 0f,
        float[] burstOffsets = null,
        float aimOffset = 0f,
        float leadMultiplier = 0f,
        float warning = 0f,
        bool convergesOnAimPoint = false,
        BulletStructure[] projectileStructures = null)
    {
        return Projectile(
            Straight(speed, threat),
            repeat,
            count,
            interval,
            aimType,
            burstCount,
            burstInterval,
            burstOffsets,
            aimOffset,
            leadMultiplier,
            warning,
            convergesOnAimPoint,
            projectileStructures);
    }

    private static BulletHellShotDefinition Projectile(
        BulletStructure structure,
        float repeat,
        int count = 1,
        float interval = 0f,
        BulletAimType aimType = BulletAimType.PlayerAimed,
        int burstCount = 1,
        float burstInterval = 0f,
        float[] burstOffsets = null,
        float aimOffset = 0f,
        float leadMultiplier = 0f,
        float warning = 0f,
        bool convergesOnAimPoint = false,
        BulletStructure[] projectileStructures = null)
    {
        return BulletHellShotDefinition.CreateProjectile(
            structure,
            count,
            interval,
            repeat,
            aimType,
            burstCount,
            burstInterval,
            burstOffsets,
            aimOffset,
            leadMultiplier,
            warning,
            convergesOnAimPoint,
            projectileStructures);
    }

    private static BulletStructure Straight(
        float speed,
        int threat,
        float acceleration = 0f,
        float accelerationDuration = 0f)
    {
        return new BulletStructure(
            speed,
            threat,
            linearAcceleration: acceleration,
            linearAccelerationDurationSeconds: accelerationDuration);
    }

    private static BulletStructure Motion(
        float speed,
        int threat,
        BulletMotionType motionType,
        float turnRate,
        float navigationConstant = 0f,
        float totalTurnAngle = float.PositiveInfinity,
        float acceleration = 0f,
        float accelerationDuration = 0f,
        float angularAcceleration = 0f,
        float angularAccelerationDuration = 0f,
        float guidanceCommandInterval = 0f)
    {
        return new BulletStructure(
            speed,
            threat,
            motionType,
            turnRate,
            navigationConstant,
            totalTurnAngleDegrees: totalTurnAngle,
            linearAcceleration: acceleration,
            linearAccelerationDurationSeconds: accelerationDuration,
            angularAccelerationDegreesPerSecondSquared: angularAcceleration,
            angularAccelerationDurationSeconds: angularAccelerationDuration,
            guidanceCommandIntervalSeconds: guidanceCommandInterval);
    }

    private static BulletStructure Split(
        float speed,
        int threat,
        float delay,
        int childCount,
        float childInterval,
        BulletSplitAimType splitAimType,
        BulletStructure child,
        BulletMotionType motionType = BulletMotionType.Straight,
        float turnRate = 0f,
        float splitWarningDuration = 0f,
        float childSpawnInterval = float.PositiveInfinity)
    {
        return new BulletStructure(
            speed,
            threat,
            motionType,
            turnRate,
            0f,
            delay,
            childCount,
            childInterval,
            splitAimType,
            child,
            splitWarningDuration,
            childSpawnIntervalSeconds: childSpawnInterval);
    }

    private static float[] Sweep(int count, float start, float end)
    {
        float[] values = new float[Mathf.Max(1, count)];
        if (values.Length == 1)
        {
            values[0] = start;
            return values;
        }

        for (int index = 0; index < values.Length; index++)
        {
            values[index] = Mathf.Lerp(start, end, index / (values.Length - 1f));
        }

        return values;
    }
}
