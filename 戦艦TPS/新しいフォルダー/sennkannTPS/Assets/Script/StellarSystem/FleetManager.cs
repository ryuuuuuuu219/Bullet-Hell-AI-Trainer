using System;
using UnityEngine;

namespace StellarSystem
{
    // 自艦隊の移動を扱う最小構成。
    // マップ時刻を唯一の基準に、直線・一定速度で移動する標的の未来位置を迎撃する。
    // 道中遭遇・戦闘シーン遷移は次の工程で実装する。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StellarSystemMap))]
    [RequireComponent(typeof(EngageCollider))]
    public sealed class FleetManager : MonoBehaviour
    {
        [Header("自艦隊の移動速度（AU/日）")]
        [Min(0.001f)] public float speedAUPerDay = 0.2f;
        [Header("到着判定半径（AU）")]
        [Min(0.0001f)] public float arrivalRadiusAU = 0.005f;
        [Header("初期拠点（惑星IDとL点番号）")]
        [Min(1)] public int initialPlanetID = 4;
        [Range(-1, 5)] public int initialLagrangeID = 2;
        [Header("迎撃予測の最大日数")]
        [Min(1)] public int maxPredictionDays = 4000;

        private StellarSystemMap map;
        private SystemController clock;
        private LineRenderer route;
        private Material routeMaterial;
        private Position2D departurePosition;
        private Position2D arrivalPosition;
        private Position2D currentPosition;
        private double departureDay;
        private double arrivalDay;
        private bool traveling;
        private bool arrived;
        // 到着後も目的地の公転に追従して、次の出撃地点を決められるようにする。
        private int stationedPlanetID;
        private int stationedLagrangeID;
        private string message = "待機中";

        public bool IsTraveling => traveling;
        public bool HasPosition => map != null && clock != null;
        public double ArrivalDay => arrivalDay;
        public double Progress => !traveling ? (arrived ? 1 : 0) :
            Math.Max(0, Math.Min(1, (clock.currentTime - departureDay) / Math.Max(1e-9, arrivalDay - departureDay)));
        public string StatusText => traveling ? "自艦隊：航行中 / " + message : "自艦隊：" + message;
        public Position2D CurrentPosition => currentPosition;
        public Position2D CourseStart => departurePosition;
        public Position2D CourseEnd => arrivalPosition;
        public double DepartureDay => departureDay;
        public bool IsWithinCourse(double day) => traveling && day >= departureDay && day <= arrivalDay;
        public double CourseSpeedAUPerDay => speedAUPerDay;
        public event Action CourseStarted;
        public event Action CourseArrived;

        // 出撃時の固定直線航路。画面上の表示位置とは独立して時刻から直接求める。
        public Position2D PositionAt(double day)
        {
            if (!traveling) return currentPosition;
            double total = Math.Max(1e-12, arrivalDay - departureDay);
            double ratio = Math.Max(0, Math.Min(1, (day - departureDay) / total));
            return departurePosition + (arrivalPosition - departurePosition) * ratio;
        }

        private void Awake()
        {
            map = GetComponent<StellarSystemMap>();
            clock = GetComponent<SystemController>();
            if (clock == null) clock = FindFirstObjectByType<SystemController>();
            stationedPlanetID = initialPlanetID;
            stationedLagrangeID = initialLagrangeID;
        }

        private void Start()
        {
            if (map == null || clock == null) { enabled = false; return; }
            currentPosition = TargetPosition(stationedPlanetID, stationedLagrangeID, clock.currentTime);
            CreateRoute();
        }

        private void OnEnable()
        {
            if (map == null) map = GetComponent<StellarSystemMap>();
            if (clock == null) clock = GetComponent<SystemController>();
            if (map != null) map.SortieRequested += OnSortieRequested;
            if (clock != null) clock.OnScheduledStop += OnClockStopped;
        }

        private void OnDisable()
        {
            if (map != null) map.SortieRequested -= OnSortieRequested;
            if (clock != null) clock.OnScheduledStop -= OnClockStopped;
        }

        private void LateUpdate()
        {
            if (map == null || clock == null) return;
            double now = clock.currentTime;
            if (traveling)
            {
                currentPosition = PositionAt(now);
                if (route != null)
                {
                    route.SetPosition(0, new Vector3((float)currentPosition.X, (float)currentPosition.Y, -0.1f));
                    route.SetPosition(1, new Vector3((float)arrivalPosition.X, (float)arrivalPosition.Y, -0.1f));
                    route.startWidth = route.endWidth = Mathf.Max(0.00001f,
                        (float)(2 * GetMapHalfHeight() / Mathf.Max(1, Screen.height) * 2));
                }
            }
            else
            {
                currentPosition = TargetPosition(stationedPlanetID, stationedLagrangeID, now);
            }
        }

        // 目標は出撃時の位置ではなく到着時刻の惑星/L点の位置。
        // 最初に到達できる時刻を0.5日単位で探索し、二分法で詰める。
        private bool TryPredictArrival(int planetID, int lagrangeID, double start,
            Position2D from, out double time, out Position2D target)
        {
            time = start;
            target = from;
            double speed = speedAUPerDay;
            if (speed <= 0 || !IsFinite(speed)) return false;
            double r = Math.Max(0, arrivalRadiusAU);
            double previous = start;
            for (double elapsed = 0.5; elapsed <= maxPredictionDays; elapsed += 0.5)
            {
                double candidate = start + elapsed;
                Position2D pos = TargetPosition(planetID, lagrangeID, candidate);
                double distanceLeft = (pos - from).Length - (speed * elapsed + r);
                if (distanceLeft <= 0)
                {
                    double lo = previous, hi = candidate;
                    for (int k = 0; k < 48; k++)
                    {
                        double mid = (lo + hi) * 0.5;
                        double d = (TargetPosition(planetID, lagrangeID, mid) - from).Length -
                            (speed * (mid - start) + r);
                        if (d <= 0) hi = mid; else lo = mid;
                    }
                    time = hi;
                    target = TargetPosition(planetID, lagrangeID, hi);
                    return true;
                }
                previous = candidate;
            }
            return false;
        }

        private void OnSortieRequested(int planetID, int lagrangeID)
        {
            if (traveling) return;
            if (map == null || clock == null) return;
            if (planetID < 1 || planetID > map.planets.Length || lagrangeID < -1 || lagrangeID > 5 || lagrangeID == 0) return;
            double start = clock.currentTime;
            Position2D origin = currentPosition;
            if (!TryPredictArrival(planetID, lagrangeID, start, origin, out double targetDay, out Position2D target))
            {
                message = "指定された日数内に迎撃航路を計算できません";
                return;
            }
            // すでに到着半径内にある場合はその場で到着扱い。
            if ((TargetPosition(planetID, lagrangeID, start) - origin).Length <= arrivalRadiusAU)
            {
                stationedPlanetID = planetID;
                stationedLagrangeID = lagrangeID;
                currentPosition = TargetPosition(planetID, lagrangeID, start);
                arrived = true;
                clock.Pause();
                message = "目的地に到着";
                return;
            }
            departurePosition = origin;
            // 到着判定は目標中心からarrivalRadiusAU以内。
            // 移動速度を正確に保つため、目標中心ではなく境界への接地点を航路終端とする。
            double distance = (target - origin).Length;
            arrivalPosition = distance <= 1e-12 ? origin :
                origin + (target - origin) * Math.Max(0, (distance - arrivalRadiusAU) / distance);
            departureDay = start;
            arrivalDay = targetDay;
            traveling = true;
            arrived = false;
            stationedPlanetID = planetID;
            stationedLagrangeID = lagrangeID;
            message = "第" + planetID + "惑星" + (lagrangeID > 0 ? " L" + lagrangeID : "") + "へ";
            CreateRoute();
            if (route != null) route.enabled = true;
            clock.ScheduleStop(arrivalDay);
            clock.Resume();
            CourseStarted?.Invoke();
        }

        private void OnClockStopped(double stopDay)
        {
            if (!traveling || stopDay + 1e-7 < arrivalDay) return;
            traveling = false;
            arrived = true;
            currentPosition = TargetPosition(stationedPlanetID, stationedLagrangeID, stopDay);
            message = "目的地に到着";
            if (route != null) route.enabled = false;
            CourseArrived?.Invoke();
            Debug.Log("[FleetManager] 到着: 第" + stationedPlanetID + "惑星 / L" + stationedLagrangeID + " 時刻=" + stopDay.ToString("F3"));
        }

        private Position2D TargetPosition(int planetID, int lagrangeID, double day)
        {
            if (map == null || map.planets == null || map.planets.Length == 0) return new Position2D(0, 0);
            int index = Mathf.Clamp(planetID - 1, 0, map.planets.Length - 1);
            return lagrangeID >= 1 && lagrangeID <= 5 ?
                map.LagrangePosition(index, lagrangeID, day) : map.PlanetPosition(index, day);
        }

        private void CreateRoute()
        {
            if (route != null) return;
            var obj = new GameObject("Player Route");
            obj.transform.SetParent(transform, false);
            route = obj.AddComponent<LineRenderer>();
            route.useWorldSpace = true;
            route.positionCount = 2;
            route.alignment = LineAlignment.View;
            route.enabled = false;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                routeMaterial = new Material(shader);
                route.sharedMaterial = routeMaterial;
            }
            route.startColor = route.endColor = new Color(0.3f, 1f, 0.45f);
        }

        private double GetMapHalfHeight()
        {
            // マップカメラの正射影サイズを参照し、ラインを画面上ほぼ2pxに保つ。
            var camera = GetComponentInChildren<Camera>();
            return camera != null ? camera.orthographicSize : 10;
        }

        private static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        private void OnDestroy()
        {
            if (routeMaterial != null) Destroy(routeMaterial);
        }
    }
}
