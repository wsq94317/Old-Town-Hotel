using UnityEngine;
using UnityEngine.UI;

public class Room2DController : MonoBehaviour
{
    public Room2DState currentState = Room2DState.Dirty;

    [Header("Optional State Visuals")]
    public GameObject dirtyVisual;
    public GameObject cleaningVisual;
    public GameObject awaitingInspectionVisual;
    public GameObject readyVisual;

    [Header("Optional Tint Target")]
    public SpriteRenderer roomSpriteRenderer;
    public Image roomImage;

    public Color dirtyColor = new Color(0.65f, 0.45f, 0.35f);
    public Color cleaningColor = new Color(0.35f, 0.65f, 0.9f);
    public Color awaitingInspectionColor = new Color(0.95f, 0.8f, 0.35f);
    public Color readyColor = new Color(0.45f, 0.8f, 0.45f);

    private void Start()
    {
        ApplyStateVisual();
    }

    private void OnValidate()
    {
        ApplyStateVisual();
    }

    public void SetState(Room2DState newState)
    {
        currentState = newState;
        ApplyStateVisual();
    }

    public void ApplyStateVisual()
    {
        SetVisualActive(dirtyVisual, currentState == Room2DState.Dirty);
        SetVisualActive(cleaningVisual, currentState == Room2DState.Cleaning);
        SetVisualActive(awaitingInspectionVisual, currentState == Room2DState.AwaitingInspection);
        SetVisualActive(readyVisual, currentState == Room2DState.Ready);

        Color stateColor = GetStateColor();

        if (roomSpriteRenderer != null)
        {
            roomSpriteRenderer.color = stateColor;
        }

        if (roomImage != null)
        {
            roomImage.color = stateColor;
        }
    }

    private void SetVisualActive(GameObject visual, bool isActive)
    {
        if (visual != null)
        {
            visual.SetActive(isActive);
        }
    }

    private Color GetStateColor()
    {
        switch (currentState)
        {
            case Room2DState.Cleaning:
                return cleaningColor;
            case Room2DState.AwaitingInspection:
                return awaitingInspectionColor;
            case Room2DState.Ready:
                return readyColor;
            default:
                return dirtyColor;
        }
    }
}
