using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VerticalTextScroll : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float padding = 12f;
    [SerializeField] private float scrollSensitivity = 30f;

    private void Awake()
    {
        if (text == null) return;

        var viewportObject = new GameObject("TextViewport", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D), typeof(TextScrollRect));
        viewportObject.layer = gameObject.layer;
        RectTransform viewport = (RectTransform)viewportObject.transform;
        viewport.SetParent(transform, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(padding, padding);
        viewport.offsetMax = new Vector2(-padding, -padding);
        viewportObject.GetComponent<Image>().color = Color.clear;

        RectTransform content = text.rectTransform;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition3D = Vector3.zero;
        content.sizeDelta = Vector2.zero;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        text.margin = Vector4.zero;
        text.raycastTarget = true;
        ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextScrollRect scroll = viewportObject.GetComponent<TextScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = scrollSensitivity;
    }
}

// Nested cards retain horizontal dragging of the surrounding card list.
public sealed class TextScrollRect : ScrollRect
{
    private ScrollRect horizontalParent;
    private bool routeToParent;

    public override void OnInitializePotentialDrag(PointerEventData eventData)
    {
        base.OnInitializePotentialDrag(eventData);
        horizontalParent = transform.parent != null
            ? transform.parent.GetComponentInParent<ScrollRect>() : null;
        if (horizontalParent != null) horizontalParent.OnInitializePotentialDrag(eventData);
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        routeToParent = horizontalParent != null && horizontalParent.horizontal &&
            Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y);
        if (routeToParent) horizontalParent.OnBeginDrag(eventData);
        else base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (routeToParent && horizontalParent != null) horizontalParent.OnDrag(eventData);
        else base.OnDrag(eventData);
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        if (routeToParent && horizontalParent != null) horizontalParent.OnEndDrag(eventData);
        else base.OnEndDrag(eventData);
        routeToParent = false;
    }
}
