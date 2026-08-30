using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class AiSaveData
{
    public bool useProximityInput = true;
    public bool useAttentionInput = true;
    public bool useCircularSensorInput = true;
    public bool useWarningLineInput = true;
    public List<CircularSensorData> circularSensors = new List<CircularSensorData>();

    public int inputNodeCount = Aidata.NeuralInputNodeCount;
    public int layer1NodeCount = Aidata.Layer1NodeCount;
    public int layer2NodeCount = Aidata.Layer2NodeCount;
    public int outputNodeCount = 2;

    public float[] inputToLayer1Weights = Array.Empty<float>();
    public float[] layer1Biases = Array.Empty<float>();
    public float[] layer1ToLayer2Weights = Array.Empty<float>();
    public float[] layer2Biases = Array.Empty<float>();
    public float[] layer2ToOutputWeights = Array.Empty<float>();
    public float[] outputBiases = Array.Empty<float>();
}

[Serializable]
public struct HistorySample
{
    public Vector2 position;
    public Vector2 positionDelta;
    public float proximityDistance;
    public float laserDistance;
}

[Serializable]
public sealed class TeacherSample
{
    public float[] inputs = Array.Empty<float>();
    public Vector2 targetMovement;
}

public class Aidata : MonoBehaviour
{
    public const int MovementOutputNodeCount = 2;
    public const int Layer1NodeCount = 16;
    public const int Layer2NodeCount = 12;
    public const int ProximityFeatureCount = 6;
    public const int AttentionFeatureCount = 7;
    public const int CircularSensorSlotCount = 10;
    public const int CircularFeatureCount = CircularSensorSlotCount + 1;
    public const int LaserFeatureCount = 10;
    public const int ExistingInputNodeCount =
        ProximityFeatureCount +
        AttentionSensor.AttentionCapacity * AttentionFeatureCount +
        CircularFeatureCount +
        WarningLineSensor.DetectionCapacity * LaserFeatureCount;
    public const int ThreatTimeInputIndex = ExistingInputNodeCount;
    public const int CurrentHistoryInputIndex = ThreatTimeInputIndex + 1;
    public const int PreviousHistoryInputIndex = CurrentHistoryInputIndex + 6;
    public const int NeuralInputNodeCount = PreviousHistoryInputIndex + 6;
    public const int FinalInputIndex = NeuralInputNodeCount - 1;

    private const float HistorySampleInterval = 0.2f;
    private const int MaximumTeacherSampleCount = 2048;
    private const int TeacherSamplesPerSave = 50;

    private const string PlayerPrefsKey = "BulletHellAITrainer.AiData.v1";

    private float[] runtimeInputs = Array.Empty<float>();
    private float[] runtimeLayer1 = Array.Empty<float>();
    private float[] runtimeLayer2 = Array.Empty<float>();
    private readonly AttentionObservation[] runtimeAttention =
        new AttentionObservation[AttentionSensor.AttentionCapacity];
    private readonly LaserSensorObservation[] runtimeLaserThreats =
        new LaserSensorObservation[WarningLineSensor.DetectionCapacity];
    private readonly int[] runtimeCircularCounts =
        new int[CircularSensorSlotCount];
    private readonly List<TeacherSample> teacherSamples =
        new List<TeacherSample>();
    private HistorySample currentSample;
    private HistorySample previousSample;
    private Vector2 lastHistoryWorldPosition;
    private float historySampleElapsedTime;
    private bool historyInitialized;
    private float nextTeacherSampleTime;
    private float nextStationaryTeacherSampleTime;
    private int teacherSamplesSinceSave;

    [Header("入力")]
    public bool useProximityInput = true;
    public bool useAttentionInput = true;
    public bool useCircularSensorInput = true;
    public bool useWarningLineInput = true;
    public List<CircularSensor> sensors = new List<CircularSensor>();

    [Header("ニューラルネットワーク構造")]
    [Min(1)] public int inputNodeCount = NeuralInputNodeCount;
    [FormerlySerializedAs("layer1nodes")]
    [Min(1)] public int layer1NodeCount = Layer1NodeCount;
    [FormerlySerializedAs("layer2nodes")]
    [Min(1)] public int layer2NodeCount = Layer2NodeCount;
    [Min(1)] public int outputNodeCount = 2;

    [Header("ニューラルネットワーク係数")]
    public float[] inputToLayer1Weights = Array.Empty<float>();
    public float[] layer1Biases = Array.Empty<float>();
    public float[] layer1ToLayer2Weights = Array.Empty<float>();
    public float[] layer2Biases = Array.Empty<float>();
    public float[] layer2ToOutputWeights = Array.Empty<float>();
    public float[] outputBiases = Array.Empty<float>();

    [Header("教師学習")]
    [SerializeField, Min(0.000001f)] private float teacherLearningRate = 0.01f;
    [SerializeField, Min(0.01f)] private float teacherSampleInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float stationarySampleInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float gradientClamp = 1f;

    [Header("NNデバッグ（実行時）")]
    [SerializeField] private float debugThreatTimeSignal = -1f;
    [SerializeField] private HistorySample debugCurrentSample;
    [SerializeField] private HistorySample debugPreviousSample;
    [SerializeField] private Vector2 debugManualInput;
    [SerializeField] private Vector2 debugPrediction;
    [SerializeField] private float debugTeacherLoss;
    [SerializeField] private int debugTeacherSampleCount;

    private void Awake()
    {
        Debug.Assert(ExistingInputNodeCount == 72);
        Debug.Assert(NeuralInputNodeCount == 85 && FinalInputIndex == 84);
        Load();
    }

    public void Save()
    {
        EnsureNeuralNetworkShape();
        SaveData(CaptureData());
    }

    public void Load()
    {
        ApplyData(LoadData());
    }

    public static AiSaveData LoadData()
    {
        AiSaveData data = new AiSaveData();
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                AiSaveData loaded = JsonUtility.FromJson<AiSaveData>(json);
                if (loaded != null)
                {
                    data = loaded;
                }
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"AI data could not be loaded: {exception.Message}");
            }
        }

        NormalizeData(data);
        return data;
    }

    public static void SaveData(AiSaveData data)
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
        PlayerPrefs.Save();
    }

    public void EnsureNeuralNetworkShape()
    {
        bool canMigrateExistingInputs =
            inputNodeCount == ExistingInputNodeCount &&
            layer1NodeCount == Layer1NodeCount &&
            layer2NodeCount == Layer2NodeCount &&
            outputNodeCount == MovementOutputNodeCount;
        bool networkShapeChanged =
            inputNodeCount != NeuralInputNodeCount ||
            layer1NodeCount != Layer1NodeCount ||
            layer2NodeCount != Layer2NodeCount ||
            outputNodeCount != MovementOutputNodeCount;
        inputNodeCount = NeuralInputNodeCount;
        layer1NodeCount = Layer1NodeCount;
        layer2NodeCount = Layer2NodeCount;
        outputNodeCount = MovementOutputNodeCount;

        if (networkShapeChanged && !canMigrateExistingInputs)
        {
            ClearNetworkCoefficients(
                ref inputToLayer1Weights,
                ref layer1Biases,
                ref layer1ToLayer2Weights,
                ref layer2Biases,
                ref layer2ToOutputWeights,
                ref outputBiases);
        }

        ResizePreserving(ref inputToLayer1Weights, inputNodeCount * layer1NodeCount);
        ResizePreserving(ref layer1Biases, layer1NodeCount);
        ResizePreserving(ref layer1ToLayer2Weights, layer1NodeCount * layer2NodeCount);
        ResizePreserving(ref layer2Biases, layer2NodeCount);
        ResizePreserving(ref layer2ToOutputWeights, layer2NodeCount * outputNodeCount);
        ResizePreserving(ref outputBiases, outputNodeCount);
    }

    public void RandomizeNetwork(int seed)
    {
        EnsureNeuralNetworkShape();

        System.Random random = new System.Random(seed);
        RandomizeWeights(
            inputToLayer1Weights,
            inputNodeCount,
            layer1NodeCount,
            random);
        RandomizeWeights(
            layer1ToLayer2Weights,
            layer1NodeCount,
            layer2NodeCount,
            random);
        RandomizeWeights(
            layer2ToOutputWeights,
            layer2NodeCount,
            MovementOutputNodeCount,
            random);

        Array.Clear(layer1Biases, 0, layer1Biases.Length);
        Array.Clear(layer2Biases, 0, layer2Biases.Length);
        Array.Clear(outputBiases, 0, outputBiases.Length);
    }

    public AiSaveData CreateSnapshot()
    {
        EnsureNeuralNetworkShape();
        return CloneData(CaptureData());
    }

    public void ApplySnapshot(AiSaveData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        ApplyData(CloneData(data));
    }

    public static AiSaveData CloneData(AiSaveData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        AiSaveData clone = new AiSaveData
        {
            useProximityInput = source.useProximityInput,
            useAttentionInput = source.useAttentionInput,
            useCircularSensorInput = source.useCircularSensorInput,
            useWarningLineInput = source.useWarningLineInput,
            inputNodeCount = source.inputNodeCount,
            layer1NodeCount = source.layer1NodeCount,
            layer2NodeCount = source.layer2NodeCount,
            outputNodeCount = source.outputNodeCount,
            inputToLayer1Weights = CloneArray(source.inputToLayer1Weights),
            layer1Biases = CloneArray(source.layer1Biases),
            layer1ToLayer2Weights = CloneArray(source.layer1ToLayer2Weights),
            layer2Biases = CloneArray(source.layer2Biases),
            layer2ToOutputWeights = CloneArray(source.layer2ToOutputWeights),
            outputBiases = CloneArray(source.outputBiases),
        };

        if (source.circularSensors != null)
        {
            foreach (CircularSensorData sensor in source.circularSensors)
            {
                if (sensor == null)
                {
                    continue;
                }

                clone.circularSensors.Add(new CircularSensorData
                {
                    innerRadius = sensor.innerRadius,
                    radius = sensor.radius,
                    angle = sensor.angle,
                    centerAngle = sensor.centerAngle,
                    priority = sensor.priority,
                    arcSegments = sensor.arcSegments,
                });
            }
        }

        NormalizeData(clone);
        return clone;
    }

    public static bool HasTrainableNetwork(AiSaveData data)
    {
        return data != null &&
               (HasNonZeroValue(data.inputToLayer1Weights) ||
                HasNonZeroValue(data.layer1ToLayer2Weights) ||
                HasNonZeroValue(data.layer2ToOutputWeights));
    }

    private AiSaveData CaptureData()
    {
        AiSaveData data = new AiSaveData
        {
            useProximityInput = useProximityInput,
            useAttentionInput = useAttentionInput,
            useCircularSensorInput = useCircularSensorInput,
            useWarningLineInput = useWarningLineInput,
            inputNodeCount = inputNodeCount,
            layer1NodeCount = layer1NodeCount,
            layer2NodeCount = layer2NodeCount,
            outputNodeCount = outputNodeCount,
            inputToLayer1Weights = inputToLayer1Weights,
            layer1Biases = layer1Biases,
            layer1ToLayer2Weights = layer1ToLayer2Weights,
            layer2Biases = layer2Biases,
            layer2ToOutputWeights = layer2ToOutputWeights,
            outputBiases = outputBiases,
        };

        foreach (CircularSensor sensor in sensors)
        {
            if (sensor == null)
            {
                continue;
            }

            data.circularSensors.Add(new CircularSensorData
            {
                innerRadius = sensor.innerRadius,
                radius = sensor.radius,
                angle = sensor.angle,
                centerAngle = sensor.centerAngle,
                priority = sensor.priority,
                arcSegments = sensor.arcSegments,
            });
        }

        return data;
    }

    private void ApplyData(AiSaveData data)
    {
        useProximityInput = data.useProximityInput;
        useAttentionInput = data.useAttentionInput;
        useCircularSensorInput = data.useCircularSensorInput;
        useWarningLineInput = data.useWarningLineInput;
        inputNodeCount = data.inputNodeCount;
        layer1NodeCount = data.layer1NodeCount;
        layer2NodeCount = data.layer2NodeCount;
        outputNodeCount = data.outputNodeCount;
        inputToLayer1Weights = data.inputToLayer1Weights;
        layer1Biases = data.layer1Biases;
        layer1ToLayer2Weights = data.layer1ToLayer2Weights;
        layer2Biases = data.layer2Biases;
        layer2ToOutputWeights = data.layer2ToOutputWeights;
        outputBiases = data.outputBiases;

        ApplySensorData(data.circularSensors);
        EnsureNeuralNetworkShape();
    }

    private void ApplySensorData(List<CircularSensorData> savedSensors)
    {
        sensors.RemoveAll(sensor => sensor == null);

        for (int index = sensors.Count - 1; index >= savedSensors.Count; index--)
        {
            CircularSensor sensor = sensors[index];
            sensors.RemoveAt(index);
            if (sensor != null)
            {
                Destroy(sensor.gameObject);
            }
        }

        for (int index = sensors.Count; index < savedSensors.Count; index++)
        {
            GameObject sensorObject = new GameObject($"Circular Sensor {index + 1}");
            sensorObject.transform.SetParent(transform, false);
            sensors.Add(sensorObject.AddComponent<CircularSensor>());
        }

        for (int index = 0; index < savedSensors.Count; index++)
        {
            CircularSensorData source = savedSensors[index];
            CircularSensor target = sensors[index];
            target.innerRadius = source.innerRadius;
            target.radius = source.radius;
            target.angle = source.angle;
            target.centerAngle = source.centerAngle;
            target.priority = source.priority;
            target.arcSegments = source.arcSegments;
            target.RebuildFanCollider();
        }
    }

    private static void NormalizeData(AiSaveData data)
    {
        data.circularSensors ??= new List<CircularSensorData>();
        bool canMigrateExistingInputs =
            data.inputNodeCount == ExistingInputNodeCount &&
            data.layer1NodeCount == Layer1NodeCount &&
            data.layer2NodeCount == Layer2NodeCount &&
            data.outputNodeCount == MovementOutputNodeCount;
        bool networkShapeChanged =
            data.inputNodeCount != NeuralInputNodeCount ||
            data.layer1NodeCount != Layer1NodeCount ||
            data.layer2NodeCount != Layer2NodeCount ||
            data.outputNodeCount != MovementOutputNodeCount;
        if (data.circularSensors.Count != CircularSensorSlotCount)
        {
            data.circularSensors = CircularSensor.CreateDefaultData();
        }

        foreach (CircularSensorData sensor in data.circularSensors)
        {
            sensor.radius = Mathf.Max(0.01f, sensor.radius);
            sensor.innerRadius = Mathf.Clamp(sensor.innerRadius, 0f, sensor.radius);
            sensor.angle = Mathf.Clamp(sensor.angle, 1f, 359f);
            sensor.arcSegments = sensor.arcSegments <= 0
                ? 16
                : Mathf.Clamp(sensor.arcSegments, 3, 64);
        }
        data.inputNodeCount = NeuralInputNodeCount;
        data.layer1NodeCount = Layer1NodeCount;
        data.layer2NodeCount = Layer2NodeCount;
        data.outputNodeCount = MovementOutputNodeCount;

        if (networkShapeChanged && !canMigrateExistingInputs)
        {
            data.useAttentionInput = true;
            ClearNetworkCoefficients(
                ref data.inputToLayer1Weights,
                ref data.layer1Biases,
                ref data.layer1ToLayer2Weights,
                ref data.layer2Biases,
                ref data.layer2ToOutputWeights,
                ref data.outputBiases);
        }

        ResizePreserving(
            ref data.inputToLayer1Weights,
            data.inputNodeCount * data.layer1NodeCount);
        ResizePreserving(ref data.layer1Biases, data.layer1NodeCount);
        ResizePreserving(
            ref data.layer1ToLayer2Weights,
            data.layer1NodeCount * data.layer2NodeCount);
        ResizePreserving(ref data.layer2Biases, data.layer2NodeCount);
        ResizePreserving(
            ref data.layer2ToOutputWeights,
            data.layer2NodeCount * data.outputNodeCount);
        ResizePreserving(ref data.outputBiases, data.outputNodeCount);
    }

    private static void ClearNetworkCoefficients(
        ref float[] inputWeights,
        ref float[] firstBiases,
        ref float[] hiddenWeights,
        ref float[] secondBiases,
        ref float[] outputWeights,
        ref float[] finalBiases)
    {
        inputWeights = Array.Empty<float>();
        firstBiases = Array.Empty<float>();
        hiddenWeights = Array.Empty<float>();
        secondBiases = Array.Empty<float>();
        outputWeights = Array.Empty<float>();
        finalBiases = Array.Empty<float>();
    }

    private static void ResizePreserving(ref float[] values, int length)
    {
        values ??= Array.Empty<float>();
        if (values.Length != length)
        {
            Array.Resize(ref values, length);
        }
    }

    private static float[] CloneArray(float[] values)
    {
        return values != null ? (float[])values.Clone() : Array.Empty<float>();
    }

    private static bool HasNonZeroValue(float[] values)
    {
        if (values == null)
        {
            return false;
        }

        foreach (float value in values)
        {
            if (!Mathf.Approximately(value, 0f))
            {
                return true;
            }
        }

        return false;
    }

    private static void RandomizeWeights(
        float[] weights,
        int inputCount,
        int outputCount,
        System.Random random)
    {
        float limit = Mathf.Sqrt(6f / (inputCount + outputCount));
        for (int index = 0; index < weights.Length; index++)
        {
            float normalized = (float)random.NextDouble() * 2f - 1f;
            weights[index] = normalized * limit;
        }
    }

    public Vector2 output()
    {
        EnsureNeuralNetworkShape();
        BuildRuntimeInputs();

        debugPrediction = Forward(runtimeInputs);
        return debugPrediction;
    }

    public void RecordTeacherSample(Vector2 targetMovement)
    {
        targetMovement = Vector2.ClampMagnitude(targetMovement, 1f);
        debugManualInput = targetMovement;

        bool stationary = targetMovement.sqrMagnitude <= Mathf.Epsilon;
        float allowedTime = stationary
            ? nextStationaryTeacherSampleTime
            : nextTeacherSampleTime;
        if (Time.time < allowedTime)
        {
            return;
        }

        TeacherSample sample = new TeacherSample
        {
            inputs = (float[])runtimeInputs.Clone(),
            targetMovement = targetMovement,
        };
        if (teacherSamples.Count >= MaximumTeacherSampleCount)
        {
            teacherSamples.RemoveAt(0);
        }

        teacherSamples.Add(sample);
        debugTeacherSampleCount = teacherSamples.Count;
        nextTeacherSampleTime = Time.time + teacherSampleInterval;
        if (stationary)
        {
            nextStationaryTeacherSampleTime = Time.time + stationarySampleInterval;
        }

        debugTeacherLoss = TrainOnSample(sample);
        teacherSamplesSinceSave++;
        if (teacherSamplesSinceSave >= TeacherSamplesPerSave)
        {
            Save();
            teacherSamplesSinceSave = 0;
        }
    }

    public float TrainOnSample(TeacherSample sample)
    {
        if (sample == null ||
            sample.inputs == null ||
            sample.inputs.Length != NeuralInputNodeCount)
        {
            return float.NaN;
        }

        Vector2 prediction = Forward(sample.inputs);
        Vector2 error = prediction - sample.targetMovement;
        float loss = error.sqrMagnitude;

        float[] outputDelta = new float[MovementOutputNodeCount];
        float[] layer2Delta = new float[layer2NodeCount];
        float[] layer1Delta = new float[layer1NodeCount];
        outputDelta[0] = SafeGradient(
            2f * error.x * (1f - prediction.x * prediction.x));
        outputDelta[1] = SafeGradient(
            2f * error.y * (1f - prediction.y * prediction.y));

        for (int source = 0; source < layer2NodeCount; source++)
        {
            float propagated = 0f;
            for (int destination = 0;
                 destination < MovementOutputNodeCount;
                 destination++)
            {
                int weightIndex = source * MovementOutputNodeCount + destination;
                propagated += outputDelta[destination] *
                    layer2ToOutputWeights[weightIndex];
            }

            layer2Delta[source] = SafeGradient(
                propagated *
                (1f - runtimeLayer2[source] * runtimeLayer2[source]));
        }

        for (int source = 0; source < layer1NodeCount; source++)
        {
            float propagated = 0f;
            for (int destination = 0; destination < layer2NodeCount; destination++)
            {
                int weightIndex = source * layer2NodeCount + destination;
                propagated += layer2Delta[destination] *
                    layer1ToLayer2Weights[weightIndex];
            }

            layer1Delta[source] = SafeGradient(
                propagated *
                (1f - runtimeLayer1[source] * runtimeLayer1[source]));
        }

        ApplyGradients(sample.inputs, outputDelta, layer2Delta, layer1Delta);
        return loss;
    }

    private Vector2 Forward(float[] inputs)
    {
        EnsureNeuralNetworkShape();

        ResizePreserving(ref runtimeLayer1, layer1NodeCount);
        ResizePreserving(ref runtimeLayer2, layer2NodeCount);

        for (int destination = 0; destination < layer1NodeCount; destination++)
        {
            float sum = layer1Biases[destination];
            for (int source = 0; source < inputNodeCount; source++)
            {
                int weightIndex = source * layer1NodeCount + destination;
                sum += inputs[source] * inputToLayer1Weights[weightIndex];
            }

            runtimeLayer1[destination] = Activate(sum);
        }

        for (int destination = 0; destination < layer2NodeCount; destination++)
        {
            float sum = layer2Biases[destination];
            for (int source = 0; source < layer1NodeCount; source++)
            {
                int weightIndex = source * layer2NodeCount + destination;
                sum += runtimeLayer1[source] * layer1ToLayer2Weights[weightIndex];
            }

            runtimeLayer2[destination] = Activate(sum);
        }

        return new Vector2(CalculateOutputNode(0), CalculateOutputNode(1));
    }

    private void ApplyGradients(
        float[] inputs,
        float[] outputDelta,
        float[] layer2Delta,
        float[] layer1Delta)
    {
        float learningRate = Mathf.Max(0.000001f, teacherLearningRate);

        for (int source = 0; source < layer2NodeCount; source++)
        {
            for (int destination = 0;
                 destination < MovementOutputNodeCount;
                 destination++)
            {
                int index = source * MovementOutputNodeCount + destination;
                ApplyParameterUpdate(
                    layer2ToOutputWeights,
                    index,
                    runtimeLayer2[source] * outputDelta[destination],
                    learningRate);
            }
        }

        for (int destination = 0;
             destination < MovementOutputNodeCount;
             destination++)
        {
            ApplyParameterUpdate(
                outputBiases,
                destination,
                outputDelta[destination],
                learningRate);
        }

        for (int source = 0; source < layer1NodeCount; source++)
        {
            for (int destination = 0; destination < layer2NodeCount; destination++)
            {
                int index = source * layer2NodeCount + destination;
                ApplyParameterUpdate(
                    layer1ToLayer2Weights,
                    index,
                    runtimeLayer1[source] * layer2Delta[destination],
                    learningRate);
            }
        }

        for (int destination = 0; destination < layer2NodeCount; destination++)
        {
            ApplyParameterUpdate(
                layer2Biases,
                destination,
                layer2Delta[destination],
                learningRate);
        }

        for (int source = 0; source < inputNodeCount; source++)
        {
            for (int destination = 0; destination < layer1NodeCount; destination++)
            {
                int index = source * layer1NodeCount + destination;
                ApplyParameterUpdate(
                    inputToLayer1Weights,
                    index,
                    inputs[source] * layer1Delta[destination],
                    learningRate);
            }
        }

        for (int destination = 0; destination < layer1NodeCount; destination++)
        {
            ApplyParameterUpdate(
                layer1Biases,
                destination,
                layer1Delta[destination],
                learningRate);
        }
    }

    private void ApplyParameterUpdate(
        float[] parameters,
        int index,
        float gradient,
        float learningRate)
    {
        float current = parameters[index];
        if (float.IsNaN(current) || float.IsInfinity(current))
        {
            current = 0f;
        }

        float next = current - learningRate * SafeGradient(gradient);
        parameters[index] = float.IsNaN(next) || float.IsInfinity(next)
            ? current
            : next;
    }

    private float SafeGradient(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0f;
        }

        float limit = Mathf.Max(0.01f, gradientClamp);
        return Mathf.Clamp(value, -limit, limit);
    }

    private void BuildRuntimeInputs()
    {
        ResizePreserving(ref runtimeInputs, inputNodeCount);
        Array.Clear(runtimeInputs, 0, runtimeInputs.Length);

        int inputIndex = 0;
        int logicalLayer = TryGetComponent(out PlayerAgent player)
            ? player.LogicalLayer
            : -1;
        Vector2 playerVelocity = TryGetComponent(out Rigidbody2D body)
            ? body.linearVelocity
            : Vector2.zero;

        WriteProximityInputs(ref inputIndex, logicalLayer);
        WriteAttentionInputs(ref inputIndex, logicalLayer, playerVelocity);
        WriteCircularInputs(ref inputIndex);
        WriteLaserInputs(ref inputIndex, logicalLayer);
        UpdateHistorySamples(logicalLayer);

        debugThreatTimeSignal = StageSpawnManager.CurrentThreatTimeSignal;
        WriteInput(ref inputIndex, debugThreatTimeSignal);
        WriteHistorySample(ref inputIndex, currentSample);
        WriteHistorySample(ref inputIndex, previousSample);
        debugCurrentSample = currentSample;
        debugPreviousSample = previousSample;

        Debug.Assert(
            inputIndex == NeuralInputNodeCount,
            $"AI input schema wrote {inputIndex} values; expected " +
            $"{NeuralInputNodeCount}.");
    }

    private void UpdateHistorySamples(int logicalLayer)
    {
        if (!historyInitialized)
        {
            Vector2 worldPosition = transform.position;
            currentSample = CaptureHistorySample(logicalLayer, worldPosition);
            currentSample.positionDelta = Vector2.zero;
            previousSample = currentSample;
            lastHistoryWorldPosition = worldPosition;
            historySampleElapsedTime = 0f;
            historyInitialized = true;
            return;
        }

        historySampleElapsedTime += Time.fixedDeltaTime;
        if (historySampleElapsedTime < HistorySampleInterval)
        {
            return;
        }

        historySampleElapsedTime %= HistorySampleInterval;
        Vector2 currentWorldPosition = transform.position;
        HistorySample newSample = CaptureHistorySample(
            logicalLayer,
            currentWorldPosition);
        float moveSpeed = TryGetComponent(out PlayerMovementController movement)
            ? movement.MoveSpeed
            : 0f;
        float maximumDistance = moveSpeed * HistorySampleInterval;
        newSample.positionDelta = maximumDistance > Mathf.Epsilon
            ? new Vector2(
                Mathf.Clamp(
                    (currentWorldPosition.x - lastHistoryWorldPosition.x) /
                    maximumDistance,
                    -1f,
                    1f),
                Mathf.Clamp(
                    (currentWorldPosition.y - lastHistoryWorldPosition.y) /
                    maximumDistance,
                    -1f,
                    1f))
            : Vector2.zero;

        previousSample = currentSample;
        currentSample = newSample;
        lastHistoryWorldPosition = currentWorldPosition;
    }

    private HistorySample CaptureHistorySample(
        int logicalLayer,
        Vector2 worldPosition)
    {
        Camera camera = Camera.main;
        Vector2 normalizedPosition = Vector2.zero;
        if (camera != null)
        {
            Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
            normalizedPosition = new Vector2(
                Mathf.Clamp(viewport.x * 2f - 1f, -1f, 1f),
                Mathf.Clamp(viewport.y * 2f - 1f, -1f, 1f));
        }

        ProximityObservation proximity = useProximityInput
            ? ProximitySensor.Observe(worldPosition, logicalLayer)
            : default;
        float proximityDistance = proximity.isValid
            ? proximity.normalizedDistance
            : 1f;

        Array.Clear(runtimeLaserThreats, 0, runtimeLaserThreats.Length);
        if (useWarningLineInput)
        {
            WarningLineSensor.SelectActiveThreats(
                worldPosition,
                logicalLayer,
                runtimeLaserThreats);
        }

        float laserDistance = 1f;
        foreach (LaserSensorObservation laser in runtimeLaserThreats)
        {
            if (laser.isValid)
            {
                laserDistance = Mathf.Min(
                    laserDistance,
                    Mathf.Clamp01(laser.surfaceDistance));
            }
        }

        return new HistorySample
        {
            position = normalizedPosition,
            positionDelta = Vector2.zero,
            proximityDistance = proximityDistance,
            laserDistance = laserDistance,
        };
    }

    private void WriteHistorySample(ref int inputIndex, HistorySample sample)
    {
        WriteInput(ref inputIndex, sample.position.x);
        WriteInput(ref inputIndex, sample.position.y);
        WriteInput(ref inputIndex, sample.positionDelta.x);
        WriteInput(ref inputIndex, sample.positionDelta.y);
        WriteInput(ref inputIndex, sample.proximityDistance);
        WriteInput(ref inputIndex, sample.laserDistance);
    }

    private void WriteProximityInputs(ref int inputIndex, int logicalLayer)
    {
        ProximityObservation observation = useProximityInput
            ? ProximitySensor.Observe(transform.position, logicalLayer)
            : default;

        WriteInput(ref inputIndex, observation.isValid ? 1f : 0f);
        WriteInput(ref inputIndex, observation.relativePosition.x);
        WriteInput(ref inputIndex, observation.relativePosition.y);
        WriteInput(ref inputIndex, observation.normalizedDistance);
        WriteInput(ref inputIndex, observation.approachDot);
        WriteInput(ref inputIndex, observation.normalizedThreat);
    }

    private void WriteAttentionInputs(
        ref int inputIndex,
        int logicalLayer,
        Vector2 playerVelocity)
    {
        Array.Clear(runtimeAttention, 0, runtimeAttention.Length);
        if (useAttentionInput)
        {
            AttentionSensor.Select(
                transform.position,
                playerVelocity,
                logicalLayer,
                runtimeAttention);
        }

        foreach (AttentionObservation observation in runtimeAttention)
        {
            WriteInput(ref inputIndex, observation.isValid ? 1f : 0f);
            WriteInput(ref inputIndex, observation.relativePosition.x);
            WriteInput(ref inputIndex, observation.relativePosition.y);
            WriteInput(ref inputIndex, observation.normalizedDistance);
            WriteInput(ref inputIndex, observation.normalizedClosestDistance);
            WriteInput(ref inputIndex, observation.normalizedClosestTime);
            WriteInput(ref inputIndex, observation.normalizedThreat);
        }
    }

    private void WriteCircularInputs(ref int inputIndex)
    {
        Array.Clear(runtimeCircularCounts, 0, runtimeCircularCounts.Length);
        int detectedBulletCount = 0;
        float weightedDetectedBulletCount = 0f;

        for (int index = 0; index < sensors.Count; index++)
        {
            CircularSensor sensor = sensors[index];
            if (sensor == null)
            {
                continue;
            }

            int count = sensor.Sense();
            if (index >= runtimeCircularCounts.Length)
            {
                continue;
            }

            runtimeCircularCounts[index] = count;
            detectedBulletCount += count;
            weightedDetectedBulletCount +=
                count * Mathf.Max(0f, sensor.priority);
        }

        for (int index = 0; index < runtimeCircularCounts.Length; index++)
        {
            float value = 0f;
            if (useCircularSensorInput && weightedDetectedBulletCount > 0f)
            {
                float priority = index < sensors.Count && sensors[index] != null
                    ? Mathf.Max(0f, sensors[index].priority)
                    : 0f;
                value = runtimeCircularCounts[index] * priority /
                        weightedDetectedBulletCount;
            }

            WriteInput(ref inputIndex, value);
        }

        WriteInput(
            ref inputIndex,
            useCircularSensorInput
                ? detectedBulletCount / (detectedBulletCount + 1f)
                : 0f);
    }

    private void WriteLaserInputs(ref int inputIndex, int logicalLayer)
    {
        Array.Clear(runtimeLaserThreats, 0, runtimeLaserThreats.Length);
        if (useWarningLineInput)
        {
            WarningLineSensor.SelectActiveThreats(
                transform.position,
                logicalLayer,
                runtimeLaserThreats);
        }

        foreach (LaserSensorObservation observation in runtimeLaserThreats)
        {
            WriteInput(ref inputIndex, observation.isValid ? 1f : 0f);
            WriteInput(ref inputIndex, observation.isActive ? 1f : 0f);
            WriteInput(ref inputIndex, observation.relativeOrigin.x);
            WriteInput(ref inputIndex, observation.relativeOrigin.y);
            WriteInput(ref inputIndex, observation.direction.x);
            WriteInput(ref inputIndex, observation.direction.y);
            WriteInput(ref inputIndex, observation.signedLineDistance);
            WriteInput(ref inputIndex, observation.surfaceDistance);
            WriteInput(ref inputIndex, observation.normalizedWarningTime);
            WriteInput(ref inputIndex, observation.normalizedThreat);
        }
    }

    private void WriteInput(ref int inputIndex, float value)
    {
        if (inputIndex < runtimeInputs.Length)
        {
            runtimeInputs[inputIndex] = value;
        }

        inputIndex++;
    }

    private float CalculateOutputNode(int outputIndex)
    {
        float sum = outputBiases[outputIndex];
        for (int source = 0; source < layer2NodeCount; source++)
        {
            int weightIndex = source * MovementOutputNodeCount + outputIndex;
            sum += runtimeLayer2[source] * layer2ToOutputWeights[weightIndex];
        }

        return Activate(sum);
    }

    private static float Activate(float value)
    {
        return (float)Math.Tanh(value);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        teacherLearningRate = Mathf.Max(0.000001f, teacherLearningRate);
        teacherSampleInterval = Mathf.Max(0.01f, teacherSampleInterval);
        stationarySampleInterval = Mathf.Max(
            teacherSampleInterval,
            stationarySampleInterval);
        gradientClamp = Mathf.Max(0.01f, gradientClamp);
        EnsureNeuralNetworkShape();
    }
#endif
}
