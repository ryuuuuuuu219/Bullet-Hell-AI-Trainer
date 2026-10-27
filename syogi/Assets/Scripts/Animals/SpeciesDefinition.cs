using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Species", menuName = "Aquarium/Species")]
public sealed class SpeciesDefinition : ScriptableObject
{
    public string speciesId;
    public string displayName;
    public AnimalTraits traits = new AnimalTraits();
    [Min(0.01f)] public float initialHealth = 50;
    public string[] pursuitSpeciesIds = Array.Empty<string>();
    public string[] fleeSpeciesIds = Array.Empty<string>();
    public bool fleeNonHerbivores;
    public bool fleeShips;
    public bool isShip;
    [Min(0)] public int shipRank;
    public bool shoot = true;
    public bool accel;
    public bool proportionalNavigation;
    public float navigationConstant = 2.8f;
    public bool reproduceOnHit;
    public bool smallHitbox;
    [Min(0.01f)] public float hitboxRadius = 5;
    public bool reflectProjectiles;
    public bool multiShot;
    public bool fullCircleShot;
    [Min(1)] public int shotCount = 1;
    [Min(0)] public float shotSpacingDegrees = 8;
    [Min(0)] public float projectileRange;
    public bool doubleOutline;
    public bool trail;
    public bool preferLowHealth;
    public bool abandonStrongerTargets = true;
    [Min(0.01f)] public float shotInterval = 1;
    [Min(0.01f)] public float bulletSpeed = 250;
    [Min(0.01f)] public float bulletPower = 25;
    [Min(0.01f)] public float bulletPowerDecay = 10;
    [Min(0)] public float angularSpeed = 180;
    [Min(0)] public float stopDistance = 150;
    [Min(0)] public float resumeDistance = 200;
    [Min(3)] public int outlineVertices = 3;
    [Min(0.01f)] public float outlineRadiusScale = 1;
    public Color outlineColor = Color.gray;

    public bool Pursues(string id) => !string.IsNullOrEmpty(id) &&
        pursuitSpeciesIds != null && Array.IndexOf(pursuitSpeciesIds, id) >= 0;

    public bool PursuesSpecies(SpeciesDefinition other) => other != null &&
        (isShip ? !other.isShip || other.shipRank <= shipRank : Pursues(other.speciesId));

    public bool HasFleeTargets => isShip || fleeShips || fleeNonHerbivores || (fleeSpeciesIds != null && fleeSpeciesIds.Length > 0);

    public bool Fears(SpeciesDefinition other) => other != null &&
        ((isShip && other.isShip && other.shipRank >= shipRank) ||
         (fleeShips && other.isShip) ||
         (fleeNonHerbivores && other.speciesId != "herbivore") ||
         (fleeSpeciesIds != null && Array.IndexOf(fleeSpeciesIds, other.speciesId) >= 0));
}
