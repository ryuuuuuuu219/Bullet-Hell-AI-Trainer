using UnityEngine;

[DefaultExecutionOrder(20)]
[RequireComponent(typeof(HerbivoreFeeding))]
public sealed class HerbivoreMovement : MonoBehaviour
{
    public const float Speed = 80f;
    public const float MaxAngularSpeed = 90f;
    [SerializeField] private Vector2 movementDirection = Vector2.right;
    public Vector2 MovementDirection => movementDirection;
    public bool IsStopped { get; private set; }
    private HerbivoreFeeding feeding;
    private GrassField field;
    private GrassTargeting.Candidate target;
    private bool hasTarget;
    private float decisionElapsed;
    private AnimalBody body;
    private bool wasRunning;
    private bool wasSleeping;
    private readonly EvasionClock evasion = new EvasionClock();
    private System.Random evasionRandom;
    private float CurrentSpeed => Mathf.Max(0, body.IsRunning ? body.Traits.runningSpeed : body.Traits.walkingSpeed);

    // Called immediately after spawning, before Start chooses the first target.
    public void SetInitialDirection(Vector2 direction)
    {
        movementDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
    }

    private void Awake()
    {
        feeding = GetComponent<HerbivoreFeeding>();
        body = GetComponent<AnimalBody>();
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
        IsStopped = body.IsSleeping || field == null || field.Grid == null || (!body.IsRunning && HasGrassHere());
        if (body.IsRunning) return;
        if (IsStopped) return;
        Vector2 position = transform.position;
        hasTarget = GrassTargeting.TrySelect(field.Grid, position.x, position.y,
            movementDirection.x, movementDirection.y, GrassField.CellSize, out target, body.SearchDistance);
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
        if (!body.IsAlive || body.IsSleeping) { wasSleeping = true; IsStopped = true; return; }
        if (wasSleeping || wasRunning != body.IsRunning) ChooseTarget(field);
        wasSleeping = false;
        wasRunning = body.IsRunning;
        // Target decisions remain once per second independently of feeding.
        // A feeding event also triggers an immediate decision after consumption.
        decisionElapsed += Time.deltaTime;
        if (decisionElapsed >= 1f)
        {
            float remainder = decisionElapsed % 1f;
            ChooseTarget(field);
            decisionElapsed = remainder;
        }
        if (body.IsRunning) IsStopped = false;
        else if (HasGrassHere()) IsStopped = true;
        if (IsStopped) return;
        // Small steps prevent skipping a grass cell during a long frame.
        float remaining = Time.deltaTime;
        while (remaining > 0f)
        {
            float dt = Mathf.Min(remaining, 0.02f);
            remaining -= dt;
            MoveStep(dt);
            if (!body.IsAlive) break;
            if (!body.IsRunning && HasGrassHere())
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
        if (CurrentSpeed > 0)
            body.Resources.SpendStamina(System.Math.Max(0, body.Traits.movementStaminaPerSecond) * dt);
        if (body.Resources.IsDead) { body.Die(); return; }
        if (evasionRandom == null)
        {
            var manager = GetComponentInParent<HerbivoreManager>();
            evasionRandom = new System.Random(unchecked((manager == null ? 1 : manager.ActualSeed) * 397 ^ body.SeparationId));
        }
        int evaded = evasion.Step(dt, body.IsRunning, evasionRandom.NextDouble);
        for (int i = 0; i < evaded % 4; i++)
            movementDirection = new Vector2(-movementDirection.y, movementDirection.x);
        Vector2 position = transform.position;
        Vector2 desired = movementDirection;
        if (body.IsRunning) desired = body.EscapeDirection;
        else if (hasTarget)
        {
            if (field.Grid.TryWorldToCell(position.x, position.y, GrassField.CellSize, out int x, out int y)
                && x == target.CellX && y == target.CellY) hasTarget = false;
            else desired = (new Vector2((float)target.X, (float)target.Y) - position).normalized;
        }

        // Start turning before reaching an edge: 2 * turning radius plus body margin.
        float half = GrassField.FieldSize * 0.5f;
        float margin = 2f * CurrentSpeed / (MaxAngularSpeed * Mathf.Deg2Rad) + body.Radius;
        Vector2 inward = Vector2.zero;
        if (position.x < -half + margin) inward.x = 1;
        else if (position.x > half - margin) inward.x = -1;
        if (position.y < -half + margin) inward.y = 1;
        else if (position.y > half - margin) inward.y = -1;
        if (inward != Vector2.zero) desired = inward.normalized;

        float turn = Mathf.Clamp(Vector2.SignedAngle(movementDirection, desired), -MaxAngularSpeed * dt, MaxAngularSpeed * dt);
        if (evaded > 0) turn = 0; // Instant +90 degrees around Z; resume steering on the next step.
        float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) + turn * Mathf.Deg2Rad;
        movementDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 next = position + movementDirection * (CurrentSpeed * dt);
        // Safety for an animal spawned directly at an edge facing outwards.
        next.x = Mathf.Clamp(next.x, -half, half - 0.001f);
        next.y = Mathf.Clamp(next.y, -half, half - 0.001f);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }
}
