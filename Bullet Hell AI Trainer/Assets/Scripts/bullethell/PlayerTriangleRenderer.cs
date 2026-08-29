using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class PlayerTriangleRenderer : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float width = 0.8f;
    [SerializeField, Min(0.01f)] private float sideLength = 20f;
    [SerializeField] private Color color = new Color(0.2f, 0.9f, 1f, 1f);

    private LineRenderer lineRenderer;

    private void Awake()
    {
        ApplyShape();
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
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.numCapVertices = 2;
        lineRenderer.alignment = LineAlignment.TransformZ;
    }
}
