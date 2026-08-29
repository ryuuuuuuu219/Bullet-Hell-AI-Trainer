using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CircularSensorData
{
    public float innerRadius;
    public float radius = 5f;
    public float angle = 90f;
    public float centerAngle = 90f;
    public float priority = 1f;
    public int arcSegments = 16;
}

public sealed class CircularSensor : MonoBehaviour
{
    public const float NearRadius = 60f;
    public const float FarRadius = 150f;
    public const float SensorAngle = 72f;
    public const float SensorPriority = 1f;
    public const int SensorArcSegments = 12;

    private static readonly float[] CenterAngles = { 0f, 72f, 144f, 216f, 288f };

    [Min(0f)] public float innerRadius;
    public float radius = 5f;
    [Range(1f, 359f)] public float angle = 90f;
    public float centerAngle = 90f;
    public float priority = 1f;
    [Range(3, 64)] public int arcSegments = 16;

    private PlayerAgent player;

    private void Awake()
    {
        player = GetComponentInParent<PlayerAgent>();
    }

    private void OnEnable()
    {
        RemoveLegacyPhysicsComponents();
    }

    public static List<CircularSensorData> CreateDefaultData()
    {
        List<CircularSensorData> data = new List<CircularSensorData>(10);
        AddSensorRange(data, 0f, NearRadius);
        AddSensorRange(data, NearRadius, FarRadius);
        return data;
    }

    private static void AddSensorRange(
        ICollection<CircularSensorData> destination,
        float minimumRadius,
        float maximumRadius)
    {
        foreach (float centerAngle in CenterAngles)
        {
            destination.Add(new CircularSensorData
            {
                innerRadius = minimumRadius,
                radius = maximumRadius,
                angle = SensorAngle,
                centerAngle = centerAngle,
                priority = SensorPriority,
                arcSegments = SensorArcSegments,
            });
        }
    }

    public void RebuildFanCollider()
    {
        radius = Mathf.Max(0.01f, radius);
        innerRadius = Mathf.Clamp(innerRadius, 0f, radius);
        angle = Mathf.Clamp(angle, 1f, 359f);
        arcSegments = Mathf.Clamp(arcSegments, 3, 64);
    }

    public int Sense()
    {
        if (player == null)
        {
            player = GetComponentInParent<PlayerAgent>();
        }

        if (player == null)
        {
            return 0;
        }

        int count = 0;
        float innerRadiusSquared = innerRadius * innerRadius;
        float radiusSquared = radius * radius;
        foreach (bullet detectedBullet in
                 BulletManager.GetActiveBullets(player.LogicalLayer))
        {
            if (detectedBullet == null)
            {
                continue;
            }

            Vector2 localPosition = transform.InverseTransformPoint(
                detectedBullet.transform.position);
            float distanceSquared = localPosition.sqrMagnitude;
            if (distanceSquared <= innerRadiusSquared ||
                distanceSquared > radiusSquared)
            {
                continue;
            }

            float bulletAngle = Mathf.Atan2(localPosition.y, localPosition.x) *
                                Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(centerAngle, bulletAngle)) <=
                angle * 0.5f)
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveLegacyPhysicsComponents()
    {
        foreach (Collider2D sensorCollider in GetComponents<Collider2D>())
        {
            sensorCollider.enabled = false;
            Destroy(sensorCollider);
        }

        Rigidbody2D sensorBody = GetComponent<Rigidbody2D>();
        if (sensorBody != null)
        {
            sensorBody.simulated = false;
            Destroy(sensorBody);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        innerRadius = Mathf.Clamp(innerRadius, 0f, radius);
        angle = Mathf.Clamp(angle, 1f, 359f);
        arcSegments = Mathf.Clamp(arcSegments, 3, 64);

        RebuildFanCollider();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Vector3 origin = transform.position;
        Vector3 previous = transform.TransformPoint(GetArcPoint(0));
        Gizmos.DrawLine(origin, previous);

        int segmentCount = Mathf.Clamp(arcSegments, 3, 64);
        for (int index = 1; index <= segmentCount; index++)
        {
            Vector3 current = transform.TransformPoint(
                GetArcPoint(index / (float)segmentCount));
            Gizmos.DrawLine(previous, current);
            previous = current;
        }

        Gizmos.DrawLine(previous, origin);
    }

    private Vector2 GetArcPoint(float interpolation)
    {
        float theta = Mathf.Deg2Rad *
                      (centerAngle - angle * 0.5f + angle * interpolation);
        return new Vector2(radius * Mathf.Cos(theta), radius * Mathf.Sin(theta));
    }
#endif
}
