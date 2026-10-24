using UnityEngine;

[DefaultExecutionOrder(30)]
[RequireComponent(typeof(AnimalBody), typeof(LineRenderer))]
public sealed class CarnivoreAnimal : MonoBehaviour
{
    public const float Speed = 120f;
    public const float AngularSpeed = 180f;
    public const float SearchRadius = 1000f;
    public const float Range = 200f;
    public const double InitialHealth = 50;
    [SerializeField, Min(1)] private int foodLayer = 1;
    [SerializeField] private Vector2 movementDirection = Vector2.right;
    public Vector2 MovementDirection => movementDirection;
    public AnimalResources Resources { get; private set; }
    public AnimalBody Body { get; private set; }
    public bool IsStopped { get; private set; }
    private CarnivoreManager manager;
    private AnimalBody target;
    private bool hadTarget;
    private float decisionElapsed;
    private float shotElapsed;
    private LineRenderer outline;

    private void Awake()
    {
        Resources = new AnimalResources(InitialHealth);
        Body = GetComponent<AnimalBody>();
        Body.Initialize(Resources, false);
        manager = GetComponentInParent<CarnivoreManager>();
        outline = GetComponent<LineRenderer>();
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.startColor = outline.endColor = Color.gray;
        outline.positionCount = Mathf.Max(1, foodLayer) + 2;
        SetInitialDirection(movementDirection);
    }

    public void SetInitialDirection(Vector2 direction)
    {
        movementDirection = direction.sqrMagnitude > 0 ? direction.normalized : Vector2.right;
        DrawOutline();
    }

    private void Start() => ChooseTarget();

    private void ChooseTarget()
    {
        if (target != null && target.IsAlive) return;
        target = null;
        hadTarget = false;
        float nearest = SearchRadius * SearchRadius;
        foreach (AnimalBody candidate in AnimalBody.Active)
        {
            if (!candidate.IsAlive || !candidate.IsHerbivore) continue;
            float distance = ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude;
            if (distance > nearest) continue;
            nearest = distance;
            target = candidate;
        }
        hadTarget = target != null;
    }

    private void Update()
    {
        float remaining = Time.deltaTime;
        if (remaining <= 0) return;
        Resources.Maintain(remaining);
        if (Resources.IsDead) { Body.Die(); return; }
        while (remaining > 0)
        {
            float dt = Mathf.Min(remaining, 0.02f);
            remaining -= dt;
            decisionElapsed += dt;
            if (decisionElapsed >= 1f)
            {
                decisionElapsed %= 1f;
                ChooseTarget();
            }
            MoveAndShoot(dt);
        }
        DrawOutline();
    }

    private void MoveAndShoot(float dt)
    {
        Vector2 position = transform.position;
        float half = GrassField.FieldSize * 0.5f;
        float margin = 2f * Speed / (AngularSpeed * Mathf.Deg2Rad) + AnimalBody.Radius;
        bool boundary = Mathf.Abs(position.x) >= half - margin || Mathf.Abs(position.y) >= half - margin;
        bool aliveTarget = target != null && target.IsAlive;
        Vector2 desired = movementDirection;
        IsStopped = false;
        if (boundary || (hadTarget && !aliveTarget)) desired = -position;
        else if (aliveTarget)
        {
            desired = (Vector2)target.transform.position - position;
            IsStopped = desired.sqrMagnitude <= Range * Range;
        }
        if (desired.sqrMagnitude > 0)
        {
            float turn = Mathf.Clamp(Vector2.SignedAngle(movementDirection, desired), -AngularSpeed * dt, AngularSpeed * dt);
            float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) + turn * Mathf.Deg2Rad;
            movementDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        if (!IsStopped)
        {
            position += movementDirection * (Speed * dt);
            position.x = Mathf.Clamp(position.x, -half, half - 0.001f);
            position.y = Mathf.Clamp(position.y, -half, half - 0.001f);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
        shotElapsed = Mathf.Min(1f, shotElapsed + dt);
        // No angle gate or lead aiming: fire along the current heading.
        if (IsStopped && shotElapsed >= 1f)
        {
            shotElapsed = 0f;
            CarnivoreProjectile.Fire(this, movementDirection, outline.sharedMaterial);
        }
    }

    private void DrawOutline()
    {
        if (outline == null) return;
        float heading = Mathf.Atan2(movementDirection.y, movementDirection.x);
        for (int i = 0; i < outline.positionCount; i++)
        {
            float angle = heading + i * Mathf.PI * 2f / outline.positionCount;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * AnimalBody.Radius);
        }
    }

    public void ConsumeKill(double amount)
    {
        if (!Body.IsAlive) return;
        Resources.Feed(amount);
        if (manager != null) manager.TryReproduce(this);
    }
}
