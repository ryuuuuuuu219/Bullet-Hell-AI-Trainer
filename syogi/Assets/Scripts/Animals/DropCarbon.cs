using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class DropCarbon : MonoBehaviour
{
    public double carbon;
    public string SpeciesId { get; private set; }
    private const float LifetimeSeconds = 180f;
    private float expiresAt;
    private float timer = 1f;
    private AnimalBody target;
    private bool isFeed;
    private CircleCollider2D contact;
    private Rigidbody2D physicsBody;

    private void Awake()
    {
        expiresAt = Time.time + LifetimeSeconds;
        contact = GetComponent<CircleCollider2D>();
        contact.isTrigger = true;
        contact.radius = 60f;
        physicsBody = GetComponent<Rigidbody2D>();
        physicsBody.gravityScale = 0;
        physicsBody.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    public static void Scatter(Vector3 position, double amount, Material material, string speciesId)
    {
        foreach (double part in CarbonDecomposition.Split(amount))
        {
            var obj = new GameObject("Carbon " + part);
            Vector2 offset = Random.insideUnitCircle * 100f;
            float half = GrassField.FieldSize * 0.5f;
            obj.transform.position = new Vector3(
                Mathf.Clamp(position.x + offset.x, -half, half - 0.001f),
                Mathf.Clamp(position.y + offset.y, -half, half - 0.001f), position.z);
            var drop = obj.AddComponent<DropCarbon>();
            drop.carbon = part;
            drop.SpeciesId = speciesId;
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 16;
            line.widthMultiplier = 3f;
            line.startColor = line.endColor = Color.yellow;
            line.sortingOrder = 2;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 10f);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => TryAbsorb(other);
    private void OnTriggerStay2D(Collider2D other) => TryAbsorb(other);

    private void TryAbsorb(Collider2D other)
    {
        if (isFeed || Time.timeScale <= 0) return;
        if (ExpireIfNeeded()) return;
        var animal = other.GetComponentInParent<AnimalBody>();
        if (animal == null || !animal.IsAlive || animal.SpeciesId == SpeciesId) return;
        double accepted = animal.Resources.AbsorbCarbon(carbon);
        carbon -= accepted;
        if (carbon > 0) return;
        target = animal;
        isFeed = true;
        contact.enabled = false;
        physicsBody.simulated = false;
    }

    private void Update()
    {
        if (ExpireIfNeeded()) return;
        if (!isFeed) return;
        if (target == null || !target.IsAlive || timer <= 0)
        {
            Destroy(gameObject);
            return;
        }
        float dt = Time.deltaTime;
        transform.position = Vector3.Lerp(transform.position, target.transform.position,
            Mathf.Clamp01(dt / timer));
        timer -= dt;
        if (timer <= 0) Destroy(gameObject);
    }

    private bool ExpireIfNeeded()
    {
        if (Time.time < expiresAt) return false;
        // Remaining carbon leaves the ecosystem; it is not scattered again.
        carbon = 0;
        contact.enabled = false;
        physicsBody.simulated = false;
        Destroy(gameObject);
        return true;
    }
}
