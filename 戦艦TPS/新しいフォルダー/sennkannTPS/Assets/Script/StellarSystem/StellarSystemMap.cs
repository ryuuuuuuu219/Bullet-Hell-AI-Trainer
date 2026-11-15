using System;
using UnityEngine;

namespace StellarSystem
{
    /*
楕円軌道はEllipticalOrbitDataで指定する。
・semiMajorAxis：double、軌道長半径
・eccentricity：double、離心率（0以上1未満）
・periapsisDeg：double、近日点方向（度）
・periodDays：double、公転周期（日）
・initialMeanAnomalyDeg：double、初期平均近点角（度）*/

    [Serializable]
    public class EllipticalOrbitData
    {
        public double semiMajorAxis;
        public double eccentricity;
        public double periapsisDeg;
        public double periodDays;
        public double initialMeanAnomalyDeg;
        public EllipticalOrbitData(double semiMajorAxis, double eccentricity, double periapsisDeg, double periodDays, double initialMeanAnomalyDeg)
        {
            this.semiMajorAxis = semiMajorAxis;
            this.eccentricity = eccentricity;
            this.periapsisDeg = periapsisDeg;
            this.periodDays = periodDays;
            this.initialMeanAnomalyDeg = initialMeanAnomalyDeg;
        }
    }

    [Serializable]
    public class PlanetDefinition
    {
        public string name;
        public EllipticalOrbitData orbitData;
        public double massSolar;
        public Color color = Color.cyan;
        public PlanetDefinition(string name, EllipticalOrbitData orbit, double mass, Color color)
        { this.name = name; this.orbitData = orbit; massSolar = mass; this.color = color; }
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
            new PlanetDefinition("第1惑星・岩石惑星", new EllipticalOrbitData(0.4, 0.02, 110, 90, 120), .000001, new Color(.7f,.6f,.5f)),
            new PlanetDefinition("第2惑星・岩石惑星", new EllipticalOrbitData(0.5, 0.01, 240, 160, 60), .000002, new Color(.95f,.7f,.35f)),
            new PlanetDefinition("第3惑星・ガス惑星", new EllipticalOrbitData(1.5, 0.03, 30, 200, 0), .0007, new Color(.8f,.65f,.9f)),
            new PlanetDefinition("第4惑星・岩石惑星", new EllipticalOrbitData(4, 0.05,0,365.25,0), .000003, new Color(.4f,.8f,1)),
            new PlanetDefinition("第5惑星（種別仮）", new EllipticalOrbitData(6, 0.08,15,1235,35), .001, new Color(.9f,.65f,.35f)),
            new PlanetDefinition("第6惑星・氷巨大惑星", new EllipticalOrbitData(12, 0.03,195,3524,80), .00005, new Color(.4f,.95f,.9f)),
            new PlanetDefinition("第7惑星・褐色矮星", new EllipticalOrbitData(24, 0.2,200,8200,170), .015, new Color(.9f,.4f,.3f))
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
        private Position2D[,] points;
        private Camera mapCamera;
        private Material orbitMaterial;
        private LineRenderer[] orbitLines;
        private const int OrbitSegments = 256;
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
        private bool rotatingView;

        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 16);
            disc = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = new Color(1, 1, 1,
                        Mathf.Clamp01((31.5f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f))) * .8f));
            disc.SetPixels(pixels);
            disc.Apply();

            // IMGUI のマップ領域と同じ画角を、実際の描画カメラにも設定する。
            mapCamera = GetComponentInChildren<Camera>();
            if (mapCamera == null)
            {
                var cameraObject = new GameObject("Map Camera");
                cameraObject.transform.SetParent(transform, false);
                mapCamera = cameraObject.AddComponent<Camera>();
            }
            if (Camera.main == null) mapCamera.tag = "MainCamera";
            mapCamera.orthographic = true;
            mapCamera.depth = 10;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(.025f, .04f, .07f);
            mapCamera.nearClipPlane = .1f;
            mapCamera.farClipPlane = 100f;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
                Debug.LogError("軌道用のシェーダーが見つかりません。URP 対応のマテリアルを設定してください。");
            else
                orbitMaterial = new Material(shader) { name = "Stellar Orbit Lines (Runtime)" };

            try { Rebuild(); }
            catch (Exception e) { validationError = e.Message; Debug.LogError(e); }
        }

        public void Rebuild()
        {
            if (planets == null || planets.Length == 0)
                throw new ArgumentException("惑星を1つ以上設定してください。");
            points = new Position2D[planets.Length, 5];
            for (int i = 0; i < planets.Length; i++)
            {
                var p = planets[i];
                if (p == null || p.orbitData == null || !IsFinite(p.massSolar) || p.massSolar <= 0 ||
                    !IsFinite(p.orbitData.semiMajorAxis) || p.orbitData.semiMajorAxis <= 0 ||
                    !IsFinite(p.orbitData.eccentricity) || p.orbitData.eccentricity < 0 || p.orbitData.eccentricity >= 1 ||
                    !IsFinite(p.orbitData.periodDays) || p.orbitData.periodDays <= 0 ||
                    !IsFinite(p.orbitData.periapsisDeg) || !IsFinite(p.orbitData.initialMeanAnomalyDeg))
                    throw new ArgumentException("惑星の軌道パラメータが不正です: " + (p != null ? p.name : i.ToString()));
                for (int j = 0; j < 5; j++)
                    points[i, j] = OrbitMath.Lagrange(starMassSolar, p.massSolar, j + 1);
            }
            if (fortresses == null) fortresses = new FortressDefinition[0];
            foreach (var f in fortresses)
                if (f == null || f.planetIndex < 0 || f.planetIndex >= planets.Length || f.point < 1 || f.point > 5)
                    throw new ArgumentException("要塞の惑星番号またはL点が不正です。");

            selectedPlanet = Mathf.Clamp(selectedPlanet, 0, planets.Length - 1);
            selectedFortress = -1;
            ElapsedDays = 0;
            FitSystem();
            BuildOrbitLines();
            SyncCamera();
            validationError = null;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        // 楕円軌道の L 点は、現在の恒星―惑星方向・距離を用いる近似配置。
        public Position2D LagrangePosition(int index, int point, double days)
        {
            var planet = PlanetPosition(index, days);
            var normalizedPoint = points[index, point - 1];
            return OrbitMath.Rotate(normalizedPoint * planet.Length, Math.Atan2(planet.Y, planet.X));
        }
        public Position2D FortressPosition(int index, double days) =>
            LagrangePosition(fortresses[index].planetIndex, fortresses[index].point, days);

        // AU / 日。差分（変位）ではなく楕円軌道の時間微分を返す。
        public Position2D PlanetVelocity(int index, double days)
        {
            var o = planets[index].orbitData;
            double mean = MeanAnomaly(o, days);
            double E = SolveKepler(mean, o.eccentricity);
            double speed = (2 * Math.PI / o.periodDays) / (1 - o.eccentricity * Math.Cos(E));
            return OrbitMath.Rotate(new Position2D(
                -o.semiMajorAxis * Math.Sin(E) * speed,
                o.semiMajorAxis * Math.Sqrt(1 - o.eccentricity * o.eccentricity) * Math.Cos(E) * speed),
                o.periapsisDeg * Math.PI / 180);
        }
        private Position2D SelectionPosition() => selectedFortress >= 0 ? FortressPosition(selectedFortress, ElapsedDays) : PlanetPosition(selectedPlanet, ElapsedDays);
        public Position2D PlanetPosition(int index, double days)
        {
            var p = planets[index];
            double m_t = MeanAnomaly(p.orbitData, days);
            double a = p.orbitData.semiMajorAxis;
            double e = p.orbitData.eccentricity;
            double E = SolveKepler(m_t, e);
            double x = a * (Math.Cos(E) - e);
            double y = a * Math.Sqrt(1 - e * e) * Math.Sin(E);
            var rotated = OrbitMath.Rotate(new Position2D(x, y), p.orbitData.periapsisDeg * Math.PI / 180);
            return rotated;
        }

        private static double MeanAnomaly(EllipticalOrbitData orbit, double days) =>
            orbit.initialMeanAnomalyDeg * Math.PI / 180 + 2 * Math.PI * (days % orbit.periodDays) / orbit.periodDays;

        public static double SolveKepler(double M, double e, double tolerance = 1e-10, int maxIterations = 100)
        {
            double E = M; // Initial guess
            for (int i = 0; i < maxIterations; i++)
            {
                double f = E - e * Math.Sin(E) - M;
                double fPrime = 1 - e * Math.Cos(E);
                double deltaE = -f / fPrime;
                E += deltaE;
                if (Math.Abs(deltaE) < tolerance)
                    return E;
            }
            throw new Exception("Kepler's equation did not converge.");
        }

        public Position2D[] PlanetOrbit(int index, int segments = 128)
        {
            var orbit = new Position2D[segments];
            for (int i = 0; i < segments; i++)
                orbit[i] = PlanetPosition(index, planets[index].orbitData.periodDays * (double)i / segments);
            return orbit;
        }

        private void Update()
        {
            if (validationError != null)
                return;

            if (!paused)
                ElapsedDays += Time.unscaledDeltaTime * daysPerSecond;

            if (follow)
                center = SelectionPosition();
        }

        private void LateUpdate()
        {
            if (validationError != null) return;
            SyncCamera();
            RefreshOrbitStyles();
        }

        private void SyncCamera()
        {
            if (mapCamera == null) return;
            float screenWidth = Mathf.Max(1, Screen.width);
            float sidebarWidth = Mathf.Min(Side, screenWidth - 1);
            mapCamera.rect = new Rect(sidebarWidth / screenWidth, 0, 1 - sidebarWidth / screenWidth, 1);
            mapCamera.orthographicSize = (float)halfHeightAU;
            mapCamera.transform.position = new Vector3((float)center.X, (float)center.Y, -10);
            mapCamera.transform.rotation = Quaternion.Euler(0, 0, (float)(ViewAngle(ElapsedDays) * Mathf.Rad2Deg));
        }

        private void FitSystem()
        {
            double radius = 1;
            foreach (var p in planets) radius = Math.Max(radius, p.orbitData.semiMajorAxis * 1.25);
            double aspect = Math.Max(.1, (Screen.width - Side) / Math.Max(1, Screen.height));
            halfHeightAU = radius / Math.Min(1, aspect); center = new Position2D(); follow = false;
        }
        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 22, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, wordWrap = true };
            small = new GUIStyle(body) { fontSize = 12 };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 8, 5, 5), wordWrap = true };
            selectedButton = new GUIStyle(button); selectedButton.normal.textColor = new Color(.35f, .95f, 1);
            toggle = new GUIStyle(GUI.skin.toggle) { font = font, fontSize = 14 };
        }
        private void OnGUI()
        {
            Styles();
            if (validationError != null) { GUI.Label(new Rect(20, 20, 700, 200), validationError, body); return; }
            float width = Mathf.Max(100, Screen.width - Side);
            Rect map = new Rect(Side, 0, width, Screen.height);
            InputMap(map);
            GUI.BeginGroup(map);
            DrawMap(width, Screen.height);
            GUI.EndGroup();
            DrawSidebar();
        }
        private Vector2 ScreenPoint(Position2D position, float width, float height)
            => ScreenPointAt(position, width, height, ElapsedDays);
        private double ViewAngle(double day)
        {
            if (!rotatingView) return 0;
            var p = PlanetPosition(selectedPlanet, day);
            return Math.Atan2(p.Y, p.X);
        }
        private Vector2 ScreenPointAt(Position2D position, float width, float height, double day)
        {
            var relative = OrbitMath.Rotate(position, -ViewAngle(day)) - OrbitMath.Rotate(center, -ViewAngle(ElapsedDays));
            double scale = height / (2 * halfHeightAU);
            return new Vector2((float)(width / 2 + relative.X * scale), (float)(height / 2 - relative.Y * scale));
        }
        private void DrawMap(float w, float h)
        {
            for (int i = 0; i < 130; i++)
                Fill(new Rect((i * 197.31f) % w, (i * 371.73f) % h, 1, 1), new Color(.25f, .32f, .43f));
            var sun = ScreenPoint(new Position2D(), w, h);
            Circle(sun, 15, new Color(1, .78f, .3f));
            GUI.Label(new Rect(sun.x + 18, sun.y - 10, 100, 24), "恒星", body);
            if (showPoints)
                for (int j = 1; j <= 5; j++)
                {
                    var pt = ScreenPoint(LagrangePosition(selectedPlanet, j, ElapsedDays), w, h);
                    Line(pt + Vector2.left * 5, pt + Vector2.right * 5, new Color(.3f, .65f, .65f), 2);
                    Line(pt + Vector2.up * 5, pt + Vector2.down * 5, new Color(.3f, .65f, .65f), 2);
                    GUI.Label(new Rect(pt.x + 7, pt.y - 23, 50, 22), "L" + j, small);
                }
            for (int i = 0; i < planets.Length; i++)
            {
                var pt = ScreenPoint(PlanetPosition(i, ElapsedDays), w, h);
                if (i == selectedPlanet && selectedFortress < 0) Circle(pt, 12, new Color(.8f, .95f, 1));
                Circle(pt, 8, planets[i].color);
                if (GUI.Button(new Rect(pt.x - 10, pt.y - 10, 20, 20), GUIContent.none, GUIStyle.none)) { selectedPlanet = i; selectedFortress = -1; }
                GUI.Label(new Rect(pt.x + 12, pt.y + 4, 130, 22), "第" + (i + 1) + "惑星", small);
            }
            for (int i = 0; i < fortresses.Length; i++)
            {
                var f = fortresses[i]; var pt = ScreenPoint(FortressPosition(i, ElapsedDays), w, h);
                Color color = i == selectedFortress ? Color.white : new Color(1, .45f, .3f);
                Line(pt + Vector2.up * 8, pt + Vector2.right * 8, color, 2); Line(pt + Vector2.right * 8, pt + Vector2.down * 8, color, 2);
                Line(pt + Vector2.down * 8, pt + Vector2.left * 8, color, 2); Line(pt + Vector2.left * 8, pt + Vector2.up * 8, color, 2);
                if (GUI.Button(new Rect(pt.x - 9, pt.y - 9, 18, 18), GUIContent.none, GUIStyle.none)) { selectedFortress = i; selectedPlanet = f.planetIndex; }
                GUI.Label(new Rect(pt.x + 12, pt.y - 21, 160, 22), "要塞 P" + (f.planetIndex + 1) + " · L" + f.point, small);
            }
            GUI.Label(new Rect(20, 16, w - 30, 30), "恒星系マップ", title);
            GUI.Label(new Rect(20, h - 55, w - 30, 45), "ホイール：拡大縮小   右ドラッグ：移動   天体・要塞：クリックで選択\n円軌道の試作配置 ／ 天体・要塞のマーカーサイズは距離縮尺とは別", small);
            GUI.Label(new Rect(20, 48, w - 30, 25), "表示幅 " + (2 * halfHeightAU * w / h).ToString("F2") + " AU", small);
            GUI.Label(new Rect(20, 75, w - 30, 48), (rotatingView ? "選択惑星と共回転する表示" : "恒星基準の表示"));
        }
        private void DrawSidebar()
        {
            Fill(new Rect(0, 0, Side, Screen.height), new Color(.045f, .07f, .11f));
            GUILayout.BeginArea(new Rect(14, 14, Side - 28, Screen.height - 28));
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("恒星系と要塞", title);
            GUILayout.Label("配置検討用プロトタイプ", small);
            GUILayout.Space(12);
            GUILayout.Label("経過 " + ElapsedDays.ToString("F2") + " 日", body);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "▶ 再生" : "Ⅱ 一時停止", button)) paused = !paused;
            GUILayout.EndHorizontal();
            GUILayout.Label("時間送り：" + daysPerSecond + " 日／秒", small);
            GUILayout.BeginHorizontal();
            foreach (double rate in new[] { 1d, 10d, 100d, 1000d })
                if (GUILayout.Button(rate.ToString(), daysPerSecond == rate ? selectedButton : button)) daysPerSecond = rate;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("恒星系全体を表示", button)) FitSystem();
            if (GUILayout.Button("選択対象を拡大・追従", button)) { follow = true; center = SelectionPosition(); halfHeightAU = planets[selectedPlanet].orbitData.semiMajorAxis * .12; }
            follow = GUILayout.Toggle(follow, "選択対象を追従", toggle);
            showPoints = GUILayout.Toggle(showPoints, "選択惑星の L1〜L5 を表示", toggle);
            rotatingView = GUILayout.Toggle(rotatingView, "選択惑星の共回転表示", toggle);
            GUILayout.Space(12);
            GUILayout.Label("惑星", body);
            for (int i = 0; i < planets.Length; i++)
                if (GUILayout.Button(planets[i].name, selectedPlanet == i && selectedFortress < 0 ? selectedButton : button)) { selectedPlanet = i; selectedFortress = -1; }
            GUILayout.Space(10);
            GUILayout.Label("要塞・拠点", body);
            for (int i = 0; i < fortresses.Length; i++)
                if (GUILayout.Button(fortresses[i].name + "\n第" + (fortresses[i].planetIndex + 1) + "惑星 / L" + fortresses[i].point, selectedFortress == i ? selectedButton : button)) { selectedFortress = i; selectedPlanet = fortresses[i].planetIndex; }
            GUILayout.Space(12);
            var p = planets[selectedPlanet]; var pos = SelectionPosition();
            GUILayout.Label(selectedFortress >= 0 ? fortresses[selectedFortress].name : p.name, body);
            GUILayout.Label("公転半径：" + p.orbitData.semiMajorAxis + " AU\n公転周期：" + planets[selectedPlanet].orbitData.periodDays.ToString("F1") + " 日\nX：" + pos.X.ToString("F5") + " AU\nY：" + pos.Y.ToString("F5") + " AU", small);
            GUILayout.Label("各要塞は対応するL点を自動維持。惑星同士の重力干渉・要塞内部・戦闘は未実装。数値と仮配置は Inspector で変更できます。", small);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void InputMap(Rect map)
        {
            var e = Event.current;
            if (!map.Contains(e.mousePosition)) return;
            if (e.type == EventType.ScrollWheel)
            {
                double old = halfHeightAU;
                halfHeightAU = Math.Max(.0001, Math.Min(10000, halfHeightAU * Math.Exp(e.delta.y * .08)));
                if (!follow)
                {
                    double sx = e.mousePosition.x - map.x - map.width / 2, sy = -(e.mousePosition.y - map.height / 2);
                    center += OrbitMath.Rotate(new Position2D(sx, sy) * (2 * (old - halfHeightAU) / map.height), ViewAngle(ElapsedDays));
                }
                e.Use();
            }
            if (e.type == EventType.MouseDrag && e.button == 1)
            { follow = false; center += OrbitMath.Rotate(new Position2D(-e.delta.x, e.delta.y) * (2 * halfHeightAU / map.height), ViewAngle(ElapsedDays)); e.Use(); }
        }
        private void Circle(Vector2 p, float radius, Color color)
        { GUI.color = color; GUI.DrawTexture(new Rect(p.x - radius, p.y - radius, 2 * radius, 2 * radius), disc); GUI.color = Color.white; }
        private static void Fill(Rect rect, Color color)
        { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white; }

        private void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            var saved = GUI.matrix;
            var d = b - a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y - width / 2, d.magnitude, width), color);
            GUI.matrix = saved;
        }

        // 軌道は各惑星につき LineRenderer 1 個。時間経過やズームでは再生成しない。
        private void BuildOrbitLines()
        {
            if (orbitLines != null)
            {
                foreach (var line in orbitLines)
                    if (line != null) Destroy(line.gameObject);
            }
            orbitLines = new LineRenderer[planets.Length];
            for (int i = 0; i < planets.Length; i++)
            {
                var go = new GameObject("Orbit - " + planets[i].name);
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.alignment = LineAlignment.View;
                line.numCornerVertices = 2;
                line.numCapVertices = 0;
                if (orbitMaterial != null) line.sharedMaterial = orbitMaterial;
                var orbit = PlanetOrbit(i, OrbitSegments);
                var positions = new Vector3[orbit.Length];
                for (int j = 0; j < orbit.Length; j++)
                    positions[j] = new Vector3((float)orbit[j].X, (float)orbit[j].Y, 0);
                line.positionCount = positions.Length;
                line.SetPositions(positions);
                orbitLines[i] = line;
            }
            RefreshOrbitStyles();
        }

        private void RefreshOrbitStyles()
        {
            if (orbitLines == null) return;
            float width = Mathf.Max(.000001f, (float)(2 * halfHeightAU / Math.Max(1, Screen.height) * 1.5));
            for (int i = 0; i < orbitLines.Length; i++)
            {
                var line = orbitLines[i];
                if (line == null) continue;
                var color = i == selectedPlanet ? new Color(.22f, .38f, .48f) : new Color(.1f, .18f, .25f);
                line.startColor = color;
                line.endColor = color;
                line.startWidth = width;
                line.endWidth = width;
            }
        }

        private void OnDestroy()
        {
            if (disc != null) Destroy(disc);
            if (font != null) Destroy(font);
            if (orbitMaterial != null) Destroy(orbitMaterial);
        }
    }
}
