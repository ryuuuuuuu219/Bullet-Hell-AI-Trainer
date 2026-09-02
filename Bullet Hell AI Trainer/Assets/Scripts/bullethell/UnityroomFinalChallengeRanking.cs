using System;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using Unityroom.Client;
#endif

public static class UnityroomFinalChallengeRanking
{
#if UNITY_WEBGL && !UNITY_EDITOR
    private static RankingSender sender;
#endif

    public static void SubmitSurvivalTime(float survivalTimeSeconds)
    {
        float score = Mathf.Round(
            Mathf.Max(0f, survivalTimeSeconds) * 1000f) / 1000f;

#if UNITY_WEBGL && !UNITY_EDITOR
        EnsureSender().QueueScore(score);
#else
        Debug.Log(
            $"[unityroomランキング] FinalChallenge生存時間 {score:F3}秒。" +
            "送信はunityroom上のWebGLビルドでのみ実行されます。");
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private static RankingSender EnsureSender()
    {
        if (sender != null)
        {
            return sender;
        }

        GameObject senderObject = new GameObject(
            "Unityroom FinalChallenge Ranking Sender");
        UnityEngine.Object.DontDestroyOnLoad(senderObject);
        sender = senderObject.AddComponent<RankingSender>();
        return sender;
    }

    private sealed class RankingSender : MonoBehaviour
    {
        private UnityroomClient client;
        private UnityroomRankingSettings settings;
        private float pendingBestScore = -1f;
        private bool isSending;

        public void QueueScore(float score)
        {
            pendingBestScore = Mathf.Max(pendingBestScore, score);
            if (!isSending)
            {
                SendPendingScoresAsync();
            }
        }

        private async void SendPendingScoresAsync()
        {
            isSending = true;

            if (!TryInitializeClient())
            {
                pendingBestScore = -1f;
                isSending = false;
                return;
            }

            while (pendingBestScore >= 0f)
            {
                float score = pendingBestScore;
                pendingBestScore = -1f;

                try
                {
                    SendScoreResponse response =
                        await client.Scoreboards.SendAsync(new SendScoreRequest
                        {
                            ScoreboardId =
                                settings.FinalChallengeScoreboardId,
                            Score = score,
                        });
                    Debug.Log(
                        response.ScoreUpdated
                            ? $"[unityroomランキング] 生存時間 {score:F3}秒を登録しました。"
                            : $"[unityroomランキング] 生存時間 {score:F3}秒は自己ベストを更新しませんでした。");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[unityroomランキング] 生存時間 {score:F3}秒の送信に失敗しました: " +
                        exception.Message);
                }
            }

            isSending = false;
            if (pendingBestScore >= 0f)
            {
                SendPendingScoresAsync();
            }
        }

        private bool TryInitializeClient()
        {
            if (client != null)
            {
                return true;
            }

            settings = UnityroomRankingSettings.Load();
            if (settings == null)
            {
                Debug.LogWarning(
                    "[unityroomランキング] 設定アセットがありません: " +
                    UnityroomRankingSettings.ResourcePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.HmacKey))
            {
                Debug.LogWarning(
                    "[unityroomランキング] HMAC認証用キーが未入力です: " +
                    UnityroomRankingSettings.ResourcePath);
                return false;
            }

            try
            {
                client = new UnityroomClient
                {
                    HmacKey = settings.HmacKey,
                    Timeout = TimeSpan.FromSeconds(10),
                };
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[unityroomランキング] HMAC認証用キーを読み込めませんでした: " +
                    exception.Message);
                client?.Dispose();
                client = null;
                return false;
            }
        }

        private void OnDestroy()
        {
            client?.Dispose();
            client = null;
            if (sender == this)
            {
                sender = null;
            }
        }
    }
#endif
}
