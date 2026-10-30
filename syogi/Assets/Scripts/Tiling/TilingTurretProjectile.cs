using UnityEngine;

namespace Aquarium.Tiling
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TilingTurretProjectile : MonoBehaviour
    {
        private const int CircleSegments = 24;
        private const float TrailSeconds = .45f;
        private Vector2 direction;
        private float speed;
        private float lifetime;
        private float elapsed;

        public void Initialize(Material material, Vector2 heading, float moveSpeed, float radius,
            float lineWidth, float lifeSeconds, Color color, bool useTrail)
        {
            direction = heading.sqrMagnitude > 0 ? heading.normalized : Vector2.up;
            speed = Mathf.Max(.01f, moveSpeed);
            lifetime = Mathf.Max(.01f, lifeSeconds);

            LineRenderer outline = GetComponent<LineRenderer>();
            outline.sharedMaterial = material;
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.positionCount = CircleSegments;
            outline.startWidth = outline.endWidth = Mathf.Max(.01f, lineWidth);
            outline.startColor = outline.endColor = color;
            outline.sortingOrder = 2;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                outline.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }

            if (!useTrail) return;
            TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = TrailSeconds;
            trail.minVertexDistance = Mathf.Max(.05f, lineWidth * .25f);
            trail.startWidth = trail.endWidth = Mathf.Max(.01f, lineWidth);
            trail.startColor = trail.endColor = color;
            trail.sortingOrder = 1;
            trail.emitting = true;
        }

        private void Update()
        {
            float step = Mathf.Min(Time.deltaTime, lifetime - elapsed);
            transform.position += (Vector3)(direction * speed * step);
            elapsed += step;
            if (elapsed >= lifetime) Destroy(gameObject);
        }
    }
}
