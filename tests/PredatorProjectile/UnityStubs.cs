// Minimal host for actual projectile Update arithmetic; not a Unity physics/lifecycle test.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class ScriptableObject { }
    public class Material { }
    public class Transform { public Vector3 position; }
    public class GameObject
    {
        public static readonly List<GameObject> All = new List<GameObject>();
        public readonly Transform transform = new Transform();
        public bool destroyed;
        public object lastComponent;
        public GameObject(string name) { All.Add(this); }
        public T AddComponent<T>() where T : new()
        {
            var component = new T();
            if (component is MonoBehaviour mono) mono.gameObject = this;
            lastComponent = component;
            if (component is CarnivoreProjectile projectile) LatestProjectile = projectile;
            return component;
        }
        public static CarnivoreProjectile LatestProjectile;
    }
    public class MonoBehaviour
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        protected static void Destroy(GameObject obj) { obj.destroyed = true; }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public Vector2 normalized { get { float n = (float)Math.Sqrt(x*x+y*y); return n > 0 ? new Vector2(x/n,y/n) : new Vector2(); } }
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x,v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x,v.y,0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x*b,a.y*b);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x,-a.y);
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 operator *(Vector3 a,float b) => new Vector3(a.x*b,a.y*b,a.z*b);
    }
    public struct Color { public static Color white => new Color(); public static Color gray => new Color(); }
    public class LineRenderer
    {
        public Material sharedMaterial; public bool useWorldSpace, loop;
        public int positionCount, sortingOrder; public float widthMultiplier;
        public Color startColor,endColor;
        public void SetPosition(int i,Vector3 p) { }
    }
    public static class Time { public static float deltaTime; }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Cos(float x)=>(float)Math.Cos(x);
        public static float Sin(float x)=>(float)Math.Sin(x);
    }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int n) { } }
    public sealed class CreateAssetMenuAttribute : Attribute { public string fileName,menuName; }
    public sealed class MinAttribute : Attribute { public MinAttribute(float n) { } }
}
public sealed class AnimalBody
{
    public static readonly List<AnimalBody> All = new List<AnimalBody>();
    public UnityEngine.Transform transform = new UnityEngine.Transform();
    public AnimalResources Resources = new AnimalResources(100);
    public SpeciesDefinition Species = new SpeciesDefinition();
    public string SpeciesId => Species.speciesId;
    public float CollisionRadius = 1;
    public bool IsAlive => !Resources.IsDead;
    public static void QuerySpatial(UnityEngine.Vector2 a,UnityEngine.Vector2 b,float padding,bool radius,List<AnimalBody> result)
    { result.Clear(); result.AddRange(All); }
    public double Hit(double power,CarnivoreAnimal owner,string species)
        => Resources.Hit(power,species,SpeciesId,out _);
}
public sealed class CarnivoreAnimal
{
    public AnimalBody Body = new AnimalBody();
    public UnityEngine.Transform transform => Body.transform;
    public int hitBirthEvents;
    public void OnAttackHit() { hitBirthEvents++; }
}
