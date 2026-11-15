using System;
using UnityEngine;

// 単位: currentTime / SortieTime = 日, timeScale = 日/実秒
public class SystemController : MonoBehaviour
{
    public double currentTime = 0.0;
    public double SortieTime = 0.0;
    public double timeScale = 10.0;
    public bool isPaused = true;

    // 予約停止時刻は絶対日付。マップの大きな時間ステップでも停止時刻を通り越さない。
    private double? scheduledStopTime;
    public event Action<double> OnScheduledStop;

    private void Update()
    {
        if (isPaused) return;
        if (!IsFinite(timeScale) || timeScale < 0) { isPaused = true; return; }

        double nextTime = currentTime + Time.unscaledDeltaTime * timeScale;
        if (scheduledStopTime.HasValue && nextTime >= scheduledStopTime.Value)
        {
            currentTime = scheduledStopTime.Value;
            scheduledStopTime = null;
            isPaused = true;
            OnScheduledStop?.Invoke(currentTime);
        }
        else
        {
            currentTime = nextTime;
        }
    }

    private static bool IsFinite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);

    public void Pause() => isPaused = true;
    public void Resume() => isPaused = false;

    public void SetTimeScale(double scale)
    {
        if (!IsFinite(scale) || scale < 0) return;
        timeScale = scale;
    }

    public void MarkSortie() => SortieTime = currentTime;

    // 既存APIの意味を維持: 即時に現在時刻を変更し、一時停止する。
    public void StopAt(double time)
    {
        if (!IsFinite(time) || time < 0) return;
        currentTime = time;
        scheduledStopTime = null;
        isPaused = true;
    }

    // 今後の遭遇判定用。現在時刻より未来の絶対時刻を指定する。
    public void ScheduleStop(double absoluteDay)
    {
        if (!IsFinite(absoluteDay) || absoluteDay < currentTime) return;
        scheduledStopTime = absoluteDay;
    }

    public void CancelScheduledStop() => scheduledStopTime = null;
}
