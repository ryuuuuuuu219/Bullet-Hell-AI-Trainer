using UnityEngine;

namespace StellarSystem
{
    // Unity Physics の Collider ではなく、将来の遭遇を予測するための接触半径。
    // 既定値は両艦隊で各0.0025 AU、合計0.005 AU。
    [DisallowMultipleComponent]
    public sealed class EngageCollider : MonoBehaviour
    {
        [Min(0f)] public double radiusAU = 0.0025;
        public bool encounterEnabled = true;
    }
}
