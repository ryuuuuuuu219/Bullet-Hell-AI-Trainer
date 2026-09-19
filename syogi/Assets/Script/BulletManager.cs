using System.Collections.Generic;
using UnityEngine;

public class BulletManager : MonoBehaviour
{
    public List<Bullet> bullets = new List<Bullet>();
    public fieldrender board;
    public BattleJudge judge;

    public void Add(Bullet bullet)
    {
        if (bullet != null && !bullets.Contains(bullet)) bullets.Add(bullet);
    }

    public void ClearBullets()
    {
        foreach (var bullet in bullets)
            if (bullet != null) Destroy(bullet.gameObject);
        bullets.Clear();
    }

    public bool ResumeTurn()
    {
        bool result = false;
        if (board == null) board = GetComponent<fieldrender>();
        if (judge == null) judge = GetComponent<BattleJudge>();
        if (board == null || board.Columns <= 0 || board.Rows <= 0) return false;

        // 全弾の移動を終えてから、到着位置で衝突を解決する。
        var active = new List<Bullet>(bullets);
        foreach (var bullet in active)
        {
            if (bullet == null) continue;
            if (bullet.MoveDirection != Vector2Int.zero) result = true;
            bullet.SetPosition(bullet.NextPosition);
        }

        var damage = new Dictionary<Bullet, int>();
        for (int i = 0; i < active.Count; i++)
        {
            var first = active[i];
            if (first == null) continue;
            for (int j = i + 1; j < active.Count; j++)
            {
                var second = active[j];
                if (second == null || first.IsPlayer == second.IsPlayer || first.Position != second.Position) continue;
                if (!damage.ContainsKey(first)) damage[first] = 0;
                if (!damage.ContainsKey(second)) damage[second] = 0;
                damage[first] += second.HP;
                damage[second] += first.HP;
            }
        }

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
