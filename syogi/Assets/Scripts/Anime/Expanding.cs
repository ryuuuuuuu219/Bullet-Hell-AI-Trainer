using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class Expanding : MonoBehaviour
{
    // 半径・太さはCanvas上の座標単位
    [Min(0f)] public float startRadius = 0f;
    [Min(0f)] public float endRadius = 800f;
    [Min(0.01f)] public float duration = 0.6f;
    [Min(0.001f)] public float lineWidth = 100f;

    // 左下=(0,0)、中央=(0.5,0.5)、右上=(1,1)
    public Vector2 center = new Vector2(0.5f, 0.5f);

    public Canvas parentCanvas;

    private RawImage image;
    private RectTransform rect;
    private Material generatedMaterial;
    private float elapsed;

    private void Awake()
    {
        if (parentCanvas != null)
        {
            transform.SetParent(parentCanvas.transform, false);
        }

        // 同じCanvas内のUIより後に描く
        transform.SetAsLastSibling();

        image = GetComponent<RawImage>();
        rect = GetComponent<RectTransform>();

        // 親の全面に広げる
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        image.raycastTarget = false;
        image.maskable = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);

        Shader shader = Shader.Find("Custom/ShockwaveInvert");

        if (shader == null)
        {
            Debug.LogError("ShockwaveInvertが見つかりません。", this);
            image.enabled = false;
            enabled = false;
            return;
        }

        generatedMaterial = new Material(shader);
        image.material = generatedMaterial;

        generatedMaterial.SetFloat("_Radius", 0f);
        generatedMaterial.SetFloat("_Width", 0f);
    }

    private void OnEnable()
    {
        elapsed = 0f;
        transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (generatedMaterial == null)
        {
            return;
        }

        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(elapsed / duration);

        float radius = Mathf.Lerp(
            startRadius + lineWidth / 2f,
            endRadius,
            t
        );

        float width = lineWidth * (1f - t);
        Vector2 size = rect.rect.size;

        generatedMaterial.SetVector(
            "_RectSize",
            new Vector4(size.x, size.y, 0f, 0f)
        );

        generatedMaterial.SetVector(
            "_Center",
            new Vector4(center.x, center.y, 0f, 0f)
        );

        generatedMaterial.SetFloat("_Radius", radius);
        generatedMaterial.SetFloat("_Width", width);

        if (t >= 1f)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (generatedMaterial != null)
        {
            Destroy(generatedMaterial);
        }
    }
}