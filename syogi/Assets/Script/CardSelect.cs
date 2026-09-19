using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CardSelect : MonoBehaviour
{
    public GameObject Render;
    [SerializeField] GameObject ScrollView;
    [SerializeField] GameObject CardDiscriptionObj;
    [SerializeField] GameObject CardPrefab;

    CardData[] CardDatas;
    readonly Dictionary<int, Button> cardButtons = new Dictionary<int, Button>();
    readonly Dictionary<int, TMP_Text> cardLabels = new Dictionary<int, TMP_Text>();
    TMP_Text description;
    public CardData SelectedCard { get; private set; }

    void Start()
    {
        CardDatas = CardDefinitions.Create();
        CardSet();
    }

    void CardSet()
    {
        var scroll = ScrollView != null ? ScrollView.GetComponent<ScrollRect>() : null;
        description = CardDiscriptionObj != null ? CardDiscriptionObj.GetComponentInChildren<TMP_Text>(true) : null;
        if (scroll == null || scroll.content == null || description == null || CardPrefab == null ||
            CardPrefab.GetComponent<Button>() == null || CardPrefab.GetComponent<RectTransform>() == null ||
            CardPrefab.GetComponentInChildren<TMP_Text>(true) == null)
        {
            Debug.LogError("CardSelect に Scroll View、説明テキスト、Button と TMP テキストを持つカードPrefabを設定してください。", this);
            return;
        }
        float height = CardPrefab.GetComponent<RectTransform>().sizeDelta.y;
        if (height <= 0f)
        {
            Debug.LogError("カードPrefabの高さは0より大きく設定してください。", this);
            return;
        }
        for (int i = 0; i < CardDatas.Length; i++)
        {
            var card = CardDatas[i];
            var instance = Instantiate(CardPrefab, scroll.content, false);
            instance.name = "Card_" + card.CardID;
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -i * height);
            rect.sizeDelta = new Vector2(0f, height);
            var label = instance.GetComponentInChildren<TMP_Text>(true);
            label.font = description.font;
            label.text = CardLabel(card);
            label.raycastTarget = false;
            var button = instance.GetComponent<Button>();
            cardButtons.Add(card.CardID, button);
            cardLabels.Add(card.CardID, label);
            button.onClick.AddListener(() => SelectCard(card));
            instance.SetActive(true);
        }
        var viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewport.rect.height, CardDatas.Length * height));
        scroll.verticalNormalizedPosition = 1f;
        SelectCard(CardDatas[0]);
    }

    string CardLabel(CardData card) => card.CardName + " ×" + card.RemainingCount;

    void SelectCard(CardData card)
    {
        if (card == null || card.RemainingCount <= 0) return;
        SelectedCard = card;
        description.text = card.CardName + "（残り" + card.RemainingCount + "枚）\n\n" + card.CardDiscription;
        var grid = Render != null ? Render.GetComponent<fieldrender>() : GetComponent<fieldrender>();
        if (grid != null) grid.DrawGrid2(card.AffectAreaRow);
    }

    public bool CanPlaceSelectedCard => SelectedCard != null && SelectedCard.RemainingCount > 0;

    public void ConsumeSelectedCard()
    {
        if (!CanPlaceSelectedCard) return;
        var card = SelectedCard;
        card.RemainingCount--;
        cardLabels[card.CardID].text = CardLabel(card);
        cardButtons[card.CardID].interactable = card.RemainingCount > 0;
        if (card.RemainingCount > 0)
        {
            SelectCard(card);
            return;
        }
        foreach (var available in CardDatas)
        {
            if (available.RemainingCount <= 0) continue;
            SelectCard(available);
            return;
        }
        SelectedCard = null;
        description.text = "カードを使い切りました。";
    }

    public void ResetCards()
    {
        if (CardDatas == null || description == null) return;
        var initial = CardDefinitions.Create();
        foreach (var card in CardDatas)
        {
            foreach (var source in initial)
                if (source.CardID == card.CardID) card.RemainingCount = source.RemainingCount;
            cardLabels[card.CardID].text = CardLabel(card);
            cardButtons[card.CardID].interactable = card.RemainingCount > 0;
        }
        if (CardDatas.Length > 0) SelectCard(CardDatas[0]);
    }
}
