using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorkerStatusCardView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI roleLabel;
    [SerializeField] private TextMeshProUGUI activityLabel;
    [SerializeField] private TextMeshProUGUI targetRoomLabel;
    [SerializeField] private TextMeshProUGUI remainingLabel;
    [SerializeField] private Button detailsButton;

    [Header("Editor preview")]
    [SerializeField] private Sprite previewPortrait;
    [SerializeField] private string previewRole = "HOUSEKEEPER (HSK)";
    [SerializeField] private string previewActivity = "Cleaning";
    [SerializeField] private int previewTargetRoom = 201;
    [SerializeField] private float previewRemaining = 45f;
    [SerializeField] private bool previewBusy = true;

    public event Action OnDetailsClicked;

    public void Bind(Sprite portrait, string roleLabelText, string activity, int? assignedRoomNumber, float remainingSeconds, bool isBusy)
    {
        if (portraitImage != null) portraitImage.sprite = portrait;
        if (roleLabel != null) roleLabel.text = roleLabelText ?? string.Empty;
        if (activityLabel != null) activityLabel.text = activity ?? string.Empty;

        if (targetRoomLabel != null)
        {
            if (isBusy && assignedRoomNumber.HasValue)
            {
                targetRoomLabel.gameObject.SetActive(true);
                targetRoomLabel.text = $"Target: Room {assignedRoomNumber.Value}";
            }
            else
            {
                targetRoomLabel.gameObject.SetActive(false);
            }
        }

        if (remainingLabel != null)
        {
            if (isBusy && remainingSeconds > 0f)
            {
                remainingLabel.gameObject.SetActive(true);
                remainingLabel.text = $"Remaining: {Mathf.CeilToInt(remainingSeconds)}s";
            }
            else
            {
                remainingLabel.gameObject.SetActive(false);
            }
        }
    }

    private void Awake()
    {
        if (detailsButton != null) detailsButton.onClick.AddListener(HandleDetailsClicked);
    }

    private void OnDestroy()
    {
        if (detailsButton != null) detailsButton.onClick.RemoveListener(HandleDetailsClicked);
    }

    private void HandleDetailsClicked() => OnDetailsClicked?.Invoke();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        Bind(previewPortrait, previewRole, previewActivity,
             previewBusy ? previewTargetRoom : (int?)null,
             previewRemaining, previewBusy);
    }
#endif
}
