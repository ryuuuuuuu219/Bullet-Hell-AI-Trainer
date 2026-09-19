using TMPro;
using UnityEngine;

public class NextStep : MonoBehaviour
{
    public TextMeshProUGUI phaselabel;
    public TextMeshProUGUI buttom;
    public BulletManager bm;
    public BoardCellInput cellInput;

    enum Phase
    {
        Config,
        Battle,
    }

    Phase currentPhase = Phase.Config;

    private void Start()
    {
        if (bm == null) bm = GetComponent<BulletManager>();
        if (cellInput == null) cellInput = GetComponent<BoardCellInput>();
        if (phaselabel != null) phaselabel.text = "配置フェーズ";
        if (buttom != null) buttom.text = "次へ";
    }

    public void Next()
    {
        if (currentPhase == Phase.Config)
        {
            currentPhase = Phase.Battle;
            if (phaselabel != null) phaselabel.text = "戦闘フェーズ";
            if (buttom != null) buttom.text = "次ターンへ";
            if (cellInput != null) cellInput.SetPlacementEnabled(false);
            return;
        }
        if (bm != null) bm.ResumeTurn();
    }
}
