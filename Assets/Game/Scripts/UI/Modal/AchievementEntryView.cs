using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AchievementEntryView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI descriptionLabel;
    [SerializeField] private GameObject lockedOverlay;
    [SerializeField] private Image progressFill;

    [Header("Theme")]
    [SerializeField] private UITheme theme;

    public void Setup(AchievementEntryInfo info)
    {
        if (info == null) return;
        if (iconImage != null) iconImage.sprite = info.icon;
        if (titleLabel != null) titleLabel.text = info.title ?? "";
        if (descriptionLabel != null) descriptionLabel.text = info.description ?? "";
        if (lockedOverlay != null) lockedOverlay.SetActive(!info.unlocked);
        if (progressFill != null)
        {
            progressFill.fillAmount = Mathf.Clamp01(info.progress);
            if (theme != null) progressFill.color = info.unlocked ? theme.successGreen : theme.secondaryGrey;
        }
    }
}
