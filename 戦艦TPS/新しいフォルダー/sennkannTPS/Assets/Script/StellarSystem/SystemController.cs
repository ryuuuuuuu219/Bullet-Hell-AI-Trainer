using UnityEngine;

public class SystemController : MonoBehaviour
{
    public double currentTime = 0.0;
    public double SortieTime = 0.0;
    public double timeScale = 1.0;
    public bool isPaused = true;

    private void Update()
    {
        if (isPaused)
            return;

        currentTime += Time.unscaledDeltaTime * timeScale;
    }

    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        isPaused = false;
    }

    public void SetTimeScale(double scale)
    {
        if (scale < 0)
            return;

        timeScale = scale;
    }

    public void StopAt(double time)
    {
        currentTime = time;
        isPaused = true;
    }
}