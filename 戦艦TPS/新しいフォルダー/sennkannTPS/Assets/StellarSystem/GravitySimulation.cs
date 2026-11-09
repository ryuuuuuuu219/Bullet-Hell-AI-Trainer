using System;
using System.Collections.Generic;

namespace StellarSystem
{
    public sealed class GravityBody
    {
        public readonly double Radius, Mass, Phase, Period;
        public GravityBody(double radius, double mass, double phase, double starMass)
        { Radius=radius; Mass=mass; Phase=phase; Period=OrbitMath.Period(radius,starMass,mass); }
        public Position2D Position(double day) => OrbitMath.Rotate(new Position2D(Radius,0),OrbitMath.Phase(day,Period,Phase));
    }
    public struct TrailSample
    {
        public Position2D Position;
        public double Day;
        public TrailSample(Position2D position,double day) {Position=position;Day=day;}
    }
    public sealed class TestParticle
    {
        public Position2D Position, Velocity;
        public int Planet, TargetPoint, Switches;
        public bool Controlled, Active=true;
        public string Status="飛行中";
        public readonly Queue<TrailSample> Trail=new Queue<TrailSample>();
        internal double LastTrailDay;
    }

    // Star-relative, non-rotating frame. Test particles have no mass and do not interact.
    public sealed class GravitySimulation
    {
        public const double G=4*Math.PI*Math.PI/(365.25*365.25);
        public const int MaxParticles=48;
        public readonly GravityBody[] Bodies;
        public readonly double StarMass;
        public readonly List<TestParticle> Particles=new List<TestParticle>();
        public double Day {get; private set;}
        public bool TimeLimited {get; private set;}
        private readonly Position2D[,] lagrange;
        public GravitySimulation(double starMass,GravityBody[] bodies)
        {
            if(bodies==null||bodies.Length==0)throw new ArgumentException("No planets");
            StarMass=starMass;Bodies=bodies;
            lagrange=new Position2D[bodies.Length,5];
            for(int i=0;i<bodies.Length;i++)for(int j=0;j<5;j++)
                lagrange[i,j]=OrbitMath.Lagrange(starMass,bodies[i].Mass,j+1)*bodies[i].Radius;
        }
        public int SelectPlanet(Position2D position)
        {
            double radius=position.Length, best=double.PositiveInfinity;int result=0;
            for(int i=0;i<Bodies.Length;i++)
            {
                double delta=Math.Abs(radius-Bodies[i].Radius);
                if(delta<best){best=delta;result=i;}
            }
            return result;
        }
        public Position2D LPoint(int planet,int point,double day)
            => OrbitMath.Rotate(lagrange[planet,point-1],OrbitMath.Phase(day,Bodies[planet].Period,Bodies[planet].Phase));
        public int NearestPoint(Position2D position,int planet,double day)
        {
            int point=1;double best=double.PositiveInfinity;
            for(int i=1;i<=5;i++)
            {double d=(LPoint(planet,i,day)-position).Length;if(d<best){best=d;point=i;}}
            return point;
        }
        public TestParticle Add(Position2D position,bool controlled)
        {
            if(Particles.Count>=MaxParticles)return null;
            int planet=SelectPlanet(position);
            var particle=new TestParticle {Position=position,Planet=planet,Controlled=controlled,
                TargetPoint=NearestPoint(position,planet,Day),Velocity=OrbitMath.Velocity(position,Bodies[planet].Period),LastTrailDay=Day};
            CheckCapture(particle,Day);
            particle.Trail.Enqueue(new TrailSample(position,Day));Particles.Add(particle);return particle;
        }
        public void Reset(){Day=0;Particles.Clear();TimeLimited=false;}
        public void Clear(){Particles.Clear();TimeLimited=false;}
        public Position2D Gravity(Position2D position,double day,int planet)
        {
            var body=Bodies[planet];var pp=body.Position(day);
            // The last term is the acceleration of the star due to the selected planet.
            // It is required for L-point equilibrium in star-relative coordinates.
            return Pull(position,StarMass)+Pull(position-pp,body.Mass)+Pull(pp,body.Mass);
        }
        private static Position2D Pull(Position2D relative,double mass)
        {
            double r=Math.Max(relative.Length,1e-12);
            return relative*(-G*mass/(r*r*r));
        }
        private Position2D Acceleration(TestParticle p,Position2D x,Position2D v,double day,int planet)
        {
            var gravity=Gravity(x,day,planet);
            if(!p.Controlled)return gravity;
            var target=LPoint(planet,p.TargetPoint,day);
            double n=2*Math.PI/Bodies[planet].Period, gain=8*n;
            var targetVelocity=OrbitMath.Velocity(target,Bodies[planet].Period);
            // Explicit idealized thruster: cancels gravity and tracks a moving L point.
            // This is NOT damping attributed to gravity, and is off by default.
            var thrust=target*(-n*n)+(target-x)*(gain*gain)+(targetVelocity-v)*(2*gain)-gravity;
            return gravity+thrust;
        }
        public void Advance(double requestedDays,int maxSteps=256)
        {
            if(double.IsNaN(requestedDays)||double.IsInfinity(requestedDays)||requestedDays<0)throw new ArgumentOutOfRangeException(nameof(requestedDays));
            if(maxSteps<1)throw new ArgumentOutOfRangeException(nameof(maxSteps));
            double end=Day+requestedDays;int steps=0;TimeLimited=false;
            while(Day<end&&steps++<maxSteps)
            {
                double dt=end-Day;
                foreach(var p in Particles)
                {
                    if(!p.Active)continue;
                    CheckCapture(p,Day);if(!p.Active)continue;
                    int next=SelectPlanet(p.Position);
                    if(next!=p.Planet){p.Planet=next;p.Switches++;p.TargetPoint=NearestPoint(p.Position,next,Day);}
                    var b=Bodies[p.Planet];var relative=p.Position-b.Position(Day);
                    dt=Math.Min(dt,b.Period/2000);
                    dt=Math.Min(dt,SafeStep(p.Position.Length,p.Velocity.Length,StarMass));
                    dt=Math.Min(dt,SafeStep(relative.Length,(p.Velocity-OrbitMath.Velocity(b.Position(Day),b.Period)).Length,b.Mass));
                }
                // Never discard physical time or take a large step to catch up.
                if(dt<=0||Day+dt==Day){TimeLimited=true;break;}
                foreach(var p in Particles)if(p.Active)
                {
                    Integrate(p,dt);
                    CheckCapture(p,Day+dt);
                    if(Day+dt-p.LastTrailDay>=Bodies[p.Planet].Period/1000||!p.Active)
                    {
                        p.Trail.Enqueue(new TrailSample(p.Position,Day+dt));
                        while(p.Trail.Count>600)p.Trail.Dequeue();p.LastTrailDay=Day+dt;
                    }
                }
                Day+=dt;
            }
            TimeLimited|=Day<end;
        }
        private static double SafeStep(double distance,double speed,double mass)
            => Math.Min(.03*Math.Sqrt(distance*distance*distance/(G*mass)),.03*distance/Math.Max(speed,1e-12));
        private void Integrate(TestParticle p,double dt)
        {
            // Keep the selected source fixed within an RK4 substep; reselect at its boundary.
            int planet=p.Planet;var x=p.Position;var v=p.Velocity;
            var a1=Acceleration(p,x,v,Day,planet);
            var v2=v+a1*(dt/2);var a2=Acceleration(p,x+v*(dt/2),v2,Day+dt/2,planet);
            var v3=v+a2*(dt/2);var a3=Acceleration(p,x+v2*(dt/2),v3,Day+dt/2,planet);
            var v4=v+a3*dt;var a4=Acceleration(p,x+v3*dt,v4,Day+dt,planet);
            p.Position=x+(v+v2*2+v3*2+v4)*(dt/6);
            p.Velocity=v+(a1+a2*2+a3*2+a4)*(dt/6);
        }
        private void CheckCapture(TestParticle p,double day)
        {
            // Numerical exclusion zones, NOT the displayed marker radius or a planetary radius model.
            if(p.Position.Length<.00465){p.Active=false;p.Status="恒星近傍で計算終了";return;}
            for(int i=0;i<Bodies.Length;i++)
                if((p.Position-Bodies[i].Position(day)).Length<1e-5)
                {p.Active=false;p.Status="惑星近傍で計算終了";return;}
            if(double.IsNaN(p.Position.X)||double.IsInfinity(p.Position.X)||double.IsNaN(p.Position.Y)||double.IsInfinity(p.Position.Y))
            {p.Active=false;p.Status="数値エラー";}
        }
    }
}
