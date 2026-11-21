using UnityEngine;

namespace Script
{
    // Isolated manual test scene; no campaign saves or shooting logic.
    public sealed class ShipSteeringSandbox : MonoBehaviour
    {
        private GameObject hull, target, replacement;
        private ShipSteeringAI ai;
        private ShipMovement motor;
        private Material material;
        private AIType selected = AIType.TOr;
        private bool movingTarget;
        private float elapsed;
        private TrailRenderer trail;

        private void Start()
        {
            material = new Material(Shader.Find("Sprites/Default"));
            hull = Marker("Steering hull", Color.cyan, 150);
            target = Marker("Target / escort", Color.red, 180);
            replacement = Marker("Fallback target", Color.yellow, 140);
            replacement.transform.position = new Vector3(-4000, 3000);
            motor = hull.AddComponent<ShipMovement>();
            ai = hull.AddComponent<ShipSteeringAI>();
            trail = hull.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.startWidth = 20; trail.endWidth = 8; trail.time = 180;
            trail.minVertexDistance = 20;
            trail.startColor = Color.cyan; trail.endColor = new Color(0, 1, 1, .1f);
            var ring = new GameObject("Reference orbit radius 3000").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sharedMaterial = material; ring.widthMultiplier = 12;
            ring.startColor = ring.endColor = new Color(.3f, .3f, .3f);
            ring.loop = true; ring.positionCount = 128;
            for (int i = 0; i < 128; i++)
            {
                float angle = i * Mathf.PI * 2 / 128;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 3000);
            }
            Restart();
        }

        private GameObject Marker(string label, Color color, float size)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = false;
            line.startColor = line.endColor = color; line.widthMultiplier = 25;
            line.loop = true; line.positionCount = 3;
            line.SetPositions(new[] { Vector3.up * size, new Vector3(-.6f, -1) * size, new Vector3(.6f, -1) * size });
            return go;
        }

        private void Restart()
        {
            elapsed = 0; target.SetActive(true); target.transform.position = Vector3.zero;
            hull.transform.position = new Vector3(3000, 0);
            hull.transform.rotation = Quaternion.identity;
            motor.Initialize(new ShipPerformance(250, 25), false);
            ai.Configure(new ShipSteeringSettings { major = selected, attackTarget = target.transform,
                escortTarget = target.transform, orbitRadius = 3000, orbitCenterPosition = Vector2.zero,
                fallback = fallbackPursuit ? AIType.C1 : AIType.AC, fallbackTarget = replacement.transform });
            trail.Clear();
        }
        private bool fallbackPursuit;
        private void Update()
        {
            elapsed += Time.deltaTime;
            if (target != null && movingTarget && target.activeSelf)
                target.transform.position = new Vector3(Mathf.Sin(elapsed * .05f) * 1500, Mathf.Sin(elapsed * .025f) * 1000);
        }
        private void OnGUI()
        {
            if (ai == null) return;
            GUILayout.BeginArea(new Rect(10, 10, 460, 300), GUI.skin.box);
            GUILayout.Label("工程04 操艦AI試験（速力250 / 旋回25 / 半径3000）");
            GUILayout.BeginHorizontal();
            foreach (AIType type in System.Enum.GetValues(typeof(AIType)))
                if (GUILayout.Button(type.ToString())) { selected = type; Restart(); }
            GUILayout.EndHorizontal();
            movingTarget = GUILayout.Toggle(movingTarget, "赤の目標を移動（Obsの中心は原点固定）");
            bool next = GUILayout.Toggle(fallbackPursuit, "目標喪失後：黄色の目標へC1（OFFはAC）");
            if (next != fallbackPursuit) { fallbackPursuit = next; Restart(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("再配置")) Restart();
            if (GUILayout.Button("目標喪失")) target.SetActive(false);
            GUILayout.EndHorizontal();
            GUILayout.Label($"主AI {selected} → 現在 {ai.ActiveType}  経過 {elapsed:F1}s\n" +
                $"速力 {motor.Velocity.magnitude:F1}  旋回指示 {motor.TurnInput:F2}\n" +
                $"周回角 {ai.HeldOrbitAngle:F0}°  方向 {ai.OrbitDirection}  LOS {ai.LosRate:F2}°/s\n" +
                "水色：自艦と軌跡／灰円：原点基準の半径3000\n終了：UnityのPlayを停止");
            GUILayout.EndArea();
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
