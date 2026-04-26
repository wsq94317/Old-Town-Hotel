using UnityEngine;

// 房间的核心数据实体。
// 它不负责画面怎么显示，只保存房间的业务状态、房号、Block 信息和房间属性。
public class Room2DEntity : MonoBehaviour
{
    // 房间身份信息。后面存档和 UI 都应该从这里读房间编号。
    public string roomId = "101";
    public string roomName = "Room 101";
    public int floorNumber = 1;
    public int roomNumber = 101;

    // 当前房态，是房间流程的核心。
    public Room2DState currentState = Room2DState.Dirty;

    // Dirty/Cleaning/AwaitingInspection 代表客人已经离开后产生的清洁链条。
    public bool guestCheckedOut = true;

    // 临时原型计数，用来确认按钮/流程是否真的被触发。
    public int actionCount;

    // 当前状态停留时间，用于以后做等待压力、优先级、评价等系统。
    public bool trackStateTime = true;
    public float stateElapsedSeconds;

    [Header("Block")]
    // Blocked 状态的原因和剩余游戏小时数。
    public Room2DBlockReason blockReason = Room2DBlockReason.None;
    public float blockRemainingHours;

    [Header("Room Attributes")]
    // 房间内部属性数据。不要用 Hierarchy 当作玩法数据源，UI/存档以后读取这个数组。
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

    // 强制设置房态。原型工具和批量重置会用到它。
    public void SetState(Room2DState newState)
    {
        EnterState(newState, ShouldStateBeCheckedOut(newState));
    }

    // 当前主按钮的默认行为：按房态推进下一步。
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

    // 原型用：模拟客人入住。正式前台系统以后会替代这个入口。
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

    // 原型用：模拟客人退房。退房后房间进入 Dirty。
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

    // 开始清洁。只有 Dirty 且客人已退房的房间可以开始清洁。
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

    // 清洁完成后进入等待检查。
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

    // 检查通过后，房间重新变成 Ready。
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

    // 将房间锁定为不可用，例如维修或装修。
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

    // 由原型时钟调用：推进 Block 剩余时间。
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

    // Block 到期后进入 Dirty，因为维修/装修结束后仍然需要清洁整理。
    public void CompleteBlock()
    {
        blockReason = Room2DBlockReason.None;
        blockRemainingHours = 0f;
        EnterState(Room2DState.Dirty, true);
    }

    // 以下 Can... 方法负责保护状态流转，避免 UI 或别的脚本乱跳状态。
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

    // 给 UI 显示用的房态文本。
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

    // 给主按钮显示下一步行动用。
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

    // 原型工具：为当前房间随机生成几个内部属性。
    // 以后新建存档时，应该由存档/生成系统统一调用类似逻辑。
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

    // 进入新房态时统一处理：状态、退房标记、Block 清理、计时器重置。
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

    // 状态改变时，状态停留时间从 0 重新计算。
    private void ResetStateTimer()
    {
        stateElapsedSeconds = 0f;
    }

    // Inspector 里手动改 currentState 时，同步 guestCheckedOut，减少初学阶段的手动错误。
    private void SyncCheckoutFlagForState()
    {
        guestCheckedOut = ShouldStateBeCheckedOut(currentState);
    }

    // Ready / Occupied / Blocked 都不是“已退房等待清洁”。
    private bool ShouldStateBeCheckedOut(Room2DState state)
    {
        return state != Room2DState.Ready
            && state != Room2DState.Occupied
            && state != Room2DState.Blocked;
    }

    // 把游戏小时转成易读文本，例如 8h 或 2d 4h。
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

    // 原型阶段的属性备注。正式版本可以改成更细的本地化文案。
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
