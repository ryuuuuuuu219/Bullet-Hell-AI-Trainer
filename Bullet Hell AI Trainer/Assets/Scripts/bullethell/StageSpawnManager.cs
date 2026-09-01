using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StageSpawnManager : MonoBehaviour
{
    private const float GenerationConditionCheckInterval = 0.1f;
    private static readonly Vector3 PlayerSpawnPosition = new Vector3(0f, -150f, 0f);
    private static readonly Vector3 BossSpawnPosition = new Vector3(0f, 150f, 0f);

    [Header("Player population")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject playerBulletPrefab;
    [SerializeField] private GameObject layerInfoPrefab;
    [SerializeField] private bool teacherModeEnabled;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;

    [Header("Enemy bullets")]
    [SerializeField] private GameObject enemyBulletPrefab;

    private readonly List<GameObject> spawnedPlayers = new List<GameObject>();
    private BulletHellShooter bulletHellShooter;
    private GameObject spawnedBoss;
    private Boss spawnedBossData;
    private PopulationSettingsData activePopulationData;
    private float nextGenerationConditionCheckTime;
    private bool isInitialized;
    private AiSaveData teacherNetworkSnapshot;
    private IReadOnlyList<float> activeThreatArrivalTimes = Array.Empty<float>();
    private float stageElapsedTime;
    private int nextThreatTimeIndex;

    public static float CurrentThreatTimeSignal { get; private set; } = -1f;

    public int StageId { get; private set; } = -1;
    public ChallengeCategory Category { get; private set; } =
        ChallengeCategory.Basic;

    public event Action<BulletHellShotDefinition> SpawnRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        CurrentThreatTimeSignal = -1f;
    }

    public void Initialize(
        ChallengeCategory category,
        int stageId,
        bool shouldEnableTeacherMode = false)
    {
        EnsureBulletHellShooter();
        bulletHellShooter.ClearEnemyAttacks();
        Category = category;
        StageId = stageId;
        teacherModeEnabled = shouldEnableTeacherMode;
        LogicalLayerVisibility.SetExclusiveVisibleLayer(
            teacherModeEnabled ? 0 : -1);
        PopulationSettingsData populationData = populationSetting.LoadData();
        activePopulationData = populationData;
        LoadTeacherNetworkSnapshot();
        List<AiSaveData> initialGenomes = BuildInitialPopulation(populationData);
        int playerCount = GetSpawnedPlayerCount(populationData);
        SpawnBoss(playerCount);
        SpawnPlayerPopulation(populationData, initialGenomes);
        StartStagePattern();
        nextGenerationConditionCheckTime = Time.unscaledTime;
        isInitialized = true;
        StageView.RefreshGenerationLabel();
        StageView.BuildLayerInfo(
            layerInfoPrefab,
            this,
            playerCount);
    }

    private void Update()
    {
        if (!isInitialized ||
            Time.unscaledTime < nextGenerationConditionCheckTime)
        {
            return;
        }

        nextGenerationConditionCheckTime = Time.unscaledTime +
            GenerationConditionCheckInterval;
        PopulationSettingsData populationData = populationSetting.LoadData();
        bool hasManualRequest = populationData.pendingManualGenerationRequests > 0;
        bool shouldAdvanceAutomatically =
            populationData.advanceWhenAllIndividualsAreHit &&
            AreAllPlayersHit();

        if (hasManualRequest || shouldAdvanceAutomatically)
        {
            AdvanceGeneration(populationData, hasManualRequest);
        }
    }

    private void FixedUpdate()
    {
        if (!isInitialized)
        {
            return;
        }

        stageElapsedTime += Time.fixedDeltaTime;
        CurrentThreatTimeSignal = Mathf.MoveTowards(
            CurrentThreatTimeSignal,
            -1f,
            Time.fixedDeltaTime * 0.1f);

        while (nextThreatTimeIndex < activeThreatArrivalTimes.Count &&
               stageElapsedTime >= activeThreatArrivalTimes[nextThreatTimeIndex])
        {
            CurrentThreatTimeSignal = 1f;
            nextThreatTimeIndex++;
        }
    }

    private void StartStagePattern()
    {
        BulletHellStageDefinition stage =
            BulletHellStageAttackDefinitions.GetStage(Category, StageId);
        ResetThreatTimeSignal(stage);
        if (stage == null)
        {
            Debug.LogWarning(
                $"Spawn control is not implemented for {Category} stage ID {StageId}.");
            return;
        }

        bulletHellShooter.StartFiring(
            stage,
            spawnedBoss?.transform,
            spawnedPlayers);
    }

    private void ResetThreatTimeSignal(BulletHellStageDefinition stage)
    {
        stageElapsedTime = 0f;
        CurrentThreatTimeSignal = -1f;
        nextThreatTimeIndex = 0;
        activeThreatArrivalTimes = stage?.ThreatArrivalTimes ?? Array.Empty<float>();
    }

    private void SpawnPlayerPopulation(
        PopulationSettingsData populationData,
        IReadOnlyList<AiSaveData> genomes)
    {
        ClearSpawnedPlayers();

        if (playerPrefab == null)
        {
            Debug.LogWarning("Player prefab is not assigned to StageSpawnManager.");
            return;
        }

        if (teacherModeEnabled)
        {
            SpawnPlayer(populationData, 0, teacherNetworkSnapshot, true);
        }

        int populationSize = GetGeneticPlayerCount(populationData);
        for (int index = 0; index < populationSize; index++)
        {
            int logicalLayer = index + (teacherModeEnabled ? 1 : 0);
            AiSaveData genome = genomes != null && index < genomes.Count
                ? genomes[index]
                : null;
            SpawnPlayer(populationData, logicalLayer, genome, false);
        }
    }

    private void SpawnPlayer(
        PopulationSettingsData populationData,
        int logicalLayer,
        AiSaveData genome,
        bool teacherControl)
    {
        GameObject player = Instantiate(
            playerPrefab,
            PlayerSpawnPosition,
            Quaternion.identity);
        player.name = teacherControl
            ? "Manual Player"
            : $"Player {logicalLayer + 1}";

        PlayerAgent playerAgent = player.GetComponent<PlayerAgent>();
        if (playerAgent == null)
        {
            playerAgent = player.AddComponent<PlayerAgent>();
        }

        playerAgent.SetLogicalLayer(logicalLayer);

        PlayerMovementController movement =
            player.GetComponent<PlayerMovementController>();
        if (movement == null)
        {
            movement = player.AddComponent<PlayerMovementController>();
        }

        movement.SetManualControl(teacherControl);
        movement.SetTeacherMode(teacherControl);
        movement.SetTeacherTrainingEnabled(Category != ChallengeCategory.Final);

        PlayerShooter playerShooter = player.GetComponent<PlayerShooter>();
        if (playerShooter == null)
        {
            playerShooter = player.AddComponent<PlayerShooter>();
        }

        playerShooter.Configure(playerBulletPrefab, logicalLayer);

        Aidata aiData = player.GetComponent<Aidata>();
        if (aiData != null)
        {
            if (movement.IsTeacherControlled && teacherNetworkSnapshot != null)
            {
                aiData.ApplySnapshot(teacherNetworkSnapshot);
            }
            else if (genome != null)
            {
                aiData.ApplySnapshot(genome);
            }
            else
            {
                int seed = CreateNetworkSeed(
                    populationData.currentGeneration,
                    logicalLayer);
                aiData.RandomizeNetwork(seed);
            }
        }

        PlayerEvaluationTracker tracker =
            player.GetComponent<PlayerEvaluationTracker>();
        if (tracker == null)
        {
            tracker = player.AddComponent<PlayerEvaluationTracker>();
        }

        tracker.Initialize(populationData.centerDistanceSampleIntervalSeconds);
        spawnedPlayers.Add(player);
    }

    private int GetSpawnedPlayerCount(PopulationSettingsData populationData)
    {
        return GetGeneticPlayerCount(populationData) +
            (teacherModeEnabled ? 1 : 0);
    }

    private int GetGeneticPlayerCount(PopulationSettingsData populationData)
    {
        return Category == ChallengeCategory.Final
            ? (teacherModeEnabled ? 0 : 1)
            : populationData.populationSize;
    }

    private void LoadTeacherNetworkSnapshot()
    {
        if (!teacherModeEnabled || teacherNetworkSnapshot != null)
        {
            return;
        }

        AiSaveData savedNetwork = Aidata.LoadData();
        if (Aidata.HasTrainableNetwork(savedNetwork))
        {
            teacherNetworkSnapshot = Aidata.CloneData(savedNetwork);
        }
    }

    private void SpawnBoss(int populationSize)
    {
        if (spawnedBoss != null)
        {
            spawnedBoss.SetActive(false);
            Destroy(spawnedBoss);
        }

        spawnedBoss = bossPrefab != null
            ? Instantiate(bossPrefab, BossSpawnPosition, Quaternion.identity)
            : new GameObject("Boss");
        spawnedBoss.transform.position = BossSpawnPosition;
        spawnedBoss.name = "Boss";

        if (!spawnedBoss.TryGetComponent(out spawnedBossData))
        {
            spawnedBossData = spawnedBoss.AddComponent<Boss>();
        }

        spawnedBossData.SetLayerCount(populationSize);
    }

    private List<AiSaveData> BuildInitialPopulation(
        PopulationSettingsData populationData)
    {
        if (populationData.currentGeneration <= 1)
        {
            return null;
        }

        AiSaveData savedGenome = Aidata.LoadData();
        if (!Aidata.HasTrainableNetwork(savedGenome))
        {
            return null;
        }

        return GenerationGeneticAlgorithm.CreatePopulationFromSavedGenome(
            savedGenome,
            GetGeneticPlayerCount(populationData),
            populationData.mutationRate,
            populationData.mutationStrength,
            populationData.eliteCount,
            CreateNetworkSeed(populationData.currentGeneration, -1));
    }

    private bool AreAllPlayersHit()
    {
        bool foundPlayer = false;
        foreach (GameObject player in spawnedPlayers)
        {
            if (player == null ||
                !player.TryGetComponent(out PlayerAgent playerAgent))
            {
                continue;
            }

            foundPlayer = true;
            if (!playerAgent.IsHit)
            {
                return false;
            }
        }

        return foundPlayer;
    }

    private void AdvanceGeneration(
        PopulationSettingsData populationData,
        bool consumeManualRequest)
    {
        List<GenerationCandidate> candidates = EvaluateCurrentGeneration(
            populationData);
        if (candidates.Count == 0)
        {
            return;
        }

        GenerationCandidate savedCandidate =
            GenerationGeneticAlgorithm.SelectGenomeToSave(
                candidates,
                populationData.geneticSaveEvaluationAxis);
        if (savedCandidate != null)
        {
            Aidata.SaveData(Aidata.CloneData(savedCandidate.Genome));
        }

        CaptureTeacherNetwork();

        int completedGeneration = populationData.currentGeneration;
        populationData.currentGeneration++;
        if (consumeManualRequest)
        {
            populationData.pendingManualGenerationRequests = Mathf.Max(
                0,
                populationData.pendingManualGenerationRequests - 1);
        }

        populationSetting.SaveData(populationData);
        activePopulationData = populationData;

        List<AiSaveData> nextGenomes =
            GenerationGeneticAlgorithm.BreedNextGeneration(
                candidates,
                GetGeneticPlayerCount(populationData),
                populationData.mutationRate,
                populationData.mutationStrength,
                populationData.eliteCount,
                CreateNetworkSeed(populationData.currentGeneration, -1));

        Debug.Log(
            $"Generation {completedGeneration} completed. " +
            $"Advancing to generation {populationData.currentGeneration}. " +
            $"Saved layer: {savedCandidate?.LogicalLayer ?? -1}.");

        bulletHellShooter.ClearEnemyAttacks();
        ClearPlayerBullets();
        SpawnBoss(GetSpawnedPlayerCount(populationData));
        SpawnPlayerPopulation(populationData, nextGenomes);
        StartStagePattern();
        StageView.RefreshGenerationLabel();
    }

    private void CaptureTeacherNetwork()
    {
        foreach (GameObject player in spawnedPlayers)
        {
            if (player == null ||
                !player.TryGetComponent(out PlayerMovementController movement) ||
                !movement.IsTeacherControlled ||
                !player.TryGetComponent(out Aidata aiData))
            {
                continue;
            }

            teacherNetworkSnapshot = aiData.CreateSnapshot();
            Aidata.SaveData(Aidata.CloneData(teacherNetworkSnapshot));
            return;
        }
    }

    public bool TryGetLayerInfo(
        int logicalLayer,
        out string playerName,
        out float score)
    {
        foreach (GameObject player in spawnedPlayers)
        {
            if (player == null ||
                !player.TryGetComponent(out PlayerAgent playerAgent) ||
                playerAgent.LogicalLayer != logicalLayer ||
                !player.TryGetComponent(out PlayerEvaluationTracker tracker))
            {
                continue;
            }

            float damage = spawnedBossData != null
                ? spawnedBossData.GetDamage(logicalLayer)
                : 0f;
            PopulationSettingsData settings = activePopulationData ??
                populationSetting.LoadData();
            playerName = player.name;
            score = settings.CalculateGenerationScore(
                damage,
                tracker.SurvivalTime,
                tracker.EdgeCollisionCumulativeTime,
                tracker.CenterDistanceSampledSum);
            return true;
        }

        playerName = $"Player {logicalLayer + 1}";
        score = 0f;
        return false;
    }

    private List<GenerationCandidate> EvaluateCurrentGeneration(
        PopulationSettingsData populationData)
    {
        List<GenerationCandidate> candidates =
            new List<GenerationCandidate>();

        foreach (GameObject player in spawnedPlayers)
        {
            if (player == null ||
                !player.TryGetComponent(out PlayerAgent playerAgent) ||
                !player.TryGetComponent(out Aidata aiData) ||
                !player.TryGetComponent(out PlayerEvaluationTracker tracker))
            {
                continue;
            }

            if (player.TryGetComponent(out PlayerMovementController movement) &&
                movement.IsExcludedFromGeneticAlgorithm)
            {
                continue;
            }

            float damage = spawnedBossData != null
                ? spawnedBossData.GetDamage(playerAgent.LogicalLayer)
                : 0f;
            GenerationCandidate candidate = new GenerationCandidate
            {
                LogicalLayer = playerAgent.LogicalLayer,
                Genome = aiData.CreateSnapshot(),
                Damage = damage,
                SurvivalTime = tracker.SurvivalTime,
                EdgeCollisionCumulativeTime =
                    tracker.EdgeCollisionCumulativeTime,
                CenterDistanceSampledSum = tracker.CenterDistanceSampledSum,
            };
            candidate.Score = populationData.CalculateGenerationScore(
                candidate.Damage,
                candidate.SurvivalTime,
                candidate.EdgeCollisionCumulativeTime,
                candidate.CenterDistanceSampledSum);
            candidates.Add(candidate);

            Debug.Log(
                $"Generation {populationData.currentGeneration}, " +
                $"layer {candidate.LogicalLayer}: score={candidate.Score:F3}, " +
                $"damage={candidate.Damage:F3}, " +
                $"survival={candidate.SurvivalTime:F3}, " +
                $"edge={candidate.EdgeCollisionCumulativeTime:F3}, " +
                $"centerSum={candidate.CenterDistanceSampledSum:F3}");
        }

        return candidates;
    }

    private static void ClearPlayerBullets()
    {
        PlayerBullet[] playerBullets = FindObjectsByType<PlayerBullet>();
        foreach (PlayerBullet playerBullet in playerBullets)
        {
            if (playerBullet == null)
            {
                continue;
            }

            ProjectilePool.Release(playerBullet.gameObject);
        }
    }

    private static int CreateNetworkSeed(int generation, int logicalLayer)
    {
        unchecked
        {
            return generation * 397 ^ logicalLayer;
        }
    }

    private void ClearSpawnedPlayers()
    {
        foreach (GameObject player in spawnedPlayers)
        {
            if (player != null)
            {
                player.SetActive(false);
                Destroy(player);
            }
        }

        spawnedPlayers.Clear();
    }

    private void EnsureBulletHellShooter()
    {
        if (bulletHellShooter == null)
        {
            bulletHellShooter = GetComponent<BulletHellShooter>();
        }

        if (bulletHellShooter == null)
        {
            bulletHellShooter = gameObject.AddComponent<BulletHellShooter>();
        }

        bulletHellShooter.Configure(enemyBulletPrefab);
        bulletHellShooter.ShotFired -= OnShotFired;
        bulletHellShooter.ShotFired += OnShotFired;
    }

    private void OnShotFired(BulletHellShotDefinition definition)
    {
        SpawnRequested?.Invoke(definition);
    }
}
