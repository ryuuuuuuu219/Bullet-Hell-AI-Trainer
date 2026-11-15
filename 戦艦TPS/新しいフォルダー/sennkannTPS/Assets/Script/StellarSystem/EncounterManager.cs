using System;
using UnityEngine;

namespace StellarSystem
{
    // 唯一の自艦隊航路を調べ、最も早い敵艦隊との接触時刻をマップ時計に予約する。
    // 遭遇時は時計を停止し、同じ航路で再開できる。戦闘シーンへの遷移は次工程。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FleetManager))]
    public sealed class EncounterManager : MonoBehaviour
    {
        [Header("接触判定の探索設定")]
        [Min(0.001f)] public double maxSampleDays = 0.5;
        [Min(100)] public int maxSamplesPerEnemy = 100000;
        [Header("検証用: 次の出撃で航路中央に静止敵艦を1隻置く")]
        public bool spawnTestEnemyOnNextSortie;

        private SystemController clock;
        private FleetManager player;
        private EnemyFleet pendingEnemy;
        private double pendingDay = double.NaN;
        private EnemyFleet encounteredEnemy;
        private bool encounterOpen;

        public event Action<EnemyFleet, double> EncounterTriggered;
        public bool IsEncounterOpen => encounterOpen;

        private void Awake()
        {
            clock = GetComponent<SystemController>();
            player = GetComponent<FleetManager>();
        }

        private void OnEnable()
        {
            if (clock == null) clock = GetComponent<SystemController>();
            if (player == null) player = GetComponent<FleetManager>();
            if (player != null) player.CourseStarted += OnCourseStarted;
            if (clock != null) clock.OnScheduledStop += OnStop;
        }

        private void OnDisable()
        {
            if (player != null) player.CourseStarted -= OnCourseStarted;
            if (clock != null) clock.OnScheduledStop -= OnStop;
        }

        private void OnCourseStarted()
        {
            encounterOpen = false;
            encounteredEnemy = null;
            if (spawnTestEnemyOnNextSortie)
            {
                CreateTestEnemy();
                spawnTestEnemyOnNextSortie = false;
            }
            Recalculate(clock.currentTime);
        }

        private void CreateTestEnemy()
        {
            var midpoint = player.CourseStart + (player.CourseEnd - player.CourseStart) * 0.5;
            var go = new GameObject("TEST - 航路上の哨戒艦隊");
            var enemy = go.AddComponent<EnemyFleet>();
            enemy.fleetName = "検証用哨戒艦隊";
            enemy.movementMode = EnemyMovementMode.Stationary;
            enemy.centerX = midpoint.X;
            enemy.centerY = midpoint.Y;
        }

        // 距離と相対速度上界から探索刻みを制限するため、狭い接触範囲を飛び越えにくい。
        // 未確定の場合、到着側の予定停止を維持する。
        private void Recalculate(double fromDay)
        {
            pendingEnemy = null;
            pendingDay = double.NaN;
            if (player == null || clock == null || !player.IsTraveling) return;
            double until = player.ArrivalDay;
            if (fromDay >= until) return;
            foreach (var enemy in FindObjectsByType<EnemyFleet>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var collider = enemy.GetComponent<EngageCollider>();
                if (collider == null || !collider.encounterEnabled) continue;
                if (!TryFindContact(enemy, fromDay, until, out double day)) continue;
                if (pendingEnemy == null || day < pendingDay)
                {
                    pendingEnemy = enemy;
                    pendingDay = day;
                }
            }
            clock.ScheduleStop(pendingEnemy == null ? player.ArrivalDay : pendingDay);
        }

        private bool TryFindContact(EnemyFleet enemy, double start, double end, out double result)
        {
            result = double.NaN;
            var playerCollider = player.GetComponent<EngageCollider>();
            var enemyCollider = enemy.GetComponent<EngageCollider>();
            if (playerCollider == null || !playerCollider.encounterEnabled || enemyCollider == null) return false;
            double radius = Math.Max(0, playerCollider.radiusAU) + Math.Max(0, enemyCollider.radiusAU);
            double upperSpeed = player.CourseSpeedAUPerDay + enemy.MaxSpeedAUPerDay;
            if (!IsFinite(upperSpeed) || upperSpeed <= 0) return false;
            double day = start;
            double distance = Distance(enemy, day);
            if (distance <= radius) { result = day; return true; }
            for (int i = 0; i < maxSamplesPerEnemy && day < end; i++)
            {
                // 前回の距離から、速度上界でも接触できない安全な時間幅だけ進める。
                double safeStep = 0.8 * (distance - radius) / upperSpeed;
                // 安全歩幅だけでは接触境界に漸近し続けるため、接触半径に比べて
                // 十分短い最小歩幅を与え、境界の内側に進んでから二分探索する。
                double minimumStep = Math.Max(1e-10, radius / upperSpeed * 0.1);
                double step = Math.Min(Math.Max(minimumStep, safeStep), Math.Max(minimumStep, maxSampleDays));
                double next = Math.Min(end, day + step);
                if (next <= day) break;
                double nextDistance = Distance(enemy, next);
                if (nextDistance <= radius)
                {
                    double lo = day, hi = next;
                    for (int j = 0; j < 50; j++)
                    {
                        double mid = (lo + hi) * .5;
                        if (Distance(enemy, mid) <= radius) hi = mid; else lo = mid;
                    }
                    result = hi;
                    return true;
                }
                day = next;
                distance = nextDistance;
            }
            return false;
        }

        private double Distance(EnemyFleet enemy, double day) =>
            (player.PositionAt(day) - enemy.PositionAt(day)).Length;

        private void OnStop(double stopDay)
        {
            if (pendingEnemy == null || !IsFinite(pendingDay) || Math.Abs(stopDay - pendingDay) > 1e-6) return;
            encounteredEnemy = pendingEnemy;
            pendingEnemy = null;
            pendingDay = double.NaN;
            encounterOpen = true;
            Debug.Log("[EncounterManager] 接触: " + encounteredEnemy.fleetName + " / day=" + stopDay.ToString("F8"));
            EncounterTriggered?.Invoke(encounteredEnemy, stopDay);
        }

        // 仮の遭遇戦完了操作。実際の戦闘結果反映・シーン遷移は次工程。
        public void CompleteEncounterAndResume()
        {
            if (!encounterOpen || clock == null) return;
            encounterOpen = false;
            if (encounteredEnemy != null)
            {
                var collider = encounteredEnemy.GetComponent<EngageCollider>();
                if (collider != null) collider.encounterEnabled = false;
            }
            encounteredEnemy = null;
            if (player.IsTraveling)
            {
                Recalculate(clock.currentTime);
                clock.Resume();
            }
        }

        private void OnGUI()
        {
            if (!encounterOpen) return;
            float x = Mathf.Max(0, (Screen.width - 420) * .5f);
            GUILayout.BeginArea(new Rect(x, 10, 420, 120), GUI.skin.box);
            GUILayout.Label("敵艦隊と接触：" + (encounteredEnemy != null ? encounteredEnemy.fleetName : "不明"));
            GUILayout.Label("接触時刻（日）: " + clock.currentTime.ToString("F6"));
            if (GUILayout.Button("遭遇戦終了（仮）・航行再開")) CompleteEncounterAndResume();
            GUILayout.EndArea();
        }

        private static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
