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
        Call(ship, "Initialize", BattleshipAnimalClass.Frigate, 3, 3, 1, sections);
        return ship;
    }
    public static void Run()
    {
        try
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
            Call(parts[0], "Hit", 30d, 1d);
            Check(parts[0].CurrentHealth == 90 && parts[1].CurrentHealth == 50 && near.CoreHealth == 230,
                "Core distributes damage once");
            near.transform.position = new Vector3(2000, 0); far.gameObject.SetActive(false);
            Call(ally, "SelectAttackTarget");
            Check(Field(ally, "attackTarget") == null, "Clear missing target");
            Check(!ObjectManager.EnemyObjects.Contains(far.gameObject), "Unregister disabled target");
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/TilingTest.unity");
            foreach (var root in scene.GetRootGameObjects())
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0, "Scene script reference");
            var test = UnityEngine.Object.FindFirstObjectByType<TilingTestScene>();
            Check(test != null, "Tiling component resolved");
            test.GenerateForValidation();
            test.GetType().GetField("shipSp", Private).SetValue(test, 12f);
            var drawing = (GameObject)Field(test, "drawing");
            var bullet = new GameObject("Resize sentinel"); bullet.transform.SetParent(drawing.transform);
            Call(test, "UpdateView");
            Check((float)Field(test, "shipSp") == 12f && bullet != null && ReferenceEquals(Field(test, "drawing"), drawing),
                "Resize preserves simulation state");
            foreach (var entry in EditorBuildSettings.scenes)
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) != null, "Build scene exists");
            Debug.Log("SCRIPT_REGRESSION_CHECKS_PASS");
        }
        finally
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            created.Clear();
        }
    }
}
