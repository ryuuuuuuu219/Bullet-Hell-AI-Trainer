using System.Collections.Generic;
using UnityEngine;

namespace Script
{
    // Non-ship entities can supply their representative position/velocity and disable on death.
    public sealed class WeaponTarget : MonoBehaviour
    {
        private static readonly List<WeaponTarget> active = new List<WeaponTarget>();
        public static IReadOnlyList<WeaponTarget> Active => active;
        public WeaponTargetCategory category = WeaponTargetCategory.Ship;
        public bool friend;
        public Vector2 velocity;
        [SerializeField, Range(0, .99999f)] private float evasion;
        public float Evasion
        {
            get => float.IsNaN(evasion) || float.IsInfinity(evasion) ? 0 : Mathf.Clamp(evasion, 0, .99999f);
            set => evasion = float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, 0, .99999f);
        }
        public BattleshipInstance Ship { get; private set; }
        public bool Alive => isActiveAndEnabled && (Ship == null || Ship.IsAlive);
        public Vector2 Position => Ship != null ? Ship.CorePosition : (Vector2)transform.position;
        public Vector2 Velocity => Ship != null ? Ship.TranslationVelocity : velocity;
        public bool IsFriend => Ship != null && Ship.TryGetComponent(out IFF iff) ? iff.IsFriend : friend;
        private void Awake() => Ship = GetComponent<BattleshipInstance>();
        private void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        private void OnDisable() => active.Remove(this);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();
    }
}
