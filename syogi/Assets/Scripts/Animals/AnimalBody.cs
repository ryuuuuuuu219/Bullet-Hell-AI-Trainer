using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(0)]
[RequireComponent(typeof(SpeciesIdentity))]
public sealed class AnimalBody : MonoBehaviour
{
    private static readonly List<AnimalBody> active = new List<AnimalBody>();
    private static int nextSeparationId;
    private static readonly SpatialGrid<AnimalBody> spatialGrid = new SpatialGrid<AnimalBody>(200);
    private static int spatialFrame = -1;
    private static float maximumBodyRadius;

    public void SyncSpatialPosition()
    {
        if (!isActiveAndEnabled || Resources == null) return;
        Vector3 position = transform.position;
        spatialGrid.Update(this, position.x, position.y);
        // Health can recover between queries. Bound by the health limit, not current HP.
        maximumBodyRadius = Mathf.Max(maximumBodyRadius, (float)AnimalTactics.Radius(Resources.HealthLimit));
    }

    public static void QuerySpatial(Vector2 start, Vector2 end, float padding,
        bool includeBodyRadius, List<AnimalBody> results)
    {
        if (spatialFrame != Time.frameCount)
        {
            spatialFrame = Time.frameCount;
            foreach (AnimalBody body in active) body.SyncSpatialPosition();
        }
        padding = Mathf.Max(0, padding) + (includeBodyRadius ? maximumBodyRadius : 0);
        spatialGrid.Query(Mathf.Min(start.x, end.x) - padding, Mathf.Min(start.y, end.y) - padding,
            Mathf.Max(start.x, end.x) + padding, Mathf.Max(start.y, end.y) + padding, results);
        // Match Active order, including equal-time hits and floating-point force sums.
        results.Sort((a, b) => a.SeparationId.CompareTo(b.SeparationId));
    }
    public int SeparationId { get; private set; }
    public static IReadOnlyList<AnimalBody> Active => active;
    public static int LivingSpeciesCount(string species)
    {
        int count = 0;
        foreach (AnimalBody body in active)
            if (body.IsAlive && body.SpeciesId == species) count++;
        return count;
    }
    public float Radius => (float)AnimalTactics.Radius(Resources == null ? 0 : Resources.Health);
    public SpeciesIdentity Identity { get; private set; }
    public string SpeciesId => Identity == null ? string.Empty : Identity.SpeciesId;
    public SpeciesDefinition Species => Identity == null ? null : Identity.Definition;
    private SpeciesManager speciesManager;
    public AnimalResources Resources { get; private set; }
    public bool IsHerbivore { get; private set; }
    public bool IsAlive => isActiveAndEnabled && Resources != null && !Resources.IsDead;
    public AnimalTraits Traits { get; private set; }
    public AnimalActivity Activity { get; private set; } = new AnimalActivity();
    public bool IsSleeping => Traits != null && !Traits.IsActive(AquariumDayNight.CurrentLight);
    public bool IsRunning => !IsSleeping && Activity.IsRunning;
    public Vector2 EscapeDirection { get; private set; }
    public float SearchDistance => (float)Traits.SearchRadius(AquariumDayNight.CurrentLight);
    private LineRenderer outline;
    private float drawnRadius = -1;
    private CircleCollider2D absorptionCollider;
    private bool dying;
    private AnimalBody sensedThreat;
    private float nextThreatScan;

    private void LateUpdate()
    {
        if (absorptionCollider != null) absorptionCollider.radius = Radius;
        if (!IsHerbivore || !IsAlive || outline == null || drawnRadius == Radius) return;
        drawnRadius = Radius;
        for (int i = 0; i < outline.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / outline.positionCount;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * drawnRadius);
        }
    }

    public bool CanDetect(AnimalBody candidate)
    {
        if (IsSleeping || candidate == null || !candidate.IsAlive) return false;
        float distanceSquared = ((Vector2)(candidate.transform.position - transform.position)).sqrMagnitude;
        float radius = SearchDistance;
        return distanceSquared <= radius * radius;
    }

    private void Update()
    {
        if (Resources != null && Resources.IsDead) { Die(); return; }
        if (!IsAlive || Time.deltaTime <= 0) return;
        if (!IsHerbivore || IsSleeping)
        {
            sensedThreat = null;
            nextThreatScan = 0;
        }
        else if (Time.time >= nextThreatScan)
        {
            nextThreatScan = Time.time + 0.2f;
            sensedThreat = null;
            float nearest = float.PositiveInfinity;
            foreach (AnimalBody other in active)
            {
                if (other == this || other.IsHerbivore || !CanDetect(other)) continue;
                float distance = ((Vector2)(transform.position - other.transform.position)).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                sensedThreat = other;
            }
        }
        // Death is immediate; detection is sampled every 0.2 simulation seconds.
        AnimalBody threat = sensedThreat != null && sensedThreat.IsAlive ? sensedThreat : null;
        if (threat != null)
        {
            Vector2 away = (Vector2)(transform.position - threat.transform.position);
            EscapeDirection = away.sqrMagnitude > 0 ? away.normalized : Vector2.right;
        }
        Activity.Step(Time.deltaTime, AquariumDayNight.CurrentLight, threat != null, Traits, Resources);
        if (Resources.IsDead) Die();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        active.Clear(); nextSeparationId = 0;
        spatialGrid.Clear(); spatialFrame = -1; maximumBodyRadius = 0;
    }
    private void OnEnable()
    {
        sensedThreat = null;
        nextThreatScan = 0;
        if (active.Contains(this)) return;
        SeparationId = ++nextSeparationId;
        active.Add(this);
        if (speciesManager != null) speciesManager.Population?.Register(this);
        SyncSpatialPosition();
    }
    private void OnDisable()
    {
        active.Remove(this); spatialGrid.Remove(this);
        if (speciesManager != null) speciesManager.Population?.Unregister(this);
    }
    public void Initialize(AnimalResources resources, bool herbivore)
    {
        Resources = resources;
        IsHerbivore = herbivore;
        outline = GetComponent<LineRenderer>();
        absorptionCollider = gameObject.AddComponent<CircleCollider2D>();
        absorptionCollider.isTrigger = true;
        absorptionCollider.radius = Radius;
        speciesManager = GetComponentInParent<SpeciesManager>();
        Identity = GetComponent<SpeciesIdentity>();
        if (speciesManager != null) Identity.Initialize(speciesManager.Definition);
        if (Species == null)
        {
            Debug.LogError("Animal requires a configured species manager or SpeciesIdentity.", this);
            gameObject.SetActive(false);
            return;
        }
        Traits = Species.traits.Copy();
        speciesManager?.Population?.Register(this);
        SyncSpatialPosition();
    }

    public double Hit(double power, CarnivoreAnimal attacker, string projectileSpeciesId)
    {
        if (!IsAlive) return power;
        double remaining = Resources.Hit(power, projectileSpeciesId, SpeciesId, out double stolen);
        if (Resources.IsDead)
        {
            Die(stolen); // Immediately remove it so another bullet cannot claim this kill.
        }
        return remaining;
    }

    public void Die()
    {
        Die(Resources == null ? 0 : Resources.ReleaseCarbon());
    }

    public void ScatterCarbon(double carbon)
    {
        DropCarbon.Scatter(transform.position, carbon, outline == null ? null : outline.sharedMaterial, SpeciesId);
    }

    private void Die(double carbon)
    {
        if (dying) return;
        dying = true;
        gameObject.SetActive(false);
        ScatterCarbon(carbon);
        Destroy(gameObject);
    }
}
