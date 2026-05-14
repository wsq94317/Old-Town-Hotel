using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AssignHskModal : ModalBase
{
    [Header("Modal 4 content")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [SerializeField] private TextMeshProUGUI estimatedCompletionLabel;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    public event Action OnConfirmed;
    public event Action OnCancelled;

    public void Setup(int targetRoomNumber, string hskCurrentActivity, float estimatedCompletionSeconds)
    {
        if (titleLabel != null) titleLabel.text = "分配 HSK";
        if (bodyLabel != null) bodyLabel.text = $"当前状态: {hskCurrentActivity}\n目标房间: {targetRoomNumber}";
        if (estimatedCompletionLabel != null)
            estimatedCompletionLabel.text = estimatedCompletionSeconds > 0
                ? $"预计完成: 约 {Mathf.CeilToInt(estimatedCompletionSeconds)}s"
                : "";
    }

    protected override void OnOpened()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(HandleConfirm);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(HandleCancel);
        }
    }

    private void HandleConfirm()
    {
        OnConfirmed?.Invoke();
        Close();
    }

    private void HandleCancel()
    {
        OnCancelled?.Invoke();
        Close();
    }
}
