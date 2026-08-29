using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class sensor_circlr : MonoBehaviour
{
    public float radius = 5f;
    [Range(1f, 359f)] public float angle = 90f;
    public float centerAngle = 90f;
    public float priority = 1f;
    [Range(3, 64)] public int arcSegments = 16;

    private PolygonCollider2D col;

    private void Start()
    {
        ConfigurePhysics();
        RebuildFanCollider();
    }

    public void RebuildFanCollider()
    {
        col ??= GetComponent<PolygonCollider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<PolygonCollider2D>();
        }

        col.isTrigger = true;
        radius = Mathf.Max(0.01f, radius);
        angle = Mathf.Clamp(angle, 1f, 359f);
        arcSegments = Mathf.Clamp(arcSegments, 3, 64);

        Vector2[] points = new Vector2[arcSegments + 2];
        points[0] = Vector2.zero;

        float startAngle = centerAngle - angle * 0.5f;
        for (int index = 0; index <= arcSegments; index++)
        {
            float interpolation = index / (float)arcSegments;
            float theta = Mathf.Deg2Rad * (startAngle + angle * interpolation);
            points[index + 1] = new Vector2(
                radius * Mathf.Cos(theta),
                radius * Mathf.Sin(theta));
        }

        col.pathCount = 1;
        col.SetPath(0, points);
    }

    private void ConfigurePhysics()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
    }

    public int sencing()
    {
        int count = bulletCount;
        bulletCount = 0;
        return count;
    }

    public int bulletCount;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out bullet detectedBullet))
        {
            PlayerAgent player = GetComponentInParent<PlayerAgent>();
            if (player != null && detectedBullet.LogicalLayer != player.LogicalLayer)
            {
                return;
            }

            bulletCount++;
            Debug.Log(
                $"Bullet detected: vector={detectedBullet.Vector}, " +
                $"threat={detectedBullet.ThreatLevel}");
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        angle = Mathf.Clamp(angle, 1f, 359f);
        arcSegments = Mathf.Clamp(arcSegments, 3, 64);

        if (gameObject.scene.IsValid() && TryGetComponent(out PolygonCollider2D existingCollider))
        {
            col = existingCollider;
            RebuildFanCollider();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Vector3 origin = transform.position;
        Vector3 previous = transform.TransformPoint(GetArcPoint(0));
        Gizmos.DrawLine(origin, previous);

        int segmentCount = Mathf.Clamp(arcSegments, 3, 64);
        for (int index = 1; index <= segmentCount; index++)
        {
            Vector3 current = transform.TransformPoint(GetArcPoint(index / (float)segmentCount));
            Gizmos.DrawLine(previous, current);
            previous = current;
        }

        Gizmos.DrawLine(previous, origin);
    }

    private Vector2 GetArcPoint(float interpolation)
    {
        float theta = Mathf.Deg2Rad *
                      (centerAngle - angle * 0.5f + angle * interpolation);
        return new Vector2(radius * Mathf.Cos(theta), radius * Mathf.Sin(theta));
    }
#endif
}

[Serializable]
public sealed class SensorCircleData
{
    public float radius = 5f;
    public float angle = 90f;
    public float centerAngle = 90f;
    public float priority = 1f;
    public int arcSegments = 16;
}

[Serializable]
public sealed class AiSaveData
{
    public bool useProximityInput = true;
    public bool useCircularSensorInput = true;
    public bool useWarningLineInput = true;
    public List<SensorCircleData> circularSensors = new List<SensorCircleData>();

    public int inputNodeCount = 3;
    public int layer1NodeCount = 10;
    public int layer2NodeCount = 10;
    public int outputNodeCount = 2;

    public float[] inputToLayer1Weights = Array.Empty<float>();
    public float[] layer1Biases = Array.Empty<float>();
    public float[] layer1ToLayer2Weights = Array.Empty<float>();
    public float[] layer2Biases = Array.Empty<float>();
    public float[] layer2ToOutputWeights = Array.Empty<float>();
    public float[] outputBiases = Array.Empty<float>();
}

public class Aidata : MonoBehaviour
{
    public const int MovementOutputNodeCount = 2;

    private const string PlayerPrefsKey = "BulletHellAITrainer.AiData.v1";

    private float[] runtimeInputs = Array.Empty<float>();
    private float[] runtimeLayer1 = Array.Empty<float>();
    private float[] runtimeLayer2 = Array.Empty<float>();

    [Header("入力")]
    public bool useProximityInput = true;
    public bool useCircularSensorInput = true;
    public bool useWarningLineInput = true;
    public List<sensor_circlr> sensors = new List<sensor_circlr>();

    [Header("ニューラルネットワーク構造")]
    [Min(1)] public int inputNodeCount = 3;
    [FormerlySerializedAs("layer1nodes")]
    [Min(1)] public int layer1NodeCount = 10;
    [FormerlySerializedAs("layer2nodes")]
    [Min(1)] public int layer2NodeCount = 10;
    [Min(1)] public int outputNodeCount = 2;

    [Header("ニューラルネットワーク係数")]
    public float[] inputToLayer1Weights = Array.Empty<float>();
    public float[] layer1Biases = Array.Empty<float>();
    public float[] layer1ToLayer2Weights = Array.Empty<float>();
    public float[] layer2Biases = Array.Empty<float>();
    public float[] layer2ToOutputWeights = Array.Empty<float>();
    public float[] outputBiases = Array.Empty<float>();

    private void Awake()
    {
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
        inputNodeCount = Mathf.Max(1, inputNodeCount);
        layer1NodeCount = Mathf.Max(1, layer1NodeCount);
        layer2NodeCount = Mathf.Max(1, layer2NodeCount);
        outputNodeCount = MovementOutputNodeCount;

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
            foreach (SensorCircleData sensor in source.circularSensors)
            {
                if (sensor == null)
                {
                    continue;
                }

                clone.circularSensors.Add(new SensorCircleData
                {
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

        foreach (sensor_circlr sensor in sensors)
        {
            if (sensor == null)
            {
                continue;
            }

            data.circularSensors.Add(new SensorCircleData
            {
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

    private void ApplySensorData(List<SensorCircleData> savedSensors)
    {
        sensors.RemoveAll(sensor => sensor == null);

        for (int index = sensors.Count; index < savedSensors.Count; index++)
        {
            GameObject sensorObject = new GameObject($"Circular Sensor {index + 1}");
            sensorObject.transform.SetParent(transform, false);
            sensors.Add(sensorObject.AddComponent<sensor_circlr>());
        }

        for (int index = 0; index < savedSensors.Count; index++)
        {
            SensorCircleData source = savedSensors[index];
            sensor_circlr target = sensors[index];
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
        data.circularSensors ??= new List<SensorCircleData>();
        foreach (SensorCircleData sensor in data.circularSensors)
        {
            sensor.radius = Mathf.Max(0.01f, sensor.radius);
            sensor.angle = Mathf.Clamp(sensor.angle, 1f, 359f);
            sensor.arcSegments = sensor.arcSegments <= 0
                ? 16
                : Mathf.Clamp(sensor.arcSegments, 3, 64);
        }
        data.inputNodeCount = Mathf.Max(1, data.inputNodeCount);
        data.layer1NodeCount = Mathf.Max(1, data.layer1NodeCount);
        data.layer2NodeCount = Mathf.Max(1, data.layer2NodeCount);
        data.outputNodeCount = MovementOutputNodeCount;

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

        ResizePreserving(ref runtimeLayer1, layer1NodeCount);
        ResizePreserving(ref runtimeLayer2, layer2NodeCount);

        for (int destination = 0; destination < layer1NodeCount; destination++)
        {
            float sum = layer1Biases[destination];
            for (int source = 0; source < inputNodeCount; source++)
            {
                int weightIndex = source * layer1NodeCount + destination;
                sum += runtimeInputs[source] * inputToLayer1Weights[weightIndex];
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

    private void BuildRuntimeInputs()
    {
        ResizePreserving(ref runtimeInputs, inputNodeCount);
        Array.Clear(runtimeInputs, 0, runtimeInputs.Length);

        int inputIndex = 0;
        if (useProximityInput && inputIndex < runtimeInputs.Length)
        {
            int logicalLayer = TryGetComponent(out PlayerAgent player)
                ? player.LogicalLayer
                : -1;

            if (BulletManager.TryGetNearest(
                    transform.position,
                    logicalLayer,
                    out bullet nearestBullet))
            {
                float distance = Vector2.Distance(
                    transform.position,
                    nearestBullet.transform.position);
                runtimeInputs[inputIndex] = 1f / (1f + distance);
            }

            inputIndex++;
        }

        if (useCircularSensorInput && inputIndex < runtimeInputs.Length)
        {
            int detectedBulletCount = 0;
            foreach (sensor_circlr sensor in sensors)
            {
                if (sensor != null)
                {
                    detectedBulletCount += sensor.sencing();
                }
            }

            runtimeInputs[inputIndex] = detectedBulletCount /
                                        (detectedBulletCount + 1f);
            inputIndex++;
        }

        if (useWarningLineInput && inputIndex < runtimeInputs.Length)
        {
            // Warning-line sensing is not implemented yet. Reserve its input node.
            runtimeInputs[inputIndex] = 0f;
        }
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
        EnsureNeuralNetworkShape();
    }
#endif
}
