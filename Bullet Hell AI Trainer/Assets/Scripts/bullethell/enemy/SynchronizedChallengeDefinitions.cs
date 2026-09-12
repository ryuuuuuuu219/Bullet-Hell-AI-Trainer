using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class BulletHellStageAttackDefinitions
{
    private static BulletHellStageDefinition[] BuildSynchronizedChallengeAStages()
    {
        BulletHellShotDefinition formationLine = BuildRectangularFormation(
            300f, 1, 2f, BuildFormationCoordinates(FormationShape.Line));
        BulletHellShotDefinition formationColumn = BuildRectangularFormation(
            300f, 1, 2f, BuildFormationCoordinates(FormationShape.Column));
        BulletHellShotDefinition formationV = BuildRectangularFormation(
            300f, 1, 2f, BuildFormationCoordinates(FormationShape.V));
        BulletHellShotDefinition formationInverseV = BuildRectangularFormation(
            300f, 1, 2f, BuildFormationCoordinates(FormationShape.InverseV));
        BulletHellShotDefinition formationX = BuildRectangularFormation(
            300f, 1, 2f, BuildFormationCoordinates(FormationShape.X));

        BulletStructure[] accelerationDistribution = new BulletStructure[5];
        for (int index = 0; index < accelerationDistribution.Length; index++)
        {
            accelerationDistribution[index] = Straight(
                50f, 1, 100f * index, 2f);
        }

        BulletStructure[] jerkProjectiles = new BulletStructure[5];
        for (int index = 0; index < jerkProjectiles.Length; index++)
        {
            jerkProjectiles[index] = Straight(
                -500f,
                1,
                300f,
                30f,
                -55f,
                30f,
                5f);
        }

        BulletStructure playerAimedSplit = Split(
            200f,
            3,
            1.2f,
            3,
            8f,
            BulletSplitAimType.PlayerAimed,
            Straight(250f, 1),
            maximumChildSpawnEvents: 1);
        BulletStructure repeatedVectorSplit = new BulletStructure(
            300f,
            3,
            childSpawnFirstDelaySeconds: 0.2f,
            splitProjectileCount: 2,
            splitAngleIntervalDegrees: 180f,
            splitAimType: BulletSplitAimType.Forward,
            childStructure: Straight(300f, 1),
            childSpawnIntervalSeconds: 0.2f);

        return new[]
        {
            Stage(0, "自機狙い奇数way",
                "単独認識\n自機狙い5way、12deg間隔\n発射周期2秒、初速400Unit/s、脅威度1",
                Pattern(Projectile(400f, 1, 2f, 5, 12f), 0f)),
            Stage(1, "自機狙い偶数way",
                "単独認識\n自機狙い2way、12deg間隔\n発射周期2秒、初速400Unit/s、脅威度1",
                Pattern(Projectile(400f, 1, 2f, 2, 12f), 0f)),
            Stage(2, "全周",
                "単独認識\n全周8way、45deg間隔\n発射周期0.5秒、初速100Unit/s、脅威度1",
                Pattern(Projectile(100f, 1, 0.5f, 8, 45f), 0f)),
            Stage(3, "編隊（矩形補正一字）",
                "単独認識\n9発、sx=5・sy=0、基準初速300Unit/s\n発射周期2秒",
                Pattern(formationLine, 0f)),
            Stage(4, "編隊（矩形補正I字）",
                "単独認識\n9発、sx=0・sy=5、基準初速300Unit/s\n発射周期2秒",
                Pattern(formationColumn, 0f)),
            Stage(5, "編隊（矩形補正V字）",
                "単独認識\n9発、x=i・y=-abs(i)、sx=5・sy=5\n発射周期2秒",
                Pattern(formationV, 0f)),
            Stage(6, "編隊（矩形補正逆V字）",
                "単独認識\n9発、x=i・y=abs(i)、sx=5・sy=5\n発射周期2秒",
                Pattern(formationInverseV, 0f)),
            Stage(7, "編隊（矩形補正X字）",
                "単独認識\n9発×2枝、x=i・y=±i、sx=5・sy=5\n発射周期2秒",
                Pattern(formationX, 0f)),
            Stage(8, "角速度",
                "単独認識\n自機狙い1way、角速度30deg/s\n発射周期2秒、初速400Unit/s、脅威度2",
                Pattern(Projectile(Motion(400f, 2,
                    BulletMotionType.ConstantTurn, 30f), 2f), 0f)),
            Stage(9, "減速",
                "単独認識\n初速500Unit/s、加速度-250Unit/s²を1.5秒\n発射周期2秒、脅威度1",
                Pattern(Projectile(Straight(500f, 1, -250f, 1.5f), 2f), 0f)),
            Stage(10, "加速",
                "単独認識\n初速150Unit/s、加速度+200Unit/s²を2秒\n発射周期2秒、脅威度1",
                Pattern(Projectile(Straight(150f, 1, 200f, 2f), 2f), 0f)),
            Stage(11, "加速度分布",
                "単独認識\n同一射線5発、初速50Unit/s\n加速度0/100/200/300/400Unit/s²を2秒",
                Pattern(Projectile(
                    accelerationDistribution[0],
                    2f,
                    5,
                    projectileStructures: accelerationDistribution), 0f)),
            Stage(12, "躍度",
                "単独認識\n同一射線5発、初速-500Unit/s、初期加速度+300Unit/s²\n線形躍度-55Unit/s³を最大寿命まで、距離倍率5",
                Pattern(Projectile(
                    jerkProjectiles[0],
                    2f,
                    5,
                    projectileStructures: jerkProjectiles), 0f)),
            Stage(13, "純粋追尾誘導弾",
                "単独認識\n純粋追尾3way、0/±25deg\n初速300Unit/s、角速度制限45deg/s、totalΔθ90deg",
                Pattern(Projectile(Motion(300f, 3,
                    BulletMotionType.Homing, 45f,
                    totalTurnAngle: 90f), 3f, 3, 25f), 0f)),
            Stage(14, "N=1比例航法誘導弾",
                "単独認識\nN=1、角速度制限12deg/s、totalΔθ90deg\n発射周期2秒、初速500Unit/s",
                Pattern(Projectile(Motion(500f, 3,
                    BulletMotionType.ProportionalNavigation,
                    12f, 1f, 90f), 2f), 0f)),
            Stage(15, "N=3比例航法誘導弾",
                "単独認識\nN=3、角速度制限18deg/s、totalΔθ120deg\n発射周期2秒、初速400Unit/s",
                Pattern(Projectile(Motion(400f, 3,
                    BulletMotionType.ProportionalNavigation,
                    18f, 3f, 120f), 2f), 0f)),
            Stage(16, "クロック式N=3比例航法誘導弾",
                "単独認識\n自機狙い1way、誘導更新0.5秒\n初速200Unit/s、角速度制限45deg/s、totalΔθ180deg",
                Pattern(Projectile(Motion(200f, 3,
                    BulletMotionType.ProportionalNavigation,
                    45f, 3f, 180f,
                    guidanceCommandInterval: 0.5f), 2f), 0f)),
            Stage(17, "連射",
                "単独認識\n連射開始時の自機照準を固定\n5連射、間隔0.2秒、発射周期2秒、初速400Unit/s",
                Pattern(Projectile(400f, 1, 2f,
                    burstCount: 5, burstInterval: 0.2f), 0f)),
            Stage(18, "予告線付き弾",
                "単独認識\n予告0.5秒の高速直進弾\n発射周期2秒、初速800Unit/s、脅威度4",
                Pattern(Projectile(800f, 4, 2f, warning: 0.5f), 0f)),
            Stage(19, "レーザー",
                "単独認識\n自機狙いレーザー、予告1秒、照射2秒、射程無限\n発射周期2秒、脅威度5",
                Pattern(BulletHellShotDefinition.CreateLaser(
                    new LaserStructure(5, 1f, 2f, LaserRange, 3f, 3f),
                    2f), 0f)),
            Stage(20, "分裂（自機狙い）",
                "単独認識\n親弾初速200Unit/s、1.2秒後に自機狙い3wayへ1回分裂\n子弾8deg間隔、初速250Unit/s",
                Pattern(Projectile(playerAimedSplit, 2f), 0f)),
            Stage(21, "反復分裂（ベクトル基準）",
                "単独認識\n親弾1way、初速300Unit/s\n0.2秒後から0.2秒周期でベクトル基準±90degへ反復分裂",
                Pattern(Projectile(repeatedVectorSplit, 4f), 0f)),
        };
    }

    private static BulletHellStageDefinition[] BuildSynchronizedChallengeBStages()
    {
        BulletStructure[] speedBurst = BuildBurstStructures(8, 200f, 30f);
        BulletStructure[] angularDistribution = new BulletStructure[8];
        for (int index = 0; index < angularDistribution.Length; index++)
        {
            float ratio = index / (angularDistribution.Length - 1f);
            angularDistribution[index] = Motion(
                400f,
                2,
                BulletMotionType.ConstantTurn,
                Mathf.Lerp(-30f, 30f, ratio),
                angularAcceleration: Mathf.Lerp(-4f, 4f, ratio),
                angularAccelerationDuration: 15f);
        }

        const int periodicShotCount = 4;
        float[] periodicOffsets = new float[periodicShotCount];
        for (int index = 0; index < periodicShotCount; index++)
        {
            periodicOffsets[index] = CalculatePeriodicOffset(index * 0.5f);
        }

        BulletStructure angularJerk = Motion(
            50f,
            2,
            BulletMotionType.ConstantTurn,
            110f,
            angularAcceleration: -120f,
            angularAccelerationDuration: float.PositiveInfinity,
            angularJerk: 60f,
            angularJerkDuration: float.PositiveInfinity);
        BulletStructure explodingGuidance = new BulletStructure(
            300f,
            4,
            BulletMotionType.ProportionalNavigation,
            45f,
            3f,
            1.5f,
            3,
            45f,
            BulletSplitAimType.PlayerAimed,
            Straight(250f, 1),
            totalTurnAngleDegrees: 180f,
            maximumChildSpawnEvents: 1);
        BulletStructure forwardSplit = Split(
            450f,
            3,
            1f,
            3,
            45f,
            BulletSplitAimType.Forward,
            Straight(250f, 1),
            maximumChildSpawnEvents: 1,
            releaseParentAfterFinalChildSpawn: false);
        BulletStructure repeatedPlayerSplit = new BulletStructure(
            250f,
            3,
            childSpawnFirstDelaySeconds: 1f,
            splitProjectileCount: 3,
            splitAngleIntervalDegrees: 45f,
            splitAimType: BulletSplitAimType.PlayerAimed,
            childStructure: Straight(250f, 1),
            childSpawnIntervalSeconds: 1f);

        return new[]
        {
            Stage(ChallengeCategory.B, 0,
                "弾速分布(n>m)(n>1)(m>1)（5*2way）",
                "組み合わせ\n自機狙い5way・2way、各5deg間隔\n5way初速400、2way初速300Unit/s、発射周期2秒",
                Pattern(Projectile(400f, 1, 2f, 5, 5f), 0f),
                Pattern(Projectile(300f, 1, 2f, 2, 5f), 0f)),
            Stage(ChallengeCategory.B, 1, "弾速分布(2n+1)*m",
                "組み合わせ\n自機狙い9wayを2群同時発射\n8deg・250Unit/sと12deg・400Unit/s、発射周期2秒",
                Pattern(Projectile(250f, 1, 2f, 9, 8f), 0f),
                Pattern(Projectile(400f, 1, 2f, 9, 12f), 0f)),
            Stage(ChallengeCategory.B, 2, "角加速度",
                "強化\n自機狙い1way、初期角速度0deg/s\n角加速度+5deg/s²を15秒、初速400Unit/s、発射周期2秒",
                Pattern(Projectile(Motion(400f, 2,
                    BulletMotionType.ConstantTurn, 0f,
                    angularAcceleration: 5f,
                    angularAccelerationDuration: 15f), 2f), 0f)),
            Stage(ChallengeCategory.B, 3,
                "特異点のある角加速度弾（減衰螺旋弾）",
                "強化\n全周16way、22.5deg間隔\n初速400Unit/s、角速度150deg/s、角加速度-10deg/s²を15秒",
                Pattern(Projectile(Motion(400f, 2,
                    BulletMotionType.ConstantTurn, 150f,
                    angularAcceleration: -10f,
                    angularAccelerationDuration: 15f),
                    2f, 16, 22.5f), 0f)),
            Stage(ChallengeCategory.B, 4, "角躍度",
                "強化\n対自機ベクトル基準、初期偏差-60deg、初速50Unit/s\n初期角速度+110deg/s、初期角加速度-120deg/s²、角躍度+60deg/s³を最大寿命まで",
                Pattern(Projectile(angularJerk, 2f, aimOffset: -60f), 0f)),
            Stage(ChallengeCategory.B, 5, "偏差の周期変化",
                "組み合わせ\n0.5秒周期、自機狙い+sin(T^1.1×180deg)×15deg\n初速300Unit/s、角加速度と反復分裂は除外",
                Pattern(Projectile(300f, 1, 2f,
                    burstCount: periodicShotCount,
                    burstInterval: 0.5f,
                    burstOffsets: periodicOffsets,
                    reaimDuringBurst: true), 0f)),
            Stage(ChallengeCategory.B, 6, "速度変化連射",
                "組み合わせ\n自機狙いを各射撃で更新、8連射、間隔0.2秒\n初速200+30n Unit/s、発射周期2秒",
                Pattern(Projectile(Straight(200f, 1), 2f,
                    burstCount: 8,
                    burstInterval: 0.2f,
                    burstStructures: speedBurst,
                    reaimDuringBurst: true), 0f)),
            Stage(ChallengeCategory.B, 7, "薙ぎ払い連射",
                "組み合わせ\n連射開始時の照準を固定、偏差-18→+18deg\n8連射、間隔0.1秒、初速200+30n Unit/s、発射周期2秒",
                Pattern(Projectile(Straight(200f, 1), 2f,
                    burstCount: 8,
                    burstInterval: 0.1f,
                    burstOffsets: Sweep(8, -18f, 18f),
                    burstStructures: speedBurst), 0f)),
            Stage(ChallengeCategory.B, 8, "角加速度分布＋編隊",
                "組み合わせ\n同一射線8発、初速400Unit/s\n角速度-30～+30deg/s、角加速度-4～+4deg/s²を15秒",
                Pattern(Projectile(
                    angularDistribution[0],
                    2f,
                    8,
                    projectileStructures: angularDistribution), 0f)),
            Stage(ChallengeCategory.B, 9, "奇数way誘導弾",
                "組み合わせ\n純粋追尾3way、0/±25deg\n初速300Unit/s、角速度制限45deg/s、totalΔθ90deg、発射周期3秒",
                Pattern(Projectile(Motion(300f, 3,
                    BulletMotionType.Homing, 45f,
                    totalTurnAngle: 90f), 3f, 3, 25f), 0f)),
            Stage(ChallengeCategory.B, 10, "加速誘導弾",
                "組み合わせ\nN=3比例航法、初速300Unit/s、加速度+400Unit/s²を1秒\n角速度制限45deg/s、totalΔθ180deg、発射周期3秒",
                Pattern(Projectile(Motion(300f, 3,
                    BulletMotionType.ProportionalNavigation,
                    45f, 3f, 180f, 400f, 1f), 3f), 0f)),
            Stage(ChallengeCategory.B, 11, "減速誘導弾",
                "組み合わせ\nN=3比例航法、初速600Unit/s、加速度-400Unit/s²を1秒\n角速度制限45deg/s、totalΔθ180deg、発射周期3秒",
                Pattern(Projectile(Motion(600f, 3,
                    BulletMotionType.ProportionalNavigation,
                    45f, 3f, 180f, -400f, 1f), 3f), 0f)),
            Stage(ChallengeCategory.B, 12, "炸裂誘導弾",
                "組み合わせ\nN=3比例航法の親弾が1.5秒後に自機狙い3wayへ分裂\n親弾初速300、子弾初速250Unit/s、45deg間隔、発射周期3秒",
                Pattern(Projectile(explodingGuidance, 3f), 0f)),
            Stage(ChallengeCategory.B, 13, "分裂（ベクトル基準3way）",
                "組み合わせ\n親弾初速450Unit/s、1秒後に進行ベクトル基準3wayへ1回分裂\n子弾45deg間隔、初速250Unit/s、親弾を残す",
                Pattern(Projectile(forwardSplit, 2f), 0f)),
            Stage(ChallengeCategory.B, 14, "反復分裂（自機狙い3way）",
                "組み合わせ\n親弾初速250Unit/s、1秒後から1秒周期で自機狙い3wayを反復生成\n子弾45deg間隔、初速250Unit/s、親弾を残す",
                Pattern(Projectile(repeatedPlayerSplit, 2f), 0f)),
        };
    }

    private static BulletHellStageDefinition[] BuildSynchronizedChallengeCStages()
    {
        BulletStructure[] angularVelocityDistribution =
            BuildAngularDistribution(includeAngularAcceleration: false);
        BulletStructure[] angularAccelerationDistribution =
            BuildAngularDistribution(includeAngularAcceleration: true);
        BulletHellShotDefinition twoGroupDistribution = BuildGroupedWays(
            2f,
            new[] { 10, 9 },
            new[] { 8f, 8f },
            new[] { 300f, 350f },
            randomize: true);
        BulletHellShotDefinition threeGroupDistribution = BuildGroupedWays(
            2f,
            new[] { 10, 9, 8 },
            new[] { 8f, 8f, 8f },
            new[] { 300f, 350f, 400f },
            randomize: true);

        const int dynamicBurstCount = 8;
        const float dynamicInterval = 0.8f;
        float[] dynamicOffsets = new float[dynamicBurstCount];
        int[] dynamicCounts = new int[dynamicBurstCount];
        float[] dynamicIntervals = new float[dynamicBurstCount];
        for (int index = 0; index < dynamicBurstCount; index++)
        {
            float periodTime = index * dynamicInterval;
            int count = Mathf.CeilToInt(periodTime / 3f) + 8;
            dynamicOffsets[index] = CalculatePeriodicOffset(periodTime);
            dynamicCounts[index] = count;
            dynamicIntervals[index] = 360f / count;
        }

        return new[]
        {
            Stage(ChallengeCategory.C, 0, "角速度分布",
                "組み合わせ\n同一射線8発、角速度-30～+30deg/s、角加速度0\n初速400Unit/s、発射周期2秒、弾速・周期±15%",
                Pattern(Projectile(
                    angularVelocityDistribution[0],
                    2f,
                    8,
                    projectileStructures: angularVelocityDistribution,
                    randomize: true), 0f)),
            Stage(ChallengeCategory.C, 1, "角加速度分布",
                "組み合わせ\n同一射線8発、初期角速度0、角加速度-4～+4deg/s²を15秒\n初速400Unit/s、発射周期2秒、弾速・周期±15%",
                Pattern(Projectile(
                    angularAccelerationDistribution[0],
                    2f,
                    8,
                    projectileStructures: angularAccelerationDistribution,
                    randomize: true), 0f)),
            Stage(ChallengeCategory.C, 2, "弾速分布(n+m)",
                "組み合わせ\n自機狙い10way・9way、各8deg間隔\n初速300・350Unit/s、発射周期2秒、弾速・周期±15%",
                Pattern(twoGroupDistribution, 0f)),
            Stage(ChallengeCategory.C, 3, "弾速分布(n+m+l)",
                "組み合わせ\n自機狙い10way・9way・8way、各8deg間隔\n初速300・350・400Unit/s、発射周期2秒、弾速・周期±15%",
                Pattern(threeGroupDistribution, 0f)),
            Stage(ChallengeCategory.C, 4, "連射(2n+1)(2n)",
                "組み合わせ\n9wayと10wayを0.8秒差で交互連射、各20deg間隔\n初速300Unit/s、周期1.6秒、弾速・周期±15%",
                Pattern(Projectile(
                    300f,
                    1,
                    1.6f,
                    9,
                    20f,
                    burstCount: 2,
                    burstInterval: 0.8f,
                    reaimDuringBurst: true,
                    randomize: true,
                    burstProjectileCounts: new[] { 9, 10 },
                    burstProjectileIntervals: new[] { 20f, 20f }), 0f)),
            Stage(ChallengeCategory.C, 5, "動的Nway",
                "組み合わせ\n0.8秒周期、N=ceil(T/3)+8、全周360/N deg間隔\n偏差sin(T^1.1×180deg)×15deg、初速300Unit/s、弾速・周期±15%",
                Pattern(Projectile(
                    300f,
                    1,
                    6f,
                    8,
                    45f,
                    burstCount: dynamicBurstCount,
                    burstInterval: dynamicInterval,
                    burstOffsets: dynamicOffsets,
                    reaimDuringBurst: true,
                    randomize: true,
                    burstProjectileCounts: dynamicCounts,
                    burstProjectileIntervals: dynamicIntervals), 0f)),
        };
    }

    private enum FormationShape
    {
        Line,
        Column,
        V,
        InverseV,
        X,
    }

    private static List<Vector2> BuildFormationCoordinates(FormationShape shape)
    {
        List<Vector2> coordinates = new List<Vector2>();
        int branchCount = shape == FormationShape.X ? 2 : 1;
        for (int branch = 0; branch < branchCount; branch++)
        {
            float branchSign = branch == 0 ? -1f : 1f;
            for (int index = -4; index <= 4; index++)
            {
                float x = shape == FormationShape.Column ? 0f : index;
                float y;
                switch (shape)
                {
                    case FormationShape.Column:
                        y = index;
                        break;
                    case FormationShape.V:
                        y = -Mathf.Abs(index);
                        break;
                    case FormationShape.InverseV:
                        y = Mathf.Abs(index);
                        break;
                    case FormationShape.X:
                        y = branchSign * index;
                        break;
                    default:
                        y = 0f;
                        break;
                }
                coordinates.Add(new Vector2(x, y));
            }
        }
        return coordinates;
    }

    private static BulletHellShotDefinition BuildRectangularFormation(
        float baseSpeed,
        int threat,
        float repeat,
        IReadOnlyList<Vector2> coordinates)
    {
        BulletStructure[] structures = new BulletStructure[coordinates.Count];
        float[] angles = new float[coordinates.Count];
        for (int index = 0; index < coordinates.Count; index++)
        {
            Vector2 coordinate = coordinates[index];
            float sx = coordinate.x == 0f ? 0f : 5f;
            float sy = coordinate.y == 0f ? 0f : 5f;
            float forwardSpeed = baseSpeed + sy * coordinate.y;
            float sideSpeed = sx * coordinate.x;
            structures[index] = Straight(
                Mathf.Sqrt(forwardSpeed * forwardSpeed +
                           sideSpeed * sideSpeed),
                threat);
            angles[index] = Mathf.Atan2(sideSpeed, forwardSpeed) *
                Mathf.Rad2Deg;
        }
        return ProjectileExplicit(structures, angles, repeat);
    }

    private static BulletStructure[] BuildAngularDistribution(
        bool includeAngularAcceleration)
    {
        BulletStructure[] structures = new BulletStructure[8];
        for (int index = 0; index < structures.Length; index++)
        {
            float ratio = index / (structures.Length - 1f);
            structures[index] = Motion(
                400f,
                2,
                BulletMotionType.ConstantTurn,
                includeAngularAcceleration
                    ? 0f
                    : Mathf.Lerp(-30f, 30f, ratio),
                angularAcceleration: includeAngularAcceleration
                    ? Mathf.Lerp(-4f, 4f, ratio)
                    : 0f,
                angularAccelerationDuration: includeAngularAcceleration
                    ? 15f
                    : 0f);
        }
        return structures;
    }

    private static BulletHellShotDefinition BuildGroupedWays(
        float repeat,
        IReadOnlyList<int> counts,
        IReadOnlyList<float> intervals,
        IReadOnlyList<float> speeds,
        bool randomize)
    {
        int totalCount = 0;
        for (int index = 0; index < counts.Count; index++)
        {
            totalCount += counts[index];
        }
        BulletStructure[] structures = new BulletStructure[totalCount];
        float[] angles = new float[totalCount];
        int writeIndex = 0;
        for (int group = 0; group < counts.Count; group++)
        {
            float center = (counts[group] - 1) * 0.5f;
            for (int projectile = 0; projectile < counts[group]; projectile++)
            {
                structures[writeIndex] = Straight(speeds[group], 1);
                angles[writeIndex] = (projectile - center) * intervals[group];
                writeIndex++;
            }
        }
        return Projectile(
            structures[0],
            repeat,
            totalCount,
            projectileStructures: structures,
            projectileAngles: angles,
            randomize: randomize);
    }

    private static float CalculatePeriodicOffset(float periodTimeSeconds)
    {
        return Mathf.Sin(
            Mathf.Pow(periodTimeSeconds, 1.1f) * 180f * Mathf.Deg2Rad) *
            15f;
    }
}
