using UnityEngine;

// 点击/拖动判定（纯 C#，EditMode 可测）：
// 按下后累计位移一旦超过阈值即进入 Drag（本次按压永久失去 Tap 资格，
// 即使拖回原点）；松手时位移仍在阈值内才算 Tap。
public sealed class TapDragClassifier
{
    public enum Result { None, Tap, Drag }

    private readonly float _dragThresholdPixels;
    private Vector2 _pressPos;
    private bool _pressed;

    public bool IsDragging { get; private set; }

    public TapDragClassifier(float dragThresholdPixels)
    {
        _dragThresholdPixels = Mathf.Max(1f, dragThresholdPixels);
    }

    public void Press(Vector2 screenPos)
    {
        _pressed = true;
        IsDragging = false;
        _pressPos = screenPos;
    }

    public void Move(Vector2 screenPos)
    {
        if (!_pressed || IsDragging) return;
        if (Vector2.Distance(screenPos, _pressPos) > _dragThresholdPixels)
        {
            IsDragging = true;
        }
    }

    public Result Release(Vector2 screenPos)
    {
        if (!_pressed) return Result.None;
        Move(screenPos); // 松手瞬间的位移也计入判定（必须先于清除按压态）
        _pressed = false;
        bool wasDrag = IsDragging;
        IsDragging = false;
        return wasDrag ? Result.Drag : Result.Tap;
    }
}
