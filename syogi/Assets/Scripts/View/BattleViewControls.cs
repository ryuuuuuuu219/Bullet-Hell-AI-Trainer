using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class BattleViewControls : MonoBehaviour
{
    [SerializeField] private SceneNavigation sceneNavigation;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private HerbivoreManager herbivoreManager;
    [SerializeField] private CarnivoreManager carnivoreManager;
    [SerializeField] private Font populationFont;
    [SerializeField, Min(1f)] private float minimumSize = 50f;
    [SerializeField, Min(1f)] private float maximumSize = 2000f;
    [SerializeField, Min(1.01f)] private float zoomFactor = 1.25f;

    private Button zoomInButton;
    private Button zoomOutButton;
    private static readonly float[] SimulationSpeeds = { 1f, 2f, 4f, 8f };
    private int simulationSpeedIndex;
    private Text simulationSpeedText;
    private Text populationText;
    private float nextPopulationRefresh;

    [TextArea(8, 30)]
    public string eventlog = string.Empty;
    [Min(0.1f)] public float loggingInterval = 15f;
    public float loggingTimer = 0f;
    private GrassField loggingGrassField;
    private bool logHeaderWritten;
    private float loggingRealElapsed;

    private readonly System.Collections.Generic.List<SpeciesManager> loggedSpecies =
        new System.Collections.Generic.List<SpeciesManager>();
    private void OnEnable()
    {
        ResetSimulationSpeed();
        BattleSimulationSpeed.Extinction += OnSpeciesExtinct;
    }
    private void OnSpeciesExtinct(string id)
    {
        eventlog += $"# extinction species={id} simulation_s={Time.timeSinceLevelLoad.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} speed=0.1\n";
        if (simulationSpeedText != null) simulationSpeedText.text = "Speed x0.1";
    }

    // Time.timeScale is global: do not carry battle acceleration into other scenes.
    private void OnDisable()
    {
        BattleSimulationSpeed.Extinction -= OnSpeciesExtinct;
        ResetSimulationSpeed();
    }

    private void ResetSimulationSpeed()
    {
        simulationSpeedIndex = 0;
        ApplySimulationSpeed();
    }

    private void CycleSimulationSpeed()
    {
        simulationSpeedIndex = BattleSimulationSpeed.ExtinctionOverride ? 0
            : (simulationSpeedIndex + 1) % SimulationSpeeds.Length;
        ApplySimulationSpeed();
    }

    private void ApplySimulationSpeed()
    {
        float speed = SimulationSpeeds[simulationSpeedIndex];
        BattleSimulationSpeed.SetUserSpeed(speed);
        if (simulationSpeedText != null) simulationSpeedText.text = $"Speed x{speed:0}";
    }

    private void Start()
    {
        if (sceneNavigation == null || targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogError("Battle view controls require scene navigation and an orthographic camera.", this);
            return;
        }

        if (EventSystem.current == null)
        {
            var events = new GameObject("Battle EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }

        var canvasObject = new GameObject("Battle Controls", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Button menu = CreateButton(canvasObject.transform, "Menu", 16f, 112f, font);
        menu.onClick.AddListener(sceneNavigation.ReturnToMainMenu);
        zoomInButton = CreateButton(canvasObject.transform, "+", 136f, 48f, font);
        zoomOutButton = CreateButton(canvasObject.transform, "-", 192f, 48f, font);
        zoomInButton.onClick.AddListener(() => Zoom(1f / zoomFactor));
        zoomOutButton.onClick.AddListener(() => Zoom(zoomFactor));
        Button speedButton = CreateButton(canvasObject.transform, "Speed x1", 256f, 160f, font);
        simulationSpeedText = speedButton.GetComponentInChildren<Text>();
        speedButton.onClick.AddListener(CycleSimulationSpeed);
        ApplySimulationSpeed();
        RefreshButtons();
        CreatePopulationText(canvasObject.transform, populationFont != null ? populationFont : font);
    }

    private void CreatePopulationText(Transform parent, Font font)
    {
        var obj = new GameObject("Population", typeof(RectTransform), typeof(Text), typeof(Outline));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(16f, -72f);
        rect.sizeDelta = new Vector2(560f, 500f);
        populationText = obj.GetComponent<Text>();
        populationText.font = font;
        populationText.fontSize = 24;
        populationText.alignment = TextAnchor.UpperLeft;
        populationText.color = Color.white;
        populationText.raycastTarget = false;
        obj.GetComponent<Outline>().effectColor = Color.black;
        populationText.text = UpdatePopulation();
    }

    private void LateUpdate()
    {
        if (populationText != null && Time.unscaledTime >= nextPopulationRefresh)
        {
            nextPopulationRefresh = Time.unscaledTime + 0.2f;
            populationText.text = UpdatePopulation();
        }
        loggingRealElapsed += Time.unscaledDeltaTime;
        if (Time.deltaTime <= 0f) return;
        float interval = float.IsNaN(loggingInterval) || float.IsInfinity(loggingInterval)
            ? 15f : Mathf.Max(0.1f, loggingInterval);
        if (float.IsNaN(loggingTimer) || float.IsInfinity(loggingTimer)) loggingTimer = 0f;
        loggingTimer -= Time.deltaTime;
        if (loggingTimer <= 0f)
        {
            // Record the actual state once, not invented intermediate samples after a long frame.
            loggingTimer = interval - ((-loggingTimer) % interval);
            AppendPopulationLog();
        }
    }

    private void AppendPopulationLog()
    {
        if (loggingGrassField == null) loggingGrassField = FindAnyObjectByType<GrassField>();
        if (!logHeaderWritten)
        {
            loggedSpecies.Clear();
            loggedSpecies.AddRange(SpeciesManager.Active);
            loggedSpecies.Sort((a, b) => string.CompareOrdinal(a.SpeciesId, b.SpeciesId));
            string extraHeader = "";
            foreach (var manager in loggedSpecies)
            {
                // Quote IDs so commas and quotes remain valid CSV header text.
                string id = manager.SpeciesId.Replace("\"", "\"\"");
                extraHeader += $",\"{id}_count\",\"{id}_carbon\"";
            }
            eventlog += "\n--- 生態系観測ログ（時間は秒、NAは参照なし） ---\n"
                + $"seed 草={ (loggingGrassField != null ? loggingGrassField.ActualSeed.ToString() : "NA") }"
                + $" 草食={ (herbivoreManager != null ? herbivoreManager.ActualSeed.ToString() : "NA") }"
                + $" 肉食={ (carnivoreManager != null ? carnivoreManager.ActualSeed.ToString() : "NA") }\n"
                + "simulation_s,real_s,speed,grass_cells,grass_total,recovering_cells,empty_cells,herbivores,herbivore_food,carnivores,carnivore_food" + extraHeader + "\n";
            foreach (var manager in loggedSpecies)
                eventlog += $"# species={manager.SpeciesId} seed={manager.ActualSeed}\n";
            logHeaderWritten = true;
        }

        int livingGrass = 0, recovering = 0, empty = 0;
        double grassTotal = 0;
        GrassGrid grid = loggingGrassField != null ? loggingGrassField.Grid : null;
        if (grid != null)
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                float amount = grid[x, y];
                if (amount > 0) { livingGrass++; grassTotal += amount; }
                else if (amount < 0) recovering++;
                else empty++;
            }
        }

        int herbivores = 0, carnivores = 0;
        double herbivoreFood = 0, carnivoreFood = 0;
        foreach (AnimalBody body in AnimalBody.Active)
        {
            if (body == null || !body.IsAlive) continue;
            if (body.IsHerbivore) { herbivores++; herbivoreFood += body.Resources.Amount; }
            else { carnivores++; carnivoreFood += body.Resources.Amount; }
        }
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string grassValues = grid == null ? "NA,NA,NA,NA"
            : $"{livingGrass},{grassTotal.ToString("F3", culture)},{recovering},{empty}";
        string speciesValues = "";
        foreach (var manager in loggedSpecies)
        {
            int count = 0;
            double carbon = 0;
            if (manager != null && manager.Population != null)
                foreach (var body in manager.Population.Individuals)
                    if (body != null && body.IsAlive) { count++; carbon += body.Resources.Amount; }
            speciesValues += $",{count},{carbon.ToString("F3", culture)}";
        }
        eventlog += string.Format(culture, "{0:F3},{1:F3},{2:0.###},{3},{4},{5:F3},{6},{7:F3}",
            Time.timeSinceLevelLoad, loggingRealElapsed, Time.timeScale, grassValues,
            herbivores, herbivoreFood, carnivores, carnivoreFood) + speciesValues + "\n";
    }

    private string UpdatePopulation()
    {
        int elapsedSeconds = Mathf.FloorToInt(Time.timeSinceLevelLoad);
        string text = $"経過時間：{elapsedSeconds / 3600:00}:{elapsedSeconds / 60 % 60:00}:{elapsedSeconds % 60:00}";
        foreach (var manager in SpeciesManager.Active)
            if (manager != null && manager.Definition != null)
                text += $"\n{manager.Definition.displayName}：{manager.LivingCount}";
        return text;
    }

    private static Button CreateButton(Transform parent, string label, float left, float width, Font font)
    {
        var obj = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -16f);
        rect.sizeDelta = new Vector2(width, 48f);
        Image background = obj.GetComponent<Image>();
        background.color = new Color(0.12f, 0.16f, 0.21f, 0.95f);
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = background;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(obj.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }

    private void Zoom(float multiplier)
    {
        targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize * multiplier,
            minimumSize, maximumSize);
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        zoomInButton.interactable = targetCamera.orthographicSize > minimumSize;
        zoomOutButton.interactable = targetCamera.orthographicSize < maximumSize;
    }

    private void OnValidate()
    {
        minimumSize = Mathf.Max(1f, minimumSize);
        maximumSize = Mathf.Max(minimumSize, maximumSize);
        zoomFactor = Mathf.Max(1.01f, zoomFactor);
    }
}
