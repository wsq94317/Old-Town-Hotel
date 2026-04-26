using TMPro;
using UnityEngine;

public class Room2DSelectionManager : MonoBehaviour
{
    public bool autoFindRooms = true;
    public Room2DController selectedRoom;
    public Room2DController[] rooms;
    public TMP_Text selectedRoomLabel;

    private void Start()
    {
        FindRoomsIfNeeded();

        if (selectedRoom == null)
        {
            selectedRoom = GetFirstRoom();
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

    public void SelectNextRoom()
    {
        SelectRoomByOffset(1);
    }

    public void SelectPreviousRoom()
    {
        SelectRoomByOffset(-1);
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

    private void FindRoomsIfNeeded()
    {
        if (autoFindRooms || rooms == null || rooms.Length == 0)
        {
            rooms = FindObjectsByType<Room2DController>(FindObjectsSortMode.None);
            SortRoomsByRoomNumber();
        }
    }

    private Room2DController GetFirstRoom()
    {
        FindRoomsIfNeeded();

        if (rooms == null || rooms.Length == 0)
        {
            return null;
        }

        return rooms[0];
    }

    private void SelectRoomByOffset(int offset)
    {
        FindRoomsIfNeeded();

        if (rooms == null || rooms.Length == 0)
        {
            return;
        }

        int currentIndex = GetSelectedRoomIndex();
        int nextIndex = currentIndex + offset;

        if (nextIndex < 0)
        {
            nextIndex = rooms.Length - 1;
        }
        else if (nextIndex >= rooms.Length)
        {
            nextIndex = 0;
        }

        SelectRoom(rooms[nextIndex]);
    }

    private int GetSelectedRoomIndex()
    {
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] == selectedRoom)
            {
                return i;
            }
        }

        return 0;
    }

    private void SortRoomsByRoomNumber()
    {
        if (rooms == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Length - 1; i++)
        {
            for (int j = i + 1; j < rooms.Length; j++)
            {
                if (GetRoomNumber(rooms[j]) < GetRoomNumber(rooms[i]))
                {
                    Room2DController tempRoom = rooms[i];
                    rooms[i] = rooms[j];
                    rooms[j] = tempRoom;
                }
            }
        }
    }

    private int GetRoomNumber(Room2DController room)
    {
        if (room != null && room.roomEntity != null)
        {
            return room.roomEntity.roomNumber;
        }

        return int.MaxValue;
    }
}
