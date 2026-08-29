using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class NetworkConnectionView : MonoBehaviour
{
    private Image line;
    private TMP_Text weightText;
    private Color normalColor;

    public void Setup(RectTransform parent, Vector2 from, Vector2 to, float weight)
    {
        RectTransform rect = GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        Vector2 delta = to - from;
        float strength = Mathf.Clamp01(Mathf.Abs(weight));
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = from;
        rect.sizeDelta = new Vector2(delta.magnitude, Mathf.Lerp(1f, 4f, strength));
        rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        normalColor = weight > 0f
            ? new Color(0.2f, 0.65f, 1f, Mathf.Lerp(0.2f, 0.85f, strength))
            : weight < 0f
                ? new Color(1f, 0.3f, 0.3f, Mathf.Lerp(0.2f, 0.85f, strength))
                : new Color(0.6f, 0.6f, 0.6f, 0.15f);

        line = gameObject.AddComponent<Image>();
        line.color = normalColor;
        line.raycastTarget = false;

        GameObject labelObject = new GameObject("Weight", typeof(RectTransform));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(80f, 22f);
        labelRect.localEulerAngles = new Vector3(0f, 0f, -rect.localEulerAngles.z);

        weightText = labelObject.AddComponent<TextMeshProUGUI>();
        weightText.text = weight.ToString("0.000");
        weightText.fontSize = 14f;
        weightText.alignment = TextAlignmentOptions.Center;
        weightText.color = Color.white;
        weightText.raycastTarget = false;
        weightText.gameObject.SetActive(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (line != null)
        {
            line.color = highlighted ? Color.yellow : normalColor;
        }

        if (weightText != null)
        {
            weightText.gameObject.SetActive(highlighted);
        }
    }
}

public sealed class NetworkNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static Sprite circleSprite;
    private readonly List<NetworkConnectionView> connections = new List<NetworkConnectionView>();
    private Image image;
    private Color normalColor;

    public void Setup(string label, Color color, float diameter)
    {
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(diameter, diameter);

        normalColor = color;
        image = gameObject.AddComponent<Image>();
        image.color = normalColor;
        image.raycastTarget = true;

        image.sprite = GetCircleSprite();
        image.type = Image.Type.Simple;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -4f);
        labelRect.sizeDelta = new Vector2(72f, 22f);

        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 13f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    public void AddConnection(NetworkConnectionView connection)
    {
        if (connection != null && !connections.Contains(connection))
        {
            connections.Add(connection);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (image != null)
        {
            image.color = Color.yellow;
        }

        SetConnectionsHighlighted(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (image != null)
        {
            image.color = normalColor;
        }

        SetConnectionsHighlighted(false);
    }

    private void SetConnectionsHighlighted(bool highlighted)
    {
        foreach (NetworkConnectionView connection in connections)
        {
            if (connection != null)
            {
                connection.SetHighlighted(highlighted);
            }
        }
    }

    private static Sprite GetCircleSprite()
    {
        if (circleSprite != null)
        {
            return circleSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated Network Node Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 1f - distance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        circleSprite.name = "Generated Network Node Circle";
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }
}

public class View : MonoBehaviour
{
    public Aidata aidata;

    public List<GameObject> inputNodeObjects = new List<GameObject>();
    public List<GameObject> layer1NodeObjects = new List<GameObject>();
    public List<GameObject> layer2NodeObjects = new List<GameObject>();
    public List<GameObject> outputNodeObjects = new List<GameObject>();

    [Min(12f)] public float nodeDiameter = 30f;
    [Min(0f)] public float horizontalPadding = 55f;
    [Min(0f)] public float verticalPadding = 45f;

    private const string GeneratedRootName = "Neural Network View";
    private const int SensorRenderLayer = 31;
    private const float SensorViewPadding = 1.1f;
    private const float SensorLineWidth = 0.8f;

    private static Material sensorLineMaterial;
    private RenderTexture sensorRenderTexture;
    private Camera sensorRenderCamera;
    private bool sensorMode;

    public void Nodeset()
    {
        Rebuild();
    }

    public void SetSensorMode(bool enabled)
    {
        if (sensorMode == enabled)
        {
            return;
        }

        sensorMode = enabled;
        Rebuild();
    }

    public void Rebuild()
    {
        RectTransform area = transform as RectTransform;
        if (area == null)
        {
            Debug.LogWarning("View requires a RectTransform.", this);
            return;
        }

        Canvas.ForceUpdateCanvases();
        RemoveGeneratedView();
        ClearNodeLists();

        AiSaveData data = GetNetworkData();
        if (data == null)
        {
            Debug.LogWarning("Neural network data could not be loaded.", this);
            return;
        }

        Rect bounds = area.rect;
        if (bounds.width <= 0f || bounds.height <= 0f)
        {
            Debug.LogWarning("Network view area has no size.", this);
            return;
        }

        RectTransform root = CreateStretchRect(GeneratedRootName, area);
        if (sensorMode)
        {
            CreateSensorVisualization(root, bounds, data.circularSensors);
            return;
        }

        RectTransform connectionsRoot = CreateStretchRect("Connections", root);
        RectTransform nodesRoot = CreateStretchRect("Nodes", root);

        List<NetworkNodeView> inputs = CreateLayer(nodesRoot, inputNodeObjects, data.inputNodeCount, 0, bounds, "I", new Color(0.25f, 0.75f, 1f));
        List<NetworkNodeView> layer1 = CreateLayer(nodesRoot, layer1NodeObjects, data.layer1NodeCount, 1, bounds, "H1-", new Color(0.35f, 1f, 0.45f));
        List<NetworkNodeView> layer2 = CreateLayer(nodesRoot, layer2NodeObjects, data.layer2NodeCount, 2, bounds, "H2-", new Color(1f, 0.75f, 0.25f));
        List<NetworkNodeView> outputs = CreateLayer(nodesRoot, outputNodeObjects, data.outputNodeCount, 3, bounds, "O", new Color(1f, 0.35f, 0.75f));

        ConnectLayers(connectionsRoot, inputs, layer1, data.inputToLayer1Weights);
        ConnectLayers(connectionsRoot, layer1, layer2, data.layer1ToLayer2Weights);
        ConnectLayers(connectionsRoot, layer2, outputs, data.layer2ToOutputWeights);
    }

    private AiSaveData GetNetworkData()
    {
        if (aidata == null)
        {
            return Aidata.LoadData();
        }

        aidata.EnsureNeuralNetworkShape();
        AiSaveData data = new AiSaveData
        {
            inputNodeCount = aidata.inputNodeCount,
            layer1NodeCount = aidata.layer1NodeCount,
            layer2NodeCount = aidata.layer2NodeCount,
            outputNodeCount = aidata.outputNodeCount,
            inputToLayer1Weights = aidata.inputToLayer1Weights,
            layer1ToLayer2Weights = aidata.layer1ToLayer2Weights,
            layer2ToOutputWeights = aidata.layer2ToOutputWeights
        };

        if (aidata.sensors == null)
        {
            return data;
        }

        foreach (CircularSensor sensor in aidata.sensors)
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

    private void CreateSensorVisualization(
        RectTransform parent,
        Rect bounds,
        List<CircularSensorData> savedSensors)
    {
        List<CircularSensorData> sensors = savedSensors != null && savedSensors.Count > 0
            ? savedSensors
            : CircularSensor.CreateDefaultData();

        int textureWidth = Mathf.Max(1, Mathf.RoundToInt(bounds.width));
        int textureHeight = Mathf.Max(1, Mathf.RoundToInt(bounds.height));
        sensorRenderTexture = new RenderTexture(
            textureWidth,
            textureHeight,
            0,
            RenderTextureFormat.ARGB32)
        {
            name = "Sensor Range Preview",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        sensorRenderTexture.Create();

        RectTransform previewRect = CreateStretchRect("Sensor Range Preview", parent);
        RawImage previewImage = previewRect.gameObject.AddComponent<RawImage>();
        previewImage.texture = sensorRenderTexture;
        previewImage.color = Color.white;
        previewImage.raycastTarget = false;

        GameObject worldRoot = new GameObject("Sensor Range World");
        worldRoot.transform.SetParent(parent, false);
        worldRoot.layer = SensorRenderLayer;

        float maximumRadius = 1f;
        foreach (CircularSensorData sensor in sensors)
        {
            if (sensor == null)
            {
                continue;
            }

            maximumRadius = Mathf.Max(maximumRadius, sensor.radius);
            CreateSensorRangeLine(worldRoot.transform, sensor);
        }

        CreatePlayerPreview(worldRoot.transform);
        CreateSensorCamera(
            worldRoot.transform,
            textureWidth,
            textureHeight,
            maximumRadius);
    }

    private static void CreateSensorRangeLine(
        Transform parent,
        CircularSensorData sensor)
    {
        GameObject lineObject = new GameObject("Sensor Range", typeof(LineRenderer));
        lineObject.layer = SensorRenderLayer;
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        line.sharedMaterial = GetSensorLineMaterial();
        line.useWorldSpace = false;
        line.loop = true;
        line.startWidth = SensorLineWidth;
        line.endWidth = SensorLineWidth;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.alignment = LineAlignment.TransformZ;

        int segmentCount = Mathf.Clamp(sensor.arcSegments, 3, 64);
        float outerRadius = Mathf.Max(0.01f, sensor.radius);
        float innerRadius = Mathf.Clamp(sensor.innerRadius, 0f, outerRadius);
        float sensorAngle = Mathf.Clamp(sensor.angle, 1f, 359f);
        float startAngle = sensor.centerAngle - sensorAngle * 0.5f;

        int outerPointCount = segmentCount + 1;
        int innerPointCount = innerRadius > 0f ? segmentCount + 1 : 1;
        line.positionCount = outerPointCount + innerPointCount;

        for (int index = 0; index < outerPointCount; index++)
        {
            float interpolation = index / (float)segmentCount;
            line.SetPosition(
                index,
                GetSensorPoint(outerRadius, startAngle + sensorAngle * interpolation));
        }

        if (innerRadius <= 0f)
        {
            line.SetPosition(outerPointCount, Vector3.zero);
            return;
        }

        for (int index = 0; index < innerPointCount; index++)
        {
            float interpolation = index / (float)segmentCount;
            line.SetPosition(
                outerPointCount + index,
                GetSensorPoint(innerRadius, startAngle + sensorAngle * (1f - interpolation)));
        }
    }

    private static Vector3 GetSensorPoint(float radius, float angleDegrees)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0f);
    }

    private static void CreatePlayerPreview(Transform parent)
    {
        GameObject playerObject = new GameObject("Player Preview", typeof(LineRenderer));
        playerObject.layer = SensorRenderLayer;
        playerObject.transform.SetParent(parent, false);
        playerObject.GetComponent<LineRenderer>().sharedMaterial = GetSensorLineMaterial();
        playerObject.AddComponent<PlayerTriangleRenderer>();
    }

    private void CreateSensorCamera(
        Transform parent,
        int textureWidth,
        int textureHeight,
        float maximumRadius)
    {
        GameObject cameraObject = new GameObject("Sensor Range Camera", typeof(Camera));
        cameraObject.layer = SensorRenderLayer;
        cameraObject.transform.SetParent(parent, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
        cameraObject.transform.localRotation = Quaternion.identity;

        sensorRenderCamera = cameraObject.GetComponent<Camera>();
        sensorRenderCamera.orthographic = true;
        sensorRenderCamera.aspect = textureWidth / (float)textureHeight;
        sensorRenderCamera.orthographicSize = maximumRadius * SensorViewPadding /
                                              Mathf.Min(1f, sensorRenderCamera.aspect);
        sensorRenderCamera.clearFlags = CameraClearFlags.SolidColor;
        sensorRenderCamera.backgroundColor = Color.clear;
        sensorRenderCamera.cullingMask = 1 << SensorRenderLayer;
        sensorRenderCamera.nearClipPlane = 0.1f;
        sensorRenderCamera.farClipPlane = 20f;
        sensorRenderCamera.allowHDR = false;
        sensorRenderCamera.allowMSAA = false;
        sensorRenderCamera.targetTexture = sensorRenderTexture;
    }

    private static Material GetSensorLineMaterial()
    {
        if (sensorLineMaterial != null)
        {
            return sensorLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("UI/Default");
        sensorLineMaterial = new Material(shader)
        {
            name = "Generated Sensor Line Material",
            hideFlags = HideFlags.HideAndDontSave,
        };
        return sensorLineMaterial;
    }

    private List<NetworkNodeView> CreateLayer(
        RectTransform parent,
        List<GameObject> objectList,
        int count,
        int layerIndex,
        Rect bounds,
        string labelPrefix,
        Color color)
    {
        List<NetworkNodeView> result = new List<NetworkNodeView>();
        count = Mathf.Max(0, count);

        float usableWidth = Mathf.Max(0f, bounds.width - horizontalPadding * 2f);
        float x = -bounds.width * 0.5f + horizontalPadding + usableWidth * layerIndex / 3f;
        float usableHeight = Mathf.Max(0f, bounds.height - verticalPadding * 2f);

        for (int i = 0; i < count; i++)
        {
            float y = count <= 1
                ? 0f
                : bounds.height * 0.5f - verticalPadding - usableHeight * i / (count - 1f);

            GameObject nodeObject = new GameObject(labelPrefix + i, typeof(RectTransform));
            RectTransform rect = nodeObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);

            NetworkNodeView node = nodeObject.AddComponent<NetworkNodeView>();
            node.Setup(labelPrefix + i, color, nodeDiameter);
            objectList.Add(nodeObject);
            result.Add(node);
        }

        return result;
    }

    private static void ConnectLayers(
        RectTransform parent,
        IReadOnlyList<NetworkNodeView> fromNodes,
        IReadOnlyList<NetworkNodeView> toNodes,
        float[] weights)
    {
        for (int from = 0; from < fromNodes.Count; from++)
        {
            for (int to = 0; to < toNodes.Count; to++)
            {
                int index = from * toNodes.Count + to;
                float weight = weights != null && index < weights.Length ? weights[index] : 0f;

                GameObject connectionObject = new GameObject($"{from}-{to}", typeof(RectTransform));
                NetworkConnectionView connection = connectionObject.AddComponent<NetworkConnectionView>();
                Vector2 fromPosition = ((RectTransform)fromNodes[from].transform).anchoredPosition;
                Vector2 toPosition = ((RectTransform)toNodes[to].transform).anchoredPosition;
                connection.Setup(parent, fromPosition, toPosition, weight);

                fromNodes[from].AddConnection(connection);
                toNodes[to].AddConnection(connection);
            }
        }
    }

    private static RectTransform CreateStretchRect(string objectName, RectTransform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void RemoveGeneratedView()
    {
        if (sensorRenderCamera != null)
        {
            sensorRenderCamera.enabled = false;
            sensorRenderCamera.targetTexture = null;
            sensorRenderCamera = null;
        }

        if (sensorRenderTexture != null)
        {
            sensorRenderTexture.Release();
            if (Application.isPlaying)
            {
                Destroy(sensorRenderTexture);
            }
            else
            {
                DestroyImmediate(sensorRenderTexture);
            }

            sensorRenderTexture = null;
        }

        Transform generated = transform.Find(GeneratedRootName);
        if (generated == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generated.gameObject);
        }
        else
        {
            DestroyImmediate(generated.gameObject);
        }
    }

    private void ClearNodeLists()
    {
        inputNodeObjects.Clear();
        layer1NodeObjects.Clear();
        layer2NodeObjects.Clear();
        outputNodeObjects.Clear();
    }
}
