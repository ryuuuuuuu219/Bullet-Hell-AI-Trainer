using System;
using System.Collections.Generic;
using UnityEngine;

public enum LaserThreatPhase
{
    Warning,
    Active,
}

[Serializable]
public sealed class LaserThreatData
{
    public int id;
    public int logicalLayer;
    public LaserThreatPhase phase;
    public int threatLevel = 4;
    public Vector2 origin;
    public Vector2 direction = Vector2.up;
    public float radius;
    public float remainingWarningTime;
}

public struct LaserSensorObservation
{
    public bool isValid;
    public bool isActive;
    public bool isInsideNearRange;
    public Vector2 relativeOrigin;
    public Vector2 direction;
    public float signedLineDistance;
    public float surfaceDistance;
    public float normalizedWarningTime;
    public float normalizedThreat;
    public float risk;
}

public static class WarningLineSensor
{
    public const int DetectionCapacity = 2;
    public const float NearRadius = 15f;
    public const float DetectionRadius = 50f;
    public const float WarningTimeReference = 10f;

    private const float ThreatReference = 5f;
    private static readonly List<LaserThreatData> ActiveThreats =
        new List<LaserThreatData>();

    public static IReadOnlyList<LaserThreatData> Threats => ActiveThreats;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveThreats.Clear();
    }

    public static void Register(LaserThreatData threat)
    {
        if (threat != null && !ActiveThreats.Contains(threat))
        {
            ActiveThreats.Add(threat);
        }
    }

    public static void Unregister(LaserThreatData threat)
    {
        if (threat != null)
        {
            ActiveThreats.Remove(threat);
        }
    }

    public static void SelectActiveThreats(
        Vector2 playerPosition,
        int logicalLayer,
        LaserSensorObservation[] destination)
    {
        SelectTopThreats(
            ActiveThreats,
            playerPosition,
            logicalLayer,
            destination);
    }

    public static void SelectTopThreats(
        IReadOnlyList<LaserThreatData> threats,
        Vector2 playerPosition,
        int logicalLayer,
        LaserSensorObservation[] destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        Array.Clear(destination, 0, destination.Length);
        if (threats == null || destination.Length == 0)
        {
            return;
        }

        List<LaserSensorObservation> candidates =
            new List<LaserSensorObservation>();
        HashSet<int> detectedIds = new HashSet<int>();

        foreach (LaserThreatData threat in threats)
        {
            if (threat == null ||
                (logicalLayer >= 0 && threat.logicalLayer != logicalLayer))
            {
                continue;
            }

            if (threat.id != 0 && !detectedIds.Add(threat.id))
            {
                continue;
            }

            if (TryCreateObservation(
                    threat,
                    playerPosition,
                    out LaserSensorObservation observation))
            {
                candidates.Add(observation);
            }
        }

        candidates.Sort((left, right) =>
        {
            int riskComparison = right.risk.CompareTo(left.risk);
            return riskComparison != 0
                ? riskComparison
                : left.surfaceDistance.CompareTo(right.surfaceDistance);
        });

        int outputCount = Mathf.Min(
            DetectionCapacity,
            destination.Length,
            candidates.Count);
        for (int index = 0; index < outputCount; index++)
        {
            destination[index] = candidates[index];
        }
    }

    private static bool TryCreateObservation(
        LaserThreatData threat,
        Vector2 playerPosition,
        out LaserSensorObservation observation)
    {
        observation = default;
        if (threat.direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        Vector2 direction = threat.direction.normalized;
        Vector2 toPlayer = playerPosition - threat.origin;
        float distanceAlongRay = Vector2.Dot(toPlayer, direction);
        if (distanceAlongRay < 0f)
        {
            return false;
        }

        float signedLineDistance =
            direction.x * toPlayer.y - direction.y * toPlayer.x;
        float surfaceDistance =
            Mathf.Abs(signedLineDistance) - Mathf.Max(0f, threat.radius);
        if (surfaceDistance > DetectionRadius)
        {
            return false;
        }

        bool isActive = threat.phase == LaserThreatPhase.Active;
        float normalizedWarningTime = Mathf.Clamp01(
            threat.remainingWarningTime / WarningTimeReference);
        float urgency = isActive ? 1f : 1f - normalizedWarningTime;
        float proximity = 1f - Mathf.Clamp01(
            Mathf.Max(surfaceDistance, 0f) / DetectionRadius);
        float normalizedThreat = Mathf.Clamp01(
            threat.threatLevel / ThreatReference);

        observation = new LaserSensorObservation
        {
            isValid = true,
            isActive = isActive,
            isInsideNearRange = surfaceDistance <= NearRadius,
            relativeOrigin = Vector2.ClampMagnitude(
                (threat.origin - playerPosition) / DetectionRadius,
                1f),
            direction = direction,
            signedLineDistance = Mathf.Clamp(
                signedLineDistance / DetectionRadius,
                -1f,
                1f),
            surfaceDistance = Mathf.Clamp(
                surfaceDistance / DetectionRadius,
                -1f,
                1f),
            normalizedWarningTime = normalizedWarningTime,
            normalizedThreat = normalizedThreat,
            risk = normalizedThreat * proximity * urgency,
        };
        return true;
    }
}
