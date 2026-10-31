using UnityEngine;

[System.Serializable]
public sealed class SurvivalGoalDefinition
{
    public string displayName;
    public SpeciesDefinition targetSpecies;
    [Min(0.01f)] public float pointsPerSecond = 1;
    [Min(0.01f)] public float goalSeconds = 1;
}

[CreateAssetMenu(fileName = "SurvivalStage", menuName = "Aquarium/Survival Stage")]
public sealed class SurvivalStageDefinition : ScriptableObject
{
    public string stageId;
    public string displayName;
    public SpeciesDefinition targetSpecies;
    [Min(0.01f)] public float pointsPerSecond = 1;
    [Tooltip("0なら時間目標なし。達成しても観察と加点を継続する。")]
    [Min(0)] public float goalSeconds;
    [Tooltip("複数目標を使うステージはこちらを設定。空なら上の従来1目標を使用する。")]
    public SurvivalGoalDefinition[] goals = System.Array.Empty<SurvivalGoalDefinition>();
}
