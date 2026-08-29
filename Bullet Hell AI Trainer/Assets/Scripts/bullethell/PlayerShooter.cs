using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerShooter : MonoBehaviour
{
    private const int BulletsPerShot = 5;
    private const float InitialSpeed = 40f;
    private const float FireInterval = 1f;
    private const float TotalSpreadDegrees = 30f;

    [SerializeField] private GameObject bulletPrefab;
    [SerializeField, Min(0)] private int logicalLayer;

    private Coroutine firingRoutine;

    private void OnEnable()
    {
        firingRoutine = StartCoroutine(FireContinuously());
    }

    private void OnDisable()
    {
        if (firingRoutine != null)
        {
            StopCoroutine(firingRoutine);
            firingRoutine = null;
        }
    }

    public void Configure(GameObject prefab, int layer)
    {
        bulletPrefab = prefab;
        logicalLayer = Mathf.Max(0, layer);
    }

    private IEnumerator FireContinuously()
    {
        WaitForSeconds interval = new WaitForSeconds(FireInterval);

        while (true)
        {
            yield return interval;
            FireSpread();
        }
    }

    private void FireSpread()
    {
        float step = TotalSpreadDegrees / (BulletsPerShot - 1);
        float firstAngle = -TotalSpreadDegrees * 0.5f;

        for (int index = 0; index < BulletsPerShot; index++)
        {
            float angle = firstAngle + step * index;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
            SpawnBullet(direction * InitialSpeed);
        }
    }

    private void SpawnBullet(Vector2 velocity)
    {
        GameObject bulletObject = bulletPrefab != null
            ? Instantiate(bulletPrefab, transform.position, Quaternion.identity)
            : new GameObject($"Player Bullet L{logicalLayer}");
        bulletObject.name = $"Player Bullet L{logicalLayer}";

        if (bulletPrefab == null)
        {
            RegularPolygonLineRenderer polygon =
                bulletObject.AddComponent<RegularPolygonLineRenderer>();
            polygon.SetStyle(6, 5f, Color.blue);
        }

        Rigidbody2D body = bulletObject.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = bulletObject.AddComponent<Rigidbody2D>();
        }

        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D collisionShape = bulletObject.GetComponent<CircleCollider2D>();
        if (collisionShape == null)
        {
            collisionShape = bulletObject.AddComponent<CircleCollider2D>();
        }

        collisionShape.isTrigger = true;
        collisionShape.radius = 5f;

        PlayerBullet bulletData = bulletObject.GetComponent<PlayerBullet>();
        if (bulletData == null)
        {
            bulletData = bulletObject.AddComponent<PlayerBullet>();
        }

        bulletData.Initialize(velocity, logicalLayer);
    }
}
