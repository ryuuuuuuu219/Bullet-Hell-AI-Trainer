using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Boss : MonoBehaviour
{
    [SerializeField] private List<int> damageByLayer = new List<int>();

    public IReadOnlyList<int> DamageByLayer => damageByLayer;

    private void Awake()
    {
        EnsureCollisionShape();
    }

    public void SetLayerCount(int layerCount)
    {
        layerCount = Mathf.Max(0, layerCount);

        while (damageByLayer.Count < layerCount)
        {
            damageByLayer.Add(0);
        }

        if (damageByLayer.Count > layerCount)
        {
            damageByLayer.RemoveRange(layerCount, damageByLayer.Count - layerCount);
        }
    }

    public void RegisterDamage(int logicalLayer, float damage)
    {
        if (logicalLayer < 0 || damage <= 0f)
        {
            return;
        }

        EnsureLayerExists(logicalLayer);
        damageByLayer[logicalLayer] += Mathf.Max(0, Mathf.RoundToInt(damage));
    }

    private void EnsureLayerExists(int logicalLayer)
    {
        while (damageByLayer.Count <= logicalLayer)
        {
            damageByLayer.Add(0);
        }
    }

    public int GetDamage(int logicalLayer)
    {
        return logicalLayer >= 0 && logicalLayer < damageByLayer.Count
            ? damageByLayer[logicalLayer]
            : 0;
    }

    private void EnsureCollisionShape()
    {
        CircleCollider2D collisionShape = GetComponent<CircleCollider2D>();
        if (collisionShape == null)
        {
            collisionShape = gameObject.AddComponent<CircleCollider2D>();
        }

        collisionShape.isTrigger = true;
        collisionShape.radius = Mathf.Max(0.5f, collisionShape.radius);
    }
}
