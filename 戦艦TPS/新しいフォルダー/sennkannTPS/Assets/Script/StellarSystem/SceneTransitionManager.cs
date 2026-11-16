using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarSystem
{
    [Serializable]
    public sealed class MapViewState
    {
        public int planetID, lagrangeID;
        public double centerX, centerY, halfHeightAU;
        public bool follow, rotatingView, showPoints;
    }

    // 同じプレイセッション中のマップ↔建造シーン往復を担当する。
    // 恒久セーブではないためアプリ再起動時の復元は別途実装すること。
    public static class SceneTransitionManager
    {
        public const string MapSceneName = "StellarSystem";
        public const string ConstructionSceneName = "ShipConstruction"; // renamed from Assets/Scenes/TilingTest.unity

        private sealed class Session
        {
            public double currentTime, sortieTime, timeScale;
            public bool paused;
            public MapViewState view;
        }

        private static Session session;

        public static bool OpenConstruction(StellarSystemMap map, SystemController clock, out string error)
        {
            error = null;
            if (map == null || clock == null)
            {
                error = "マップまたは時計が見つかりません。";
                return false;
            }
            var fleet = map.GetComponent<FleetManager>();
            if (fleet != null && fleet.IsTraveling)
            {
                error = "航行中は建造できません。";
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(ConstructionSceneName))
            {
                error = "建造シーン ShipConstruction がBuild Settingsに未登録です。「戦艦TPS/既存タイリングテストを建造シーンへ改名・接続」を実行してください。";
                Debug.LogWarning("[SceneTransitionManager] " + error);
                return false;
            }
            session = new Session
            {
                currentTime = clock.currentTime,
                sortieTime = clock.SortieTime,
                timeScale = clock.timeScale,
                paused = clock.isPaused,
                view = map.CaptureViewState()
            };
            clock.Pause();
            try
            {
                SceneManager.LoadScene(ConstructionSceneName);
                return true;
            }
            catch (Exception ex)
            {
                var originalPaused = session != null && session.paused;
                session = null;
                if (!originalPaused) clock.Resume();
                error = "建造シーンを読み込めません: " + ex.Message;
                Debug.LogException(ex);
                return false;
            }
        }

        public static bool ReturnToMap(out string error)
        {
            error = null;
            if (!Application.CanStreamedLevelBeLoaded(MapSceneName))
            {
                error = "恒星系マップがBuild Settingsに登録されていません。";
                Debug.LogWarning("[SceneTransitionManager] " + error);
                return false;
            }
            try
            {
                SceneManager.LoadScene(MapSceneName);
                return true;
            }
            catch (Exception ex)
            {
                error = "恒星系マップを読み込めません: " + ex.Message;
                Debug.LogException(ex);
                return false;
            }
        }

        public static void TryRestoreMap(StellarSystemMap map, SystemController clock)
        {
            if (session == null || map == null || clock == null) return;
            var saved = session;
            session = null;
            clock.StopAt(saved.currentTime);
            clock.SortieTime = saved.sortieTime;
            clock.SetTimeScale(saved.timeScale);
            map.RestoreViewState(saved.view);
            if (!saved.paused) clock.Resume();
        }
    }
}
