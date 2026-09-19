using UnityEngine;

[RequireComponent(typeof(BulletView))]
public class Bullet : MonoBehaviour
{
    [SerializeField] bulletData_Card data;
    [SerializeField] bool isPlayer = true;
    BulletView view;
    int turnsSinceSubBulletEmission;
    int subBulletEmissions;

    public bulletData_Card Data => data;
    public Vector2Int Position => new Vector2Int(data.x, data.y);
    public Vector2Int NextPosition => new Vector2Int(data.nextX, data.nextY);
    public Vector2Int MoveDirection => (Attribute == Attribute.wall || Attribute == Attribute.gus || Attribute == Attribute.mirror) ? Vector2Int.zero : MoveDirection_override != Vector2Int.zero ? MoveDirection_override : (NextPosition - Position);
    public Vector2Int MoveDirection_override=Vector2Int.zero;
    public int HP => data.HP;
    public bool IsPlayer => isPlayer;

    public Attribute Attribute => data.attribute;
    public Vector2Int[] DetectRange => data.detectrange;
    public bulletData_Card[] SubBullets => data.subBullets;
    public bool HasPendingSubBullets => data != null && data.subBullets != null && data.subBullets.Length > 0 && subBulletEmissions < data.subBulletCount;

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
        turnsSinceSubBulletEmission = 0;
        subBulletEmissions = 0;
        view = GetComponent<BulletView>();
        view.Initialize(this, size, offset);
    }

    public void SetMoveDirection(Vector2Int direction)
    {
        if (data == null) return;
        MoveDirection_override = direction;
        data.nextX = data.x + direction.x;
        data.nextY = data.y + direction.y;
    }

    public bool AdvanceSubBulletTurn()
    {
        if (!HasPendingSubBullets) return false;
        turnsSinceSubBulletEmission++;
        if (turnsSinceSubBulletEmission < Mathf.Max(1, data.subBulletDelay)) return false;
        turnsSinceSubBulletEmission = 0;
        subBulletEmissions++;
        return true;
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
