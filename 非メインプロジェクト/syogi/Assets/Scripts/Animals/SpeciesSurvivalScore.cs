using System;

// One attempt. Population size and individual replacement do not multiply time.
public sealed class SpeciesSurvivalScore
{
    public enum Status { NotStarted, Surviving, Extinct, Invalid }
    public Status State { get; private set; }
    public double SurvivalSeconds { get; private set; }
    public double Score => Math.Floor(SurvivalSeconds * pointsPerSecond);
    public bool GoalReached => goalSeconds > 0 && SurvivalSeconds >= goalSeconds;
    private readonly double pointsPerSecond;
    private readonly double goalSeconds;

    public SpeciesSurvivalScore(double pointsPerSecond, double goalSeconds)
    {
        if (!Finite(pointsPerSecond) || pointsPerSecond <= 0 || !Finite(goalSeconds) || goalSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(pointsPerSecond));
        this.pointsPerSecond = pointsPerSecond;
        this.goalSeconds = goalSeconds;
    }

    public void Begin(int population)
    {
        if (State != Status.NotStarted) return;
        State = population > 0 ? Status.Surviving : Status.Invalid;
    }

    public void Tick(int population, double deltaSeconds)
    {
        if (State != Status.Surviving) return;
        // Never award the frame in which extinction is detected.
        if (population <= 0) { State = Status.Extinct; return; }
        if (Finite(deltaSeconds) && deltaSeconds > 0) SurvivalSeconds += deltaSeconds;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
