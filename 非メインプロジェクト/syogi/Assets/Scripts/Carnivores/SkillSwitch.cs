using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillSwitch : MonoBehaviour
{
    private AnimalBody body;
    private void Awake() => body = GetComponent<AnimalBody>();
    private SpeciesDefinition Species => body == null ? null : body.Species;
    public bool accel => Species != null && Species.accel;
    public bool shoot => Species != null && Species.shoot;
    private float shotElapsed;

    public float MovementSpeed(AnimalTraits traits) =>
        AccelerationSkill.Speed(traits, isActiveAndEnabled && accel);

    public void ApplyAccelerationCost(AnimalTraits traits, AnimalResources resources, float seconds)
    {
        if (isActiveAndEnabled && accel)
            AccelerationSkill.Consume(traits, resources, seconds);
    }

    public void StepShooting(CarnivoreAnimal owner, Vector2 direction, Material material, float seconds)
    {
        if (!isActiveAndEnabled || !shoot) return;
        ShootingSkill.Step(owner, direction, material, seconds, ref shotElapsed);
    }
}
