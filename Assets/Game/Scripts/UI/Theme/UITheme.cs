using UnityEngine;

[CreateAssetMenu(fileName = "UITheme", menuName = "Old Town Hotel/UI Theme", order = 110)]
public sealed class UITheme : ScriptableObject
{
    [Header("Palette — page & structure")]
    public Color creamPage   = Hex("#FFF7E8");
    public Color creamSoft   = Hex("#F3E6D0");
    public Color brownDeep   = Hex("#B85E3C");
    public Color goldAccent  = Hex("#C99A4A");

    [Header("Room state colors")]
    public Color stateReady    = Hex("#6A9F5C");
    public Color stateDirty    = Hex("#B85842");
    public Color stateCleaning = Hex("#4A6FA5");
    public Color stateInsp     = Hex("#84598E");
    public Color stateOccupied = Hex("#6E4E3A");
    public Color stateBlocked  = Hex("#6B6B6B");

    [Header("Auxiliary colors")]
    public Color warnRed       = Hex("#A8442E");
    public Color infoBlue      = Hex("#5A7A9E");
    public Color successGreen  = Hex("#6A9F5C");
    public Color secondaryGrey = Hex("#8A7E6E");

    [Header("Typography (size in points)")]
    public float titleLg = 24f;
    public float titleMd = 20f;
    public float titleSm = 18f;
    public float bodyMd  = 16f;
    public float bodySm  = 14f;
    public float aux     = 12f;

    [Header("Animation timings (seconds)")]
    public float modalAnim       = 0.16f;
    public float stateColorFade  = 0.20f;
    public float buttonPress     = 0.08f;
    public float toastDuration   = 1.80f;
    public float warningShake    = 0.24f;

    [Header("UI refresh")]
    public float pollFallbackHz = 4f;

    private void Reset()
    {
        creamPage      = Hex("#FFF7E8");
        creamSoft      = Hex("#F3E6D0");
        brownDeep      = Hex("#B85E3C");
        goldAccent     = Hex("#C99A4A");
        stateReady     = Hex("#6A9F5C");
        stateDirty     = Hex("#B85842");
        stateCleaning  = Hex("#4A6FA5");
        stateInsp      = Hex("#84598E");
        stateOccupied  = Hex("#6E4E3A");
        stateBlocked   = Hex("#6B6B6B");
        warnRed        = Hex("#A8442E");
        infoBlue       = Hex("#5A7A9E");
        successGreen   = Hex("#6A9F5C");
        secondaryGrey  = Hex("#8A7E6E");
    }

    private static Color Hex(string code)
    {
        return ColorUtility.TryParseHtmlString(code, out var c) ? c : Color.magenta;
    }
}
