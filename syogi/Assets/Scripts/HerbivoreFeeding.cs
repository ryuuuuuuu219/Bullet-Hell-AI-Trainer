using UnityEngine;

[DefaultExecutionOrder(10)]
public sealed class HerbivoreFeeding : MonoBehaviour
{
    [SerializeField] private GrassField grassField;
    public const float ConsumptionPerSecond = 1f;
    private const float FeedingInterval = 1f;
    private float feedingCounter;
    private HerbivoreResources resources;
    private HerbivoreManager manager;
    public double ResourceAmount => resources == null ? HerbivoreResources.Initial : resources.Amount;
    public double Health => resources == null ? HerbivoreResources.InitialHealth : resources.Health;
    public GrassField Field => grassField;
    public event System.Action<GrassField> AfterFeeding;

    private void OnEnable()
    {
        feedingCounter = 0f;
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
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (feedingCounter > 0f)
            feedingCounter = Mathf.Max(0f, feedingCounter - dt);

        resources.Maintain(dt);
        if (resources.IsDead)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        TryFeed();
    }

    public void TryFeed()
    {
        if (!isActiveAndEnabled || Time.deltaTime <= 0f || resources == null || resources.IsDead || grassField == null) return;
        if (feedingCounter != 0f) return;
        float consumed = grassField.ConsumeGrass((Vector2)transform.position, ConsumptionPerSecond * FeedingInterval);
        if (consumed <= 0f) return;
        feedingCounter += consumed;
        resources.Feed(consumed);
        AfterFeeding?.Invoke(grassField);
        if (manager != null && TryGetComponent(out HerbivoreMovement movement))
            manager.TryReproduce(transform.position, resources, movement.MovementDirection);
    }

    private void OnDisable()
    {
        feedingCounter = 0f;
    }
}
