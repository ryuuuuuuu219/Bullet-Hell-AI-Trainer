using TMPro;
using UnityEngine;

public class NextStep : MonoBehaviour
{
    public TextMeshProUGUI phaselabel;
    public TextMeshProUGUI buttom;
    public BulletManager bm;
    public BoardCellInput cellInput;
    public CardSelect cardSelect;
    public CardPlacement placement;
    public GameObject resetButton;
    public BattleJudge judge;
    public TextMeshProUGUI resultLabel;
    public CpuPlayer cpu;

    enum Phase
    {
        Config,
        Battle,
        Judge
    }

    Phase currentPhase = Phase.Config;

    private void Start()
    {
        if (bm == null) bm = GetComponent<BulletManager>();
        if (cellInput == null) cellInput = GetComponent<BoardCellInput>();
        if (cardSelect == null) cardSelect = GetComponent<CardSelect>();
        if (placement == null) placement = GetComponent<CardPlacement>();
        if (judge == null) judge = GetComponent<BattleJudge>();
        if (cpu == null) cpu = GetComponent<CpuPlayer>();
        if (phaselabel != null) phaselabel.text = "配置フェーズ";
        if (buttom != null) buttom.text = "次へ";
        if (resetButton != null) resetButton.SetActive(true);
        if (resultLabel != null)
        {
            resultLabel.enableAutoSizing = true;
            resultLabel.fontSizeMin = 36f;
            resultLabel.fontSizeMax = 200f;
            resultLabel.raycastTarget = false;
            resultLabel.gameObject.SetActive(false);
        }
    }

    int NobodyTurns = 0;
    public void Next()
    {
        if (currentPhase == Phase.Judge)
        {
            ResetGame();
            return;
        }
        if (currentPhase == Phase.Config)
        {
            currentPhase = Phase.Battle;
            if (cpu != null) cpu.PlaceHand();
            if (phaselabel != null) phaselabel.text = "戦闘フェーズ";
            if (buttom != null) buttom.text = "次ターンへ";
            if (cellInput != null) cellInput.SetPlacementEnabled(false);
            if (resetButton != null) resetButton.SetActive(false);
            return;
        }
        if (bm != null && !bm.ResumeTurn())
        {
            NobodyTurns++;
        }
        else
        {
            NobodyTurns = 0;
        }
        if (NobodyTurns >= 2)
        {
            currentPhase = Phase.Judge;
            var result = judge != null ? judge.Judge() : BattleJudge.Result.Undecided;
            if (phaselabel != null)
            {
                switch (result)
                {
                    case BattleJudge.Result.PlayerWin:
                        phaselabel.text = "判定フェーズ：勝利";
                        break;
                    case BattleJudge.Result.OpponentWin:
                        phaselabel.text = "判定フェーズ：敗北";
                        break;
                    case BattleJudge.Result.Draw:
                        phaselabel.text = "判定フェーズ：引き分け";
                        break;
                    default:
                        phaselabel.text = "判定フェーズ";
                        break;
                }
            }
            if (resultLabel != null)
            {
                switch (result)
                {
                    case BattleJudge.Result.PlayerWin:
                        resultLabel.text = "勝利";
                        break;
                    case BattleJudge.Result.OpponentWin:
                        resultLabel.text = "敗北";
                        break;
                    case BattleJudge.Result.Draw:
                        resultLabel.text = "引き分け";
                        break;
                    default:
                        resultLabel.text = "判定中";
                        break;
                }
                resultLabel.gameObject.SetActive(true);
            }
            if (buttom != null) buttom.text = "リセット";
            if (resetButton != null) resetButton.SetActive(true);
        }
    }

    public void ResetGame()
    {
        if (bm == null) bm = GetComponent<BulletManager>();
        if (cellInput == null) cellInput = GetComponent<BoardCellInput>();
        if (cardSelect == null) cardSelect = GetComponent<CardSelect>();
        if (placement == null) placement = GetComponent<CardPlacement>();
        if (judge == null) judge = GetComponent<BattleJudge>();
        if (cpu == null) cpu = GetComponent<CpuPlayer>();
        if (cpu != null) cpu.ClearPlaced();
        if (bm != null) bm.ClearBullets();
        if (placement != null) placement.ClearPlaced();
        if (judge != null) judge.ResetDamage();
        if (cardSelect != null) cardSelect.ResetCards();
        currentPhase = Phase.Config;
        NobodyTurns = 0;
        if (phaselabel != null) phaselabel.text = "配置フェーズ";
        if (buttom != null) buttom.text = "次へ";
        if (cellInput != null) cellInput.SetPlacementEnabled(true);
        if (resetButton != null) resetButton.SetActive(true);
        if (resultLabel != null) resultLabel.gameObject.SetActive(false);
    }
}
