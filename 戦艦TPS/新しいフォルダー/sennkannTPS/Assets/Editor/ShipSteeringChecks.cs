using System;
using Script;
using UnityEditor;
using UnityEngine;

public static class ShipSteeringChecks
{
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); }
    private static void Near(float value, float expected, string message, float tolerance = .01f)
        => Check(Mathf.Abs(value - expected) <= tolerance, $"{message}: {value} != {expected}");

    [MenuItem("戦艦TPS/検証/操艦AI 7種類（工程04）")]
    public static void Run()
    {
        var hull = new GameObject("AI test hull");
        var target = new GameObject("AI test target");
        var replacement = new GameObject("AI replacement");
        try
        {
            var motor = hull.AddComponent<ShipMovement>();
            var ai = hull.AddComponent<ShipSteeringAI>();
            void Setup(AIType type, float heading = 0, float radius = 3000)
            {
                target.SetActive(true);
                hull.transform.position = Vector3.zero;
                hull.transform.rotation = Quaternion.Euler(0, 0, heading - 90);
                target.transform.position = new Vector3(3000, 0);
                motor.Initialize(new ShipPerformance(250, 25), false);
                ai.Configure(new ShipSteeringSettings { major = type, attackTarget = target.transform,
                    escortTarget = target.transform, orbitRadius = radius, orbitCenterPosition = new Vector2(3000, 0) });
            }
            void TargetAngle(float degrees) => target.transform.position =
                new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * 3000;
            Setup(AIType.C1); TargetAngle(2); ai.Tick(1); motor.Tick(1);
            Near(Mathf.DeltaAngle(0, hull.transform.eulerAngles.z + 90), 2, "C1 arrival limit");
            Near(motor.Velocity.magnitude, 250, "full speed");
            Setup(AIType.C1); TargetAngle(-90); ai.Tick(1); motor.Tick(1);
            Near(Mathf.DeltaAngle(0, hull.transform.eulerAngles.z + 90), -25, "clockwise sign and cap");
            Setup(AIType.C2); ai.Tick(.1f); Near(motor.TurnInput, 0, "LOS initializes without spike");
            TargetAngle(1); ai.Tick(.1f); Near(motor.TurnInput, 1, "LOS positive left");
            TargetAngle(-1); ai.Tick(.1f); Near(motor.TurnInput, -1, "LOS negative right");
            TargetAngle(-.9f); ai.Tick(.1f); Near(motor.TurnInput, 0, "LOS deadband");
            Setup(AIType.C2); TargetAngle(90); ai.Tick(.1f); Near(motor.TurnInput, 1, "90 degrees uses pursuit");
            Setup(AIType.C2, 180); TargetAngle(179); ai.Tick(1);
            TargetAngle(-179); ai.Tick(1); Near(ai.LosRate, 2, "LOS wraparound");

            Near(ShipSteeringAI.SelectOrbitAngle(2759, 3000), 105, "inside radius");
            Near(ShipSteeringAI.SelectOrbitAngle(2760, 3000), 90, "lower inclusive");
            Near(ShipSteeringAI.SelectOrbitAngle(3240, 3000), 90, "upper inclusive");
            Near(ShipSteeringAI.SelectOrbitAngle(3241, 3000), 75, "outside radius");
            Setup(AIType.TOr, -90); ai.Tick(.1f);
            Near(ai.OrbitDirection, 1, "CCW selected"); Near(motor.TurnInput, 0, "tangent");
            target.transform.position = new Vector3(6000, 0);
            ai.Tick(2.9f); Near(ai.HeldOrbitAngle, 90, "angle held before 3 seconds");
            ai.Tick(.1f); Near(ai.HeldOrbitAngle, 75, "angle refreshed at 3 seconds");
            target.transform.position = hull.transform.position; ai.Tick(.1f);
            Near(motor.TurnInput, 0, "coincident center holds heading"); Near(ai.OrbitDirection, 1, "coincident center preserves side");
            Check(motor.Command == ShipThrottle.AheadFull, "coincident center full ahead");
            target.transform.position = new Vector3(0, 3000); ai.Tick(.1f);
            Check(motor.TurnInput != 0, "moving center bearing updates without waiting 3 seconds");
            Setup(AIType.TOr, 90); ai.Tick(.1f); Near(ai.OrbitDirection, -1, "CW selected");
            Setup(AIType.TOr, -80); ai.Tick(.1f); Near(motor.TurnInput, 0, "10 degree deadband inclusive");
            Setup(AIType.Obs, -90, 2000); ai.Tick(.1f);
            float turn = motor.TurnInput; TargetAngle(90); ai.Tick(.1f);
            Near(motor.TurnInput, turn, "Obs center remains fixed when target moves");
            Setup(AIType.DF, -90, 3000); ai.Tick(.1f);
            target.transform.position = new Vector3(0, 3000); ai.Tick(.1f);
            Check(motor.TurnInput != 0, "DF follows escort current position");

            Setup(AIType.BS, 15); TargetAngle(90); ai.Tick(.1f);
            Check(!ai.BroadsideTurning, "BS 15 inclusive does not start");
            hull.transform.rotation = Quaternion.Euler(0, 0, -74); ai.Tick(.1f);
            Check(ai.BroadsideTurning && motor.TurnInput < 0, "BS starts beyond 15");
            hull.transform.rotation = Quaternion.Euler(0, 0, -80); ai.Tick(.1f);
            Check(ai.BroadsideTurning, "BS keeps turning at 10");
            hull.transform.rotation = Quaternion.Euler(0, 0, -82); ai.Tick(.1f);
            Check(!ai.BroadsideTurning, "BS stops at 8");
            motor.Tick(.1f); Near(motor.Velocity.magnitude, 62.5f, "BS quarter speed");
            Setup(AIType.AC); ai.Tick(1); motor.Tick(1); Near(hull.transform.position.magnitude, 0, "AC stopped");
            foreach (var type in new[] { AIType.TOr, AIType.Obs, AIType.DF })
            {
                Setup(type); ai.Tick(.1f); target.SetActive(false); ai.Tick(.1f);
                Check(ai.UsingFallback && ai.ActiveType == AIType.AC && motor.Command == ShipThrottle.Stop, $"{type} loss fallback");
            }
            Setup(AIType.TOr);
            ai.Configure(new ShipSteeringSettings { major = AIType.TOr, attackTarget = target.transform,
                fallback = AIType.C2, fallbackTarget = replacement.transform });
            replacement.transform.position = Vector3.up * 5000;
            target.SetActive(false); ai.Tick(.1f);
            Check(ai.ActiveType == AIType.C2 && motor.Command == ShipThrottle.AheadFull, "explicit fallback target");
            replacement.SetActive(false); ai.Tick(.1f);
            Check(ai.MissingTarget && motor.Command == ShipThrottle.Stop && motor.TurnInput == 0, "missing replacement safety stop");
            bool rejected = false;
            try { new ShipSteeringSettings { major = AIType.DF }.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "DF needs explicit radius");

            // Real common motor trajectories, without teleport/radius projection.
            foreach (var type in new[] { AIType.TOr, AIType.Obs, AIType.DF })
            {
                Setup(type, -90);
                float minimum = float.MaxValue, maximum = 0, accumulatedAngle = 0;
                float previous = 180;
                for (int i = 0; i < 12000; i++)
                {
                    ai.Tick(.02f); motor.Tick(.02f);
                    Vector2 offset = (Vector2)hull.transform.position - new Vector2(3000, 0);
                    float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                    accumulatedAngle += Mathf.DeltaAngle(previous, angle); previous = angle;
                    if (i > 3000) { minimum = Mathf.Min(minimum, offset.magnitude); maximum = Mathf.Max(maximum, offset.magnitude); }
                }
                Check(minimum > 2400 && maximum < 3600 && accumulatedAngle > 720, $"{type} bounded repeated orbit");
                Debug.Log($"STEERING_ORBIT: {type}, 240s, radius min={minimum:F2}, max={maximum:F2}, sweep={accumulatedAngle:F2}");
            }
            if (Application.isPlaying) CheckConstructedShip(target.transform);
            Debug.Log("SHIP_STEERING_CHECKS_PASS: seven types, sign/caps, orbit timing, hysteresis, target loss, trajectories.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hull);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(replacement);
        }
    }

    private static void CheckConstructedShip(Transform target)
    {
        var material = new Material(Shader.Find("Battleship/TilingTest"));
        BattleshipInstance ship = null;
        try
        {
            target.gameObject.SetActive(true);
            target.position = new Vector3(3000, 100);
            ship = ShipBuilder.BuildWithAI(new ShipBuildData(), material, Vector2.zero, 0, false,
                new ShipSteeringSettings { major = AIType.C1, attackTarget = target });
            var ai = ship.GetComponent<ShipSteeringAI>();
            var motor = ship.GetComponent<ShipMovement>();
            Check(ship.IsAlive && motor.enabled && !motor.playerControlled, "built ship AI integration");
            const System.Reflection.BindingFlags hidden = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(BattleshipInstance).GetMethod("AccumulateImpact", hidden).Invoke(ship, new object[] { 0d, 150d });
            ai.Tick(1); motor.Tick(1);
            Near(Mathf.DeltaAngle(0, ship.transform.eulerAngles.z + 90), 0, "EMP 100+ blocks AI turn");
            Near(motor.Velocity.magnitude, 0, "EMP delays AI full-ahead command");
            typeof(BattleshipInstance).GetField("emAccumulation", hidden).SetValue(ship, 50d);
            float expected = Mathf.Atan2(100, 3000) * Mathf.Rad2Deg;
            ai.Tick(1); motor.Tick(1);
            Near(Mathf.DeltaAngle(0, ship.transform.eulerAngles.z + 90), expected, "EMP-aware arrival limit");
            Debug.Log("SHIP_STEERING_BUILT_SHIP_PASS: builder, common motor, EMP delay and turn arrival limit.");
        }
        finally
        {
            if (ship != null) UnityEngine.Object.DestroyImmediate(ship.gameObject);
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    public static void RunBatch()
    {
        try { Run(); ScriptRegressionChecks.Run(); EditorApplication.Exit(0); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
