using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ActiveGuestCardView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI typeLabel;
    [SerializeField] private TextMeshProUGUI requiredRoomLabel;
    [SerializeField] private TextMeshProUGUI preferenceLabel;
    [SerializeField] private TextMeshProUGUI waitLabel;
    [SerializeField] private TextMeshProUGUI moodLabel;
    [SerializeField] private TextMeshProUGUI notesLabel;
    [SerializeField] private Button ctaButton;
    [SerializeField] private TextMeshProUGUI ctaButtonLabel;
    [SerializeField] private GameObject emptyStatePlaceholder;
    [SerializeField] private TextMeshProUGUI emptyStateLabel; // optional message inside the placeholder
    [SerializeField] private GameObject contentRoot;

    [Header("Editor preview")]
    [SerializeField] private bool previewHasActiveGuest = true;
    [SerializeField] private Sprite previewPortrait;
    [SerializeField] private string previewType = "BUSINESS GUEST";
    [SerializeField] private string previewRequiredRoom = "KING ROOM";
    [SerializeField] private string previewPreference = "Prefers: High floor, Quiet side";
    [SerializeField] private string previewWait = "Waiting: 12 min";
    [SerializeField] private string previewMood = "Mood: Normal";
    [SerializeField] private string previewNotes = "Note: Wants quick check-in";
    [SerializeField] private string previewCtaLabel = "Check Available Rooms";

    public event Action OnCtaClicked;

    public object BoundGuestRef { get; private set; }

    public void BindEmpty(string message = null)
    {
        BoundGuestRef = null;
        if (contentRoot != null) contentRoot.SetActive(false);
        if (emptyStatePlaceholder != null) emptyStatePlaceholder.SetActive(true);
        if (emptyStateLabel != null && !string.IsNullOrEmpty(message)) emptyStateLabel.text = message;
    }

    public void Bind(object guestRef, Sprite portrait, string type, string requiredRoom,
                     string preference, string wait, string mood, string notes, string ctaLabel)
    {
        BoundGuestRef = guestRef;
        if (contentRoot != null) contentRoot.SetActive(true);
        if (emptyStatePlaceholder != null) emptyStatePlaceholder.SetActive(false);

        if (portraitImage != null)   portraitImage.sprite = portrait;
        if (typeLabel != null)       typeLabel.text = type ?? string.Empty;
        if (requiredRoomLabel != null) requiredRoomLabel.text = requiredRoom ?? string.Empty;
        if (preferenceLabel != null) preferenceLabel.text = preference ?? string.Empty;
        if (waitLabel != null)       waitLabel.text = wait ?? string.Empty;
        if (moodLabel != null)       moodLabel.text = mood ?? string.Empty;
        if (notesLabel != null)      notesLabel.text = notes ?? string.Empty;
        SetCtaLabel(ctaLabel);
    }

    public void SetCtaLabel(string label)
    {
        if (ctaButtonLabel != null) ctaButtonLabel.text = label ?? string.Empty;
    }

    public void SetCtaInteractable(bool value)
    {
        if (ctaButton != null) ctaButton.interactable = value;
    }

    private void Awake()
    {
        if (ctaButton != null) ctaButton.onClick.AddListener(HandleCtaClicked);
    }

    private void OnDestroy()
    {
        if (ctaButton != null) ctaButton.onClick.RemoveListener(HandleCtaClicked);
    }

    private void HandleCtaClicked() => OnCtaClicked?.Invoke();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (previewHasActiveGuest)
        {
            Bind(null, previewPortrait, previewType, previewRequiredRoom,
                 previewPreference, previewWait, previewMood, previewNotes, previewCtaLabel);
        }
        else
        {
            BindEmpty();
        }
    }
#endif
}
