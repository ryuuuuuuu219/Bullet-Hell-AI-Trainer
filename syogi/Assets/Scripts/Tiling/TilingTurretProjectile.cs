using System;
using UnityEngine;

namespace Aquarium.Tiling
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TilingTurretProjectile : MonoBehaviour
    {
        private const int CircleSegments = 24;
        private const float TrailSeconds = 1f;
        private Vector2 direction;
        private float speed;
        private float lifetime;
        private float elapsed;
        private bool guided;
        private float maximumTurnDegreesPerSecond;
        private float navigationConstant;
        private Func<Vector2> targetProvider;
        private Vector2 previousLineOfSight;

        public void Initialize(Material material, Vector2 heading, float moveSpeed, float radius,
            float lineWidth, float lifeSeconds, Color color, bool useTrail,
            bool useGuidance = false, float turnDegreesPerSecond = 0f, float proportionalNavigation = 0f,
            Func<Vector2> guidanceTarget = null)
        {
            direction = heading.sqrMagnitude > 0 ? heading.normalized : Vector2.up;
            speed = Mathf.Max(.01f, moveSpeed);
            lifetime = Mathf.Max(.01f, lifeSeconds);
            guided = useGuidance && guidanceTarget != null && turnDegreesPerSecond > 0f && proportionalNavigation > 0f;
            maximumTurnDegreesPerSecond = Mathf.Max(0f, turnDegreesPerSecond);
            navigationConstant = Mathf.Max(0f, proportionalNavigation);
            targetProvider = guidanceTarget;
            if (guided)
            {
                Vector2 lineOfSight = targetProvider() - (Vector2)transform.position;
                previousLineOfSight = lineOfSight.sqrMagnitude > .000001f ? lineOfSight.normalized : direction;
            }

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
            trail.widthMultiplier = Mathf.Max(.01f, radius * 2f);
            trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            trail.startColor = trail.endColor = color;
            trail.sortingOrder = 1;
            trail.emitting = true;
        }

        private void Update()
        {
            float step = Mathf.Min(Time.deltaTime, lifetime - elapsed);
            if (guided && step > 0f) ApplyProportionalNavigation(step);
            transform.position += (Vector3)(direction * speed * step);
            elapsed += step;
            if (elapsed >= lifetime) Destroy(gameObject);
        }

        private void ApplyProportionalNavigation(float seconds)
        {
            Vector2 lineOfSight = targetProvider() - (Vector2)transform.position;
            if (lineOfSight.sqrMagnitude <= .000001f) return;
            lineOfSight.Normalize();
            float sightRate = Vector2.SignedAngle(previousLineOfSight, lineOfSight) / seconds;
            float turnRate = Mathf.Clamp(navigationConstant * sightRate,
                -maximumTurnDegreesPerSecond, maximumTurnDegreesPerSecond);
            direction = Quaternion.Euler(0f, 0f, turnRate * seconds) * direction;
            direction.Normalize();
            previousLineOfSight = lineOfSight;
        }
    }
}
