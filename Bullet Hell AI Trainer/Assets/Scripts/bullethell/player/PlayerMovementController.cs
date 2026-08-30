using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Aidata))]
public sealed class PlayerMovementController : MonoBehaviour
{
    private static readonly Plane MovementPlane =
        new Plane(Vector3.forward, Vector3.zero);

    [SerializeField, Min(0f)] private float moveSpeed = 300f;
    [SerializeField] private bool teacherModeEnabled;

    private Aidata aiData;
    private PlayerAgent playerAgent;
    private Rigidbody2D body;
    private Camera movementCamera;

    public bool IsManualControl { get; private set; }
    public bool IsTeacherControlled =>
        teacherModeEnabled && playerAgent != null && playerAgent.LogicalLayer == 0;
    public bool IsExcludedFromGeneticAlgorithm =>
        IsManualControl || IsTeacherControlled;
    public float MoveSpeed => moveSpeed;

    public void SetManualControl(bool enabled)
    {
        IsManualControl = enabled;
    }

    public void SetTeacherMode(bool enabled)
    {
        teacherModeEnabled = enabled;
    }

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

        Vector2 prediction = aiData.output();
        bool useManualInput = IsManualControl || IsTeacherControlled;
        Vector2 movementOutput = useManualInput
            ? ReadManualMovement()
            : prediction;
        movementOutput = Vector2.ClampMagnitude(movementOutput, 1f);
        if (IsTeacherControlled)
        {
            aiData.RecordTeacherSample(movementOutput);
        }
        Vector2 desiredVelocity = movementOutput * moveSpeed;
        Vector2 nextPosition = currentPosition +
            desiredVelocity * Time.fixedDeltaTime;
        Vector2 clampedNextPosition = ClampToCameraView(nextPosition);
        body.linearVelocity = (clampedNextPosition - currentPosition) /
            Time.fixedDeltaTime;
    }

    private static Vector2 ReadManualMovement()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed)
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
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
