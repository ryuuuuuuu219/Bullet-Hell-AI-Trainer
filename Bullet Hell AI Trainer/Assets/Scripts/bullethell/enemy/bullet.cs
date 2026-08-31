using System.Collections.Generic;
using UnityEngine;

public sealed class bullet : MonoBehaviour
{
    private const float FlightWarningLength = 1000f;
    private const float FlightWarningWidth = 2f;
    private const float MaximumLifetimeSeconds = 30f;

    [SerializeField] private Vector2 vector;
    [SerializeField, Min(0)] private int threatLevel = 1;
    [SerializeField, Min(0)] private int logicalLayer;

    private Rigidbody2D body;
    private Transform target;
    private GameObject sourcePrefab;
    private float splitTime;
    private bool splitWarningStarted;
    private Vector2 warnedSplitDirection;
    private readonly List<LaserAttack> splitWarningLines =
        new List<LaserAttack>();
    private Vector2 previousLineOfSight;
    private bool hasPreviousLineOfSight;
    private float usedTurnAngleDegrees;
    private LineRenderer flightWarningLine;
    private bool showFlightWarningLine;
    private float releaseTime;

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
        if (Time.time >= releaseTime)
        {
            ProjectilePool.Release(gameObject);
            return;
        }

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
            if (!splitWarningStarted &&
                Structure.SplitWarningDurationSeconds > 0f)
            {
                BeginSplitWarning();
            }
            else
            {
                SplitProjectile();
            }
        }
    }

    private void LateUpdate()
    {
        UpdateFlightWarningLine();
    }

    private void OnDisable()
    {
        BulletManager.Unregister(this);
        ClearSplitWarningLines();
        showFlightWarningLine = false;
        if (flightWarningLine != null)
        {
            flightWarningLine.enabled = false;
        }
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
        GameObject projectilePrefab,
        bool enableFlightWarningLine = false)
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
        releaseTime = Time.time + MaximumLifetimeSeconds;
        splitTime = Structure.HasSplit
            ? Time.time + Structure.SplitDelaySeconds
            : float.PositiveInfinity;
        splitWarningStarted = false;
        warnedSplitDirection = Vector2.zero;
        ClearSplitWarningLines();
        previousLineOfSight = GetLineOfSight();
        hasPreviousLineOfSight = previousLineOfSight.sqrMagnitude > Mathf.Epsilon;
        usedTurnAngleDegrees = 0f;
        showFlightWarningLine = enableFlightWarningLine;
        if (showFlightWarningLine)
        {
            EnsureFlightWarningLine();
        }
        else if (flightWarningLine != null)
        {
            flightWarningLine.enabled = false;
        }
        LogicalLayerVisibility.Apply(gameObject, logicalLayer);

        if (refreshRegistration)
        {
            BulletManager.Register(this);
        }
    }

    private void EnsureFlightWarningLine()
    {
        if (flightWarningLine == null)
        {
            GameObject warningObject = new GameObject(
                "Flight Warning Line",
                typeof(LineRenderer));
            warningObject.transform.SetParent(transform, false);
            flightWarningLine = warningObject.GetComponent<LineRenderer>();
            flightWarningLine.useWorldSpace = true;
            flightWarningLine.loop = false;
            flightWarningLine.positionCount = 2;
            flightWarningLine.alignment = LineAlignment.TransformZ;
            flightWarningLine.sharedMaterial = LaserAttack.GetLineMaterial();
            flightWarningLine.startColor = Color.gray;
            flightWarningLine.endColor = Color.gray;
            flightWarningLine.startWidth = FlightWarningWidth;
            flightWarningLine.endWidth = FlightWarningWidth;
        }

        flightWarningLine.enabled = true;
        UpdateFlightWarningLine();
    }

    private void UpdateFlightWarningLine()
    {
        if (!showFlightWarningLine || flightWarningLine == null || body == null)
        {
            if (flightWarningLine != null)
            {
                flightWarningLine.enabled = false;
            }

            return;
        }

        Vector2 velocity = body.linearVelocity;
        if (velocity.sqrMagnitude <= Mathf.Epsilon)
        {
            flightWarningLine.enabled = false;
            return;
        }

        flightWarningLine.enabled =
            LogicalLayerVisibility.IsVisible(logicalLayer);
        Vector2 origin = body.position;
        flightWarningLine.SetPosition(0, origin);
        flightWarningLine.SetPosition(
            1,
            origin + velocity.normalized * FlightWarningLength);
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
        float requestedTurn = Mathf.Clamp(
            Mathf.DeltaAngle(currentAngle, targetAngle),
            -Mathf.Abs(maximumTurnRate) * Time.fixedDeltaTime,
            Mathf.Abs(maximumTurnRate) * Time.fixedDeltaTime);
        float appliedTurn = ApplyTurnAngleBudget(requestedTurn);
        body.linearVelocity = Rotate(
            body.linearVelocity,
            appliedTurn).normalized * Structure.Speed;
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
        float appliedTurn = ApplyTurnAngleBudget(
            commandedTurnRate * Time.fixedDeltaTime);
        body.linearVelocity = Rotate(
            body.linearVelocity,
            appliedTurn).normalized * Structure.Speed;
        previousLineOfSight = lineOfSight;
    }

    private float ApplyTurnAngleBudget(float requestedTurnDegrees)
    {
        float remainingTurn = Mathf.Max(
            0f,
            Structure.TotalTurnAngleDegrees - usedTurnAngleDegrees);
        float appliedTurn = Mathf.Clamp(
            requestedTurnDegrees,
            -remainingTurn,
            remainingTurn);
        usedTurnAngleDegrees += Mathf.Abs(appliedTurn);
        return appliedTurn;
    }

    private void SplitProjectile()
    {
        BulletStructure childStructure = Structure.ChildStructure;
        if (sourcePrefab == null || childStructure == null)
        {
            ProjectilePool.Release(gameObject);
            return;
        }

        Vector2 baseDirection = splitWarningStarted
            ? warnedSplitDirection
            : ResolveSplitDirection();
        if (baseDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            baseDirection = Vector2.down;
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
                sourcePrefab,
                !childStructure.HasSplit);
            childObject.SetActive(true);
            childBody.position = spawnPosition;
            childBody.linearVelocity = childVelocity;
        }

        ProjectilePool.Release(gameObject);
    }

    private void BeginSplitWarning()
    {
        splitWarningStarted = true;
        warnedSplitDirection = ResolveSplitDirection();
        if (warnedSplitDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            warnedSplitDirection = Vector2.down;
        }

        body.linearVelocity = Vector2.zero;
        vector = Vector2.zero;
        splitTime = Time.time + Structure.SplitWarningDurationSeconds;

        PlayerAgent targetPlayer = target != null
            ? target.GetComponent<PlayerAgent>()
            : null;
        if (targetPlayer == null)
        {
            return;
        }

        float centerIndex = (Structure.SplitProjectileCount - 1) * 0.5f;
        Vector2 warningOrigin = body.position;
        for (int index = 0; index < Structure.SplitProjectileCount; index++)
        {
            float angleOffset =
                (index - centerIndex) * Structure.SplitAngleIntervalDegrees;
            Vector2 direction = Rotate(warnedSplitDirection, angleOffset).normalized;
            GameObject warningObject = new GameObject(
                $"Split Projectile Warning Layer {logicalLayer}",
                typeof(LineRenderer),
                typeof(LaserAttack));
            LaserAttack warningLine = warningObject.GetComponent<LaserAttack>();
            warningLine.Configure(
                warningOrigin,
                direction,
                targetPlayer,
                logicalLayer,
                Structure.SplitWarningDurationSeconds,
                0f,
                1000f,
                2f,
                2f,
                Structure.ChildStructure.ThreatLevel);
            splitWarningLines.Add(warningLine);
        }
    }

    private Vector2 ResolveSplitDirection()
    {
        Vector2 baseDirection = body.linearVelocity.sqrMagnitude > Mathf.Epsilon
            ? body.linearVelocity.normalized
            : Vector2.down;
        if (Structure.SplitAimType != BulletSplitAimType.PlayerAimed)
        {
            return baseDirection;
        }

        Vector2 lineOfSight = GetLineOfSight();
        return lineOfSight.sqrMagnitude > Mathf.Epsilon
            ? lineOfSight
            : baseDirection;
    }

    private void ClearSplitWarningLines()
    {
        foreach (LaserAttack warningLine in splitWarningLines)
        {
            if (warningLine != null)
            {
                Destroy(warningLine.gameObject);
            }
        }

        splitWarningLines.Clear();
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
