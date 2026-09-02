using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LaserPool
{
    private static readonly Stack<LaserAttack> Pool =
        new Stack<LaserAttack>();

    private static Transform poolRoot;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Pool.Clear();
        poolRoot = null;
    }

    public static void Prewarm(int initialCount)
    {
        while (Pool.Count < Mathf.Max(0, initialCount))
        {
            LaserAttack instance = CreateInstance();
            instance.IsStored = true;
            instance.transform.SetParent(GetPoolRoot(), false);
            Pool.Push(instance);
        }
    }

    public static LaserAttack Acquire()
    {
        LaserAttack instance = null;
        while (Pool.Count > 0 && instance == null)
        {
            instance = Pool.Pop();
        }

        if (instance == null)
        {
            instance = CreateInstance();
        }

        instance.IsStored = false;
        instance.transform.SetParent(null, false);
        instance.gameObject.SetActive(true);
        return instance;
    }

    public static void Release(LaserAttack instance)
    {
        if (instance == null || instance.IsStored)
        {
            return;
        }

        instance.IsStored = true;
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);
        Pool.Push(instance);
    }

    private static LaserAttack CreateInstance()
    {
        GameObject laserObject = new GameObject(
            "Pooled Laser",
            typeof(LineRenderer),
            typeof(LaserAttack));
        laserObject.SetActive(false);
        return laserObject.GetComponent<LaserAttack>();
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null)
        {
            return poolRoot;
        }

        GameObject rootObject = new GameObject("Laser Pool");
        rootObject.SetActive(false);
        Object.DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;
        return poolRoot;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class LaserAttack : MonoBehaviour
{
    private const float InfiniteRangeVisualLength = 1000f;
    private static readonly Color WarningColor = Color.gray;
    private static readonly Color ActiveColor = Color.red;
    private static Material lineMaterial;
    private static int nextThreatId;

    private LineRenderer lineRenderer;
    private LaserThreatData threatData;
    private Vector2 origin;
    private Vector2 direction;
    private float length;
    private bool hasInfiniteRange;
    private float warningWidth;
    private float laserWidth;
    private int logicalLayer;
    private PlayerAgent targetPlayer;
    private bool isConfigured;

    internal bool IsStored { get; set; }

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
        lineRenderer.sharedMaterial = GetLineMaterial();
    }

    internal static Material GetLineMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("UI/Default");
        if (shader == null)
        {
            Debug.LogError("No compatible shader was found for laser warning lines.");
            return null;
        }

        lineMaterial = new Material(shader)
        {
            name = "Generated Laser Line Material",
            hideFlags = HideFlags.HideAndDontSave,
        };
        return lineMaterial;
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
        StopAllCoroutines();
        UnregisterThreat();
        origin = start;
        direction = aimDirection.sqrMagnitude > Mathf.Epsilon
            ? aimDirection.normalized
            : Vector2.down;
        logicalLayer = Mathf.Max(0, layer);
        targetPlayer = target;
        hasInfiniteRange = float.IsPositiveInfinity(beamLength);
        length = hasInfiniteRange
            ? float.PositiveInfinity
            : Mathf.Max(0.01f, beamLength);
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

        LaserPool.Release(this);
    }

    private void ApplyLine(Color color, float width)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.SetPosition(0, origin);
        float visualLength = hasInfiniteRange
            ? InfiniteRangeVisualLength
            : length;
        lineRenderer.SetPosition(1, origin + direction * visualLength);
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
        float distanceAlongLaser = Mathf.Max(
            0f,
            Vector2.Dot(playerPosition - origin, direction));
        if (!hasInfiniteRange)
        {
            distanceAlongLaser = Mathf.Min(distanceAlongLaser, length);
        }
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

    private void OnDisable()
    {
        StopAllCoroutines();
        UnregisterThreat();
    }

    private void OnDestroy()
    {
        UnregisterThreat();
    }

    private void UnregisterThreat()
    {
        if (isConfigured)
        {
            WarningLineSensor.Unregister(threatData);
            isConfigured = false;
        }

        targetPlayer = null;
    }
}
