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
        BulletStructure[] projectileStructures,
        float[] projectileAngleOffsets,
        BulletStructure[] burstStructures,
        bool reaimDuringBurst,
        bool randomizeSpeedAndInterval,
        int[] burstProjectileCounts,
        float[] burstProjectileAngleIntervals)
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
        ProjectileAngleOffsets = projectileAngleOffsets ?? Array.Empty<float>();
        BurstStructures = burstStructures ?? Array.Empty<BulletStructure>();
        ReaimDuringBurst = reaimDuringBurst;
        RandomizeSpeedAndInterval = randomizeSpeedAndInterval;
        BurstProjectileCounts = burstProjectileCounts ?? Array.Empty<int>();
        BurstProjectileAngleIntervals = burstProjectileAngleIntervals ??
            Array.Empty<float>();
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
    public IReadOnlyList<float> ProjectileAngleOffsets { get; }
    public IReadOnlyList<BulletStructure> BurstStructures { get; }
    public bool ReaimDuringBurst { get; }
    public bool RandomizeSpeedAndInterval { get; }
    public IReadOnlyList<int> BurstProjectileCounts { get; }
    public IReadOnlyList<float> BurstProjectileAngleIntervals { get; }

    public float GetProjectileAngleOffset(int projectileIndex)
    {
        if (projectileIndex >= 0 &&
            projectileIndex < ProjectileAngleOffsets.Count)
        {
            return ProjectileAngleOffsets[projectileIndex];
        }

        float centerIndex = (ProjectileCount - 1) * 0.5f;
        return (projectileIndex - centerIndex) * AngleIntervalDegrees;
    }

    public float GetProjectileAngleOffset(
        int projectileIndex,
        int projectileCount,
        float angleIntervalDegrees)
    {
        if (projectileIndex >= 0 &&
            projectileIndex < ProjectileAngleOffsets.Count)
        {
            return ProjectileAngleOffsets[projectileIndex];
        }

        float centerIndex = (projectileCount - 1) * 0.5f;
        return (projectileIndex - centerIndex) * angleIntervalDegrees;
    }

    public BulletStructure GetBurstStructure(int burstIndex)
    {
        return burstIndex >= 0 &&
               burstIndex < BurstStructures.Count &&
               BurstStructures[burstIndex] != null
            ? BurstStructures[burstIndex]
            : Bullet;
    }

    public int GetBurstProjectileCount(int burstIndex)
    {
        return burstIndex >= 0 && burstIndex < BurstProjectileCounts.Count
            ? Mathf.Max(1, BurstProjectileCounts[burstIndex])
            : ProjectileCount;
    }

    public float GetBurstProjectileAngleInterval(int burstIndex)
    {
        return burstIndex >= 0 &&
               burstIndex < BurstProjectileAngleIntervals.Count
            ? BurstProjectileAngleIntervals[burstIndex]
            : AngleIntervalDegrees;
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
        BulletStructure[] projectileStructures = null,
        float[] projectileAngleOffsets = null,
        BulletStructure[] burstStructures = null,
        bool reaimDuringBurst = false,
        bool randomizeSpeedAndInterval = false,
        int[] burstProjectileCounts = null,
        float[] burstProjectileAngleIntervals = null)
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
            projectileStructures,
            projectileAngleOffsets,
            burstStructures,
            reaimDuringBurst,
            randomizeSpeedAndInterval,
            burstProjectileCounts,
            burstProjectileAngleIntervals);
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
            null,
            null,
            null,
            false,
            false,
            null,
            null);
    }
}

public sealed class BulletHellStagePattern
{
    public BulletHellStagePattern(
        BulletHellShotDefinition shot,
        float initialDelaySeconds = -1f,
        float repeatIntervalDecreaseSecondsPerCycle = 0f,
        float minimumRepeatIntervalSeconds = 0f)
    {
        Shot = shot;
        InitialDelaySeconds = initialDelaySeconds >= 0f
            ? initialDelaySeconds
            : shot?.RepeatIntervalSeconds ?? 0f;
        RepeatIntervalDecreaseSecondsPerCycle = Mathf.Max(
            0f,
            repeatIntervalDecreaseSecondsPerCycle);
        MinimumRepeatIntervalSeconds = Mathf.Max(
            0f,
            minimumRepeatIntervalSeconds);
    }

    public BulletHellShotDefinition Shot { get; }
    public float InitialDelaySeconds { get; }
    public float RepeatIntervalDecreaseSecondsPerCycle { get; }
    public float MinimumRepeatIntervalSeconds { get; }

    public float GetRepeatInterval(int cycleIndex)
    {
        float baseInterval = Shot?.RepeatIntervalSeconds ?? 0f;
        return Mathf.Max(
            MinimumRepeatIntervalSeconds,
            baseInterval - RepeatIntervalDecreaseSecondsPerCycle *
            Mathf.Max(0, cycleIndex));
    }
}

public sealed class BulletHellStageDefinition
{
    public BulletHellStageDefinition(
        ChallengeCategory category,
        int id,
        string title,
        string description,
        params BulletHellStagePattern[] patterns)
        : this(
            category,
            id,
            title,
            description,
            Array.Empty<float>(),
            patterns)
    {
    }

    public BulletHellStageDefinition(
        ChallengeCategory category,
        int id,
        string title,
        string description,
        float[] threatArrivalTimes,
        params BulletHellStagePattern[] patterns)
    {
        Category = category;
        Id = id;
        ChallengeCode = $"{GetCategoryPrefix(category)}-{id + 1}";
        Title = title;
        Description = description;
        this.threatArrivalTimes = NormalizeThreatArrivalTimes(threatArrivalTimes);
        Patterns = patterns ?? Array.Empty<BulletHellStagePattern>();
    }

    public ChallengeCategory Category { get; }
    public int Id { get; }
    public string ChallengeCode { get; }
    public string Title { get; }
    public string Description { get; }
    public float[] threatArrivalTimes { get; }
    public IReadOnlyList<float> ThreatArrivalTimes => threatArrivalTimes;
    public IReadOnlyList<BulletHellStagePattern> Patterns { get; }
    public bool IsPlayable => Patterns.Count > 0;

    private static string GetCategoryPrefix(ChallengeCategory category)
    {
        switch (category)
        {
            case ChallengeCategory.Basic:
                return "A";
            case ChallengeCategory.Applied:
                return "B";
            case ChallengeCategory.Advanced:
                return "C";
            case ChallengeCategory.Final:
                return "D";
            default:
                return "?";
        }
    }

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
    private const float LaserRange = float.PositiveInfinity;
    // Final Challenge does not currently state the first spiral stage speed.
    // Keep the implementation fallback explicit so it can be replaced when
    // the memo gains a value.
    private const float FinalBarrierSpiralSpeed = 150f;
    private static readonly BulletHellStageDefinition[][] StageGroups =
    {
        BuildBasicStages(),
        BuildAppliedStages(),
        BuildAdvancedStages(),
        BuildFinalStages(),
    };

    public static IReadOnlyList<BulletHellStageDefinition> GetStages(
        ChallengeCategory category)
    {
        int categoryIndex = (int)category;
        return categoryIndex >= 0 && categoryIndex < StageGroups.Length
            ? StageGroups[categoryIndex]
            : Array.Empty<BulletHellStageDefinition>();
    }

    public static int GetCount(ChallengeCategory category)
    {
        return GetStages(category).Count;
    }

    public static BulletHellStageDefinition GetStage(
        ChallengeCategory category,
        int stageId)
    {
        IReadOnlyList<BulletHellStageDefinition> stages = GetStages(category);
        return stageId >= 0 && stageId < stages.Count
            ? stages[stageId]
            : null;
    }

    private static BulletHellStageDefinition[] BuildBasicStages()
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
            Stage(9, "N=1比例航法誘導弾・4way", "強化\n航法定数N=1の比例航法誘導弾、4way\n角速度制限：12deg/s\ntotalΔθ：90deg\n発射周期：2秒\n弾速：500/3（約166.67）\n脅威度：3",
                Pattern(Projectile(Motion(500f / 3f, 3, BulletMotionType.ProportionalNavigation,
                    12f, 1f, 90f), 2f, 4, 45f))),
            Stage(10, "減速弾", "単独認識\n減速する直進弾\n初速：500Unit/s\n加速度：-250Unit/s²\n加減速時間：1.5秒\n発射周期：2秒\n脅威度：1",
                Pattern(Projectile(Straight(500f, 1, -250f, 1.5f), 2f))),
            Stage(11, "加速弾", "単独認識\n加速する直進弾\n初速：150Unit/s\n加速度：+200Unit/s²\n加減速時間：2秒\n発射周期：2秒\n脅威度：1",
                Pattern(Projectile(Straight(150f, 1, 200f, 2f), 2f))),
            Stage(12, "移動方向への偏差射撃", "強化\n自機の移動方向を参照する偏差射撃\nリード量：LOS角速度×3\n発射周期：1.2秒\n弾速：500\n脅威度：1",
                Pattern(Projectile(500f, 1, 1.2f, leadMultiplier: 3f))),
            Stage(13, "N=3誘導弾", "強化\n航法定数N=3の比例航法誘導弾\n角速度制限：18deg/s\ntotalΔθ：120deg\n発射周期：2秒\n弾速：400\n脅威度：3",
                Pattern(Projectile(Motion(400f, 3, BulletMotionType.ProportionalNavigation,
                    18f, 3f, 120f), 2f))),
            Stage(14, "連射", "単独認識\n自機狙い1way。連射開始時の射線を固定\n連射数：5\n連射間隔：0.2秒\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f,
                    burstCount: 5, burstInterval: 0.2f))),
            Stage(15, "薙ぎ払い連射", "強化\n射線を変化させる連射弾\n連射数：10\n連射間隔：0.1秒\n偏差角：-18→+18\n発射周期：2秒\n弾速：400\n脅威度：1",
                Pattern(Projectile(400f, 1, 2f,
                    burstCount: 10, burstInterval: 0.1f,
                    burstOffsets: Sweep(10, -18f, 18f)))),
            Stage(16, "予告線付き高速弾", "単独認識\n予告線付き高速直進弾\n予告時間：0.5秒\n発射周期：2秒\n弾速：800\n脅威度：4",
                Pattern(Projectile(800f, 4, 2f, warning: 0.5f))),
            Stage(17, "レーザー", "強化\n予告線付きレーザー\n予告時間：1秒\n照射時間：2秒\n発射周期：2秒\n脅威度：5",
                Pattern(BulletHellShotDefinition.CreateLaser(new LaserStructure(
                    5, 1f, 2f, LaserRange, 3f, 3f), 2f))),
            Stage(18, "時限分裂弾", "単独認識\n1.2秒後にベクトル基準5wayへ分裂\n子弾：基礎的飛翔体5way\n間隔：16deg\n発射周期：2秒\n弾速：200→250\n脅威度：3→1",
                Pattern(Projectile(timedForwardSplit, 2f))),
            Stage(19, "飛翔中の弾を起点とする自機狙い拡散弾", "強化\n飛翔中の親弾を起点とする対プレイヤー3way\n分裂時間：1.2秒\n間隔：8deg\n発射周期：2秒\n弾速：200→250\n脅威度：3→1",
                Pattern(Projectile(timedPlayerAimedSplit, 2f))),
        };
    }

    private static BulletHellStageDefinition[] BuildAppliedStages()
    {
        BuildXProjectiles(
            3,
            1,
            0f,
            0f,
            out float[] xAngles,
            out BulletStructure[] xStructures);
        BulletStructure[] burstSpeeds = BuildBurstStructures(8, 200f, 30f);

        return new[]
        {
            Stage(ChallengeCategory.Applied, 0, "自機狙い奇数way・二重一斉発射",
                "強化\n自機狙い9wayを2組同時発射\n第1群：間隔8deg、弾速250\n第2群：間隔12deg、弾速400\n発射周期：2秒\n脅威度：1",
                Pattern(Projectile(250f, 1, 2f, 9, 8f)),
                Pattern(Projectile(400f, 1, 2f, 9, 12f))),
            Stage(ChallengeCategory.Applied, 1, "自機狙いway混成・三重一斉発射",
                "強化\n10way・9way・8wayを同時発射\n発射周期：2秒\n弾速：300・350・400\n脅威度：1",
                Pattern(Projectile(300f, 1, 2f, 10, 8f)),
                Pattern(Projectile(350f, 1, 2f, 9, 8f)),
                Pattern(Projectile(400f, 1, 2f, 8, 8f))),
            Stage(ChallengeCategory.Applied, 2, "X字収束弾",
                "組み合わせ\n9発×2列を全周囲3方向へ展開\n角度：±36deg、弾速：220～380\n発射周期：2秒\n脅威度：1",
                Pattern(ProjectileExplicit(xStructures, xAngles, 2f))),
            Stage(ChallengeCategory.Applied, 3, "減衰螺旋弾",
                "強化\n自機狙いから全周囲16方向へ展開\n角速度150deg/s、角加速度-10deg/s²、15秒\n発射周期：2秒\n弾速：400\n脅威度：2",
                Pattern(Projectile(Motion(400f, 2, BulletMotionType.ConstantTurn,
                    150f, angularAcceleration: -10f,
                    angularAccelerationDuration: 15f), 2f, 16, 22.5f))),
            Stage(ChallengeCategory.Applied, 4, "0.5秒クロック比例航法誘導弾",
                "強化\n全周囲6方向、N=3、誘導更新0.5秒\n角速度制限45deg/s、totalΔθ180deg\n発射周期：2秒\n弾速：200\n脅威度：3",
                Pattern(Projectile(Motion(200f, 3,
                    BulletMotionType.ProportionalNavigation, 45f, 3f, 180f,
                    guidanceCommandInterval: 0.5f), 2f, 6, 60f))),
            Stage(ChallengeCategory.Applied, 5, "比例航法誘導弾",
                "強化\n全周囲6方向、継続N=3比例航法誘導\n角速度制限45deg/s、totalΔθ180deg\n発射周期：2秒\n弾速：400\n脅威度：3",
                Pattern(Projectile(Motion(400f, 3,
                    BulletMotionType.ProportionalNavigation, 45f, 3f, 180f),
                    2f, 6, 60f))),
            Stage(ChallengeCategory.Applied, 6, "連射",
                "組み合わせ\n自機狙い。射撃ごとに再照準\n8連射、間隔0.2秒、弾速200+30n\n発射周期：2秒\n脅威度：1",
                Pattern(Projectile(Straight(200f, 1), 2f,
                    burstCount: 8, burstInterval: 0.2f,
                    burstStructures: burstSpeeds, reaimDuringBurst: true))),
            Stage(ChallengeCategory.Applied, 7, "薙ぎ払い連射",
                "組み合わせ\n連射開始時の自機照準を固定\n8連射、間隔0.1秒、偏差角-18→+18\n弾速200+30n、発射周期2秒\n脅威度：1",
                Pattern(Projectile(Straight(200f, 1), 2f,
                    burstCount: 8, burstInterval: 0.1f,
                    burstOffsets: Sweep(8, -18f, 18f),
                    burstStructures: burstSpeeds))),
        };
    }

    private static BulletHellStageDefinition[] BuildAdvancedStages()
    {
        BulletStructure c1Stage3 = Straight(224f, 1);
        BulletStructure c1Stage2 = Split(192f, 3, 1.6f, 3, 40f,
            BulletSplitAimType.PlayerAimed, c1Stage3,
            splitWarningDuration: 0.5f);
        BulletStructure c1Stage1 = Split(160f, 4, 1.6f, 4, 30f,
            BulletSplitAimType.PlayerAimed, c1Stage2);

        BuildXProjectiles(6, 1, 20f, 10f,
            out float[] c3Angles, out BulletStructure[] c3Structures);

        BulletStructure c5 = new BulletStructure(
            300f, 4, BulletMotionType.ProportionalNavigation, 45f, 3f,
            1.8f, 3, 45f, BulletSplitAimType.PlayerAimed,
            Straight(250f, 1), totalTurnAngleDegrees: 180f);
        BulletStructure c6 = Split(300f, 3, 0.2f, 2, 0f,
            BulletSplitAimType.Forward, Straight(300f, 1),
            childSpawnInterval: 0.2f,
            childAngles: new[] { -90f, 90f });
        LaserStructure gridLaser = new LaserStructure(
            5, 1f, 2f, LaserRange, 3f, 3f);
        BulletStructure c7 = SplitLaser(
            200f, 1, 0.3f, 2, 180f, gridLaser, 0.3f);

        BulletStructure c8Stage4 = Straight(100f, 1);
        BulletStructure c8Stage3 = Split(200f / 3f, 3, 1f, 2, 90f,
            BulletSplitAimType.Forward, c8Stage4, childSpawnInterval: 1f);
        BulletStructure c8Stage2 = Split(100f, 4, 1f, 2, 90f,
            BulletSplitAimType.Forward, c8Stage3, childSpawnInterval: 1f);
        BulletStructure c8Stage1 = Split(400f / 3f, 4, 1f, 2, 90f,
            BulletSplitAimType.Forward, c8Stage2, childSpawnInterval: 1f);

        BulletStructure c9Stage4 = Straight(224f, 1);
        BulletStructure c9Stage3 = Split(224f, 3, 1.6f, 1, 0f,
            BulletSplitAimType.Forward, c9Stage4,
            motionType: BulletMotionType.ConstantTurn, turnRate: 40f);
        BulletStructure c9Stage2 = Split(192f, 4, 1.6f, 5, 72f,
            BulletSplitAimType.Forward, c9Stage3,
            motionType: BulletMotionType.ConstantTurn, turnRate: 40f);
        BulletStructure c9Stage1 = Split(160f, 4, 1.6f, 5, 72f,
            BulletSplitAimType.Forward, c9Stage2,
            motionType: BulletMotionType.ConstantTurn, turnRate: 40f);

        BulletStructure c10 = Split(
            400f, 3, 1.5f, 5, 0f, BulletSplitAimType.Forward,
            Straight(400f, 1), BulletMotionType.ConstantTurn, 180f,
            childSpawnInterval: 0.6f,
            childAngles: new[] { 90f, 70f, 50f, 30f, 10f },
            angularAcceleration: -6f,
            angularAccelerationDuration: 30f);

        return new[]
        {
            Stage(ChallengeCategory.Advanced, 0, "幾何的弾幕B",
                "組み合わせ\n自機狙い2way→4way→3way\n間隔40→30→40deg、各段階1.6秒\n第3段階は0.5秒予告、弾速160→192→224\n発射周期5秒、脅威度4→3→1",
                Pattern(Projectile(c1Stage1, 5f, 2, 40f,
                    randomize: true))),
            Stage(ChallengeCategory.Advanced, 1, "奇数・偶数交互連射",
                "組み合わせ\n9wayと10wayを0.8秒差で交互発射\n間隔20deg、各周期1.6秒、弾速300\n脅威度：1",
                Pattern(Projectile(300f, 1, 1.6f, 9, 20f,
                    burstCount: 2, burstInterval: 0.8f,
                    randomize: true,
                    reaimDuringBurst: true,
                    burstProjectileCounts: new[] { 9, 10 },
                    burstProjectileIntervals: new[] { 20f, 20f }), 1.6f)),
            Stage(ChallengeCategory.Advanced, 2, "X字加速弾",
                "組み合わせ\n全周囲6方向のX字弾幕\n弾速220～380、加速度20Unit/s²を10秒\n発射周期2秒、脅威度1",
                Pattern(ProjectileExplicit(c3Structures, c3Angles, 2f, true))),
            Stage(ChallengeCategory.Advanced, 3, "減速比例航法誘導弾",
                "組み合わせ\nN=3、角速度制限45deg/s、totalΔθ180deg\n初速600、加速度-400Unit/s²を1秒\n発射周期3秒、脅威度3",
                Pattern(Projectile(Motion(600f, 3,
                    BulletMotionType.ProportionalNavigation, 45f, 3f, 180f,
                    -400f, 1f), 3f, randomize: true))),
            Stage(ChallengeCategory.Advanced, 4, "分裂比例航法誘導弾",
                "組み合わせ\nN=3誘導弾が1.8秒後に自機狙い3wayへ分裂\n間隔45deg、弾速300→250、発射周期3秒\n脅威度4→1",
                Pattern(Projectile(c5, 3f, randomize: true))),
            Stage(ChallengeCategory.Advanced, 5, "分裂連射弾",
                "組み合わせ\n自機狙い4wayが親を残して0.2秒周期分裂\n子弾はベクトル基準±90deg、弾速300\n発射周期4秒、脅威度3→1",
                Pattern(Projectile(c6, 4f, 4, 45f, randomize: true))),
            Stage(ChallengeCategory.Advanced, 6, "2way挟み込みレーザー格子",
                "組み合わせ\n自機狙い2wayが親を残して0.3秒周期で±90degレーザー\n弾速200、射程無限、幅3、予告1秒、照射2秒\n発射周期4秒、脅威度1・5",
                Pattern(Projectile(c7, 4f, 2, 90f, randomize: true))),
            Stage(ChallengeCategory.Advanced, 7, "多段階分裂弾",
                "組み合わせ\n2wayを4段階展開。各中間弾は親を残して1秒周期分裂\n間隔90deg、弾速400/3→100→200/3→100\n発射周期4秒、脅威度4→3→1",
                Pattern(Projectile(c8Stage1, 4f, 2, 90f,
                    randomize: true))),
            Stage(ChallengeCategory.Advanced, 8, "幾何的弾幕A",
                "組み合わせ\n曲がる5wayを3段階展開後1way\n間隔72deg、角速度40deg/s、各段階1.6秒\n弾速160→192→224→224、発射周期8秒\n脅威度4→4→3→1",
                Pattern(Projectile(c9Stage1, 8f, 5, 72f,
                    randomize: true))),
            Stage(ChallengeCategory.Advanced, 9, "螺旋分裂連射弾",
                "組み合わせ\n全周囲6方向の螺旋弾が1.5秒後から0.6秒周期分裂\n角速度180deg/s、角加速度-6deg/s²を30秒\n子弾角度+90/+70/+50/+30/+10、弾速400\n発射周期4秒、脅威度3→1",
                Pattern(Projectile(c10, 4f, 6, 60f, randomize: true))),
        };
    }

    private static BulletHellStageDefinition[] BuildFinalStages()
    {
        BulletStructure acceleratedChild = Straight(150f, 1, 25f, 8f);
        BulletStructure barrierSpiral = new BulletStructure(
            FinalBarrierSpiralSpeed,
            2,
            BulletMotionType.ConstantTurn,
            120f,
            childSpawnFirstDelaySeconds: 2f,
            splitProjectileCount: 3,
            splitAimType: BulletSplitAimType.Forward,
            childStructure: acceleratedChild,
            angularAccelerationDegreesPerSecondSquared: -8f,
            angularAccelerationDurationSeconds: 4f,
            childSpawnIntervalSeconds: 0.8f,
            splitAngleOffsetsDegrees: new[] { 125f, 150f, 160f },
            maximumChildSpawnEvents: 6);

        LaserStructure barrierLaser = new LaserStructure(
            5, 1f, 2f, LaserRange, 4f, 4f);
        BulletStructure laserEmitter = new BulletStructure(
            400f / 3f,
            7,
            childSpawnFirstDelaySeconds: 0.3f,
            splitProjectileCount: 2,
            splitAngleIntervalDegrees: 180f,
            splitAimType: BulletSplitAimType.Forward,
            childLaserStructure: barrierLaser,
            childSpawnIntervalSeconds: 0.3f);
        BulletStructure barrierSplit = Split(
            650f / 4f, 4, 0.8f, 2, 180f,
            BulletSplitAimType.Forward, laserEmitter);

        return new[]
        {
            Stage(ChallengeCategory.Final, 0, "弾幕結界",
                "個体数1、教育モードの逆伝播停止\n周期8-0.005n秒（6～8秒）\nT+2：8発×6way螺旋弾、角速度120deg/s\nT+4～8：各螺旋弾から0.8秒周期で125/150/160degへ3発加速弾\nT+4：自機狙い4way拡散弾、弾速162.5\nT+4.8以降：弾速400/3の各拡散弾から0.3秒周期で±90degレーザー\nレーザー射程無限、幅4、最大脅威度7",
                Pattern(ProjectileRepeatedDirections(
                    barrierSpiral, 8f, 6, 60f, 8),
                    2f, 0.005f, 6f),
                Pattern(Projectile(barrierSplit, 8f, 4, 90f),
                    4f, 0.005f, 6f)),
        };
    }

    private static BulletHellStageDefinition Stage(
        int id,
        string title,
        string details,
        params BulletHellStagePattern[] patterns)
    {
        return new BulletHellStageDefinition(
            ChallengeCategory.Basic,
            id,
            title,
            $"課題A-{id + 1}\n{title}\n{details}",
            patterns);
    }

    private static BulletHellStageDefinition Stage(
        ChallengeCategory category,
        int id,
        string title,
        string details,
        params BulletHellStagePattern[] patterns)
    {
        string prefix = category == ChallengeCategory.Applied ? "B" :
            category == ChallengeCategory.Advanced ? "C" :
            category == ChallengeCategory.Final ? "D" : "A";
        return new BulletHellStageDefinition(
            category,
            id,
            title,
            $"課題{prefix}-{id + 1}\n{title}\n{details}",
            patterns);
    }

    private static BulletHellStagePattern Pattern(
        BulletHellShotDefinition shot,
        float initialDelay = -1f,
        float repeatDecrease = 0f,
        float minimumRepeat = 0f)
    {
        return new BulletHellStagePattern(
            shot,
            initialDelay,
            repeatDecrease,
            minimumRepeat);
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
        BulletStructure[] projectileStructures = null,
        float[] projectileAngles = null,
        BulletStructure[] burstStructures = null,
        bool reaimDuringBurst = false,
        bool randomize = false,
        int[] burstProjectileCounts = null,
        float[] burstProjectileIntervals = null)
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
            projectileStructures,
            projectileAngles,
            burstStructures,
            reaimDuringBurst,
            randomize,
            burstProjectileCounts,
            burstProjectileIntervals);
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
        BulletStructure[] projectileStructures = null,
        float[] projectileAngles = null,
        BulletStructure[] burstStructures = null,
        bool reaimDuringBurst = false,
        bool randomize = false,
        int[] burstProjectileCounts = null,
        float[] burstProjectileIntervals = null)
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
            projectileStructures,
            projectileAngles,
            burstStructures,
            reaimDuringBurst,
            randomize,
            burstProjectileCounts,
            burstProjectileIntervals);
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
        float childSpawnInterval = float.PositiveInfinity,
        float[] childAngles = null,
        float angularAcceleration = 0f,
        float angularAccelerationDuration = 0f,
        int maximumChildSpawnEvents = 0)
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
            angularAccelerationDegreesPerSecondSquared: angularAcceleration,
            angularAccelerationDurationSeconds: angularAccelerationDuration,
            childSpawnIntervalSeconds: childSpawnInterval,
            splitAngleOffsetsDegrees: childAngles,
            maximumChildSpawnEvents: maximumChildSpawnEvents);
    }

    private static BulletStructure SplitLaser(
        float speed,
        int threat,
        float delay,
        int laserCount,
        float laserInterval,
        LaserStructure laser,
        float childSpawnInterval)
    {
        return new BulletStructure(
            speed,
            threat,
            childSpawnFirstDelaySeconds: delay,
            splitProjectileCount: laserCount,
            splitAngleIntervalDegrees: laserInterval,
            splitAimType: BulletSplitAimType.Forward,
            childLaserStructure: laser,
            childSpawnIntervalSeconds: childSpawnInterval);
    }

    private static BulletHellShotDefinition ProjectileExplicit(
        BulletStructure[] structures,
        float[] angles,
        float repeat,
        bool randomize = false)
    {
        return Projectile(
            structures[0],
            repeat,
            structures.Length,
            projectileStructures: structures,
            projectileAngles: angles,
            randomize: randomize);
    }

    private static BulletHellShotDefinition ProjectileRepeatedDirections(
        BulletStructure structure,
        float repeat,
        int directionCount,
        float directionInterval,
        int repetitionsPerDirection)
    {
        int count = Mathf.Max(1, directionCount) *
            Mathf.Max(1, repetitionsPerDirection);
        float[] angles = new float[count];
        int writeIndex = 0;
        for (int directionIndex = 0;
             directionIndex < directionCount;
             directionIndex++)
        {
            float angle = directionIndex * directionInterval;
            for (int repetition = 0;
                 repetition < repetitionsPerDirection;
                 repetition++)
            {
                angles[writeIndex++] = angle;
            }
        }

        return Projectile(
            structure,
            repeat,
            count,
            projectileAngles: angles);
    }

    private static BulletStructure[] BuildBurstStructures(
        int count,
        float initialSpeed,
        float speedStep)
    {
        BulletStructure[] structures = new BulletStructure[Mathf.Max(1, count)];
        for (int index = 0; index < structures.Length; index++)
        {
            structures[index] = Straight(initialSpeed + speedStep * index, 1);
        }

        return structures;
    }

    private static void BuildXProjectiles(
        int radialCount,
        int threat,
        float acceleration,
        float accelerationDuration,
        out float[] angles,
        out BulletStructure[] structures)
    {
        const int rowCount = 2;
        const int bulletsPerRow = 9;
        int count = radialCount * rowCount * bulletsPerRow;
        angles = new float[count];
        structures = new BulletStructure[count];
        int writeIndex = 0;
        for (int radial = 0; radial < radialCount; radial++)
        {
            float radialAngle = radial * (360f / radialCount);
            for (int row = 0; row < rowCount; row++)
            {
                float rowSign = row == 0 ? 1f : -1f;
                for (int index = -4; index <= 4; index++)
                {
                    angles[writeIndex] = radialAngle + rowSign * index * 9f;
                    structures[writeIndex] = Straight(
                        300f + index * 20f,
                        threat,
                        acceleration,
                        accelerationDuration);
                    writeIndex++;
                }
            }
        }
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
