using System.Collections.Generic;
using UnityEngine;

public sealed class BulletManager : MonoBehaviour
{
    private static readonly List<bullet> ActiveBulletList = new List<bullet>();
    private static readonly Dictionary<int, List<bullet>> ActiveBulletsByLayer =
        new Dictionary<int, List<bullet>>();
    private static readonly IReadOnlyList<bullet> EmptyBulletList =
        new List<bullet>();
    private static BulletManager instance;

    public static IReadOnlyList<bullet> ActiveBullets => ActiveBulletList;
    public static int ActiveBulletCount => ActiveBulletList.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveBulletList.Clear();
        ActiveBulletsByLayer.Clear();
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        EnsureInstance();
    }

    public static void Register(bullet bulletData)
    {
        if (bulletData == null)
        {
            return;
        }

        EnsureInstance();
        if (!ActiveBulletList.Contains(bulletData))
        {
            ActiveBulletList.Add(bulletData);
        }

        int logicalLayer = bulletData.LogicalLayer;
        if (!ActiveBulletsByLayer.TryGetValue(
                logicalLayer,
                out List<bullet> layerBullets))
        {
            layerBullets = new List<bullet>();
            ActiveBulletsByLayer.Add(logicalLayer, layerBullets);
        }

        if (!layerBullets.Contains(bulletData))
        {
            layerBullets.Add(bulletData);
        }
    }

    public static void Unregister(bullet bulletData)
    {
        if (bulletData != null)
        {
            ActiveBulletList.Remove(bulletData);
            if (ActiveBulletsByLayer.TryGetValue(
                    bulletData.LogicalLayer,
                    out List<bullet> layerBullets))
            {
                layerBullets.Remove(bulletData);
            }
        }
    }

    public static IReadOnlyList<bullet> GetActiveBullets(int logicalLayer)
    {
        return ActiveBulletsByLayer.TryGetValue(
            logicalLayer,
            out List<bullet> layerBullets)
            ? layerBullets
            : EmptyBulletList;
    }

    public static bool TryGetNearest(Vector2 position, out bullet nearestBullet)
    {
        return TryGetNearest(position, -1, out nearestBullet);
    }

    public static bool TryGetNearest(
        Vector2 position,
        int logicalLayer,
        out bullet nearestBullet)
    {
        nearestBullet = null;
        float nearestSqrDistance = float.PositiveInfinity;

        List<bullet> candidates = logicalLayer >= 0 &&
                                  ActiveBulletsByLayer.TryGetValue(
                                      logicalLayer,
                                      out List<bullet> layerBullets)
            ? layerBullets
            : ActiveBulletList;

        for (int index = candidates.Count - 1; index >= 0; index--)
        {
            bullet candidate = candidates[index];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                candidates.RemoveAt(index);
                ActiveBulletList.Remove(candidate);
                continue;
            }

            float sqrDistance = ((Vector2)candidate.transform.position - position).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearestSqrDistance = sqrDistance;
            nearestBullet = candidate;
        }

        return nearestBullet != null;
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindAnyObjectByType<BulletManager>();
        if (instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject(nameof(BulletManager));
        instance = managerObject.AddComponent<BulletManager>();
        DontDestroyOnLoad(managerObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
