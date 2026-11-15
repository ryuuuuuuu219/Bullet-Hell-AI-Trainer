using System;
using UnityEngine;

namespace StellarSystem
{
    public enum EnemyMovementMode { Stationary, CircularDefense, EllipticalPatrol, LinearAttack }

    // 敵艦隊の移動を時刻関数として定義する。物理 Collider / Rigidbody は使わない。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngageCollider))]
    public sealed class EnemyFleet : MonoBehaviour
    {
        public string fleetName = "敵哨戒艦隊";
        public EnemyMovementMode movementMode = EnemyMovementMode.EllipticalPatrol;
        [Header("軌道中心または静止座標 (AU)")]
        public double centerX, centerY;
        [Header("円軌道: 半径/周期/開始角度")]
        [Min(0.000001f)] public double orbitRadiusAU = 0.1;
        [Min(0.000001f)] public double periodDays = 5;
        public double phaseDegrees;
        [Header("楕円哨戒: 軌道長半径/離心率/近日点角度")]
        [Min(0.000001f)] public double semiMajorAxisAU = 0.1;
        [Range(0f, 0.99f)] public double eccentricity = 0.15;
        public double periapsisDegrees;
        [Header("直線攻撃: 初期時刻での座標/速度 (AU/日)")]
        public double startDay;
        public double velocityX, velocityY;

        private LineRenderer marker;
        private Material markerMaterial;
        private SystemController clock;
        private Camera mapCamera;

        public Position2D PositionAt(double day)
        {
            var center = new Position2D(centerX, centerY);
            double angle = phaseDegrees * Math.PI / 180;
            switch (movementMode)
            {
                case EnemyMovementMode.Stationary:
                    return center;
                case EnemyMovementMode.LinearAttack:
                    return center + new Position2D(velocityX, velocityY) * (day - startDay);
                case EnemyMovementMode.CircularDefense:
                    if (periodDays <= 0) return center;
                    return center + OrbitMath.Rotate(new Position2D(orbitRadiusAU, 0),
                        angle + (day % periodDays) / periodDays * (Math.PI * 2));
                case EnemyMovementMode.EllipticalPatrol:
                    if (periodDays <= 0 || semiMajorAxisAU <= 0 || eccentricity < 0 || eccentricity >= 1) return center;
                    double mean = angle + (day % periodDays) / periodDays * (Math.PI * 2);
                    double E = StellarSystemMap.SolveKepler(mean, eccentricity);
                    var pos = new Position2D(semiMajorAxisAU * (Math.Cos(E) - eccentricity),
                        semiMajorAxisAU * Math.Sqrt(1 - eccentricity * eccentricity) * Math.Sin(E));
                    return center + OrbitMath.Rotate(pos, periapsisDegrees * Math.PI / 180);
                default: return center;
            }
        }

        // 時刻間隔の安全な上限を求めるための速度上界 (AU/日)。
        public double MaxSpeedAUPerDay
        {
            get
            {
                if (movementMode == EnemyMovementMode.LinearAttack)
                    return Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
                if (movementMode == EnemyMovementMode.CircularDefense)
                    return periodDays > 0 ? 2 * Math.PI * orbitRadiusAU / periodDays : 0;
                if (movementMode == EnemyMovementMode.EllipticalPatrol && periodDays > 0 && eccentricity >= 0 && eccentricity < 1)
                    return 2 * Math.PI * semiMajorAxisAU / periodDays * Math.Sqrt((1 + eccentricity) / (1 - eccentricity));
                return 0;
            }
        }

        private void Start()
        {
            clock = FindFirstObjectByType<SystemController>();
            var map = FindFirstObjectByType<StellarSystemMap>();
            mapCamera = map != null ? map.GetComponentInChildren<Camera>() : Camera.main;
            var go = new GameObject("Enemy Marker");
            go.transform.SetParent(transform, false);
            marker = go.AddComponent<LineRenderer>();
            marker.useWorldSpace = true;
            marker.loop = true;
            marker.positionCount = 4;
            marker.alignment = LineAlignment.View;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) { markerMaterial = new Material(shader); marker.sharedMaterial = markerMaterial; }
            marker.startColor = marker.endColor = new Color(1f, 0.35f, 0.25f);
        }

        private void LateUpdate()
        {
            if (marker == null || clock == null) return;
            Position2D p = PositionAt(clock.currentTime);
            float radius = mapCamera != null ? Mathf.Max(0.00001f,
                2 * mapCamera.orthographicSize / Mathf.Max(1, Screen.height) * 6) : .02f;
            marker.startWidth = marker.endWidth = radius * .25f;
            marker.SetPosition(0, new Vector3((float)p.X, (float)p.Y + radius, -.2f));
            marker.SetPosition(1, new Vector3((float)p.X + radius, (float)p.Y, -.2f));
            marker.SetPosition(2, new Vector3((float)p.X, (float)p.Y - radius, -.2f));
            marker.SetPosition(3, new Vector3((float)p.X - radius, (float)p.Y, -.2f));
        }

        private void OnDestroy() { if (markerMaterial != null) Destroy(markerMaterial); }
    }
}
