using UnityEngine;

public sealed class GrassField : MonoBehaviour
{
    public const int FieldSize = 10000;
    public const int CellSize = 100;
    public const int CellCount = FieldSize / CellSize;
    [SerializeField, Min(0)] private int initialSpawnCount = 150;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private int seed = 1;
    [SerializeField, Min(0.01f)] private float updateInterval = 15f;
    [SerializeField, Min(1)] private float maxGrass = 3f;
    [SerializeField, Tooltip("枯死後の草量（0以下）。負の間は更新ごとに+1し、0で止まります。")]
    private float deathAmount = -1f;
    [SerializeField, Min(0)] private float increaseAmount = 1f;
    [SerializeField, Range(0, 8)] private int birthNeighborMinimum = 1;
    [SerializeField, Range(0, 8)] private int birthNeighborMaximum = 1;
    [SerializeField] private bool growthEnabled = true;
    [SerializeField] private bool wrapBoundary;
    public GrassGrid Grid { get; private set; }
    public int ActualSeed { get; private set; }
    private float elapsed;
    private Texture2D texture;
    private Sprite sprite;
    private GameObject display;
    private Color32[] pixels;
    private bool displayDirty;

    /// <summary>
    /// Consumes grass at continuous world XY coordinates (Unity main thread).
    /// Returns the actual amount removed. Outside [-5000, 5000), empty cells,
    /// non-positive requests and calls before Awake return zero.
    /// </summary>
    public float ConsumeGrass(Vector2 worldPosition, float requestedAmount)
    {
        if (Grid == null) return 0;
        float consumed = Grid.ConsumeAtWorldPosition(worldPosition.x, worldPosition.y, CellSize, requestedAmount);
        if (consumed > 0) displayDirty = true;
        return consumed;
    }

    private void LateUpdate()
    {
        // Batch consumption by multiple animals into one texture upload.
        if (displayDirty) RefreshDisplay();
    }

    private void Awake()
    {
        ActualSeed = randomizeSeed ? System.Environment.TickCount : seed;
        Grid = new GrassGrid(CellCount, CellCount, Mathf.Clamp(initialSpawnCount, 0, CellCount * CellCount), ActualSeed);
        texture = new Texture2D(CellCount, CellCount, TextureFormat.RGBA32, false);
        texture.name = "Grass Amounts";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        pixels = new Color32[CellCount * CellCount];
        sprite = Sprite.Create(texture, new Rect(0, 0, CellCount, CellCount), new Vector2(0.5f, 0.5f), 1f / CellSize);
        display = new GameObject("Grass Cells");
        display.transform.SetParent(transform, false);
        display.transform.position = Vector3.zero;
        var renderer = display.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = -10;
        RefreshDisplay();
    }

    private void Update()
    {
        // Independent simulation clock; pausing game time also pauses grass.
        elapsed += Time.deltaTime;
        float interval = Mathf.Max(0.01f, updateInterval);
        if (elapsed < interval) return;
        while (elapsed >= interval)
        {
            elapsed -= interval;
            Grid.Step(Mathf.Max(1, maxGrass), Mathf.Max(0, increaseAmount),
                Mathf.Clamp(birthNeighborMinimum, 0, 8), growthEnabled, wrapBoundary,
                Mathf.Clamp(birthNeighborMaximum, Mathf.Clamp(birthNeighborMinimum, 0, 8), 8),
                float.IsNaN(deathAmount) || float.IsInfinity(deathAmount) ? -1f : Mathf.Min(0f, deathAmount));
        }
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        for (int y = 0; y < CellCount; y++)
        for (int x = 0; x < CellCount; x++)
        {
            float amount = Grid[x, y];
            pixels[y * CellCount + x] = amount <= 0 ? new Color32(0, 0, 0, 0)
                : (Color32)Color.Lerp(new Color(0.05f, 0.25f, 0.06f), new Color(0.25f, 0.85f, 0.15f),
                    Mathf.Clamp01((amount - 1f) / Mathf.Max(1, maxGrass - 1)));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        displayDirty = false;
    }

    private void OnDestroy()
    {
        if (display != null) Destroy(display);
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}
