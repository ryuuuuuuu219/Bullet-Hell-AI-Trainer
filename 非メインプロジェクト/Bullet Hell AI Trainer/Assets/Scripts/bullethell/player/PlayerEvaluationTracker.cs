using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerEvaluationTracker : MonoBehaviour
{
    private const float ViewportEdgeTolerance = 0.001f;
    private const float ViewportCornerDistance = 0.70710678f;

    private PlayerAgent playerAgent;
    private Camera evaluationCamera;
    private float sampleIntervalSeconds = 0.5f;
    private float sampleTimer;

    public float SurvivalTime { get; private set; }
    public float EdgeCollisionCumulativeTime { get; private set; }
    public float CenterDistanceSampledSum { get; private set; }

    private void Awake()
    {
        playerAgent = GetComponent<PlayerAgent>();
    }

    public void Initialize(float centerDistanceSampleIntervalSeconds)
    {
        sampleIntervalSeconds = Mathf.Max(
            0.01f,
            centerDistanceSampleIntervalSeconds);
        sampleTimer = sampleIntervalSeconds;
        SurvivalTime = 0f;
        EdgeCollisionCumulativeTime = 0f;
        CenterDistanceSampledSum = 0f;
    }

    private void Update()
    {
        if (playerAgent != null && playerAgent.IsHit)
        {
            return;
        }

        SurvivalTime += Time.deltaTime;

        if (!TryGetViewportPosition(out Vector3 viewportPosition))
        {
            return;
        }

        if (viewportPosition.x <= ViewportEdgeTolerance ||
            viewportPosition.x >= 1f - ViewportEdgeTolerance ||
            viewportPosition.y <= ViewportEdgeTolerance ||
            viewportPosition.y >= 1f - ViewportEdgeTolerance)
        {
            EdgeCollisionCumulativeTime += Time.deltaTime;
        }

        sampleTimer -= Time.deltaTime;
        while (sampleTimer <= 0f)
        {
            Vector2 fromCenter = new Vector2(
                viewportPosition.x - 0.5f,
                viewportPosition.y - 0.5f);
            CenterDistanceSampledSum += Mathf.Clamp01(
                fromCenter.magnitude / ViewportCornerDistance);
            sampleTimer += sampleIntervalSeconds;
        }
    }

    private bool TryGetViewportPosition(out Vector3 viewportPosition)
    {
        if (evaluationCamera == null)
        {
            evaluationCamera = Camera.main;
        }

        if (evaluationCamera == null)
        {
            viewportPosition = default;
            return false;
        }

        viewportPosition = evaluationCamera.WorldToViewportPoint(
            new Vector3(transform.position.x, transform.position.y, 0f));
        return viewportPosition.z > 0f;
    }
}
