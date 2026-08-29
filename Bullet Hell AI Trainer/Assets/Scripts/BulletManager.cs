using System.Collections.Generic;
using UnityEngine;

public sealed class BulletManager : MonoBehaviour
{
    private static readonly List<bullet> ActiveBulletList = new List<bullet>();
    private static BulletManager instance;

    public static IReadOnlyList<bullet> ActiveBullets => ActiveBulletList;
    public static int ActiveBulletCount => ActiveBulletList.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveBulletList.Clear();
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
    }

    public static void Unregister(bullet bulletData)
    {
        if (bulletData != null)
        {
            ActiveBulletList.Remove(bulletData);
        }
    }

    public static bool TryGetNearest(Vector2 position, out bullet nearestBullet)
    {
        nearestBullet = null;
        float nearestSqrDistance = float.PositiveInfinity;

        for (int index = ActiveBulletList.Count - 1; index >= 0; index--)
        {
            bullet candidate = ActiveBulletList[index];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                ActiveBulletList.RemoveAt(index);
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
