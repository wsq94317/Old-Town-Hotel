using TMPro;
using UnityEngine;

public class Room2DSelectionManager : MonoBehaviour
{
    public Room2DController selectedRoom;
    public TMP_Text selectedRoomLabel;

    private void Start()
    {
        if (selectedRoom == null)
        {
            selectedRoom = FindFirstObjectByType<Room2DController>();
        }

        ApplySelection();
    }

    public void SelectRoom(Room2DController room)
    {
        if (selectedRoom != null)
        {
            selectedRoom.SetSelected(false);
        }

        selectedRoom = room;
        ApplySelection();
    }

    public void PerformNextActionOnSelectedRoom()
    {
        if (selectedRoom != null)
        {
            selectedRoom.PerformNextAction();
            ApplySelection();
        }
    }

    private void ApplySelection()
    {
        if (selectedRoom != null)
        {
            selectedRoom.SetSelected(true);
        }

        if (selectedRoomLabel != null)
        {
            selectedRoomLabel.text = GetSelectedRoomDisplayName();
        }
    }

    private string GetSelectedRoomDisplayName()
    {
        if (selectedRoom == null || selectedRoom.roomEntity == null)
        {
            return "Selected: None";
        }

        return "Selected: " + selectedRoom.roomEntity.roomName;
    }
}
