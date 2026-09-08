using UnityEngine;
using UnityEngine.InputSystem;

public interface ITeacherTargetProvider
{
    bool TryGetTeacherTarget(out Vector2 targetMovement);
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Aidata))]
public sealed class PlayerMovementController : MonoBehaviour
{
    private static readonly Plane MovementPlane =
        new Plane(Vector3.forward, Vector3.zero);

    [SerializeField, Min(0f)] private float moveSpeed = 300f;
    [SerializeField] private bool teacherModeEnabled;
    [SerializeField] private bool teacherTrainingEnabled = true;
    private MonoBehaviour scriptedTeacherTargetProvider;

    private Aidata aiData;
    private PlayerAgent playerAgent;
    private Rigidbody2D body;
    private Camera movementCamera;

    public bool IsManualControl { get; private set; }
    public bool IsManualTeacherControlled =>
        teacherModeEnabled && playerAgent != null && playerAgent.LogicalLayer == 0;
    public bool HasScriptedTeacher =>
        scriptedTeacherTargetProvider is ITeacherTargetProvider;
    public bool IsTeacherControlled =>
        IsManualTeacherControlled;
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

    public void SetTeacherTrainingEnabled(bool enabled)
    {
        teacherTrainingEnabled = enabled;
    }

    public void SetScriptedTeacherTargetProvider(MonoBehaviour provider)
    {
        if (provider != null && !(provider is ITeacherTargetProvider))
        {
            Debug.LogError(
                $"{provider.GetType().Name} must implement " +
                $"{nameof(ITeacherTargetProvider)}.",
                provider);
            return;
        }

        scriptedTeacherTargetProvider = provider;
    }

    private void Awake()
    {
        aiData = GetComponent<Aidata>();
        playerAgent = GetComponent<PlayerAgent>();
        body = GetComponent<Rigidbody2D>();
        ResolveScriptedTeacherTargetProvider();

        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void ResolveScriptedTeacherTargetProvider()
    {
        if (scriptedTeacherTargetProvider is ITeacherTargetProvider)
        {
            return;
        }

        MonoBehaviour[] components = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            if (component is ITeacherTargetProvider)
            {
                scriptedTeacherTargetProvider = component;
                return;
            }
        }
    }

    private void Start()
    {
        if (IsManualControl || IsManualTeacherControlled)
        {
            WarpCursorToPlayerPosition();
        }
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
        bool useManualInput = IsManualControl || IsManualTeacherControlled;
        Vector2 manualMovement = useManualInput
            ? ReadManualMovement()
            : Vector2.zero;
        Vector2 scriptedTeacherTarget = Vector2.zero;
        bool hasScriptedTeacherTarget =
            teacherTrainingEnabled &&
            ScriptedTeacherSettings.IsEnabled &&
            !IsManualTeacherControlled &&
            TryGetScriptedTeacherTarget(out scriptedTeacherTarget);

        Vector2 movementOutput = useManualInput
            ? manualMovement
            : prediction;
        movementOutput = Vector2.ClampMagnitude(movementOutput, 1f);
        if (teacherTrainingEnabled)
        {
            if (IsManualTeacherControlled)
            {
                aiData.RecordTeacherSample(
                    manualMovement,
                    TeacherTargetSource.ManualPointer);
            }
            else if (hasScriptedTeacherTarget)
            {
                aiData.RecordTeacherSample(
                    scriptedTeacherTarget,
                    TeacherTargetSource.ScriptedProvider);
            }
        }
        Vector2 desiredVelocity = movementOutput * moveSpeed;
        Vector2 nextPosition = currentPosition +
            desiredVelocity * Time.fixedDeltaTime;
        Vector2 clampedNextPosition = ClampToCameraView(nextPosition);
        body.linearVelocity = (clampedNextPosition - currentPosition) /
            Time.fixedDeltaTime;
    }

    private bool TryGetScriptedTeacherTarget(out Vector2 targetMovement)
    {
        targetMovement = Vector2.zero;
        if (!(scriptedTeacherTargetProvider is ITeacherTargetProvider provider))
        {
            return false;
        }

        if (!provider.TryGetTeacherTarget(out targetMovement))
        {
            targetMovement = Vector2.zero;
            return false;
        }

        targetMovement = Vector2.ClampMagnitude(targetMovement, 1f);
        return true;
    }

    private Vector2 ReadManualMovement()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !TryGetMovementCamera(out Camera camera))
        {
            return Vector2.zero;
        }

        Ray pointerRay = camera.ScreenPointToRay(mouse.position.ReadValue());
        if (!MovementPlane.Raycast(pointerRay, out float distance))
        {
            return Vector2.zero;
        }

        Vector3 pointerWorldPosition = pointerRay.GetPoint(distance);
        Vector2 movementDelta = (Vector2)pointerWorldPosition - body.position;
        float maximumStep = moveSpeed * Time.fixedDeltaTime;
        if (maximumStep <= Mathf.Epsilon)
        {
            return Vector2.zero;
        }

        return Vector2.ClampMagnitude(movementDelta / maximumStep, 1f);
    }

    private void WarpCursorToPlayerPosition()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !TryGetMovementCamera(out Camera camera))
        {
            return;
        }

        Vector3 screenPosition = camera.WorldToScreenPoint(
            new Vector3(body.position.x, body.position.y, 0f));
        if (screenPosition.z <= 0f)
        {
            return;
        }

        mouse.WarpCursorPosition(new Vector2(screenPosition.x, screenPosition.y));
    }

    private Vector2 ClampToCameraView(Vector2 worldPosition)
    {
        if (!TryGetMovementCamera(out Camera camera))
        {
            return worldPosition;
        }

        Vector3 viewportPosition = camera.WorldToViewportPoint(
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

        Ray viewportRay = camera.ViewportPointToRay(
            new Vector3(clampedX, clampedY, 0f));
        if (!MovementPlane.Raycast(viewportRay, out float distance))
        {
            return worldPosition;
        }

        Vector3 intersection = viewportRay.GetPoint(distance);
        return new Vector2(intersection.x, intersection.y);
    }

    private bool TryGetMovementCamera(out Camera camera)
    {
        if (movementCamera == null)
        {
            movementCamera = Camera.main;
        }

        camera = movementCamera;
        return camera != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }
#endif
}
