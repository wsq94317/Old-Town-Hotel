using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum GuestPatienceState
{
    Calm = 0,
    Normal = 1,
    Impatient = 2,
    Critical = 3,
}

[DisallowMultipleComponent]
public sealed class GuestQueueCardView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI typeLabel;
    [SerializeField] private TextMeshProUGUI needLabel;
    [SerializeField] private TextMeshProUGUI waitLabel;
    [SerializeField] private Image moodIcon;
    [SerializeField] private Button tapButton;

    [Header("Mood sprites")]
    [SerializeField] private Sprite moodHappy;
    [SerializeField] private Sprite moodNormal;
    [SerializeField] private Sprite moodSad;
    [SerializeField] private Sprite moodAngry;

    [Header("Editor preview")]
    [SerializeField] private Sprite previewPortrait;
    [SerializeField] private string previewType = "BUSINESS";
    [SerializeField] private string previewNeed = "KING ROOM";
    [SerializeField] private int previewWaitMinutes = 12;
    [SerializeField] private GuestPatienceState previewMood = GuestPatienceState.Normal;

    public event Action<GuestQueueCardView> OnTapped;

    public object BoundGuestRef { get; private set; }

    public void Bind(object guestRef, Sprite portrait, string guestType, string roomNeed, string waitText, GuestPatienceState mood)
    {
        BoundGuestRef = guestRef;
        if (portraitImage != null) portraitImage.sprite = portrait;
        if (typeLabel != null) typeLabel.text = guestType ?? string.Empty;
        if (needLabel != null) needLabel.text = roomNeed ?? string.Empty;
        if (waitLabel != null) waitLabel.text = waitText ?? string.Empty;
        if (moodIcon != null) moodIcon.sprite = SpriteForMood(mood);
    }

    private Sprite SpriteForMood(GuestPatienceState mood)
    {
        switch (mood)
        {
            case GuestPatienceState.Calm:      return moodHappy;
            case GuestPatienceState.Normal:    return moodNormal;
            case GuestPatienceState.Impatient: return moodSad;
            case GuestPatienceState.Critical:  return moodAngry;
            default:                           return moodNormal;
        }
    }

    private void Awake()
    {
        if (tapButton != null) tapButton.onClick.AddListener(HandleTap);
    }

    private void OnDestroy()
    {
        if (tapButton != null) tapButton.onClick.RemoveListener(HandleTap);
    }

    private void HandleTap() => OnTapped?.Invoke(this);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        Bind(null, previewPortrait, previewType, previewNeed, $"{previewWaitMinutes} min", previewMood);
    }
#endif
}
