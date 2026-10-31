using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aquarium.Tiling
{
    /// <summary>battle0上で、指定された艦種と区画数から戦艦型動物を生成する。</summary>
    public sealed class BattleshipAnimalManager : MonoBehaviour
    {
        private sealed class WeaponChoice
        {
            public BattleshipWeaponKind Kind;
            public int Grade;
            public int Weight;
        }

        [Header("スポーン設定")]
        [SerializeField, InspectorName("艦種")] private BattleshipAnimalClass shipClass = BattleshipAnimalClass.Cruiser;
        [SerializeField, InspectorName("目安区画数"), Range(1, 128)] private int targetSections = 25;
        [SerializeField, InspectorName("スポーン数"), Min(1)] private int spawnCount = 1;
        [SerializeField, InspectorName("シード")] private int seed = 1;
        [SerializeField, InspectorName("再生開始時にスポーン")] private bool spawnOnStart = true;

        [Header("区画生成設定")]
        [SerializeField, Min(1)] private int maximumAttempts = 2000;
        [SerializeField] private bool triangleStart;
        [SerializeField] private float initialAngle;
        [SerializeField] private bool formEnds = true;
        [SerializeField, Range(0, 12)] private int endSearchDepth = 6;
        [SerializeField, Range(0, 10000)] private int endSearchLimit = 1200;
        [SerializeField, Min(1f)] private float sectionWorldSize = 100f;

        [Header("表示")]
        [SerializeField] private Color squareSectionColor = new Color(0.12f, 0.55f, 0.85f, 1f);
        [SerializeField] private Color triangleSectionColor = new Color(0.95f, 0.55f, 0.12f, 1f);

        private readonly List<GameObject> spawnedAnimals = new List<GameObject>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private Material sectionMaterial;
        private System.Random reproductionRandom;
        private int birthSerial;

        public BattleshipAnimalClass ShipClass => shipClass;
        public int TargetSections => targetSections;
        public int SpawnedCount => spawnedAnimals.Count(x => x != null);
        public int TotalActualSections => spawnedAnimals
            .Where(x => x != null)
            .Select(x => x.GetComponent<BattleshipAnimalInstance>())
            .Where(x => x != null)
            .Sum(x => x.ActualSections);
        internal Material SectionMaterial => sectionMaterial;

        private void Start()
        {
            if (spawnOnStart) Respawn();
        }

        [ContextMenu("戦艦型動物を再スポーン")]
        public void Respawn()
        {
            ClearSpawnedAnimals();
            EnsureMaterial();
            WarnIfSectionCountIsUnusual();

            var positionRandom = new System.Random(seed ^ unchecked((int)0x5f3759df));
            reproductionRandom = new System.Random(seed ^ unchecked((int)0x2c1b3c6d));
            birthSerial = 0;
            for (int i = 0; i < spawnCount; i++)
            {
                int animalSeed = unchecked(seed + i * 7919);
                Vector2 position = ChooseSpawnPosition(positionRandom);
                float heading = (float)(positionRandom.NextDouble() * 360d);
                SpawnOne(i, shipClass, targetSections, animalSeed, position, heading,
                    unchecked(animalSeed ^ (i + 1) * 104729), null);
            }
        }

        private BattleshipAnimalInstance SpawnOne(int index, BattleshipAnimalClass animalClass,
            int requestedSections, int animalSeed, Vector2 position, float heading, int loadoutSeed,
            BattleshipAnimalInstance birthPayer)
        {
            requestedSections = Mathf.Clamp(requestedSections, 1, 128);
            var generator = new TilingGenerator(animalSeed, requestedSections, maximumAttempts, triangleStart, initialAngle);
            while (!generator.Done) generator.Step();
            if (formEnds)
                foreach (int ignored in generator.FormEnds(endSearchDepth, endSearchLimit)) { }

            TilingGenerator.Tile[] tiles = generator.Frames[generator.Frames.Count - 1].Tiles;
            if (birthPayer != null && !birthPayer.TryPayBirth(tiles.Length * 30d, tiles.Length * 50d))
                return null;
            var root = new GameObject($"Battleship Animal {index + 1} [{animalClass}] {tiles.Length} sections");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(position.x, position.y, 0f);
            root.transform.rotation = Quaternion.Euler(0, 0, heading);
            var instance = root.AddComponent<BattleshipAnimalInstance>();

            Vector2 center = CalculateCenter(tiles);
            var roles = BuildRoles(tiles.Length, new System.Random(loadoutSeed));
            var sections = new List<BattleshipSection>(tiles.Length);
            var loadoutRandom = new System.Random(unchecked(loadoutSeed ^ (int)0x6a09e667));
            for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
            {
                BattleshipSectionRole role = roles[tileIndex];
                BattleshipWeaponRuntime weapon = role == BattleshipSectionRole.Turret
                    ? CreateWeapon(PickWeighted(BuildWeaponChoices(animalClass), loadoutRandom), loadoutRandom)
                    : null;
                sections.Add(CreateSection(root.transform, instance, tiles[tileIndex], tileIndex, center, role, weapon));
            }
            ConnectNeighbors(tiles, sections);
            instance.Initialize(this, animalClass, requestedSections, tiles.Length, animalSeed, sections);

            spawnedAnimals.Add(root);
            if (!generator.Reached)
                Debug.LogWarning($"{root.name}: 生成試行上限に達し、指定した{requestedSections}区画へ到達しませんでした。", this);
            return instance;
        }

        private BattleshipSection CreateSection(Transform parent, BattleshipAnimalInstance owner,
            TilingGenerator.Tile tile, int index, Vector2 center, BattleshipSectionRole role,
            BattleshipWeaponRuntime weapon)
        {
            var section = new GameObject($"Section {index + 1:000}");
            section.transform.SetParent(parent, false);

            var mesh = new Mesh { name = $"Battleship section {index + 1}" };
            var vertices = new Vector3[tile.Vertices.Length];
            var colors = new Color[tile.Vertices.Length];
            Color color = RoleColor(role, weapon, tile.Triangle ? triangleSectionColor : squareSectionColor);
            for (int i = 0; i < tile.Vertices.Length; i++)
            {
                vertices[i] = new Vector3(
                    ((float)tile.Vertices[i].X - center.x) * sectionWorldSize,
                    ((float)tile.Vertices[i].Y - center.y) * sectionWorldSize,
                    0f);
                colors[i] = color;
            }

            var triangles = new int[(tile.Vertices.Length - 2) * 3];
            for (int i = 0; i < tile.Vertices.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.RecalculateBounds();

            section.AddComponent<MeshFilter>().sharedMesh = mesh;
            section.AddComponent<MeshRenderer>().sharedMaterial = sectionMaterial;
            var collider = section.AddComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            collider.SetPath(0, vertices.Select(x => new Vector2(x.x, x.y)).ToArray());
            var runtime = section.AddComponent<BattleshipSection>();
            runtime.Initialize(owner, index, role, weapon);
            if (role == BattleshipSectionRole.Armor) AddArmorMarker(section.transform, vertices);
            generatedMeshes.Add(mesh);
            return runtime;
        }

        private static Vector2 ChooseSpawnPosition(System.Random random)
        {
            float half = GrassField.FieldSize * 0.5f;
            float x = (float)((random.NextDouble() * 2d - 1d) * half);
            float y = (float)((random.NextDouble() * 2d - 1d) * half);
            return new Vector2(Mathf.Min(x, half - 0.001f), Mathf.Min(y, half - 0.001f));
        }

        private static Vector2 CalculateCenter(IReadOnlyList<TilingGenerator.Tile> tiles)
        {
            double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            foreach (var point in tiles.SelectMany(x => x.Vertices))
            {
                minX = System.Math.Min(minX, point.X);
                maxX = System.Math.Max(maxX, point.X);
                minY = System.Math.Min(minY, point.Y);
                maxY = System.Math.Max(maxY, point.Y);
            }
            return new Vector2((float)((minX + maxX) * 0.5), (float)((minY + maxY) * 0.5));
        }

        private static List<BattleshipSectionRole> BuildRoles(int count, System.Random random)
        {
            var result = new List<BattleshipSectionRole>(count) { BattleshipSectionRole.Core };
            int remaining = Mathf.Max(0, count - 1);
            int turretCount = remaining / 2;
            int armorCount = remaining - turretCount;
            for (int i = 0; i < armorCount; i++) result.Add(BattleshipSectionRole.Armor);
            for (int i = 0; i < turretCount; i++) result.Add(BattleshipSectionRole.Turret);
            for (int i = result.Count - 1; i > 1; i--)
            {
                int j = random.Next(1, i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        private static List<WeaponChoice> BuildWeaponChoices(BattleshipAnimalClass rank)
        {
            var result = new List<WeaponChoice>
            {
                new WeaponChoice { Kind = BattleshipWeaponKind.SmallGun, Grade = 1, Weight = 2 },
                new WeaponChoice { Kind = BattleshipWeaponKind.SmallGun, Grade = 2, Weight = 3 },
                new WeaponChoice { Kind = BattleshipWeaponKind.MachineGun, Grade = 1, Weight = 4 }
            };
            if (rank >= BattleshipAnimalClass.Destroyer)
            {
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.SmallGun, Grade = 3, Weight = 5 });
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.MachineGun, Grade = 2, Weight = 6 });
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.HeavyGun, Grade = 1, Weight = 15 });
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.SmallGun, Grade = 1, Weight = 8 });
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.MachineGun, Grade = 2, Weight = 10 });
            }
            if (rank >= BattleshipAnimalClass.Cruiser)
            {
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.AntiShipMissile, Grade = 1, Weight = 35 });
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.SmallGun, Grade = 1, Weight = 20 });
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.MachineGun, Grade = 2, Weight = 25 });
            }
            if (rank >= BattleshipAnimalClass.Battleship)
                result.Add(new WeaponChoice { Kind = BattleshipWeaponKind.HeavyGun, Grade = 2, Weight = 60 });
            return result;
        }

        private static WeaponChoice PickWeighted(IReadOnlyList<WeaponChoice> choices, System.Random random)
        {
            int roll = random.Next(choices.Sum(x => x.Weight));
            foreach (WeaponChoice choice in choices)
            {
                if (roll < choice.Weight) return choice;
                roll -= choice.Weight;
            }
            return choices[choices.Count - 1];
        }

        private static BattleshipWeaponRuntime CreateWeapon(WeaponChoice choice, System.Random random)
        {
            float sample = (float)random.NextDouble();
            var result = new BattleshipWeaponRuntime
            {
                Kind = choice.Kind,
                Grade = choice.Grade,
                Random = new System.Random(random.Next()),
                Timer = 0
            };
            switch (choice.Kind)
            {
                case BattleshipWeaponKind.SmallGun:
                    result.Power = Mathf.Lerp(50f, 100f, sample) + choice.Grade * 50f;
                    result.Interval = 2f; result.Speed = 450f; result.Decay = 10f; result.Spread = 5f; result.SpCost = 10f;
                    break;
                case BattleshipWeaponKind.HeavyGun:
                    result.Power = Mathf.Lerp(300f, 500f, sample) + choice.Grade * 100f;
                    result.Interval = 5f; result.Speed = 600f; result.Decay = 30f; result.Spread = 3f; result.SpCost = 10f;
                    break;
                case BattleshipWeaponKind.AntiShipMissile:
                    result.Power = Mathf.Lerp(100f, 200f, sample) + choice.Grade * 60f;
                    result.Interval = 5f; result.Speed = 600f; result.Decay = 50f; result.SpCost = 10f;
                    result.GuidanceTurnSpeed = 50f + choice.Grade * 20f;
                    result.NavigationConstant = Mathf.Lerp(1f, 3f, (float)random.NextDouble());
                    break;
                default:
                    result.Power = 10f + choice.Grade * 30f;
                    result.Interval = 0.1f; result.Speed = 400f; result.Decay = 10f; result.Spread = 12f; result.SpCost = 0.3f;
                    break;
            }
            return result;
        }

        private static void ConnectNeighbors(IReadOnlyList<TilingGenerator.Tile> tiles,
            IReadOnlyList<BattleshipSection> sections)
        {
            for (int i = 0; i < tiles.Count; i++)
            for (int j = i + 1; j < tiles.Count; j++)
            {
                int shared = tiles[i].Vertices.Count(a => tiles[j].Vertices.Any(b =>
                    System.Math.Abs(a.X - b.X) < TilingGenerator.Epsilon &&
                    System.Math.Abs(a.Y - b.Y) < TilingGenerator.Epsilon));
                if (shared < 2) continue;
                sections[i].AddNeighbor(sections[j]);
                sections[j].AddNeighbor(sections[i]);
            }
        }

        private static Color RoleColor(BattleshipSectionRole role, BattleshipWeaponRuntime weapon, Color fallback)
        {
            if (role == BattleshipSectionRole.Core) return new Color(0.32f, 0.95f, 0.48f);
            if (role == BattleshipSectionRole.Armor) return fallback;
            return weapon == null ? Color.white : weapon.Kind switch
            {
                BattleshipWeaponKind.SmallGun => new Color(0.95f, 0.82f, 0.25f),
                BattleshipWeaponKind.HeavyGun => new Color(0.95f, 0.45f, 0.15f),
                BattleshipWeaponKind.AntiShipMissile => new Color(0.75f, 0.35f, 0.95f),
                _ => new Color(0.25f, 0.9f, 0.95f)
            };
        }

        private void AddArmorMarker(Transform parent, IReadOnlyList<Vector3> vertices)
        {
            Vector3 center = Vector3.zero;
            foreach (Vector3 vertex in vertices) center += vertex;
            center /= vertices.Count;
            var marker = new GameObject("Armor marker");
            marker.transform.SetParent(parent, false);
            var line = marker.AddComponent<LineRenderer>();
            line.sharedMaterial = sectionMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 20;
            line.widthMultiplier = Mathf.Max(2f, sectionWorldSize * 0.03f);
            line.startColor = line.endColor = Color.red;
            float radius = sectionWorldSize * 0.18f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, -0.02f));
            }
        }

        internal void TryReproduce(BattleshipAnimalInstance parent)
        {
            if (parent == null || !parent.CanReproduce) return;
            if (reproductionRandom == null) reproductionRandom = new System.Random(seed ^ unchecked((int)0x2c1b3c6d));
            int living = BattleshipAnimalInstance.Active.Count(x => x != null && x.IsAlive && x.ShipClass == parent.ShipClass);
            int baseline = parent.ShipClass switch
            {
                BattleshipAnimalClass.Frigate => 20,
                BattleshipAnimalClass.Destroyer => 10,
                BattleshipAnimalClass.Cruiser => 5,
                _ => 3
            };
            double probability = Mathf.Clamp01(1f - (float)living / baseline);
            parent.ResetReproductionAge();
            if (reproductionRandom.NextDouble() >= probability) return;

            int requested = Mathf.Clamp(parent.RequestedSections + reproductionRandom.Next(-1, 2), 1, 128);
            float heading = (float)(reproductionRandom.NextDouble() * 360d);
            int loadoutSeed = unchecked(reproductionRandom.Next() ^ (++birthSerial * 486187739));
            SpawnOne(spawnedAnimals.Count + birthSerial, parent.ShipClass, requested, parent.GenerationSeed,
                parent.transform.position, heading, loadoutSeed, parent);
        }

        internal void NotifyDeath(BattleshipAnimalInstance instance)
        {
            if (instance != null) spawnedAnimals.Remove(instance.gameObject);
        }

        private void EnsureMaterial()
        {
            if (sectionMaterial != null) return;
            Shader shader = Shader.Find("Aquarium/TilingTest");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            sectionMaterial = new Material(shader) { name = "Battleship Animal Sections" };
        }

        private void WarnIfSectionCountIsUnusual()
        {
            bool usual = shipClass switch
            {
                BattleshipAnimalClass.Frigate => targetSections >= 5 && targetSections <= 10,
                BattleshipAnimalClass.Destroyer => targetSections >= 15 && targetSections <= 20,
                BattleshipAnimalClass.Cruiser => targetSections >= 25 && targetSections <= 30,
                BattleshipAnimalClass.Battleship => targetSections >= 40,
                _ => true
            };
            if (!usual)
                Debug.LogWarning($"{shipClass}の指定区画数{targetSections}は設計上の目安範囲外です。指定値のまま生成します。", this);
        }

        private void ClearSpawnedAnimals()
        {
            foreach (GameObject animal in spawnedAnimals)
                if (animal != null) DestroyGeneratedObject(animal);
            spawnedAnimals.Clear();
            foreach (Mesh mesh in generatedMeshes)
                if (mesh != null) DestroyGeneratedObject(mesh);
            generatedMeshes.Clear();
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private void OnDestroy()
        {
            ClearSpawnedAnimals();
            if (sectionMaterial != null) DestroyGeneratedObject(sectionMaterial);
        }

        private void OnValidate()
        {
            targetSections = Mathf.Clamp(targetSections, 1, 128);
            spawnCount = Mathf.Max(1, spawnCount);
            maximumAttempts = Mathf.Clamp(maximumAttempts, 1, 2000);
            sectionWorldSize = Mathf.Max(1f, sectionWorldSize);
        }
    }
}
