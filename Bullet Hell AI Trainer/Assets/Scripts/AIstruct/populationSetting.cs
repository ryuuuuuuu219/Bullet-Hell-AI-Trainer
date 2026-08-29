using System;
using UnityEngine;

public enum GeneticSaveEvaluationAxis
{
    Damage,
    SurvivalTime,
    EdgeCollisionCumulativeTime,
    CenterDistanceSampledSum,
}

[Serializable]
public sealed class PopulationSettingsData
{
    public int populationSize = 5;
    public int currentGeneration = 1;
    public float mutationRate = 0.01f;
    public bool advanceWhenAllIndividualsAreHit = true;
    public int pendingManualGenerationRequests = 0;

    public GeneticSaveEvaluationAxis geneticSaveEvaluationAxis =
        GeneticSaveEvaluationAxis.SurvivalTime;

    public float damageWeight = 0.3f;
    public float survivalTimeWeight = 1f;
    public float edgeCollisionCumulativeTimeWeight = -5f;
    public float centerDistanceSampledSumWeight = -0.5f;
    public float centerDistanceSampleIntervalSeconds = 0.5f;

    public float CalculateGenerationScore(
        float damage,
        float survivalTime,
        float edgeCollisionCumulativeTime,
        float centerDistanceSampledSum)
    {
        return damage * damageWeight +
               survivalTime * survivalTimeWeight +
               edgeCollisionCumulativeTime * edgeCollisionCumulativeTimeWeight +
               centerDistanceSampledSum * centerDistanceSampledSumWeight;
    }
}

[Serializable]
internal sealed class LegacyPopulationSettingsData
{
    public int populationSize = 5;
    public int currentGeneration = 1;
    public float mutationRate = 0.01f;
    public bool advanceWhenAllIndividualsAreHit = true;
    public int pendingManualGenerationRequests = 0;
    public bool evaluateDps = true;
    public bool evaluateSurvivalTime = true;
    public bool evaluateTimeToScreenEdgeCollision = true;
    public bool evaluateDistanceFromScreenEdge = true;
}

public class populationSetting : MonoBehaviour
{
    public const int MinimumPopulationSize = 1;
    public const int MaximumPopulationSize = 5;

    private const string PlayerPrefsKey = "BulletHellAITrainer.PopulationSettings.v1";
    private const string LegacyPopulationSizeKey = "PopulationSize";

    [Header("世代設定")]
    [Range(MinimumPopulationSize, MaximumPopulationSize)]
    public int populationSize = MaximumPopulationSize;
    [Min(1)] public int currentGeneration = 1;
    [Range(0f, 1f)] public float mutationRate = 0.01f;
    public bool advanceWhenAllIndividualsAreHit = true;
    [Min(0)] public int pendingManualGenerationRequests;

    [Header("評価軸")]
    public GeneticSaveEvaluationAxis geneticSaveEvaluationAxis =
        GeneticSaveEvaluationAxis.SurvivalTime;
    public float damageWeight = 0.3f;
    public float survivalTimeWeight = 1f;
    public float edgeCollisionCumulativeTimeWeight = -5f;
    public float centerDistanceSampledSumWeight = -0.5f;
    [Min(0.01f)] public float centerDistanceSampleIntervalSeconds = 0.5f;

    private void Awake()
    {
        Load();
    }

    public void Save()
    {
        NormalizeFields();
        SaveData(CaptureData());
    }

    public void Load()
    {
        ApplyData(LoadData());
    }

    public void RequestManualGenerationAdvance()
    {
        pendingManualGenerationRequests++;
        Save();
    }

    public bool ConsumeManualGenerationRequest()
    {
        if (pendingManualGenerationRequests <= 0)
        {
            return false;
        }

        pendingManualGenerationRequests--;
        Save();
        return true;
    }

    public void AdvanceGeneration()
    {
        currentGeneration++;
        Save();
    }

    public static PopulationSettingsData LoadData()
    {
        PopulationSettingsData data = new PopulationSettingsData();
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                if (json.Contains("\"evaluateDps\""))
                {
                    LegacyPopulationSettingsData legacy =
                        JsonUtility.FromJson<LegacyPopulationSettingsData>(json);
                    if (legacy != null)
                    {
                        data = ConvertLegacyData(legacy);
                    }
                }
                else
                {
                    PopulationSettingsData loaded =
                        JsonUtility.FromJson<PopulationSettingsData>(json);
                    if (loaded != null)
                    {
                        data = loaded;
                    }
                }
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"Population settings could not be loaded: {exception.Message}");
            }
        }
        else if (PlayerPrefs.HasKey(LegacyPopulationSizeKey))
        {
            data.populationSize = PlayerPrefs.GetInt(
                LegacyPopulationSizeKey,
                MaximumPopulationSize);
        }

        NormalizeData(data);
        return data;
    }

    public static void SaveData(PopulationSettingsData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        NormalizeData(data);
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void DeleteSavedData()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.DeleteKey(LegacyPopulationSizeKey);
        PlayerPrefs.Save();
    }

    private PopulationSettingsData CaptureData()
    {
        return new PopulationSettingsData
        {
            populationSize = populationSize,
            currentGeneration = currentGeneration,
            mutationRate = mutationRate,
            advanceWhenAllIndividualsAreHit = advanceWhenAllIndividualsAreHit,
            pendingManualGenerationRequests = pendingManualGenerationRequests,
            geneticSaveEvaluationAxis = geneticSaveEvaluationAxis,
            damageWeight = damageWeight,
            survivalTimeWeight = survivalTimeWeight,
            edgeCollisionCumulativeTimeWeight = edgeCollisionCumulativeTimeWeight,
            centerDistanceSampledSumWeight = centerDistanceSampledSumWeight,
            centerDistanceSampleIntervalSeconds = centerDistanceSampleIntervalSeconds,
        };
    }

    private void ApplyData(PopulationSettingsData data)
    {
        populationSize = data.populationSize;
        currentGeneration = data.currentGeneration;
        mutationRate = data.mutationRate;
        advanceWhenAllIndividualsAreHit = data.advanceWhenAllIndividualsAreHit;
        pendingManualGenerationRequests = data.pendingManualGenerationRequests;
        geneticSaveEvaluationAxis = data.geneticSaveEvaluationAxis;
        damageWeight = data.damageWeight;
        survivalTimeWeight = data.survivalTimeWeight;
        edgeCollisionCumulativeTimeWeight = data.edgeCollisionCumulativeTimeWeight;
        centerDistanceSampledSumWeight = data.centerDistanceSampledSumWeight;
        centerDistanceSampleIntervalSeconds = data.centerDistanceSampleIntervalSeconds;
    }

    private void NormalizeFields()
    {
        populationSize = Mathf.Clamp(
            populationSize,
            MinimumPopulationSize,
            MaximumPopulationSize);
        currentGeneration = Mathf.Max(1, currentGeneration);
        mutationRate = Mathf.Clamp01(mutationRate);
        pendingManualGenerationRequests = Mathf.Max(0, pendingManualGenerationRequests);
        geneticSaveEvaluationAxis = NormalizeAxis(geneticSaveEvaluationAxis);
        damageWeight = Mathf.Clamp(damageWeight, 0f, 10f);
        survivalTimeWeight = Mathf.Clamp(survivalTimeWeight, 0f, 10f);
        edgeCollisionCumulativeTimeWeight = Mathf.Clamp(
            edgeCollisionCumulativeTimeWeight,
            -10f,
            0f);
        centerDistanceSampledSumWeight = Mathf.Clamp(
            centerDistanceSampledSumWeight,
            -10f,
            0f);
        centerDistanceSampleIntervalSeconds = Mathf.Max(
            0.01f,
            centerDistanceSampleIntervalSeconds);
    }

    private static void NormalizeData(PopulationSettingsData data)
    {
        data.populationSize = Mathf.Clamp(
            data.populationSize,
            MinimumPopulationSize,
            MaximumPopulationSize);
        data.currentGeneration = Mathf.Max(1, data.currentGeneration);
        data.mutationRate = Mathf.Clamp01(data.mutationRate);
        data.pendingManualGenerationRequests = Mathf.Max(0, data.pendingManualGenerationRequests);
        data.geneticSaveEvaluationAxis = NormalizeAxis(data.geneticSaveEvaluationAxis);
        data.damageWeight = Mathf.Clamp(data.damageWeight, 0f, 10f);
        data.survivalTimeWeight = Mathf.Clamp(data.survivalTimeWeight, 0f, 10f);
        data.edgeCollisionCumulativeTimeWeight = Mathf.Clamp(
            data.edgeCollisionCumulativeTimeWeight,
            -10f,
            0f);
        data.centerDistanceSampledSumWeight = Mathf.Clamp(
            data.centerDistanceSampledSumWeight,
            -10f,
            0f);
        data.centerDistanceSampleIntervalSeconds = Mathf.Max(
            0.01f,
            data.centerDistanceSampleIntervalSeconds);
    }

    private static PopulationSettingsData ConvertLegacyData(
        LegacyPopulationSettingsData legacy)
    {
        return new PopulationSettingsData
        {
            populationSize = legacy.populationSize,
            currentGeneration = legacy.currentGeneration,
            mutationRate = legacy.mutationRate,
            advanceWhenAllIndividualsAreHit = legacy.advanceWhenAllIndividualsAreHit,
            pendingManualGenerationRequests = legacy.pendingManualGenerationRequests,
            geneticSaveEvaluationAxis = GeneticSaveEvaluationAxis.SurvivalTime,
            damageWeight = legacy.evaluateDps ? 0.3f : 0f,
            survivalTimeWeight = legacy.evaluateSurvivalTime ? 1f : 0f,
            edgeCollisionCumulativeTimeWeight =
                legacy.evaluateTimeToScreenEdgeCollision ? -5f : 0f,
            centerDistanceSampledSumWeight =
                legacy.evaluateDistanceFromScreenEdge ? -0.5f : 0f,
            centerDistanceSampleIntervalSeconds = 0.5f,
        };
    }

    private static GeneticSaveEvaluationAxis NormalizeAxis(
        GeneticSaveEvaluationAxis axis)
    {
        return Enum.IsDefined(typeof(GeneticSaveEvaluationAxis), axis)
            ? axis
            : GeneticSaveEvaluationAxis.SurvivalTime;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeFields();
    }
#endif
}
