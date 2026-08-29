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
        PolygonCollider2D collisionShape = GetComponent<PolygonCollider2D>();
        if (collisionShape == null)
        {
            collisionShape = gameObject.AddComponent<PolygonCollider2D>();
        }

        collisionShape.isTrigger = true;
        collisionShape.SetPath(0, new[]
        {
            new Vector2(0f, 0.6f),
            new Vector2(-0.5f, -0.6f),
            new Vector2(0.5f, -0.6f),
        });
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        logicalLayer = Mathf.Max(0, logicalLayer);
    }
#endif
}
