using System;
using System.Collections.Generic;
using UnityEngine;

public struct AttentionObservation
{
    public bool isValid;
    public Vector2 relativePosition;
    public float normalizedDistance;
    public float normalizedClosestDistance;
    public float normalizedClosestTime;
    public float normalizedThreat;
    public float risk;
}

public static class AttentionSensor
{
    public const int AttentionCapacity = 5;
    public const float DistanceReference = 150f;
    public const float PredictionTimeLimit = 3f;

    private const float ThreatReference = 5f;

    public static void Select(
        Vector2 playerPosition,
        Vector2 playerVelocity,
        int logicalLayer,
        AttentionObservation[] destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        Array.Clear(destination, 0, destination.Length);
        List<AttentionObservation> candidates =
            new List<AttentionObservation>();

        foreach (bullet candidate in BulletManager.ActiveBullets)
        {
            if (candidate == null ||
                (logicalLayer >= 0 && candidate.LogicalLayer != logicalLayer))
            {
                continue;
            }

            Vector2 relativePosition =
                (Vector2)candidate.transform.position - playerPosition;
            Vector2 relativeVelocity = candidate.Vector - playerVelocity;
            float velocitySquared = relativeVelocity.sqrMagnitude;
            float closestTime = velocitySquared > Mathf.Epsilon
                ? Mathf.Clamp(
                    -Vector2.Dot(relativePosition, relativeVelocity) /
                    velocitySquared,
                    0f,
                    PredictionTimeLimit)
                : 0f;
            float distance = relativePosition.magnitude;
            float closestDistance =
                (relativePosition + relativeVelocity * closestTime).magnitude;
            float normalizedThreat = Mathf.Clamp01(
                candidate.ThreatLevel / ThreatReference);
            float closestProximity = 1f - Mathf.Clamp01(
                closestDistance / DistanceReference);
            float urgency = 1f - Mathf.Clamp01(
                closestTime / PredictionTimeLimit);

            candidates.Add(new AttentionObservation
            {
                isValid = true,
                relativePosition = Vector2.ClampMagnitude(
                    relativePosition / DistanceReference,
                    1f),
                normalizedDistance = Mathf.Clamp01(
                    distance / DistanceReference),
                normalizedClosestDistance = Mathf.Clamp01(
                    closestDistance / DistanceReference),
                normalizedClosestTime = Mathf.Clamp01(
                    closestTime / PredictionTimeLimit),
                normalizedThreat = normalizedThreat,
                risk = normalizedThreat * closestProximity * urgency,
            });
        }

        candidates.Sort((left, right) => right.risk.CompareTo(left.risk));
        int outputCount = Mathf.Min(
            AttentionCapacity,
            destination.Length,
            candidates.Count);
        for (int index = 0; index < outputCount; index++)
        {
            destination[index] = candidates[index];
        }
    }
}
