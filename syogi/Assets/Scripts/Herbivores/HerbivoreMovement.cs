using UnityEngine;

[DefaultExecutionOrder(20)]
[RequireComponent(typeof(HerbivoreFeeding))]
public sealed class HerbivoreMovement : MonoBehaviour
{
    public const float Speed = 100f;
    public const float MaxAngularSpeed = 90f;
    [SerializeField] private Vector2 movementDirection = Vector2.right;
    public Vector2 MovementDirection => movementDirection;
    public bool IsStopped { get; private set; }
    private HerbivoreFeeding feeding;
    private GrassField field;
    private GrassTargeting.Candidate target;
    private bool hasTarget;
    private float decisionElapsed;

    // Called immediately after spawning, before Start chooses the first target.
    public void SetInitialDirection(Vector2 direction)
    {
        movementDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
    }

    private void Awake()
    {
        feeding = GetComponent<HerbivoreFeeding>();
        movementDirection = movementDirection.sqrMagnitude > 0 ? movementDirection.normalized : Vector2.right;
    }

    private void OnEnable()
    {
        decisionElapsed = 0f;
        feeding.AfterFeeding += ChooseTarget;
    }
    private void OnDisable() => feeding.AfterFeeding -= ChooseTarget;
    private void Start() => ChooseTarget(feeding.Field);

    private void ChooseTarget(GrassField grassField)
    {
        decisionElapsed = 0f;
        field = grassField;
        hasTarget = false;
        IsStopped = field == null || field.Grid == null || HasGrassHere();
        if (IsStopped) return;
        Vector2 position = transform.position;
        hasTarget = GrassTargeting.TrySelect(field.Grid, position.x, position.y,
            movementDirection.x, movementDirection.y, GrassField.CellSize, out target);
    }

    private bool HasGrassHere()
    {
        Vector3 position = transform.position;
        return field.Grid.TryWorldToCell(position.x, position.y, GrassField.CellSize, out int x, out int y)
            && field.Grid[x, y] > 0;
    }

    private void Update()
    {
        if (field == null || field.Grid == null) return;
        // Target decisions remain once per second independently of feeding.
        // A feeding event also triggers an immediate decision after consumption.
        decisionElapsed += Time.deltaTime;
        if (decisionElapsed >= 1f)
        {
            float remainder = decisionElapsed % 1f;
            ChooseTarget(field);
            decisionElapsed = remainder;
        }
        if (HasGrassHere()) IsStopped = true;
        if (IsStopped) return;
        // Small steps prevent skipping a grass cell during a long frame.
        float remaining = Time.deltaTime;
        while (remaining > 0f)
        {
            float dt = Mathf.Min(remaining, 0.02f);
            remaining -= dt;
            MoveStep(dt);
            if (HasGrassHere())
            {
                IsStopped = true;
                // Check the newly entered cell before the next frame.
                feeding.TryFeed();
                break;
            }
        }
    }

    private void MoveStep(float dt)
    {
        Vector2 position = transform.position;
        Vector2 desired = movementDirection;
        if (hasTarget)
        {
            if (field.Grid.TryWorldToCell(position.x, position.y, GrassField.CellSize, out int x, out int y)
                && x == target.CellX && y == target.CellY) hasTarget = false;
            else desired = (new Vector2((float)target.X, (float)target.Y) - position).normalized;
        }

        // Start turning before reaching an edge: 2 * turning radius plus body margin.
        float half = GrassField.FieldSize * 0.5f;
        float margin = 2f * Speed / (MaxAngularSpeed * Mathf.Deg2Rad) + 6f;
        Vector2 inward = Vector2.zero;
        if (position.x < -half + margin) inward.x = 1;
        else if (position.x > half - margin) inward.x = -1;
        if (position.y < -half + margin) inward.y = 1;
        else if (position.y > half - margin) inward.y = -1;
        if (inward != Vector2.zero) desired = inward.normalized;

        float turn = Mathf.Clamp(Vector2.SignedAngle(movementDirection, desired), -MaxAngularSpeed * dt, MaxAngularSpeed * dt);
        float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) + turn * Mathf.Deg2Rad;
        movementDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 next = position + movementDirection * (Speed * dt);
        // Safety for an animal spawned directly at an edge facing outwards.
        next.x = Mathf.Clamp(next.x, -half, half - 0.001f);
        next.y = Mathf.Clamp(next.y, -half, half - 0.001f);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }
}
