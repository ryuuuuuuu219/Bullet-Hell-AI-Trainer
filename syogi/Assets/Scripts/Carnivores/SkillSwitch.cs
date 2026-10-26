using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillSwitch : MonoBehaviour
{
    public bool accel;
    public bool shoot = true;
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
