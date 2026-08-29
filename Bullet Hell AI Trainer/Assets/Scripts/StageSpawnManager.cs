using System;
using System.Collections;
using UnityEngine;

public sealed class StageSpawnManager : MonoBehaviour
{
    [SerializeField] private GameObject enemyBulletPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform target;

    public int StageId { get; private set; } = -1;

    public event Action<SpawnRequest> SpawnRequested;

    public void Initialize(int stageId)
    {
        StopAllCoroutines();
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

    public void SetSpawnResources(GameObject bulletPrefab, Transform origin, Transform aimTarget)
    {
        enemyBulletPrefab = bulletPrefab;
        spawnPoint = origin;
        target = aimTarget;
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
                speed: 5f,
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

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject bullet = Instantiate(enemyBulletPrefab, position, Quaternion.identity);
        Rigidbody2D body = bullet.GetComponent<Rigidbody2D>();

        if (body == null)
        {
            return;
        }

        Vector2 direction = target != null
            ? ((Vector2)target.position - body.position).normalized
            : Vector2.down;
        body.linearVelocity = direction * request.Speed;
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
