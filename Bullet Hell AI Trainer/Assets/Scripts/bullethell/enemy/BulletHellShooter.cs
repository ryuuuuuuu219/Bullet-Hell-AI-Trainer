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
    private readonly List<Coroutine> firingRoutines = new List<Coroutine>();

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

        firingRoutines.Add(StartCoroutine(RunFiringPattern(
            new BulletHellStagePattern(definition),
            source,
            targets)));
    }

    public void StartFiring(
        BulletHellStageDefinition stage,
        Transform source,
        IReadOnlyList<GameObject> targets)
    {
        StopFiring();
        if (stage == null)
        {
            return;
        }

        foreach (BulletHellStagePattern pattern in stage.Patterns)
        {
            if (pattern?.Shot == null)
            {
                continue;
            }

            firingRoutines.Add(StartCoroutine(RunFiringPattern(
                pattern,
                source,
                targets)));
        }
    }

    public void StopFiring()
    {
        foreach (Coroutine routine in firingRoutines)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }
        }

        firingRoutines.Clear();
    }

    public void Fire(
        BulletHellShotDefinition definition,
        Transform source,
        IReadOnlyList<GameObject> targets)
    {
        Fire(definition, source, targets, 0f);
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
        BulletHellStagePattern pattern,
        Transform source,
        IReadOnlyList<GameObject> targets)
    {
        BulletHellShotDefinition definition = pattern.Shot;
        if (pattern.InitialDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(pattern.InitialDelaySeconds);
        }

        while (true)
        {
            for (int burstIndex = 0;
                 burstIndex < definition.BurstCount;
                 burstIndex++)
            {
                Fire(
                    definition,
                    source,
                    targets,
                    definition.GetBurstAngleOffset(burstIndex));
                if (burstIndex + 1 < definition.BurstCount &&
                    definition.BurstIntervalSeconds > 0f)
                {
                    yield return new WaitForSeconds(
                        definition.BurstIntervalSeconds);
                }
            }

            if (definition.RepeatIntervalSeconds <= 0f)
            {
                yield break;
            }

            float burstDuration =
                (definition.BurstCount - 1) * definition.BurstIntervalSeconds;
            float remainingInterval =
                definition.RepeatIntervalSeconds - burstDuration;
            yield return remainingInterval > 0f
                ? new WaitForSeconds(remainingInterval)
                : null;
        }
    }

    private void Fire(
        BulletHellShotDefinition definition,
        Transform source,
        IReadOnlyList<GameObject> targets,
        float burstAngleOffset)
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

                FireForTarget(
                    definition,
                    sourcePosition,
                    target,
                    burstAngleOffset);
                firedForTarget = true;
            }
        }

        if (!firedForTarget &&
            definition.AttackType == BulletHellAttackType.Projectile)
        {
            FireProjectiles(
                definition,
                sourcePosition,
                null,
                0,
                burstAngleOffset);
        }

        activeLasers.RemoveAll(laser => laser == null);
    }

    private void FireForTarget(
        BulletHellShotDefinition definition,
        Vector2 sourcePosition,
        PlayerAgent target,
        float burstAngleOffset)
    {
        switch (definition.AttackType)
        {
            case BulletHellAttackType.Projectile:
                FireProjectiles(
                    definition,
                    sourcePosition,
                    target.transform,
                    target.LogicalLayer,
                    burstAngleOffset);
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
        int logicalLayer,
        float burstAngleOffset)
    {
        if (enemyBulletPrefab == null || definition.Bullet == null)
        {
            return;
        }

        Vector2 baseDirection = ResolveAimDirection(
            definition.AimType,
            sourcePosition,
            aimTarget,
            definition.LeadMultiplier);
        baseDirection = Quaternion.Euler(
            0f,
            0f,
            definition.AimAngleOffsetDegrees + burstAngleOffset) *
            baseDirection;

        for (int index = 0; index < definition.ProjectileCount; index++)
        {
            float angleOffset = definition.GetProjectileAngleOffset(index);
            Vector2 direction = Quaternion.Euler(0f, 0f, angleOffset) *
                baseDirection;
            Vector2 projectilePosition = sourcePosition;
            if (definition.ConvergesOnAimPoint && aimTarget != null)
            {
                Vector2 aimPoint = aimTarget.position;
                float travelDistance = Vector2.Distance(sourcePosition, aimPoint);
                projectilePosition = aimPoint -
                    direction.normalized * travelDistance;
            }

            BulletStructure projectileStructure =
                definition.GetProjectileStructure(index);
            if (definition.ProjectileWarningDuration > 0f && aimTarget != null)
            {
                firingRoutines.Add(StartCoroutine(SpawnWarnedProjectile(
                    projectilePosition,
                    direction,
                    projectileStructure,
                    logicalLayer,
                    aimTarget,
                    definition.ProjectileWarningDuration)));
            }
            else
            {
                SpawnProjectile(
                    projectilePosition,
                    direction,
                    projectileStructure,
                    logicalLayer,
                    aimTarget);
            }
        }
    }

    private void SpawnProjectile(
        Vector2 position,
        Vector2 direction,
        BulletStructure structure,
        int logicalLayer,
        Transform aimTarget)
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
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        bullet bulletData = bulletObject.GetComponent<bullet>();
        if (bulletData == null)
        {
            bulletData = bulletObject.AddComponent<bullet>();
        }

        bulletData.SetData(
            movementVector,
            structure,
            logicalLayer,
            aimTarget,
            enemyBulletPrefab);
        bulletObject.SetActive(true);
        body.position = position;
        body.linearVelocity = movementVector;
    }

    private IEnumerator SpawnWarnedProjectile(
        Vector2 position,
        Vector2 direction,
        BulletStructure structure,
        int logicalLayer,
        Transform aimTarget,
        float warningDuration)
    {
        PlayerAgent targetPlayer = aimTarget != null
            ? aimTarget.GetComponent<PlayerAgent>()
            : null;
        if (targetPlayer != null)
        {
            GameObject warningObject = new GameObject(
                $"Projectile Warning Layer {logicalLayer}",
                typeof(LineRenderer),
                typeof(LaserAttack));
            warningObject.transform.SetParent(transform, true);
            LaserAttack warningLine = warningObject.GetComponent<LaserAttack>();
            warningLine.Configure(
                position,
                direction,
                targetPlayer,
                logicalLayer,
                warningDuration,
                0f,
                1000f,
                2f,
                2f,
                structure.ThreatLevel);
            activeLasers.Add(warningLine);
        }

        yield return new WaitForSeconds(warningDuration);
        if (this != null && aimTarget != null)
        {
            SpawnProjectile(
                position,
                direction,
                structure,
                logicalLayer,
                aimTarget);
        }
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
            target.transform,
            0f);
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
        Transform target,
        float leadMultiplier)
    {
        if (aimType != BulletAimType.PlayerAimed || target == null)
        {
            return Vector2.down;
        }

        Vector2 relativePosition = (Vector2)target.position - sourcePosition;
        Vector2 direction = relativePosition.normalized;
        if (Mathf.Abs(leadMultiplier) <= Mathf.Epsilon ||
            relativePosition.sqrMagnitude <= Mathf.Epsilon ||
            !target.TryGetComponent(out Rigidbody2D targetBody))
        {
            return direction;
        }

        float lineOfSightRateRadians =
            (relativePosition.x * targetBody.linearVelocity.y -
             relativePosition.y * targetBody.linearVelocity.x) /
            relativePosition.sqrMagnitude;
        float leadAngle =
            lineOfSightRateRadians * Mathf.Rad2Deg * leadMultiplier;
        return Quaternion.Euler(0f, 0f, leadAngle) * direction;
    }

    private void OnDestroy()
    {
        // Scene shutdown destroys active projectiles and child lasers itself.
        // Returning projectiles here could create the persistent pool root from
        // inside OnDestroy, which Unity rejects while closing the scene.
        StopFiring();
    }
}
