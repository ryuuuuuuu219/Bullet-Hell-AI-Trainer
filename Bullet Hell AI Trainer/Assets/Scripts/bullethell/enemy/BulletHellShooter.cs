using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BulletHellShooter : MonoBehaviour
{
    [SerializeField] private GameObject enemyBulletPrefab;

    private readonly List<LaserAttack> activeLasers =
        new List<LaserAttack>();
    private Coroutine firingRoutine;

    public event Action<BulletHellShotDefinition> ShotFired;

    public void Configure(GameObject bulletPrefab)
    {
        enemyBulletPrefab = bulletPrefab;
    }

    public void StartFiring(
        BulletHellShotDefinition definition,
        Transform source,
        IReadOnlyList<GameObject> targets)
    {
        StopFiring();
        if (definition == null)
        {
            return;
        }

        if (definition.RepeatIntervalSeconds <= 0f)
        {
            Fire(definition, source, targets);
            return;
        }

        firingRoutine = StartCoroutine(RunFiringPattern(
            definition,
            source,
            targets));
    }

    public void StopFiring()
    {
        if (firingRoutine == null)
        {
            return;
        }

        StopCoroutine(firingRoutine);
        firingRoutine = null;
    }

    public void Fire(
        BulletHellShotDefinition definition,
        Transform source,
        IReadOnlyList<GameObject> targets)
    {
        if (definition == null)
        {
            return;
        }

        ShotFired?.Invoke(definition);

        Vector2 sourcePosition = source != null
            ? source.position
            : transform.position;
        bool firedForTarget = false;

        if (targets != null)
        {
            foreach (GameObject targetObject in targets)
            {
                if (targetObject == null ||
                    !targetObject.TryGetComponent(out PlayerAgent target))
                {
                    continue;
                }

                FireForTarget(definition, sourcePosition, target);
                firedForTarget = true;
            }
        }

        if (!firedForTarget &&
            definition.AttackType == BulletHellAttackType.Projectile)
        {
            FireProjectiles(definition, sourcePosition, null, 0);
        }

        activeLasers.RemoveAll(laser => laser == null);
    }

    public void ClearEnemyAttacks()
    {
        StopFiring();

        foreach (LaserAttack laser in activeLasers)
        {
            if (laser != null)
            {
                laser.gameObject.SetActive(false);
                Destroy(laser.gameObject);
            }
        }

        activeLasers.Clear();

        for (int index = BulletManager.ActiveBullets.Count - 1;
             index >= 0;
             index--)
        {
            bullet enemyBullet = BulletManager.ActiveBullets[index];
            if (enemyBullet != null)
            {
                ProjectilePool.Release(enemyBullet.gameObject);
            }
        }
    }

    private IEnumerator RunFiringPattern(
        BulletHellShotDefinition definition,
        Transform source,
        IReadOnlyList<GameObject> targets)
    {
        WaitForSeconds interval = new WaitForSeconds(
            definition.RepeatIntervalSeconds);

        while (true)
        {
            yield return interval;
            Fire(definition, source, targets);
        }
    }

    private void FireForTarget(
        BulletHellShotDefinition definition,
        Vector2 sourcePosition,
        PlayerAgent target)
    {
        switch (definition.AttackType)
        {
            case BulletHellAttackType.Projectile:
                FireProjectiles(
                    definition,
                    sourcePosition,
                    target.transform,
                    target.LogicalLayer);
                break;
            case BulletHellAttackType.Laser:
                FireLaser(definition, sourcePosition, target);
                break;
        }
    }

    private void FireProjectiles(
        BulletHellShotDefinition definition,
        Vector2 sourcePosition,
        Transform aimTarget,
        int logicalLayer)
    {
        if (enemyBulletPrefab == null || definition.Bullet == null)
        {
            return;
        }

        if (definition.Bullet.MotionType != BulletMotionType.Straight)
        {
            Debug.LogWarning(
                $"Bullet motion is not implemented: {definition.Bullet.MotionType}",
                this);
            return;
        }

        Vector2 baseDirection = ResolveAimDirection(
            definition.AimType,
            sourcePosition,
            aimTarget);

        for (int index = 0; index < definition.ProjectileCount; index++)
        {
            float angleOffset = definition.GetProjectileAngleOffset(index);
            Vector2 direction = Quaternion.Euler(0f, 0f, angleOffset) *
                baseDirection;
            SpawnProjectile(
                sourcePosition,
                direction,
                definition.Bullet,
                logicalLayer);
        }
    }

    private void SpawnProjectile(
        Vector2 position,
        Vector2 direction,
        BulletStructure structure,
        int logicalLayer)
    {
        GameObject bulletObject = ProjectilePool.Acquire(
            enemyBulletPrefab,
            position,
            Quaternion.identity);
        Rigidbody2D body = bulletObject.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            Debug.LogWarning("Enemy bullet prefab requires a Rigidbody2D.", this);
            ProjectilePool.Release(bulletObject);
            return;
        }

        Vector2 movementVector = direction.normalized * structure.Speed;
        body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;

        bullet bulletData = bulletObject.GetComponent<bullet>();
        if (bulletData == null)
        {
            bulletData = bulletObject.AddComponent<bullet>();
        }

        bulletData.SetData(movementVector, structure, logicalLayer);
        bulletObject.SetActive(true);
        body.position = position;
        body.linearVelocity = movementVector;
    }

    private void FireLaser(
        BulletHellShotDefinition definition,
        Vector2 sourcePosition,
        PlayerAgent target)
    {
        LaserStructure structure = definition.Laser;
        if (structure == null)
        {
            return;
        }

        Vector2 direction = ResolveAimDirection(
            definition.AimType,
            sourcePosition,
            target.transform);
        GameObject laserObject = new GameObject(
            $"Laser Layer {target.LogicalLayer}",
            typeof(LineRenderer),
            typeof(LaserAttack));
        laserObject.transform.SetParent(transform, true);
        LaserAttack laserAttack = laserObject.GetComponent<LaserAttack>();
        laserAttack.Configure(
            sourcePosition,
            direction,
            target,
            target.LogicalLayer,
            structure.WarningDuration,
            structure.ActiveDuration,
            structure.Length,
            structure.WarningWidth,
            structure.ActiveWidth,
            structure.ThreatLevel);
        activeLasers.Add(laserAttack);
    }

    private static Vector2 ResolveAimDirection(
        BulletAimType aimType,
        Vector2 sourcePosition,
        Transform target)
    {
        return aimType == BulletAimType.PlayerAimed && target != null
            ? ((Vector2)target.position - sourcePosition).normalized
            : Vector2.down;
    }

    private void OnDestroy()
    {
        // Scene shutdown destroys active projectiles and child lasers itself.
        // Returning projectiles here could create the persistent pool root from
        // inside OnDestroy, which Unity rejects while closing the scene.
        StopFiring();
    }
}
