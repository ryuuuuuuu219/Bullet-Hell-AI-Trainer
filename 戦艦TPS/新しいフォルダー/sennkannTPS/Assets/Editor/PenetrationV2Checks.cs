using System;
using System.Collections.Generic;
using System.Reflection;
using Script;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PenetrationV2Checks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Hidden).Invoke(obj, args);
    private static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
    private static void Near(double actual, double expected, string text) => Check(Math.Abs(actual - expected) < .001, $"{text}: {actual} != {expected}");
    public static void RunInPlayMode()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        SessionState.SetBool("PenetrationV2.Pending", true);
        Install(); EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]
    private static void Install()
    {
        EditorApplication.update -= Tick;
        if (SessionState.GetBool("PenetrationV2.Pending", false)) EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        EditorApplication.isPaused = false;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!Application.isPlaying || Time.frameCount < 3) return;
        EditorApplication.update -= Tick; SessionState.SetBool("PenetrationV2.Pending", false);
        try { Run(); ScriptRegressionChecks.RunEffectsChecks(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static GameObject Obj(string name)
    { var go = new GameObject(name); objects.Add(go); return go; }
    private static BattleshipInstance Ship(bool friend, Vector2 position)
    {
        var ship = Obj("V2 ship").AddComponent<BattleshipInstance>();
        ship.transform.position = position;
        var iff = ship.gameObject.AddComponent(typeof(BattleshipInstance).Assembly.GetType("Script.IFF"));
        iff.GetType().GetField("IsFriend").SetValue(iff, friend);
        var parts = new List<BattleshipSection>();
        for (int i = 0; i < 3; i++)
        {
            var part = Obj("V2 part"); part.transform.SetParent(ship.transform, false);
            part.transform.localPosition = new Vector2(i == 1 ? 0 : 1000, i * 1000);
            if (i == 1) part.transform.localPosition = Vector2.zero;
            var section = part.AddComponent<BattleshipSection>();
            Call(section, "Initialize", ship, i, i == 0 ? BattleshipSectionRole.Core : BattleshipSectionRole.Armor, null);
            section.GetComponent<PolygonCollider2D>().SetPath(0, new[] {new Vector2(-2,-2),new Vector2(2,-2),new Vector2(2,2),new Vector2(-2,2)});
            parts.Add(section);
        }
        Call(parts[0], "AddNeighbor", parts[1]); Call(parts[0], "AddNeighbor", parts[2]);
        Call(parts[1], "AddNeighbor", parts[0]); Call(parts[2], "AddNeighbor", parts[0]);
        Call(ship,"Initialize",BattleshipClass.Frigate,3,3,1,parts,true);
        return ship;
    }
    private static ProtectionTestProjectile Probe(BattleshipInstance owner, BattleshipInstance target, Vector2 position,
        double power, BattleshipProjectile.Affect affect = BattleshipProjectile.Affect.AP, Vector2? velocity = null)
    {
        var probe = Obj("V2 probe").AddComponent<ProtectionTestProjectile>();
        probe.Initialize(null,position,velocity ?? Vector2.right * 1000,1,10,target,power,5,null,affect,20,new ProjectileEffectSettings(),owner);
        return probe;
    }
    private static int Explosions() => UnityEngine.Object.FindObjectsByType<ExplosionEffect>(FindObjectsSortMode.None).Length;
    private static void CheckOffsetBlast()
    {
        var source = Ship(true, new Vector2(-30000,0));
        var target = Ship(false, new Vector2(30000,0));
        target.transform.rotation = Quaternion.Euler(0,0,37);
        var parts = target.GetComponentsInChildren<BattleshipSection>();
        for (int i=1;i<=2;i++)
        {
            parts[i].transform.localPosition=Vector3.zero;
            float x=i*100;
            parts[i].GetComponent<PolygonCollider2D>().SetPath(0,new[] {new Vector2(x,-50),new Vector2(x+100,-50),new Vector2(x+100,50),new Vector2(x,50)});
            Near(Vector2.Distance(parts[i].WorldCenter,target.transform.TransformPoint(new Vector2(x+50,0))),0,"offset center with hull rotation");
        }
        Vector2 impact=target.transform.TransformPoint(new Vector2(100,0));
        typeof(ExplosionEffect).GetMethod("Detonate",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,
            new object[] {impact,300d,20d,3d,source,source.ShipClass,null,target,BattleshipProjectile.Affect.HE,null});
        Near(parts[1].CurrentHealth,80,"full blast at contact section");
        Near(parts[2].CurrentHealth,80,"full blast at adjacent section");
        parts[1].GetComponent<PolygonCollider2D>().SetPath(0,new[] {new Vector2(100,0),new Vector2(200,0),new Vector2(100,100)});
        Near(Vector2.Distance(parts[1].WorldCenter,target.transform.TransformPoint(new Vector2(400f/3,100f/3))),0,"triangle centroid");
        Debug.Log("HE_SECTION_CENTER_PASS: offset polygons, hull rotation, triangle centroid, blast damage 20 at both distances");
    }
    public static void Run()
    {
        var previousQueries = Physics2D.queriesHitTriggers;
        try
        {
            CheckOffsetBlast();
            foreach (BattleshipProjectile.Affect ammo in Enum.GetValues(typeof(BattleshipProjectile.Affect)))
                Near(ProjectileEffects.ExplosionRadius(20, ammo), ProjectileEffects.Explodes(ammo) ? 30 : 0,
                    "blast radius shares expiry explosion eligibility " + ammo);
            HESectionDamageCalculation.Run();
            Near(ProjectilePenetration.AfterArmor(50,5,25),4.5,"power times penetration");
            Near(ProjectilePenetration.AfterArmor(50,.5,25),0,"equality stops");
            Near(ProjectilePenetration.AfterArmor(50,.4,25),0,"lower stops");
            Near(ProjectilePenetration.AfterArmor(50,5,double.PositiveInfinity),0,"grazing stops");
            var source = Ship(true,new Vector2(-20000,0));
            var enemy = Ship(false,new Vector2(20000,0));
            var armor = enemy.GetComponentsInChildren<BattleshipSection>()[1];
            armor.ArmorThickness = 250;
            object[] hit = {50d,5d,Vector2.zero,Vector2.right,Vector2.left,Vector2.zero};
            Call(armor,"HitProjectile",hit);
            Near(armor.CurrentHealth,50,"blocked impact still damages HP");
            Near((double)hit[1],0,"blocked impact zeros penetration");
            Call(armor,"Recover",50d);
            armor.ArmorThickness = 25; armor.ArmorInterception = 1000;
            hit[1]=5d;
            Near((double)Call(armor,"HitProjectile",hit),50,"power unchanged on passage");
            Near((double)hit[1],4.5,"legacy interception ignored");

            Physics2D.queriesHitTriggers = false;
            var a = Probe(source,null,new Vector2(0,10000),50);
            var b = Probe(enemy,null,new Vector2(10,10000),20);
            Call(a,"SimulateStep",.02f);
            Near(a.RemainingPower,30,"swept bullet interception survivor");
            Near((double)Get(Get(b,"flight"),"Power"),0,"intercepted bullet consumed");
            Near(a.Penetration,5,"interception changes power only");
            var c = Probe(source,null,new Vector2(0,11000),20);
            var d = Probe(source,null,new Vector2(10,11000),20);
            Call(c,"SimulateStep",.02f); Near(c.RemainingPower,20,"friendly bullets pass");
            var e = Probe(source,null,new Vector2(0,12000),20);
            var f = Probe(enemy,null,new Vector2(10,12000),20);
            Call(e,"SimulateStep",.02f); Near(e.RemainingPower,0,"equal bullets both intercepted");
            Near((double)Get(Get(f,"flight"),"Power"),0,"equal second bullet intercepted");
            var beam = Probe(enemy,null,new Vector2(10,13000),20,BattleshipProjectile.Affect.Plasma);
            var g = Probe(source,null,new Vector2(0,13000),50);
            Call(g,"SimulateStep",.02f); Near(g.RemainingPower,50,"plasma cannot intercept");
            Check(!beam.GetComponent<CircleCollider2D>().enabled,"beam circle excluded");

            int before = Explosions();
            var he = Probe(source,null,new Vector2(0,14000),50,BattleshipProjectile.Affect.HE);
            Set(Get(he,"flight"),"FuseDistance",10d);
            Call(he,"SimulateStep",.1f);
            Near(he.transform.position.x,10,"fuse clamps long frame to flight distance");
            Check(Explosions()==before+1,"fuse explodes once");
            Call(he,"SimulateStep",.1f); Check(Explosions()==before+1,"no second detonation");

            // Defended target receives blast even with zero remaining penetration.
            Call(armor,"Recover",100d); armor.ArmorThickness=100000;
            var blast = typeof(ExplosionEffect).GetMethod("Detonate",BindingFlags.Static|BindingFlags.NonPublic);
            blast.Invoke(null,new object[] {(Vector2)armor.transform.position+Vector2.left*10,20d,50d,0d,source,source.ShipClass,null,enemy,BattleshipProjectile.Affect.HE,null});
            Near(armor.CurrentHealth,50,"HE full power and armor bypass");
            Call(armor,"Recover",100d);
            before=Explosions();
            var incoming = Probe(source,enemy,(Vector2)armor.transform.position+Vector2.left*10,50,BattleshipProjectile.Affect.HE);
            var interceptor = Probe(enemy,null,(Vector2)armor.transform.position+Vector2.left*8,100);
            Call(incoming,"SimulateStep",.001f);
            Check(Explosions()==before+1,"intercepted HE explodes");
            Near(armor.CurrentHealth,100 - ProjectileEffects.BlastPower(50,
                Vector2.Distance(incoming.transform.position,armor.transform.position)),"intercepted HE uses pre-contact power");
            Call(incoming,"SimulateStep",.1f); Check(Explosions()==before+1,"interception blast once");
            UnityEngine.Object.DestroyImmediate(interceptor.gameObject);
            var direct=Probe(source,enemy,(Vector2)armor.transform.position+Vector2.left*10,20,BattleshipProjectile.Affect.HE);
            Set(Get(direct,"flight"),"FuseDistance",30d);
            before=Explosions(); Call(direct,"SimulateStep",.1f);
            Check(Explosions()==before+1,"direct contact explodes before fuse");
            Call(direct,"SimulateStep",.1f);
            Check(Explosions()==before+1,"direct contact and fuse never duplicate AoE");

            foreach (var affect in new[] {BattleshipProjectile.Affect.HEAT,BattleshipProjectile.Affect.HESH})
            {
                var p=Probe(source,null,new Vector2(0,15000),30,affect);
                var q=Probe(enemy,null,new Vector2(2,15000),40);
                before=Explosions(); Call(p,"SimulateStep",.001f);
                Check(Explosions()==before+1,$"{affect} belongs to HE family");
                UnityEngine.Object.DestroyImmediate(q.gameObject);
            }
            // Exercise the real firing adapter: the muzzle, not the hull center, sets the fuse.
            var muzzle=source.GetComponentsInChildren<BattleshipSection>()[2];
            var runtime=typeof(MvpWeaponCatalog).GetMethod("CreateRuntime",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{1,0,1});
            runtime.GetType().GetField("Affect").SetValue(runtime,BattleshipProjectile.Affect.HE);
            runtime.GetType().GetField("ExplosionRadius").SetValue(runtime,75d);
            Vector2 aim=muzzle.FiringPosition+Vector2.right*50;
            typeof(BattleshipProjectile).GetMethod("Fire",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{muzzle,runtime,Vector2.right,(Vector2?)aim,null});
            var fired=UnityEngine.Object.FindFirstObjectByType<BattleshipProjectile>();
            objects.Add(fired.gameObject);
            Near((double)Get(Get(fired,"flight"),"FuseDistance"),50,"real muzzle determines fuse");
            before=Explosions(); Call(fired,"Advance",1f);
            Near(Vector2.Distance(fired.transform.position,aim),0,"actual fired HE detonates at fuse distance");
            Check(Explosions()==before+1,"actual fire adapter explodes once");
            Debug.Log("PENETRATION_V2_PASS: armor boundaries, HP before gate, unchanged power, interception, IFF, beams, HE armor bypass, fuse and one-shot explosions.");
        }
        finally
        {
            Physics2D.queriesHitTriggers=previousQueries;
            foreach(var go in objects) if(go!=null) UnityEngine.Object.DestroyImmediate(go);
            objects.Clear();
            foreach(var effect in UnityEngine.Object.FindObjectsByType<ExplosionEffect>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
        }
    }
}
