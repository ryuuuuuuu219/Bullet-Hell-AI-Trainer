using System.Collections.Generic;

public static class CardDefinitions
{
    public const int BoardSize = 11;
    public const int PlacementRows = 3;

    static void ValidateAffectAreaRow(int affectAreaRow)
    {
        if (affectAreaRow % 2 != 0) return;
        string message = $"AffectAreaRow は奇数にしてください。指定値: {affectAreaRow}";
        UnityEngine.Debug.LogError(message);
        throw new System.ArgumentOutOfRangeException(nameof(affectAreaRow), affectAreaRow, message);
    }

    public static CardData[] Create()
    {
        return new[]
        {
            CreateHorizontalVolleyCard(0, 3, 3),
            CreateDefenseWallCard(1, 3, 1),
            CreatePiercingBulletCard(2, 3, 1),
            CreateGasCard(3, 3, 6),
            CreateMissileCard(4, 3, 6),
            CreateMirrorCard(5, 1, 6),
            CreateDispersionCard(6, 3, 2, 1, "分裂弾（右）"),
            CreateDispersionCard(7, 3, 2, -1, "分裂弾（左）")
        };
    }

    static CardData NameBullets(CardData card)
    {
        foreach (var bullet in card.bullet)
            SetName(bullet, card.CardName);
        return card;
    }

    static void SetName(bulletData_Card bullet, string cardName)
    {
        bullet.name = cardName;
        if (bullet.subBullets == null) return;
        foreach (var child in bullet.subBullets)
            if (child != null) SetName(child, cardName);
    }

    public static CardData CreateHorizontalVolleyCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        var bullets = new List<bulletData_Card>();
        for (int x = 0; x < affectAreaRow; x++)
        {
            bullets.Add(new bulletData_Card { x = x, y = 0, nextX = x, nextY = 1, HP = 1 });
        }

        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "横一列斉射",
            CardDiscription = "横一列の編隊になって飛翔するHP1の弾幕3斉射",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = bullets
        });
    }

    public static CardData CreateDefenseWallCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "防御壁",
            CardDiscription = "留まる壁HP9",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = center, y = center, nextX = center, nextY = center, HP = 9, attribute = Attribute.wall }
            }
        });
    }

    public static CardData CreatePiercingBulletCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "貫通弾",
            CardDiscription = "一発のみ、HP9で飛翔する貫通弾",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = center, y = center, nextX = center, nextY = center + 1, HP = 9 }
            }
        });
    }

    public static CardData CreateGasCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "ガス弾",
            CardDiscription = "炸裂し、その座標に接触した弾幕のHPを削る",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, nextX = center, nextY = center + 1,
                    HP = 2, attribute = Attribute.dispersion,
                    subBullets = new[]
                    {
                        new bulletData_Card { x = 0, y = 0, nextX = 0, nextY = 0, HP = 2, attribute = Attribute.gus }
                    },
                    subBulletCount = 1, subBulletDelay = 5
                }
            }
        });
    }

    public static CardData CreateMissileCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "ミサイル",
            CardDiscription = "周辺の弾幕に向かい誘導する",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, nextX = center, nextY = center + 1,
                    HP = 2, attribute = Attribute.missile,
                    detectrange = new[]
                    {
                        new UnityEngine.Vector2Int(-1, 1), new UnityEngine.Vector2Int(0, 1),
                        new UnityEngine.Vector2Int(1, 1)
                    }
                }
            }
        });
    }

    public static CardData CreateMirrorCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "反射トラップ",
            CardDiscription = "接触した弾幕を反射する",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, nextX = center, nextY = center,
                    HP = 2, attribute = Attribute.mirror
                }
            }
        });
    }

    public static CardData CreateDispersionCard(int cardId, int affectAreaRow, int remainingCount,
        int horizontalDirection, string cardName)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = cardName,
            CardDiscription = "複数の弾を生成しながら飛翔する",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, nextX = center + horizontalDirection, nextY = center + 1,
                    HP = 6, attribute = Attribute.dispersion,
                    subBullets = new[]
                    {
                        new bulletData_Card { x = 0, y = 0, nextX = 0, nextY = 1, HP = 1, attribute = Attribute.projectile }
                    },
                    subBulletCount = 6, subBulletDelay = 0
                }
            }
        });
    }
}
