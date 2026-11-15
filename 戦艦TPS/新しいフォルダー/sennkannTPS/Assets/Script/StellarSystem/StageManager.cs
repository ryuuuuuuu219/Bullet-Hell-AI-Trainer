using System;
using UnityEngine;

namespace StellarSystem
{
    public enum StageProgress { Unknown, Locked, Available, Cleared }

    // 恒星系マップの論理的な目的地。Transformの位置ではなく planetID / lagrangeID で識別する。
    // lagrangeID = -1 は惑星本体、1..5 は各L点。
    public sealed class StageManager : MonoBehaviour
    {
        [Min(1)] public int planetID = 1;
        [Range(-1, 5)] public int lagrangeID = -1;
        public bool isBase;
        public string displayName;
        public string enemyFaction;
        [TextArea(3, 6)] public string briefing;
        public StageProgress progress = StageProgress.Unknown;
        public bool allowSortie = true;

        public bool IsSelectable => !isBase;

        // 表示上のクリック判定は StellarSystemMap のGUIボタンが担当する。
        // ここでは選択通知を責務として持つ。
        public void Select(Action<StageManager> onSelected)
        {
            if (!IsSelectable || onSelected == null) return;
            onSelected(this);
        }
    }
}
