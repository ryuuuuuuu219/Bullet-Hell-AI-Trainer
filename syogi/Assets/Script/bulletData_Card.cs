using UnityEngine;

[System.Serializable]
public class bulletData_Card
{
    public string name;
    public string displayCode;
    public int x;
    public int y;
    public Vector2Int moveVector;
    public int HP;
    public Attribute attribute;
    public bulletData_Card[] subBullets; // サブ弾幕の配列を追加
    public int subBulletCount; // サブ弾幕の数を追加
    public int subBulletDelay; // サブ弾幕の発射タイミングを追加
    public Vector2Int[] detectrange; //誘導弾の検知範囲を追加

    public bulletData_Card CopyAt(int positionX, int positionY, bool isPlayer)
    {
        Vector2Int[] copiedDetectRange = null;
        if (detectrange != null)
        {
            copiedDetectRange = new Vector2Int[detectrange.Length];
            for (int i = 0; i < detectrange.Length; i++)
                copiedDetectRange[i] = new Vector2Int(detectrange[i].x, isPlayer ? detectrange[i].y : -detectrange[i].y);
        }
        return new bulletData_Card
        {
            x = positionX,
            y = positionY,
            moveVector = new Vector2Int(moveVector.x, isPlayer ? moveVector.y : -moveVector.y),
            HP = HP,
            name = name,
            displayCode = displayCode,
            attribute = attribute,
            subBullets = subBullets,
            subBulletCount = subBulletCount,
            subBulletDelay = subBulletDelay,
            detectrange = copiedDetectRange
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
