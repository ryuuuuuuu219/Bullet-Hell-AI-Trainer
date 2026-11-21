using UnityEngine;

namespace StellarSystem
{
    // Added to the existing TilingTest scene by ConstructionSceneSetup.
    // Intentionally independent of TilingTestScene's generation and test UI.
    public sealed class ShipConstructionReturnUI : MonoBehaviour
    {
        private string error;

        public bool TryCommitAndReturn(out string failure)
        {
            var construction = GetComponent<Script.TilingTestScene>();
            if (construction == null) construction = FindFirstObjectByType<Script.TilingTestScene>();
            failure = "建造画面が見つかりません。";
            if (construction == null || !construction.TryGetBuild(out var data, out failure)) return false;
            if (!Application.CanStreamedLevelBeLoaded(SceneTransitionManager.MapSceneName)) {
                failure = "恒星系マップがBuild Settingsに登録されていません。";
                return false;
            }
            return Script.ShipBuildStore.TrySave(data, out failure) && SceneTransitionManager.ReturnToMap(out failure);
        }

        private void OnGUI()
        {
            float width = Mathf.Min(245f, Mathf.Max(1f, Screen.width - 16f));
            const float height = 120f;
            var rect = new Rect(Mathf.Max(0f, Screen.width - width - 8f),
                Mathf.Max(0f, Screen.height - height - 8f), width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("艦船建造 / タイリング試験");
            var construction = GetComponent<Script.TilingTestScene>();
            if (construction == null) construction = FindFirstObjectByType<Script.TilingTestScene>();
            Script.ShipBuildData data = null;
            string validation = "建造画面が見つかりません。";
            bool valid = construction != null && construction.TryGetBuild(out data, out validation);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && valid;
            if (GUILayout.Button("恒星系マップへ戻る", GUILayout.Height(30f)))
            {
                if (!TryCommitAndReturn(out error))
                    Debug.LogWarning("[ShipConstructionReturnUI] " + error);
            }
            GUI.enabled = previousEnabled;
            if (!valid) GUILayout.Label(validation);
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error);
            GUILayout.EndArea();
        }
    }
}
