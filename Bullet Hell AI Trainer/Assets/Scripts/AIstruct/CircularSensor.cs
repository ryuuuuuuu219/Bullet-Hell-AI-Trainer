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

    private PolygonCollider2D sensorCollider;
    private int detectedBulletCount;

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

    private void Start()
    {
        ConfigurePhysics();
        RebuildFanCollider();
    }

    public void RebuildFanCollider()
    {
        sensorCollider ??= GetComponent<PolygonCollider2D>();
        if (sensorCollider == null)
        {
            sensorCollider = gameObject.AddComponent<PolygonCollider2D>();
        }

        sensorCollider.isTrigger = true;
        radius = Mathf.Max(0.01f, radius);
        innerRadius = Mathf.Clamp(innerRadius, 0f, radius);
        angle = Mathf.Clamp(angle, 1f, 359f);
        arcSegments = Mathf.Clamp(arcSegments, 3, 64);

        Vector2[] points = new Vector2[arcSegments + 2];
        points[0] = Vector2.zero;

        float startAngle = centerAngle - angle * 0.5f;
        for (int index = 0; index <= arcSegments; index++)
        {
            float interpolation = index / (float)arcSegments;
            float theta = Mathf.Deg2Rad * (startAngle + angle * interpolation);
            points[index + 1] = new Vector2(
                radius * Mathf.Cos(theta),
                radius * Mathf.Sin(theta));
        }

        sensorCollider.pathCount = 1;
        sensorCollider.SetPath(0, points);
    }

    public int Sense()
    {
        int count = detectedBulletCount;
        detectedBulletCount = 0;
        return count;
    }

    private void ConfigurePhysics()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision.TryGetComponent(out bullet detectedBullet))
        {
            return;
        }

        PlayerAgent player = GetComponentInParent<PlayerAgent>();
        if (player != null && detectedBullet.LogicalLayer != player.LogicalLayer)
        {
            return;
        }

        float distance = Vector2.Distance(
            transform.position,
            detectedBullet.transform.position);
        if (distance <= innerRadius)
        {
            return;
        }

        detectedBulletCount++;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        innerRadius = Mathf.Clamp(innerRadius, 0f, radius);
        angle = Mathf.Clamp(angle, 1f, 359f);
        arcSegments = Mathf.Clamp(arcSegments, 3, 64);

        if (gameObject.scene.IsValid() &&
            TryGetComponent(out PolygonCollider2D existingCollider))
        {
            sensorCollider = existingCollider;
            RebuildFanCollider();
        }
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
