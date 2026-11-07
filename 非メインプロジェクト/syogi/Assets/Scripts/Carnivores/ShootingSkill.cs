using UnityEngine;

public static class ShootingSkill
{
    public static void Step(CarnivoreAnimal owner, Vector2 direction, Material material,
        float seconds, ref float elapsed)
    {
        float interval = Mathf.Max(0.01f, owner.Body.Species.shotInterval);
        elapsed = Mathf.Min(interval, elapsed + seconds);
        if (elapsed < interval) return;
        elapsed = 0f;
        owner.Resources.SpendStamina(System.Math.Max(0, owner.Body.Traits.shootingStaminaCost));
        if (owner.Resources.IsDead) { owner.Body.Die(); return; }
        var species = owner.Body.Species;
        int count = species.multiShot ? Mathf.Max(1, species.shotCount) : 1;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)PredatorAbilities.ShotAngle(i, count, species.shotSpacingDegrees,
                species.multiShot && species.fullCircleShot) * Mathf.Deg2Rad;
            Vector2 shot = new Vector2(direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle),
                direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle));
            CarnivoreProjectile.Fire(owner, shot, material);
        }
    }
}
