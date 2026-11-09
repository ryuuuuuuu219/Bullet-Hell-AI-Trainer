using System;
using UnityEngine;

namespace StellarSystem
{
    [Serializable]
    public class PlanetDefinition
    {
        public string name;
        public double orbitAU, massSolar, phaseDegrees;
        public Color color = Color.cyan;
        public PlanetDefinition(string name, double orbit, double mass, double phase, Color color)
        { this.name = name; orbitAU = orbit; massSolar = mass; phaseDegrees = phase; this.color = color; }
    }
    [Serializable]
    public class FortressDefinition
    {
        public string name;
        [Min(0)] public int planetIndex;
        [Range(1, 5)] public int point = 3;
        public FortressDefinition(string name, int planet, int point)
        { this.name = name; planetIndex = planet; this.point = point; }
    }

    public class StellarSystemMap : MonoBehaviour
    {
        [Header("試作値：距離 AU／質量 太陽質量／位相 度")]
        public double starMassSolar = 1;
        public PlanetDefinition[] planets = {
            new PlanetDefinition("第1惑星（仮）", 0.7, 0.000001, 25, new Color(.7f,.6f,.5f)),
            new PlanetDefinition("第2惑星（仮）", 1.2, 0.000002, 150, new Color(.95f,.7f,.35f)),
            new PlanetDefinition("第3惑星・ガス惑星（仮）", 2.2, .0007, 250, new Color(.8f,.65f,.9f)),
            new PlanetDefinition("第4惑星・岩石惑星", 3.5, .000003, 60, new Color(.4f,.8f,1)),
            new PlanetDefinition("第5惑星（種別仮）", 6, .001, 210, new Color(.9f,.65f,.35f)),
            new PlanetDefinition("第6惑星・氷巨大惑星", 12, .00005, 320, new Color(.4f,.95f,.9f)),
            new PlanetDefinition("第7惑星・褐色矮星", 24, .015, 125, new Color(.9f,.4f,.3f))
        };
        [Header("要塞配置：第5惑星L3以外は表示確認用の仮配置")]
        public FortressDefinition[] fortresses = {
            new FortressDefinition("内縁監視要塞（仮）", 1, 1),
            new FortressDefinition("第4惑星 前哨要塞（仮）", 3, 2),
            new FortressDefinition("セカンドウィング拠点", 4, 3),
            new FortressDefinition("氷惑星 補給要塞（仮）", 5, 4),
            new FortressDefinition("外縁警戒要塞（仮）", 6, 5)
        };
        public double ElapsedDays { get; private set; }
        private double[] periods;
        private Position2D[,] points;
        private int selectedPlanet = 4, selectedFortress = -1;
        private bool paused = true, follow, showPoints = true;
        private double daysPerSecond = 10, halfHeightAU = 29;
        private Position2D center;
        private Vector2 scroll;
        private Texture2D disc;
        private Font font;
        private GUIStyle title, body, small, button, selectedButton, toggle;
        private string validationError;
        private const float Side = 300;
        public GravitySimulation Experiment {get; private set;}
        private bool placing, controlledSpawn, rotatingView, inspectLagrange;
        private int examplePoint=4;

        private void Awake()
        {
            try { Rebuild(); }
            catch (Exception e) { validationError = e.Message; Debug.LogError(e); }
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 16);
            disc = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[4096];
            for (int y=0;y<64;y++) for(int x=0;x<64;x++)
                pixels[y*64+x] = new Color(1,1,1,Mathf.Clamp01((31.5f-Vector2.Distance(new Vector2(x,y), new Vector2(31.5f,31.5f)))*.8f));
            disc.SetPixels(pixels); disc.Apply();
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Map Camera");
                cameraObject.transform.SetParent(transform);
                var camera = cameraObject.AddComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f,.04f,.07f);
                camera.transform.position = new Vector3(0,0,-10);
            }
        }
        public void Rebuild()
        {
            if (planets == null || planets.Length == 0) throw new ArgumentException("惑星を1つ以上設定してください。");
            periods = new double[planets.Length]; points = new Position2D[planets.Length,5];
            for (int i=0;i<planets.Length;i++)
            {
                var p = planets[i];
                if (p == null || double.IsNaN(p.phaseDegrees) || double.IsInfinity(p.phaseDegrees)) throw new ArgumentException("惑星設定が不正です。");
                periods[i] = OrbitMath.Period(p.orbitAU,starMassSolar,p.massSolar);
                for(int j=0;j<5;j++) points[i,j] = OrbitMath.Lagrange(starMassSolar,p.massSolar,j+1)*p.orbitAU;
            }
            if (fortresses == null) fortresses = new FortressDefinition[0];
            foreach(var f in fortresses)
                if(f == null || f.planetIndex < 0 || f.planetIndex >= planets.Length || f.point < 1 || f.point > 5)
                    throw new ArgumentException("要塞の惑星番号またはL点が不正です。");
            selectedPlanet = Mathf.Clamp(selectedPlanet,0,planets.Length-1);
            selectedFortress = -1;
            var bodies=new GravityBody[planets.Length];
            for(int i=0;i<planets.Length;i++) bodies[i]=new GravityBody(planets[i].orbitAU,planets[i].massSolar,planets[i].phaseDegrees,starMassSolar);
            Experiment=new GravitySimulation(starMassSolar,bodies);
            ElapsedDays=0;
            FitSystem();
        }
        public Position2D PlanetPosition(int index, double days) => OrbitMath.Rotate(new Position2D(planets[index].orbitAU,0),OrbitMath.Phase(days,periods[index],planets[index].phaseDegrees));
        public Position2D LagrangePosition(int index, int point, double days) => OrbitMath.Rotate(points[index,point-1],OrbitMath.Phase(days,periods[index],planets[index].phaseDegrees));
        public Position2D FortressPosition(int index, double days) => LagrangePosition(fortresses[index].planetIndex,fortresses[index].point,days);
        public Position2D PlanetVelocity(int index, double days) => OrbitMath.Velocity(PlanetPosition(index,days),periods[index]);
        private Position2D SelectionPosition() => inspectLagrange ? LagrangePosition(selectedPlanet,examplePoint,ElapsedDays) : selectedFortress >= 0 ? FortressPosition(selectedFortress,ElapsedDays) : PlanetPosition(selectedPlanet,ElapsedDays);
        private void Update()
        {
            if(validationError != null) return;
            if(!paused) {Experiment.Advance(Time.unscaledDeltaTime*daysPerSecond);ElapsedDays=Experiment.Day;}
            if(follow) center = SelectionPosition();
        }
        private void FitSystem()
        {
            double radius=1;
            foreach(var p in planets) radius=Math.Max(radius,p.orbitAU*1.25);
            double aspect=Math.Max(.1,(Screen.width-Side)/Math.Max(1,Screen.height));
            halfHeightAU=radius/Math.Min(1,aspect); center=new Position2D(); follow=false;
        }
        private void Styles()
        {
            if(title!=null) return;
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=22,fontStyle=FontStyle.Bold};
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=14,wordWrap=true};
            small=new GUIStyle(body){fontSize=12};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=13,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(10,8,5,5),wordWrap=true};
            selectedButton=new GUIStyle(button); selectedButton.normal.textColor=new Color(.35f,.95f,1);
            toggle=new GUIStyle(GUI.skin.toggle){font=font,fontSize=14};
        }
        private void OnGUI()
        {
            Styles();
            Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.025f,.04f,.07f));
            if(validationError!=null) { GUI.Label(new Rect(20,20,700,200),validationError,body); return; }
            float width=Mathf.Max(100,Screen.width-Side);
            Rect map=new Rect(Side,0,width,Screen.height);
            InputMap(map);
            GUI.BeginGroup(map);
            DrawMap(width,Screen.height);
            GUI.EndGroup();
            DrawSidebar();
        }
        private Vector2 ScreenPoint(Position2D position,float width,float height)
            => ScreenPointAt(position,width,height,ElapsedDays);
        private double ViewAngle(double day) => rotatingView ? OrbitMath.Phase(day,periods[selectedPlanet],planets[selectedPlanet].phaseDegrees) : 0;
        private Vector2 ScreenPointAt(Position2D position,float width,float height,double day)
        {
            var relative=OrbitMath.Rotate(position,-ViewAngle(day))-OrbitMath.Rotate(center,-ViewAngle(ElapsedDays));
            double scale=height/(2*halfHeightAU);
            return new Vector2((float)(width/2+relative.X*scale),(float)(height/2-relative.Y*scale));
        }
        private void DrawMap(float w,float h)
        {
            for(int i=0;i<130;i++)
                Fill(new Rect((i*197.31f)%w,(i*371.73f)%h,1,1),new Color(.25f,.32f,.43f));
            for(int i=0;i<planets.Length;i++)
            {
                Vector2 previous=ScreenPoint(new Position2D(planets[i].orbitAU,0),w,h);
                for(int k=1;k<=180;k++)
                {
                    var next=ScreenPoint(OrbitMath.Rotate(new Position2D(planets[i].orbitAU,0),k*Math.PI/90),w,h);
                    Line(previous,next,i==selectedPlanet ? new Color(.22f,.38f,.48f) : new Color(.1f,.18f,.25f),1);
                    previous=next;
                }
            }
            foreach(var particle in Experiment.Particles)
            {
                Color color=particle.Controlled ? new Color(.9f,.45f,1) : new Color(.3f,1,.6f);
                Vector2 previous=default;bool first=true;
                foreach(var sample in particle.Trail)
                {
                    var next=ScreenPointAt(sample.Position,w,h,sample.Day);
                    if(!first)Line(previous,next,new Color(color.r*.55f,color.g*.55f,color.b*.55f),1);
                    previous=next;first=false;
                }
                var at=ScreenPoint(particle.Position,w,h);
                if(!first)Line(previous,at,color,1);
                Circle(at,particle.Active?3:2,particle.Active?color:Color.gray);
            }
            var sun=ScreenPoint(new Position2D(),w,h);
            Circle(sun,15,new Color(1,.78f,.3f));
            GUI.Label(new Rect(sun.x+18,sun.y-10,100,24),"恒星",body);
            if(showPoints)
                for(int j=1;j<=5;j++)
                {
                    var pt=ScreenPoint(LagrangePosition(selectedPlanet,j,ElapsedDays),w,h);
                    Line(pt+Vector2.left*5,pt+Vector2.right*5,new Color(.3f,.65f,.65f),1);
                    Line(pt+Vector2.up*5,pt+Vector2.down*5,new Color(.3f,.65f,.65f),1);
                    GUI.Label(new Rect(pt.x+7,pt.y-23,50,22),"L"+j,small);
                }
            for(int i=0;i<planets.Length;i++)
            {
                var pt=ScreenPoint(PlanetPosition(i,ElapsedDays),w,h);
                if(i==selectedPlanet && selectedFortress<0) Circle(pt,12,new Color(.8f,.95f,1));
                Circle(pt,8,planets[i].color);
                if(GUI.Button(new Rect(pt.x-10,pt.y-10,20,20),GUIContent.none,GUIStyle.none)) { selectedPlanet=i; selectedFortress=-1; inspectLagrange=false; }
                GUI.Label(new Rect(pt.x+12,pt.y+4,130,22),"第"+(i+1)+"惑星",small);
            }
            for(int i=0;i<fortresses.Length;i++)
            {
                var f=fortresses[i]; var pt=ScreenPoint(FortressPosition(i,ElapsedDays),w,h);
                Color color=i==selectedFortress ? Color.white : new Color(1,.45f,.3f);
                Line(pt+Vector2.up*8,pt+Vector2.right*8,color,2); Line(pt+Vector2.right*8,pt+Vector2.down*8,color,2);
                Line(pt+Vector2.down*8,pt+Vector2.left*8,color,2); Line(pt+Vector2.left*8,pt+Vector2.up*8,color,2);
                if(GUI.Button(new Rect(pt.x-9,pt.y-9,18,18),GUIContent.none,GUIStyle.none)) { selectedFortress=i; selectedPlanet=f.planetIndex; inspectLagrange=false; }
                GUI.Label(new Rect(pt.x+12,pt.y-21,160,22),"要塞 P"+(f.planetIndex+1)+" · L"+f.point,small);
            }
            GUI.Label(new Rect(20,16,w-30,30),"恒星系マップ",title);
            GUI.Label(new Rect(20,h-55,w-30,45),"ホイール：拡大縮小   右ドラッグ：移動   天体・要塞：クリックで選択\n円軌道の試作配置 ／ 天体・要塞のマーカーサイズは距離縮尺とは別",small);
            GUI.Label(new Rect(20,48,w-30,25),"表示幅 "+(2*halfHeightAU*w/h).ToString("F2")+" AU",small);
            GUI.Label(new Rect(20,75,w-30,48),(rotatingView?"選択惑星と共回転する表示":"恒星基準の表示")+(placing?" ／ 左クリックで物体を配置":"")+"\n緑：重力のみ　紫：位置維持制御あり",small);
        }
        private void DrawSidebar()
        {
            Fill(new Rect(0,0,Side,Screen.height),new Color(.045f,.07f,.11f));
            GUILayout.BeginArea(new Rect(14,14,Side-28,Screen.height-28));
            scroll=GUILayout.BeginScrollView(scroll);
            GUILayout.Label("恒星系と要塞",title);
            GUILayout.Label("配置検討用プロトタイプ",small);
            GUILayout.Space(12);
            GUILayout.Label("経過 "+ElapsedDays.ToString("F2")+" 日",body);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(paused?"▶ 再生":"Ⅱ 一時停止",button)) paused=!paused;
            if(GUILayout.Button("時刻・物体をリセット",button)) {Experiment.Reset();ElapsedDays=0;paused=true;}
            GUILayout.EndHorizontal();
            GUILayout.Label("時間送り："+daysPerSecond+" 日／秒",small);
            GUILayout.BeginHorizontal();
            foreach(double rate in new[]{1d,10d,100d,1000d})
                if(GUILayout.Button(rate.ToString(),daysPerSecond==rate?selectedButton:button)) daysPerSecond=rate;
            GUILayout.EndHorizontal();
            if(GUILayout.Button("恒星系全体を表示",button)) FitSystem();
            if(GUILayout.Button("選択対象を拡大・追従",button)) { follow=true;center=SelectionPosition();halfHeightAU=planets[selectedPlanet].orbitAU*.12; }
            follow=GUILayout.Toggle(follow,"選択対象を追従",toggle);
            showPoints=GUILayout.Toggle(showPoints,"選択惑星の L1〜L5 を表示",toggle);
            rotatingView=GUILayout.Toggle(rotatingView,"選択惑星の共回転表示",toggle);
            DrawExperimentControls();
            GUILayout.Space(12);
            GUILayout.Label("惑星",body);
            for(int i=0;i<planets.Length;i++)
                if(GUILayout.Button(planets[i].name,selectedPlanet==i&&selectedFortress<0?selectedButton:button)) {selectedPlanet=i;selectedFortress=-1;inspectLagrange=false;}
            GUILayout.Space(10);
            GUILayout.Label("要塞・拠点",body);
            for(int i=0;i<fortresses.Length;i++)
                if(GUILayout.Button(fortresses[i].name+"\n第"+(fortresses[i].planetIndex+1)+"惑星 / L"+fortresses[i].point,selectedFortress==i?selectedButton:button)) {selectedFortress=i;selectedPlanet=fortresses[i].planetIndex;inspectLagrange=false;}
            GUILayout.Space(12);
            var p=planets[selectedPlanet];var pos=SelectionPosition();
            GUILayout.Label(selectedFortress>=0?fortresses[selectedFortress].name:p.name,body);
            GUILayout.Label("公転半径："+p.orbitAU+" AU\n公転周期："+periods[selectedPlanet].ToString("F1")+" 日\nX："+pos.X.ToString("F5")+" AU\nY："+pos.Y.ToString("F5")+" AU",small);
            GUILayout.Label("各要塞は対応するL点を自動維持。惑星同士の重力干渉・要塞内部・戦闘は未実装。数値と仮配置は Inspector で変更できます。",small);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        private void InputMap(Rect map)
        {
            var e=Event.current;
            if(!map.Contains(e.mousePosition)) return;
            if(placing&&e.type==EventType.MouseDown&&e.button==0)
            {
                var screenOffset=new Position2D(e.mousePosition.x-map.x-map.width/2,-(e.mousePosition.y-map.height/2));
                Experiment.Add(center+OrbitMath.Rotate(screenOffset*(2*halfHeightAU/map.height),ViewAngle(ElapsedDays)),controlledSpawn);
                e.Use();
            }
            if(e.type==EventType.ScrollWheel)
            {
                double old=halfHeightAU;
                halfHeightAU=Math.Max(.0001,Math.Min(10000,halfHeightAU*Math.Exp(e.delta.y*.08)));
                if(!follow)
                {
                    double sx=e.mousePosition.x-map.x-map.width/2,sy=-(e.mousePosition.y-map.height/2);
                    center+=OrbitMath.Rotate(new Position2D(sx,sy)*(2*(old-halfHeightAU)/map.height),ViewAngle(ElapsedDays));
                }
                e.Use();
            }
            if(e.type==EventType.MouseDrag&&e.button==1)
            { follow=false;center+=OrbitMath.Rotate(new Position2D(-e.delta.x,e.delta.y)*(2*halfHeightAU/map.height),ViewAngle(ElapsedDays));e.Use(); }
        }
        private void DrawExperimentControls()
        {
            GUILayout.Space(12);
            GUILayout.Label("重力の実験",body);
            placing=GUILayout.Toggle(placing,"クリックで物体を置く",toggle);
            controlledSpawn=GUILayout.Toggle(controlledSpawn,"新しい物体に位置維持制御",toggle);
            GUILayout.Label("初速は対象惑星との共回転速度。第2重力源は、恒星からの距離が最も近い公転半径の惑星を随時選びます。",small);
            GUILayout.BeginHorizontal();
            for(int i=1;i<=5;i++)if(GUILayout.Button("L"+i,examplePoint==i?selectedButton:button))examplePoint=i;
            GUILayout.EndHorizontal();
            if(GUILayout.Button("選択惑星のL点付近に4個置く",button))SpawnLagrangeExample(false);
            if(GUILayout.Button("同じ初期条件で重力／制御を比較",button))SpawnLagrangeExample(true);
            if(GUILayout.Button("実験物体だけ消す",button))Experiment.Clear();
            int active=0;foreach(var particle in Experiment.Particles)if(particle.Active)active++;
            GUILayout.Label("物体 "+Experiment.Particles.Count+" / "+GravitySimulation.MaxParticles+"（飛行中 "+active+"）",small);
            if(Experiment.TimeLimited)GUILayout.Label("接近時の精度維持のため時間送りを制限中",small);
            if(Experiment.Particles.Count>0)
            {
                var last=Experiment.Particles[Experiment.Particles.Count-1];
                double distance=(last.Position-Experiment.LPoint(last.Planet,last.TargetPoint,ElapsedDays)).Length;
                GUILayout.Label("最後の物体：第"+(last.Planet+1)+"惑星 / L"+last.TargetPoint+"\n目標点との距離 "+distance.ToString("G4")+" AU\n重力源の切替 "+last.Switches+"回 ／ "+last.Status,small);
            }
            GUILayout.Label("重力のみでは必ず収束しません。制御ありは推力でL点を追従する比較用モデルです。",small);
        }
        public void SpawnLagrangeExample(bool comparison)
        {
            int required=comparison?8:4;
            if(Experiment.Particles.Count+required>GravitySimulation.MaxParticles)return;
            var target=LagrangePosition(selectedPlanet,examplePoint,ElapsedDays);
            double radius=planets[selectedPlanet].orbitAU*.005;
            for(int i=0;i<4;i++)
            {
                var position=target+OrbitMath.Rotate(new Position2D(radius,0),ViewAngle(ElapsedDays)+i*Math.PI/2);
                Experiment.Add(position,comparison?false:controlledSpawn);
                if(comparison)Experiment.Add(position,true);
            }
            inspectLagrange=true;rotatingView=true;follow=true;showPoints=true;
            center=target;halfHeightAU=planets[selectedPlanet].orbitAU*.04;paused=true;
            daysPerSecond=1000;
        }
        private void Circle(Vector2 p,float radius,Color color)
        {GUI.color=color;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,2*radius,2*radius),disc);GUI.color=Color.white;}
        private static void Fill(Rect rect,Color color)
        {GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=Color.white;}
        private static void Line(Vector2 a,Vector2 b,Color color,float width)
        {
            var saved=GUI.matrix;var d=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);
            Fill(new Rect(a.x,a.y-width/2,d.magnitude,width),color);GUI.matrix=saved;
        }
        private void OnDestroy()
        {if(disc!=null) Destroy(disc);if(font!=null) Destroy(font);}
    }
}
