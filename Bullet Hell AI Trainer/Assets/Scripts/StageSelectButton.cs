using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class StageSelectButton : MonoBehaviour
{
    [SerializeField] private int stageId;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(SelectStage);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(SelectStage);
        }
    }

    public void SelectStage()
    {
        GameSceneManager.SetStage(stageId);
    }
}
