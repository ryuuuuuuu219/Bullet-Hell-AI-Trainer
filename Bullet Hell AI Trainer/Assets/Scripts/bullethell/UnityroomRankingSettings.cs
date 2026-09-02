using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = AssetName,
    menuName = "Bullet Hell AI Trainer/unityroomランキング設定")]
public sealed class UnityroomRankingSettings : ScriptableObject
{
    public const string AssetName = "UnityroomRankingSettings";
    public const string ResourcePath = "Assets/Resources/" + AssetName + ".asset";

    [Header("unityroom API")]
    [SerializeField, Tooltip("unityroomのAPIキー画面にあるHMAC認証用キー")]
    private string hmacKey = string.Empty;

    [SerializeField, Min(1), Tooltip("FinalChallenge生存時間用スコアボードのID")]
    private int finalChallengeScoreboardId = 1;

    public string HmacKey => hmacKey?.Trim();
    public int FinalChallengeScoreboardId => finalChallengeScoreboardId;

    public static UnityroomRankingSettings Load()
    {
        return Resources.Load<UnityroomRankingSettings>(AssetName);
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EnsureLocalSettingsAssetExists()
    {
        if (AssetDatabase.LoadAssetAtPath<UnityroomRankingSettings>(
                ResourcePath) != null)
        {
            return;
        }

        Directory.CreateDirectory("Assets/Resources");
        UnityroomRankingSettings settings =
            CreateInstance<UnityroomRankingSettings>();
        AssetDatabase.CreateAsset(settings, ResourcePath);
        AssetDatabase.SaveAssets();
        Debug.Log(
            "unityroomランキング設定を作成しました。" +
            "InspectorでHMAC認証用キーとスコアボードIDを入力してください: " +
            ResourcePath);
    }
#endif
}
