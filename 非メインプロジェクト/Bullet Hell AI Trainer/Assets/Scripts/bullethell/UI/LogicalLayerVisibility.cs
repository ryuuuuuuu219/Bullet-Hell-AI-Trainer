using System.Collections.Generic;
using UnityEngine;

public static class LogicalLayerVisibility
{
    private static readonly Dictionary<int, bool> VisibilityByLayer =
        new Dictionary<int, bool>();
    private static int exclusiveVisibleLayer = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        VisibilityByLayer.Clear();
        exclusiveVisibleLayer = -1;
    }

    public static bool IsVisible(int logicalLayer)
    {
        if (exclusiveVisibleLayer >= 0)
        {
            return logicalLayer == exclusiveVisibleLayer;
        }

        return !VisibilityByLayer.TryGetValue(logicalLayer, out bool visible) ||
               visible;
    }

    public static void SetExclusiveVisibleLayer(int logicalLayer)
    {
        exclusiveVisibleLayer = logicalLayer >= 0 ? logicalLayer : -1;
        RefreshAllSceneObjects();
    }

    public static void SetVisible(int logicalLayer, bool visible)
    {
        logicalLayer = Mathf.Max(0, logicalLayer);
        VisibilityByLayer[logicalLayer] = visible;

        RefreshSceneObjects(logicalLayer);
    }

    public static void SetDefaultVisibleLayer(int logicalLayer, int layerCount)
    {
        exclusiveVisibleLayer = -1;
        VisibilityByLayer.Clear();
        int safeLayerCount = Mathf.Max(0, layerCount);
        for (int index = 0; index < safeLayerCount; index++)
        {
            VisibilityByLayer[index] = index == logicalLayer;
        }

        RefreshAllSceneObjects();
    }

    private static void RefreshAllSceneObjects()
    {
        foreach (PlayerAgent player in
                 Object.FindObjectsByType<PlayerAgent>(FindObjectsInactive.Include))
        {
            Apply(player.gameObject, player.LogicalLayer);
        }

        foreach (bullet enemyBullet in BulletManager.ActiveBullets)
        {
            if (enemyBullet != null)
            {
                Apply(enemyBullet.gameObject, enemyBullet.LogicalLayer);
            }
        }

        foreach (PlayerBullet playerBullet in Object.FindObjectsByType<PlayerBullet>())
        {
            Apply(playerBullet.gameObject, playerBullet.LogicalLayer);
        }

        foreach (LaserAttack laserAttack in
                 Object.FindObjectsByType<LaserAttack>(FindObjectsInactive.Include))
        {
            Apply(laserAttack.gameObject, laserAttack.LogicalLayer);
        }
    }

    private static void RefreshSceneObjects(int logicalLayer)
    {
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
        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        bool hasReplacedShapeRenderer =
            target.GetComponent<RegularPolygonLineRenderer>() != null ||
            target.GetComponent<PlayerTriangleRenderer>() != null;
        foreach (Renderer targetRenderer in
                 target.GetComponentsInChildren<Renderer>(true))
        {
            bool isReplacedShapeLine = spriteRenderer != null &&
                targetRenderer.gameObject == target &&
                targetRenderer is LineRenderer &&
                hasReplacedShapeRenderer;
            targetRenderer.enabled = visible && !isReplacedShapeLine;
        }
    }
}
