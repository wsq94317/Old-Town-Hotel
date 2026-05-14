using System;
using UnityEngine;
using UnityEngine.UI;

public enum HotelTab
{
    FrontDesk = 0,
    Rooms = 1,
    Lounge = 2,
}

[DisallowMultipleComponent]
public sealed class BottomNavView : MonoBehaviour
{
    [Header("Wiring — 3 tab buttons")]
    [SerializeField] private Button frontDeskButton;
    [SerializeField] private Button roomsButton;
    [SerializeField] private Button loungeButton;

    [Header("Tab graphics (Image components on each tab)")]
    [SerializeField] private Image frontDeskIcon;
    [SerializeField] private Image roomsIcon;
    [SerializeField] private Image loungeIcon;

    [Header("Sprite pairs — Active / Inactive per tab")]
    [SerializeField] private Sprite frontDeskActive;
    [SerializeField] private Sprite frontDeskInactive;
    [SerializeField] private Sprite roomsActive;
    [SerializeField] private Sprite roomsInactive;
    [SerializeField] private Sprite loungeActive;
    [SerializeField] private Sprite loungeInactive;

    [Header("Editor preview")]
    [SerializeField] private HotelTab previewActive = HotelTab.FrontDesk;

    public event Action<HotelTab> OnTabSelected;

    public HotelTab CurrentTab { get; private set; } = HotelTab.FrontDesk;

    public void SetActiveTab(HotelTab tab)
    {
        CurrentTab = tab;
        ApplySpriteSwap(tab);
    }

    private void ApplySpriteSwap(HotelTab tab)
    {
        if (frontDeskIcon != null)
            frontDeskIcon.sprite = (tab == HotelTab.FrontDesk) ? frontDeskActive : frontDeskInactive;
        if (roomsIcon != null)
            roomsIcon.sprite = (tab == HotelTab.Rooms) ? roomsActive : roomsInactive;
        if (loungeIcon != null)
            loungeIcon.sprite = (tab == HotelTab.Lounge) ? loungeActive : loungeInactive;
    }

    private void Awake()
    {
        if (frontDeskButton != null) frontDeskButton.onClick.AddListener(() => Select(HotelTab.FrontDesk));
        if (roomsButton != null)     roomsButton.onClick.AddListener(() => Select(HotelTab.Rooms));
        if (loungeButton != null)    loungeButton.onClick.AddListener(() => Select(HotelTab.Lounge));
    }

    private void OnDestroy()
    {
        if (frontDeskButton != null) frontDeskButton.onClick.RemoveAllListeners();
        if (roomsButton != null)     roomsButton.onClick.RemoveAllListeners();
        if (loungeButton != null)    loungeButton.onClick.RemoveAllListeners();
    }

    private void Select(HotelTab tab)
    {
        if (CurrentTab == tab) return;
        SetActiveTab(tab);
        OnTabSelected?.Invoke(tab);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        ApplySpriteSwap(previewActive);
    }
#endif
}
