using UnityEngine;

// Temporary visible aim target; no collision or damage behavior.
public class target : MonoBehaviour
{
    private Material markerMaterial;
    private void Awake()
    {
        LineRenderer marker = gameObject.AddComponent<LineRenderer>();
        marker.useWorldSpace = false;
        marker.loop = true;
        marker.positionCount = 4;
        marker.SetPositions(new[] { new Vector3(0, 15, 0), new Vector3(15, 0, 0),
            new Vector3(0, -15, 0), new Vector3(-15, 0, 0) });
        marker.startWidth = marker.endWidth = 3f;
        marker.startColor = marker.endColor = Color.yellow;
        markerMaterial = new Material(Shader.Find("Sprites/Default"));
        marker.sharedMaterial = markerMaterial;
    }
    private void OnDestroy()
    {
        if (markerMaterial != null) Destroy(markerMaterial);
    }
}
