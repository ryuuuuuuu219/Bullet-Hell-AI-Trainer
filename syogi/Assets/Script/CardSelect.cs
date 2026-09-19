using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSelect : MonoBehaviour
{
    public GameObject Render;
    [SerializeField] GameObject ScrollView;
    [SerializeField] GameObject CardDiscriptionObj;
    [SerializeField] GameObject CardPrefab;

    CardData[] CardDatas;
    public CardData SelectedCard { get; private set; }

    void Start()
    {
        CardDatas = CardDefinitions.Create();
        CardSet();
    }

    void CardSet()
    {
        var scroll = ScrollView != null ? ScrollView.GetComponent<ScrollRect>() : null;
        var description = CardDiscriptionObj != null ? CardDiscriptionObj.GetComponentInChildren<TMP_Text>(true) : null;
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
            label.text = card.CardName;
            label.raycastTarget = false;
            instance.GetComponent<Button>().onClick.AddListener(() => SelectCard(card, description));
            instance.SetActive(true);
        }
        var viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewport.rect.height, CardDatas.Length * height));
        scroll.verticalNormalizedPosition = 1f;
        SelectCard(CardDatas[0], description);
    }

    void SelectCard(CardData card, TMP_Text description)
    {
        SelectedCard = card;
        description.text = card.CardName + "\n\n" + card.CardDiscription;
        var grid = Render != null ? Render.GetComponent<fieldrender>() : GetComponent<fieldrender>();
        if (grid != null) grid.DrawGrid2(card.AffectAreaRow);
    }
}
