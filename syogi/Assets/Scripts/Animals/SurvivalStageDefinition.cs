using UnityEngine;

[CreateAssetMenu(fileName = "SurvivalStage", menuName = "Aquarium/Survival Stage")]
public sealed class SurvivalStageDefinition : ScriptableObject
{
    public string stageId;
    public string displayName;
    public SpeciesDefinition targetSpecies;
    [Min(0.01f)] public float pointsPerSecond = 1;
    [Tooltip("0なら時間目標なし。達成しても観察と加点を継続する。")]
    [Min(0)] public float goalSeconds;
}
