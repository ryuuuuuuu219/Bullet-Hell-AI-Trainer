using UnityEngine;

public sealed class bullet : MonoBehaviour
{
    [SerializeField] private Vector2 vector;
    [SerializeField, Min(0)] private int threatLevel = 1;
    [SerializeField, Min(0)] private int logicalLayer;

    private Rigidbody2D body;
    private Transform target;
    private GameObject sourcePrefab;
    private float splitTime;
    private Vector2 previousLineOfSight;
    private bool hasPreviousLineOfSight;

    public Vector2 Vector => vector;
    public int ThreatLevel => threatLevel;
    public int LogicalLayer => logicalLayer;
    public BulletStructure Structure { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            vector = body.linearVelocity;
        }
    }

    private void OnEnable()
    {
        BulletManager.Register(this);
    }

    private void FixedUpdate()
    {
        if (ProjectilePool.ReleaseIfOutsideCameraView(gameObject))
        {
            return;
        }

        if (body == null || Structure == null)
        {
            return;
        }

        ApplyMotion();
        vector = body.linearVelocity;

        if (Structure.HasSplit && Time.time >= splitTime)
        {
            SplitProjectile();
        }
    }

    private void OnDisable()
    {
        BulletManager.Unregister(this);
    }

    public void SetData(Vector2 movementVector, int threat)
    {
        SetData(movementVector, threat, logicalLayer);
    }

    public void SetData(Vector2 movementVector, int threat, int layer)
    {
        SetData(
            movementVector,
            BulletStructure.Straight(movementVector.magnitude, threat),
            layer);
    }

    public void SetData(
        Vector2 movementVector,
        BulletStructure structure,
        int layer)
    {
        SetData(movementVector, structure, layer, null, null);
    }

    public void SetData(
        Vector2 movementVector,
        BulletStructure structure,
        int layer,
        Transform aimTarget,
        GameObject projectilePrefab)
    {
        bool refreshRegistration = isActiveAndEnabled &&
                                   logicalLayer != Mathf.Max(0, layer);
        if (refreshRegistration)
        {
            BulletManager.Unregister(this);
        }

        gameObject.layer = ProjectileCollisionLayers.EnemyBullet;
        vector = movementVector;
        Structure = structure ?? BulletStructure.Straight(
            movementVector.magnitude,
            threatLevel);
        threatLevel = Structure.ThreatLevel;
        logicalLayer = Mathf.Max(0, layer);
        target = aimTarget;
        sourcePrefab = projectilePrefab;
        splitTime = Structure.HasSplit
            ? Time.time + Structure.SplitDelaySeconds
            : float.PositiveInfinity;
        previousLineOfSight = GetLineOfSight();
        hasPreviousLineOfSight = previousLineOfSight.sqrMagnitude > Mathf.Epsilon;
        LogicalLayerVisibility.Apply(gameObject, logicalLayer);

        if (refreshRegistration)
        {
            BulletManager.Register(this);
        }
    }

    private void ApplyMotion()
    {
        switch (Structure.MotionType)
        {
            case BulletMotionType.ConstantTurn:
                body.linearVelocity = Rotate(
                    body.linearVelocity,
                    Structure.TurnRateDegreesPerSecond * Time.fixedDeltaTime);
                break;
            case BulletMotionType.Homing:
                TurnTowardTarget(Structure.TurnRateDegreesPerSecond);
                break;
            case BulletMotionType.ProportionalNavigation:
                ApplyProportionalNavigation();
                break;
        }
    }

    private void TurnTowardTarget(float maximumTurnRate)
    {
        Vector2 lineOfSight = GetLineOfSight();
        if (lineOfSight.sqrMagnitude <= Mathf.Epsilon ||
            body.linearVelocity.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        float currentAngle = Mathf.Atan2(
            body.linearVelocity.y,
            body.linearVelocity.x) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(lineOfSight.y, lineOfSight.x) * Mathf.Rad2Deg;
        float nextAngle = Mathf.MoveTowardsAngle(
            currentAngle,
            targetAngle,
            Mathf.Abs(maximumTurnRate) * Time.fixedDeltaTime);
        body.linearVelocity = DirectionFromAngle(nextAngle) * Structure.Speed;
    }

    private void ApplyProportionalNavigation()
    {
        Vector2 lineOfSight = GetLineOfSight();
        if (lineOfSight.sqrMagnitude <= Mathf.Epsilon ||
            body.linearVelocity.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        if (!hasPreviousLineOfSight)
        {
            previousLineOfSight = lineOfSight;
            hasPreviousLineOfSight = true;
            return;
        }

        float previousAngle = Mathf.Atan2(
            previousLineOfSight.y,
            previousLineOfSight.x) * Mathf.Rad2Deg;
        float currentLineOfSightAngle = Mathf.Atan2(
            lineOfSight.y,
            lineOfSight.x) * Mathf.Rad2Deg;
        float lineOfSightRate = Mathf.DeltaAngle(
            previousAngle,
            currentLineOfSightAngle) / Time.fixedDeltaTime;
        float commandedTurnRate = Mathf.Clamp(
            lineOfSightRate * Structure.NavigationConstant,
            -Mathf.Abs(Structure.TurnRateDegreesPerSecond),
            Mathf.Abs(Structure.TurnRateDegreesPerSecond));
        body.linearVelocity = Rotate(
            body.linearVelocity,
            commandedTurnRate * Time.fixedDeltaTime).normalized * Structure.Speed;
        previousLineOfSight = lineOfSight;
    }

    private void SplitProjectile()
    {
        BulletStructure childStructure = Structure.ChildStructure;
        if (sourcePrefab == null || childStructure == null)
        {
            ProjectilePool.Release(gameObject);
            return;
        }

        Vector2 baseDirection = body.linearVelocity.sqrMagnitude > Mathf.Epsilon
            ? body.linearVelocity.normalized
            : Vector2.down;
        if (Structure.SplitAimType == BulletSplitAimType.PlayerAimed)
        {
            Vector2 lineOfSight = GetLineOfSight();
            if (lineOfSight.sqrMagnitude > Mathf.Epsilon)
            {
                baseDirection = lineOfSight;
            }
        }

        float centerIndex = (Structure.SplitProjectileCount - 1) * 0.5f;
        Vector2 spawnPosition = body.position;
        for (int index = 0; index < Structure.SplitProjectileCount; index++)
        {
            float angleOffset =
                (index - centerIndex) * Structure.SplitAngleIntervalDegrees;
            Vector2 direction = Rotate(baseDirection, angleOffset).normalized;
            GameObject childObject = ProjectilePool.Acquire(
                sourcePrefab,
                spawnPosition,
                Quaternion.identity);
            if (childObject == null ||
                !childObject.TryGetComponent(out Rigidbody2D childBody))
            {
                if (childObject != null)
                {
                    ProjectilePool.Release(childObject);
                }

                continue;
            }

            if (!childObject.TryGetComponent(out bullet childData))
            {
                childData = childObject.AddComponent<bullet>();
            }

            Vector2 childVelocity = direction * childStructure.Speed;
            childBody.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
            childData.SetData(
                childVelocity,
                childStructure,
                logicalLayer,
                target,
                sourcePrefab);
            childObject.SetActive(true);
            childBody.position = spawnPosition;
            childBody.linearVelocity = childVelocity;
        }

        ProjectilePool.Release(gameObject);
    }

    private Vector2 GetLineOfSight()
    {
        return target != null
            ? ((Vector2)target.position - (body != null
                ? body.position
                : (Vector2)transform.position)).normalized
            : Vector2.zero;
    }

    private static Vector2 Rotate(Vector2 value, float angleDegrees)
    {
        return Quaternion.Euler(0f, 0f, angleDegrees) * value;
    }

    private static Vector2 DirectionFromAngle(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerAgent player = other.GetComponent<PlayerAgent>();
        if (player == null || player.LogicalLayer != logicalLayer)
        {
            return;
        }

        player.RegisterHit(this);
        ProjectilePool.Release(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        threatLevel = Mathf.Max(0, threatLevel);
        logicalLayer = Mathf.Max(0, logicalLayer);
    }
#endif
}
