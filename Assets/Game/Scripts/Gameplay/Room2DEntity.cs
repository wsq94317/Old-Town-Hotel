using UnityEngine;

public class Room2DEntity : MonoBehaviour
{
    public string roomId = "101";
    public string roomName = "Room 101";
    public int floorNumber = 1;
    public int roomNumber = 101;
    public Room2DState currentState = Room2DState.Dirty;
    public int actionCount;

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
    }

    public void PerformNextAction()
    {
        actionCount++;

        switch (currentState)
        {
            case Room2DState.Dirty:
                currentState = Room2DState.Cleaning;
                break;
            case Room2DState.Cleaning:
                currentState = Room2DState.AwaitingInspection;
                break;
            case Room2DState.AwaitingInspection:
                currentState = Room2DState.Ready;
                break;
            default:
                currentState = Room2DState.Dirty;
                break;
        }
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
                return "Next: Start Cleaning";
        }
    }

    public string GetActionCountDisplayName()
    {
        return "Actions: " + actionCount;
    }
}
