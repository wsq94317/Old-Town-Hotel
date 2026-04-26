using UnityEngine;

public class Room2DEntity : MonoBehaviour
{
    public string roomId = "101";
    public string roomName = "Room 101";
    public int floorNumber = 1;
    public int roomNumber = 101;
    public Room2DState currentState = Room2DState.Dirty;
    public bool guestCheckedOut = true;
    public int actionCount;
    public bool trackStateTime = true;
    public float stateElapsedSeconds;

    private void OnValidate()
    {
        SyncCheckoutFlagForState();
    }

    private void Update()
    {
        if (!trackStateTime)
        {
            return;
        }

        stateElapsedSeconds += Time.deltaTime;
    }

    public void SetIdentity(int newFloorNumber, int newRoomNumber)
    {
        floorNumber = newFloorNumber;
        roomNumber = newRoomNumber;
        roomId = newRoomNumber.ToString();
        roomName = "Room " + newRoomNumber;
    }

    public void SetState(Room2DState newState)
    {
        currentState = newState;
        SyncCheckoutFlagForState();
        ResetStateTimer();
    }

    public void PerformNextAction()
    {
        switch (currentState)
        {
            case Room2DState.Dirty:
                StartCleaning();
                break;
            case Room2DState.Cleaning:
                FinishCleaning();
                break;
            case Room2DState.AwaitingInspection:
                ApproveInspection();
                break;
            case Room2DState.Ready:
                SimulateCheckIn();
                break;
            default:
                SimulateCheckout();
                break;
        }
    }

    public bool SimulateCheckIn()
    {
        if (!CanSimulateCheckIn())
        {
            return false;
        }

        actionCount++;
        EnterState(Room2DState.Occupied, false);
        return true;
    }

    public bool SimulateCheckout()
    {
        if (!CanSimulateCheckout())
        {
            return false;
        }

        actionCount++;
        EnterState(Room2DState.Dirty, true);
        return true;
    }

    public bool StartCleaning()
    {
        if (!CanStartCleaning())
        {
            return false;
        }

        actionCount++;
        EnterState(Room2DState.Cleaning, true);
        return true;
    }

    public bool FinishCleaning()
    {
        if (!CanFinishCleaning())
        {
            return false;
        }

        actionCount++;
        EnterState(Room2DState.AwaitingInspection, true);
        return true;
    }

    public bool ApproveInspection()
    {
        if (!CanApproveInspection())
        {
            return false;
        }

        actionCount++;
        EnterState(Room2DState.Ready, false);
        return true;
    }

    public bool CanSimulateCheckout()
    {
        return currentState == Room2DState.Occupied;
    }

    public bool CanSimulateCheckIn()
    {
        return currentState == Room2DState.Ready;
    }

    public bool CanStartCleaning()
    {
        return currentState == Room2DState.Dirty && guestCheckedOut;
    }

    public bool CanFinishCleaning()
    {
        return currentState == Room2DState.Cleaning;
    }

    public bool CanApproveInspection()
    {
        return currentState == Room2DState.AwaitingInspection;
    }

    public string GetStateDisplayName()
    {
        switch (currentState)
        {
            case Room2DState.Cleaning:
                return "Cleaning";
            case Room2DState.AwaitingInspection:
                return "Awaiting Inspection";
            case Room2DState.Ready:
                return "Ready";
            case Room2DState.Occupied:
                return "Occupied";
            default:
                return "Dirty";
        }
    }

    public string GetNextActionDisplayName()
    {
        switch (currentState)
        {
            case Room2DState.Cleaning:
                return "Next: Finish Cleaning";
            case Room2DState.AwaitingInspection:
                return "Next: Approve Inspection";
            case Room2DState.Ready:
                return "Next: Simulate Check In";
            case Room2DState.Occupied:
                return "Next: Simulate Checkout";
            default:
                return guestCheckedOut ? "Next: Start Cleaning" : "Next: Wait for Checkout";
        }
    }

    public string GetActionCountDisplayName()
    {
        return "Actions: " + actionCount;
    }

    public string GetCheckoutDisplayName()
    {
        return guestCheckedOut ? "Checked Out" : "Not Checked Out";
    }

    public string GetStateTimeDisplayName()
    {
        return "State Time: " + Mathf.FloorToInt(stateElapsedSeconds) + "s";
    }

    private void EnterState(Room2DState newState, bool newGuestCheckedOut)
    {
        currentState = newState;
        guestCheckedOut = newGuestCheckedOut;
        ResetStateTimer();
    }

    private void ResetStateTimer()
    {
        stateElapsedSeconds = 0f;
    }

    private void SyncCheckoutFlagForState()
    {
        switch (currentState)
        {
            case Room2DState.Ready:
            case Room2DState.Occupied:
                guestCheckedOut = false;
                break;
            default:
                guestCheckedOut = true;
                break;
        }
    }
}
