using UnityEngine;

[System.Serializable]
public class BarrageSettings
{
    [Min(0.01f)] public float fireInterval = 0.2f;
    [Range(1, 360)] public int bulletCount = 1;
    [Range(0f, 360f)] public float spreadAngle = 0f;
    // World XY direction. Positive angular speed rotates counterclockwise.
    public Vector2 direction = Vector2.down;
    [Min(0f)] public float speed = 50f;
    public float angularSpeed = 0f;
    [Range(3, 128)] public int sides = 32;
    [Min(0.01f)] public float radius = 5f;
    [Min(0.01f)] public float lineWidth = 5f;
    [Min(0.01f)] public float lifetime = 10f;
    public AnimationCurve powerCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public bool useLinearPower;
    public float powerSlope;
    public float powerConstant = 1f;

    public BarrageSettings ValidatedCopy()
    {
        if (!BarrageComposer.IsFinite(fireInterval) || fireInterval <= 0 || bulletCount <= 0 ||
            !BarrageComposer.IsFinite(speed) || speed <= 0 ||
            !BarrageComposer.IsFinite(lifetime) || lifetime <= 0 ||
            !BarrageComposer.IsFinite(radius) || radius <= 0 ||
            !BarrageComposer.IsFinite(lineWidth) || lineWidth <= 0 || sides < 3 ||
            !BarrageComposer.IsFinite(spreadAngle) || spreadAngle < 0 || spreadAngle > 360 ||
            !BarrageComposer.IsFinite(angularSpeed) ||
            !BarrageComposer.IsFinite(direction.x) || !BarrageComposer.IsFinite(direction.y) ||
            direction.sqrMagnitude <= 0 || !BarrageComposer.IsFinite(direction.sqrMagnitude) ||
            !BarrageComposer.IsFinite(powerSlope) || !BarrageComposer.IsFinite(powerConstant))
        {
            Debug.LogError("Invalid barrage settings. Positive interval, speed, lifetime and size are required; firing cancelled.");
            return null;
        }
        BarrageSettings copy = (BarrageSettings)MemberwiseClone();
        copy.direction = direction.normalized;
        copy.powerCurve = powerCurve == null
            ? AnimationCurve.EaseInOut(0f, 1f, 1f, 0f)
            : new AnimationCurve(powerCurve.keys)
            {
                preWrapMode = powerCurve.preWrapMode,
                postWrapMode = powerCurve.postWrapMode
            };
        return copy;
    }
}

[RequireComponent(typeof(LineRenderer))]
public class Bullet : MonoBehaviour
{
    private float speed = 50f;
    private float lifetime = 10f;
    private float elapsed;
    private float angularSpeed;
    private Vector3 direction = Vector3.down;
    private LineRenderer outline;
    private bool initialized;
    private float power = 1f;
    private bool useLinearPower;
    private float powerSlope, powerConstant;
    public float Power => power;

    public AnimationCurve powerCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    Color color => Color.HSVToRGB(Mathf.Clamp01(power) * 0.8333333f, 1f, 1f);

    // Preserve the original initialization API.
    public void Initialize(Material material)
    {
        Initialize(material, new BarrageSettings { powerCurve = this.powerCurve });
    }

    public void Initialize(Material material, BarrageSettings settings)
    {
        BarrageSettings config = (settings ?? new BarrageSettings()).ValidatedCopy();
        if (config == null) { Destroy(gameObject); return; }
        speed = config.speed;
        lifetime = config.lifetime;
        angularSpeed = config.angularSpeed;
        direction = new Vector3(config.direction.x, config.direction.y, 0f);
        powerCurve = config.powerCurve;
        useLinearPower = config.useLinearPower;
        powerSlope = config.powerSlope;
        powerConstant = config.powerConstant;
        elapsed = 0f;

        outline = GetComponent<LineRenderer>();
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.positionCount = config.sides;
        outline.startWidth = config.lineWidth;
        outline.endWidth = config.lineWidth;
        outline.sharedMaterial = material;

        for (int i = 0; i < config.sides; i++)
        {
            float angle = i * Mathf.PI * 2f / config.sides;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * config.radius);
        }

        initialized = true;
        SetColor();
    }

    private void SetColor()
    {
        power = useLinearPower ? powerSlope * elapsed + powerConstant : powerCurve.Evaluate(elapsed);
        outline.startColor = color;
        outline.endColor = color;
    }

    void Update()
    {
        if (!initialized) return;

        float step = Mathf.Min(Time.deltaTime, lifetime - elapsed);
        direction = Quaternion.Euler(0f, 0f, angularSpeed * step) * direction;
        transform.position += direction * speed * step;
        elapsed += step;
        SetColor();
        if (elapsed >= lifetime) Destroy(gameObject);
    }
}
