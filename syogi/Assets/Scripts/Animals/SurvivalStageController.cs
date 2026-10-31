using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// All animal Start/Update methods run before the first evaluation in LateUpdate.
[DefaultExecutionOrder(1000)]
public sealed class SurvivalStageController : MonoBehaviour
{
    [SerializeField] private SurvivalStageDefinition stage;
    [SerializeField] private SceneNavigation sceneNavigation;
    [SerializeField] private bool unlockNextStageOnFirstGoal = true;
    public SpeciesSurvivalScore Evaluation { get; private set; }
    public IReadOnlyList<SpeciesSurvivalScore> Evaluations => evaluations;
    public event System.Action GoalReached;
    public event System.Action Finished;
    private sealed class GoalRuntime
    {
        public string Name;
        public SpeciesManager Target;
        public SpeciesSurvivalScore Evaluation;
        public float GoalSeconds;
        public bool Notified;
    }
    private readonly List<SpeciesSurvivalScore> evaluations = new List<SpeciesSurvivalScore>();
    private readonly List<GoalRuntime> goals = new List<GoalRuntime>();
    private string stageName;
    private string error;
    private bool finishedNotified;

    private void Start()
    {
        if (stage == null || string.IsNullOrWhiteSpace(stage.stageId))
        { Fail("ステージが未設定です"); return; }
        stageName = stage.displayName;
        if (stage.goals != null && stage.goals.Length > 0)
        {
            foreach (SurvivalGoalDefinition goal in stage.goals)
            {
                if (goal == null || goal.targetSpecies == null || string.IsNullOrWhiteSpace(goal.targetSpecies.speciesId) || goal.goalSeconds <= 0)
                { Fail("目標の対象種または時間が不正です"); return; }
                if (!TryAddGoal(string.IsNullOrWhiteSpace(goal.displayName) ? goal.targetSpecies.displayName : goal.displayName,
                    goal.targetSpecies, goal.pointsPerSecond, goal.goalSeconds)) return;
            }
        }
        else
        {
            if (stage.targetSpecies == null || string.IsNullOrWhiteSpace(stage.targetSpecies.speciesId))
            { Fail("対象種が未設定です"); return; }
            if (!TryAddGoal(stage.targetSpecies.displayName, stage.targetSpecies, stage.pointsPerSecond, stage.goalSeconds)) return;
        }
        Evaluation = evaluations.Count > 0 ? evaluations[0] : null;
    }

    private bool TryAddGoal(string name, SpeciesDefinition species, float pointsPerSecond, float goalSeconds)
    {
        SpeciesManager target = null;
        foreach (SpeciesManager manager in SpeciesManager.Active)
            if (manager != null && manager.isActiveAndEnabled && manager.SpeciesId == species.speciesId)
            { target = manager; break; }
        if (target == null) { Fail($"対象種のマネージャーがありません：{species.displayName}"); return false; }
        SpeciesSurvivalScore evaluation;
        try { evaluation = new SpeciesSurvivalScore(pointsPerSecond, goalSeconds); }
        catch (System.ArgumentOutOfRangeException) { Fail("得点係数または目標時間が不正です"); return false; }
        evaluation.Begin(target.LivingCount);
        if (evaluation.State == SpeciesSurvivalScore.Status.Invalid)
        { Fail($"開始時に対象種がいません：{species.displayName}"); return false; }
        evaluations.Add(evaluation);
        goals.Add(new GoalRuntime { Name = name, Target = target, Evaluation = evaluation, GoalSeconds = goalSeconds });
        return true;
    }

    private void LateUpdate()
    {
        if (goals.Count == 0) return;
        bool anySurviving = false;
        for (int i = 0; i < goals.Count; i++)
        {
            GoalRuntime goal = goals[i];
            if (goal.Evaluation.State == SpeciesSurvivalScore.Status.Surviving)
                goal.Evaluation.Tick(goal.Target != null && goal.Target.isActiveAndEnabled ? goal.Target.LivingCount : 0, Time.deltaTime);
            if (!goal.Notified && goal.Evaluation.GoalReached)
            {
                goal.Notified = true;
                GoalReached?.Invoke();
                if (i == 0 && unlockNextStageOnFirstGoal && sceneNavigation != null)
                    sceneNavigation.UnlockNextStage(SceneManager.GetActiveScene().name);
            }
            anySurviving |= goal.Evaluation.State == SpeciesSurvivalScore.Status.Surviving;
        }
        if (!anySurviving && !finishedNotified)
        {
            finishedNotified = true;
            Finished?.Invoke();
        }
    }

    private void Fail(string message)
    {
        error = message;
        Debug.LogError("Survival stage: " + message, this);
    }

    public string Summary
    {
        get
        {
            if (error != null) return "生存スコア：計測不可（" + error + "）";
            if (goals.Count == 0) return "生存スコア：準備中";
            var result = new StringBuilder(stageName);
            for (int i = 0; i < goals.Count; i++)
            {
                GoalRuntime goal = goals[i];
                string state = goal.Evaluation.State == SpeciesSurvivalScore.Status.Extinct ? "絶滅・確定"
                    : goal.Evaluation.GoalReached ? "達成" : "計測中";
                result.Append($"\n目標{i + 1}：{goal.Name} {goal.GoalSeconds:0.##}秒"
                    + $"　{goal.Evaluation.SurvivalSeconds:0.0}秒（{state}）");
            }
            return result.ToString();
        }
    }
}
