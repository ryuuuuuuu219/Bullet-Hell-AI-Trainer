using System;
using TMPro;
using UnityEngine;

public class BattleJudge : MonoBehaviour
{
    public enum Result
    {
        Undecided,
        PlayerWin,
        OpponentWin,
        Draw
    }

    [SerializeField] int playerDamage;
    [SerializeField] int opponentDamage;

    [SerializeField] TextMeshProUGUI damageUI;
    string damageText => "ダメージ量\t相手："+opponentDamage+"\n\t\t\t自分："+playerDamage;

    // それぞれが受けたダメージの累計。
    public int PlayerDamage => playerDamage;
    public int OpponentDamage => opponentDamage;
    public Result CurrentResult { get; private set; } = Result.Undecided;

    public event Action<int, int, Result> StateChanged;

    private void Update()
    {
        damageUI.text = damageText;
    }

    public void DamagePlayer(int amount)
    {
        if (amount <= 0) return;
        playerDamage += amount;
        CurrentResult = Result.Undecided;
        StateChanged?.Invoke(playerDamage, opponentDamage, CurrentResult);
    }

    public void DamageOpponent(int amount)
    {
        if (amount <= 0) return;
        opponentDamage += amount;
        CurrentResult = Result.Undecided;
        StateChanged?.Invoke(playerDamage, opponentDamage, CurrentResult);
    }

    // 判定を求めた時点で、受けたダメージが少ない側を勝ちとする。
    public Result Judge()
    {
        if (playerDamage < opponentDamage) CurrentResult = Result.PlayerWin;
        else if (opponentDamage < playerDamage) CurrentResult = Result.OpponentWin;
        else CurrentResult = Result.Draw;
        StateChanged?.Invoke(playerDamage, opponentDamage, CurrentResult);
        return CurrentResult;
    }

    public void ResetDamage()
    {
        playerDamage = 0;
        opponentDamage = 0;
        CurrentResult = Result.Undecided;
        StateChanged?.Invoke(playerDamage, opponentDamage, CurrentResult);
    }

    public void RestoreDamage(int player, int opponent)
    {
        playerDamage = player;
        opponentDamage = opponent;
        CurrentResult = Result.Undecided;
        StateChanged?.Invoke(playerDamage, opponentDamage, CurrentResult);
    }
}
