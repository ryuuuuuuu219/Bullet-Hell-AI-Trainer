using UnityEngine;

[System.Serializable]
public class bulletData_Card
{

    public int x;
    public int y;
    public int nextX;
    public int nextY;
    public int HP;
    public Attribute attribute;
    public bulletData_Card[] subBullets; // サブ弾幕の配列を追加
    public int subBulletCount; // サブ弾幕の数を追加
    public int subBulletDelay; // サブ弾幕の発射タイミングを追加
    public Vector2Int[] detectrange; //誘導弾の検知範囲を追加

    public bulletData_Card CopyAt(int positionX, int positionY, bool isPlayer)
    {
        return new bulletData_Card
        {
            x = positionX,
            y = positionY,
            nextX = positionX + nextX - x,
            nextY = positionY + (isPlayer ? nextY - y : y - nextY),
            HP = HP,
            attribute = attribute,
            subBullets = subBullets,
            subBulletCount = subBulletCount,
            subBulletDelay = subBulletDelay,
            detectrange = detectrange
        };
    }
}

[System.Serializable]
public enum Attribute
{
    projectile, // 弾丸
    wall,       // 壁 移動速度はVector2Int.zero　HPが0になるまで留まる
    gus,        // ガス 移動速度はVector2Int.zero　接触のたびに自身のHPぶんのダメージを与える
    missile,    // 誘導処理を追加
    mirror,     // 反射処理を追加　移動速度はVector2Int.zero
    dispersion,  // 分散処理を追加 壁・ガス・反射はこの属性のサブ弾幕として生成される
}
