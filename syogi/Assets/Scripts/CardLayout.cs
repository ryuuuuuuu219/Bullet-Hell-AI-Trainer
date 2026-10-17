using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardLayout : MonoBehaviour
{
    [SerializeField] private CardCatalog catalog;
    [SerializeField] private RectTransform cardPrefab;
    [SerializeField] private RectTransform cardParent;
    [SerializeField] private float minX = -650f;
    [SerializeField] private float maxX = 800f;
    [SerializeField] private float y = -600f;
    [SerializeField] private ScrollRect selectionScroll;
    [SerializeField] private TMP_Text selectionCountLabel;
    [SerializeField] private float selectedSpacing = 20f;
    public const int MaxSelectedCards = 5;

    private readonly List<RectTransform> instances = new List<RectTransform>();
    private readonly List<CardData> selectedCards = new List<CardData>();
    private readonly List<RectTransform> selectedViews = new List<RectTransform>();
    public IReadOnlyList<CardData> SelectedCards => selectedCards;

    public void ToggleCard(CardData card)
    {
        int index = selectedCards.IndexOf(card);
        if (index >= 0) RemoveCard(index);
        else AddCard(card);
    }

    public bool AddCard(CardData card)
    {
        if (card == null || selectedCards.Contains(card) || selectedCards.Count >= MaxSelectedCards ||
            cardPrefab == null || selectionScroll == null || selectionScroll.content == null)
            return false;

        RectTransform view = Instantiate(cardPrefab, selectionScroll.content, false);
        selectedCards.Add(card);
        selectedViews.Add(view);
        view.name = "Selected_" + card.displayName;
        TMP_Text label = view.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = card.displayName;
        Button button = view.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
        }
        view.gameObject.SetActive(true);
        ArrangeSelectedCards();
        return true;
    }

    public void RemoveCard(int index)
    {
        if (index < 0 || index >= selectedCards.Count) return;
        RectTransform view = selectedViews[index];
        selectedCards.RemoveAt(index);
        selectedViews.RemoveAt(index);
        if (view != null)
        {
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }
        ArrangeSelectedCards();
    }

    private void ArrangeSelectedCards()
    {
        RefreshSelectionCount();
        if (selectionScroll == null || selectionScroll.content == null) return;
        RectTransform content = selectionScroll.content;
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        float x = 0f;
        foreach (RectTransform view in selectedViews)
        {
            view.anchorMin = view.anchorMax = new Vector2(0f, 0.5f);
            view.pivot = new Vector2(0f, 0.5f);
            view.anchoredPosition3D = new Vector3(x, 0f, 0f);
            x += view.rect.width + Mathf.Max(0f, selectedSpacing);
        }
        float viewportWidth = selectionScroll.viewport != null
            ? selectionScroll.viewport.rect.width : ((RectTransform)selectionScroll.transform).rect.width;
        float width = selectedViews.Count == 0 ? 0f : x - Mathf.Max(0f, selectedSpacing);
        content.sizeDelta = new Vector2(Mathf.Max(viewportWidth, width), 0f);
        selectionScroll.StopMovement();
        float left = Mathf.Clamp(content.anchoredPosition.x, Mathf.Min(0f, viewportWidth - content.rect.width), 0f);
        content.anchoredPosition = new Vector2(left, 0f);
    }

    private void Start()
    {
        Rebuild();
    }

    // Positions use the parent's center as the UI anchor, with Z fixed at zero.
    public static Vector3 GetCardPosition(int index, int count, float minX, float maxX, float y)
    {
        if (count <= 0 || index < 0 || index >= count)
            throw new System.ArgumentOutOfRangeException(nameof(index));

        float t = count == 1 ? 0.5f : (float)index / (count - 1);
        return new Vector3(Mathf.Lerp(minX, maxX, t), y, 0f);
    }

    public void Rebuild()
    {
        Clear();
        while (selectedCards.Count > 0) RemoveCard(selectedCards.Count - 1);
        RefreshSelectionCount();
        if (catalog == null || cardPrefab == null || cardParent == null)
        {
            Debug.LogError("Assign the card catalog, prefab and parent.", this);
            return;
        }

        if (catalog.cards == null)
            return;

        // Empty Inspector entries do not create gaps between visible cards.
        var cards = new List<CardData>();
        foreach (CardData card in catalog.cards)
            if (card != null) cards.Add(card);

        for (int i = 0; i < cards.Count; i++)
        {
            RectTransform view = Instantiate(cardPrefab, cardParent, false);
            instances.Add(view);
            view.name = $"Card_{i}_{cards[i].displayName}";
            view.anchorMin = view.anchorMax = new Vector2(0.5f, 0.5f);
            view.pivot = new Vector2(0.5f, 0.5f);
            view.anchoredPosition3D = GetCardPosition(i, cards.Count, minX, maxX, y);
            TMP_Text label = view.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = cards[i].displayName;
            CardData card = cards[i];
            Button button = view.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => ToggleCard(card));
            }
            view.gameObject.SetActive(true);
        }
        ArrangeSelectedCards();
    }

    private void RefreshSelectionCount()
    {
        if (selectionCountLabel != null)
            selectionCountLabel.text = $"選択中：{selectedCards.Count}/{MaxSelectedCards}";
    }

    private void OnDestroy()
    {
        Clear();
        foreach (RectTransform view in selectedViews)
            if (view != null) Destroy(view.gameObject);
        selectedViews.Clear();
        selectedCards.Clear();
    }

    private void Clear()
    {
        foreach (RectTransform view in instances)
        {
            if (view == null) continue;
            view.gameObject.SetActive(false);
            Destroy(view.gameObject);
        }
        instances.Clear();
    }
}
