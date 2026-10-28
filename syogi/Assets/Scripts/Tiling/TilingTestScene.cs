using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aquarium.Tiling
{
    public sealed class TilingTestScene : MonoBehaviour
    {
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
        public int DisplayedTileCount { get; private set; }
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
            for (int i = 0; i < frame.Tiles.Length; i++)
            {
                var tile = frame.Tiles[i];
                var go = new GameObject($"Section {i + 1} ({(tile.Triangle ? "Triangle" : "Square")})"); go.transform.SetParent(drawing.transform);
                var mesh = new Mesh { name = "Unit section" }; meshes.Add(mesh);
                var vertices = tile.Vertices.Select(p => new Vector3((float)p.X, (float)p.Y, 0)).ToArray();
                mesh.vertices = vertices;
                mesh.triangles = tile.Triangle ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                bool added = i >= frame.Tiles.Length - frame.Added;
                Color color = tile.Triangle ? new Color(.91f, .60f, .28f) : new Color(.25f, .62f, .84f);
                if (added) color = Color.Lerp(color, Color.white, .4f);
                mesh.colors = Enumerable.Repeat(color, vertices.Length).ToArray(); mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                AddLine(go, vertices, true, new Color(.08f, .12f, .17f), .018f);
            }
            var final = generator.Frames[generator.Frames.Count - 1].Tiles.SelectMany(t => t.Vertices).ToArray();
            float extent = Mathf.Max(.8f, (float)final.Max(p => System.Math.Abs(p.X)) + .5f);
            float ymin = (float)final.Min(p => p.Y) - .5f, ymax = (float)final.Max(p => p.Y) + .5f;
            float aspect = Mathf.Max(.1f, (float)Screen.width / Mathf.Max(1, Screen.height) / .67f);
            viewCamera.orthographicSize = Mathf.Max((ymax - ymin) / 2, extent / aspect);
            viewCamera.transform.position = new Vector3(0, (ymin + ymax) / 2, -10);
            AddLine(drawing, new[] { new Vector3(0, ymin, .02f), new Vector3(0, ymax, .02f) }, false, new Color(.4f, .45f, .5f), .01f);
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
            GUILayout.EndArea(); GUI.matrix = oldMatrix;
        }
        private void OnDestroy() { ClearDrawing(); if (material != null) Release(material); }
        private void Update()
        {
            if (generator != null && (lastWidth != Screen.width || lastHeight != Screen.height)) DrawFrame();
        }
    }
}
