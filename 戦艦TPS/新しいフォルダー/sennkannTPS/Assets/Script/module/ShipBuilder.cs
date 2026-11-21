using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Script
{
    public static class ShipBuilder
    {
        public static BattleshipInstance BuildWithAI(ShipBuildData source, Material material, Vector2 position,
            float headingDegrees, bool friend, ShipSteeringSettings steering)
        {
            if (steering == null) throw new ArgumentNullException(nameof(steering));
            steering.Validate();
            var ship = Build(source, material, position, headingDegrees, friend, false);
            ship.gameObject.AddComponent<ShipSteeringAI>().Configure(steering);
            return ship;
        }

        public static TilingGenerator GenerateHull(ShipBuildData data)
        {
            if (!data.Validate(out string error)) throw new ArgumentException(error);
            var generator = new TilingGenerator(data.seed, data.referenceSections, data.maximumAttempts, data.triangleStart, data.startAngle);
            while (!generator.Done) generator.Step();
            if (data.formEnds) foreach (var step in generator.FormEnds(data.endSearchDepth, data.endSearchLimit)) { }
            return generator;
        }

        public static BattleshipInstance Build(ShipBuildData source, Material material, Vector2 position, float headingDegrees,
            bool friend, bool playerControlled, bool stationary = false)
        {
            if (source == null || !source.TryCreateConstructionSnapshot(out var data, out _))
                throw new ArgumentException("建造入力または設備性能が不正です。", nameof(source));
            var generator = GenerateHull(data);
            var tiles = generator.Frames[generator.Frames.Count - 1].Tiles;
            var root = new GameObject("Constructed ship");
            var meshOwner = root.AddComponent<ShipGeneratedMeshes>();
            var objects = new List<GameObject>();
            foreach (var tile in tiles)
            {
                var part = new GameObject("Section");
                part.transform.SetParent(root.transform, false);
                var mesh = new Mesh { name = "Ship section" };
                mesh.vertices = tile.Vertices.Select(p => new Vector3((float)p.X, (float)p.Y) * data.sectionSize).ToArray();
                mesh.triangles = tile.Triangle ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
                mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
                mesh.RecalculateBounds();
                meshOwner.Meshes.Add(mesh);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
                objects.Add(part);
            }
            var ship = Attach(data, root, tiles, objects, stationary, playerControlled);
            var iff = root.AddComponent<IFF>();
            iff.IsFriend = friend;
            iff.RegistryToManager();
            root.transform.position = position;
            // Data headings: 0 = +X, 90 = +Y. Geometry's local bow is +Y.
            root.transform.rotation = Quaternion.Euler(0, 0, headingDegrees - 90);
            return ship;
        }

        // Reuses the existing preview meshes. Preserve the prior truncation/fill policy
        // for generated over/undershoot; never reject a valid design for its tile count.
        internal static BattleshipInstance Attach(ShipBuildData source, GameObject root, TilingGenerator.Tile[] tiles,
            List<GameObject> objects, bool stationary, bool playerControlled)
        {
            if (!source.TryCreateConstructionSnapshot(out var data, out string error, false)) throw new ArgumentException(error);
            var ship = root.AddComponent<BattleshipInstance>();
            var random = new System.Random(unchecked(data.seed * 397) ^ data.shipClass * 7919 ^ tiles.Length);
            int turretCount = Math.Min(tiles.Length - 1, data.weaponCounts.Sum());
            int armorCount = tiles.Length - 1 - turretCount;
            var roles = Enumerable.Repeat(BattleshipSectionRole.Armor, armorCount)
                .Concat(Enumerable.Repeat(BattleshipSectionRole.Turret, turretCount)).ToList();
            Shuffle(roles, random);
            var weapons = new List<int>();
            for (int i = 0; i < 7; i++)
                for (int j = 0; j < data.weaponCounts[i] && weapons.Count < turretCount; j++) weapons.Add(data.weaponIDs[i]);
            Shuffle(weapons, random);
            // Count mounted turret sections, merging duplicate build slots of the same kind.
            var mountedCounts = weapons.GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
            var armorKinds = new List<int>();
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < data.armor[i].count && armorKinds.Count < armorCount; j++) armorKinds.Add(i);
            while (armorKinds.Count < armorCount) armorKinds.Add(0);
            Shuffle(armorKinds, new System.Random(unchecked(data.seed * 733 + tiles.Length * 37 + 1151)));
            int weaponIndex = 0, armorIndex = 0;
            var sections = new List<BattleshipSection>();
            for (int i = 0; i < tiles.Length; i++)
            {
                var role = i == 0 ? BattleshipSectionRole.Core : roles[i - 1];
                var polygon = objects[i].AddComponent<PolygonCollider2D>();
                polygon.SetPath(0, tiles[i].Vertices.Select(p => new Vector2((float)p.X, (float)p.Y) * data.sectionSize).ToArray());
                var section = objects[i].AddComponent<BattleshipSection>();
                BattleshipWeaponRuntime weapon = null;
                if (role == BattleshipSectionRole.Turret)
                {
                    int weaponId = weapons[weaponIndex++];
                    weapon = MvpWeaponCatalog.CreateRuntime(weaponId, data.seed + i, mountedCounts[weaponId]);
                }
                section.Initialize(ship, i, role, weapon);
                if (role == BattleshipSectionRole.Armor)
                {
                    int kind = armorKinds[armorIndex++];
                    var preset = data.armor[kind];
                    section.Mode = (ArmorMode)kind;
                    section.SetConstructionHealth(ShipBuildData.ArmorHP[preset.hpSettingID]);
                    section.ArmorThickness = ShipBuildData.Thickness[preset.defenseThicknessSettingID];
                    section.ArmorInterception = ShipBuildData.Interception[preset.defenseThicknessSettingID];
                }
                else section.SetConstructionHealth(role == BattleshipSectionRole.Core ? ShipBuildData.CoreHP[data.coreHPSettingID] : data.TurretHP);
                var mesh = objects[i].GetComponent<MeshFilter>().sharedMesh;
                var color = role == BattleshipSectionRole.Core ? Color.green : role == BattleshipSectionRole.Armor ? new Color(.85f, .3f, .3f) : new Color(.3f, .6f, 1);
                mesh.colors = Enumerable.Repeat(color, mesh.vertexCount).ToArray();
                objects[i].name = $"Section {i} {role}";
                sections.Add(section);
            }
            for (int i = 0; i < tiles.Length; i++)
            for (int j = i + 1; j < tiles.Length; j++)
            {
                int shared = tiles[i].Vertices.Count(a => tiles[j].Vertices.Any(b =>
                    Math.Abs(a.X - b.X) < TilingGenerator.Epsilon && Math.Abs(a.Y - b.Y) < TilingGenerator.Epsilon));
                if (shared >= 2) { sections[i].AddNeighbor(sections[j]); sections[j].AddNeighbor(sections[i]); }
            }
            ship.Initialize((BattleshipClass)data.shipClass, data.referenceSections, tiles.Length, data.seed, sections, stationary);
            ship.ApplyConstruction(data, playerControlled);
            ship.GetComponent<ShipMovement>().enabled = !stationary;
            root.AddComponent<WeaponTarget>();
            root.AddComponent<ShipFiringAI>().enabled = !stationary && !playerControlled;
            return ship;
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
        }

    }
}
