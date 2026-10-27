using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(40)]
public sealed class CarnivoreProjectile : MonoBehaviour
{
    public const float Speed = 250f;
    public const float Radius = 5f;
    public const double InitialPower = 25;
    public const double PowerDecayPerSecond = 10;
    private CarnivoreAnimal owner;
    private AnimalBody ownerBody;
    private SpeciesDefinition sourceSpecies;
    public string SpeciesId { get; private set; }
    private Vector2 direction;
    private double power = InitialPower;
    private float speed = Speed;
    private double decay = PowerDecayPerSecond;
    private double remainingRange = double.PositiveInfinity;
    private readonly HashSet<AnimalBody> hitBodies = new HashSet<AnimalBody>();
    private readonly List<Contact> contacts = new List<Contact>();
    private readonly List<AnimalBody> candidates = new List<AnimalBody>();
    private struct Contact
    {
        public AnimalBody Body;
        public double Fraction;
        public int Order;
    }

    public static void Fire(CarnivoreAnimal owner, Vector2 direction, Material material)
    {
        var obj = new GameObject("Carnivore Bullet");
        obj.transform.position = owner.transform.position;
        var bullet = obj.AddComponent<CarnivoreProjectile>();
        bullet.owner = owner;
        bullet.ownerBody = owner.Body;
        bullet.SpeciesId = owner.Body.SpeciesId;
        bullet.sourceSpecies = owner.Body.Species;
        bullet.power = owner.Body.Species.bulletPower;
        bullet.speed = Mathf.Max(0.01f, owner.Body.Species.bulletSpeed);
        bullet.decay = System.Math.Max(0.01, owner.Body.Species.bulletPowerDecay);
        if (owner.Body.Species.projectileRange > 0) bullet.remainingRange = owner.Body.Species.projectileRange;
        bullet.direction = direction.normalized;
        var line = obj.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 16;
        line.widthMultiplier = 1f;
        line.startColor = line.endColor = Color.white;
        line.sortingOrder = 1;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Radius);
        }
    }

    private void Update()
    {
        double remainingSeconds = Time.deltaTime;
        Vector2 position = transform.position;
        // Re-query after reflection so the unused frame time follows the reflected path.
        while (remainingSeconds > 0)
        {
            double flightSeconds = System.Math.Min(remainingSeconds,
                System.Math.Min(power / decay, remainingRange / speed));
            if (flightSeconds <= 1e-9 || power <= 0 || remainingRange <= 0)
            {
                Destroy(gameObject);
                return;
            }
            float distance = (float)(speed * flightSeconds);
            Vector2 end = position + direction * distance;
            contacts.Clear();
            AnimalBody.QuerySpatial(position, end, Radius, true, candidates);
            foreach (AnimalBody body in candidates)
            {
                if (body == ownerBody || !body.IsAlive || hitBodies.Contains(body)) continue;
                if (SpeciesId != null && !sourceSpecies.PursuesSpecies(body.Species)) continue;
                Vector2 center = body.transform.position;
                if (ProjectileSweep.TryHit(position.x, position.y, end.x, end.y, center.x, center.y,
                    body.CollisionRadius + Radius, out double fraction))
                    contacts.Add(new Contact { Body = body, Fraction = fraction, Order = contacts.Count });
            }
            contacts.Sort((a, b) =>
            {
                int order = a.Fraction.CompareTo(b.Fraction);
                return order != 0 ? order : a.Order.CompareTo(b.Order);
            });
            double previousFraction = 0;
            bool reflected = false;
            foreach (Contact contact in contacts)
            {
                if (contact.Body == null || !contact.Body.IsAlive) continue;
                power = System.Math.Max(0, power - decay * flightSeconds * (contact.Fraction - previousFraction));
                previousFraction = contact.Fraction;
                if (power <= 0) { Destroy(gameObject); return; }
                hitBodies.Add(contact.Body);
                if (PredatorAbilities.CanReflect(contact.Body.Species.reflectProjectiles,
                    contact.Body.Resources.Health, power))
                {
                    position += direction * (distance * (float)contact.Fraction);
                    remainingRange -= distance * contact.Fraction;
                    remainingSeconds -= flightSeconds * contact.Fraction;
                    direction = -direction;
                    ownerBody = contact.Body;
                    owner = null;
                    SpeciesId = null; // Neutral: no same-species damage reduction or hit-birth owner.
                    reflected = true;
                    break;
                }
                power = contact.Body.Hit(power, owner, SpeciesId);
                if (owner != null) owner.OnAttackHit();
                if (power <= 0) { Destroy(gameObject); return; }
            }
            if (reflected) continue;
            power = System.Math.Max(0, power - decay * flightSeconds * (1 - previousFraction));
            remainingRange -= distance;
            remainingSeconds -= flightSeconds;
            position = end;
        }
        if (power <= 1e-9 || remainingRange <= 1e-6) { Destroy(gameObject); return; }
        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }
}
