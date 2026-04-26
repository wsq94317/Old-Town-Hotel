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

    [Header("Block")]
    public Room2DBlockReason blockReason = Room2DBlockReason.None;
    public float blockRemainingHours;

    [Header("Room Attributes")]
    public Room2DAttribute[] roomAttributes;

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
        EnterState(newState, ShouldStateBeCheckedOut(newState));
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
            case Room2DState.Blocked:
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

    public bool StartBlock(Room2DBlockReason reason, float durationHours)
    {
        if (!CanStartBlock() || reason == Room2DBlockReason.None || durationHours <= 0f)
        {
            return false;
        }

        actionCount++;
        blockReason = reason;
        blockRemainingHours = durationHours;
        EnterState(Room2DState.Blocked, false);
        return true;
    }

    public bool AdvanceBlockTime(float gameHours)
    {
        if (currentState != Room2DState.Blocked || gameHours <= 0f)
        {
            return false;
        }

        blockRemainingHours = Mathf.Max(0f, blockRemainingHours - gameHours);

        if (blockRemainingHours <= 0f)
        {
            CompleteBlock();
        }

        return true;
    }

    public void CompleteBlock()
    {
        blockReason = Room2DBlockReason.None;
        blockRemainingHours = 0f;
        EnterState(Room2DState.Dirty, true);
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

    public bool CanStartBlock()
    {
        return currentState != Room2DState.Occupied
            && currentState != Room2DState.Cleaning
            && currentState != Room2DState.Blocked;
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
            case Room2DState.Blocked:
                return "Blocked";
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
            case Room2DState.Blocked:
                return "Blocked: " + GetBlockDisplayName();
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

    public string GetBlockDisplayName()
    {
        if (currentState != Room2DState.Blocked)
        {
            return "Not Blocked";
        }

        return blockReason + " " + FormatHours(blockRemainingHours);
    }

    [ContextMenu("Generate Prototype Room Attributes")]
    public void GeneratePrototypeRoomAttributes()
    {
        Room2DAttributeType[] possibleTypes =
        {
            Room2DAttributeType.Bed,
            Room2DAttributeType.Floor,
            Room2DAttributeType.Wardrobe,
            Room2DAttributeType.Bathroom,
            Room2DAttributeType.Wallpaper,
            Room2DAttributeType.AirConditioner,
            Room2DAttributeType.Window
        };

        int attributeCount = Random.Range(3, 6);
        roomAttributes = new Room2DAttribute[attributeCount];

        for (int i = 0; i < roomAttributes.Length; i++)
        {
            Room2DAttributeType attributeType = possibleTypes[(roomNumber + i) % possibleTypes.Length];
            int condition = Random.Range(35, 101);

            roomAttributes[i] = new Room2DAttribute
            {
                type = attributeType,
                condition = condition,
                note = GetPrototypeAttributeNote(attributeType, condition)
            };
        }
    }

    private void EnterState(Room2DState newState, bool newGuestCheckedOut)
    {
        currentState = newState;
        guestCheckedOut = newGuestCheckedOut;

        if (newState != Room2DState.Blocked)
        {
            blockReason = Room2DBlockReason.None;
            blockRemainingHours = 0f;
        }

        ResetStateTimer();
    }

    private void ResetStateTimer()
    {
        stateElapsedSeconds = 0f;
    }

    private void SyncCheckoutFlagForState()
    {
        guestCheckedOut = ShouldStateBeCheckedOut(currentState);
    }

    private bool ShouldStateBeCheckedOut(Room2DState state)
    {
        return state != Room2DState.Ready
            && state != Room2DState.Occupied
            && state != Room2DState.Blocked;
    }

    private string FormatHours(float hours)
    {
        if (hours >= 24f)
        {
            int days = Mathf.FloorToInt(hours / 24f);
            int remainingHours = Mathf.CeilToInt(hours - days * 24f);
            return days + "d " + remainingHours + "h";
        }

        return Mathf.CeilToInt(hours) + "h";
    }

    private string GetPrototypeAttributeNote(Room2DAttributeType attributeType, int condition)
    {
        if (condition >= 75)
        {
            return "Good";
        }

        if (condition >= 50)
        {
            return "Worn";
        }

        return "Problem: " + attributeType;
    }
}
