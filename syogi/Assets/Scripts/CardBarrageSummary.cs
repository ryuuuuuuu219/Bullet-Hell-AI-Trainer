using System.Globalization;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public sealed class CardBarrageSummary : MonoBehaviour
{
    [SerializeField] private CardCatalog catalog;
    [SerializeField] private CardLayout selection;
    private TMP_Text label;

    private void OnEnable()
    {
        label = GetComponent<TMP_Text>();
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        string summary = selection != null
            ? BuildSummary(catalog, selection.SelectedCards) : BuildSummary(catalog);
        if (label.text != summary) label.text = summary;
    }

    // Calculate without changing the catalog's initial values or modifiers.
    public static string BuildSummary(CardCatalog source)
    {
        return BuildSummary(source, source != null ? source.cards : null);
    }

    public static string BuildSummary(CardCatalog source, IEnumerable<CardData> cards)
    {
        if (source == null || source.parameters == null) return string.Empty;
        var result = new StringBuilder();
        foreach (CardParameterValue parameter in source.parameters)
        {
            if (parameter == null) continue;
            float modifier = parameter.mod;
            if (!string.IsNullOrEmpty(parameter.ID) && cards != null)
                foreach (CardData card in cards)
                {
                    if (card?.items == null) continue;
                    foreach (CardItem item in card.items)
                        if (item != null && item.ID == parameter.ID)
                            modifier += item.mod;
                }

            if (result.Length > 0) result.AppendLine();
            result.Append(GetLabel(parameter.ID)).Append(": ")
                .Append(Format(parameter.initialValue)).Append(" + ");
            if (modifier < 0f) result.Append('(');
            result.Append(Format(modifier));
            if (modifier < 0f) result.Append(')');
            result.Append(" = ").Append(Format(parameter.initialValue + modifier));
        }
        return result.ToString();
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetLabel(string id)
    {
        switch (id)
        {
            case CardParameterIds.BulletSpeed: return "弾速";
            case CardParameterIds.FireInterval: return "発射周期";
            case CardParameterIds.BulletCount: return "発射数";
            case CardParameterIds.FormationAngleStart: return "編隊角度（始点）";
            case CardParameterIds.FormationAngleEnd: return "編隊角度（終点）";
            case CardParameterIds.AngularSpeedStart: return "角速度（始点）";
            case CardParameterIds.AngularSpeedEnd: return "角速度（終点）";
            case CardParameterIds.SpeedOffsetStart: return "弾速差（始点）";
            case CardParameterIds.SpeedOffsetEnd: return "弾速差（終点）";
            case CardParameterIds.PowerSlopeStart: return "威力・時間係数（始点）";
            case CardParameterIds.PowerSlopeEnd: return "威力・時間係数（終点）";
            case CardParameterIds.PowerConstantStart: return "威力・定数（始点）";
            case CardParameterIds.PowerConstantEnd: return "威力・定数（終点）";
            case CardParameterIds.SplitInterval: return "分裂間隔";
            default: return id ?? "未設定";
        }
    }
}
