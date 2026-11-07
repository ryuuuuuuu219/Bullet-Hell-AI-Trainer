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
                "中央に自機を直接狙う弾を含む、奇数本の扇状弾を学習しよう。",
                Pattern(Projectile(400f, 1, 2f, 5, 12f), 0f)),
            Stage(1, "自機狙い偶数way",
                "中央に自機を直接狙う弾を含まない、偶数本の扇状弾を学習しよう。\n" +
                "自機を挟み込むように展開されるため、動かないのも有効。",
                Pattern(Projectile(400f, 1, 2f, 2, 12f), 0f)),
            Stage(2, "全周",
                "Bossを中心として全方向へ発射される8way弾を学習しよう。",
                Pattern(Projectile(100f, 1, 0.5f, 8, 45f), 0f)),
            Stage(3, "編隊（矩形補正一字）",
                "同一点から横一列へ広がる一字編隊を学習しよう。",
                Pattern(formationLine, 0f)),
            Stage(4, "編隊（矩形補正I字）",
                "同一点から進行方向の縦一列へ広がるI字編隊を学習しよう。",
                Pattern(formationColumn, 0f)),
            Stage(5, "編隊（矩形補正V字）",
                "同一点からV字へ広がる編隊を学習しよう。",
                Pattern(formationV, 0f)),
            Stage(6, "編隊（矩形補正逆V字）",
                "同一点から逆V字へ広がる編隊を学習しよう。\n" +
                "V字編隊とは違い、両翼が早く到達してくるのに注意。",
                Pattern(formationInverseV, 0f)),
            Stage(7, "編隊（矩形補正X字）",
                "同一点から二つの枝に分かれ、X字へ広がる編隊を学習しよう。",
                Pattern(formationX, 0f)),
            Stage(8, "角速度",
                "一定の角速度で軌道が曲がる弾を学習しよう。",
                Pattern(Projectile(Motion(400f, 2,
                    BulletMotionType.ConstantTurn, 30f), 2f), 0f)),
            Stage(9, "減速",
                "高速で接近したあとに減速する弾を学習しよう。",
                Pattern(Projectile(Straight(500f, 1, -250f, 1.5f), 2f), 0f)),
            Stage(10, "加速",
                "低速で発射されたあとに加速する弾を学習しよう。",
                Pattern(Projectile(Straight(150f, 1, 200f, 2f), 2f), 0f)),
            Stage(11, "加速度分布",
                "同じ方向へ発射された弾が、加速度の差によって前後へ分かれる動きを学習しよう。",
                Pattern(Projectile(
                    accelerationDistribution[0],
                    2f,
                    5,
                    projectileStructures: accelerationDistribution), 0f)),
            Stage(12, "躍度",
                "加速度が時間変化し、進行方向を二度反転する直進弾を学習しよう。\n" +
                "高速で戻ってくる動きに注意。",
                Pattern(Projectile(
                    jerkProjectiles[0],
                    2f,
                    5,
                    projectileStructures: jerkProjectiles), 0f)),
            Stage(13, "純粋追尾誘導弾",
                "自機の現在位置を追い続ける誘導弾を学習しよう。",
                Pattern(Projectile(Motion(300f, 3,
                    BulletMotionType.Homing, 45f,
                    totalTurnAngle: 90f), 3f, 3, 25f), 0f)),
            Stage(14, "N=1比例航法誘導弾",
                "航法定数N=1の比例航法で自機を追う誘導弾を学習しよう。",
                Pattern(Projectile(Motion(500f, 3,
                    BulletMotionType.ProportionalNavigation,
                    12f, 1f, 90f), 2f), 0f)),
            Stage(15, "N=3比例航法誘導弾",
                "航法定数N=3の比例航法で自機を追う誘導弾を学習しよう。",
                Pattern(Projectile(Motion(400f, 3,
                    BulletMotionType.ProportionalNavigation,
                    18f, 3f, 120f), 2f), 0f)),
            Stage(16, "クロック式N=3比例航法誘導弾",
                "一定周期で誘導方向を更新する、N=3の比例航法誘導弾を学習しよう。",
                Pattern(Projectile(Motion(200f, 3,
                    BulletMotionType.ProportionalNavigation,
                    45f, 3f, 180f,
                    guidanceCommandInterval: 0.5f), 2f), 0f)),
            Stage(17, "連射",
                "連射開始時の照準方向を固定した連続射撃を学習しよう。",
                Pattern(Projectile(400f, 1, 2f,
                    burstCount: 5, burstInterval: 0.2f), 0f)),
            Stage(18, "予告線付き弾",
                "発射前に予告線が表示される高速弾を学習しよう。\n" +
                "常に動いておく、という戦術も有効。",
                Pattern(Projectile(800f, 4, 2f, warning: 0.5f), 0f)),
            Stage(19, "レーザー",
                "予告後に一定時間照射されるレーザーを学習しよう。",
                Pattern(BulletHellShotDefinition.CreateLaser(
                    new LaserStructure(5, 1f, 2f, LaserRange, 3f, 3f),
                    2f), 0f)),
            Stage(20, "分裂（自機狙い）",
                "親弾が途中で一度分裂し、分裂地点から自機を狙う子弾を学習しよう。",
                Pattern(Projectile(playerAimedSplit, 2f), 0f)),
            Stage(21, "反復分裂（ベクトル基準）",
                "親弾を残したまま、親弾の進行方向を基準とする子弾を繰り返し生成する弾を学習しよう。",
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
                "要素：自機狙い奇数way・自機狙い偶数way\n" +
                "自機狙い5wayと2wayを、異なる弾速で同時に発射する弾幕を学習しよう。",
                Pattern(Projectile(400f, 1, 2f, 5, 5f), 0f),
                Pattern(Projectile(300f, 1, 2f, 2, 5f), 0f)),
            Stage(ChallengeCategory.B, 1, "弾速分布(2n+1)*m",
                "要素：自機狙い奇数way・弾速分布\n" +
                "同じ奇数wayを、異なる弾速の複数群として同時に発射する弾幕を学習しよう。",
                Pattern(Projectile(250f, 1, 2f, 9, 8f), 0f),
                Pattern(Projectile(400f, 1, 2f, 9, 12f), 0f)),
            Stage(ChallengeCategory.B, 2, "角加速度",
                "要素：角加速度\n" +
                "角加速度によって角速度が時間変化する弾を学習しよう。",
                Pattern(Projectile(Motion(400f, 2,
                    BulletMotionType.ConstantTurn, 0f,
                    angularAcceleration: 5f,
                    angularAccelerationDuration: 15f), 2f), 0f)),
            Stage(ChallengeCategory.B, 3,
                "特異点のある角加速度弾（減衰螺旋弾）",
                "要素：特異点のある角加速度弾\n" +
                "曲率が変化しながら一点付近へ滞留する、減衰螺旋弾を学習しよう。",
                Pattern(Projectile(Motion(400f, 2,
                    BulletMotionType.ConstantTurn, 150f,
                    angularAcceleration: -10f,
                    angularAccelerationDuration: 15f),
                    2f, 16, 22.5f), 0f)),
            Stage(ChallengeCategory.B, 4, "角躍度",
                "要素：特異点・角加速度・角躍度\n" +
                "角躍度によって曲がり方が連続変化し、発射時の対自機ベクトルを3回横切る弾を学習しよう。",
                Pattern(Projectile(angularJerk, 2f, aimOffset: -60f), 0f)),
            Stage(ChallengeCategory.B, 5, "偏差の周期変化",
                "要素：時間に伴う偏差変化・周期変化\n" +
                "時間に応じて、自機狙い方向からの偏差角が周期的に変化する射撃を学習しよう。",
                Pattern(Projectile(300f, 1, 2f,
                    burstCount: periodicShotCount,
                    burstInterval: 0.5f,
                    burstOffsets: periodicOffsets,
                    reaimDuringBurst: true), 0f)),
            Stage(ChallengeCategory.B, 6, "速度変化連射",
                "要素：連射・時間に伴う初速変化\n" +
                "射撃ごとに自機を再照準し、後の弾ほど初速が増える連射を学習しよう。",
                Pattern(Projectile(Straight(200f, 1), 2f,
                    burstCount: 8,
                    burstInterval: 0.2f,
                    burstStructures: speedBurst,
                    reaimDuringBurst: true), 0f)),
            Stage(ChallengeCategory.B, 7, "薙ぎ払い連射",
                "要素：連射・時間に伴う偏差変化\n" +
                "固定した自機照準を基準に、偏差角を片側から反対側へ動かす連射を学習しよう。",
                Pattern(Projectile(Straight(200f, 1), 2f,
                    burstCount: 8,
                    burstInterval: 0.1f,
                    burstOffsets: Sweep(8, -18f, 18f),
                    burstStructures: speedBurst), 0f)),
            Stage(ChallengeCategory.B, 8, "角加速度分布＋編隊",
                "要素：角加速度分布・編隊\n" +
                "同じ射線から出た複数の弾が、角速度と角加速度の差によって別々の軌道へ広がる弾幕を学習しよう。",
                Pattern(Projectile(
                    angularDistribution[0],
                    2f,
                    8,
                    projectileStructures: angularDistribution), 0f)),
            Stage(ChallengeCategory.B, 9, "奇数way誘導弾",
                "要素：自機狙い奇数way・純粋追尾誘導弾\n" +
                "3wayで発射され、それぞれが自機を追い続ける純粋追尾弾を学習しよう。",
                Pattern(Projectile(Motion(300f, 3,
                    BulletMotionType.Homing, 45f,
                    totalTurnAngle: 90f), 3f, 3, 25f), 0f)),
            Stage(ChallengeCategory.B, 10, "加速誘導弾",
                "要素：加速・N=3比例航法誘導弾\n" +
                "加速しながら比例航法で自機を追う誘導弾を学習しよう。",
                Pattern(Projectile(Motion(300f, 3,
                    BulletMotionType.ProportionalNavigation,
                    45f, 3f, 180f, 400f, 1f), 3f), 0f)),
            Stage(ChallengeCategory.B, 11, "減速誘導弾",
                "要素：減速・N=3比例航法誘導弾\n" +
                "減速しながら比例航法で自機を追う誘導弾を学習しよう。",
                Pattern(Projectile(Motion(600f, 3,
                    BulletMotionType.ProportionalNavigation,
                    45f, 3f, 180f, -400f, 1f), 3f), 0f)),
            Stage(ChallengeCategory.B, 12, "炸裂誘導弾",
                "要素：N=3比例航法誘導弾・分裂（自機狙い）\n" +
                "比例航法で追尾する親弾が、途中で自機狙い3wayへ分裂する弾幕を学習しよう。",
                Pattern(Projectile(explodingGuidance, 3f), 0f)),
            Stage(ChallengeCategory.B, 13, "分裂（ベクトル基準3way）",
                "要素：分裂（ベクトル基準3way）\n" +
                "親弾の進行方向を基準として3wayへ分裂する弾を学習しよう。",
                Pattern(Projectile(forwardSplit, 2f), 0f)),
            Stage(ChallengeCategory.B, 14, "反復分裂（自機狙い3way）",
                "要素：反復分裂（自機狙い3way）\n" +
                "親弾を残し、分裂地点から自機を狙う3wayを繰り返し生成する弾を学習しよう。",
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
                "要素：角速度分布\n" +
                "同じ射線から発射された8発が、それぞれ異なる角速度で左右へ曲がる弾幕を学習しよう。",
                Pattern(Projectile(
                    angularVelocityDistribution[0],
                    2f,
                    8,
                    projectileStructures: angularVelocityDistribution,
                    randomize: true), 0f)),
            Stage(ChallengeCategory.C, 1, "角加速度分布",
                "要素：角加速度分布\n" +
                "同じ射線から発射された8発が、それぞれ異なる角加速度で軌道を変える弾幕を学習しよう。",
                Pattern(Projectile(
                    angularAccelerationDistribution[0],
                    2f,
                    8,
                    projectileStructures: angularAccelerationDistribution,
                    randomize: true), 0f)),
            Stage(ChallengeCategory.C, 2, "弾速分布(n+m)",
                "要素：自機狙い偶数way・自機狙い奇数way・弾速分布\n" +
                "自機狙い10wayと9wayを、異なる弾速で同時に発射する弾幕を学習しよう。",
                Pattern(twoGroupDistribution, 0f)),
            Stage(ChallengeCategory.C, 3, "弾速分布(n+m+l)",
                "要素：自機狙い偶数way・自機狙い奇数way・弾速分布\n" +
                "way数と弾速が異なる3群を、同時に発射する弾幕を学習しよう。",
                Pattern(threeGroupDistribution, 0f)),
            Stage(ChallengeCategory.C, 4, "連射(2n+1)(2n)",
                "要素：連射・自機狙い偶数way・自機狙い奇数way\n" +
                "奇数wayと偶数wayを、時間差で交互に連射する弾幕を学習しよう。",
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
                "要素：動的Nway・自機狙い奇数way・自機狙い偶数way\n" +
                "時間経過に伴って弾数が増え、発射方向も周期的に変化する全周Nway弾を学習しよう。",
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

    private static BulletHellStageDefinition BuildChallengeD4Stage()
    {
        List<BulletHellStagePattern> patterns = new List<BulletHellStagePattern>();
        FormationShape[] shapes =
        {
            FormationShape.Column, FormationShape.V, FormationShape.X,
        };
        float[][] times =
        {
            new[] { 0f, 0.33f, 0.66f },
            new[] { 1f, 1.66f, 2.33f },
            new[] { 2f, 2.66f, 3.33f },
        };
        float[] expansionRates = { 10f, 11f, 13f };
        float[] offsets = { 0f, -10f, 10f };
        for (int shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
        {
            List<Vector2> coordinates = BuildFormationCoordinates(
                shapes[shapeIndex],
                radius: shapeIndex == 0 ? 2 : 4,
                uniqueCenter: true);
            for (int volley = 0; volley < offsets.Length; volley++)
            {
                patterns.Add(Pattern(BuildRectangularFormation(
                    250f, 1, 6f, coordinates,
                    expansionRates[shapeIndex], offsets[volley]),
                    times[shapeIndex][volley]));
            }
        }

        BulletStructure angularJerk = new BulletStructure(
            400f, 2, BulletMotionType.ConstantTurn, 270f,
            angularAccelerationDegreesPerSecondSquared: -50f,
            angularAccelerationDurationSeconds: float.PositiveInfinity,
            angularJerkDegreesPerSecondCubed: 5f,
            angularJerkDurationSeconds: 20f,
            useTimeTimeForMotion: true);
        patterns.Add(Pattern(Projectile(angularJerk, 6f, 12, 30f), 4f));

        return Stage(ChallengeCategory.D, 3, "○字編隊",
            "要素：編隊（矩形補正I字）・編隊（矩形補正V字）・" +
            "編隊（矩形補正X字）・全周・角躍度\n" +
            "偏差を変えて発射するI字・V字・X字編隊と、" +
            "角躍度によって回転速度が変化する全周弾を組み合わせた" +
            "完成弾幕に対応しよう。",
            patterns.ToArray());
    }

    private static BulletHellStageDefinition BuildChallengeD5Stage()
    {
        BulletStructure navigationA = new BulletStructure(
            300f, 3, BulletMotionType.ProportionalNavigation, 90f,
            navigationConstant: 3f,
            totalTurnAngleDegrees: 100f);
        BulletStructure homingB = new BulletStructure(
            300f, 3, BulletMotionType.Homing, 20f,
            totalTurnAngleDegrees: 20f);
        BulletStructure clockNavigation = new BulletStructure(
            50f, 3, BulletMotionType.ProportionalNavigation, 45f,
            navigationConstant: 8f,
            totalTurnAngleDegrees: 90f,
            linearAcceleration: 50f,
            linearAccelerationDurationSeconds: 10f,
            guidanceCommandIntervalSeconds: 0.8f);

        return Stage(ChallengeCategory.D, 4, "誘導弾だらけ",
            "要素：自機狙い偶数way・N=3比例航法誘導弾・純粋追尾誘導弾・クロック式比例航法誘導弾・加速・連射\n" +
            "N=3比例航法弾と純粋追尾弾、偏差を変えて連射する" +
            "加速付きクロック式比例航法誘導弾を組み合わせた" +
            "完成弾幕に対応しよう。",
            Pattern(Projectile(navigationA, 2f, 2,
                projectileAngles: new[] { -90f, 90f }), 0f),
            Pattern(Projectile(homingB, 2f, 2,
                projectileAngles: new[] { -20f, 20f }), 0f),
            Pattern(Projectile(clockNavigation, 2f,
                burstCount: 8,
                burstInterval: 0.1f,
                burstOffsets: new[] { 0f, 10f, 25f, 15f, -5f, -15f, -30f, -20f },
                reaimDuringBurst: true), 0f));
    }

    private static BulletHellStageDefinition[] BuildChallengeRankingStages()
    {
        return new[]
        {
            Stage(ChallengeCategory.Ranking, 0, "順次",
                "要素：D-1 弾幕結界・D-2 分裂・追尾複合弾幕・" +
                "D-3 多弾頭・特異点のある角加速度弾・D-4 ○字編隊・" +
                "D-5 誘導弾だらけ\n" +
                "Challenge Dの完成弾幕へ、8秒ごとに定められた順序で挑戦しよう。\n" +
                "Bossへ与えた累積ダメージ量でハイスコアを目指そう。"),
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

    private static List<Vector2> BuildFormationCoordinates(
        FormationShape shape,
        int radius = 4,
        bool uniqueCenter = false)
    {
        List<Vector2> coordinates = new List<Vector2>();
        int branchCount = shape == FormationShape.X ? 2 : 1;
        for (int branch = 0; branch < branchCount; branch++)
        {
            float branchSign = branch == 0 ? -1f : 1f;
            for (int index = -radius; index <= radius; index++)
            {
                if (uniqueCenter && branch > 0 && index == 0)
                {
                    continue;
                }
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
        IReadOnlyList<Vector2> coordinates,
        float expansionRate = 5f,
        float aimOffset = 0f)
    {
        BulletStructure[] structures = new BulletStructure[coordinates.Count];
        float[] angles = new float[coordinates.Count];
        for (int index = 0; index < coordinates.Count; index++)
        {
            Vector2 coordinate = coordinates[index];
            float sx = coordinate.x == 0f ? 0f : expansionRate;
            float sy = coordinate.y == 0f ? 0f : expansionRate;
            float forwardSpeed = baseSpeed + sy * coordinate.y;
            float sideSpeed = sx * coordinate.x;
            structures[index] = Straight(
                Mathf.Sqrt(forwardSpeed * forwardSpeed +
                           sideSpeed * sideSpeed),
                threat);
            angles[index] = Mathf.Atan2(sideSpeed, forwardSpeed) *
                Mathf.Rad2Deg;
        }
        return Projectile(
            structures[0], repeat, structures.Length,
            aimOffset: aimOffset,
            projectileStructures: structures,
            projectileAngles: angles);
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
