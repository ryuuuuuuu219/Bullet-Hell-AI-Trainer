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

    public static void SubmitDamage(float damage)
    {
        float score = Mathf.Max(0f, damage);

#if UNITY_WEBGL && !UNITY_EDITOR
        EnsureSender().QueueScore(score);
#else
        Debug.Log(
            $"[unityroomランキング] Challenge Ranking E-1 ダメージ量 {score:F0}。" +
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
            "Unityroom Challenge Ranking Sender");
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
                                settings.ChallengeRankingScoreboardId,
                            Score = score,
                        });
                    Debug.Log(
                        response.ScoreUpdated
                            ? $"[unityroomランキング] ダメージ量 {score:F0}を登録しました。"
                            : $"[unityroomランキング] ダメージ量 {score:F0}は自己ベストを更新しませんでした。");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[unityroomランキング] ダメージ量 {score:F0}の送信に失敗しました: " +
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
