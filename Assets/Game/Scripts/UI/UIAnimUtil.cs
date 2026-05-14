using System.Collections;
using UnityEngine;

public static class UIAnimUtil
{
    public static IEnumerator ScalePulse(Transform target, float fromScale, float toScale, float duration)
    {
        if (target == null) yield break;
        float t = 0f;
        Vector3 from = Vector3.one * fromScale;
        Vector3 to = Vector3.one * toScale;
        target.localScale = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            target.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        target.localScale = to;
    }

    public static IEnumerator ColorFade(UnityEngine.UI.Graphic target, Color from, Color to, float duration)
    {
        if (target == null) yield break;
        float t = 0f;
        target.color = from;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            target.color = Color.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        target.color = to;
    }

    public static IEnumerator Shake(Transform target, float amplitudePixels, float duration, int cycles = 3)
    {
        if (target == null) yield break;
        Vector3 origin = target.localPosition;
        float cycleDuration = duration / cycles;
        for (int i = 0; i < cycles; i++)
        {
            float t = 0f;
            while (t < cycleDuration)
            {
                t += Time.unscaledDeltaTime;
                float phase = (t / cycleDuration) * Mathf.PI * 2f;
                target.localPosition = origin + new Vector3(Mathf.Sin(phase) * amplitudePixels, 0f, 0f);
                yield return null;
            }
        }
        target.localPosition = origin;
    }
}
