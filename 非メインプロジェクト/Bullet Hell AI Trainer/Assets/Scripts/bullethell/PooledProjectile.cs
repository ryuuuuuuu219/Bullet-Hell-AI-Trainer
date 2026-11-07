using UnityEngine;

public sealed class PooledProjectile : MonoBehaviour
{
    public GameObject SourcePrefab { get; set; }
    public bool IsStored { get; set; }
}
