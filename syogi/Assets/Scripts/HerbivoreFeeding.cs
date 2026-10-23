using UnityEngine;

[DefaultExecutionOrder(10)]
public sealed class HerbivoreFeeding : MonoBehaviour
{
    [SerializeField] private GrassField grassField;
    private float elapsed;
    public GrassField Field => grassField;
    public event System.Action<GrassField> AfterFeeding;

    private void Awake()
    {
        if (grassField == null) grassField = FindAnyObjectByType<GrassField>();
        if (grassField == null)
        {
            Debug.LogWarning("HerbivoreFeeding requires a GrassField in the scene.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (grassField == null) return;
        elapsed += Time.deltaTime;
        while (elapsed >= 1f)
        {
            elapsed -= 1f;
            grassField.ConsumeGrass((Vector2)transform.position, 1);
            AfterFeeding?.Invoke(grassField);
        }
    }

    private void OnDisable()
    {
        elapsed = 0f;
    }
}
