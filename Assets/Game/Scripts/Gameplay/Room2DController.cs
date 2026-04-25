using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Room2DController : MonoBehaviour
{
    public Room2DEntity roomEntity;
    public Room2DOverview roomOverview;
    public string roomName = "Room 101";
    public Room2DState currentState = Room2DState.Dirty;
    public int actionCount;

    [Header("Optional State Visuals")]
    public GameObject dirtyVisual;
    public GameObject cleaningVisual;
    public GameObject awaitingInspectionVisual;
    public GameObject readyVisual;

    [Header("Optional Tint Target")]
    public SpriteRenderer roomSpriteRenderer;
    public Image roomImage;

    [Header("Optional UI")]
    public TMP_Text roomNameLabelTextMeshPro;
    public TMP_Text stateLabelTextMeshPro;
    public TMP_Text nextActionLabelTextMeshPro;
    public TMP_Text actionCountLabelTextMeshPro;

    public Color dirtyColor = new Color(0.65f, 0.45f, 0.35f);
    public Color cleaningColor = new Color(0.35f, 0.65f, 0.9f);
    public Color awaitingInspectionColor = new Color(0.95f, 0.8f, 0.35f);
    public Color readyColor = new Color(0.45f, 0.8f, 0.45f);

    private void Awake()
    {
        FindRoomEntityIfNeeded();
        FindRoomOverviewIfNeeded();
    }

    private void Start()
    {
        FindRoomOverviewIfNeeded();
        ApplyStateVisual();
    }

    private void OnValidate()
    {
        FindRoomEntityIfNeeded();
        ApplyStateVisual();
    }

    public void SetState(Room2DState newState)
    {
        if (roomEntity != null)
        {
            roomEntity.SetState(newState);
        }
        else
        {
            currentState = newState;
        }

        ApplyStateVisual();
        RefreshOverview();
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
        PerformNextAction();
    }

    public void PerformNextAction()
    {
        if (roomEntity != null)
        {
            roomEntity.PerformNextAction();
            ApplyStateVisual();
            RefreshOverview();
            return;
        }

        actionCount++;

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
        Room2DState visualState = GetCurrentState();

        SetVisualActive(dirtyVisual, visualState == Room2DState.Dirty);
        SetVisualActive(cleaningVisual, visualState == Room2DState.Cleaning);
        SetVisualActive(awaitingInspectionVisual, visualState == Room2DState.AwaitingInspection);
        SetVisualActive(readyVisual, visualState == Room2DState.Ready);

        Color stateColor = GetStateColor();

        if (roomSpriteRenderer != null)
        {
            roomSpriteRenderer.color = stateColor;
        }

        if (roomImage != null)
        {
            roomImage.color = stateColor;
        }

        if (stateLabelTextMeshPro != null)
        {
            stateLabelTextMeshPro.text = GetStateDisplayName();
        }

        if (nextActionLabelTextMeshPro != null)
        {
            nextActionLabelTextMeshPro.text = GetNextActionDisplayName();
        }

        if (roomNameLabelTextMeshPro != null)
        {
            roomNameLabelTextMeshPro.text = GetRoomName();
        }

        if (actionCountLabelTextMeshPro != null)
        {
            actionCountLabelTextMeshPro.text = GetActionCountDisplayName();
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
        switch (GetCurrentState())
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
        if (roomEntity != null)
        {
            return roomEntity.GetStateDisplayName();
        }

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

    private string GetNextActionDisplayName()
    {
        if (roomEntity != null)
        {
            return roomEntity.GetNextActionDisplayName();
        }

        switch (currentState)
        {
            case Room2DState.Cleaning:
                return "Next: Finish Cleaning";
            case Room2DState.AwaitingInspection:
                return "Next: Approve Inspection";
            case Room2DState.Ready:
                return "Next: Simulate Checkout";
            default:
                return "Next: Start Cleaning";
        }
    }

    private string GetActionCountDisplayName()
    {
        if (roomEntity != null)
        {
            return roomEntity.GetActionCountDisplayName();
        }

        return "Actions: " + actionCount;
    }

    private string GetRoomName()
    {
        if (roomEntity != null)
        {
            return roomEntity.roomName;
        }

        return roomName;
    }

    private Room2DState GetCurrentState()
    {
        if (roomEntity != null)
        {
            return roomEntity.currentState;
        }

        return currentState;
    }

    private void FindRoomEntityIfNeeded()
    {
        if (roomEntity == null)
        {
            roomEntity = GetComponent<Room2DEntity>();
        }
    }

    private void FindRoomOverviewIfNeeded()
    {
        if (roomOverview == null)
        {
            roomOverview = FindFirstObjectByType<Room2DOverview>();
        }
    }

    private void RefreshOverview()
    {
        FindRoomOverviewIfNeeded();

        if (roomOverview != null)
        {
            roomOverview.RefreshSummary();
        }
    }
}
