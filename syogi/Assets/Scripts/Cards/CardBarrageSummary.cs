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
    public static string BuildSummary(CardCatalog source, bool isparent = true)
    {
        return BuildSummary(source, source != null ? source.cards : null, isparent);
    }

    public static string BuildSummary(CardCatalog source, IEnumerable<CardData> cards, bool isparent = true)
    {
        if (source == null) return string.Empty;
        // Materialize once: parent and child must use the same selected cards.
        var selected = cards == null ? new List<CardData>() : new List<CardData>(cards);
        var result = new StringBuilder();
        CardInitialSettings settings = source.initialSettings;
        if (settings != null)
        {
            string direction = settings.directionBasis == CardDirectionBasis.Aim ? "照準"
                : settings.directionBasis == CardDirectionBasis.Movement ? "移動ベクトル"
                : settings.directionBasis.ToString();
            string interpolation = settings.interpolation == CardFormationInterpolation.Linear
                ? "線形" : settings.interpolation.ToString();
            result.Append("発射基準: ").AppendLine(direction);
            result.Append("編隊の補間方式: ").AppendLine(interpolation);
            result.Append("分裂遅延: ").AppendLine(settings.hasSplitDelay
                ? Format(settings.splitDelay) + " 秒" : "なし");
        }
        foreach (CardParameterValue parameter in source.parameters ?? System.Array.Empty<CardParameterValue>())
        {
            if (parameter == null) continue;
            float modifier = BarrageComposer.SumModifier(parameter, selected, isparent);

            if (result.Length > 0) result.AppendLine();
            result.Append(CardParameterIds.GetLabel(parameter.ID)).Append(": ")
                .Append(Format(parameter.initialValue)).Append(" + ");
            if (modifier < 0f) result.Append('(');
            result.Append(Format(modifier));
            if (modifier < 0f) result.Append(')');
            float value = parameter.initialValue + modifier;
            if (parameter.ID == CardParameterIds.BulletCount) value = (float)System.Math.Floor(value);
            result.Append(" = ").Append(Format(value));
        }
        if (isparent)
        {
            if (result.Length > 0) result.AppendLine();
            result.Append("子弾の発射構造: ");
            if (settings == null) result.Append("初期設定なし");
            else if (!settings.hasChildFiringStructure) result.Append("定義なし");
            else if (source.ChildStructure == null) result.Append("未設定");
            else if (source.ChildStructure == source) result.Append("参照エラー（親と同一）");
            else
            {
                result.AppendLine("定義あり").AppendLine("------------------------------");
                result.Append(BuildSummary(source.ChildStructure, selected, false));
            }
        }
        return result.ToString();
    }

    private static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

}
