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
    private Text populationText;
    private int displayedPopulation = -1;
    private int displayedCarnivores = -1;
    private int displayedElapsedSeconds = -1;

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
        rect.sizeDelta = new Vector2(420f, 110f);
        populationText = obj.GetComponent<Text>();
        populationText.font = font;
        populationText.fontSize = 24;
        populationText.alignment = TextAnchor.MiddleLeft;
        populationText.color = Color.white;
        populationText.raycastTarget = false;
        obj.GetComponent<Outline>().effectColor = Color.black;
        UpdatePopulation();
    }

    private void LateUpdate()
    {
        if (populationText != null) UpdatePopulation();
    }

    private void UpdatePopulation()
    {
        int count = herbivoreManager != null ? herbivoreManager.LivingCount : 0;
        int carnivores = carnivoreManager != null ? carnivoreManager.LivingCount : 0;
        int elapsedSeconds = Mathf.FloorToInt(Time.timeSinceLevelLoad);
        if (count == displayedPopulation && carnivores == displayedCarnivores && elapsedSeconds == displayedElapsedSeconds) return;
        displayedPopulation = count;
        displayedCarnivores = carnivores;
        displayedElapsedSeconds = elapsedSeconds;
        populationText.text = $"経過時間：{elapsedSeconds / 3600:00}:{elapsedSeconds / 60 % 60:00}:{elapsedSeconds % 60:00}\n草食動物：{count}\n肉食動物：{carnivores}";
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
