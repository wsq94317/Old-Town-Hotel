using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public abstract class ModalBase : MonoBehaviour
{
    [Header("Required wiring")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform contentTransform;

    [Header("Optional wiring")]
    [SerializeField] private Button backdropButton;

    public virtual bool DismissOnBackdropTap => true;

    private Action onClosedCallback;
    private bool isAnimating;

    internal void OnOpenedByManager(Action onClosed)
    {
        onClosedCallback = onClosed;
        if (backdropButton != null)
        {
            backdropButton.onClick.RemoveAllListeners();
            backdropButton.onClick.AddListener(OnBackdropTap);
        }
        OnOpened();
        StartCoroutine(AnimateIn(GetAnimationDuration()));
    }

    public void Close()
    {
        if (isAnimating || onClosedCallback == null) return;
        OnClosing();
        StartCoroutine(AnimateOutThenDestroy(GetAnimationDuration()));
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosing() { }

    private void OnBackdropTap()
    {
        if (!DismissOnBackdropTap) return;
        Close();
    }

    private float GetAnimationDuration()
    {
        var theme = ModalManager.Instance != null ? ModalManager.Instance.Theme : null;
        return theme != null ? theme.modalAnim : 0.16f;
    }

    private IEnumerator AnimateIn(float duration)
    {
        isAnimating = true;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (contentTransform != null) contentTransform.localScale = Vector3.one * 0.92f;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (canvasGroup != null) canvasGroup.alpha = k;
            if (contentTransform != null) contentTransform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (contentTransform != null) contentTransform.localScale = Vector3.one;
        isAnimating = false;
    }

    private IEnumerator AnimateOutThenDestroy(float duration)
    {
        isAnimating = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (canvasGroup != null) canvasGroup.alpha = 1f - k;
            if (contentTransform != null) contentTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, k);
            yield return null;
        }

        var cb = onClosedCallback;
        onClosedCallback = null;
        cb?.Invoke();
        Destroy(gameObject);
    }
}
