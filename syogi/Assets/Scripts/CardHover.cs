using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class CardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Min(1f)] private float hoverScale = 1.3f;
    private Vector3 originalScale;
    private int originalSiblingIndex;
    private Transform originalParent;
    private bool hovering;

    public void OnPointerEnter(PointerEventData eventData)
    {
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
        Restore();
    }

    private void OnDisable()
    {
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
