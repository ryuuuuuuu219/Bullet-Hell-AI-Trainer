using System.Collections.Generic;

[System.Serializable]
public class CardData
{
    public int CardID;
    public string CardName;
    public string CardDiscription;
    public int AffectAreaRow;
    public int RemainingCount;
    public List<bulletData_Card> bullet;
}

