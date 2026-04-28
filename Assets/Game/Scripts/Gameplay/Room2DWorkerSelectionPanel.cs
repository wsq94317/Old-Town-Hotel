using UnityEngine;

// 最小 worker 选择面板逻辑。
// 它不做自动派工，只让玩家明确选择一个 worker，再手动派到当前选中的房间。
public class Room2DWorkerSelectionPanel : MonoBehaviour
{
    public enum PrototypeWorkerType
    {
        None,
        Housekeeper,
        Inspector
    }

    // 自动寻找场景里的 worker 和房间选择器，减少 Unity 初学阶段的拖拽绑定。
    public bool autoFindReferences = true;
    public Room2DSelectionManager selectionManager;
    public Housekeeper2D[] housekeepers;
    public Inspector2D inspector;

    [Header("Selected Worker")]
    // 当前玩家选中的 worker。只有被选中的 worker 会响应 Assign Selected Worker。
    public PrototypeWorkerType selectedWorkerType = PrototypeWorkerType.Housekeeper;
    public int selectedHousekeeperIndex;
    public string selectedWorkerName = "None";
    public string lastManualAssignmentResult = "None";

    private void Start()
    {
        FindReferencesIfNeeded();
        RefreshSelectedWorkerName();
    }

    [ContextMenu("Select Housekeeper")]
    public void SelectHousekeeper()
    {
        FindReferencesIfNeeded();

        selectedWorkerType = PrototypeWorkerType.Housekeeper;
        selectedHousekeeperIndex = Mathf.Clamp(selectedHousekeeperIndex, 0, GetLastHousekeeperIndex());
        RefreshSelectedWorkerName();
    }

    [ContextMenu("Select Next Housekeeper")]
    public void SelectNextHousekeeper()
    {
        FindReferencesIfNeeded();

        selectedWorkerType = PrototypeWorkerType.Housekeeper;

        if (housekeepers == null || housekeepers.Length == 0)
        {
            selectedHousekeeperIndex = 0;
            RefreshSelectedWorkerName();
            return;
        }

        selectedHousekeeperIndex++;
        if (selectedHousekeeperIndex >= housekeepers.Length)
        {
            selectedHousekeeperIndex = 0;
        }

        RefreshSelectedWorkerName();
    }

    [ContextMenu("Select Inspector")]
    public void SelectInspector()
    {
        FindReferencesIfNeeded();

        selectedWorkerType = PrototypeWorkerType.Inspector;
        RefreshSelectedWorkerName();
    }

    [ContextMenu("Assign Selected Worker To Selected Room")]
    public void AssignSelectedWorkerToSelectedRoom()
    {
        FindReferencesIfNeeded();

        Room2DEntity selectedRoom = GetSelectedRoomEntity();
        if (selectedRoom == null)
        {
            lastManualAssignmentResult = "Assign failed: no selected room";
            return;
        }

        if (selectedWorkerType == PrototypeWorkerType.Housekeeper)
        {
            AssignSelectedHousekeeper(selectedRoom);
            return;
        }

        if (selectedWorkerType == PrototypeWorkerType.Inspector)
        {
            AssignSelectedInspector(selectedRoom);
            return;
        }

        lastManualAssignmentResult = "Assign failed: no selected worker";
    }

    public string GetWorkerPanelText()
    {
        FindReferencesIfNeeded();
        RefreshSelectedWorkerName();

        return "Workers\n"
            + "Selected: " + selectedWorkerName + "\n"
            + "Target Room: " + GetSelectedRoomName() + "\n"
            + "Manual: " + lastManualAssignmentResult + "\n"
            + BuildHousekeeperListText() + "\n"
            + BuildInspectorText();
    }

    private void AssignSelectedHousekeeper(Room2DEntity selectedRoom)
    {
        Housekeeper2D housekeeper = GetSelectedHousekeeper();
        if (housekeeper == null)
        {
            lastManualAssignmentResult = "Assign failed: no housekeeper";
            return;
        }

        bool assigned = housekeeper.AssignRoom(selectedRoom);
        lastManualAssignmentResult = assigned
            ? GetHousekeeperDisplayName(selectedHousekeeperIndex) + " -> " + selectedRoom.roomName
            : GetHousekeeperDisplayName(selectedHousekeeperIndex) + " failed: " + selectedRoom.GetStateDisplayName();
    }

    private void AssignSelectedInspector(Room2DEntity selectedRoom)
    {
        if (inspector == null)
        {
            lastManualAssignmentResult = "Assign failed: no inspector";
            return;
        }

        bool assigned = inspector.AssignRoom(selectedRoom);
        lastManualAssignmentResult = assigned
            ? GetInspectorDisplayName() + " -> " + selectedRoom.roomName
            : GetInspectorDisplayName() + " failed: " + selectedRoom.GetStateDisplayName();
    }

    private void FindReferencesIfNeeded()
    {
        if (!autoFindReferences)
        {
            return;
        }

        if (selectionManager == null)
        {
            selectionManager = FindFirstObjectByType<Room2DSelectionManager>();
        }

        if (housekeepers == null || housekeepers.Length == 0)
        {
            housekeepers = FindObjectsByType<Housekeeper2D>(FindObjectsSortMode.None);
            SortHousekeepersByName();
        }

        if (inspector == null)
        {
            inspector = FindFirstObjectByType<Inspector2D>();
        }
    }

    private Room2DEntity GetSelectedRoomEntity()
    {
        if (selectionManager == null || selectionManager.selectedRoom == null)
        {
            return null;
        }

        return selectionManager.selectedRoom.roomEntity;
    }

    private string GetSelectedRoomName()
    {
        Room2DEntity room = GetSelectedRoomEntity();
        return room != null ? room.roomName : "None";
    }

    private Housekeeper2D GetSelectedHousekeeper()
    {
        if (housekeepers == null || housekeepers.Length == 0)
        {
            return null;
        }

        selectedHousekeeperIndex = Mathf.Clamp(selectedHousekeeperIndex, 0, housekeepers.Length - 1);
        return housekeepers[selectedHousekeeperIndex];
    }

    private void RefreshSelectedWorkerName()
    {
        if (selectedWorkerType == PrototypeWorkerType.Housekeeper)
        {
            selectedWorkerName = GetSelectedHousekeeper() != null
                ? GetHousekeeperDisplayName(selectedHousekeeperIndex)
                : "HSK None";
            return;
        }

        if (selectedWorkerType == PrototypeWorkerType.Inspector)
        {
            selectedWorkerName = inspector != null ? GetInspectorDisplayName() : "Inspector None";
            return;
        }

        selectedWorkerName = "None";
    }

    private string BuildHousekeeperListText()
    {
        if (housekeepers == null || housekeepers.Length == 0)
        {
            return "HSK: None";
        }

        string text = "Housekeepers";
        for (int i = 0; i < housekeepers.Length; i++)
        {
            Housekeeper2D housekeeper = housekeepers[i];
            if (housekeeper == null)
            {
                continue;
            }

            string selectedMark = selectedWorkerType == PrototypeWorkerType.Housekeeper && i == selectedHousekeeperIndex ? "> " : "- ";
            text += "\n" + selectedMark + GetHousekeeperDisplayName(i)
                + ": " + housekeeper.currentState
                + " / " + housekeeper.assignedRoomName
                + " / " + Mathf.FloorToInt(housekeeper.cleaningTimerSeconds) + "s";
        }

        return text;
    }

    private string BuildInspectorText()
    {
        if (inspector == null)
        {
            return "Inspector: None";
        }

        string selectedMark = selectedWorkerType == PrototypeWorkerType.Inspector ? "> " : "- ";
        return "Inspector\n"
            + selectedMark + GetInspectorDisplayName()
            + ": " + inspector.currentState
            + " / " + inspector.assignedRoomName
            + " / " + Mathf.FloorToInt(inspector.inspectionTimerSeconds) + "s";
    }

    private string GetHousekeeperDisplayName(int index)
    {
        Housekeeper2D housekeeper = housekeepers != null && index >= 0 && index < housekeepers.Length
            ? housekeepers[index]
            : null;

        if (housekeeper != null && !string.IsNullOrEmpty(housekeeper.name))
        {
            return housekeeper.name;
        }

        return "HSK " + (index + 1);
    }

    private string GetInspectorDisplayName()
    {
        if (inspector != null && !string.IsNullOrEmpty(inspector.name))
        {
            return inspector.name;
        }

        return "Inspector";
    }

    private int GetLastHousekeeperIndex()
    {
        if (housekeepers == null || housekeepers.Length == 0)
        {
            return 0;
        }

        return housekeepers.Length - 1;
    }

    private void SortHousekeepersByName()
    {
        if (housekeepers == null)
        {
            return;
        }

        for (int i = 0; i < housekeepers.Length - 1; i++)
        {
            for (int j = i + 1; j < housekeepers.Length; j++)
            {
                string leftName = housekeepers[i] != null ? housekeepers[i].name : "";
                string rightName = housekeepers[j] != null ? housekeepers[j].name : "";
                if (string.CompareOrdinal(rightName, leftName) < 0)
                {
                    Housekeeper2D temp = housekeepers[i];
                    housekeepers[i] = housekeepers[j];
                    housekeepers[j] = temp;
                }
            }
        }
    }
}
