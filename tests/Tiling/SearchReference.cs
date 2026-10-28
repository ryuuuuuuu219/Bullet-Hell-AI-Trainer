using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using Aquarium.Tiling;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

static class SearchReference
{
    public static TilingGenerator.Tile[] Load()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText("experiments/battleship-11/design.json"));
        var root=doc.RootElement;
        var vertices=root.GetProperty("vertices").EnumerateArray().ToDictionary(v=>v.GetProperty("id").GetString(),v=>new TilingGenerator.Point(v.GetProperty("position")[0].GetDouble(),v.GetProperty("position")[1].GetDouble()));
        double offset=1+Math.Sqrt(3)/2-.5; // P08 square center = (1+sqrt(3))/2.
        return root.GetProperty("parts").EnumerateArray().Select(p=>new TilingGenerator.Tile(p.GetProperty("vertex_ids").EnumerateArray().Select(id=>{var v=vertices[id.GetString()];return new TilingGenerator.Point(v.X,v.Y-offset);}).ToArray())).ToArray();
    }
    public static bool Same(TilingGenerator.Tile a,TilingGenerator.Tile b) => a.Vertices.Length==b.Vertices.Length && a.Vertices.All(p=>b.Vertices.Any(q=>Math.Abs(p.X-q.X)<1e-6&&Math.Abs(p.Y-q.Y)<1e-6));
    public static void Run(string[] args)
    {
        var target=Load();int from=args.Length>1?int.Parse(args[1]):1;int end=args.Length>2?int.Parse(args[2]):1000;
        for(int seed=from;seed<=end;seed++)
        {
            var g=new TilingGenerator(seed,20,160,false);
            while(!g.Done)
            {
                g.Step();
                var f=g.Frames.Last();
                if(f.Tiles.Length==20 && f.Tiles.All(t=>target.Any(q=>Same(t,q))))
                {Console.WriteLine($"FOUND seed={seed} attempts={g.Attempts} frames={g.Frames.Count}");return;}
                // Growth cannot remove accepted tiles, so a non-reference tile
                // proves this seed cannot reach this exact translated reference.
                if(f.Tiles.Any(t=>!target.Any(q=>Same(t,q))))break;
            }
            if(seed%1000==0)Console.WriteLine($"searched through {seed}");
        }
        Console.WriteLine("No matching seed in range.");
    }

    public static int[] FindChoices()
    {
        var target=Load();var engine=new TilingGenerator(0,20,20,false);
        var visited=new HashSet<string>();
        object Call(string name,params object[] args)=>typeof(TilingGenerator).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).Invoke(engine,args);
        bool IsTarget(IEnumerable<TilingGenerator.Tile> parts)=>parts.All(t=>target.Any(q=>Same(t,q)));
        List<int> Grow(List<TilingGenerator.Tile> parts,List<int> path)
        {
            string key=string.Join(",",parts.Select(t=>Array.FindIndex(target,q=>Same(t,q))).OrderBy(i=>i));
            if(!visited.Add(key))return null;
            if(parts.Count==20)return path;
            List<int> Attempt(List<TilingGenerator.Tile> trial,List<int> choices,int depth)
            {
                var frontier=(IList)Call("GetFrontier",trial);
                for(int fi=0;fi<frontier.Count;fi++)
                {
                    var edges=(IList)frontier[fi].GetType().GetField("Edges").GetValue(frontier[fi]);
                    for(int ei=0;ei<edges.Count;ei++)for(int shape=0;shape<2;shape++)
                    {
                        var tile=Call("Attach",edges[ei],shape==0);
                        var pair=(List<TilingGenerator.Tile>)Call("MirrorPair",tile);
                        if(!IsTarget(pair)||!(bool)Call("Fits",pair,trial))continue;
                        var extended=trial.Concat(pair).ToList();var next=choices.Concat(new[]{fi,ei,shape}).ToList();
                        TilingGenerator.Tile gap;
                        while((gap=TilingGenerator.FindAxisGap(extended))!=null)extended.Add(gap);
                        if(!IsTarget(extended))continue;
                        var found=Grow(extended,path.Concat(next).ToList());
                        if(found!=null)return found;
                    }
                }
                return null;
            }
            return Attempt(parts,new List<int>(),1);
        }
        var answer=Grow(engine.Frames[0].Tiles.ToList(),new List<int>());
        Console.WriteLine("Reference choice search: "+(answer==null?"unreachable":string.Join(",",answer)));
        return answer?.ToArray();
    }
}
