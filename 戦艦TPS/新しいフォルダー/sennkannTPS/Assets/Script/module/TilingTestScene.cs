using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Script
{
    public sealed partial class TilingTestScene : MonoBehaviour
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
            public float GuidanceTurnSpeed;
            public float NavigationConstant;
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
        [SerializeField] private bool enableTurretTest = false;
        [SerializeField] private ShipClass shipClass = ShipClass.Cruiser;
        [SerializeField, Min(1f)] private float sectionWorldSize = 100f;
        [SerializeField, Min(0f)] private float spPerSection = 100f;
        [SerializeField] private Vector2 bodyForward = Vector2.up;
        [Header("防護分析・試験弾の発射")]
        [SerializeField] private bool protectionTestMode = false;
        [SerializeField, Min(.01f), Tooltip("ドラッグのワールド座標差に掛ける初速倍率（1/秒）")]
        private float launchVelocityScale = 1f;
        [SerializeField, Min(.1f)] private float testProjectileRadius = 5f;
        [SerializeField, Min(.1f)] private float testProjectileLifetime = 15f;
        [SerializeField, Min(.01f)] private float testProjectilePower = 100f;
        [SerializeField, Min(.01f)] private float testProjectilePenetration = 1f;
        [SerializeField, InspectorName("試験弾の弾種")] private BattleshipProjectile.Affect testProjectileAffect = BattleshipProjectile.Affect.AP;
        [SerializeField] private ProjectileEffectSettings projectileEffects = new ProjectileEffectSettings();
        [SerializeField, InspectorName("電磁バリア有効")] private bool testBarrierEnabled;
        [SerializeField, Min(1), InspectorName("バリア目安区画数（試験設定）")] private int barrierReferenceSections = 24;
        [SerializeField, InspectorName("バリア退出時にも軽減（未確定・試験設定）")] private bool barrierReduceOnExit;
        private LineRenderer barrierOutline;
        // Legacy serialized value; radius is now derived from launch power for all explosive ammo.
        [SerializeField, HideInInspector] private float testExplosionRadius = 100f;
        [SerializeField, InspectorName("試験標的の装甲モード")] private ArmorMode armorMode = ArmorMode.Basic;
        [SerializeField, Min(0f), InspectorName("試験標的の実装甲厚")] private float armorThickness = 50f;
        [SerializeField, Min(0f), InspectorName("ERA印加ベクトルの絶対値")] private float eraVectorMagnitude = 50f;
        private float appliedArmorThickness = float.NaN;
        [SerializeField, Min(0f), InspectorName("試験標的の装甲区画の迎撃力")]
        private double armorInterception = 0.1d;
        private double appliedArmorInterception = double.NaN;
        private float appliedEraVectorMagnitude = float.NaN;
        private BattleshipInstance testTarget;
        private readonly Queue<string> hitLog = new Queue<string>();
        private ProtectionTestLauncher testLauncher;
        private GameObject testProjectiles;
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
        private sealed class FloatingHit
        {
            public Vector2 Position;
            public string Text;
            public float StartedAt;
        }
        private const float FloatingHitSeconds = 2f;
        private readonly List<FloatingHit> floatingHits = new List<FloatingHit>();
        private GUIStyle floatingHitStyle;
        private Vector2 panelScroll;
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
            LoadShipBuildSettings();
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
                var shader = tileShader != null ? tileShader : Shader.Find("Battleship/TilingTest");
                if (shader == null) throw new System.InvalidOperationException("TilingTest shader is missing.");
                material = new Material(shader);
            }
            if (testLauncher == null)
            {
                testProjectiles = new GameObject("Protection Test Projectiles");
                testProjectiles.transform.SetParent(transform, false);
                testLauncher = gameObject.AddComponent<ProtectionTestLauncher>();
                testLauncher.Initialize(viewCamera, material, testProjectiles.transform);
            }
        }
        private void Regenerate()
        {
            if (!int.TryParse(seedText, out int nextSeed) || !int.TryParse(targetText, out int nextTarget) ||
                !int.TryParse(attemptsText, out int nextAttempts) || nextTarget < 1 || nextTarget > 128 || nextAttempts < 1 || nextAttempts > 2000)
            {
                generationInputError = "シードは整数、目安区画数は1～128、試行上限は1～2000で入力してください。";
                return;
            }
            generationInputError = null;
            seed = nextSeed; targetSections = nextTarget; maximumAttempts = nextAttempts;
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
            if (generation != null) StopCoroutine(generation);
            generation = null;
            generating = false;
            EnsureView();
            generator = new TilingGenerator(seed, targetSections, maximumAttempts, triangleStart,
                (int)(triangleStart ? triangleStartAngle : squareStartAngle));
            while (!generator.Done) generator.Step();
            if (formEnds) foreach (var progress in generator.FormEnds(endSearchDepth, endSearchLimit)) { }
            frameIndex = generator.Frames.Count - 1; DrawFrame();
        }
        private void ClearDrawing()
        {
            StopMovementTrial();
            ClearTestProjectiles();
            testTarget = null;
            hitLog.Clear();
            floatingHits.Clear();
            if (drawing != null) { drawing.SetActive(false); Release(drawing); }
            foreach (var mesh in meshes) Release(mesh);
            meshes.Clear();
        }
        private void ClearTestProjectiles()
        {
            if (testLauncher != null) testLauncher.Cancel();
            if (testProjectiles == null) return;
            for (int i = testProjectiles.transform.childCount - 1; i >= 0; i--)
            {
                GameObject bullet = testProjectiles.transform.GetChild(i).gameObject;
                bullet.SetActive(false);
                Release(bullet);
            }
        }
        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        private const float OutlineWidthScale = .018f;
        private void DrawFrame()
        {
            lastWidth = Screen.width; lastHeight = Screen.height;
            turrets.Clear();
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
                AddLine(go, vertices, true, new Color(.08f, .12f, .17f), OutlineWidthScale * sectionWorldSize);
            }
            UpdateView();
            float ymin = viewCamera.transform.position.y - viewCamera.orthographicSize;
            float ymax = viewCamera.transform.position.y + viewCamera.orthographicSize;
            AddLine(drawing, new[] { new Vector3(0, ymin, .02f), new Vector3(0, ymax, .02f) }, false, new Color(.4f, .45f, .5f), .01f * sectionWorldSize);
            if (!generating && frameIndex == generator.Frames.Count - 1)
                SetupTurrets(frame.Tiles, sectionObjects);
        }

        private void UpdateView()
        {
            lastWidth = Screen.width; lastHeight = Screen.height;
            var final = generator.Frames[generator.Frames.Count - 1].Tiles.SelectMany(t => t.Vertices).ToArray();
            float extent = Mathf.Max(.8f, (float)final.Max(p => System.Math.Abs(p.X)) + .5f) * sectionWorldSize;
            float ymin = ((float)final.Min(p => p.Y) - .5f) * sectionWorldSize;
            float ymax = ((float)final.Max(p => p.Y) + .5f) * sectionWorldSize;
            float aspect = Mathf.Max(.1f, (float)Screen.width / Mathf.Max(1, Screen.height) / .67f);
            viewCamera.orthographicSize = Mathf.Max((ymax - ymin) / 2, extent / aspect);
            viewCamera.transform.position = new Vector3(0, (ymin + ymax) / 2, -10);
        }

        private void SetupTurrets(TilingGenerator.Tile[] tiles, List<GameObject> sectionObjects)
        {
            turrets.Clear(); armorCount = 0;
            turretRandom = new System.Random(unchecked(seed * 397) ^ (int)shipClass * 7919 ^ tiles.Length);
            maximumShipSp = Mathf.Max(0f, spPerSection) * tiles.Length;
            shipSp = maximumShipSp;
            if (tiles.Length == 0) return;
            if (manualWeaponAllocation && manualArmorAllocation && TryGetBuild(out var build, out _, false))
            {
                testTarget = ShipBuilder.Attach(build, drawing, tiles, sectionObjects, true, false);
                armorCount = testTarget.GetComponentsInChildren<BattleshipSection>().Count(s => s.Role == BattleshipSectionRole.Armor);
                foreach (var section in testTarget.GetComponentsInChildren<BattleshipSection>())
                {
                    section.GetComponent<Collider2D>().isTrigger = false;
                    section.HitResolved += ShowFloatingHit;
                    RefreshArmorMarker(section.gameObject, section);
                    var weapon = section.ConstructionWeapon;
                    if (weapon != null)
                    {
                        var point = Centroid(tiles[section.SectionId]) * sectionWorldSize;
                        turrets.Add(new TurretRuntime { Position = point, Kind = (WeaponKind)weapon.Kind,
                            Grade = 1, Power = weapon.Power, Interval = weapon.Interval, Speed = weapon.Speed,
                            GuidanceTurnSpeed = weapon.GuidanceTurnSpeed, NavigationConstant = weapon.NavigationConstant,
                            Timer = weapon.Interval });
                        AddMarker(section.gameObject, point, Color.white, .12f * sectionWorldSize);
                    }
                }
                SetupBarrierOutline();
                Physics2D.SyncTransforms();
                return;
            }
            testTarget = drawing.AddComponent<BattleshipInstance>();
            var sections = new List<BattleshipSection>(tiles.Length);

            var roles = new List<SectionRole>(tiles.Length - 1);
            int nonCore = tiles.Length - 1;
            int turretCount = manualWeaponAllocation ? Mathf.Min(nonCore, RequestedTurretTotal) : nonCore / 2;
            armorCount = nonCore - turretCount;
            for (int i = 0; i < armorCount; i++) roles.Add(SectionRole.Armor);
            for (int i = 0; i < turretCount; i++) roles.Add(SectionRole.Turret);
            Shuffle(roles, turretRandom);

            ColorizeSection(sectionObjects[0], new Color(.32f, .95f, .48f), "Core");
            sections.Add(CreateTestSection(sectionObjects[0], 0, BattleshipSectionRole.Core));
            var choices = BuildWeaponChoices(shipClass);
            List<WeaponChoice> manualChoices = null;
            int manualTurretIndex = 0;
            if (manualWeaponAllocation)
            {
                manualChoices = new List<WeaponChoice>(turretCount);
                for (int kindIndex = 0; kindIndex < requestedWeaponCounts.Length && manualChoices.Count < turretCount; kindIndex++)
                {
                    var kind = (WeaponKind)kindIndex;
                    var availableChoices = choices.Where(x => x.Kind == kind).ToList();
                    if (availableChoices.Count == 0)
                        availableChoices.Add(new WeaponChoice { Kind = kind, Grade = 1, Weight = 1 });
                    for (int j = 0; j < requestedWeaponCounts[kindIndex] && manualChoices.Count < turretCount; j++)
                        manualChoices.Add(PickWeighted(availableChoices, turretRandom));
                }
                Shuffle(manualChoices, turretRandom);
            }
            // Compute a deterministic per-armor-section preset list, without consuming turret RNG.
            var armorAssignments = new List<ArmorMode>(armorCount);
            if (manualArmorAllocation)
            {
                for (int kindIndex = 0; kindIndex < requestedArmorCounts.Length && armorAssignments.Count < armorCount; kindIndex++)
                    for (int j = 0; j < requestedArmorCounts[kindIndex] && armorAssignments.Count < armorCount; j++)
                        armorAssignments.Add((ArmorMode)kindIndex);
                while (armorAssignments.Count < armorCount) armorAssignments.Add(ArmorMode.Basic);
                Shuffle(armorAssignments, new System.Random(unchecked(seed * 733 + tiles.Length * 37 + 1151)));
            }
            int nextArmorIndex = 0;
            for (int i = 1; i < tiles.Length; i++)
            {
                SectionRole role = roles[i - 1];
                sections.Add(CreateTestSection(sectionObjects[i], i,
                    role == SectionRole.Armor ? BattleshipSectionRole.Armor : BattleshipSectionRole.Turret));
                if (role == SectionRole.Armor)
                {
                    if (manualArmorAllocation) sections[i].Mode = armorAssignments[nextArmorIndex++];

                    RefreshArmorMarker(sectionObjects[i], sections[i]);

                    ColorizeSection(sectionObjects[i], new Color(.86f, .30f, .30f), "Armor");
                    AddMarker(sectionObjects[i], Centroid(tiles[i]) * sectionWorldSize,
                        new Color(1f, .18f, .18f), .18f * sectionWorldSize);
                    continue;
                }

                WeaponChoice choice = manualWeaponAllocation ? manualChoices[manualTurretIndex++] : PickWeighted(choices, turretRandom);
                var turret = CreateTurret(choice, Centroid(tiles[i]) * sectionWorldSize, turretRandom);
                turrets.Add(turret);
                ColorizeSection(sectionObjects[i], WeaponColor(choice.Kind), $"Turret {WeaponLabel(choice.Kind)}{GradeLabel(choice.Grade)}");
                AddMarker(sectionObjects[i], turret.Position, Color.white, .12f * sectionWorldSize);
            }
            // Use the generator's shared edges for the combat model's detachment rules.
            for (int i = 0; i < tiles.Length; i++)
            for (int j = i + 1; j < tiles.Length; j++)
            {
                int shared = tiles[i].Vertices.Count(a => tiles[j].Vertices.Any(b =>
                    System.Math.Abs(a.X - b.X) < TilingGenerator.Epsilon &&
                    System.Math.Abs(a.Y - b.Y) < TilingGenerator.Epsilon));
                if (shared < 2) continue;
                sections[i].AddNeighbor(sections[j]);
                sections[j].AddNeighbor(sections[i]);
            }
            testTarget.Initialize((BattleshipClass)shipClass, targetSections, tiles.Length,
                seed, sections, stationary: true);
            SetupBarrierOutline();
            Physics2D.SyncTransforms();
        }

        private void SetupBarrierOutline()
        {
            var barrierObject = new GameObject("電磁バリア");
            barrierObject.transform.SetParent(testTarget.transform, false);
            barrierOutline = barrierObject.AddComponent<LineRenderer>();
            barrierOutline.sharedMaterial = material;
            barrierOutline.useWorldSpace = true;
            barrierOutline.loop = true;
            barrierOutline.positionCount = 96;
            barrierOutline.startColor = barrierOutline.endColor = new Color(.1f, .8f, 1f, .5f);
            barrierOutline.widthMultiplier = 1f;
            UpdateBarrier();
        }

        #region marker
        #endregion
        private BattleshipSection CreateTestSection(GameObject section, int id, BattleshipSectionRole role)
        {
            var collider = section.AddComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            collider.SetPath(0, section.GetComponent<MeshFilter>().sharedMesh.vertices
                .Select(p => new Vector2(p.x, p.y)).ToArray());
            var runtime = section.AddComponent<BattleshipSection>();
            runtime.Initialize(testTarget, id, role, null);
            runtime.Mode = armorMode;
            runtime.ArmorThickness = armorThickness;
            runtime.ArmorInterception = armorInterception;
            runtime.EraVectorMagnitude = eraVectorMagnitude;
            runtime.HitResolved += ShowFloatingHit;
            // Static analysis geometry: the probe uses casts, not trigger callbacks.
            // Solid colliders remain queryable when queriesHitTriggers is disabled.
            collider.isTrigger = false;


            RefreshArmorMarker(section, runtime);

            return runtime;
        }

        private const string ArmorMarkerObjectName = "Armor Marker";
        private const float EraScale = 0.9f;

        private void RefreshArmorMarker(GameObject sectionObject, BattleshipSection runtime)
        {
            if (sectionObject == null || runtime == null) return;

            var old = sectionObject.transform.Find(ArmorMarkerObjectName);
            if (old != null) Destroy(old.gameObject);

            if (runtime.Role != BattleshipSectionRole.Armor) return;
            if (runtime.Mode == ArmorMode.Basic) return;

            var meshFilter = sectionObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            Vector3[] vertices3 = meshFilter.sharedMesh.vertices;
            if (vertices3 == null || vertices3.Length < 3) return;

            Vector3[] markerPoints;

            if (runtime.Mode == ArmorMode.ERA)
            {
                Vector3 center = Vector3.zero;
                foreach (var v in vertices3) center += v;
                center /= vertices3.Length;

                markerPoints = vertices3
                    .Select(v => center + (v - center) * EraScale + new Vector3(0, 0, -0.015f))
                    .ToArray();
            }
            else // ArmorMode.AntiEM
            {
                int n = vertices3.Length;
                markerPoints = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = vertices3[i];
                    Vector3 b = vertices3[(i + 1) % n];
                    markerPoints[i] = (a + b) * 0.5f + new Vector3(0, 0, -0.015f);
                }
            }

            var marker = new GameObject(ArmorMarkerObjectName);
            marker.transform.SetParent(sectionObject.transform, false);

            var line = marker.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = markerPoints.Length;
            line.SetPositions(markerPoints);
            line.startWidth = OutlineWidthScale * sectionWorldSize;
            line.endWidth = OutlineWidthScale * sectionWorldSize;
            line.startColor = Color.white;
            line.endColor = Color.white;
        }

        private void RefreshAllArmorMarkers()
        {
            if (testTarget == null) return;

            foreach (var section in testTarget.GetComponentsInChildren<BattleshipSection>())
            {
                RefreshArmorMarker(section.gameObject, section);
            }
        }

        private void RecordTestHit(string result)
        {
            hitLog.Enqueue(result);
            while (hitLog.Count > 12) hitLog.Dequeue();
        }

        private void ShowFloatingHit(Vector2 position, double health, double damage, double maximum)
        {
            if (floatingHits.Count >= 64) floatingHits.RemoveAt(0);
            floatingHits.Add(new FloatingHit {
                Position = position,
                Text = $"{health:0.##}（-{damage:0.##}）/{maximum:0.##}",
                StartedAt = Time.time
            });
        }

        private void DrawFloatingHits()
        {
            if (viewCamera == null || floatingHits.Count == 0) return;
            if (floatingHitStyle == null)
                floatingHitStyle = new GUIStyle(GUI.skin.label) {
                    alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold
                };
            Rect viewport = viewCamera.pixelRect;
            var clip = new Rect(viewport.x, Screen.height - viewport.yMax, viewport.width, viewport.height);
            Color previousColor = GUI.color;
            GUI.BeginGroup(clip);
            foreach (FloatingHit hit in floatingHits)
            {
                float age = Time.time - hit.StartedAt;
                if (age >= FloatingHitSeconds) continue;
                Vector3 point = viewCamera.WorldToScreenPoint(hit.Position);
                if (point.z <= 0) continue;
                float alpha = 1f - Mathf.Clamp01(age / FloatingHitSeconds);
                var content = new GUIContent(hit.Text);
                Vector2 size = floatingHitStyle.CalcSize(content) + new Vector2(12, 4);
                float x = Mathf.Clamp(point.x - clip.x - size.x * .5f, 0, Mathf.Max(0, clip.width - size.x));
                float y = Mathf.Clamp(Screen.height - point.y - clip.y - size.y - age * 30f,
                    0, Mathf.Max(0, clip.height - size.y));
                GUI.color = new Color(1, 1, 1, alpha);
                floatingHitStyle.normal.textColor = Color.black;
                GUI.Label(new Rect(x + 1, y + 1, size.x, size.y), content, floatingHitStyle);
                floatingHitStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y, size.x, size.y), content, floatingHitStyle);
            }
            GUI.EndGroup();
            GUI.color = previousColor;
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
                    turret.Interval = 5f; turret.Speed = 600f; turret.Decay = 50f; turret.Spread = 0f; turret.SpCost = 10f;
                    turret.GuidanceTurnSpeed = 50f + choice.Grade * 20f;
                    turret.NavigationConstant = Mathf.Lerp(1f, 3f, (float)random.NextDouble());
                    break;
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
                default: return new Color(1f, .3f, .65f);
            }
        }

        private void TickTurrets()
        {
            if (protectionTestMode || !enableTurretTest || turrets.Count == 0 || drawing == null || turretRandom == null) return;
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
            bool guided = turret.Kind == WeaponKind.AntiShipMissile;
            bulletObject.AddComponent<TilingTurretProjectile>().Initialize(material, direction, turret.Speed,
                ProjectileRadius(turret.Kind), 1f,
                Mathf.Max(.01f, turret.Power / Mathf.Max(.01f, turret.Decay)), ProjectileColor(turret.Kind),
                turret.Kind != WeaponKind.MachineGun, guided, turret.GuidanceTurnSpeed,
                turret.NavigationConstant, guided ? GetAimPoint : null);
        }
        private void AddLine(GameObject parent, Vector3[] points, bool loop, Color color, float width)
        {
            var go = new GameObject("Outline"); go.transform.SetParent(parent.transform);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = false; line.loop = loop; line.positionCount = points.Length;
            line.SetPositions(points.Select(p => p + new Vector3(0, 0, -.01f)).ToArray());
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
        }
        // Manual weapon allocation uses only the weapon kinds already supported by this test scene.
        // Order follows WeaponKind: SmallGun, HeavyGun, AntiShipMissile, MachineGun.
        private bool manualWeaponAllocation;
        private readonly int[] requestedWeaponCounts = new int[4];
        private static readonly string[] weaponKindLabels = MvpWeaponCatalog.Entries.Select(definition => definition.Name).ToArray();

        private int RequestedTurretTotal => requestedWeaponCounts.Sum();

        private void DrawWeaponConfiguration()
        {
            GUILayout.Space(8);
            GUILayout.Label("兵装の種類・個別数を指定", labelStyle);
            bool nextManual = GUILayout.Toggle(manualWeaponAllocation, "砲塔構成を手動指定（OFFは従来のランダム配置）");
            if (nextManual != manualWeaponAllocation)
            {
                manualWeaponAllocation = nextManual;
                if (HasFinished) DrawFrame();
            }
            if (!manualWeaponAllocation) return;
            for (int i = 0; i < requestedWeaponCounts.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(weaponKindLabels[i], GUILayout.Width(110));
                GUI.enabled = requestedWeaponCounts[i] > 0;
                bool minus = GUILayout.Button("−", GUILayout.Width(36));
                GUI.enabled = true;
                GUILayout.Label(requestedWeaponCounts[i].ToString(), GUILayout.Width(42));
                bool plus = GUILayout.Button("＋", GUILayout.Width(36));
                GUILayout.EndHorizontal();
                if (!minus && !plus) continue;
                requestedWeaponCounts[i] = Mathf.Clamp(requestedWeaponCounts[i] + (plus ? 1 : -1), 0, 128);
                if (HasFinished) DrawFrame();
            }
            int available = Mathf.Max(0, DisplayedTileCount - 1);
            GUILayout.Label($"指定砲塔数：{RequestedTurretTotal} ／ 現在の使用可能区画：{available}", labelStyle);
            if (RequestedTurretTotal > available)
                GUILayout.Label("注意：指定数が区画数を超過しています。現状は区画数を上限とし、表の上から優先して配置します。", labelStyle);
            GUILayout.Label("兵装・装甲の手動指定時はMVP固定性能を使用。データは7枠対応、現在の選択肢はMVP4種。", labelStyle);
        }

        // Construction armor quantities are independent from the protection test's global preset.
        private bool manualArmorAllocation = true;
        private readonly int[] requestedArmorCounts = new int[3]; // Basic, ERA, AntiEM
        private static readonly string[] armorBuildLabels = { "Basic", "ERA", "AEM（対電磁）" };
        private int RequestedArmorTotal => requestedArmorCounts.Sum();

        private void DrawArmorConstruction()
        {
            GUILayout.Space(8);
            GUILayout.Label("装甲区画の建造設定", labelStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(250));
            for (int i = 0; i < requestedArmorCounts.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(armorBuildLabels[i], GUILayout.Width(120));
                bool canMinus = requestedArmorCounts[i] > 0;
                bool oldGuiEnabled = GUI.enabled;
                GUI.enabled = oldGuiEnabled && canMinus;
                bool minus = GUILayout.Button("−", GUILayout.Width(36));
                GUI.enabled = oldGuiEnabled;
                GUILayout.Label(requestedArmorCounts[i].ToString(), GUILayout.Width(42));
                bool plus = GUILayout.Button("＋", GUILayout.Width(36));
                GUILayout.EndHorizontal();
                if (!minus && !plus) continue;
                requestedArmorCounts[i] = Mathf.Clamp(requestedArmorCounts[i] + (plus ? 1 : -1), 0, 60);
                if (HasFinished) DrawFrame();
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"指定装甲 {RequestedArmorTotal} ＋ 指定砲塔 {RequestedTurretTotal} ＝ {RequestedArmorTotal + RequestedTurretTotal}", labelStyle);
            if (!manualWeaponAllocation)
                GUILayout.Label("差分：砲塔数を手動指定してください", labelStyle);
            else if (int.TryParse(targetText, out int referenceCount) && referenceCount >= 1 && referenceCount <= 128)
            {
                int difference = RequestedArmorTotal + RequestedTurretTotal - referenceCount;
                GUILayout.Label($"目安区画数 {referenceCount} ／ 差分 {difference:+0;-0;0}", labelStyle);
                GUILayout.Label(difference == 0 ? "一致" : difference > 0 ? $"指定数が {difference} 区画多い" : $"指定数が {-difference} 区画少ない", labelStyle);
            }
            else GUILayout.Label("差分：目安区画数を1～128で入力してください", labelStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            for (int presetIndex = 0; presetIndex < 3; presetIndex++) { GUILayout.Label(armorBuildLabels[presetIndex]); DrawArmorPresets(presetIndex); }
            GUILayout.Label($"指定数：{RequestedArmorTotal} ／ 実際の装甲区画：{armorCount}", labelStyle);
            if (RequestedArmorTotal > armorCount)
                GUILayout.Label("注意：指定数が装甲区画数を超えています。Basic → ERA → AEMの順で上限まで割り当てます。", labelStyle);
            if (RequestedArmorTotal < armorCount)
                GUILayout.Label("未指定の装甲区画はBasicで補完します。", labelStyle);
            GUILayout.Label("装甲種の割当位置はシードで固定します。", labelStyle);
        }

        private enum ConstructionTab { Hull, Weapons, Armor, Facilities, Performance }
        private ConstructionTab constructionTab = ConstructionTab.Hull;
        private const string ShipBuildPrefix = "ShipBuild.";

        private void LoadShipBuildSettings()
        {
            seed = PlayerPrefs.GetInt(ShipBuildPrefix + "Seed", seed);
            targetSections = Mathf.Clamp(PlayerPrefs.GetInt(ShipBuildPrefix + "TargetSections", targetSections), 1, 128);
            maximumAttempts = Mathf.Clamp(PlayerPrefs.GetInt(ShipBuildPrefix + "MaxAttempts", maximumAttempts), 1, 2000);
            int selectedClass = PlayerPrefs.GetInt(ShipBuildPrefix + "ShipClass", (int)shipClass);
            if (System.Enum.IsDefined(typeof(ShipClass), selectedClass)) shipClass = (ShipClass)selectedClass;
            triangleStart = PlayerPrefs.GetInt(ShipBuildPrefix + "TriangleStart", triangleStart ? 1 : 0) != 0;
            int squareAngle = PlayerPrefs.GetInt(ShipBuildPrefix + "SquareAngle", (int)squareStartAngle);
            int triangleAngle = PlayerPrefs.GetInt(ShipBuildPrefix + "TriangleAngle", (int)triangleStartAngle);
            if (System.Enum.IsDefined(typeof(StartAngle), squareAngle)) squareStartAngle = (StartAngle)squareAngle;
            if (System.Enum.IsDefined(typeof(StartAngle), triangleAngle)) triangleStartAngle = (StartAngle)triangleAngle;
            formEnds = PlayerPrefs.GetInt(ShipBuildPrefix + "FormEnds", formEnds ? 1 : 0) != 0;
            endSearchDepth = Mathf.Clamp(PlayerPrefs.GetInt(ShipBuildPrefix + "EndSearchDepth", endSearchDepth), 1, 12);
            endSearchLimit = Mathf.Clamp(PlayerPrefs.GetInt(ShipBuildPrefix + "EndSearchLimit", endSearchLimit), 1, 10000);
            manualWeaponAllocation = PlayerPrefs.GetInt(ShipBuildPrefix + "ManualWeaponAllocation", 0) != 0;
            for (int i = 0; i < requestedWeaponCounts.Length; i++)
                requestedWeaponCounts[i] = Mathf.Clamp(PlayerPrefs.GetInt(ShipBuildPrefix + "WeaponCount" + i, 0), 0, 128);
            manualArmorAllocation = true;
            for (int i = 0; i < requestedArmorCounts.Length; i++)
                requestedArmorCounts[i] = Mathf.Clamp(PlayerPrefs.GetInt(ShipBuildPrefix + "ArmorCount" + i, 0), 0, 128);
            if (ShipBuildStore.TryLoad(out var saved, out savedBuildError)) ApplySavedBuild(saved);
            else if (savedBuildError == null && !PlayerPrefs.HasKey(ShipBuildPrefix + "Seed")) ApplySavedBuild(new ShipBuildData());
        }


        private void DrawHullTab()
        {
            GUILayout.Label("船体設計 / タイリング生成", labelStyle);
            if (!string.IsNullOrEmpty(generationInputError)) GUILayout.Label(generationInputError, labelStyle);
            DrawBuildPresets();
            GUILayout.BeginHorizontal();
            GUILayout.Label("シード", GUILayout.Width(52));
            seedText = GUILayout.TextField(seedText, GUILayout.Width(75));
            GUILayout.Label("目標区画数", GUILayout.Width(80));
            targetText = GUILayout.TextField(targetText, GUILayout.Width(52));
            GUILayout.Label("試行上限", GUILayout.Width(72));
            attemptsText = GUILayout.TextField(attemptsText, GUILayout.Width(60));
            GUILayout.EndHorizontal();
            triangleStart = GUILayout.Toggle(triangleStart, "初期形状：三角形（OFFは正方形）");
            int angleIndex = (int)(triangleStart ? triangleStartAngle : squareStartAngle);
            var angleOptions = System.Enum.GetValues(typeof(StartAngle)).Cast<StartAngle>().ToArray();
            bool anglesEnabled = GUI.enabled;
            Color angleColor = GUI.color;
            for (int row = 0; row < angleOptions.Length; row += 8)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < 8; column++)
                {
                    int optionIndex = row + column;
                    if (optionIndex >= angleOptions.Length) { GUILayout.Label("", GUILayout.MinWidth(0), GUILayout.ExpandWidth(true)); continue; }
                    var option = angleOptions[optionIndex];
                    bool valid = ShipBuildData.IsValidStartAngle(triangleStart, (int)option);
                    GUI.enabled = anglesEnabled && valid;
                    GUI.color = new Color(angleColor.r, angleColor.g, angleColor.b, angleColor.a * (valid ? 1f : .4f));
                    if (GUILayout.Toggle(angleIndex == (int)option, ((int)option) + "°", GUI.skin.button,
                        GUILayout.MinWidth(0), GUILayout.ExpandWidth(true)) && valid && angleIndex != (int)option)
                    {
                        if (triangleStart) triangleStartAngle = option;
                        else squareStartAngle = option;
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUI.enabled = anglesEnabled;
            GUI.color = angleColor;
            formEnds = GUILayout.Toggle(formEnds, "艦首・艦尾を形成");
            if (formEnds)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("探索深度 " + endSearchDepth, GUILayout.Width(100));
                endSearchDepth = Mathf.RoundToInt(GUILayout.HorizontalSlider(endSearchDepth, 1, 12));
                GUILayout.Label("候補数 " + endSearchLimit, GUILayout.Width(110));
                endSearchLimit = Mathf.RoundToInt(GUILayout.HorizontalSlider(endSearchLimit, 1, 10000));
                GUILayout.EndHorizontal();
            }
            GUI.enabled = !generating;
            if (GUILayout.Button("編集中の設定で再生成")) Regenerate();
            GUI.enabled = true;
            GUILayout.Label(formEnds
                ? $"端部：上 {generator.UpperEndStatus} / 下 {generator.LowerEndStatus} / 追加 {generator.EndTilesAdded} 区画"
                : "端部形成なし", labelStyle);
            string status = !generator.InitialSymmetric ? "初期区画がY軸非対称：角度を変更してください"
                : generating ? "生成中" : generator.Reached ? "目標到達" : "試行上限で停止";
            GUILayout.Label($"{status} / 確定 {generator.Count} 区画 / {generator.Attempts} 試行 / 増設 {generator.Accepted} 回", labelStyle);
            GUI.enabled = !generating;
            GUILayout.BeginHorizontal();
            int next = frameIndex;
            if (GUILayout.Button("前", GUILayout.Width(45))) next--;
            next = Mathf.RoundToInt(GUILayout.HorizontalSlider(next, 0, generator.Frames.Count - 1));
            if (GUILayout.Button("次", GUILayout.Width(45))) next++;
            if (GUILayout.Button("最終", GUILayout.Width(52))) next = generator.Frames.Count - 1;
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            next = Mathf.Clamp(next, 0, generator.Frames.Count - 1);
            if (next != frameIndex) { frameIndex = next; DrawFrame(); }
            var frame = generator.Frames[frameIndex];
            GUILayout.Label($"過程 {frameIndex + 1}/{generator.Frames.Count} : {frame.Label} / 表示 {frame.Tiles.Length} 区画", labelStyle);
        }

        private void DrawWeaponsTab()
        {
            GUILayout.Label("兵装 / 砲塔動作試験", labelStyle);
            bool nextEnabled = GUILayout.Toggle(enableTurretTest, "砲塔試験を有効にする");
            if (nextEnabled != enableTurretTest)
            {
                enableTurretTest = nextEnabled;
                if (enableTurretTest) protectionTestMode = false;
                if (testLauncher != null) testLauncher.Cancel();
                if (HasFinished) DrawFrame();
            }
            GUILayout.Label($"艦種 {shipClass} / 砲塔 {turrets.Count} / 装甲区画 {armorCount}", labelStyle);
            GUILayout.Label(protectionTestMode ? "防護分析が有効な間は砲塔の自動射撃を停止" : "砲塔はマウス位置へ自動射撃", labelStyle);
            GUILayout.Label(
    enableTurretTest ? "砲塔試験：ON（マウス位置へ自動射撃）"
    : protectionTestMode ? "砲塔試験：OFF（防護分析中）"
    : "砲塔試験：OFF",
    labelStyle);
            DrawWeaponConfiguration();
        }

        private void DrawArmorTab()
        {
            GUILayout.Label("装甲 / 防護分析", labelStyle);
            DrawArmorConstruction();
            bool nextMode = GUILayout.Toggle(protectionTestMode, "防護分析：試験弾を左ドラッグで発射");
            if (nextMode != protectionTestMode)
            {
                protectionTestMode = nextMode;
                if (protectionTestMode) enableTurretTest = false;
                if (testLauncher != null) testLauncher.Cancel();
                if (HasFinished) DrawFrame();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("試験弾を消去")) ClearTestProjectiles();
            if (GUILayout.Button("標的を復元")) DrawFrame();
            GUILayout.EndHorizontal();
            GUILayout.Label($"装甲厚 {armorThickness:0.#} / ERAベクトル {eraVectorMagnitude:0.#} / 迎撃力 {armorInterception:0.###}", labelStyle);
            testBarrierEnabled = GUILayout.Toggle(testBarrierEnabled, "電磁バリア有効");
            GUILayout.Label($"試験弾：威力 {testProjectilePower:0.##} / 貫通力 {testProjectilePenetration:0.##}（詳細値はInspectorで変更）", labelStyle);
            if (protectionTestMode && testLauncher != null) GUILayout.Label(testLauncher.Readout, labelStyle);
            if (testTarget != null)
            {
                GUILayout.Label($"標的HP {testTarget.CoreHealth:0.##}/{testTarget.CoreHealthLimit:0.##} / 残存区画 {testTarget.LivingSectionCount}/{DisplayedTileCount}", labelStyle);
                GUILayout.Label($"熱量 {testTarget.HeatAccumulation:0.##}% / EM {testTarget.EmAccumulation:0.##}% / バリア半径 {testTarget.BarrierRadius:0.##}", labelStyle);
            }
            foreach (string entry in hitLog) GUILayout.Label(entry, labelStyle);
        }

        private void DrawFacilitiesTab()
        {
            GUILayout.Label("施設設定", labelStyle);
            DrawEngineSettings();

        }

        private void DrawPerformanceTab()
        {
            GUILayout.Label("艦船性能確認", labelStyle);
            GUILayout.Label($"艦種：{shipClass} / シード：{seed} / 目標区画数：{targetSections}", labelStyle);
            GUILayout.Label($"実際の区画数：{DisplayedTileCount} / 砲塔：{TurretCount} / 装甲区画：{armorCount}", labelStyle);
            GUILayout.Label(manualWeaponAllocation ? $"指定兵装：{RequestedTurretTotal} 基（区画上限適用後 {TurretCount} 基）" : "兵装：従来のランダム構成", labelStyle);
            GUILayout.Label($"装甲：指定数 Basic {requestedArmorCounts[0]}, ERA {requestedArmorCounts[1]}, AEM {requestedArmorCounts[2]}（不足分Basic）", labelStyle);
            if (testTarget != null)
                GUILayout.Label($"コアHP：{testTarget.CoreHealth:0.##}/{testTarget.CoreHealthLimit:0.##}", labelStyle);
            DrawBuildPerformance();
            GUILayout.Label("※戦闘中の損傷値は建造設定として保存しません。", labelStyle);
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
            GUILayout.Label("艦船建造 / Ship Construction", labelStyle);
            int selectedTab = GUILayout.Toolbar((int)constructionTab,
                new[] { "船体", "兵装", "装甲", "施設", "性能確認" });
            if (selectedTab != (int)constructionTab) { StopMovementTrial(); panelScroll = Vector2.zero; }
            constructionTab = (ConstructionTab)Mathf.Clamp(selectedTab, 0, 4);
            GUILayout.Space(7);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            switch (constructionTab)
            {
                case ConstructionTab.Hull: DrawHullTab(); break;
                case ConstructionTab.Weapons: DrawWeaponsTab(); break;
                case ConstructionTab.Armor: DrawArmorTab(); break;
                case ConstructionTab.Facilities: DrawFacilitiesTab(); break;
                case ConstructionTab.Performance: DrawPerformanceTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea(); GUI.matrix = oldMatrix;
            DrawFloatingHits();
        }
        private void OnDestroy() { ClearDrawing(); if (material != null) Release(material); }
        private void Update()
        {
            UpdateBarrier();
            if (testTarget != null && !testTarget.HasConstructionData && (appliedArmorThickness != armorThickness || appliedEraVectorMagnitude != eraVectorMagnitude ||
                appliedArmorInterception != armorInterception))
            {
                foreach (var section in testTarget.GetComponentsInChildren<BattleshipSection>())
                {
                    section.ArmorThickness = armorThickness;
                    section.ArmorInterception = armorInterception;
                    section.EraVectorMagnitude = eraVectorMagnitude;
                }
                appliedArmorThickness = armorThickness;
                appliedArmorInterception = armorInterception;
                appliedEraVectorMagnitude = eraVectorMagnitude;
            }
            floatingHits.RemoveAll(hit => Time.time - hit.StartedAt >= FloatingHitSeconds);
            if (generator != null && (lastWidth != Screen.width || lastHeight != Screen.height))
            { if (testLauncher != null) testLauncher.Cancel(); UpdateView(); }
            if (testLauncher != null)
                testLauncher.Tick(protectionTestMode && !generating && testTarget != null && testTarget.IsAlive &&
                    ProjectilePenetration.IsValid(testProjectilePenetration) &&
                    testProjectilePower > 0 && !float.IsInfinity(testProjectilePower),
                    Mathf.Max(.01f, launchVelocityScale), Mathf.Max(.1f, testProjectileRadius),
                    Mathf.Max(.1f, testProjectileLifetime), testTarget, testProjectilePower,
                    testProjectilePenetration, RecordTestHit, testProjectileAffect, testExplosionRadius, projectileEffects);
            TickTurrets();
        }

        private void UpdateBarrier()
        {
            if (testTarget == null) return;
            testTarget.barrierEnabled = testBarrierEnabled;
            testTarget.barrierReferenceSections = Mathf.Max(1, barrierReferenceSections);
            testTarget.barrierReduceOnExit = barrierReduceOnExit;
            if (barrierOutline == null) return;
            barrierOutline.enabled = testBarrierEnabled && testTarget.IsAlive;
            Vector2 center = testTarget.CorePosition;
            for (int i = 0; i < barrierOutline.positionCount; i++)
            {
                float angle = 2f * Mathf.PI * i / barrierOutline.positionCount;
                Vector2 p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * testTarget.BarrierRadius;
                barrierOutline.SetPosition(i, new Vector3(p.x, p.y, -.2f));
            }
        }
    }
}
