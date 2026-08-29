using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Aidata))]
public sealed class PlayerMovementController : MonoBehaviour
{
    private static readonly Plane MovementPlane =
        new Plane(Vector3.forward, Vector3.zero);

    [SerializeField, Min(0f)] private float moveSpeed = 300f;

    private Aidata aiData;
    private PlayerAgent playerAgent;
    private Rigidbody2D body;
    private Camera movementCamera;

    private void Awake()
    {
        aiData = GetComponent<Aidata>();
        playerAgent = GetComponent<PlayerAgent>();
        body = GetComponent<Rigidbody2D>();

        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void FixedUpdate()
    {
        Vector2 currentPosition = ClampToCameraView(body.position);
        if (body.position != currentPosition)
        {
            body.position = currentPosition;
        }

        if (playerAgent != null && playerAgent.IsHit)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 movementOutput = Vector2.ClampMagnitude(aiData.output(), 1f);
        Vector2 desiredVelocity = movementOutput * moveSpeed;
        Vector2 nextPosition = currentPosition +
            desiredVelocity * Time.fixedDeltaTime;
        Vector2 clampedNextPosition = ClampToCameraView(nextPosition);
        body.linearVelocity = (clampedNextPosition - currentPosition) /
            Time.fixedDeltaTime;
    }

    private Vector2 ClampToCameraView(Vector2 worldPosition)
    {
        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        if (movementCamera == null)
        {
            return worldPosition;
        }

        Vector3 viewportPosition = movementCamera.WorldToViewportPoint(
            new Vector3(worldPosition.x, worldPosition.y, 0f));
        if (viewportPosition.z <= 0f)
        {
            return worldPosition;
        }

        float clampedX = Mathf.Clamp01(viewportPosition.x);
        float clampedY = Mathf.Clamp01(viewportPosition.y);
        if (Mathf.Approximately(viewportPosition.x, clampedX) &&
            Mathf.Approximately(viewportPosition.y, clampedY))
        {
            return worldPosition;
        }

        Ray viewportRay = movementCamera.ViewportPointToRay(
            new Vector3(clampedX, clampedY, 0f));
        if (!MovementPlane.Raycast(viewportRay, out float distance))
        {
            return worldPosition;
        }

        Vector3 intersection = viewportRay.GetPoint(distance);
        return new Vector2(intersection.x, intersection.y);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }
#endif
}
