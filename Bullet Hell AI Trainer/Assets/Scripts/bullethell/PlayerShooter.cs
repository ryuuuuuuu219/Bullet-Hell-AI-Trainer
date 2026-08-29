using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerShooter : MonoBehaviour
{
    private const int BulletsPerShot = 10;
    private const float InitialSpeed = 7f;
    private const float FireInterval = 0.2f;
    private const float TotalSpreadDegrees = 30f;

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

    public void SetLogicalLayer(int value)
    {
        logicalLayer = Mathf.Max(0, value);
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
        GameObject bulletObject = new GameObject($"Player Bullet L{logicalLayer}");
        bulletObject.transform.position = transform.position;

        Rigidbody2D body = bulletObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D collisionShape = bulletObject.AddComponent<CircleCollider2D>();
        collisionShape.isTrigger = true;
        collisionShape.radius = 0.15f;

        PlayerBullet bulletData = bulletObject.AddComponent<PlayerBullet>();
        bulletData.Initialize(velocity, logicalLayer);
    }
}
