using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(0)]
public sealed class AnimalBody : MonoBehaviour
{
    private static readonly List<AnimalBody> active = new List<AnimalBody>();
    private static int nextSeparationId;
    public int SeparationId { get; private set; }
    public static IReadOnlyList<AnimalBody> Active => active;
    public float Radius => (float)AnimalTactics.Radius(Resources == null ? 0 : Resources.Health);
    // Zero selects the current MVP default; explicit IDs allow future species.
    [SerializeField, Min(0)] private int speciesId;
    public int SpeciesId => speciesId;
    public AnimalResources Resources { get; private set; }
    public bool IsHerbivore { get; private set; }
    public bool IsAlive => isActiveAndEnabled && Resources != null && !Resources.IsDead;
    public AnimalTraits Traits { get; private set; }
    public AnimalActivity Activity { get; private set; } = new AnimalActivity();
    public bool IsSleeping => Traits != null && !Traits.IsActive(AquariumDayNight.CurrentLight);
    public bool IsRunning => !IsSleeping && Activity.IsRunning;
    public Vector2 EscapeDirection { get; private set; }
    public float SearchDistance => (float)Traits.SearchRadius(AquariumDayNight.CurrentLight);
    private GrassField grass;
    private LineRenderer outline;
    private float drawnRadius = -1;
    private CircleCollider2D absorptionCollider;
    private bool dying;

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
        if (distanceSquared > SearchDistance * SearchDistance) return false;
        float coverage = grass == null ? 0 : grass.GrassCoverage(candidate.transform.position);
        double intervening = grass == null ? 0 : grass.GrassBetween(transform.position, candidate.transform.position);
        double radius = Traits.DetectionRadius(AquariumDayNight.CurrentLight, coverage, candidate.Traits.maximumGrassConcealment, intervening);
        return distanceSquared <= radius * radius;
    }

    private void Update()
    {
        if (Resources != null && Resources.IsDead) { Die(); return; }
        if (!IsAlive || Time.deltaTime <= 0) return;
        AnimalBody threat = null;
        float nearest = float.PositiveInfinity;
        if (IsHerbivore && !IsSleeping)
        {
            foreach (AnimalBody other in active)
            {
                if (other == this || other.IsHerbivore || !CanDetect(other)) continue;
                float distance = ((Vector2)(transform.position - other.transform.position)).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                threat = other;
            }
        }
        if (threat != null)
        {
            Vector2 away = (Vector2)(transform.position - threat.transform.position);
            EscapeDirection = away.sqrMagnitude > 0 ? away.normalized : Vector2.right;
        }
        Activity.Step(Time.deltaTime, AquariumDayNight.CurrentLight, threat != null, Traits, Resources);
        if (Resources.IsDead) Die();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() { active.Clear(); nextSeparationId = 0; }
    private void OnEnable()
    {
        if (active.Contains(this)) return;
        SeparationId = ++nextSeparationId;
        active.Add(this);
    }
    private void OnDisable() => active.Remove(this);
    public void Initialize(AnimalResources resources, bool herbivore)
    {
        Resources = resources;
        IsHerbivore = herbivore;
        outline = GetComponent<LineRenderer>();
        absorptionCollider = gameObject.AddComponent<CircleCollider2D>();
        absorptionCollider.isTrigger = true;
        absorptionCollider.radius = Radius;
        var herbivoreManager = GetComponentInParent<HerbivoreManager>();
        var carnivoreManager = GetComponentInParent<CarnivoreManager>();
        Traits = (herbivore ? (herbivoreManager == null ? AnimalTraits.HerbivoreDefaults() : herbivoreManager.Traits)
            : (carnivoreManager == null ? new AnimalTraits() : carnivoreManager.Traits)).Copy();
        grass = FindAnyObjectByType<GrassField>();
        if (speciesId == 0) speciesId = herbivore ? 1 : 2;
    }

    public double Hit(double power, CarnivoreAnimal attacker, int projectileSpeciesId)
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

    private void Die(double carbon)
    {
        if (dying) return;
        dying = true;
        gameObject.SetActive(false);
        DropCarbon.Scatter(transform.position, carbon, outline == null ? null : outline.sharedMaterial);
        Destroy(gameObject);
    }
}
