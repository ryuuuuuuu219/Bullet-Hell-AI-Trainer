using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class PlayerBullet : MonoBehaviour
{
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0f)] private float damageDecayPerSecond = 0.1f;
    [SerializeField, Min(0)] private int logicalLayer;

    public float Damage => damage;
    public int LogicalLayer => logicalLayer;

    public void Initialize(Vector2 velocity, int layer)
    {
        damage = 1f;
        damageDecayPerSecond = 0.1f;
        logicalLayer = Mathf.Max(0, layer);

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearVelocity = velocity;
    }

    private void Update()
    {
        damage = Mathf.Max(0f, damage - damageDecayPerSecond * Time.deltaTime);
        if (damage <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Boss boss = other.GetComponentInParent<Boss>();
        if (boss == null)
        {
            return;
        }

        boss.RegisterDamage(logicalLayer, damage);
        Destroy(gameObject);
    }
}
