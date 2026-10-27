using TMPro;
using UnityEngine;

[RequireComponent(typeof(HerbivoreFeeding))]
public sealed class HerbivoreStatusUI : MonoBehaviour
{
    private const float VisibilityDistance = 500f;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Vector2 worldOffset = new Vector2(88f, 88f);
    private HerbivoreFeeding animal;
    private AnimalBody body;
    private bool shownSleeping;
    private Camera viewCamera;
    private RectTransform labelRect;
    private int shownHealth = -1;
    private float shownHealthLimit = -1;
    private int shownResource = -1;
    private int shownStamina = -1;
    private float nextTextRefresh;

    private void Awake()
    {
        animal = GetComponent<HerbivoreFeeding>();
        body = GetComponent<AnimalBody>();
        if (statusText == null)
        {
            Debug.LogError("Herbivore status text is not assigned.", this);
            enabled = false;
            return;
        }

        labelRect = statusText.rectTransform;
        Canvas canvas = statusText.GetComponent<Canvas>();
        if (canvas == null) canvas = statusText.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        statusText.raycastTarget = false;
        statusText.fontSize = 18f;
        statusText.enableAutoSizing = false;
        statusText.alignment = TextAlignmentOptions.BottomLeft;
        labelRect.pivot = Vector2.zero;
        labelRect.sizeDelta = new Vector2(160f, 48f);
    }

    private void LateUpdate()
    {
        if (viewCamera == null) viewCamera = Camera.main;
        // Ignore camera depth: the animals move on the XY plane.
        bool visible = viewCamera != null &&
            ((Vector2)(transform.position - viewCamera.transform.position)).sqrMagnitude <=
            VisibilityDistance * VisibilityDistance;
        bool becameVisible = visible && !statusText.enabled;
        if (statusText.enabled != visible) statusText.enabled = visible;
        if (!visible) return;

        // Keep the label upright and readable while the camera pans or zooms.
        labelRect.position = transform.position + (Vector3)worldOffset;
        labelRect.rotation = Quaternion.identity;
        if (viewCamera != null && viewCamera.orthographic && viewCamera.pixelHeight > 0)
            labelRect.localScale = Vector3.one * (2f * viewCamera.orthographicSize / viewCamera.pixelHeight);

        if (!becameVisible && Time.unscaledTime < nextTextRefresh) return;
        nextTextRefresh = Time.unscaledTime + 0.2f;
        int health = Mathf.RoundToInt((float)animal.Health * 10f);
        int resource = Mathf.RoundToInt((float)animal.ResourceAmount * 10f);
        int stamina = Mathf.RoundToInt((float)body.Resources.Stamina * 10f);
        float healthLimit = (float)body.Resources.HealthLimit;
        bool sleeping = body.IsSleeping;
        if (health == shownHealth && healthLimit == shownHealthLimit && resource == shownResource && stamina == shownStamina && sleeping == shownSleeping) return;
        shownHealth = health;
        shownHealthLimit = healthLimit;
        shownResource = resource;
        shownStamina = stamina;
        shownSleeping = sleeping;
        labelRect.sizeDelta = new Vector2(160f, sleeping ? 96f : 72f);
        statusText.SetText(sleeping
            ? "HP: {0:1}/{3:1}\n炭素: {1:1}/50\nSP: {2:1}/100\n休眠中zzz・・・"
            : "HP: {0:1}/{3:1}\n炭素: {1:1}/50\nSP: {2:1}/100", health / 10f, resource / 10f, stamina / 10f, healthLimit);
    }
}
