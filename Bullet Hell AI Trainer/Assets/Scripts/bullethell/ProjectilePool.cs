using System.Collections.Generic;
using UnityEngine;

public static class ProjectilePool
{
    private static readonly Dictionary<GameObject, Stack<GameObject>> Pools =
        new Dictionary<GameObject, Stack<GameObject>>();

    private static Transform poolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Pools.Clear();
        poolRoot = null;
    }

    public static GameObject Acquire(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }

        if (!Pools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            Pools.Add(prefab, pool);
        }

        GameObject instance = null;
        while (pool.Count > 0 && instance == null)
        {
            instance = pool.Pop();
        }

        if (instance == null)
        {
            instance = Object.Instantiate(prefab);
            instance.SetActive(false);
            PooledProjectile marker = instance.GetComponent<PooledProjectile>();
            if (marker == null)
            {
                marker = instance.AddComponent<PooledProjectile>();
            }

            marker.SourcePrefab = prefab;
        }

        PooledProjectile pooledProjectile = instance.GetComponent<PooledProjectile>();
        pooledProjectile.IsStored = false;
        instance.transform.SetParent(null, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    public static void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledProjectile marker = instance.GetComponent<PooledProjectile>();
        if (marker == null)
        {
            Object.Destroy(instance);
            return;
        }

        if (marker.IsStored)
        {
            return;
        }

        marker.IsStored = true;
        if (instance.TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        instance.SetActive(false);
        instance.transform.SetParent(GetPoolRoot(), false);

        if (marker.SourcePrefab == null)
        {
            Object.Destroy(instance);
            return;
        }

        if (!Pools.TryGetValue(marker.SourcePrefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            Pools.Add(marker.SourcePrefab, pool);
        }

        pool.Push(instance);
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null)
        {
            return poolRoot;
        }

        GameObject rootObject = new GameObject("Projectile Pool");
        Object.DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;
        return poolRoot;
    }
}
