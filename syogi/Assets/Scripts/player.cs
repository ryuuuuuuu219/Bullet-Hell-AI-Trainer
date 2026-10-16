using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class player : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 500f;
    LineRenderer lineRenderer;
    public float size = 30f;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 3;
        lineRenderer.SetPosition(0, new Vector3(-1f, 0.6f, 0f)*size);
        lineRenderer.SetPosition(1, new Vector3(1f, 0.6f, 0f)*size);
        lineRenderer.SetPosition(2, new Vector3(0f, -1.3f, 0f)*size);
        lineRenderer.loop = true;
        lineRenderer.startWidth = 30f;
        lineRenderer.endWidth = 30f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
        lineRenderer.useWorldSpace = false;
    }


    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Vector2 direction = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            direction.y += 1f;
        }
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            direction.y -= 1f;
        }
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            direction.x -= 1f;
        }
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            direction.x += 1f;
        }

        Vector2 movement = Vector2.ClampMagnitude(direction, 1f) * moveSpeed * Time.deltaTime;
        transform.position += new Vector3(movement.x, movement.y, 0f);
    }
}
