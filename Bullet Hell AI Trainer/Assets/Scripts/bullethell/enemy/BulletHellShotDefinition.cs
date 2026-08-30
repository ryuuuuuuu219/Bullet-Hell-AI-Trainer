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
        float projectileWarningDuration)
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

    public float GetProjectileAngleOffset(int projectileIndex)
    {
        float centerIndex = (ProjectileCount - 1) * 0.5f;
        return (projectileIndex - centerIndex) * AngleIntervalDegrees;
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
        float warningDuration = 0f)
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
            warningDuration);
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
            0f);
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
        BulletStructure splitOneWay = Split(
            64f, 2, 5f, 1, 0f, BulletSplitAimType.Forward,
            Straight(48f, 1));
        BulletStructure dispenser = Split(
            64f, 3, 5f, 3, 4f, BulletSplitAimType.PlayerAimed,
            Straight(48f, 1));
        BulletStructure geometricA = Split(
            160f, 3, 1.6f, 5, 72f, BulletSplitAimType.Forward,
            Split(
                192f, 3, 1.6f, 5, 72f, BulletSplitAimType.Forward,
                Split(
                    224f, 3, 1.6f, 1, 0f, BulletSplitAimType.Forward,
                    Straight(224f, 1),
                    BulletMotionType.ConstantTurn,
                    40f),
                BulletMotionType.ConstantTurn,
                40f),
            BulletMotionType.ConstantTurn,
            40f);
        BulletStructure geometricB = Split(
            160f, 3, 1.6f, 4, 45f, BulletSplitAimType.PlayerAimed,
            Split(
                192f, 3, 1.6f, 3, 10f, BulletSplitAimType.PlayerAimed,
                Straight(224f, 1),
                splitWarningDuration: 0.5f));

        return new[]
        {
            Stage(0, "自機狙い1way", "単独認識\n自機狙い1way\n2秒ごとに発射\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f))),
            Stage(1, "高頻度1way", "強化\n自機狙い1way\n1秒ごとに発射\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 1f))),
            Stage(2, "自機狙い5way・広間隔", "単独認識\n自機狙い5way\n2秒ごとに発射\n間隔10°\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, 5, 10f))),
            Stage(3, "自機狙い4+3way", "組み合わせ\n自機狙い3way＋時間差4way\n4wayは目標方向を基準に-9°／-3°／+3°／+9°\n2秒ごとに発射\n間隔10°／6°\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, 3, 10f), 2f),
                Pattern(Projectile(40f, 1, 2f, 4, 6f), 3f)),
            Stage(4, "編隊射撃", "強化\n自機狙い4way\n2秒ごとに発射\n間隔4°\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, 4, 4f))),
            Stage(5, "固定偏差1way・左右交互", "単独認識\n固定偏差1way\n2秒ごとに左右10°へ交互に発射\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 4f, aimOffset: -10f), 2f),
                Pattern(Projectile(40f, 1, 4f, aimOffset: 10f), 4f)),
            Stage(6, "弱い偏差射撃", "強化\n偏差射撃1way\n視線角速度の1.5倍でリード\n2秒ごとに発射\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, leadMultiplier: 1.5f))),
            Stage(7, "偏差射撃", "強化\n偏差射撃3way\n視線角速度の3倍でリード\n2秒ごとに発射\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, 3, 10f, leadMultiplier: 3f))),
            Stage(8, "固定方向5連射", "単独認識\n固定方向へ0.2秒間隔5連射\n2秒ごとに発射\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, burstCount: 5, burstInterval: 0.2f))),
            Stage(9, "薙ぎ払い", "強化\n-15°から+15°へ0.03秒間隔10連射\n2秒ごとに発射\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, burstCount: 10, burstInterval: 0.03f,
                    burstOffsets: Sweep(10, -15f, 15f)))),
            Stage(10, "3点バースト", "組み合わせ\n0→-3°→+5°へ0.2秒間隔3発射\n2秒ごとに発射\n弾速：64\n脅威度：1",
                Pattern(Projectile(64f, 1, 2f, burstCount: 3, burstInterval: 0.2f,
                    burstOffsets: new[] { 0f, -3f, 5f }))),
            Stage(11, "予告線付き通常弾", "単独認識\n予告線付き直進弾\n4秒ごとに発射\n弾速：40\n予告：2秒\n脅威度：1",
                Pattern(Projectile(40f, 1, 4f, warning: 2f))),
            Stage(12, "予告線付き中速弾", "強化\n予告線付き直進弾\n4秒ごとに発射\n弾速：80\n予告：2秒\n脅威度：2",
                Pattern(Projectile(80f, 2, 4f, warning: 2f))),
            Stage(13, "高速弾", "強化\n予告線付き高速弾\n4秒ごとに発射\n弾速：120\n予告：2秒\n脅威度：3",
                Pattern(Projectile(120f, 3, 4f, warning: 2f))),
            Stage(14, "予告線付き高速3点バースト", "組み合わせ\n0→-1°→+2°へ0.2秒間隔3連射\n4秒ごとに発射\n弾速：112\n予告：2秒\n脅威度：3",
                Pattern(Projectile(112f, 3, 4f, burstCount: 3, burstInterval: 0.2f,
                    burstOffsets: new[] { 0f, -1f, 2f }, warning: 2f))),
            Stage(15, "乱射", "強化\n指定リードで0.03秒間隔10連射\n4秒ごとに発射\n弾速：112\n予告：2秒\n脅威度：3",
                Pattern(Projectile(112f, 3, 4f, burstCount: 10, burstInterval: 0.03f,
                    burstOffsets: new[] { 0f, -1f, 2f, -5f, 7f, -2f, 3f, -3f, 5f, -2f },
                    warning: 2f))),
            Stage(16, "レーザー", "単独認識\n予告線付きレーザー\n4秒ごとに発射\n予告：2秒\n脅威度：4",
                Pattern(BulletHellShotDefinition.CreateLaser(new LaserStructure(
                    4, 2f, 0.5f, WarningLength, 2f, 12f), 4f))),
            Stage(17, "時限分裂1way", "単独認識\n5秒後に1wayへ展開\n2秒ごとに発射\n弾速：64→48\n脅威度：2→1",
                Pattern(Projectile(splitOneWay, 2f))),
            Stage(18, "ディスペンサー", "強化\n5秒後に自機狙い3wayへ展開\n2秒ごとに発射\n間隔4°\n弾速：64→48\n脅威度：3→1",
                Pattern(Projectile(dispenser, 2f))),
            Stage(19, "曲がる1way", "単独認識\n曲がる弾1way\n1秒ごとに発射\n角速度18deg/s\n弾速：32\n脅威度：1",
                Pattern(Projectile(Motion(32f, 1, BulletMotionType.ConstantTurn, 18f), 1f))),
            Stage(20, "曲がる3way", "強化\n曲がる弾3way\n1秒ごとに発射\n間隔20°\n角速度18deg/s\n弾速：32\n脅威度：2",
                Pattern(Projectile(Motion(32f, 2, BulletMotionType.ConstantTurn, 18f), 1f, 3, 20f))),
            Stage(21, "曲がる5way", "強化\n曲がる弾5way\n1秒ごとに発射\n間隔72°\n角速度18deg/s\n弾速：32\n脅威度：2",
                Pattern(Projectile(Motion(32f, 2, BulletMotionType.ConstantTurn, 18f), 1f, 5, 72f))),
            Stage(22, "単発の弱誘導弾", "単独認識\n誘導弾1発\n2秒ごとに発射\n角速度制限9deg/s\n累積旋回角制限90°\n弾速：64\n脅威度：2",
                Pattern(Projectile(Motion(64f, 2, BulletMotionType.Homing, 9f, totalTurnAngle: 90f), 2f))),
            Stage(23, "誘導弾　初級", "強化\n誘導弾を0.2秒間隔3発射\n2秒ごとに発射\n角速度制限18deg/s\n累積旋回角制限90°\n弾速：64\n脅威度：3",
                Pattern(Projectile(Motion(64f, 3, BulletMotionType.Homing, 18f, totalTurnAngle: 90f), 2f,
                    burstCount: 3, burstInterval: 0.2f))),
            Stage(24, "単発の比例航法誘導弾", "単独認識\n比例航法誘導弾1発\n2秒ごとに発射\n角速度制限180deg/s\n累積旋回角制限90°\n比例定数2.6\n弾速：64\n脅威度：4",
                Pattern(Projectile(Motion(64f, 4, BulletMotionType.ProportionalNavigation, 180f, 2.6f, 90f), 2f))),
            Stage(25, "誘導弾　上級", "強化\n比例航法誘導弾を0.2秒間隔3発射\n2秒ごとに発射\n角速度制限180deg/s\n累積旋回角制限90°\n比例定数2.6\n弾速：64\n脅威度：5",
                Pattern(Projectile(Motion(64f, 5, BulletMotionType.ProportionalNavigation, 180f, 2.6f, 90f), 2f,
                    burstCount: 3, burstInterval: 0.2f))),
            Stage(26, "壁", "組み合わせ\n自機狙い10way＋時間差4way\n2秒ごとに発射\n間隔4°／45°\n弾速：40\n脅威度：1",
                Pattern(Projectile(40f, 1, 2f, 10, 4f), 2f),
                Pattern(Projectile(40f, 1, 2f, 4, 45f, BulletAimType.FixedDown), 3f)),
            Stage(27, "ずっと連射", "組み合わせ\n自機狙い3way\n0.4秒ごとに発射\n間隔7°\n弾速：32\n脅威度：1",
                Pattern(Projectile(32f, 1, 0.4f, 3, 7f))),
            Stage(28, "幾何的弾幕A", "組み合わせ\n曲がる5wayを3段階展開後、1wayへ展開\n8秒ごとに発射\n各段階1.6秒後に展開\n間隔72°\n角速度40deg/s\n弾速：160→192→224→224\n脅威度：3→3→3→1",
                Pattern(Projectile(geometricA, 8f, 5, 72f))),
            Stage(29, "幾何的弾幕B", "組み合わせ\n自機狙い2way→4way→3way拡散弾\nBPM：30\n第1段階の間隔40°\n各段階1.6秒後に展開\n第3段階は予告線の0.5秒後に展開\n弾速：160→192→224\n脅威度：3→3→1",
                Pattern(Projectile(geometricB, 2f, 2, 40f))),
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
            $"課題{id + 1}\n{title}\n{details}",
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
        float warning = 0f)
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
            warning);
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
        float warning = 0f)
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
            warning);
    }

    private static BulletStructure Straight(float speed, int threat)
    {
        return BulletStructure.Straight(speed, threat);
    }

    private static BulletStructure Motion(
        float speed,
        int threat,
        BulletMotionType motionType,
        float turnRate,
        float navigationConstant = 0f,
        float totalTurnAngle = float.PositiveInfinity)
    {
        return new BulletStructure(
            speed,
            threat,
            motionType,
            turnRate,
            navigationConstant,
            totalTurnAngleDegrees: totalTurnAngle);
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
        float splitWarningDuration = 0f)
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
            splitWarningDuration);
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
