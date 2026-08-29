using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Aidata))]
public sealed class PlayerMovementController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    private Aidata aiData;
    private PlayerAgent playerAgent;
    private Rigidbody2D body;

    private void Awake()
    {
        aiData = GetComponent<Aidata>();
        playerAgent = GetComponent<PlayerAgent>();
        body = GetComponent<Rigidbody2D>();

        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void FixedUpdate()
    {
        if (playerAgent != null && playerAgent.IsHit)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 movementOutput = Vector2.ClampMagnitude(aiData.output(), 1f);
        body.linearVelocity = movementOutput * moveSpeed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
    }
#endif
}
