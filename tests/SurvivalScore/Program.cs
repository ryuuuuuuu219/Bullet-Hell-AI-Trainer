using System;

static class Program
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    static void Main()
    {
        var run = new SpeciesSurvivalScore(2, 60);
        run.Tick(10, 10);
        Check(run.SurvivalSeconds == 0, "Not started");
        run.Begin(10);
        run.Tick(100, 59.5);
        Check(run.Score == 119 && !run.GoalReached, "Population does not multiply score");
        run.Tick(1, 0);
        run.Tick(1, double.NaN);
        run.Tick(1, double.PositiveInfinity);
        run.Tick(1, -10);
        Check(run.SurvivalSeconds == 59.5, "Pause/invalid delta");
        run.Tick(0, 0.5);
        Check(run.State == SpeciesSurvivalScore.Status.Extinct && !run.GoalReached && run.Score == 119, "Extinction boundary");
        run.Begin(1);
        run.Tick(1, 100);
        Check(run.Score == 119, "No restart after extinction");
        var success = new SpeciesSurvivalScore(1, 60);
        success.Begin(1);
        success.Tick(1, 60);
        Check(success.GoalReached && success.Score == 60, "Exact threshold");
        success.Tick(2, 8);
        Check(success.Score == 68, "Goal continues and scaled delta accepted");
        success.Tick(0, 1);
        Check(success.GoalReached && success.Score == 68, "Goal retained after extinction");
        var empty = new SpeciesSurvivalScore(1, 0);
        empty.Begin(0);
        empty.Tick(1, 100);
        Check(empty.State == SpeciesSurvivalScore.Status.Invalid && empty.Score == 0, "Empty initial population");
        var fresh = new SpeciesSurvivalScore(1, 0);
        fresh.Begin(1);
        fresh.Tick(1, 1.9);
        Check(fresh.Score == 1 && !fresh.GoalReached, "Fresh attempt, floor, optional goal");
        foreach (var value in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
        {
            bool rejected = false;
            try { new SpeciesSurvivalScore(value, 0); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid coefficient");
        }
        Console.WriteLine($"PASS: {checks} survival score checks");
    }
}
