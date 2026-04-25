using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Optional UI")]
    public Text stateLabel;
    public TMP_Text stateLabelTextMeshPro;

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

    public void SetDirty()
    {
        SetState(Room2DState.Dirty);
    }

    public void SetCleaning()
    {
        SetState(Room2DState.Cleaning);
    }

    public void SetAwaitingInspection()
    {
        SetState(Room2DState.AwaitingInspection);
    }

    public void SetReady()
    {
        SetState(Room2DState.Ready);
    }

    public void CycleToNextState()
    {
        switch (currentState)
        {
            case Room2DState.Dirty:
                SetState(Room2DState.Cleaning);
                break;
            case Room2DState.Cleaning:
                SetState(Room2DState.AwaitingInspection);
                break;
            case Room2DState.AwaitingInspection:
                SetState(Room2DState.Ready);
                break;
            default:
                SetState(Room2DState.Dirty);
                break;
        }
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

        if (stateLabel != null)
        {
            stateLabel.text = GetStateDisplayName();
        }

        if (stateLabelTextMeshPro != null)
        {
            stateLabelTextMeshPro.text = GetStateDisplayName();
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

    private string GetStateDisplayName()
    {
        switch (currentState)
        {
            case Room2DState.Cleaning:
                return "Cleaning";
            case Room2DState.AwaitingInspection:
                return "Awaiting Inspection";
            case Room2DState.Ready:
                return "Ready";
            default:
                return "Dirty";
        }
    }
}
