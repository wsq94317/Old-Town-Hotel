using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ToastView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI messageLabel;
    [SerializeField] private Image iconImage;

    [Header("Theme")]
    [SerializeField] private UITheme theme;

    [Header("Tuning")]
    [SerializeField] private float fadeInDuration = 0.16f;
    [SerializeField] private float fadeOutDuration = 0.32f;

    public void Show(string message, float visibleDurationSeconds = -1f)
    {
        if (gameObject.activeSelf) StopAllCoroutines();
        gameObject.SetActive(true);
        if (messageLabel != null) messageLabel.text = message ?? string.Empty;

        float duration = visibleDurationSeconds > 0f
            ? visibleDurationSeconds
            : (theme != null ? theme.toastDuration : 1.8f);

        StartCoroutine(AnimateLifecycle(duration));
    }

    public void HideImmediate()
    {
        StopAllCoroutines();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private IEnumerator AnimateLifecycle(float visibleDuration)
    {
        yield return Fade(0f, 1f, fadeInDuration);
        yield return new WaitForSecondsRealtime(visibleDuration);
        yield return Fade(1f, 0f, fadeOutDuration);
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null || duration <= 0f)
        {
            if (canvasGroup != null) canvasGroup.alpha = to;
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
