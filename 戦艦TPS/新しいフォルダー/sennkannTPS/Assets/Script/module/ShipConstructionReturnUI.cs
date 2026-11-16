using UnityEngine;

namespace StellarSystem
{
    // Added to the existing TilingTest scene by ConstructionSceneSetup.
    // Intentionally independent of TilingTestScene's generation and test UI.
    public sealed class ShipConstructionReturnUI : MonoBehaviour
    {
        private string error;

        private void OnGUI()
        {
            float width = Mathf.Min(245f, Mathf.Max(1f, Screen.width - 16f));
            const float height = 84f;
            var rect = new Rect(Mathf.Max(0f, Screen.width - width - 8f),
                Mathf.Max(0f, Screen.height - height - 8f), width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("艦船建造 / タイリング試験");
            if (GUILayout.Button("恒星系マップへ戻る", GUILayout.Height(30f)))
            {
                if (!SceneTransitionManager.ReturnToMap(out error))
                    Debug.LogWarning("[ShipConstructionReturnUI] " + error);
            }
            if (!string.IsNullOrEmpty(error)) GUILayout.Label(error);
            GUILayout.EndArea();
        }
    }
}
