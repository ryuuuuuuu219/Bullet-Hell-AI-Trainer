using UnityEngine;

public static class ProjectileCollisionLayers
{
    public const int PlayerHitbox = 6;
    public const int EnemyBullet = 7;
    public const int PlayerBullet = 8;
    public const int BossHitbox = 9;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureCollisionMatrix()
    {
        int[] controlledLayers =
        {
            PlayerHitbox,
            EnemyBullet,
            PlayerBullet,
            BossHitbox,
        };

        foreach (int firstLayer in controlledLayers)
        {
            foreach (int secondLayer in controlledLayers)
            {
                bool shouldCollide =
                    IsPair(firstLayer, secondLayer, PlayerHitbox, EnemyBullet) ||
                    IsPair(firstLayer, secondLayer, PlayerBullet, BossHitbox);
                Physics2D.IgnoreLayerCollision(
                    firstLayer,
                    secondLayer,
                    !shouldCollide);
            }
        }
    }

    private static bool IsPair(
        int firstLayer,
        int secondLayer,
        int expectedFirst,
        int expectedSecond)
    {
        return (firstLayer == expectedFirst && secondLayer == expectedSecond) ||
               (firstLayer == expectedSecond && secondLayer == expectedFirst);
    }
}
