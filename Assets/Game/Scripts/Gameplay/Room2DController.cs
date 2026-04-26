using UnityEngine;
using UnityEngine.UI;

public class Room2DController : MonoBehaviour
{
    public Room2DEntity roomEntity;
    public Room2DLabelView labelView;
    public Room2DOverview roomOverview;
    public string roomName = "Room 101";
    public Room2DState currentState = Room2DState.Dirty;
    public int actionCount;

    [Header("Optional State Visuals")]
    public GameObject dirtyVisual;
    public GameObject cleaningVisual;
    public GameObject awaitingInspectionVisual;
    public GameObject readyVisual;
    public GameObject occupiedVisual;
    public GameObject blockedVisual;
    public GameObject selectedVisual;

    [Header("Optional Tint Target")]
    public SpriteRenderer roomSpriteRenderer;
    public Image roomImage;

    public Color dirtyColor = new Color(0.65f, 0.45f, 0.35f);
    public Color cleaningColor = new Color(0.35f, 0.65f, 0.9f);
    public Color awaitingInspectionColor = new Color(0.95f, 0.8f, 0.35f);
    public Color readyColor = new Color(0.45f, 0.8f, 0.45f);
    public Color occupiedColor = new Color(0.75f, 0.6f, 0.9f);
    public Color blockedColor = new Color(0.35f, 0.35f, 0.35f);
    public float prototypeMaintenanceBlockHours = 8f;
    public float prototypeRenovationBlockHours = 72f;

    private void Awake()
    {
        FindRoomEntityIfNeeded();
        FindLabelViewIfNeeded();
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
        FindLabelViewIfNeeded();
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

    public void SetOccupied()
    {
        SetState(Room2DState.Occupied);
    }

    public void SetBlocked()
    {
        SetState(Room2DState.Blocked);
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
            case Room2DState.Ready:
                SetState(Room2DState.Occupied);
                break;
            default:
                SetState(Room2DState.Dirty);
                break;
        }
    }

    public void SimulateCheckIn()
    {
        PerformRoomAction(entity => entity.SimulateCheckIn(), Room2DState.Occupied);
    }

    public void SimulateCheckout()
    {
        PerformRoomAction(entity => entity.SimulateCheckout(), Room2DState.Dirty);
    }

    [ContextMenu("Start Maintenance Block")]
    public void StartMaintenanceBlock()
    {
        PerformRoomAction(entity => entity.StartBlock(Room2DBlockReason.Maintenance, prototypeMaintenanceBlockHours), Room2DState.Blocked);
    }

    [ContextMenu("Start Renovation Block")]
    public void StartRenovationBlock()
    {
        PerformRoomAction(entity => entity.StartBlock(Room2DBlockReason.Renovation, prototypeRenovationBlockHours), Room2DState.Blocked);
    }

    public void StartCleaning()
    {
        PerformRoomAction(entity => entity.StartCleaning(), Room2DState.Cleaning);
    }

    public void FinishCleaning()
    {
        PerformRoomAction(entity => entity.FinishCleaning(), Room2DState.AwaitingInspection);
    }

    public void ApproveInspection()
    {
        PerformRoomAction(entity => entity.ApproveInspection(), Room2DState.Ready);
    }

    public void ApplyStateVisual()
    {
        Room2DState visualState = GetCurrentState();

        SetVisualActive(dirtyVisual, visualState == Room2DState.Dirty);
        SetVisualActive(cleaningVisual, visualState == Room2DState.Cleaning);
        SetVisualActive(awaitingInspectionVisual, visualState == Room2DState.AwaitingInspection);
        SetVisualActive(readyVisual, visualState == Room2DState.Ready);
        SetVisualActive(occupiedVisual, visualState == Room2DState.Occupied);
        SetVisualActive(blockedVisual, visualState == Room2DState.Blocked);

        Color stateColor = GetStateColor();

        if (roomSpriteRenderer != null)
        {
            roomSpriteRenderer.color = stateColor;
        }

        if (roomImage != null)
        {
            roomImage.color = stateColor;
        }

        RefreshLabelView();
    }

    public void SetSelected(bool isSelected)
    {
        SetVisualActive(selectedVisual, isSelected);
    }

    private void SetVisualActive(GameObject visual, bool isActive)
    {
        if (visual != null)
        {
            visual.SetActive(isActive);
        }
    }

    private void PerformRoomAction(System.Func<Room2DEntity, bool> entityAction, Room2DState fallbackState)
    {
        if (roomEntity != null)
        {
            if (!entityAction(roomEntity))
            {
                return;
            }
        }
        else
        {
            currentState = fallbackState;
            actionCount++;
        }

        ApplyStateVisual();
        RefreshOverview();
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
            case Room2DState.Occupied:
                return occupiedColor;
            case Room2DState.Blocked:
                return blockedColor;
            default:
                return dirtyColor;
        }
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

    private void FindLabelViewIfNeeded()
    {
        if (labelView == null)
        {
            labelView = GetComponentInChildren<Room2DLabelView>(true);
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

    private void RefreshLabelView()
    {
        FindLabelViewIfNeeded();

        if (labelView != null && roomEntity != null)
        {
            labelView.Refresh(roomEntity);
        }
    }
}
