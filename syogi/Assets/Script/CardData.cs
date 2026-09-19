using System.Collections.Generic;

[System.Serializable]
public class CardData
{
    public int CardID;
    public string CardName;
    public string CardDiscription;
    public int AffectAreaRow;
    public List<bulletData_Card> bullet;
}

