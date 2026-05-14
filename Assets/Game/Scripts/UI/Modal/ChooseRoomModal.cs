using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChooseRoomModal : ModalBase
{
    [Header("Modal 1 content")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private Transform roomRowRoot;
    [SerializeField] private ChooseRoomRowView roomRowPrefab;
    [SerializeField] private GameObject emptyBanner;
    [SerializeField] private TextMeshProUGUI emptyBannerLabel;
    [SerializeField] private Button gotoRoomsButton;
    [SerializeField] private Button cancelButton;

    public event Action<Room2DEntity> OnRoomSelected;
    public event Action OnGotoRoomsRequested;
    public event Action OnCancelled;

    private readonly List<ChooseRoomRowView> spawnedRows = new List<ChooseRoomRowView>();

    public void Setup(IList<RoomSuitability> readyRooms)
    {
        if (titleLabel != null) titleLabel.text = "选择可用房间";

        ClearRows();

        bool empty = readyRooms == null || readyRooms.Count == 0;
        if (emptyBanner != null) emptyBanner.SetActive(empty);
        if (gotoRoomsButton != null) gotoRoomsButton.gameObject.SetActive(empty);
        if (emptyBannerLabel != null && empty)
            emptyBannerLabel.text = "没有合适的可用房间，需要清洁/检查房间";

        if (!empty)
        {
            for (int i = 0; i < readyRooms.Count; i++)
            {
                var entry = readyRooms[i];
                if (entry.Room == null) continue;
                if (roomRowPrefab == null || roomRowRoot == null) break;
                var row = Instantiate(roomRowPrefab, roomRowRoot);
                row.Setup(entry.Room, entry.Rank);
                row.OnRowTapped += HandleRowTapped;
                spawnedRows.Add(row);
            }
        }
    }

    protected override void OnOpened()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => { OnCancelled?.Invoke(); Close(); });
        }
        if (gotoRoomsButton != null)
        {
            gotoRoomsButton.onClick.RemoveAllListeners();
            gotoRoomsButton.onClick.AddListener(() => { OnGotoRoomsRequested?.Invoke(); Close(); });
        }
    }

    protected override void OnClosing()
    {
        ClearRows();
    }

    private void HandleRowTapped(Room2DEntity room)
    {
        OnRoomSelected?.Invoke(room);
        Close();
    }

    private void ClearRows()
    {
        foreach (var row in spawnedRows)
        {
            if (row == null) continue;
            row.OnRowTapped -= HandleRowTapped;
            if (row.gameObject != null) Destroy(row.gameObject);
        }
        spawnedRows.Clear();
    }
}
