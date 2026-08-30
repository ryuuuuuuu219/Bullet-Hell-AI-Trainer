using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(PlayerAgent))]
public sealed class PlayerTriangleRenderer : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float width = 0.8f;
    [SerializeField, Min(0.01f)] private float sideLength = 20f;
    [SerializeField] private Color color = Color.blue;
    [SerializeField] private Color hitColor = new Color(0.2f, 0.9f, 1f, 1f);

    private LineRenderer lineRenderer;
    private PlayerAgent playerAgent;

    private void Awake()
    {
        playerAgent = GetComponent<PlayerAgent>();
        ApplyShape();
    }

    private void OnEnable()
    {
        if (playerAgent == null)
        {
            playerAgent = GetComponent<PlayerAgent>();
        }

        if (playerAgent != null)
        {
            playerAgent.Hit += HandleHit;
            ApplyColor(playerAgent.IsHit ? hitColor : color);
        }
    }

    private void OnDisable()
    {
        if (playerAgent != null)
        {
            playerAgent.Hit -= HandleHit;
        }
    }

    private void Reset()
    {
        ApplyShape();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyShape();
    }
#endif

    private void ApplyShape()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            return;
        }

        float circumradius = sideLength / Mathf.Sqrt(3f);
        float bottomY = -circumradius * 0.5f;
        float halfSideLength = sideLength * 0.5f;

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, new Vector3(0f, circumradius, 0f));
        lineRenderer.SetPosition(1, new Vector3(-halfSideLength, bottomY, 0f));
        lineRenderer.SetPosition(2, new Vector3(halfSideLength, bottomY, 0f));
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        ApplyColor(color);
        lineRenderer.numCornerVertices = 2;
        lineRenderer.numCapVertices = 2;
        lineRenderer.alignment = LineAlignment.TransformZ;
    }

    private void HandleHit(bullet source)
    {
        ApplyColor(hitColor);
    }

    private void ApplyColor(Color targetColor)
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.startColor = targetColor;
        lineRenderer.endColor = targetColor;
    }
}
