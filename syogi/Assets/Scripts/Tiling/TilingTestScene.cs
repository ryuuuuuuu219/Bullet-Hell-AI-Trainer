using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aquarium.Tiling
{
    public sealed class TilingTestScene : MonoBehaviour
    {
        private enum ShipClass { Frigate, Destroyer, Cruiser, Battleship }
        private enum SectionRole { Core, Armor, Turret }
        private enum WeaponKind { SmallGun, HeavyGun, AntiShipMissile, MachineGun }

        private sealed class WeaponChoice
        {
            public WeaponKind Kind;
            public int Grade;
            public int Weight;
        }

        private sealed class TurretRuntime
        {
            public Vector2 Position;
            public WeaponKind Kind;
            public int Grade;
            public float Power;
            public float Interval;
            public float Speed;
            public float Decay;
            public float Spread;
            public float SpCost;
            public float Timer;
        }

        public enum StartAngle
        {
            [InspectorName("-30°")] Minus30 = -30,
            [InspectorName("0°")] Degrees0 = 0,
            [InspectorName("30°")] Degrees30 = 30,
            [InspectorName("45°")] Degrees45 = 45,
            [InspectorName("60°")] Degrees60 = 60,
            [InspectorName("90°")] Degrees90 = 90,
            [InspectorName("120°")] Degrees120 = 120,
            [InspectorName("135°")] Degrees135 = 135,
            [InspectorName("150°")] Degrees150 = 150,
            [InspectorName("180°")] Degrees180 = 180,
            [InspectorName("210°")] Degrees210 = 210,
            [InspectorName("225°")] Degrees225 = 225,
            [InspectorName("240°")] Degrees240 = 240,
            [InspectorName("270°")] Degrees270 = 270,
            [InspectorName("300°")] Degrees300 = 300,
            [InspectorName("315°")] Degrees315 = 315,
            [InspectorName("330°")] Degrees330 = 330
        }

        [SerializeField] private int seed = 2;
        [SerializeField, Range(1, 128)] private int targetSections = 24;
        [SerializeField, Range(1, 2000)] private int maximumAttempts = 160;
        [SerializeField] private bool triangleStart;
        [SerializeField, Tooltip("正方形の初期角度。左辺を底辺とする0°から反時計回り。Y軸対称になる角度は45°刻み。")]
        private StartAngle squareStartAngle = StartAngle.Degrees0;
        [SerializeField, Tooltip("三角形の初期角度。底辺が左の0°から反時計回り。Y軸対称になる角度は30°+60°刻み。")]
        private StartAngle triangleStartAngle = StartAngle.Degrees30;
        [SerializeField, InspectorName("艦首・艦尾を形成")] private bool formEnds = true;
        [SerializeField, Range(1, 12), InspectorName("端部の追加手数上限")] private int endSearchDepth = 6;
        [SerializeField, Range(1, 10000), InspectorName("端部の候補評価上限")] private int endSearchLimit = 1200;
        [SerializeField] private Shader tileShader = null;
        [Header("砲台射撃テスト")]
        [SerializeField] private bool enableTurretTest = true;
        [SerializeField] private ShipClass shipClass = ShipClass.Cruiser;
        [SerializeField, Min(1f)] private float sectionWorldSize = 100f;
        [SerializeField, Min(0f)] private float spPerSection = 100f;
        [SerializeField] private Vector2 bodyForward = Vector2.up;
        private TilingGenerator generator;
        private Coroutine generation;
        private Camera viewCamera;
        private GameObject drawing;
        private Material material;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private string seedText, targetText, attemptsText;
        private int frameIndex;
        private bool generating;
        private int lastWidth, lastHeight;
        private GUIStyle labelStyle;
        private readonly List<TurretRuntime> turrets = new List<TurretRuntime>();
        private System.Random turretRandom;
        private float shipSp;
        private float maximumShipSp;
        private int armorCount;
        public int DisplayedTileCount { get; private set; }
        public int TurretCount => turrets.Count;
        public int ActiveTestBulletCount => drawing == null ? 0 : drawing.GetComponentsInChildren<TilingTurretProjectile>().Length;
        public bool HasFinished => generator != null && generator.Done && !generating;

        private void Start()
        {
            seedText = seed.ToString(); targetText = targetSections.ToString(); attemptsText = maximumAttempts.ToString();
            EnsureView(); Regenerate();
        }
        private void EnsureView()
        {
            if (viewCamera == null)
            {
                // The diagram camera only covers the lower viewport. Clear the entire
                // target first so IMGUI text above it never accumulates across frames.
                var background = new GameObject("Full Screen Clear Camera");
                background.transform.SetParent(transform);
                var clearCamera = background.AddComponent<Camera>();
                clearCamera.clearFlags = CameraClearFlags.SolidColor;
                clearCamera.backgroundColor = new Color(.065f, .085f, .12f);
                clearCamera.cullingMask = 0;
                clearCamera.depth = -100;
                clearCamera.rect = new Rect(0, 0, 1, 1);
                clearCamera.allowHDR = false;
                var go = new GameObject("Tiling Camera"); go.transform.SetParent(transform);
                go.transform.position = new Vector3(0, 0, -10);
                viewCamera = go.AddComponent<Camera>(); viewCamera.orthographic = true;
                viewCamera.clearFlags = CameraClearFlags.SolidColor;
                viewCamera.backgroundColor = new Color(.065f, .085f, .12f);
                viewCamera.nearClipPlane = .1f; viewCamera.farClipPlane = 100;
                viewCamera.depth = -99;
                viewCamera.rect = new Rect(0, 0, 1, .67f);
            }
            if (material == null)
            {
                var shader = tileShader != null ? tileShader : Shader.Find("Aquarium/TilingTest");
                if (shader == null) throw new System.InvalidOperationException("TilingTest shader is missing.");
                material = new Material(shader);
            }
        }
        private void Regenerate()
        {
            if (!int.TryParse(seedText, out seed)) seed = 3;
            if (!int.TryParse(targetText, out targetSections)) targetSections = 24;
            if (!int.TryParse(attemptsText, out maximumAttempts)) maximumAttempts = 160;
            targetSections = Mathf.Clamp(targetSections, 1, 128); maximumAttempts = Mathf.Clamp(maximumAttempts, 1, 2000);
            seedText = seed.ToString(); targetText = targetSections.ToString(); attemptsText = maximumAttempts.ToString();
            if (generation != null) StopCoroutine(generation);
            generator = new TilingGenerator(seed, targetSections, maximumAttempts, triangleStart,
                (int)(triangleStart ? triangleStartAngle : squareStartAngle));
            frameIndex = 0; DrawFrame(); generation = StartCoroutine(Generate());
        }
        private IEnumerator Generate()
        {
            generating = true;
            while (!generator.Done)
            {
                int oldCount = generator.Frames.Count;
                generator.Step();
                if (oldCount != generator.Frames.Count) { frameIndex = generator.Frames.Count - 1; DrawFrame(); }
                yield return null;
            }
            if (formEnds)
                foreach (var progress in generator.FormEnds(endSearchDepth, endSearchLimit))
                {
                    if (frameIndex != generator.Frames.Count - 1) { frameIndex = generator.Frames.Count - 1; DrawFrame(); }
                    yield return null;
                }
            generating = false; generation = null;
            frameIndex = generator.Frames.Count - 1;
            DrawFrame();
        }
        // Used by the editor smoke check to exercise the same rendering path synchronously.
        public void GenerateForValidation()
        {
            EnsureView();
            generator = new TilingGenerator(seed, targetSections, maximumAttempts, triangleStart,
                (int)(triangleStart ? triangleStartAngle : squareStartAngle));
            while (!generator.Done) generator.Step();
            if (formEnds) foreach (var progress in generator.FormEnds(endSearchDepth, endSearchLimit)) { }
            frameIndex = generator.Frames.Count - 1; DrawFrame();
        }
        private void ClearDrawing()
        {
            if (drawing != null) { drawing.SetActive(false); Release(drawing); }
            foreach (var mesh in meshes) Release(mesh);
            meshes.Clear();
        }
        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private void DrawFrame()
        {
            lastWidth = Screen.width; lastHeight = Screen.height;
            ClearDrawing();
            drawing = new GameObject("Generated Sections"); drawing.transform.SetParent(transform);
            var frame = generator.Frames[frameIndex]; DisplayedTileCount = frame.Tiles.Length;
            var sectionObjects = new List<GameObject>(frame.Tiles.Length);
            for (int i = 0; i < frame.Tiles.Length; i++)
            {
                var tile = frame.Tiles[i];
                var go = new GameObject($"Section {i + 1} ({(tile.Triangle ? "Triangle" : "Square")})"); go.transform.SetParent(drawing.transform);
                sectionObjects.Add(go);
                var mesh = new Mesh { name = "Unit section" }; meshes.Add(mesh);
                var vertices = tile.Vertices.Select(p => new Vector3((float)p.X, (float)p.Y, 0) * sectionWorldSize).ToArray();
                mesh.vertices = vertices;
                mesh.triangles = tile.Triangle ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                bool added = i >= frame.Tiles.Length - frame.Added;
                Color color = tile.Triangle ? new Color(.91f, .60f, .28f) : new Color(.25f, .62f, .84f);
                if (added) color = Color.Lerp(color, Color.white, .4f);
                mesh.colors = Enumerable.Repeat(color, vertices.Length).ToArray(); mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                AddLine(go, vertices, true, new Color(.08f, .12f, .17f), .018f * sectionWorldSize);
            }
            var final = generator.Frames[generator.Frames.Count - 1].Tiles.SelectMany(t => t.Vertices).ToArray();
            float extent = Mathf.Max(.8f, (float)final.Max(p => System.Math.Abs(p.X)) + .5f) * sectionWorldSize;
            float ymin = ((float)final.Min(p => p.Y) - .5f) * sectionWorldSize;
            float ymax = ((float)final.Max(p => p.Y) + .5f) * sectionWorldSize;
            float aspect = Mathf.Max(.1f, (float)Screen.width / Mathf.Max(1, Screen.height) / .67f);
            viewCamera.orthographicSize = Mathf.Max((ymax - ymin) / 2, extent / aspect);
            viewCamera.transform.position = new Vector3(0, (ymin + ymax) / 2, -10);
            AddLine(drawing, new[] { new Vector3(0, ymin, .02f), new Vector3(0, ymax, .02f) }, false, new Color(.4f, .45f, .5f), .01f * sectionWorldSize);
            if (enableTurretTest && !generating && frameIndex == generator.Frames.Count - 1)
                SetupTurrets(frame.Tiles, sectionObjects);
        }

        private void SetupTurrets(TilingGenerator.Tile[] tiles, List<GameObject> sectionObjects)
        {
            turrets.Clear(); armorCount = 0;
            turretRandom = new System.Random(unchecked(seed * 397) ^ (int)shipClass * 7919 ^ tiles.Length);
            maximumShipSp = Mathf.Max(0f, spPerSection) * tiles.Length;
            shipSp = maximumShipSp;
            if (tiles.Length == 0) return;

            var roles = new List<SectionRole>(tiles.Length - 1);
            int nonCore = tiles.Length - 1;
            int turretCount = nonCore / 2;
            armorCount = nonCore - turretCount;
            for (int i = 0; i < armorCount; i++) roles.Add(SectionRole.Armor);
            for (int i = 0; i < turretCount; i++) roles.Add(SectionRole.Turret);
            Shuffle(roles, turretRandom);

            ColorizeSection(sectionObjects[0], new Color(.32f, .95f, .48f), "Core");
            var choices = BuildWeaponChoices(shipClass);
            for (int i = 1; i < tiles.Length; i++)
            {
                SectionRole role = roles[i - 1];
                if (role == SectionRole.Armor)
                {
                    ColorizeSection(sectionObjects[i], new Color(.86f, .30f, .30f), "Armor");
                    AddMarker(sectionObjects[i], Centroid(tiles[i]) * sectionWorldSize,
                        new Color(1f, .18f, .18f), .18f * sectionWorldSize);
                    continue;
                }

                WeaponChoice choice = PickWeighted(choices, turretRandom);
                var turret = CreateTurret(choice, Centroid(tiles[i]) * sectionWorldSize, turretRandom);
                turrets.Add(turret);
                ColorizeSection(sectionObjects[i], WeaponColor(choice.Kind), $"Turret {WeaponLabel(choice.Kind)}{GradeLabel(choice.Grade)}");
                AddMarker(sectionObjects[i], turret.Position, Color.white, .12f * sectionWorldSize);
            }
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T value = values[i]; values[i] = values[j]; values[j] = value;
            }
        }

        private static Vector2 Centroid(TilingGenerator.Tile tile)
        {
            double x = 0, y = 0;
            foreach (var point in tile.Vertices) { x += point.X; y += point.Y; }
            return new Vector2((float)(x / tile.Vertices.Length), (float)(y / tile.Vertices.Length));
        }

        private void ColorizeSection(GameObject section, Color color, string role)
        {
            section.name += " - " + role;
            MeshFilter filter = section.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            filter.sharedMesh.colors = Enumerable.Repeat(color, filter.sharedMesh.vertexCount).ToArray();
        }

        private void AddMarker(GameObject section, Vector2 center, Color color, float radius)
        {
            var marker = new GameObject("Role Marker"); marker.transform.SetParent(section.transform, false);
            var line = marker.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = false; line.loop = true; line.positionCount = 24;
            line.startWidth = line.endWidth = .018f * sectionWorldSize;
            line.startColor = line.endColor = color;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius, -.03f));
            }
        }

        private static List<WeaponChoice> BuildWeaponChoices(ShipClass rank)
        {
            var result = new List<WeaponChoice>
            {
                new WeaponChoice { Kind = WeaponKind.SmallGun, Grade = 1, Weight = 2 },
                new WeaponChoice { Kind = WeaponKind.SmallGun, Grade = 2, Weight = 3 },
                new WeaponChoice { Kind = WeaponKind.MachineGun, Grade = 1, Weight = 4 }
            };
            if ((int)rank >= (int)ShipClass.Destroyer)
            {
                result.Add(new WeaponChoice { Kind = WeaponKind.SmallGun, Grade = 3, Weight = 5 });
                result.Add(new WeaponChoice { Kind = WeaponKind.MachineGun, Grade = 2, Weight = 6 });
                result.Add(new WeaponChoice { Kind = WeaponKind.HeavyGun, Grade = 1, Weight = 15 });
                result.Add(new WeaponChoice { Kind = WeaponKind.SmallGun, Grade = 1, Weight = 8 });
                result.Add(new WeaponChoice { Kind = WeaponKind.MachineGun, Grade = 2, Weight = 10 });
            }
            if ((int)rank >= (int)ShipClass.Cruiser)
            {
                result.Add(new WeaponChoice { Kind = WeaponKind.AntiShipMissile, Grade = 1, Weight = 35 });
                result.Add(new WeaponChoice { Kind = WeaponKind.SmallGun, Grade = 1, Weight = 20 });
                result.Add(new WeaponChoice { Kind = WeaponKind.MachineGun, Grade = 2, Weight = 25 });
            }
            if ((int)rank >= (int)ShipClass.Battleship)
                result.Add(new WeaponChoice { Kind = WeaponKind.HeavyGun, Grade = 2, Weight = 60 });
            return result;
        }

        private static WeaponChoice PickWeighted(List<WeaponChoice> choices, System.Random random)
        {
            int total = choices.Sum(c => c.Weight), roll = random.Next(total);
            foreach (WeaponChoice choice in choices)
            {
                if (roll < choice.Weight) return choice;
                roll -= choice.Weight;
            }
            return choices[choices.Count - 1];
        }

        private static TurretRuntime CreateTurret(WeaponChoice choice, Vector2 position, System.Random random)
        {
            float sample = (float)random.NextDouble();
            var turret = new TurretRuntime { Position = position, Kind = choice.Kind, Grade = choice.Grade };
            switch (choice.Kind)
            {
                case WeaponKind.SmallGun:
                    turret.Power = Mathf.Lerp(50f, 100f, sample) + choice.Grade * 50f;
                    turret.Interval = 2f; turret.Speed = 450f; turret.Decay = 10f; turret.Spread = 5f; turret.SpCost = 10f; break;
                case WeaponKind.HeavyGun:
                    turret.Power = Mathf.Lerp(300f, 500f, sample) + choice.Grade * 100f;
                    turret.Interval = 5f; turret.Speed = 600f; turret.Decay = 30f; turret.Spread = 3f; turret.SpCost = 10f; break;
                case WeaponKind.AntiShipMissile:
                    turret.Power = Mathf.Lerp(100f, 200f, sample) + choice.Grade * 60f;
                    turret.Interval = 5f; turret.Speed = 600f; turret.Decay = 50f; turret.Spread = 0f; turret.SpCost = 10f; break;
                default:
                    turret.Power = 10f + choice.Grade * 30f;
                    turret.Interval = .1f; turret.Speed = 400f; turret.Decay = 10f; turret.Spread = 12f; turret.SpCost = .3f; break;
            }
            turret.Timer = 0f; // Loading begins at birth; no immediate first shot.
            return turret;
        }

        private static Color WeaponColor(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.SmallGun: return new Color(.95f, .82f, .25f);
                case WeaponKind.HeavyGun: return new Color(.95f, .45f, .15f);
                case WeaponKind.AntiShipMissile: return new Color(.75f, .35f, .95f);
                default: return new Color(.25f, .90f, .95f);
            }
        }

        private static string WeaponLabel(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.SmallGun: return "Small";
                case WeaponKind.HeavyGun: return "Heavy";
                case WeaponKind.AntiShipMissile: return "Missile";
                default: return "Machine";
            }
        }

        private static string GradeLabel(int grade) => grade <= 0 ? "" : ((char)('A' + grade - 1)).ToString();

        private static float ProjectileRadius(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.MachineGun: return 2f;
                case WeaponKind.AntiShipMissile: return 7f;
                default: return 5f;
            }
        }

        private static Color ProjectileColor(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.MachineGun: return Color.yellow;
                case WeaponKind.SmallGun: return new Color(1f, .5f, 0f);
                case WeaponKind.HeavyGun: return new Color(1f, .25f, 0f);
                default: return Color.red;
            }
        }

        private void TickTurrets()
        {
            if (!enableTurretTest || turrets.Count == 0 || drawing == null || turretRandom == null) return;
            Vector2 target = GetAimPoint();
            foreach (TurretRuntime turret in turrets)
            {
                turret.Timer += Time.deltaTime;
                while (turret.Timer >= turret.Interval)
                {
                    turret.Timer -= turret.Interval;
                    if (shipSp + .000001f < turret.SpCost) continue;
                    shipSp = Mathf.Max(0f, shipSp - turret.SpCost);
                    Fire(turret, target);
                }
            }
        }

        private Vector2 GetAimPoint()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || viewCamera == null)
                return bodyForward.sqrMagnitude > 0 ? bodyForward.normalized * sectionWorldSize * 10f : Vector2.up * sectionWorldSize * 10f;
            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.forward, Vector3.zero);
            return plane.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance)
                : (bodyForward.sqrMagnitude > 0 ? bodyForward.normalized * sectionWorldSize * 10f : Vector2.up * sectionWorldSize * 10f);
        }

        private void Fire(TurretRuntime turret, Vector2 target)
        {
            Vector2 direction = target - turret.Position;
            if (direction.sqrMagnitude <= .000001f)
                direction = bodyForward.sqrMagnitude > 0 ? bodyForward.normalized : Vector2.up;
            float scatter = turret.Spread <= 0 ? 0f : ((float)turretRandom.NextDouble() - .5f) * turret.Spread;
            direction = Quaternion.Euler(0, 0, scatter) * direction.normalized;
            var bulletObject = new GameObject($"{WeaponLabel(turret.Kind)}{GradeLabel(turret.Grade)} Test Bullet");
            bulletObject.transform.position = new Vector3(turret.Position.x, turret.Position.y, -.05f);
            bulletObject.transform.SetParent(drawing.transform, true);
            bulletObject.AddComponent<TilingTurretProjectile>().Initialize(material, direction, turret.Speed,
                ProjectileRadius(turret.Kind), 1f,
                Mathf.Max(.01f, turret.Power / Mathf.Max(.01f, turret.Decay)), ProjectileColor(turret.Kind),
                turret.Kind != WeaponKind.MachineGun);
        }
        private void AddLine(GameObject parent, Vector3[] points, bool loop, Color color, float width)
        {
            var go = new GameObject("Outline"); go.transform.SetParent(parent.transform);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = false; line.loop = loop; line.positionCount = points.Length;
            line.SetPositions(points.Select(p => p + new Vector3(0, 0, -.01f)).ToArray());
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
        }
        private void OnGUI()
        {
            if (generator == null) return;
            // Labels are transparent; repaint the panel before drawing updated text.
            var oldColor = GUI.color;
            GUI.color = new Color(.10f, .13f, .18f, 1);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * .33f), Texture2D.whiteTexture);
            GUI.color = oldColor;
            if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            // Fit the panel to a logical width, including small Game view windows.
            float scale = Mathf.Clamp(Screen.width / 850f, .55f, 1.4f);
            var oldMatrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(12, 8, Screen.width / scale - 24, Screen.height * .33f / scale - 8));
            GUILayout.Label(formEnds ? $"端部：上 {generator.UpperEndStatus} / 下 {generator.LowerEndStatus} / 追加{generator.EndTilesAdded}区画" : "タイリングテスト / 辺長1・Y軸対称 / 端部形成OFF", labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("シード", GUILayout.Width(45)); seedText = GUILayout.TextField(seedText, GUILayout.Width(70));
            GUILayout.Label("目安区画数", GUILayout.Width(72)); targetText = GUILayout.TextField(targetText, GUILayout.Width(45));
            GUILayout.Label("試行上限", GUILayout.Width(62)); attemptsText = GUILayout.TextField(attemptsText, GUILayout.Width(55));
            triangleStart = GUILayout.Toggle(triangleStart, "初期：三角形");
            if (GUILayout.Button("再生成", GUILayout.Width(70))) Regenerate();
            GUILayout.EndHorizontal();
            GUILayout.Label($"初期角度：{(int)(triangleStart ? triangleStartAngle : squareStartAngle)}°（Inspectorで変更後、再生成）", labelStyle);
            string status = !generator.InitialSymmetric ? "初期区画がY軸非対称：角度を変更してください（形状のみ表示）"
                : generating ? "生成中" : generator.Reached ? "目安に到達" : "試行上限で停止";
            GUILayout.Label($"{status}  確定{generator.Count}区画 / {generator.Attempts}試行 / 増設{generator.Accepted}回 / 軸上補完{generator.AxisGapFilled}区画 / 重なり{generator.OverlapRejected}回", labelStyle);
            GUI.enabled = !generating;
            GUILayout.BeginHorizontal();
            int next = frameIndex;
            if (GUILayout.Button("前", GUILayout.Width(45))) next--;
            next = Mathf.RoundToInt(GUILayout.HorizontalSlider(next, 0, generator.Frames.Count - 1));
            if (GUILayout.Button("次", GUILayout.Width(45))) next++;
            if (GUILayout.Button("最終", GUILayout.Width(45))) next = generator.Frames.Count - 1;
            GUILayout.EndHorizontal(); GUI.enabled = true;
            next = Mathf.Clamp(next, 0, generator.Frames.Count - 1);
            if (next != frameIndex) { frameIndex = next; DrawFrame(); }
            var frame = generator.Frames[frameIndex];
            GUILayout.Label($"過程 {frameIndex + 1}/{generator.Frames.Count} : {frame.Label} / 表示{frame.Tiles.Length}区画   青：正方形  橙：三角形  明色：今回追加", labelStyle);
            if (enableTurretTest && !generating && frameIndex == generator.Frames.Count - 1)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"砲撃テスト：{shipClass} / コア1 / 装甲{armorCount} / 砲塔{turrets.Count} / SP {shipSp:0.0}/{maximumShipSp:0.0}", labelStyle);
                if (GUILayout.Button("SP全回復", GUILayout.Width(80))) shipSp = maximumShipSp;
                GUILayout.EndHorizontal();
                GUILayout.Label("緑：コア　赤：装甲　黄・橙・紫・水色：砲塔　砲塔はマウス現在位置へ自動射撃", labelStyle);
            }
            GUILayout.EndArea(); GUI.matrix = oldMatrix;
        }
        private void OnDestroy() { ClearDrawing(); if (material != null) Release(material); }
        private void Update()
        {
            if (generator != null && (lastWidth != Screen.width || lastHeight != Screen.height)) DrawFrame();
            TickTurrets();
        }
    }
}
