using UnityEngine;

[DefaultExecutionOrder(10)]
[RequireComponent(typeof(AnimalBody))]
public sealed class HerbivoreFeeding : MonoBehaviour
{
    [SerializeField] private GrassField grassField;
    public const float ConsumptionPerSecond = 1f;
    private const float FeedingInterval = 1f;
    private float feedingCounter;
    private HerbivoreResources resources;
    private HerbivoreManager manager;
    private AnimalBody body;
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
        var definition = GetComponentInParent<SpeciesManager>()?.Definition ?? GetComponent<SpeciesIdentity>()?.Definition;
        resources = new HerbivoreResources(definition != null ? definition.initialHealth : HerbivoreResources.InitialHealth);
        body = GetComponent<AnimalBody>();
        body.Initialize(resources, true);
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
        if (dt <= 0f || !body.IsAlive || body.IsSleeping) return;
        if (feedingCounter > 0f)
            feedingCounter = Mathf.Max(0f, feedingCounter - dt);

        if (resources.IsDead)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        TryFeed();
        if (manager != null && TryGetComponent(out HerbivoreMovement movement))
            manager.TryReproduce(body, movement.MovementDirection);
    }

    public void TryFeed()
    {
        if (!isActiveAndEnabled || Time.deltaTime <= 0f || resources == null || resources.IsDead || grassField == null || body.IsSleeping || body.IsRunning) return;
        if (feedingCounter != 0f) return;
        float consumed = grassField.ConsumeGrass((Vector2)transform.position, ConsumptionPerSecond * FeedingInterval);
        if (consumed <= 0f) return;
        feedingCounter += consumed;
        resources.Feed(consumed);
        AfterFeeding?.Invoke(grassField);
    }

    private void OnDisable()
    {
        feedingCounter = 0f;
    }
}
