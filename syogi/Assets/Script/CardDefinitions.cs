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

    static CardData NameBullets(CardData card, string displayCode)
    {
        foreach (var bullet in card.bullet)
            SetName(bullet, card.CardName, displayCode);
        return card;
    }

    static void SetName(bulletData_Card bullet, string cardName, string displayCode)
    {
        bullet.name = cardName;
        bullet.displayCode = displayCode;
        if (bullet.subBullets == null) return;
        foreach (var child in bullet.subBullets)
            if (child != null) SetName(child, cardName, displayCode);
    }

    public static CardData CreateHorizontalVolleyCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        var bullets = new List<bulletData_Card>();
        for (int x = 0; x < affectAreaRow; x++)
        {
            bullets.Add(new bulletData_Card { x = x, y = center, moveVector = new UnityEngine.Vector2Int(0, 1), HP = 1 });
        }

        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "横一列斉射",
            CardDiscription = "内訳：通常弾(Ball)×3\n横一列の編隊になって飛翔するHP1の弾幕3斉射",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = bullets
        }, "Ball");
    }

    public static CardData CreateDefenseWallCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "防御壁",
            CardDiscription = "内訳：防御壁(DeFw)×1\n留まる壁HP9",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = center, y = center, moveVector = UnityEngine.Vector2Int.zero, HP = 9, attribute = Attribute.wall }
            }
        }, "DeFw");
    }

    public static CardData CreatePiercingBulletCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "貫通弾",
            CardDiscription = "内訳：貫通弾(APb)×1\nHP9で飛翔する貫通弾",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card { x = center, y = center, moveVector = new UnityEngine.Vector2Int(0, 1), HP = 9 }
            }
        }, "APb");
    }

    public static CardData CreateGasCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "ガス弾",
            CardDiscription = "内訳：ガス弾(Gab)×1\n一定距離で炸裂し、その座標に接触した弾幕のHPを削る",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, moveVector = new UnityEngine.Vector2Int(0, 1),
                    HP = 2, attribute = Attribute.dispersion,
                    subBullets = new[]
                    {
                        new bulletData_Card { x = 0, y = 0, moveVector = UnityEngine.Vector2Int.zero, HP = 2, attribute = Attribute.gus }
                    },
                    subBulletCount = 1, subBulletDelay = 5
                }
            }
        }, "Gab");
    }

    public static CardData CreateMissileCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "ミサイル",
            CardDiscription = "内訳：ミサイル(Msl)×1\n周辺の弾幕に向かい誘導する",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, moveVector = new UnityEngine.Vector2Int(0, 1),
                    HP = 2, attribute = Attribute.missile,
                    detectrange = new[]
                    {
                        new UnityEngine.Vector2Int(-1, 1), new UnityEngine.Vector2Int(0, 1),
                        new UnityEngine.Vector2Int(1, 1)
                    }
                }
            }
        }, "Msl");
    }

    public static CardData CreateMirrorCard(int cardId, int affectAreaRow, int remainingCount)
    {
        ValidateAffectAreaRow(affectAreaRow);
        int center = (affectAreaRow - 1) / 2;
        return NameBullets(new CardData
        {
            CardID = cardId,
            CardName = "反射トラップ",
            CardDiscription = "内訳：反射トラップ(RefT)×1\n接触した弾幕を反射する",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, moveVector = UnityEngine.Vector2Int.zero,
                    HP = 2, attribute = Attribute.mirror
                }
            }
        }, "RefT");
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
            CardDiscription = "内訳：分裂弾("+ (horizontalDirection > 0 ? "Sp-r" : "Sp-l") + ")×1\n複数の弾を生成しながら飛翔する",
            AffectAreaRow = affectAreaRow,
            RemainingCount = remainingCount,
            bullet = new List<bulletData_Card>
            {
                new bulletData_Card
                {
                    x = center, y = center, moveVector = new UnityEngine.Vector2Int(horizontalDirection, 1),
                    HP = 6, attribute = Attribute.dispersion,
                    subBullets = new[]
                    {
                        new bulletData_Card { x = 0, y = 0, moveVector = new UnityEngine.Vector2Int(0, 1), HP = 1, attribute = Attribute.projectile }
                    },
                    subBulletCount = 6, subBulletDelay = 0
                }
            }
        }, horizontalDirection > 0 ? "Sp-r" : "Sp-l");
    }
}
