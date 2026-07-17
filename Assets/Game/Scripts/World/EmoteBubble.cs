using UnityEngine;

// 头顶表情气泡占位：小色块 quad（正式美术后换 emoji 贴图）。
// 💤=灰蓝(偷懒) / 🐌=棕(拖延标记) / ❗=红(抓包瞬间闪现)。
public class EmoteBubble : MonoBehaviour
{
    public enum Emote { None, Sleep, Delay, Alert, Grudge }

    private Renderer _renderer;
    private Emote _current = Emote.None;
    private FloorVisibilityController _floors;
    private static Material _matSleep, _matDelay, _matAlert, _matGrudge;

    public static EmoteBubble Attach(Transform parent)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.name = "Emote";
        quad.transform.SetParent(parent);
        quad.transform.localPosition = new Vector3(0.35f, 1.85f, 0);
        quad.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
        quad.AddComponent<BillboardSprite>();
        var bubble = quad.AddComponent<EmoteBubble>();
        bubble._renderer = quad.GetComponent<Renderer>();
        bubble.Show(Emote.None);
        return bubble;
    }

    public void Show(Emote emote)
    {
        _current = emote;
        if (_renderer == null) return;
        if (emote == Emote.None) { _renderer.enabled = false; return; }
        _renderer.enabled = true;
        _renderer.sharedMaterial = MaterialFor(emote);
    }

    // 按层显隐自己管：气泡挂在纸片人身上但比 AgentFloorVisibility 的缓存晚出生，
    // 且"隐藏中的气泡"不能被楼层逻辑掰回可见。
    private void LateUpdate()
    {
        if (_renderer == null) return;
        if (_floors == null) { _floors = FindFirstObjectByType<FloorVisibilityController>(); if (_floors == null) return; }
        _renderer.enabled = _current != Emote.None
            && FloorMath.FloorIndexForY(transform.position.y) == _floors.CurrentFloor;
    }

    private static Material MaterialFor(Emote e)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        switch (e)
        {
            case Emote.Sleep:
                if (_matSleep == null) _matSleep = new Material(shader) { color = new Color(0.55f, 0.65f, 0.85f) };
                return _matSleep;
            case Emote.Delay:
                if (_matDelay == null) _matDelay = new Material(shader) { color = new Color(0.6f, 0.4f, 0.2f) };
                return _matDelay;
            case Emote.Grudge:
                if (_matGrudge == null) _matGrudge = new Material(shader) { color = new Color(0.75f, 0.15f, 0.5f) };
                return _matGrudge;
            default:
                if (_matAlert == null) _matAlert = new Material(shader) { color = new Color(0.95f, 0.15f, 0.15f) };
                return _matAlert;
        }
    }
}
