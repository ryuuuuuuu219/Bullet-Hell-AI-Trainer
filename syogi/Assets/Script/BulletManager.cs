using System.Collections.Generic;
using UnityEngine;

public class BulletManager : MonoBehaviour
{
    public List<Bullet> bullets = new List<Bullet>();
    public fieldrender board;
    public BattleJudge judge;

    List<Bullet.TurnState> previousTurn;
    public bool CanUndoTurn => previousTurn != null;

    public void SaveTurn()
    {
        previousTurn = new List<Bullet.TurnState>();
        foreach (var bullet in bullets)
            if (bullet != null) previousTurn.Add(bullet.CaptureTurnState());
    }

    public bool UndoTurn()
    {
        if (previousTurn == null) return false;
        if (board == null) board = GetComponent<fieldrender>();
        if (board == null || board.FoundationRect == null || board.Columns <= 0 || board.Rows <= 0) return false;

        var saved = previousTurn;
        previousTurn = null;
        ClearBullets();
        foreach (var state in saved)
        {
            var bullet = Bullet.Create(board.FoundationRect, state.Data,
                new Vector2Int(board.Columns, board.Rows), board.FieldRightTopPos, state.IsPlayer);
            bullet.RestoreTurnState(state);
            Add(bullet);
        }
        return true;
    }

    public void ForgetTurn() => previousTurn = null;

    public void Add(Bullet bullet)
    {
        if (bullet != null && !bullets.Contains(bullet)) bullets.Add(bullet);
    }

    public void ClearBullets()
    {
        foreach (var bullet in bullets)
            if (bullet != null)
            {
                bullet.gameObject.SetActive(false);
                Destroy(bullet.gameObject);
            }
        bullets.Clear();
    }

    Vector2Int GetMissileDirection(Bullet missile, List<Bullet> active, Dictionary<Bullet, Vector2Int> startPositions)
    {
        var range = missile.DetectRange;
        if (range == null) return missile.MoveDirection;

        var position = startPositions[missile];
        foreach (var offset in range)
        {
            var targetPosition = position + offset;
            if (targetPosition.x < 0 || targetPosition.x >= board.Columns ||
                targetPosition.y < 0 || targetPosition.y >= board.Rows) continue;

            foreach (var other in active)
            {
                if (other == null || other == missile || other.IsPlayer == missile.IsPlayer) continue;
                if (startPositions[other] != targetPosition) continue;
                return new Vector2Int(Mathf.Clamp(offset.x, -1, 1), Mathf.Clamp(offset.y, -1, 1));
            }
        }
        return missile.MoveDirection;
    }

    // 同じ時刻 t (0 <= t <= 1) に二つの移動点が一致するかを整数演算で調べる。
    static bool PathsMeet(Vector2Int firstStart, Vector2Int firstEnd, Vector2Int secondStart, Vector2Int secondEnd)
    {
        long relativeX = (long)firstStart.x - secondStart.x;
        long relativeY = (long)firstStart.y - secondStart.y;
        long relativeMoveX = (long)firstEnd.x - firstStart.x - ((long)secondEnd.x - secondStart.x);
        long relativeMoveY = (long)firstEnd.y - firstStart.y - ((long)secondEnd.y - secondStart.y);

        if (relativeMoveX == 0 && relativeMoveY == 0)
            return relativeX == 0 && relativeY == 0;

        if (relativeX * relativeMoveY - relativeY * relativeMoveX != 0) return false;
        long meetingTimeNumerator = -(relativeX * relativeMoveX + relativeY * relativeMoveY);
        long meetingTimeDenominator = relativeMoveX * relativeMoveX + relativeMoveY * relativeMoveY;
        return meetingTimeNumerator >= 0 && meetingTimeNumerator <= meetingTimeDenominator;
    }

    public bool ResumeTurn()
    {
        bool result = false;
        if (board == null) board = GetComponent<fieldrender>();
        if (judge == null) judge = GetComponent<BattleJudge>();
        if (board == null || board.Columns <= 0 || board.Rows <= 0) return false;

        // 分裂を先に処理し、子弾もこのターンに移動させる。
        var active = new List<Bullet>(bullets);
        var parent = board.FoundationRect;
        var spawnedThisTurn = new HashSet<Bullet>();
        foreach (var bullet in new List<Bullet>(active))
        {
            if (bullet == null || !bullet.HasPendingSubBullets) continue;
            result = true;
            if (!bullet.AdvanceSubBulletTurn() || parent == null) continue;
            foreach (var template in bullet.SubBullets)
            {
                if (template == null) continue;
                int x = bullet.Position.x + template.x;
                int y = bullet.Position.y + template.y;
                if (x < 0 || x >= board.Columns || y < 0 || y >= board.Rows) continue;
                var childData = template.CopyAt(x, y, bullet.IsPlayer);
                var child = Bullet.Create(parent, childData, new Vector2Int(board.Columns, board.Rows),
                    board.FieldRightTopPos, bullet.IsPlayer);
                bullet.SetHP(Mathf.Max(0, bullet.HP - childData.HP));
                Add(child);
                active.Add(child);
                spawnedThisTurn.Add(child);
            }
            if (bullet.HP <= 0)
            {
                active.Remove(bullet);
                bullets.Remove(bullet);
                bullet.gameObject.SetActive(false);
                Destroy(bullet.gameObject);
            }
        }

        // 索敵には分裂後の全弾のターン開始位置を使う。
        var startPositions = new Dictionary<Bullet, Vector2Int>();
        foreach (var bullet in active)
            if (bullet != null) startPositions[bullet] = bullet.Position;

        foreach (var bullet in active)
        {
            if (bullet == null) continue;
            if (bullet.Attribute == Attribute.missile)
                bullet.SetMoveDirection(GetMissileDirection(bullet, active, startPositions));
            var destination = bullet.Position + bullet.MoveDirection;
            if (destination != bullet.Position) result = true;
            bullet.SetPosition(destination);
        }

        // 既存弾同士は移動経路を、生成直後の子弾が関わる衝突は終点を比較する。
        var damage = new Dictionary<Bullet, int>();
        for (int i = 0; i < active.Count; i++)
        {
            var first = active[i];
            if (first == null) continue;
            for (int j = i + 1; j < active.Count; j++)
            {
                var second = active[j];
                if (second == null) continue;
                bool meets = spawnedThisTurn.Contains(first) || spawnedThisTurn.Contains(second)
                    ? first.Position == second.Position
                    : PathsMeet(startPositions[first], first.Position, startPositions[second], second.Position);
                if (!meets) continue;
                if (!damage.ContainsKey(first)) damage[first] = 0;
                if (!damage.ContainsKey(second)) damage[second] = 0;
                if(first.Attribute!=Attribute.gus)damage[first] += second.HP;
                if (second.Attribute != Attribute.gus) damage[second] += first.HP;
                if(first.Attribute==Attribute.mirror) second.SetMoveDirection(-second.MoveDirection);
                if(second.Attribute==Attribute.mirror) first.SetMoveDirection(-first.MoveDirection);
            }
        }

        // ダメージを適用し、HPが0以下になった弾を削除する。
        foreach (var bullet in active)
        {
            if (bullet == null) continue;
            if (damage.TryGetValue(bullet, out int amount)) bullet.SetHP(bullet.HP - amount);
            var position = bullet.Position;
            if (bullet.HP > 0 && position.x >= 0 && position.x < board.Columns &&
                position.y >= 0 && position.y < board.Rows) continue;
            if (bullet.HP > 0 && judge != null)
            {
                if (position.y >= board.Rows) judge.DamageOpponent(bullet.HP);
                else if (position.y < 0) judge.DamagePlayer(bullet.HP);
            }
            bullets.Remove(bullet);
            Destroy(bullet.gameObject);
        }
        return result;
    }
}
