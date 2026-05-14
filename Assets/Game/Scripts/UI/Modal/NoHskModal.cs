using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NoHskModal : ModalBase
{
    [Header("Modal 5 content")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI bodyLabel;
    [SerializeField] private Button acknowledgeButton;

    public override bool DismissOnBackdropTap => false;

    public event Action OnAcknowledged;

    public void Setup(string title = null, string body = null)
    {
        if (titleLabel != null) titleLabel.text = string.IsNullOrEmpty(title) ? "无可用 HSK" : title;
        if (bodyLabel != null) bodyLabel.text = string.IsNullOrEmpty(body) ? "目前 HSK 正在工作中，请稍后再试或等待完成。" : body;
    }

    protected override void OnOpened()
    {
        if (acknowledgeButton != null)
        {
            acknowledgeButton.onClick.RemoveAllListeners();
            acknowledgeButton.onClick.AddListener(HandleAck);
        }
    }

    private void HandleAck()
    {
        OnAcknowledged?.Invoke();
        Close();
    }
}
