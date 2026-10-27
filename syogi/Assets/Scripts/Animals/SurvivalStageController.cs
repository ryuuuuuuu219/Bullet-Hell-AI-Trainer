using UnityEngine;

// All animal Start/Update methods run before the first evaluation in LateUpdate.
[DefaultExecutionOrder(1000)]
public sealed class SurvivalStageController : MonoBehaviour
{
    [SerializeField] private SurvivalStageDefinition stage;
    public SpeciesSurvivalScore Evaluation { get; private set; }
    public event System.Action GoalReached;
    public event System.Action Finished;
    private SpeciesManager target;
    private string stageName;
    private string speciesName;
    private float goalSeconds;
    private string error;

    private void Start()
    {
        if (stage == null || string.IsNullOrWhiteSpace(stage.stageId) || stage.targetSpecies == null ||
            string.IsNullOrWhiteSpace(stage.targetSpecies.speciesId))
        { Fail("ステージまたは対象種が未設定です"); return; }
        stageName = stage.displayName;
        speciesName = stage.targetSpecies.displayName;
        goalSeconds = stage.goalSeconds;
        foreach (var manager in SpeciesManager.Active)
            if (manager != null && manager.isActiveAndEnabled && manager.SpeciesId == stage.targetSpecies.speciesId)
                target = manager;
        if (target == null) { Fail("対象種のマネージャーがありません"); return; }
        try { Evaluation = new SpeciesSurvivalScore(stage.pointsPerSecond, goalSeconds); }
        catch (System.ArgumentOutOfRangeException) { Fail("得点係数または目標時間が不正です"); return; }
        Evaluation.Begin(target.LivingCount);
        if (Evaluation.State == SpeciesSurvivalScore.Status.Invalid) Fail("開始時に対象種がいません");
    }

    private void LateUpdate()
    {
        if (Evaluation == null || Evaluation.State != SpeciesSurvivalScore.Status.Surviving) return;
        bool achieved = Evaluation.GoalReached;
        Evaluation.Tick(target != null && target.isActiveAndEnabled ? target.LivingCount : 0, Time.deltaTime);
        if (!achieved && Evaluation.GoalReached) GoalReached?.Invoke();
        if (Evaluation.State == SpeciesSurvivalScore.Status.Extinct) Finished?.Invoke();
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
            if (Evaluation == null) return "生存スコア：準備中";
            string state = Evaluation.State == SpeciesSurvivalScore.Status.Extinct ? "絶滅・確定" : "計測中";
            string goal = goalSeconds > 0 ? $"\n目標：{goalSeconds:0.##}秒（{(Evaluation.GoalReached ? "達成" : "未達成")}）" : "";
            return $"{stageName} / {speciesName}\n存続：{Evaluation.SurvivalSeconds:0.0}秒　スコア：{Evaluation.Score:0}（{state}）{goal}";
        }
    }
}
