using UnityEngine;

[DefaultExecutionOrder(30)]
[RequireComponent(typeof(AnimalBody), typeof(LineRenderer))]
[RequireComponent(typeof(SkillSwitch))]
public sealed class CarnivoreAnimal : MonoBehaviour
{
    public const float Speed = 105f;
    public const float AngularSpeed = 180f;
    public const float SearchRadius = 1000f;
    public const float Range = 200f;
    public const float StopDistance = 150f;
    public const float ResumeDistance = 200f;
    public const double InitialHealth = 50;
    [SerializeField, Min(1)] private int foodLayer = 1;
    [SerializeField] private Vector2 movementDirection = Vector2.right;
    [SerializeField, Min(0)] private float separationRadius = 200f;
    [SerializeField, Min(0)] private float separationStrength = 50f;
    [SerializeField, Min(0)] private float separationMaxSpeed = 50f;
    public Vector2 MovementDirection => movementDirection;
    public AnimalResources Resources { get; private set; }
    public AnimalBody Body { get; private set; }
    public bool IsStopped { get; private set; }
    private CarnivoreManager manager;
    private AnimalBody target;
    private bool hadTarget;
    private float decisionElapsed;
    private SkillSwitch skills;
    private LineRenderer outline;

    private void Awake()
    {
        Resources = new AnimalResources(InitialHealth);
        Body = GetComponent<AnimalBody>();
        Body.Initialize(Resources, false);
        manager = GetComponentInParent<CarnivoreManager>();
        skills = GetComponent<SkillSwitch>();
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

    private bool CanPursue(AnimalBody candidate) => candidate != null && candidate.IsAlive && candidate.IsHerbivore &&
        !AnimalTactics.Abandon(Resources.Health, candidate.Resources.Health, Resources.Amount, candidate.Resources.Amount) &&
        Body.CanDetect(candidate);

    private void DropTarget() { target = null; hadTarget = false; IsStopped = false; }
    private void LateUpdate() => DrawOutline();

    private void ChooseTarget()
    {
        if (CanPursue(target)) return;
        target = null;
        hadTarget = false;
        IsStopped = false;
        float nearest = Body.SearchDistance * Body.SearchDistance;
        foreach (AnimalBody candidate in AnimalBody.Active)
        {
            if (!CanPursue(candidate)) continue;
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
        if (remaining <= 0 || Body.IsSleeping || !Body.IsAlive) return;
        skills.ApplyAccelerationCost(Body.Traits, Resources, remaining);
        if (target != null && !CanPursue(target)) DropTarget();
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
            if (!Body.IsAlive) return;
        }
        if (manager != null) manager.TryReproduce(this);
    }

    private void MoveAndShoot(float dt)
    {
        if (target != null && !CanPursue(target)) DropTarget();
        Vector2 position = transform.position;
        float half = GrassField.FieldSize * 0.5f;
        float speed = skills.MovementSpeed(Body.Traits);
        float margin = 2f * speed / (AngularSpeed * Mathf.Deg2Rad) + Body.Radius;
        bool boundary = Mathf.Abs(position.x) >= half - margin || Mathf.Abs(position.y) >= half - margin;
        bool aliveTarget = target != null && target.IsAlive;
        Vector2 desired = movementDirection;
        if (boundary || (hadTarget && !aliveTarget))
        {
            IsStopped = false;
            desired = -position;
        }
        else if (aliveTarget)
        {
            desired = (Vector2)target.transform.position - position;
            if (IsStopped)
            {
                if (desired.sqrMagnitude >= ResumeDistance * ResumeDistance) IsStopped = false;
            }
            else if (desired.sqrMagnitude <= StopDistance * StopDistance) IsStopped = true;
        }
        else IsStopped = false;
        if (desired.sqrMagnitude > 0)
        {
            float turn = Mathf.Clamp(Vector2.SignedAngle(movementDirection, desired), -AngularSpeed * dt, AngularSpeed * dt);
            float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) + turn * Mathf.Deg2Rad;
            movementDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        if (!IsStopped)
        {
            if (speed > 0)
                Resources.SpendStamina(System.Math.Max(0, Body.Traits.movementStaminaPerSecond) * dt);
            if (Resources.IsDead) { Body.Die(); return; }
            position += movementDirection * (speed * dt);
        }
        // Separation is a position correction, including while stopped to shoot.
        position += SeparationVelocity() * dt;
        position.x = Mathf.Clamp(position.x, -half, half - 0.001f);
        position.y = Mathf.Clamp(position.y, -half, half - 0.001f);
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        // No angle gate or lead aiming: fire along the current heading.
        if (target != null && !CanPursue(target)) { DropTarget(); }
        skills.StepShooting(this, movementDirection, outline.sharedMaterial, dt);
    }

    private void DrawOutline()
    {
        if (outline == null) return;
        float heading = Mathf.Atan2(movementDirection.y, movementDirection.x);
        for (int i = 0; i < outline.positionCount; i++)
        {
            float angle = heading + i * Mathf.PI * 2f / outline.positionCount;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Body.Radius);
        }
    }

    private Vector2 SeparationVelocity()
    {
        double x = 0, y = 0;
        Vector2 position = transform.position;
        foreach (AnimalBody other in AnimalBody.Active)
        {
            if (other == Body || !other.IsAlive || other.IsHerbivore || other.SpeciesId != Body.SpeciesId) continue;
            Vector2 delta = position - (Vector2)other.transform.position;
            CarnivoreSpacing.Repulsion(delta.x, delta.y, Body.SeparationId, other.SeparationId,
                separationRadius, separationStrength, out double rx, out double ry);
            x += rx;
            y += ry;
        }
        CarnivoreSpacing.Limit(ref x, ref y, separationMaxSpeed);
        return new Vector2((float)x, (float)y);
    }

    public void ConsumeKill(double amount)
    {
        if (!Body.IsAlive || Body.IsSleeping) return;
        Resources.Feed(amount);
    }
}
