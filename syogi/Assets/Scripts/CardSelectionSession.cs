using System;
using System.Collections.Generic;
using UnityEngine;

// Session-only selection. Assets are never modified by gameplay.
public static class CardSelectionSession
{
    public static CardCatalog Catalog { get; private set; }
    private static CardInventory inventory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Catalog = null; inventory = null; }

    public static CardInventory GetInventory(CardCatalog catalog)
    {
        if (catalog == null) return null;
        if (inventory == null || Catalog != catalog)
        {
            Catalog = catalog;
            inventory = new CardInventory(catalog.cards);
        }
        return inventory;
    }

    public static void Save(CardCatalog catalog, IReadOnlyList<CardData> cards)
    {
        GetInventory(catalog)?.SetSelection(cards);
    }

    public static CardData[] Restore(CardCatalog catalog)
    {
        CardInventory current = GetInventory(catalog);
        return current == null ? Array.Empty<CardData>() : new List<CardData>(current.Selected).ToArray();
    }
}
