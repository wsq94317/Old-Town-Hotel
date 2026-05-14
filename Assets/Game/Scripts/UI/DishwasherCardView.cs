using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DishwasherCardView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private TextMeshProUGUI remainingLabel;
    [SerializeField] private Image progressFill;

    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Editor preview")]
    [SerializeField] private bool previewWashing = true;
    [SerializeField] private float previewRemaining = 35f;
    [SerializeField] private float previewDuration = 60f;

    public void Bind(bool washing, float remainingSeconds, float totalDurationSeconds)
    {
        if (statusLabel != null)
            statusLabel.text = washing ? "运行中" : "空闲";

        if (remainingLabel != null)
        {
            if (!washing)
            {
                remainingLabel.text = "";
                remainingLabel.gameObject.SetActive(false);
            }
            else
            {
                remainingLabel.gameObject.SetActive(true);
                remainingLabel.text = $"剩余时间: {Mathf.CeilToInt(remainingSeconds)}s";
            }
        }

        if (progressFill != null)
        {
            float ratio = 0f;
            if (washing && totalDurationSeconds > 0f)
                ratio = Mathf.Clamp01(1f - (remainingSeconds / totalDurationSeconds));
            progressFill.fillAmount = ratio;
            if (theme != null) progressFill.color = theme.stateCleaning;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        Bind(previewWashing, previewRemaining, previewDuration);
    }
#endif
}
