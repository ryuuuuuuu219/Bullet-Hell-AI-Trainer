using UnityEngine;

public sealed class bullet : MonoBehaviour
{
    [SerializeField] private Vector2 vector;
    [SerializeField, Min(0)] private int threatLevel = 1;
    [SerializeField, Min(0)] private int logicalLayer;

    private Rigidbody2D body;

    public Vector2 Vector => vector;
    public int ThreatLevel => threatLevel;
    public int LogicalLayer => logicalLayer;
    public BulletStructure Structure { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            vector = body.linearVelocity;
        }
    }

    private void OnEnable()
    {
        BulletManager.Register(this);
    }

    private void FixedUpdate()
    {
        if (body != null)
        {
            vector = body.linearVelocity;
        }
    }

    private void OnDisable()
    {
        BulletManager.Unregister(this);
    }

    public void SetData(Vector2 movementVector, int threat)
    {
        SetData(movementVector, threat, logicalLayer);
    }

    public void SetData(Vector2 movementVector, int threat, int layer)
    {
        SetData(
            movementVector,
            BulletStructure.Straight(movementVector.magnitude, threat),
            layer);
    }

    public void SetData(
        Vector2 movementVector,
        BulletStructure structure,
        int layer)
    {
        bool refreshRegistration = isActiveAndEnabled &&
                                   logicalLayer != Mathf.Max(0, layer);
        if (refreshRegistration)
        {
            BulletManager.Unregister(this);
        }

        gameObject.layer = ProjectileCollisionLayers.EnemyBullet;
        vector = movementVector;
        Structure = structure ?? BulletStructure.Straight(
            movementVector.magnitude,
            threatLevel);
        threatLevel = Structure.ThreatLevel;
        logicalLayer = Mathf.Max(0, layer);
        LogicalLayerVisibility.Apply(gameObject, logicalLayer);

        if (refreshRegistration)
        {
            BulletManager.Register(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerAgent player = other.GetComponent<PlayerAgent>();
        if (player == null || player.LogicalLayer != logicalLayer)
        {
            return;
        }

        player.RegisterHit(this);
        ProjectilePool.Release(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        threatLevel = Mathf.Max(0, threatLevel);
        logicalLayer = Mathf.Max(0, logicalLayer);
    }
#endif
}
