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
    [SerializeField] private CardCatalog cardCatalog;
    [SerializeField] private Transform aimTarget;
    private BarrageDefinition definition;
    private bool firingValid;
    private Vector2 movementDirection = Vector2.down;

    private void Awake()
    {
        if (cardCatalog != null)
        {
            firingValid = BarrageComposer.TryCompose(cardCatalog.initialSettings, cardCatalog.parameters,
                CardSelectionSession.Restore(cardCatalog), out definition, out string error);
            if (!firingValid) Debug.LogError(error, this);
        }
        else
        {
            barrage = (barrage ?? new BarrageSettings()).ValidatedCopy();
            firingValid = barrage != null;
        }
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
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            TickFire();
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
        if (direction.sqrMagnitude > 0) movementDirection = direction.normalized;
        TickFire();
    }

    private void TickFire()
    {
        if (!autoFire || !firingValid) return;
        float interval = definition != null ? definition.interval : barrage.fireInterval;
        fireTimer += Time.deltaTime;
        while (fireTimer >= interval && firingValid)
        {
            float remaining = fireTimer - interval;
            if (remaining == fireTimer)
            {
                Debug.LogError("FireInterval is too small for the current timer precision. Firing cancelled.", this);
                firingValid = false;
                break;
            }
            fireTimer = remaining;
            FireBullet();
        }
    }

    // Applies to future shots; already-fired bullets retain their settings.
    public void ConfigureBarrage(BarrageSettings settings)
    {
        barrage = (settings ?? new BarrageSettings()).ValidatedCopy();
        definition = null;
        firingValid = barrage != null;
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
        if (lineRenderer == null || !firingValid) return;
        if (definition != null) { FireFormation(); return; }
        BarrageSettings shot = (barrage ?? new BarrageSettings()).ValidatedCopy();
        if (shot == null) { firingValid = false; return; }
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

    private void FireFormation()
    {
        Vector3 origin = transform.position + Vector3.down * (1.3f * size);
        Vector2 forward = movementDirection;
        if (definition.basis == CardDirectionBasis.Aim)
        {
            if (aimTarget == null)
            {
                Debug.LogError("Assign the barrage aim target. Firing cancelled.", this);
                firingValid = false;
                return;
            }
            forward = (Vector2)(aimTarget.position - origin);
        }
        if (forward.sqrMagnitude <= 0 || !BarrageComposer.IsFinite(forward.sqrMagnitude))
        {
            Debug.LogError("Invalid barrage direction. Firing cancelled.", this);
            firingValid = false;
            return;
        }
        forward.Normalize();
        for (int i = 0; i < definition.count; i++)
        {
            BarrageProjectile data = definition.GetProjectile(i);
            Vector3 heading = Quaternion.Euler(0, 0, data.angle) * (Vector3)forward;
            var shot = new BarrageSettings
            {
                fireInterval = definition.interval, speed = data.speed,
                direction = heading, angularSpeed = data.angularSpeed,
                radius = definition.radius, lifetime = definition.lifetime,
                useLinearPower = true, powerSlope = data.powerSlope, powerConstant = data.powerConstant
            };
            GameObject bulletObject = new GameObject("PlayerBullet");
            bulletObject.transform.position = origin;
            bulletObject.AddComponent<Bullet>().Initialize(lineRenderer.sharedMaterial, shot);
        }
    }
}
