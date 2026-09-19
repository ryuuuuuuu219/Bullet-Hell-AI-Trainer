using UnityEngine;

[RequireComponent(typeof(BulletView))]
public class Bullet : MonoBehaviour
{
    [SerializeField] bulletData_Card data;
    [SerializeField] bool isPlayer = true;
    BulletView view;

    public bulletData_Card Data => data;
    public Vector2Int Position => new Vector2Int(data.x, data.y);
    public Vector2Int NextPosition => new Vector2Int(data.nextX, data.nextY);
    public Vector2Int MoveDirection => NextPosition - Position;
    public int HP => data.HP;
    public bool IsPlayer => isPlayer;

    public static Bullet Create(RectTransform parent, bulletData_Card data, Vector2Int size, Vector2 offset, bool isPlayer = true)
    {
        var obj = new GameObject("Bullet_" + data.x + "_" + data.y,
            typeof(RectTransform), typeof(BulletView), typeof(Bullet));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var bullet = obj.GetComponent<Bullet>();
        bullet.Initialize(data, size, offset, isPlayer);
        return bullet;
    }

    public void Initialize(bulletData_Card bulletData, Vector2Int size, Vector2 offset, bool belongsToPlayer = true)
    {
        data = bulletData;
        isPlayer = belongsToPlayer;
        view = GetComponent<BulletView>();
        view.Initialize(this, size, offset);
    }

    public void SetPosition(Vector2Int position)
    {
        if (data == null) return;
        var direction = MoveDirection;
        data.x = position.x;
        data.y = position.y;
        data.nextX = position.x + direction.x;
        data.nextY = position.y + direction.y;
        RefreshDisplay();
    }

    public void SetHP(int hp)
    {
        if (data == null) return;
        data.HP = hp;
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        if (view != null) view.RefreshDisplay();
    }

}
