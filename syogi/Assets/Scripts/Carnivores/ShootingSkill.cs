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
        CarnivoreProjectile.Fire(owner, direction, material);
    }
}
