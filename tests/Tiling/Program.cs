using System;
using System.Linq;
using System.Collections.Generic;
using Aquarium.Tiling;

class Program
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static double Distance(TilingGenerator.Point a, TilingGenerator.Point b) => Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    static void Validate(TilingGenerator.Frame f)
    {
        for (int i=0;i<f.Tiles.Length;i++)
        {
            var t=f.Tiles[i];
            for(int j=0;j<t.Vertices.Length;j++) Check(Math.Abs(Distance(t.Vertices[j], t.Vertices[(j+1)%t.Vertices.Length])-1)<1e-6,"Edge length");
            for(int j=0;j<i;j++) Check(!TilingGenerator.Overlaps(t,f.Tiles[j]),"Overlap");
            Check(f.Tiles.Any(other=>other.Vertices.Length==t.Vertices.Length && t.Vertices.All(p=>other.Vertices.Any(q=>Distance(new TilingGenerator.Point(-p.X,p.Y),q)<1e-6))),"Mirror symmetry");
        }
        // Connectivity must be by full edges, not just a shared point.
        var reached=new HashSet<int>{0};
        bool changed=true;
        while(changed){changed=false;for(int i=0;i<f.Tiles.Length;i++) if(!reached.Contains(i) && reached.Any(j=>f.Tiles[i].Vertices.Count(p=>f.Tiles[j].Vertices.Any(q=>Distance(p,q)<1e-6))>=2)){reached.Add(i);changed=true;}}
        Check(reached.Count==f.Tiles.Length,"Disconnected");
    }
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "bow-case")
        {
            var g = new TilingGenerator(2714,32,233,true,30);
            while(!g.Done)g.Step();
            var f=g.Frames.Last();Validate(f);
            Console.WriteLine($"seed=2714 count={g.Count} attempts={g.Attempts}");
            for(int i=0;i<f.Tiles.Length;i++)Console.WriteLine($"{i+1}: "+string.Join(" ",f.Tiles[i].Vertices.Select(p=>$"({p.X:F9},{p.Y:F9})")));
            return;
        }
        if (args.Length > 0 && args[0] == "search") { SearchReference.Run(args); return; }
        if (args.Length > 0 && args[0] == "choices") { SearchReference.FindChoices(); return; }
        double h=Math.Sqrt(3)/2;
        TestEnds();
        var right=new TilingGenerator.Tile(new TilingGenerator.Point(0,0),new TilingGenerator.Point(1,0),new TilingGenerator.Point(.5,h));
        var left=new TilingGenerator.Tile(new TilingGenerator.Point(0,0),new TilingGenerator.Point(-.5,h),new TilingGenerator.Point(-1,0));
        var notch=new List<TilingGenerator.Tile>{right,left};
        Check(TilingGenerator.FindAxisGap(new List<TilingGenerator.Tile>{right})==null,"One-sided edge must not fill");
        var patch=TilingGenerator.FindAxisGap(notch);
        Check(patch!=null&&patch.Triangle,"Mirrored notch not detected");
        notch.Add(patch);Validate(new TilingGenerator.Frame(notch,"test"));
        Check(TilingGenerator.FindAxisGap(notch)==null,"Already filled/shared edges filled twice");
        var inverted=new[]{right,left}.Select(t=>new TilingGenerator.Tile(t.Vertices.Reverse().Select(p=>new TilingGenerator.Point(p.X,-p.Y)).ToArray())).ToList();
        Check(TilingGenerator.FindAxisGap(inverted)!=null,"Downward notch not detected");
        var away=new[]{right,left}.Select(t=>new TilingGenerator.Tile(t.Vertices.Select(p=>new TilingGenerator.Point(p.X+2,p.Y)).ToArray())).ToList();
        Check(TilingGenerator.FindAxisGap(away)==null,"Off-axis notch must remain unchanged");
        Console.WriteLine("PASS: local axis gap detection, both vertical directions, one-sided rejection, no duplicate fill, off-axis unchanged.");
        foreach (var setting in new[]{(triangle:false,angle:45.0),(triangle:false,angle:-90.0),(triangle:true,angle:-30.0),(triangle:true,angle:90.0)})
        {
            var angled = new TilingGenerator(2,8,30,setting.triangle,setting.angle);
            Check(angled.InitialSymmetric,"Valid starting angle rejected");
            while(!angled.Done)angled.Step();
            foreach(var f in angled.Frames)Validate(f);
        }
        foreach(var setting in new[]{(triangle:false,angle:30.0),(triangle:true,angle:0.0)})
        {
            var angled = new TilingGenerator(2,8,30,setting.triangle,setting.angle);
            Check(!angled.InitialSymmetric&&angled.Done&&!angled.Reached,"Asymmetric start must remain preview-only");
            angled.Step();Check(angled.Count==1&&angled.Attempts==0,"Asymmetric initial tile grew");
        }
        var rotatedSquare = new TilingGenerator(2,initialAngle:90);
        Check(Distance(rotatedSquare.Frames[0].Tiles[0].Vertices[0],new TilingGenerator.Point(.5,-.5))<1e-6,"Rotation direction");
        Console.WriteLine("PASS: initial angles, CCW rotation, symmetric growth and asymmetric preview.");
        int frames=0, reached=0;
        foreach(bool triangle in new[]{false,true})
        for(int seed=1;seed<=12;seed++)
        {
            var g=new TilingGenerator(seed,24,160,triangle);
            while(!g.Done)g.Step();
            foreach(var f in g.Frames){Validate(f);frames++;}
            Check(g.Attempts<=160,"Attempt bound");
            Check(g.Count<=25+g.AxisGapFilled,"Target overshoot must come only from pair/local fills");
            Check(g.Frames.Count==g.Accepted+g.AxisGapFilled+1,"One frame per attachment or local fill");
            Check(TilingGenerator.FindAxisGap(g.Frames.Last().Tiles.ToList())==null,"Unfilled eligible axis gap");
            if(g.Reached)reached++;
            if(seed==3&&!triangle){var repeat=new TilingGenerator(seed,24,160,false);while(!repeat.Done)repeat.Step();Check(repeat.Count==g.Count&&repeat.Attempts==g.Attempts,"Reproducibility");}
            Console.WriteLine($"{(triangle?"triangle":"square")} seed={seed} count={g.Count} attempts={g.Attempts} reached={g.Reached}");
        }
        var one=new TilingGenerator(1,1);Check(one.Done&&one.Count==1,"Initial completion");
        // Directly attaching a square to a triangle must survive immediately.
        var attachment = new TilingGenerator(new ChoiceRandom(new[]{0,0,1}),10,1,true);
        attachment.Step();
        Check(attachment.Accepted==1 && attachment.Frames.Last().Tiles.Any(t=>!t.Triangle),"Triangle-square attachment rejected");
        Validate(attachment.Frames.Last());
        var choices = new ChoiceRandom(SearchReference.FindChoices());
        var referenceRun = new TilingGenerator(choices,20,20,false);
        while(!referenceRun.Done)referenceRun.Step();
        foreach(var f in referenceRun.Frames)Validate(f);
        var final=referenceRun.Frames.Last(); var reference=SearchReference.Load();
        Check(final.Tiles.Length==20,"Reference count");
        Check(final.Tiles.Count(t=>!t.Triangle)==7,"Reference square count");
        Check(final.Tiles.All(t=>reference.Any(q=>SearchReference.Same(t,q))),"Reference exact tiling");
        Console.WriteLine("PASS: reference reached by normal edge attachments with prescribed random choices; 20 tiles, 7 squares, 13 triangles, exact reference coordinates up to translation.");
        Console.WriteLine($"PASS: 24 runs, {frames} frames, {reached} reached target; edge lengths, overlap, symmetry, connectivity, bounded termination.");
    }
    static void TestEnds()
    {
        TilingGenerator.Tile Tile(params double[] xy) => new TilingGenerator.Tile(Enumerable.Range(0,xy.Length/2).Select(i=>new TilingGenerator.Point(xy[i*2],xy[i*2+1])).ToArray());
        var h=Math.Sqrt(3)/2;
        var pairs=new[]{Tile(0,0,h,.5,0,1),Tile(0,0,0,1,-h,.5)};
        var squares=new[]{Tile(0,0,1,0,1,1,0,1),Tile(-1,0,0,0,0,1,-1,1)};
        foreach(bool upper in new[]{true,false})
        {
            Check(TilingGenerator.DetectEnd(pairs,upper)==TilingGenerator.EndShape.TrianglePairTip,"Rhombus detection");
            Check(TilingGenerator.DetectEnd(squares,upper)==TilingGenerator.EndShape.SquarePairEdge,"Square pair detection");
            foreach(double angle in new[]{0.0,45.0})
            {
                var g=new TilingGenerator(1,1,initialAngle:angle);
                Check(TilingGenerator.DetectEnd(g.Frames[0].Tiles,upper)==(angle==0?TilingGenerator.EndShape.SquareEdge:TilingGenerator.EndShape.SquareTip),"Square cap detection");
            }
        }
        var triangle=new TilingGenerator(1,1,triangleStart:true,initialAngle:30);
        Check(TilingGenerator.DetectEnd(triangle.Frames[0].Tiles,false)==TilingGenerator.EndShape.TriangleTip,"Triangle tip");
        Check(TilingGenerator.DetectEnd(triangle.Frames[0].Tiles,true)==TilingGenerator.EndShape.None,"Triangle base must not finish");
        var blocked=pairs.Concat(new[]{Tile(2,0,3,0,3,1,2,1)}).ToArray();
        Check(TilingGenerator.DetectEnd(blocked,true)==TilingGenerator.EndShape.None,"Lateral extreme must reject cap");
        foreach(var progress in triangle.FormEnds()) { }
        Validate(triangle.Frames.Last());
        Check(triangle.EndTilesAdded>0 && TilingGenerator.DetectEnd(triangle.Frames.Last().Tiles,true)!=TilingGenerator.EndShape.None,"Simple end search");
        int count=triangle.Count;
        foreach(var progress in triangle.FormEnds()) { }
        Check(triangle.Count==count,"End processing must be idempotent");
        var failed=new TilingGenerator(1,1,triangleStart:true,initialAngle:30);
        foreach(var progress in failed.FormEnds(0,0)) { }
        Check(failed.Count==1&&failed.Frames.Count==1,"Failed search must not mutate");
        int complete=0;
        foreach(bool tri in new[]{false,true})
        foreach(int seed in new[]{2,17,2714})
        {
            var g=new TilingGenerator(seed,32,233,tri,tri?30:0);
            while(!g.Done)g.Step();
            int start=g.Frames.Count;
            foreach(var progress in g.FormEnds()) { }
            foreach(var f in g.Frames.Skip(start))Validate(f);
            if(TilingGenerator.DetectEnd(g.Frames.Last().Tiles,true)!=TilingGenerator.EndShape.None && TilingGenerator.DetectEnd(g.Frames.Last().Tiles,false)!=TilingGenerator.EndShape.None)complete++;
            Check(g.EndTilesAdded<=24,"End depth bound");
            Console.WriteLine($"Ends triangle={tri} seed={seed}: {g.UpperEndStatus} / {g.LowerEndStatus}, added={g.EndTilesAdded}");
        }
        Console.WriteLine($"PASS: five end shapes, lateral rejection, failure rollback, idempotence, end geometry; {complete}/6 ships completed both ends.");
    }
    private sealed class ChoiceRandom : Random
    {
        private readonly Queue<int> values;
        public ChoiceRandom(IEnumerable<int> values){this.values=new Queue<int>(values);}
        public override int Next(int maxValue)
        {
            if(values.Count==0)throw new Exception("Reference choice stream exhausted");
            int value=values.Dequeue();Check(value>=0&&value<maxValue,"Invalid reference random choice");return value;
        }
    }
}
