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

    private void OnValidate()
    {
        SyncCheckoutFlagForState();
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
            default:
                SimulateCheckout();
                break;
        }
    }

    public bool SimulateCheckout()
    {
        if (!CanSimulateCheckout())
        {
            return false;
        }

        actionCount++;
        currentState = Room2DState.Dirty;
        guestCheckedOut = true;
        return true;
    }

    public bool StartCleaning()
    {
        if (!CanStartCleaning())
        {
            return false;
        }

        actionCount++;
        currentState = Room2DState.Cleaning;
        guestCheckedOut = true;
        return true;
    }

    public bool FinishCleaning()
    {
        if (!CanFinishCleaning())
        {
            return false;
        }

        actionCount++;
        currentState = Room2DState.AwaitingInspection;
        guestCheckedOut = true;
        return true;
    }

    public bool ApproveInspection()
    {
        if (!CanApproveInspection())
        {
            return false;
        }

        actionCount++;
        currentState = Room2DState.Ready;
        guestCheckedOut = false;
        return true;
    }

    public bool CanSimulateCheckout()
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

    private void SyncCheckoutFlagForState()
    {
        switch (currentState)
        {
            case Room2DState.Ready:
                guestCheckedOut = false;
                break;
            default:
                guestCheckedOut = true;
                break;
        }
    }
}
