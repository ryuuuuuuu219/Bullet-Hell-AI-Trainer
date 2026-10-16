using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class player : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 500f;
    LineRenderer lineRenderer;
    public float size = 30f;
    [SerializeField] private BarrageSettings barrage = new BarrageSettings();
    [SerializeField] private bool autoFire = true;
    private float fireTimer;

    private void Awake()
    {
        barrage = (barrage ?? new BarrageSettings()).ValidatedCopy();
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, new Vector3(-1f, 0.6f, 0f)*size);
        lineRenderer.SetPosition(1, new Vector3(1f, 0.6f, 0f)*size);
        lineRenderer.SetPosition(2, new Vector3(0f, -1.3f, 0f)*size);
        lineRenderer.loop = true;
        lineRenderer.startWidth = 30f;
        lineRenderer.endWidth = 30f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
        lineRenderer.useWorldSpace = false;
    }


    void Update()
    {
        if (autoFire)
        {
            float interval = Mathf.Max(0.01f, barrage.fireInterval);
            fireTimer += Time.deltaTime;
            while (fireTimer >= interval)
            {
                fireTimer -= interval;
                FireBullet();
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2 direction = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            direction.y += 1f;
        }
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            direction.y -= 1f;
        }
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            direction.x -= 1f;
        }
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            direction.x += 1f;
        }

        Vector2 movement = Vector2.ClampMagnitude(direction, 1f) * moveSpeed * Time.deltaTime;
        transform.position += new Vector3(movement.x, movement.y, 0f);
    }

    // Applies to future shots; already-fired bullets retain their settings.
    public void ConfigureBarrage(BarrageSettings settings)
    {
        barrage = (settings ?? new BarrageSettings()).ValidatedCopy();
        fireTimer = 0f;
    }

    public void SetAutoFire(bool enabled)
    {
        autoFire = enabled;
        fireTimer = 0f;
    }

    // Emits one volley, including when automatic firing is disabled.
    public void FireBullet()
    {
        if (lineRenderer == null) return;
        BarrageSettings shot = (barrage ?? new BarrageSettings()).ValidatedCopy();
        Vector3 origin = transform.position + Vector3.down * (1.3f * size);
        Vector3 centerDirection = new Vector3(shot.direction.x, shot.direction.y, 0f);
        bool fullCircle = Mathf.Approximately(shot.spreadAngle, 360f);
        float angleStep = shot.bulletCount <= 1 ? 0f
            : shot.spreadAngle / (fullCircle ? shot.bulletCount : shot.bulletCount - 1);

        for (int i = 0; i < shot.bulletCount; i++)
        {
            float angle = shot.bulletCount == 1 ? 0f : -shot.spreadAngle * 0.5f + angleStep * i;
            Vector3 direction = Quaternion.Euler(0f, 0f, angle) * centerDirection;
            shot.direction = new Vector2(direction.x, direction.y);
            GameObject bulletObject = new GameObject("PlayerBullet");
            bulletObject.transform.position = origin;
            Bullet bullet = bulletObject.AddComponent<Bullet>();
            bullet.Initialize(lineRenderer.sharedMaterial, shot);
        }
    }
}
