using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Cards/Card Catalog")]
public sealed class CardCatalog : ScriptableObject
{
    public CardInitialSettings initialSettings = new CardInitialSettings();
    public CardParameterValue[] parameters = Array.Empty<CardParameterValue>();
    public CardData[] cards = Array.Empty<CardData>();
    // Assign a separate catalog asset; never recursively construct ScriptableObjects.
    public CardCatalog ChildStructure;
}
