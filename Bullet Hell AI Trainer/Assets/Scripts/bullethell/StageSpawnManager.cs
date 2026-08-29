using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class StageSpawnManager : MonoBehaviour
{
    private static readonly Vector3 PlayerSpawnPosition = new Vector3(0f, -150f, 0f);
    private static readonly Vector3 BossSpawnPosition = new Vector3(0f, 150f, 0f);

    [Header("Player population")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject playerBulletPrefab;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;

    [Header("Enemy bullets")]
    [SerializeField] private GameObject enemyBulletPrefab;

    private readonly List<GameObject> spawnedPlayers = new List<GameObject>();
    private GameObject spawnedBoss;

    public int StageId { get; private set; } = -1;

    public event Action<SpawnRequest> SpawnRequested;

    public void Initialize(int stageId)
    {
        StopAllCoroutines();
        SpawnBoss();
        SpawnPlayerPopulation();
        StageId = stageId;

        switch (stageId)
        {
            case 0:
                StartCoroutine(RunStage0());
                break;
            default:
                Debug.LogWarning($"Spawn control is not implemented for stage ID {stageId}.");
                break;
        }
    }

    private void SpawnPlayerPopulation()
    {
        ClearSpawnedPlayers();

        if (playerPrefab == null)
        {
            Debug.LogWarning("Player prefab is not assigned to StageSpawnManager.");
            return;
        }

        PopulationSettingsData populationData = populationSetting.LoadData();
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
                int seed = CreateNetworkSeed(populationData.currentGeneration, index);
                aiData.RandomizeNetwork(seed);
            }

            spawnedPlayers.Add(player);
        }
    }

    private void SpawnBoss()
    {
        if (spawnedBoss != null)
        {
            Destroy(spawnedBoss);
        }

        spawnedBoss = bossPrefab != null
            ? Instantiate(bossPrefab, BossSpawnPosition, Quaternion.identity)
            : new GameObject("Boss");
        spawnedBoss.transform.position = BossSpawnPosition;
        spawnedBoss.name = "Boss";

        if (!spawnedBoss.TryGetComponent(out Boss boss))
        {
            boss = spawnedBoss.AddComponent<Boss>();
        }

        PopulationSettingsData populationData = populationSetting.LoadData();
        boss.SetLayerCount(populationData.populationSize);
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
            GameObject bulletObject = Instantiate(
                enemyBulletPrefab,
                position,
                Quaternion.identity);
            Rigidbody2D body = bulletObject.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                Debug.LogWarning("Enemy bullet prefab requires a Rigidbody2D.");
                Destroy(bulletObject);
                continue;
            }

            Vector2 direction = aimTarget != null
                ? ((Vector2)aimTarget.position - body.position).normalized
                : Vector2.down;
            Vector2 movementVector = direction * request.Speed;
            body.linearVelocity = movementVector;

            bullet bulletData = bulletObject.GetComponent<bullet>();
            if (bulletData == null)
            {
                bulletData = bulletObject.AddComponent<bullet>();
            }

            bulletData.SetData(movementVector, request.Threat, logicalLayer);
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
