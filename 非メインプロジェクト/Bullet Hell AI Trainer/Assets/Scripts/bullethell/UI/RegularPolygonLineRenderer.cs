using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class RegularPolygonLineRenderer : MonoBehaviour
{
    [SerializeField, Min(3)] private int sides = 6;
    [SerializeField, Min(0.01f)] private float radius = 0.5f;
    [SerializeField, Min(0.01f)] private float lineWidth = 1f;
    [SerializeField] private Color color = Color.white;

    private LineRenderer lineRenderer;

    private void Awake()
    {
        ApplyShape();
    }

    private void Reset()
    {
        ApplyShape();
    }

    public void SetStyle(int sideCount, float shapeRadius, Color shapeColor)
    {
        sides = Mathf.Max(3, sideCount);
        radius = Mathf.Max(0.01f, shapeRadius);
        color = shapeColor;
        ApplyShape();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        sides = Mathf.Max(3, sides);
        radius = Mathf.Max(0.01f, radius);
        lineWidth = Mathf.Max(0.01f, lineWidth);
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

        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = sides;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.alignment = LineAlignment.TransformZ;

        float angleStep = Mathf.PI * 2f / sides;
        for (int index = 0; index < sides; index++)
        {
            float angle = Mathf.PI * 0.5f + angleStep * index;
            lineRenderer.SetPosition(index, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f));
        }
    }
}
