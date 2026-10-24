using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class CameraDragPan : MonoBehaviour
{
    private Camera targetCamera;
    private readonly Plane fieldPlane = new Plane(Vector3.forward, Vector3.zero);
    private Vector3 dragAnchor;
    private bool dragging;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.isPressed)
        {
            dragging = false;
            return;
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
        {
            dragging = targetCamera.pixelRect.Contains(screenPosition)
                && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                && TryGetFieldPosition(screenPosition, out dragAnchor);
        }

        if (!dragging || !TryGetFieldPosition(screenPosition, out Vector3 currentPosition)) return;

        // Keep the grabbed world point under the pointer, preserving camera Z.
        Vector3 offset = dragAnchor - currentPosition;
        offset.z = 0f;
        transform.position += offset;
    }

    private bool TryGetFieldPosition(Vector2 screenPosition, out Vector3 position)
    {
        Ray ray = targetCamera.ScreenPointToRay(screenPosition);
        if (fieldPlane.Raycast(ray, out float distance))
        {
            position = ray.GetPoint(distance);
            return true;
        }
        position = default;
        return false;
    }

    private void OnDisable() => dragging = false;

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) dragging = false;
    }
}
