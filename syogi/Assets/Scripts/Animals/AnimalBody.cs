using System.Collections.Generic;
using UnityEngine;

public sealed class AnimalBody : MonoBehaviour
{
    private static readonly List<AnimalBody> active = new List<AnimalBody>();
    public static IReadOnlyList<AnimalBody> Active => active;
    public const float Radius = 80f;
    public AnimalResources Resources { get; private set; }
    public bool IsHerbivore { get; private set; }
    public bool IsAlive => isActiveAndEnabled && Resources != null && !Resources.IsDead;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();
    private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
    private void OnDisable() => active.Remove(this);
    public void Initialize(AnimalResources resources, bool herbivore)
    {
        Resources = resources;
        IsHerbivore = herbivore;
    }

    public double Hit(double power, CarnivoreAnimal attacker)
    {
        if (!IsAlive) return power;
        double remaining = Resources.Hit(power, out double stolen);
        if (Resources.IsDead)
        {
            Die(); // Immediately remove it so another bullet cannot claim this kill.
            if (attacker != null) attacker.ConsumeKill(stolen);
        }
        return remaining;
    }

    public void Die()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
