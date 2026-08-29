using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class LaserAttack : MonoBehaviour
{
    private static readonly Color WarningColor = Color.gray;
    private static readonly Color ActiveColor = Color.red;
    private static int nextThreatId;

    private LineRenderer lineRenderer;
    private LaserThreatData threatData;
    private Vector2 origin;
    private Vector2 direction;
    private float length;
    private float warningWidth;
    private float laserWidth;
    private int logicalLayer;
    private PlayerAgent targetPlayer;
    private bool isConfigured;

    public int LogicalLayer => logicalLayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetIds()
    {
        nextThreatId = 0;
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = 2;
        lineRenderer.alignment = LineAlignment.TransformZ;
    }

    public void Configure(
        Vector2 start,
        Vector2 aimDirection,
        PlayerAgent target,
        int layer,
        float warningDuration,
        float activeDuration,
        float beamLength,
        float warningLineWidth,
        float activeLaserWidth,
        int threatLevel)
    {
        origin = start;
        direction = aimDirection.sqrMagnitude > Mathf.Epsilon
            ? aimDirection.normalized
            : Vector2.down;
        logicalLayer = Mathf.Max(0, layer);
        targetPlayer = target;
        length = Mathf.Max(0.01f, beamLength);
        warningWidth = Mathf.Max(0.01f, warningLineWidth);
        laserWidth = Mathf.Max(0.01f, activeLaserWidth);

        threatData = new LaserThreatData
        {
            id = ++nextThreatId,
            logicalLayer = logicalLayer,
            phase = LaserThreatPhase.Warning,
            threatLevel = Mathf.Max(0, threatLevel),
            origin = origin,
            direction = direction,
            radius = laserWidth * 0.5f,
            remainingWarningTime = Mathf.Max(0f, warningDuration),
        };

        ApplyLine(WarningColor, warningWidth);
        LogicalLayerVisibility.Apply(gameObject, logicalLayer);
        WarningLineSensor.Register(threatData);
        isConfigured = true;
        StartCoroutine(RunAttack(
            Mathf.Max(0f, warningDuration),
            Mathf.Max(0f, activeDuration)));
    }

    private IEnumerator RunAttack(float warningDuration, float activeDuration)
    {
        float warningEndTime = Time.time + warningDuration;
        while (Time.time < warningEndTime)
        {
            threatData.remainingWarningTime = Mathf.Max(
                0f,
                warningEndTime - Time.time);
            yield return null;
        }

        threatData.phase = LaserThreatPhase.Active;
        threatData.remainingWarningTime = 0f;
        ApplyLine(ActiveColor, laserWidth);

        float activeEndTime = Time.time + activeDuration;
        while (Time.time < activeEndTime)
        {
            RegisterLaserHits();
            yield return null;
        }

        Destroy(gameObject);
    }

    private void ApplyLine(Color color, float width)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, origin + direction * length);
    }

    private void RegisterLaserHits()
    {
        if (targetPlayer == null ||
            targetPlayer.LogicalLayer != logicalLayer)
        {
            return;
        }

        Collider2D hitbox = targetPlayer.GetComponent<Collider2D>();
        Vector2 playerPosition = targetPlayer.transform.position;
        float distanceAlongLaser = Mathf.Clamp(
            Vector2.Dot(playerPosition - origin, direction),
            0f,
            length);
        Vector2 closestPointOnLaser = origin + direction * distanceAlongLaser;
        Vector2 closestPointOnPlayer = hitbox != null
            ? hitbox.ClosestPoint(closestPointOnLaser)
            : playerPosition;

        if ((closestPointOnPlayer - closestPointOnLaser).sqrMagnitude <=
            laserWidth * laserWidth * 0.25f)
        {
            targetPlayer.RegisterHit(logicalLayer);
        }
    }

    private void OnDestroy()
    {
        if (isConfigured)
        {
            WarningLineSensor.Unregister(threatData);
        }
    }
}
