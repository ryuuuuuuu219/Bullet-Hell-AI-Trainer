using TMPro;
using UnityEngine;

[RequireComponent(typeof(CarnivoreAnimal))]
public sealed class CarnivoreStatusUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Vector2 worldOffset = new Vector2(88f, 88f);
    private CarnivoreAnimal animal;
    private Camera viewCamera;
    private RectTransform labelRect;
    private int shownHealth = -1;
    private int shownResource = -1;

    private void Awake()
    {
        animal = GetComponent<CarnivoreAnimal>();
        if (statusText == null)
        {
            Debug.LogError("Carnivore status text is not assigned.", this);
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
        // Keep the label upright and readable while the camera pans or zooms.
        labelRect.position = transform.position + (Vector3)worldOffset;
        labelRect.rotation = Quaternion.identity;
        if (viewCamera != null && viewCamera.orthographic && viewCamera.pixelHeight > 0)
            labelRect.localScale = Vector3.one * (2f * viewCamera.orthographicSize / viewCamera.pixelHeight);

        int health = Mathf.RoundToInt((float)animal.Resources.Health * 10f);
        int resource = Mathf.RoundToInt((float)animal.Resources.Amount * 10f);
        if (health == shownHealth && resource == shownResource) return;
        shownHealth = health;
        shownResource = resource;
        statusText.SetText("HP: {0:1}/100\n資源: {1:1}/50", health / 10f, resource / 10f);
    }
}
