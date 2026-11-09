using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellarSystem.Editor
{
    public static class MapSetup
    {
        public const string ScenePath = "Assets/StellarSystem/Scenes/StellarSystem.unity";

        [MenuItem("戦艦TPS/恒星系マップを開く")]
        public static void OpenMap()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else CreateScene();
        }

        private static void CreateScene()
        {
            Directory.CreateDirectory("Assets/StellarSystem/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Stellar System").AddComponent<StellarSystemMap>();
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.Refresh();
        }

        // Run in an isolated validation project; does not change the user's current scene/build settings.
        public static void BuildAndValidate()
        {
            foreach (double mass in new[] { .000001, .000003, .00005, .001, .015 })
            {
                double mu=mass/(1+mass);
                for(int point=1;point<=3;point++)
                {
                    var p=OrbitMath.Lagrange(1,mass,point);
                    Assert(Math.Abs(OrbitMath.Residual(p.X-mu,mu))<1e-9,"Collinear equilibrium");
                    Assert(point==1 ? p.X>0&&p.X<1 : point==2 ? p.X>1 : p.X<0,"Point ordering");
                }
                var l4=OrbitMath.Lagrange(1,mass,4);var l5=OrbitMath.Lagrange(1,mass,5);
                Assert(Math.Abs(l4.Length-1)<1e-12 && Math.Abs((l4-new Position2D(1,0)).Length-1)<1e-12,"L4 equilateral distances");
                Assert(l4.Y>0&&l5.Y<0&&Math.Abs(l4.Y+l5.Y)<1e-12,"L4/L5 orientation");
            }
            CreateScene();
            var map=UnityEngine.Object.FindFirstObjectByType<StellarSystemMap>();
            map.Rebuild();
            for(int i=0;i<map.planets.Length;i++)
            {
                var p=map.planets[i];double period=OrbitMath.Period(p.orbitAU,map.starMassSolar,p.massSolar);
                foreach(double day in new[] {0d,1d,1000d,1000000d})
                {
                    var position=map.PlanetPosition(i,day);
                    Assert(Math.Abs(position.Length-p.orbitAU)<1e-10,"Circular radius after time acceleration");
                    Assert((position-map.PlanetPosition(i,day+period)).Length<1e-8,"Period wrap");
                    double dt=.001;
                    var derivative=(map.PlanetPosition(i,day+dt)-map.PlanetPosition(i,day-dt))*(.5/dt);
                    Assert((derivative-map.PlanetVelocity(i,day)).Length<1e-8,"Velocity consistency");
                }
            }
            for(int i=0;i<map.fortresses.Length;i++)
            {
                var f=map.fortresses[i];
                var initial=map.FortressPosition(i,0)-map.PlanetPosition(f.planetIndex,0);
                var later=map.FortressPosition(i,1000)-map.PlanetPosition(f.planetIndex,1000);
                Assert(Math.Abs(initial.Length-later.Length)<1e-10,"Fortress follows rotating frame");
            }
            bool rejected=false;
            try {OrbitMath.Period(-1,1,.001);}catch(ArgumentOutOfRangeException){rejected=true;}
            Assert(rejected,"Reject invalid orbit");
            AssetDatabase.SaveAssets();
            File.WriteAllText("validation-result.txt","PASS: Unity compilation; circular orbit radius/period/velocity; L1-L3 equilibrium; L4/L5 geometry; fortress co-rotation; invalid radius; scene creation.\n");
            Debug.Log("STELLAR_SYSTEM_VALIDATION_PASS");
        }
        private static void Assert(bool value,string message)
        {if(!value)throw new Exception("Validation failed: "+message);}
    }
}
