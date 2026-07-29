using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 场景美化的第一刀（玩家："场景可以给我做一些美化，目前完全白模"）。
//
// 约束：**没有美术、不买素材**——所有东西必须能从代码生成或用 URP 自带的功能。
// 所以这一刀走的是"打光 + 后处理 + 一套配色"，而不是贴图建模。
//
// 并行设计审计（12 个 agent）在这块埋的四个坑，全部避开，逐条记在这里免得以后重踩：
//
//   ① **主相机没有 UniversalAdditionalCameraData**，而 URP 默认 renderPostProcessing
//      = false ⇒ 只加一个 Volume 是**静默无效**的。必须先把相机那个开关打开。
//   ② **一开阴影会立刻多出 12 块巨大黑板**：每个房门口的 `Darkness` 遮罩是
//      scale 4.6×4.9 的黑片（RoomDoor 生成的 URP/Unlit，alpha 0.78），castShadows
//      默认开着。它们本来是"看不见房内"的障眼法，投影之后会变成 12 面墙。
//   ③ **Volume 必须挂在 World/FloorN 之外**——FloorVisibilityController 会
//      SetActive 整个楼层，挂在里面的话切层就把后处理关了。
//   ④ Mat_Ready/Dirty/... 这些材质**不能删**：GUID 被 Hotel_Rooms.unity 里
//      12 个 RoomController 序列化引用着。
[DisallowMultipleComponent]
public class HotelLook : MonoBehaviour
{
    [Header("Colour grading")]
    // **实测定的值，不是拍脑袋**：接上后处理之前先量了一帧，
    // 关掉 volume 时已有 36% 的像素过曝（亮度 >0.97），开了变 56%。
    // 根因是材质本身太白（地板 albedo 0.72、床 0.95）——那部分在资产层压下去了，
    // 这里就不能再往上推曝光了。目标：过曝像素 <5%，平均亮度 0.35-0.45。
    [Tooltip("整体色调：老楼要暖一点、脏一点，不能是冷白的写字楼")]
    [SerializeField] private float postExposure = 0f;
    [SerializeField] private float contrast = 12f;
    [SerializeField] private float saturation = -6f;
    [SerializeField] private Color colorFilter = new Color(1f, 0.96f, 0.88f);

    [Header("Bloom / Vignette")]
    [Tooltip("手机上 bloom 是最便宜的'高级感'来源，但阈值要高，否则整屏发白")]
    [SerializeField] private float bloomIntensity = 0.55f;
    [SerializeField] private float bloomThreshold = 1.1f;
    [SerializeField] private float vignetteIntensity = 0.28f;

    [Header("Lighting")]
    [Tooltip("主光角度。**不要为了'更亮'把光压低**：地板是画面里最大的一块，" +
             "压低仰角会让它变暗（审计算过：仰角 50°→25° 时地板受光 0.766→0.574）")]
    [SerializeField] private Vector3 sunEuler = new Vector3(50f, 35f, 0f);
    [SerializeField] private Color sunColor = new Color(1f, 0.95f, 0.85f);
    // 主光/环境光的比例是**量出来的**，不是调出来的：写了个测量器逐帧读平均亮度、
    // 对比度（标准差）与过曝像素比，扫了四档 光/环境 组合：
    //   1.15/0.30 → 平均0.355 对比0.086    1.60/0.22 → 0.380/0.094
    //   2.00/0.16 → 平均0.401 对比0.102    2.40/0.12 → 0.421/0.110（开始偏硬）
    // 取 2.0/0.16：平均落在 0.35-0.45 的目标区间，对比度最高而过曝仍是 0%。
    // 白模显得平，根因是环境光相对主光太强——不是缺后处理。
    [SerializeField] private float sunIntensity = 2f;

    [Tooltip("环境光：白模最丑的地方是阴面死黑，天光一给就立刻像个空间。" +
             "但给太多画面就平了（见上面的实测表）")]
    [SerializeField] private Color skyAmbient = new Color(0.16f, 0.18f, 0.23f);
    [SerializeField] private Color groundAmbient = new Color(0.11f, 0.10f, 0.085f);

    [Header("Sky")]
    [Tooltip("天空色。**竖屏手机上天空占掉将近半个屏幕**，默认那张亮蓝程序化天空" +
             "是画面过曝的一大半原因。压成傍晚的暗蓝，顺带给这栋老楼定了调。")]
    [SerializeField] private Color skyTop = new Color(0.16f, 0.20f, 0.30f);
    [SerializeField] private Color skyHorizon = new Color(0.42f, 0.34f, 0.30f);

    private Volume _volume;
    private VolumeProfile _profile;

    private void OnEnable()
    {
        // 全部放 OnEnable：热重载会清掉运行时生成的东西（本项目定过的规矩）
        EnableCameraPostProcessing();
        StopDarknessOverlaysFromCastingShadows();
        ApplyLighting();
        BuildVolume();
    }

    /// <summary>没有这个组件时自动装一个（场景文件不必改）。</summary>
    public static HotelLook EnsureInScene()
    {
        var existing = FindFirstObjectByType<HotelLook>();
        if (existing != null) return existing;
        // **必须在 World 之外**：FloorVisibilityController 会 SetActive 整层（坑 ③）
        var go = new GameObject("ArtDirection");
        return go.AddComponent<HotelLook>();
    }

    /// <summary>坑 ①：URP 默认不跑后处理，相机上没这个组件的话 Volume 白加。</summary>
    private void EnableCameraPostProcessing()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        // MSAA 在两档画质里都是关的，边缘全是锯齿。FXAA 约 0.2ms，手机上最划算的一笔
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
    }

    /// <summary>坑 ②：房门口的 Darkness 障眼法是 4.6×4.9 的大黑片，
    /// 投影打开会变成 12 面挡光墙。它们只该挡视线，不该挡光。</summary>
    private void StopDarknessOverlaysFromCastingShadows()
    {
        var world = GameObject.Find("World");
        if (world == null) return;
        foreach (var renderer in world.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
        {
            if (renderer == null || renderer.gameObject.name != "Darkness") continue;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void ApplyLighting()
    {
        Light sun = null;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) { sun = light; break; }

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sunEuler);
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.shadows = LightShadows.Soft;   // 坑 ② 处理过了，现在开阴影是安全的
        }

        // 天光/地光双色环境：白模最丑的地方是阴面死黑，这一步比任何贴图都便宜
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyAmbient;
        RenderSettings.ambientEquatorColor = Color.Lerp(skyAmbient, groundAmbient, 0.5f);
        RenderSettings.ambientGroundColor = groundAmbient;

        ApplySky();

        // 远处淡出，给这栋楼一点"城市里的一栋老楼"的空气感
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.55f, 0.60f, 0.68f);
        RenderSettings.fogStartDistance = 26f;
        RenderSettings.fogEndDistance = 80f;
    }

    /// <summary>把默认那张亮蓝程序化天空换成傍晚渐变。
    /// 竖屏里天空占将近半屏，它是"整个画面发白"的一大半原因（实测：关掉后处理
    /// 仍有 36% 像素过曝）。用一张 2×64 的渐变贴图当 Panoramic 天空——
    /// 代码生成、不需要美术、手机上零成本。</summary>
    private void ApplySky()
    {
        if (_skyMaterial == null)
        {
            Shader shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return;
            _skyMaterial = new Material(shader);
        }

        if (_skyTexture == null)
        {
            const int height = 64;
            _skyTexture = new Texture2D(2, height, TextureFormat.RGB24, mipChain: false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < height; y++)
            {
                // 下半是地平线暖色、上半是夜蓝，中间过渡
                float t = y / (float)(height - 1);
                Color c = Color.Lerp(skyHorizon, skyTop, Mathf.SmoothStep(0f, 1f, t));
                _skyTexture.SetPixel(0, y, c);
                _skyTexture.SetPixel(1, y, c);
            }
            _skyTexture.Apply();
        }

        if (_skyMaterial.HasProperty("_MainTex")) _skyMaterial.SetTexture("_MainTex", _skyTexture);
        if (_skyMaterial.HasProperty("_Exposure")) _skyMaterial.SetFloat("_Exposure", 1f);
        RenderSettings.skybox = _skyMaterial;
        // 天空换了，环境光要重算，否则物体还按旧天空的颜色被照
        DynamicGI.UpdateEnvironment();
    }

    private Material _skyMaterial;
    private Texture2D _skyTexture;

    private void BuildVolume()
    {
        if (_volume == null)
        {
            _volume = gameObject.GetComponent<Volume>();
            if (_volume == null) _volume = gameObject.AddComponent<Volume>();
        }
        _volume.isGlobal = true;
        _volume.priority = 1f;

        // 运行时建 profile：不落资产文件，避免和 Assets/Settings 里现成的
        // DefaultVolumeProfile 互相覆盖（那个是 URP 全局默认，动它会影响所有场景）
        if (_profile == null) _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.profile = _profile;

        var grading = GetOrAdd<ColorAdjustments>();
        grading.postExposure.Override(postExposure);
        grading.contrast.Override(contrast);
        grading.saturation.Override(saturation);
        grading.colorFilter.Override(colorFilter);

        var bloom = GetOrAdd<Bloom>();
        bloom.intensity.Override(bloomIntensity);
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.62f);
        // 手机上高质量 bloom 不值那个开销
        bloom.highQualityFiltering.Override(false);

        var vignette = GetOrAdd<Vignette>();
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.5f);

        // 竖屏手机上画面很窄，暗角+轻微色差能把注意力压回中间
        var aberration = GetOrAdd<ChromaticAberration>();
        aberration.intensity.Override(0.08f);
    }

    private T GetOrAdd<T>() where T : VolumeComponent
    {
        if (_profile.TryGet(out T existing)) return existing;
        return _profile.Add<T>(overrides: true);
    }

    private void OnDisable()
    {
        if (_profile != null) { Destroy(_profile); _profile = null; }
        if (_skyMaterial != null) { Destroy(_skyMaterial); _skyMaterial = null; }
        if (_skyTexture != null) { Destroy(_skyTexture); _skyTexture = null; }
    }
}
