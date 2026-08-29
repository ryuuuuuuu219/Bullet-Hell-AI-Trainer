using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAgent : MonoBehaviour
{
    [SerializeField, Min(0)] private int logicalLayer;

    public int LogicalLayer => logicalLayer;
    public bool IsHit { get; private set; }

    public event Action<bullet> Hit;

    private void Awake()
    {
        EnsureCollisionShape();
    }

    public void SetLogicalLayer(int value)
    {
        logicalLayer = Mathf.Max(0, value);
        LogicalLayerVisibility.Apply(gameObject, logicalLayer);
    }

    public void RegisterHit(bullet source)
    {
        if (source == null || source.LogicalLayer != logicalLayer)
        {
            return;
        }

        IsHit = true;
        Hit?.Invoke(source);
    }

    public void ResetHitState()
    {
        IsHit = false;
    }

    private void EnsureCollisionShape()
    {
        gameObject.layer = ProjectileCollisionLayers.PlayerHitbox;
        CircleCollider2D collisionShape = GetComponent<CircleCollider2D>();
        if (collisionShape == null)
        {
            collisionShape = gameObject.AddComponent<CircleCollider2D>();
        }

        collisionShape.isTrigger = true;
        collisionShape.radius = 5f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        logicalLayer = Mathf.Max(0, logicalLayer);
    }
#endif
}
