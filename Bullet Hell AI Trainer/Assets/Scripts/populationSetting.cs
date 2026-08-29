using System;
using UnityEngine;

[Serializable]
public sealed class PopulationSettingsData
{
    public int populationSize = 50;
    public int currentGeneration = 1;
    public float mutationRate = 0.01f;
    public bool advanceWhenAllIndividualsAreHit = true;
    public int pendingManualGenerationRequests;

    public bool evaluateDps = true;
    public bool evaluateSurvivalTime = true;
    public bool evaluateTimeToScreenEdgeCollision = true;
    public bool evaluateDistanceFromScreenEdge = true;
}

public class populationSetting : MonoBehaviour
{
    public const int MinimumPopulationSize = 1;
    public const int MaximumPopulationSize = 50;

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
    public bool evaluateDps = true;
    public bool evaluateSurvivalTime = true;
    public bool evaluateTimeToScreenEdgeCollision = true;
    public bool evaluateDistanceFromScreenEdge = true;

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
                PopulationSettingsData loaded = JsonUtility.FromJson<PopulationSettingsData>(json);
                if (loaded != null)
                {
                    data = loaded;
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
            evaluateDps = evaluateDps,
            evaluateSurvivalTime = evaluateSurvivalTime,
            evaluateTimeToScreenEdgeCollision = evaluateTimeToScreenEdgeCollision,
            evaluateDistanceFromScreenEdge = evaluateDistanceFromScreenEdge,
        };
    }

    private void ApplyData(PopulationSettingsData data)
    {
        populationSize = data.populationSize;
        currentGeneration = data.currentGeneration;
        mutationRate = data.mutationRate;
        advanceWhenAllIndividualsAreHit = data.advanceWhenAllIndividualsAreHit;
        pendingManualGenerationRequests = data.pendingManualGenerationRequests;
        evaluateDps = data.evaluateDps;
        evaluateSurvivalTime = data.evaluateSurvivalTime;
        evaluateTimeToScreenEdgeCollision = data.evaluateTimeToScreenEdgeCollision;
        evaluateDistanceFromScreenEdge = data.evaluateDistanceFromScreenEdge;
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
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeFields();
    }
#endif
}
