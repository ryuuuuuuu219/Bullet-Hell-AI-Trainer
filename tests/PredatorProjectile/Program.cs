using System;
using System.Reflection;
using UnityEngine;

static class Program
{
    static readonly MethodInfo Update = typeof(CarnivoreProjectile).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
    static void Check(bool test,string message) { if (!test) throw new Exception(message); }
    static void Equal(double a,double b,string message) => Check(Math.Abs(a-b)<0.0001,message+$": {a} != {b}");
    static double Power(CarnivoreProjectile bullet) => (double)typeof(CarnivoreProjectile).GetField("power",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bullet);
    static CarnivoreAnimal Shooter()
    {
        AnimalBody.All.Clear();
        var owner = new CarnivoreAnimal();
        owner.Body.Species = new SpeciesDefinition { speciesId="yoshi", pursuitSpeciesIds=new[] { "beetle" }, bulletPower=10, bulletPowerDecay=1, bulletSpeed=100 };
        AnimalBody.All.Add(owner.Body);
        return owner;
    }
    static AnimalBody Target(float x,bool reflect=false)
    {
        var body = new AnimalBody(); body.transform.position=new Vector3(x,0,0);
        body.Species.speciesId="beetle"; body.Species.reflectProjectiles=reflect;
        body.CollisionRadius=5; AnimalBody.All.Add(body); return body;
    }
    static CarnivoreProjectile Fire(CarnivoreAnimal owner)
    { CarnivoreProjectile.Fire(owner,new Vector2(1,0),null); return GameObject.LatestProjectile; }
    static void Step(CarnivoreProjectile bullet,float seconds)
    { Time.deltaTime=seconds; if(!bullet.gameObject.destroyed) Update.Invoke(bullet,null); }
    static void Main()
    {
        var owner=Shooter(); var reflector=Target(50,true); var bullet=Fire(owner);
        Step(bullet,1);
        Equal(reflector.Resources.Health,100,"reflection prevents damage");
        Equal(owner.Body.Resources.Health,90.74,"neutral reflection can hit original shooter at swept contact time");
        Check(bullet.gameObject.destroyed && owner.hitBirthEvents==0,"reflection is not an owned damaging hit");

        owner=Shooter(); reflector=Target(50,true); bullet=Fire(owner); AnimalBody.All.Remove(owner.Body);
        Step(bullet,1);
        Equal(bullet.transform.position.x,-20,"remaining frame travels reflected path");
        Equal(Power(bullet),9,"reflection preserves time decay and current power");
        Check(bullet.SpeciesId==null && !bullet.gameObject.destroyed,"neutral survives with remaining power");
        owner=Shooter(); reflector=Target(50,true); bullet=Fire(owner); AnimalBody.All.Remove(owner.Body);
        Step(bullet,0.5f); Step(bullet,0.5f);
        Equal(bullet.transform.position.x,-20,"reflection partition-independent position");
        Equal(Power(bullet),9,"reflection partition-independent decay");

        owner=Shooter(); var victim=Target(50); bullet=Fire(owner); Step(bullet,1);
        Equal(victim.Resources.Health,90.4,"damage includes flight decay");
        Check(owner.hitBirthEvents==1 && bullet.gameObject.destroyed,"one hit callback even when projectile consumed");
        Step(bullet,1); Check(owner.hitBirthEvents==1,"destroyed projectile never repeats hit");

        owner=Shooter(); owner.Body.Species.bulletPower=1; owner.Body.Species.bulletPowerDecay=1;
        victim=Target(120); bullet=Fire(owner); Step(bullet,2);
        Equal(victim.Resources.Health,100,"expired projectile cannot hit beyond lifetime");
        Check(bullet.gameObject.destroyed,"time-expired projectile removed");

        owner=Shooter(); owner.Body.Species.projectileRange=20; victim=Target(50); bullet=Fire(owner); Step(bullet,1);
        Equal(victim.Resources.Health,100,"range stops before distant target");
        Check(bullet.gameObject.destroyed,"range-expired projectile removed");
        owner=Shooter(); owner.Body.Species.pursuitSpeciesIds=Array.Empty<string>();
        victim=Target(50,true); bullet=Fire(owner); Step(bullet,1);
        Equal(victim.Resources.Health,100,"non-target takes no damage and does not reflect");
        Equal(bullet.transform.position.x,100,"non-target does not block projectile");
        Equal(Power(bullet),9,"non-target does not consume impact power");
        Check(bullet.SpeciesId=="yoshi" && owner.hitBirthEvents==0,"no reflection or birth on excluded target");
        Console.WriteLine("PASS actual projectile Update: reflection, shooter return, decay, frame splitting, hit callback, lifetime and range (stub host)");
    }
}
