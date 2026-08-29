using System;
using System.Collections;
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

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;

    [Header("Enemy bullets")]
    [SerializeField] private GameObject enemyBulletPrefab;

    private readonly List<GameObject> spawnedPlayers = new List<GameObject>();
    private GameObject spawnedBoss;
    private Boss spawnedBossData;
    private PopulationSettingsData activePopulationData;
    private float nextGenerationConditionCheckTime;
    private bool isInitialized;

    public int StageId { get; private set; } = -1;

    public event Action<SpawnRequest> SpawnRequested;

    public void Initialize(int stageId)
    {
        StopAllCoroutines();
        StageId = stageId;
        PopulationSettingsData populationData = populationSetting.LoadData();
        activePopulationData = populationData;
        List<AiSaveData> initialGenomes = BuildInitialPopulation(populationData);
        SpawnBoss(populationData.populationSize);
        SpawnPlayerPopulation(populationData, initialGenomes);
        StartStagePattern();
        nextGenerationConditionCheckTime = Time.unscaledTime;
        isInitialized = true;
        StageView.RefreshGenerationLabel();
        StageView.BuildLayerInfo(
            layerInfoPrefab,
            this,
            populationData.populationSize);
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

    private void StartStagePattern()
    {
        switch (StageId)
        {
            case 0:
                StartCoroutine(RunStage0());
                break;
            default:
                Debug.LogWarning($"Spawn control is not implemented for stage ID {StageId}.");
                break;
        }
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

        int populationSize = populationData.populationSize;
        for (int index = 0; index < populationSize; index++)
        {
            GameObject player = Instantiate(
                playerPrefab,
                PlayerSpawnPosition,
                Quaternion.identity);
            player.name = $"Player {index + 1}";
            PlayerAgent playerAgent = player.GetComponent<PlayerAgent>();
            if (playerAgent == null)
            {
                playerAgent = player.AddComponent<PlayerAgent>();
            }

            playerAgent.SetLogicalLayer(index);

            PlayerShooter playerShooter = player.GetComponent<PlayerShooter>();
            if (playerShooter == null)
            {
                playerShooter = player.AddComponent<PlayerShooter>();
            }

            playerShooter.Configure(playerBulletPrefab, index);

            Aidata aiData = player.GetComponent<Aidata>();
            if (aiData != null)
            {
                if (genomes != null && index < genomes.Count)
                {
                    aiData.ApplySnapshot(genomes[index]);
                }
                else
                {
                    int seed = CreateNetworkSeed(
                        populationData.currentGeneration,
                        index);
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
            populationData.populationSize,
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
                populationData.populationSize,
                populationData.mutationRate,
                populationData.mutationStrength,
                populationData.eliteCount,
                CreateNetworkSeed(populationData.currentGeneration, -1));

        Debug.Log(
            $"Generation {completedGeneration} completed. " +
            $"Advancing to generation {populationData.currentGeneration}. " +
            $"Saved layer: {savedCandidate?.LogicalLayer ?? -1}.");

        StopAllCoroutines();
        ClearAllBullets();
        SpawnBoss(populationData.populationSize);
        SpawnPlayerPopulation(populationData, nextGenomes);
        StartStagePattern();
        StageView.RefreshGenerationLabel();
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

    private static void ClearAllBullets()
    {
        for (int index = BulletManager.ActiveBullets.Count - 1;
             index >= 0;
             index--)
        {
            bullet enemyBullet = BulletManager.ActiveBullets[index];
            if (enemyBullet == null)
            {
                continue;
            }

            ProjectilePool.Release(enemyBullet.gameObject);
        }

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

    private IEnumerator RunStage0()
    {
        WaitForSeconds interval = new WaitForSeconds(2f);

        while (true)
        {
            yield return interval;
            DispatchSpawn(new SpawnRequest(
                stageId: 0,
                pattern: SpawnPattern.PlayerAimedOneWay,
                bulletCount: 1,
                speed: 40f,
                threat: 1));
        }
    }

    private void DispatchSpawn(SpawnRequest request)
    {
        SpawnRequested?.Invoke(request);

        if (enemyBulletPrefab == null)
        {
            return;
        }

        bool spawnedForPlayer = false;
        foreach (GameObject player in spawnedPlayers)
        {
            if (player == null || !player.TryGetComponent(out PlayerAgent playerAgent))
            {
                continue;
            }

            SpawnBulletForTarget(request, player.transform, playerAgent.LogicalLayer);
            spawnedForPlayer = true;
        }

        if (!spawnedForPlayer)
        {
            SpawnBulletForTarget(request, null, 0);
        }
    }

    private void SpawnBulletForTarget(
        SpawnRequest request,
        Transform aimTarget,
        int logicalLayer)
    {
        Vector3 position = spawnedBoss != null
            ? spawnedBoss.transform.position
            : BossSpawnPosition;

        for (int index = 0; index < request.BulletCount; index++)
        {
            GameObject bulletObject = ProjectilePool.Acquire(
                enemyBulletPrefab,
                position,
                Quaternion.identity);
            Rigidbody2D body = bulletObject.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                Debug.LogWarning("Enemy bullet prefab requires a Rigidbody2D.");
                ProjectilePool.Release(bulletObject);
                continue;
            }

            Vector2 direction = aimTarget != null
                ? ((Vector2)aimTarget.position - (Vector2)position).normalized
                : Vector2.down;
            Vector2 movementVector = direction * request.Speed;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

            bullet bulletData = bulletObject.GetComponent<bullet>();
            if (bulletData == null)
            {
                bulletData = bulletObject.AddComponent<bullet>();
            }

            bulletData.SetData(movementVector, request.Threat, logicalLayer);
            bulletObject.SetActive(true);
            body.position = position;
            body.linearVelocity = movementVector;
        }
    }

    public enum SpawnPattern
    {
        PlayerAimedOneWay
    }

    public readonly struct SpawnRequest
    {
        public SpawnRequest(int stageId, SpawnPattern pattern, int bulletCount, float speed, int threat)
        {
            StageId = stageId;
            Pattern = pattern;
            BulletCount = bulletCount;
            Speed = speed;
            Threat = threat;
        }

        public int StageId { get; }
        public SpawnPattern Pattern { get; }
        public int BulletCount { get; }
        public float Speed { get; }
        public int Threat { get; }
    }
}
