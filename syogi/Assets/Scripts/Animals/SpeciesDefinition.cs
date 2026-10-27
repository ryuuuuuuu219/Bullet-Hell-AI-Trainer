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
    public bool shoot = true;
    public bool accel;
    [Min(0.01f)] public float shotInterval = 1;
    [Min(0.01f)] public float bulletSpeed = 250;
    [Min(0.01f)] public float bulletPower = 25;
    [Min(0.01f)] public float bulletPowerDecay = 10;
    [Min(0)] public float angularSpeed = 180;
    [Min(0)] public float stopDistance = 150;
    [Min(0)] public float resumeDistance = 200;
    [Min(3)] public int outlineVertices = 3;
    public Color outlineColor = Color.gray;

    public bool Pursues(string id) => !string.IsNullOrEmpty(id) &&
        pursuitSpeciesIds != null && Array.IndexOf(pursuitSpeciesIds, id) >= 0;
}
