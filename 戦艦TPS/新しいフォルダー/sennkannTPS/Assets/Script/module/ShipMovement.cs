using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Script
{
    public enum ShipThrottle { ReverseSlow, Stop, AheadSlow, AheadFull }

    // A fixed construction snapshot; damage never changes these baseline values.
    [DefaultExecutionOrder(30)]
    public sealed class ShipMovement : MonoBehaviour
    {
        public bool playerControlled;
        public float halfExtent = 30000;
        public Vector2 gravityCenter;
        public float gravitySpeed;
        public ShipPerformance Performance { get; private set; }
        public ShipThrottle Command { get; private set; } = ShipThrottle.Stop;
        public ShipThrottle AppliedThrottle { get; private set; } = ShipThrottle.Stop;
        public Vector2 Velocity { get; private set; }
        public float TurnInput { get; set; }
        public float PendingSeconds { get; private set; }
        private BattleshipInstance ship;
        public static float ThrottleFraction(ShipThrottle throttle) => throttle switch
        { ShipThrottle.ReverseSlow => -.25f, ShipThrottle.Stop => 0, ShipThrottle.AheadSlow => .25f, _ => 1 };

        public void Initialize(ShipPerformance performance, bool player)
        {
            Performance = performance;
            playerControlled = player;
            ship = GetComponent<BattleshipInstance>();
            Command = AppliedThrottle = ShipThrottle.Stop;
            TurnInput = PendingSeconds = 0;
            Velocity = Vector2.zero;
        }

        public void SetThrottle(ShipThrottle throttle)
        {
            if (!Enum.IsDefined(typeof(ShipThrottle), throttle)) throw new ArgumentOutOfRangeException(nameof(throttle));
            if (Command == throttle) return;
            Command = throttle;
            // New commands replace an outstanding command; delay is sampled at receipt.
            PendingSeconds = ship != null && ship.EmpActive ? .02f * Mathf.Clamp((float)ship.EmAccumulation, 0, 100) : 0;
            if (PendingSeconds == 0) AppliedThrottle = Command;
        }

        private void Update()
        {
            if (ship != null && !ship.IsAlive) return;
            var keyboard = Keyboard.current;
            if (playerControlled && keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                    SetThrottle((ShipThrottle)Mathf.Min(3, (int)Command + 1));
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                    SetThrottle((ShipThrottle)Mathf.Max(0, (int)Command - 1));
                if (keyboard.spaceKey.wasPressedThisFrame) SetThrottle(ShipThrottle.Stop);
                TurnInput = (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1 : 0) -
                    (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1 : 0);
            }
            Tick(Time.deltaTime);
        }

        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float effectiveTurn = Performance.MaximumTurnSpeed;
            bool emp = ship != null && ship.EmpActive;
            if (emp) effectiveTurn *= 1 - Mathf.Clamp((float)ship.EmAccumulation, 0, 100) / 100f;
            // Split a long frame at the delayed command boundary.
            float oldDuration = emp ? Mathf.Min(seconds, PendingSeconds) : 0;
            if (oldDuration > 0) Advance(oldDuration, effectiveTurn);
            PendingSeconds = Mathf.Max(0, PendingSeconds - seconds);
            if (!emp || PendingSeconds == 0) { AppliedThrottle = Command; PendingSeconds = 0; }
            if (seconds > oldDuration) Advance(seconds - oldDuration, effectiveTurn);
        }

        private void Advance(float seconds, float turn)
        {
            transform.Rotate(0, 0, Mathf.Clamp(TurnInput, -1, 1) * turn * seconds);
            Vector2 position = transform.position;
            Vector2 gravity = (gravityCenter - position).normalized * gravitySpeed;
            Velocity = (Vector2)transform.up * (Performance.MaximumSpeed * ThrottleFraction(AppliedThrottle)) + gravity;
            Vector2 next = position + Velocity * seconds;
            float bound = Mathf.Max(0, halfExtent);
            transform.position = new Vector3(Mathf.Clamp(next.x, -bound, bound), Mathf.Clamp(next.y, -bound, bound), transform.position.z);
        }
    }
}
