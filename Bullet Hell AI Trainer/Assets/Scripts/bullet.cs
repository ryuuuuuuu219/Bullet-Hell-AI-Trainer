using UnityEngine;

public sealed class bullet : MonoBehaviour
{
    [SerializeField] private Vector2 vector;
    [SerializeField, Min(0)] private int threatLevel = 1;

    private Rigidbody2D body;

    public Vector2 Vector => vector;
    public int ThreatLevel => threatLevel;

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
        vector = movementVector;
        threatLevel = Mathf.Max(0, threat);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        threatLevel = Mathf.Max(0, threatLevel);
    }
#endif
}
