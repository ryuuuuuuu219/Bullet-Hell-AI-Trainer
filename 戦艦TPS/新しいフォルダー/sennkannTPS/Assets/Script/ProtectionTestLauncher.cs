using UnityEngine;
using UnityEngine.InputSystem;

namespace Script
{
    public sealed class ProtectionTestLauncher : MonoBehaviour
    {
        private Camera view;
        private Material material;
        private Transform projectileRoot;
        private LineRenderer preview;
        private bool dragging;
        private Vector2 pressScreen, origin, velocity;
        public string Readout { get; private set; } = "左ドラッグ：始点から終点へ発射 / 右クリック・Esc：取消";

        public void Initialize(Camera camera, Material sharedMaterial, Transform root)
        {
            view = camera;
            material = sharedMaterial;
            projectileRoot = root;
            var go = new GameObject("Launch velocity preview");
            go.transform.SetParent(transform, false);
            preview = go.AddComponent<LineRenderer>();
            preview.sharedMaterial = material;
            preview.useWorldSpace = true;
            preview.positionCount = 5;
            preview.startColor = preview.endColor = Color.cyan;
            preview.enabled = false;
        }

        public void Cancel()
        {
            dragging = false;
            if (preview != null) preview.enabled = false;
        }

        public void Tick(bool canLaunch, float velocityScale, float radius, float lifetime,
            BattleshipInstance target, double power, double penetration, System.Action<string> report,
            BattleshipProjectile.Affect affect = BattleshipProjectile.Affect.AP, float explosionRadius = 1f,
            ProjectileEffectSettings effects = null)
        {
            Mouse mouse = Mouse.current;
            if (!canLaunch || mouse == null || !Application.isFocused || view == null)
            { Cancel(); return; }
            Vector2 screen = mouse.position.ReadValue();
            if (mouse.rightButton.wasPressedThisFrame ||
                (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
            { Cancel(); return; }
            if (mouse.leftButton.wasPressedThisFrame && view.pixelRect.Contains(screen))
            {
                pressScreen = screen;
                origin = WorldPoint(screen);
                dragging = true;
            }
            if (!dragging) return;
            if (!view.pixelRect.Contains(screen)) { Cancel(); return; }
            Vector2 endpoint = WorldPoint(screen);
            velocity = (endpoint - origin) * velocityScale;
            Readout = $"発射座標 ({origin.x:0.0}, {origin.y:0.0}) / 初速 ({velocity.x:0.0}, {velocity.y:0.0}) / 速さ {velocity.magnitude:0.0} 単位/秒";
            Vector2 direction = (endpoint - origin).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x);
            float head = Mathf.Min((endpoint - origin).magnitude * .25f, radius * 4f);
            preview.startWidth = preview.endWidth = Mathf.Max(.1f, radius * .25f);
            preview.SetPositions(new[] { AtDepth(origin), AtDepth(endpoint),
                AtDepth(endpoint - direction * head + side * head * .5f), AtDepth(endpoint),
                AtDepth(endpoint - direction * head - side * head * .5f) });
            preview.enabled = true;
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                // Ignore click jitter, but preserve the actual world-space velocity.
                if ((screen - pressScreen).sqrMagnitude >= 9f && velocity.sqrMagnitude > .000001f)
                {
                    var go = new GameObject("Protection Test Projectile");
                    go.transform.SetParent(projectileRoot, false);
                    go.AddComponent<ProtectionTestProjectile>().Initialize(material, origin, velocity, radius,
                        lifetime, target, power, penetration, report, affect, explosionRadius, effects);
                }
                Cancel();
            }
            else if (!mouse.leftButton.isPressed) Cancel();
        }

        private Vector2 WorldPoint(Vector2 screen)
        {
            Ray ray = view.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            return plane.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : Vector2.zero;
        }

        private static Vector3 AtDepth(Vector2 point) => new Vector3(point.x, point.y, -.15f);
        private void OnDisable() => Cancel();
        private void OnApplicationFocus(bool focused) { if (!focused) Cancel(); }
    }
}
