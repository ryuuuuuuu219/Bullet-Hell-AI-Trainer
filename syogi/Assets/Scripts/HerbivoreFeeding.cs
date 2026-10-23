using UnityEngine;

[DefaultExecutionOrder(10)]
public sealed class HerbivoreFeeding : MonoBehaviour
{
    [SerializeField] private GrassField grassField;
    public const float ConsumptionPerSecond = 1f;
    private const float FeedingInterval = 1f;
    private double elapsed;
    private HerbivoreResources resources;
    private HerbivoreManager manager;
    public double ResourceAmount => resources == null ? HerbivoreResources.Initial : resources.Amount;
    public GrassField Field => grassField;
    public event System.Action<GrassField> AfterFeeding;

    private void OnEnable()
    {
        // Each animal has its own phase, even when all are spawned in one frame.
        elapsed = Random.Range(0f, FeedingInterval - 0.001f);
    }

    private void Awake()
    {
        resources = new HerbivoreResources();
        manager = GetComponentInParent<HerbivoreManager>();
        if (grassField == null) grassField = FindAnyObjectByType<GrassField>();
        if (grassField == null)
        {
            Debug.LogWarning("HerbivoreFeeding requires a GrassField in the scene.", this);
        }
    }

    private void Update()
    {
        double remaining = Time.deltaTime;
        while (remaining > 0)
        {
            // Apply maintenance up to the next meal, preserving event order on long frames.
            double step = System.Math.Min(remaining, FeedingInterval - elapsed);
            resources.Maintain(step);
            remaining -= step;
            elapsed += step;
            if (resources.IsDead)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            if (elapsed < FeedingInterval) continue;
            elapsed = 0;
            if (grassField != null)
            {
                float consumed = grassField.ConsumeGrass((Vector2)transform.position, ConsumptionPerSecond * FeedingInterval);
                resources.Feed(consumed);
                AfterFeeding?.Invoke(grassField);
            }
            if (manager != null) manager.TryReproduce(transform.position, resources);
        }
    }

    private void OnDisable()
    {
        elapsed = 0f;
    }
}
