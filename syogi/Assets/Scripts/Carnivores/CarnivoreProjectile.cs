using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(40)]
public sealed class CarnivoreProjectile : MonoBehaviour
{
    public const float Speed = 250f;
    public const float Radius = 5f;
    public const double InitialPower = 15;
    public const double PowerDecayPerSecond = 10;
    private CarnivoreAnimal owner;
    private AnimalBody ownerBody;
    private Vector2 direction;
    private double power = InitialPower;
    private readonly HashSet<AnimalBody> hitBodies = new HashSet<AnimalBody>();
    private readonly List<Contact> contacts = new List<Contact>();
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
        if (Time.deltaTime <= 0) return;
        float distance = Speed * Time.deltaTime;
        double flightSeconds = distance / Speed;
        Vector2 start = transform.position;
        Vector2 end = start + direction * distance;
        contacts.Clear();
        foreach (AnimalBody body in AnimalBody.Active)
        {
            if (body == ownerBody || !body.IsAlive || hitBodies.Contains(body)) continue;
            Vector2 center = body.transform.position;
            if (ProjectileSweep.TryHit(start.x, start.y, end.x, end.y, center.x, center.y,
                AnimalBody.Radius + Radius, out double fraction))
                contacts.Add(new Contact { Body = body, Fraction = fraction, Order = contacts.Count });
        }
        contacts.Sort((a, b) =>
        {
            int order = a.Fraction.CompareTo(b.Fraction);
            return order != 0 ? order : a.Order.CompareTo(b.Order);
        });
        // Snapshot permits deaths and births to change the registry during hits.
        double previousFraction = 0;
        foreach (Contact contact in contacts)
        {
            if (contact.Body == null || !contact.Body.IsAlive) continue;
            // Decay only up to each contact, so damage is independent of frame length.
            power = System.Math.Max(0, power - PowerDecayPerSecond * flightSeconds * (contact.Fraction - previousFraction));
            previousFraction = contact.Fraction;
            if (power <= 0) { Destroy(gameObject); return; }
            hitBodies.Add(contact.Body);
            power = contact.Body.Hit(power, owner);
            if (power <= 0) { Destroy(gameObject); return; }
        }
        power = System.Math.Max(0, power - PowerDecayPerSecond * flightSeconds * (1 - previousFraction));
        if (power <= 0) { Destroy(gameObject); return; }
        transform.position = new Vector3(end.x, end.y, transform.position.z);
    }
}
