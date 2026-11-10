using System;
using System.Collections.Generic;
using System.Reflection;
using Script;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with -batchmode -executeMethod ScriptRegressionChecks.Run -quit.
public static class ScriptRegressionChecks
{
    private static readonly List<GameObject> created = new List<GameObject>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static object Field(object target, string name)
        => target.GetType().GetField(name, Private).GetValue(target);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static BattleshipAnimalInstance Ship(bool friend, float x)
    {
        var go = new GameObject("Regression ship"); created.Add(go);
        go.transform.position = new Vector3(x, 0);
        var ship = go.AddComponent<BattleshipAnimalInstance>();
        var iff = go.AddComponent(typeof(BattleshipAnimalInstance).Assembly.GetType("Script.IFF"));
        iff.GetType().GetField("IsFriend").SetValue(iff, friend);
        iff.GetType().GetMethod("RegistryToManager").Invoke(iff, null);
        var sections = new List<BattleshipSection>();
        for (int i = 0; i < 3; i++)
        {
            var part = new GameObject("Part"); part.transform.SetParent(go.transform);
            var section = part.AddComponent<BattleshipSection>();
            Call(section, "Initialize", ship, i, i == 0 ? BattleshipSectionRole.Core : BattleshipSectionRole.Turret, null);
            sections.Add(section);
        }
        Call(sections[0], "AddNeighbor", sections[1]);
        Call(sections[0], "AddNeighbor", sections[2]);
        Call(ship, "Initialize", BattleshipAnimalClass.Frigate, 3, 3, 1, sections, false);
        return ship;
    }
    public static void Run() => RunChecks(false);
    public static void RunProtection() => RunChecks(true);

    // Batch entry point without -quit: wait for the actual Play Mode physics world.
    public static void RunInPlayMode()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TilingTest.unity");
        SessionState.SetBool("ScriptRegressionChecks.PlayMode", true);
        InstallPlayModeCheck();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void InstallPlayModeCheck()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode ||
            !SessionState.GetBool("ScriptRegressionChecks.PlayMode", false)) return;
        SessionState.SetBool("ScriptRegressionChecks.PlayMode", false);
        try { RunChecks(false); EditorApplication.Exit(0); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static void RunChecks(bool protectionOnly)
    {
        try
        {
            if (!protectionOnly)
            {
            ProjectilePenetration.RemainingPower(40, 1, 0, 100, out double hp);
            Check(hp == 60, "Penetration damage");
            var playerObject = new GameObject("Regression player"); created.Add(playerObject);
            var player = playerObject.AddComponent(typeof(BattleshipAnimalInstance).Assembly.GetType("Script.Player"));
            Call(player, "Hit", 40d, 1d);
            Check((double)player.GetType().GetField("Health").GetValue(player) == 60, "Player damage");
            var ally = Ship(true, 0); var near = Ship(false, 100); var far = Ship(false, 500);
            Call(ally, "SelectAttackTarget");
            Check(ReferenceEquals(Field(ally, "attackTarget"), near), "Closest target component");
            ally.targetSelectionMode = BattleshipAnimalInstance.TargetSelectionMode.Farthest;
            Call(ally, "SelectAttackTarget");
            Check(ReferenceEquals(Field(ally, "attackTarget"), far), "Farthest target");
            Call(near, "SelectAttackTarget");
            Check(ReferenceEquals(Field(near, "attackTarget"), ally), "Enemy acquires ally");
            var parts = (List<BattleshipSection>)Field(near, "sections");
            Call(parts[1], "Hit", 40d, 1d);
            Check(parts[1].CurrentHealth == 60 && near.CoreHealth == 260, "Damage applied exactly once");
            ally.targetSelectionMode = BattleshipAnimalInstance.TargetSelectionMode.Covered;
            Call(ally, "SelectAttackTarget");
            Check(ReferenceEquals(Field(ally, "attackTarget"), near), "Lowest health target");
            Call(parts[0], "Hit", 30d, .5d);
            Check(parts[0].CurrentHealth == 90 && parts[1].CurrentHealth == 50 && near.CoreHealth == 230,
                "Core distributes damage once");
            near.transform.position = new Vector3(2000, 0); far.gameObject.SetActive(false);
            Call(ally, "SelectAttackTarget");
            Check(Field(ally, "attackTarget") == null, "Clear missing target");
            Check(!ObjectManager.EnemyObjects.Contains(far.gameObject), "Unregister disabled target");
            }
            CheckProtectionProjectile();
            CheckHEExplosion();
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();

            var scene = Application.isPlaying ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene("Assets/Scenes/TilingTest.unity");
            foreach (var root in scene.GetRootGameObjects())
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0, "Scene script reference");
            var test = UnityEngine.Object.FindFirstObjectByType<TilingTestScene>();
            Check(test != null, "Tiling component resolved");
            test.GenerateForValidation();
            var target = (BattleshipAnimalInstance)Field(test, "testTarget");
            Check(target != null && target.IsAlive && target.LivingSectionCount == test.DisplayedTileCount,
                "Tiling sections connected to combat target");
            foreach (var section in target.GetComponentsInChildren<BattleshipSection>())
                Check(section.Owner == target && !section.GetComponent<PolygonCollider2D>().isTrigger,
                    "Section owner and polygon collider");
            var originalPosition = target.transform.position;
            Call(target, "Update");
            Check(target.transform.position == originalPosition, "Test target stays stationary");
            test.GetType().GetField("shipSp", Private).SetValue(test, 12f);
            var drawing = (GameObject)Field(test, "drawing");
            var bullet = new GameObject("Resize sentinel"); bullet.transform.SetParent(drawing.transform);
            Call(test, "UpdateView");
            Check((float)Field(test, "shipSp") == 12f && bullet != null && ReferenceEquals(Field(test, "drawing"), drawing),
                "Resize preserves simulation state");
            var targetSections = (List<BattleshipSection>)Field(target, "sections");
            var armor = targetSections.Find(section => section.Role == BattleshipSectionRole.Armor);
            Check(armor != null, "Armor section for hit feedback");
            foreach (double blockedPower in new[] { 40d, 50d })
            {
                object[] blockedArgs = { blockedPower, 1d, Vector2.zero, Vector2.right, Vector2.left, Vector2.zero };
                double remaining = (double)Call(armor, "HitProjectile", blockedArgs);
                Check(remaining == 0 && armor.CurrentHealth == 100d && (double)blockedArgs[1] == 1d && (Vector2)blockedArgs[5] == Vector2.zero,
                    "Armor blocks lower and equal power without HP damage");
            }
            ((System.Collections.IList)Field(test, "floatingHits")).Clear();
            Call(armor, "HitAt", 90d, 1d, new Vector2(12, 34));
            var properties = new MaterialPropertyBlock();
            foreach (var visual in armor.GetComponentsInChildren<Renderer>())
            {
                visual.GetPropertyBlock(properties);
                Check(Math.Abs(properties.GetFloat("_Opacity") - .1f) < 1e-6,
                    "HP ratio controls section, outline and marker opacity");
            }
            var floating = (System.Collections.IList)Field(test, "floatingHits");
            Check(floating.Count == 1, "One floating label per hit");
            var label = floating[0];
            Check((string)label.GetType().GetField("Text").GetValue(label) == "10（-90）/100",
                "Post-hit HP, actual damage and maximum HP text");
            Check((Vector2)label.GetType().GetField("Position").GetValue(label) == new Vector2(12, 34),
                "Floating text uses collision position");
            Call(armor, "Recover", 90d);
            armor.GetComponent<Renderer>().GetPropertyBlock(properties);
            Check(properties.GetFloat("_Opacity") == 1f, "Recovery restores opacity");
            Check(armor.Armor == 50d && armor.LastImpactAngle == 0f && armor.LastEffectiveArmor == 50d,
                "Default physical armor and perpendicular impact");
            Call(armor, "HitSurface", 90d, 1d, Vector2.zero,
                new Vector2(.5f, Mathf.Sqrt(3f) * .5f), Vector2.left, Vector2.zero);
            Check(Math.Abs(armor.LastImpactAngle - 60f) < .001f &&
                Math.Abs(armor.LastEffectiveArmor - 100d) < .001 && armor.CurrentHealth == 100d,
                "60 degree impact doubles effective thickness and absorbs damage");
            Call(armor, "HitSurface", 100d, 1d, Vector2.zero, Vector2.up, Vector2.left, Vector2.zero);
            Check(armor.LastImpactAngle == 90f && double.IsPositiveInfinity(armor.LastEffectiveArmor) &&
                armor.CurrentHealth == 100d, "Grazing impact has no NaN or HP damage");
            object[] impactArgs = { 90d, 1d, Vector2.zero, new Vector2(1, 1), Vector2.left, Vector2.zero };
            Call(armor, "HitSurface", impactArgs);
            Check((Vector2)impactArgs[5] == new Vector2(1, 1), "Outgoing direction is returned via out argument");
            Check(Math.Abs(armor.LastEffectiveArmor - 50 * Math.Sqrt(2)) < .001 &&
                Math.Abs(armor.CurrentHealth - 10d) < .001,
                "Penetration uses shared HP damage without subtracting armor");
            var core = ((List<BattleshipSection>)Field(target, "sections"))[0];
            Check(armor.Mode == ArmorMode.Basic, "Default armor mode remains Basic");
            armor.Mode = ArmorMode.ERA;
            Call(armor, "Recover", 90d);
            object[] eraArgs = { 60d, 1d, Vector2.zero, new Vector2(1, 1), new Vector2(-2, 0), Vector2.zero };
            Call(armor, "HitSurface", eraArgs);
            Check((Vector2)eraArgs[5] == new Vector2(-49, 1), "ERA adds normalized surface normal times 50");
            Check(armor.LastEffectiveArmor < 51d && Math.Abs(armor.CurrentHealth - 40d) < 1e-6,
                "ERA-adjusted direction determines passage before shared damage");
            foreach (double initialPenetration in new[] { .1d, .05d })
            {
                Call(armor, "Recover", 60d);
                object[] exhaustedArgs = { 60d, initialPenetration, Vector2.zero, Vector2.right, Vector2.left, Vector2.zero };
                double remaining = (double)Call(armor, "HitProjectile", exhaustedArgs);
                Check(remaining == 0d && (double)exhaustedArgs[1] == 0d &&
                    (Vector2)exhaustedArgs[5] == Vector2.zero && Math.Abs(armor.CurrentHealth - 40d) < 1e-6,
                    "Exhausted penetration applies full damage once and then stops");
            }
            core.Mode = ArmorMode.ERA;
            object[] coreArgs = { 1d, 1d, Vector2.zero, Vector2.right, Vector2.left, Vector2.zero };
            Call(core, "HitSurface", coreArgs);
            Check((Vector2)coreArgs[5] == Vector2.right, "ERA only applies to armor sections");
            Call(core, "Hit", 1000000d, 1d);
            Check(target != null && !target.IsAlive && target.LivingSectionCount == 0,
                "Destroyed test target retained for inspection");
            foreach (var renderer in core.GetComponentsInChildren<Renderer>())
                Check(!renderer.enabled, "Destroyed section outline/marker hidden");
            Call(test, "DrawFrame");
            var restored = (BattleshipAnimalInstance)Field(test, "testTarget");
            Check(restored != null && restored.IsAlive && restored.CoreHealth == restored.CoreHealthLimit,
                "Restore rebuilds healthy target");
            Check(((System.Collections.IList)Field(test, "floatingHits")).Count == 0,
                "Restore clears floating labels");
            foreach (var entry in EditorBuildSettings.scenes)
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) != null, "Build scene exists");
            Debug.Log(protectionOnly ? "PROTECTION_REGRESSION_CHECKS_PASS" : "SCRIPT_REGRESSION_CHECKS_PASS");
        }
        finally
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }
    }

    [MenuItem("戦艦TPS/検証/HE範囲ダメージ")]
    public static void RunHEChecks()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Play Modeで実行してください。");
        try { CheckHEExplosion(); Debug.Log("PASS: HE area damage"); }
        finally
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }
    }

    private static void CheckHEExplosion()
    {
        var ship = Ship(false, 20000);
        var parts = (List<BattleshipSection>)Field(ship, "sections");
        for (int i = 0; i < parts.Count; i++)
        {
            Call(parts[i], "Initialize", ship, i,
                i == 0 ? BattleshipSectionRole.Core : i == 1 ? BattleshipSectionRole.Armor : BattleshipSectionRole.Turret, null);
            parts[i].transform.localPosition = i == 0 ? new Vector3(0, 30) : new Vector3((i - 1) * 10, 0);
            parts[i].GetComponent<PolygonCollider2D>().SetPath(0, new[] {
                new Vector2(-2,-2), new Vector2(2,-2), new Vector2(2,2), new Vector2(-2,2) });
        }
        // Multiple colliders belonging to one section must not multiply damage.
        var extra = new GameObject("Extra HE collider");
        extra.transform.SetParent(parts[2].transform, false);
        extra.AddComponent<CircleCollider2D>().isTrigger = true;
        var go = new GameObject("HE regression probe"); created.Add(go);
        var projectile = go.AddComponent<ProtectionTestProjectile>();
        bool previousQueries = Physics2D.queriesHitTriggers;
        var previousSimulation = Physics2D.simulationMode;
        var reports = new List<string>();
        var previousEffects = new HashSet<ExplosionEffect>(UnityEngine.Object.FindObjectsByType<ExplosionEffect>(FindObjectsSortMode.None));
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.SyncTransforms();
            Physics2D.Simulate(.02f);
            Physics2D.queriesHitTriggers = false;
            projectile.Initialize(null, new Vector2(19990, 0), new Vector2(1000, 0),
                .1f, 15f, ship, 60d, 1d, reports.Add, BattleshipProjectile.Affect.HE, 15f);
            Call(projectile, "SimulateStep", .03f);
            Check(Math.Abs(parts[1].CurrentHealth - 40d) < 1e-6, "HE uses impact power before armor consumes the projectile");
            Check(parts[2].CurrentHealth == 40d, "Nearby section receives one independent blast hit despite duplicate colliders");
            Check(parts[0].CurrentHealth == 100d, "Section outside blast stays intact");
            Check(Math.Abs(ship.CoreHealth - 180d) < 1e-6, "Blast damage counted once in ship health");
            Check(projectile.RemainingPower == 0d && reports.Count == 1, "HE detonates once and stops");
            Call(projectile, "SimulateStep", .03f);
            Check(parts[2].CurrentHealth == 40d && reports.Count == 1, "HE cannot damage again before deferred destruction");
        }
        finally
        {
            Physics2D.queriesHitTriggers = previousQueries;
            Physics2D.simulationMode = previousSimulation;
            foreach (var effect in UnityEngine.Object.FindObjectsByType<ExplosionEffect>(FindObjectsSortMode.None))
                if (!previousEffects.Contains(effect)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
        }
    }

    private static void CheckProtectionProjectile()
    {
        var ship = Ship(false, 10000);
        var parts = (List<BattleshipSection>)Field(ship, "sections");
        for (int i = 0; i < parts.Count; i++)
        {
            Call(parts[i], "Initialize", ship, i,
                i == 0 ? BattleshipSectionRole.Core : i == 1 ? BattleshipSectionRole.Armor : BattleshipSectionRole.Turret, null);
            parts[i].transform.localPosition = i == 0 ? new Vector3(0, 30) : new Vector3((i - 1) * 10, 0);
            parts[i].GetComponent<PolygonCollider2D>().SetPath(0, new[] {
                new Vector2(-2,-2), new Vector2(2,-2), new Vector2(2,2), new Vector2(-2,2) });
            parts[i].GetComponent<PolygonCollider2D>().isTrigger = false;
        }
        var go = new GameObject("Regression probe"); created.Add(go);
        var projectile = go.AddComponent<ProtectionTestProjectile>();
        var material = new Material(Shader.Find("Aquarium/TilingTest"));
        bool previousQueries = Physics2D.queriesHitTriggers;
        var previousSimulation = Physics2D.simulationMode;
        var reports = new List<string>();
        try
        {
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.SyncTransforms();
            Physics2D.Simulate(.02f);
            Physics2D.queriesHitTriggers = false;
            projectile.Initialize(material, new Vector2(9990, 0), new Vector2(1000, 0),
                .1f, 15f, ship, 90d, 1d, reports.Add);
            Call(projectile, "SimulateStep", .01f);
            Check(Math.Abs(parts[1].CurrentHealth - 10d) < 1e-6 &&
                Math.Abs(projectile.RemainingPower - 81d) < 1e-6,
                $"Armor hit: HP={parts[1].CurrentHealth}, power={projectile.RemainingPower}, alive={ship.IsAlive}, contacts={((List<RaycastHit2D>)Field(projectile, "contacts")).Count}, position={projectile.transform.position}, armor={parts[1].transform.position}");
            Call(projectile, "SimulateStep", .001f);
            Check(parts[1].CurrentHealth == 10d && reports.Count == 1 && Math.Abs(projectile.Penetration - .9d) < 1e-9,
                "No repeated damage or penetration loss while overlapping");
            Call(projectile, "SimulateStep", .02f);
            Check(Math.Abs(parts[2].CurrentHealth - 19d) < 1e-6 && reports.Count == 2,
                "Swept collision reaches second section at high speed");
            Check(Math.Abs(ship.CoreHealth - 129d) < 1e-6, "Damage counted once per section");
            Check(parts[0].CurrentHealth == 100d, "Off-path section not hit");
            parts[1].Mode = ArmorMode.ERA;
            Call(parts[1], "Recover", 90d);
            var eraObject = new GameObject("ERA regression probe"); created.Add(eraObject);
            var eraProbe = eraObject.AddComponent<ProtectionTestProjectile>();
            eraProbe.Initialize(material, new Vector2(9990, 0), new Vector2(1000, 0),
                .1f, 15f, ship, 60d, 1d, reports.Add);
            Call(eraProbe, "SimulateStep", .03f);
            Check(eraProbe.Velocity == new Vector2(-1000, 0) && eraProbe.transform.position.x < 9990,
                "ERA changes actual flight and uses remaining frame time");
            Check(Math.Abs(parts[2].CurrentHealth - 19d) < 1e-6 && reports.Count == 3,
                "Old-path contacts are discarded after ERA deflection");
        }
        finally
        {
            Physics2D.queriesHitTriggers = previousQueries;
            Physics2D.simulationMode = previousSimulation;
            UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
