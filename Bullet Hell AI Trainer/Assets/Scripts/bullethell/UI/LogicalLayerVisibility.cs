using System.Collections.Generic;
using UnityEngine;

public static class LogicalLayerVisibility
{
    private static readonly Dictionary<int, bool> VisibilityByLayer =
        new Dictionary<int, bool>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        VisibilityByLayer.Clear();
    }

    public static bool IsVisible(int logicalLayer)
    {
        return !VisibilityByLayer.TryGetValue(logicalLayer, out bool visible) ||
               visible;
    }

    public static void SetVisible(int logicalLayer, bool visible)
    {
        logicalLayer = Mathf.Max(0, logicalLayer);
        VisibilityByLayer[logicalLayer] = visible;

        foreach (PlayerAgent player in
                 Object.FindObjectsByType<PlayerAgent>(FindObjectsInactive.Include))
        {
            if (player.LogicalLayer == logicalLayer)
            {
                Apply(player.gameObject, logicalLayer);
            }
        }

        foreach (bullet enemyBullet in BulletManager.ActiveBullets)
        {
            if (enemyBullet != null && enemyBullet.LogicalLayer == logicalLayer)
            {
                Apply(enemyBullet.gameObject, logicalLayer);
            }
        }

        foreach (PlayerBullet playerBullet in Object.FindObjectsByType<PlayerBullet>())
        {
            if (playerBullet.LogicalLayer == logicalLayer)
            {
                Apply(playerBullet.gameObject, logicalLayer);
            }
        }

        foreach (LaserAttack laserAttack in
                 Object.FindObjectsByType<LaserAttack>(FindObjectsInactive.Include))
        {
            if (laserAttack.LogicalLayer == logicalLayer)
            {
                Apply(laserAttack.gameObject, logicalLayer);
            }
        }
    }

    public static void Apply(GameObject target, int logicalLayer)
    {
        if (target == null)
        {
            return;
        }

        bool visible = IsVisible(logicalLayer);
        foreach (Renderer targetRenderer in
                 target.GetComponentsInChildren<Renderer>(true))
        {
            targetRenderer.enabled = visible;
        }
    }
}
