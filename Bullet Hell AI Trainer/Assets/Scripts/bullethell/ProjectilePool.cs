using System.Collections.Generic;
using UnityEngine;

public static class ProjectilePool
{
    private const float ViewportReleaseMargin = 0.1f;

    private static readonly Dictionary<GameObject, Stack<GameObject>> Pools =
        new Dictionary<GameObject, Stack<GameObject>>();
    private static Transform poolRoot;
    private static Camera mainCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Pools.Clear();
        poolRoot = null;
        mainCamera = null;
    }

    public static void Prewarm(GameObject prefab, int initialCount)
    {
        if (prefab == null || initialCount <= 0)
        {
            return;
        }

        if (!Pools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>(initialCount);
            Pools.Add(prefab, pool);
        }

        while (pool.Count < initialCount)
        {
            GameObject instance = CreateInstance(prefab);
            PooledProjectile marker = instance.GetComponent<PooledProjectile>();
            marker.IsStored = true;
            pool.Push(instance);
        }
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
            instance = CreateInstance(prefab);
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

    private static GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = Object.Instantiate(prefab, GetPoolRoot(), false);
        instance.SetActive(false);
        PooledProjectile marker = instance.GetComponent<PooledProjectile>();
        if (marker == null)
        {
            marker = instance.AddComponent<PooledProjectile>();
        }

        marker.SourcePrefab = prefab;
        return instance;
    }

    public static bool ReleaseIfOutsideCameraView(GameObject instance)
    {
        if (instance == null)
        {
            return false;
        }

        Camera camera = GetMainCamera();
        if (camera == null)
        {
            return false;
        }

        Vector3 viewportPosition = camera.WorldToViewportPoint(
            instance.transform.position);
        if (viewportPosition.z > 0f &&
            viewportPosition.x >= -ViewportReleaseMargin &&
            viewportPosition.x <= 1f + ViewportReleaseMargin &&
            viewportPosition.y >= -ViewportReleaseMargin &&
            viewportPosition.y <= 1f + ViewportReleaseMargin)
        {
            return false;
        }

        Release(instance);
        return true;
    }

    private static Camera GetMainCamera()
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
        {
            mainCamera = Camera.main;
        }

        return mainCamera;
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null)
        {
            return poolRoot;
        }

        GameObject rootObject = new GameObject("Projectile Pool");
        rootObject.SetActive(false);
        Object.DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;
        return poolRoot;
    }
}
