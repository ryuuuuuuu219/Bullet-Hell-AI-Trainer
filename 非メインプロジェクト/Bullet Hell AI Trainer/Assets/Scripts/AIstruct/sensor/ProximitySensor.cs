using UnityEngine;

public struct ProximityObservation
{
    public bool isValid;
    public Vector2 relativePosition;
    public float normalizedDistance;
    public float approachDot;
    public float normalizedThreat;
}

public static class ProximitySensor
{
    public const float DistanceReference = 150f;
    private const float ThreatReference = 10f;

    public static float Sense(Vector2 position, int logicalLayer)
    {
        ProximityObservation observation = Observe(position, logicalLayer);
        return observation.isValid
            ? 1f / (1f + observation.normalizedDistance * DistanceReference)
            : 0f;
    }

    public static ProximityObservation Observe(
        Vector2 position,
        int logicalLayer)
    {
        if (!BulletManager.TryGetNearest(
                position,
                logicalLayer,
                out bullet nearestBullet))
        {
            return default;
        }

        Vector2 relativePosition =
            (Vector2)nearestBullet.transform.position - position;
        float distance = relativePosition.magnitude;
        Vector2 toPlayer = -relativePosition.normalized;
        Vector2 bulletDirection = nearestBullet.Vector.sqrMagnitude > 0f
            ? nearestBullet.Vector.normalized
            : Vector2.zero;

        return new ProximityObservation
        {
            isValid = true,
            relativePosition = Vector2.ClampMagnitude(
                relativePosition / DistanceReference,
                1f),
            normalizedDistance = Mathf.Clamp01(distance / DistanceReference),
            approachDot = Vector2.Dot(bulletDirection, toPlayer),
            normalizedThreat = Mathf.Clamp01(
                nearestBullet.ThreatLevel / ThreatReference),
        };
    }
}
