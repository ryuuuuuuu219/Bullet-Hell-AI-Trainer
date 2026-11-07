using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Runtime candidates are independent copies of the catalog's starter cards.
public sealed class CardInventory
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public CardData[] cards;
        public int[] selectedIndices;
        public long nextDropId;
    }

    public SaveData CaptureSave()
    {
        return new SaveData
        {
            cards = candidates.ConvertAll(Copy).ToArray(),
            selectedIndices = selected.ConvertAll(card => candidates.IndexOf(card)).ToArray(),
            nextDropId = nextDropId
        };
    }

    public static CardInventory FromSave(SaveData data)
    {
        if (data == null || data.version != 1 || data.cards == null ||
            data.cards.Length > Capacity || data.selectedIndices == null ||
            data.selectedIndices.Length > 5 || data.nextDropId < 1 || data.nextDropId == long.MaxValue)
            throw new ArgumentException("Invalid card save data.");
        foreach (CardData card in data.cards)
        {
            if (card == null || card.items == null)
                throw new ArgumentException("Invalid saved card.");
            foreach (CardItem item in card.items)
                if (item == null || string.IsNullOrEmpty(item.ID) || !BarrageComposer.IsFinite(item.mod))
                    throw new ArgumentException("Invalid saved card parameter.");
        }
        var restored = new CardInventory(data.cards);
        foreach (int index in data.selectedIndices)
        {
            if (index < 0 || index >= restored.candidates.Count ||
                restored.selected.Contains(restored.candidates[index]))
                throw new ArgumentException("Invalid saved selection.");
            restored.selected.Add(restored.candidates[index]);
        }
        restored.nextDropId = data.nextDropId;
        return restored;
    }

    public const int Capacity = 16;
    private readonly List<CardData> candidates = new List<CardData>();
    private readonly List<CardData> selected = new List<CardData>();
    public IReadOnlyList<CardData> Candidates => candidates.AsReadOnly();
    public IReadOnlyList<CardData> Selected => selected.AsReadOnly();

    public CardInventory(IEnumerable<CardData> initialCards)
    {
        if (initialCards == null) return;
        foreach (CardData card in initialCards)
        {
            if (card == null) continue;
            if (candidates.Count == Capacity) break;
            candidates.Add(Copy(card));
        }
    }

    public void SetSelection(IEnumerable<CardData> cards)
    {
        var next = new List<CardData>();
        if (cards != null)
            foreach (CardData card in cards)
                if (card != null && candidates.Contains(card) && !next.Contains(card) && next.Count < 5)
                    next.Add(card);
        selected.Clear();
        selected.AddRange(next);
    }

    // SplitInterval is excluded until runtime splitting is implemented.
    // Each parameter gets an independent centered random modifier.
    private static readonly string[] dropIds =
    {
        CardParameterIds.BulletSpeed, CardParameterIds.BulletRadius,
        CardParameterIds.BulletLifetime, CardParameterIds.FireInterval,
        CardParameterIds.BulletCount, CardParameterIds.FormationAngleStart,
        CardParameterIds.FormationAngleEnd, CardParameterIds.AngularSpeedStart,
        CardParameterIds.AngularSpeedEnd, CardParameterIds.SpeedOffsetStart,
        CardParameterIds.SpeedOffsetEnd, CardParameterIds.PowerSlopeStart,
        CardParameterIds.PowerSlopeEnd, CardParameterIds.PowerConstantStart,
        CardParameterIds.PowerConstantEnd
    };
    public int itemsnum => dropIds.Length;
    private long nextDropId = 1;
#if !UNITY_5_3_OR_NEWER
    private static readonly Random random = new Random();
#endif
    private static float RandomWeight()
    {
#if UNITY_5_3_OR_NEWER
        return UnityEngine.Random.Range(0f, 1f);
#else
        return (float)random.NextDouble();
#endif
    }

    public static float GetDropMultiplier(string id)
    {
        if (Array.IndexOf(dropIds, id) < 0)
            throw new ArgumentException("Unsupported drop parameter: " + id);
        // Convert authored values to per-unit multipliers for score * (10 + stageID).
        float value = 0;
        switch (id)
        {
            case CardParameterIds.BulletSpeed: value = 500f; break;
            case CardParameterIds.BulletRadius: value = 50f; break;
            case CardParameterIds.BulletLifetime: value = 20f; break;
            case CardParameterIds.FireInterval: value = 6f; break;
            case CardParameterIds.BulletCount: value = 10f; break;
            case CardParameterIds.FormationAngleStart: value = 360f; break;
            case CardParameterIds.FormationAngleEnd: value = 360f; break;
            case CardParameterIds.AngularSpeedStart: value = -200f; break;
            case CardParameterIds.AngularSpeedEnd: value = 200f; break;
            case CardParameterIds.SpeedOffsetStart: value = -200f; break;
            case CardParameterIds.SpeedOffsetEnd: value = 200f; break;
            case CardParameterIds.PowerSlopeStart: value = 1f; break;
            case CardParameterIds.PowerSlopeEnd: value = 1f; break;
            case CardParameterIds.PowerConstantStart: value = 1f; break;
            case CardParameterIds.PowerConstantEnd: value = 1f; break;
        }


        return value / 1000f;
    }

    // Optional random source permits deterministic checks without Unity runtime.
    public bool TryAddDrop(float score, int stageID, out CardData drop, out string error,
        Func<float> randomWeight = null)
    {
        drop = null;
        error = null;
        double total = (double)score * (10d + stageID);
        if (!BarrageComposer.IsFinite(score) || score < 0 || stageID < 1 || total > float.MaxValue)
        {
            error = "Drop requires a finite nonnegative scale and stageID >= 1.";
            return false;
        }
        var weights = new double[itemsnum];
        for (int i = 0; i < weights.Length; i++)
        {
            float weight = (randomWeight ?? RandomWeight)();
            if (!BarrageComposer.IsFinite(weight) || weight < 0 || weight > 1)
            {
                error = "Drop random weight must be finite and between 0 and 1.";
                return false;
            }
            weights[i] = ConvertDropWeight((weight - 0.5d)*2d);
        }
        var items = new CardItem[itemsnum];
        var name = new StringBuilder("ID:").Append(nextDropId);
        for (int i = 0; i < items.Length; i++)
        {
            double modifier = total * weights[i] * GetDropMultiplier(dropIds[i]);
            if (double.IsNaN(modifier) || double.IsInfinity(modifier) || Math.Abs(modifier) > float.MaxValue)
            {
                error = "Drop modifier overflow: " + dropIds[i];
                return false;
            }
            items[i] = new CardItem { ID = dropIds[i], mod = (float)modifier };
            name.Append(' ').Append(CardParameterIds.GetLabel(dropIds[i])).Append(':')
                .Append(items[i].mod.ToString("0.00", CultureInfo.InvariantCulture));
        }
        // Commit only after the complete card has been validated.
        int oldest = -1;
        if (candidates.Count == Capacity)
        {
            oldest = candidates.FindIndex(card => !selected.Contains(card));
            if (oldest < 0) { error = "No unselected card can be replaced."; return false; }
        }
        drop = new CardData { displayName = name.ToString(), items = items };
        if (oldest >= 0) candidates.RemoveAt(oldest);
        candidates.Add(drop);
        nextDropId++;
        return true;
    }

    // Continuous value and slope at zero; approaches -1/5 for negative inputs.
    public static double ConvertDropWeight(double x)
    {
        return x < 0d ? -0.2d + 1.2d * Math.Exp(x / 1.2d) : 1d + x;
    }

    public static CardData Copy(CardData source)
    {
        var items = new List<CardItem>();
        if (source.items != null)
            foreach (CardItem item in source.items)
                if (item != null) items.Add(new CardItem { ID = item.ID, mod = item.mod });
        return new CardData { displayName = source.displayName, items = items.ToArray() };
    }
}
