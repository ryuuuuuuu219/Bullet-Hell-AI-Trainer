using System.Collections.Generic;
using UnityEngine;

public static class ScriptedTeacherSettings
{
    private const string EnabledKey = "BulletHellAITrainer.ScriptedTeacherEnabled.v1";

    public static bool IsEnabled => PlayerPrefs.GetInt(EnabledKey, 1) != 0;

    public static void Save(bool enabled)
    {
        PlayerPrefs.SetInt(EnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAgent))]
[RequireComponent(typeof(PlayerMovementController))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ScriptedTeacher : MonoBehaviour, ITeacherTargetProvider
{
    private const float ThreatReference = 10f;
    [SerializeField, Min(0.1f)] private float predictionTime = 2f;
    [SerializeField, Min(4)] private int candidateDirectionCount = 16;
    [SerializeField] private float[] candidateTimes = { 0f, 0.25f, 0.5f, 1f };
    [SerializeField, Min(0.01f)] private float simulationStep = 0.05f;
    [SerializeField, Min(0f)] private float losRateThresholdDegrees = 2f;
    [SerializeField, Range(0f, 1f)] private float previousDirectionWeight = 0.3f;
    [SerializeField] private int debugBulletThreatCount;
    [SerializeField] private int debugLaserThreatCount;
    [SerializeField] private Vector2 debugSelectedTarget;
    [SerializeField] private Vector2 debugTeacherTarget;

    private readonly List<BulletThreat> threats = new List<BulletThreat>();
    private readonly List<float> clearances = new List<float>();
    private PlayerAgent agent;
    private PlayerMovementController movement;
    private Rigidbody2D body;
    private Collider2D hitbox;
    private Camera movementCamera;
    private Vector2 previousDirection;

    private readonly struct BulletThreat
    {
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly float Radius;
        public readonly float Weight;
        public readonly Vector2 Avoidance;

        public BulletThreat(Vector2 position, Vector2 velocity, float radius,
            float weight, Vector2 avoidance)
        {
            Position = position;
            Velocity = velocity;
            Radius = radius;
            Weight = weight;
            Avoidance = avoidance;
        }
    }

    private struct Candidate
    {
        public bool Valid;
        public Vector2 Target;
        public Vector2 Direction;
        public float TravelTime;
        public float First;
        public float Second;
        public float Third;
        public float Sum;
    }

    private void Awake()
    {
        agent = GetComponent<PlayerAgent>();
        movement = GetComponent<PlayerMovementController>();
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<Collider2D>();
        movement.SetScriptedTeacherTargetProvider(this);
    }

    public bool TryGetTeacherTarget(out Vector2 targetMovement)
    {
        targetMovement = Vector2.zero;
        if (agent == null || movement == null || body == null ||
            !TryGetCamera(out Camera camera))
        {
            return false;
        }

        Vector2 start = body.position;
        float playerRadius = RadiusOf(hitbox);
        Vector2 combined = CollectThreats(start, body.linearVelocity, playerRadius);
        debugLaserThreatCount = CountLasers();

        if (threats.Count == 0 && debugLaserThreatCount == 0)
        {
            previousDirection = Vector2.zero;
            Vector2 initialPosition = StageSpawnManager.PlayerInitialPosition;
            debugSelectedTarget = initialPosition;
            targetMovement = MoveToward(initialPosition, start);
            debugTeacherTarget = targetMovement;
            return true;
        }

        Candidate best = Evaluate(start, playerRadius, Vector2.zero, 0f, start);
        float baseAngle = combined.sqrMagnitude > Mathf.Epsilon
            ? Mathf.Atan2(combined.y, combined.x) * Mathf.Rad2Deg
            : 0f;
        float angleStep = 360f / Mathf.Max(4, candidateDirectionCount);

        for (int i = 0; i < candidateDirectionCount; i++)
        {
            float angle = (baseAngle + i * angleStep) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            foreach (float travelTime in candidateTimes)
            {
                if (travelTime <= Mathf.Epsilon) continue;
                Vector2 target = start + direction * movement.MoveSpeed * travelTime;

                // A screen-exterior target is invalid; it is never clamped.
                if (!InsideView(camera, target)) continue;

                Candidate candidate = Evaluate(
                    start, playerRadius, direction, travelTime, target);
                if (Better(candidate, best)) best = candidate;
            }
        }

        if (!best.Valid) return false;
        debugSelectedTarget = best.Target;
        targetMovement = best.Direction;
        if (targetMovement.sqrMagnitude > Mathf.Epsilon &&
            previousDirection.sqrMagnitude > Mathf.Epsilon)
        {
            targetMovement = Vector2.Lerp(
                targetMovement, previousDirection, previousDirectionWeight).normalized;
        }
        previousDirection = targetMovement;
        debugTeacherTarget = targetMovement;
        return true;
    }

    private Vector2 CollectThreats(Vector2 playerPosition,
        Vector2 playerVelocity, float playerRadius)
    {
        threats.Clear();
        Vector2 combined = Vector2.zero;
        float losThreshold = losRateThresholdDegrees * Mathf.Deg2Rad;
        IReadOnlyList<bullet> bullets =
            BulletManager.GetActiveBullets(agent.LogicalLayer);

        foreach (bullet source in bullets)
        {
            if (source == null || !source.isActiveAndEnabled) continue;
            Vector2 velocity = source.Vector;
            Vector2 position = source.transform.position;
            Vector2 relativePosition = position - playerPosition;
            Vector2 relativeVelocity = velocity - playerVelocity;
            float speedSquared = relativeVelocity.sqrMagnitude;
            if (velocity.sqrMagnitude <= Mathf.Epsilon ||
                speedSquared <= Mathf.Epsilon ||
                Vector2.Dot(relativePosition, relativeVelocity) >= 0f) continue;

            float closestTime = -Vector2.Dot(relativePosition, relativeVelocity) /
                speedSquared;
            if (closestTime < 0f || closestTime > predictionTime) continue;

            float closestDistance =
                (relativePosition + relativeVelocity * closestTime).magnitude;
            float bulletRadius = RadiusOf(source.GetComponent<Collider2D>());
            float safeDistance = playerRadius * 2f + bulletRadius;
            float losRate = relativePosition.sqrMagnitude > Mathf.Epsilon
                ? Mathf.Abs(Cross(relativePosition, relativeVelocity) /
                    relativePosition.sqrMagnitude)
                : 0f;
            if (losRate >= losThreshold && closestDistance > safeDistance) continue;

            Vector2 avoidance = new Vector2(-velocity.y, velocity.x).normalized;
            float side = Vector2.Dot(playerPosition - position, avoidance);
            if (side < 0f || (Mathf.Approximately(side, 0f) &&
                previousDirection.sqrMagnitude > Mathf.Epsilon &&
                Vector2.Dot(previousDirection, avoidance) < 0f))
            {
                avoidance = -avoidance;
            }

            float urgency = 1f - Mathf.Clamp01(closestTime / predictionTime);
            float distanceRisk = 1f - Mathf.Clamp01(
                closestDistance / Mathf.Max(safeDistance * 2f, 0.01f));
            float losRisk = losThreshold > Mathf.Epsilon
                ? 1f - Mathf.Clamp01(losRate / losThreshold)
                : 0f;
            float weight = Mathf.Max(0.1f, source.ThreatLevel / ThreatReference) *
                urgency * Mathf.Max(distanceRisk, losRisk);
            BulletThreat threat = new BulletThreat(
                position, velocity, bulletRadius, weight, avoidance);
            threats.Add(threat);
            combined += threat.Avoidance * threat.Weight;
        }
        debugBulletThreatCount = threats.Count;
        return combined;
    }

    private Candidate Evaluate(Vector2 start, float playerRadius,
        Vector2 direction, float travelTime, Vector2 target)
    {
        clearances.Clear();
        int stepCount = StepCount();

        foreach (BulletThreat threat in threats)
        {
            clearances.Add(MinimumBulletClearance(
                threat, start, playerRadius, direction, travelTime));
        }

        foreach (LaserThreatData laser in WarningLineSensor.Threats)
        {
            if (!Relevant(laser)) continue;
            float minimum = float.PositiveInfinity;
            for (int i = 0; i <= stepCount; i++)
            {
                float time = StepTime(i);
                if (laser.phase == LaserThreatPhase.Warning &&
                    time < laser.remainingWarningTime) continue;

                Vector2 player = PredictPlayer(start, direction, travelTime, time);
                Vector2 rayDirection = laser.direction.normalized;
                float along = Mathf.Max(
                    0f, Vector2.Dot(player - laser.origin, rayDirection));
                Vector2 closest = laser.origin + rayDirection * along;
                minimum = Mathf.Min(minimum, Vector2.Distance(player, closest) -
                    (playerRadius * 2f + Mathf.Max(0f, laser.radius)));
            }
            if (!float.IsPositiveInfinity(minimum)) clearances.Add(minimum);
        }

        clearances.Sort();
        if (clearances.Count == 0) return default;
        float first = clearances[0];
        float second = clearances.Count > 1 ? clearances[1] : first;
        float third = clearances.Count > 2 ? clearances[2] : second;
        float sum = 0f;
        foreach (float value in clearances) sum += value;
        return new Candidate
        {
            Valid = true,
            Target = target,
            Direction = direction,
            TravelTime = travelTime,
            First = first,
            Second = second,
            Third = third,
            Sum = sum,
        };
    }

    private float MinimumBulletClearance(
        BulletThreat threat,
        Vector2 start,
        float playerRadius,
        Vector2 direction,
        float travelTime)
    {
        float movingDuration = Mathf.Min(travelTime, predictionTime);
        Vector2 initialOffset = threat.Position - start;
        Vector2 relativeVelocity = threat.Velocity -
            direction * movement.MoveSpeed;
        float minimumDistance = MinimumRelativeDistance(
            initialOffset, relativeVelocity, movingDuration);

        if (movingDuration < predictionTime)
        {
            Vector2 stopPosition = start +
                direction * movement.MoveSpeed * movingDuration;
            Vector2 offsetAtStop = threat.Position +
                threat.Velocity * movingDuration - stopPosition;
            minimumDistance = Mathf.Min(
                minimumDistance,
                MinimumRelativeDistance(
                    offsetAtStop,
                    threat.Velocity,
                    predictionTime - movingDuration));
        }

        return minimumDistance - (playerRadius * 2f + threat.Radius);
    }

    private static float MinimumRelativeDistance(
        Vector2 initialOffset,
        Vector2 relativeVelocity,
        float duration)
    {
        if (duration <= 0f || relativeVelocity.sqrMagnitude <= Mathf.Epsilon)
        {
            return initialOffset.magnitude;
        }

        float closestTime = Mathf.Clamp(
            -Vector2.Dot(initialOffset, relativeVelocity) /
            relativeVelocity.sqrMagnitude,
            0f,
            duration);
        return (initialOffset + relativeVelocity * closestTime).magnitude;
    }

    private int CountLasers()
    {
        int count = 0;
        foreach (LaserThreatData laser in WarningLineSensor.Threats)
            if (Relevant(laser)) count++;
        return count;
    }

    private bool Relevant(LaserThreatData laser)
    {
        return laser != null &&
            laser.logicalLayer == agent.LogicalLayer &&
            laser.direction.sqrMagnitude > Mathf.Epsilon &&
            (laser.phase == LaserThreatPhase.Active ||
             laser.remainingWarningTime <= predictionTime);
    }

    private int StepCount()
    {
        return Mathf.Max(1, Mathf.CeilToInt(
            predictionTime / Mathf.Max(0.01f, simulationStep)));
    }

    private float StepTime(int index)
    {
        return Mathf.Min(
            index * Mathf.Max(0.01f, simulationStep), predictionTime);
    }

    private Vector2 PredictPlayer(Vector2 start, Vector2 direction,
        float travelTime, float predictionOffset)
    {
        return start + direction * movement.MoveSpeed *
            Mathf.Min(predictionOffset, travelTime);
    }

    private static bool Better(Candidate candidate, Candidate best)
    {
        if (!candidate.Valid) return false;
        if (!best.Valid) return true;
        int result = Compare(candidate.First, best.First);
        if (result != 0) return result > 0;
        result = Compare(candidate.Second, best.Second);
        if (result != 0) return result > 0;
        result = Compare(candidate.Third, best.Third);
        if (result != 0) return result > 0;
        result = Compare(candidate.Sum, best.Sum);
        if (result != 0) return result > 0;
        return candidate.TravelTime < best.TravelTime;
    }

    private static int Compare(float left, float right)
    {
        float difference = left - right;
        return Mathf.Abs(difference) <= 0.0001f
            ? 0
            : difference > 0f ? 1 : -1;
    }

    private Vector2 MoveToward(Vector2 target, Vector2 current)
    {
        float step = movement.MoveSpeed * Time.fixedDeltaTime;
        return step > Mathf.Epsilon
            ? Vector2.ClampMagnitude((target - current) / step, 1f)
            : Vector2.zero;
    }

    private bool TryGetCamera(out Camera camera)
    {
        if (movementCamera == null) movementCamera = Camera.main;
        camera = movementCamera;
        return camera != null;
    }

    private static bool InsideView(Camera camera, Vector2 position)
    {
        Vector3 viewport = camera.WorldToViewportPoint(
            new Vector3(position.x, position.y, 0f));
        return viewport.z > 0f &&
            viewport.x >= 0f && viewport.x <= 1f &&
            viewport.y >= 0f && viewport.y <= 1f;
    }

    private static float RadiusOf(Collider2D collider)
    {
        if (collider == null) return 0f;
        Vector3 extents = collider.bounds.extents;
        return Mathf.Max(extents.x, extents.y);
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        predictionTime = Mathf.Max(0.1f, predictionTime);
        candidateDirectionCount = Mathf.Max(4, candidateDirectionCount);
        simulationStep = Mathf.Max(0.01f, simulationStep);
        losRateThresholdDegrees = Mathf.Max(0f, losRateThresholdDegrees);
        previousDirectionWeight = Mathf.Clamp01(previousDirectionWeight);
        if (candidateTimes == null || candidateTimes.Length == 0)
            candidateTimes = new[] { 0f, 0.25f, 0.5f, 1f };
        for (int i = 0; i < candidateTimes.Length; i++)
            candidateTimes[i] = Mathf.Max(0f, candidateTimes[i]);
    }
#endif
}
