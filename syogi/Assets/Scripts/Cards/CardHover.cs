using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(1f)] private float hoverScale = 1.3f;
    private Vector3 originalScale;
    private int originalSiblingIndex;
    private Transform originalParent;
    private bool hovering;
    private bool pointerInside;

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (hovering) return;
        originalScale = transform.localScale;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalParent = transform.parent;
        hovering = true;
        // Leave the position untouched, including when selected cards are rearranged.
        transform.localScale = originalScale * hoverScale;
        transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
            Restore();
    }

    private void Update()
    {
        if (hovering && !pointerInside &&
            (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            Restore();
    }

    private void OnDisable()
    {
        pointerInside = false;
        Restore();
    }

    private void Restore()
    {
        if (!hovering) return;
        hovering = false;
        transform.localScale = originalScale;
        if (transform.parent == originalParent)
            transform.SetSiblingIndex(originalSiblingIndex);
    }
}
