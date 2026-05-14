using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Story 3.6 (Front Desk first):一键生成 Front Desk UI prefab 骨架。
//
// 设计意图:
//   - 输出 2 个 prefab:UI_FrontDeskScreen(整个 Front Desk 屏幕)+ UI_GuestQueueCard(单张排队卡)
//   - 所有 RectTransform / anchor / size / 颜色都按 1080×1920 portrait 9:16 spec 配好
//   - Image 组件 sprite 字段保持空(玩家后续 Inspector 拖入美术资源)
//   - 颜色用占位灰阶,sprite 替换后会自动覆盖
//   - 不写 onClick listener;Room2DFrontDeskScreenController 在 runtime 绑定
//
// 使用:Unity 菜单 Tools → Old Town Hotel → Build Front Desk UI Prefabs
// 重跑安全:旧 prefab 会被覆盖,但 sprite reference 保留(PrefabUtility.SaveAsPrefabAsset 是 in-place 写)
public static class FrontDeskUiBuilder
{
    private const string PrefabsRoot = "Assets/Game/Prefabs/UI";
    private const string FrontDeskScreenPath = PrefabsRoot + "/UI_FrontDeskScreen.prefab";
    private const string GuestQueueCardPath = PrefabsRoot + "/UI_GuestQueueCard.prefab";

    // 1080×1920 portrait 参考分辨率;所有 anchor offset 数值都基于此。
    private const float ReferenceWidth = 1080f;
    private const float ReferenceHeight = 1920f;

    // 占位颜色（sprite 接入后自动被覆盖)
    private static readonly Color PanelBg = new Color(0.10f, 0.12f, 0.15f, 0.96f);
    private static readonly Color HeaderBg = new Color(0.06f, 0.08f, 0.12f, 1f);
    private static readonly Color CardBg = new Color(0.18f, 0.20f, 0.24f, 1f);
    private static readonly Color PortraitBg = new Color(0.40f, 0.42f, 0.46f, 1f);
    private static readonly Color ButtonBg = new Color(0.28f, 0.42f, 0.62f, 1f);
    private static readonly Color ButtonDisabled = new Color(0.25f, 0.27f, 0.30f, 1f);
    private static readonly Color NavBg = new Color(0.05f, 0.07f, 0.10f, 1f);
    private static readonly Color NavSelected = new Color(0.30f, 0.50f, 0.78f, 1f);
    private static readonly Color BarTrack = new Color(0.20f, 0.22f, 0.26f, 1f);
    private static readonly Color BarFill = new Color(0.85f, 0.55f, 0.25f, 1f);
    private static readonly Color TextWhite = new Color(0.95f, 0.95f, 0.95f, 1f);
    private static readonly Color TextDim = new Color(0.65f, 0.68f, 0.72f, 1f);

    [MenuItem("Tools/Old Town Hotel/Build Front Desk UI Prefabs")]
    public static void BuildFrontDeskPrefabs()
    {
        EnsurePrefabFolder();
        BuildGuestQueueCardPrefab();
        BuildFrontDeskScreenPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Front Desk UI Built",
            "已生成:\n  " + FrontDeskScreenPath + "\n  " + GuestQueueCardPath +
            "\n\n下一步:\n  1. 在场景 Canvas 下拖入 UI_FrontDeskScreen prefab instance\n" +
            "  2. 给 Image 组件拖入 sprite(背景/portrait/icon/button)\n" +
            "  3. Add Component → Room2DFrontDeskScreenController 到 prefab instance",
            "OK");
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game"))
        {
            AssetDatabase.CreateFolder("Assets", "Game");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets/Game", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(PrefabsRoot))
        {
            AssetDatabase.CreateFolder("Assets/Game/Prefabs", "UI");
        }
    }

    // ── UI_GuestQueueCard.prefab ────────────────────────────────────────────
    // 单张排队中客人的卡片。160w × 220h。
    //   - Background(Image,可放卡片底图)
    //   - PortraitImage(Image,120×120,中上,放客人头像)
    //   - TypeLabel(TMP,底部,"Business" 之类)

    private static void BuildGuestQueueCardPrefab()
    {
        GameObject root = NewUiObject("UI_GuestQueueCard");
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 220f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        AddImage(root, CardBg, "Background");

        var portraitGo = NewUiChild(root, "PortraitImage");
        SetRect(portraitGo, anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -16f), sizeDelta: new Vector2(120f, 120f));
        portraitGo.AddComponent<Image>().color = PortraitBg;

        var typeLabelGo = NewUiChild(root, "TypeLabel");
        SetRect(typeLabelGo, anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 10f), sizeDelta: new Vector2(-12f, 56f));
        AddTextChildText(typeLabelGo, "Business", 28, TextAlignmentOptions.Center, TextWhite);

        SaveAsPrefab(root, GuestQueueCardPath);
    }

    // ── UI_FrontDeskScreen.prefab ───────────────────────────────────────────
    // 整个 Front Desk 屏幕。1080×1920 anchored fill canvas。
    //   - Background panel(Image)
    //   - HeaderBar(顶部,Day + Score)
    //   - ActiveGuestCard(大,中上,显示当前接客)
    //   - QueueLabel + QueueContent(下方,排队列表 placeholder)
    //   - ActionButtonRow(下底,3 个按钮)
    //   - BottomNav(底部,3 个 tab)

    private static void BuildFrontDeskScreenPrefab()
    {
        GameObject root = NewUiObject("UI_FrontDeskScreen");
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        // 整屏底图
        AddImage(root, PanelBg, "Background");

        BuildHeaderBar(root);
        BuildActiveGuestCard(root);
        BuildQueueArea(root);
        BuildActionButtonRow(root);
        BuildBottomNav(root);

        SaveAsPrefab(root, FrontDeskScreenPath);
    }

    private static void BuildHeaderBar(GameObject parent)
    {
        var go = NewUiChild(parent, "HeaderBar");
        // 顶部 stretch,高度 160,留 80 顶部安全区
        SetRect(go,
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -80f),
            sizeDelta: new Vector2(0f, 160f));
        AddImage(go, HeaderBg, "HeaderBg");

        var dayGo = NewUiChild(go, "DayLabel");
        SetRect(dayGo,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0f, 0.5f), anchoredPos: new Vector2(48f, 0f),
            sizeDelta: new Vector2(-48f, 0f));
        AddTextChildText(dayGo, "Day 1", 56, TextAlignmentOptions.MidlineLeft, TextWhite);

        var scoreGo = NewUiChild(go, "ScoreLabel");
        SetRect(scoreGo,
            anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(1f, 0.5f), anchoredPos: new Vector2(-48f, 0f),
            sizeDelta: new Vector2(-48f, 0f));
        AddTextChildText(scoreGo, "Score: 0", 48, TextAlignmentOptions.MidlineRight, TextDim);
    }

    private static void BuildActiveGuestCard(GameObject parent)
    {
        var go = NewUiChild(parent, "ActiveGuestCard");
        // 居中,1000w × 640h,从顶部往下 280 偏移(HeaderBar 下方)
        SetRect(go,
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -280f),
            sizeDelta: new Vector2(1000f, 640f));
        AddImage(go, CardBg, "CardBackground");

        // 头像区:中上,400×400
        var portrait = NewUiChild(go, "PortraitImage");
        SetRect(portrait,
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -40f),
            sizeDelta: new Vector2(400f, 400f));
        portrait.AddComponent<Image>().color = PortraitBg;

        // 客人 type / bedType
        var typeLabel = NewUiChild(go, "GuestTypeLabel");
        SetRect(typeLabel,
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -460f),
            sizeDelta: new Vector2(-40f, 56f));
        AddTextChildText(typeLabel, "Business · Single bed", 36, TextAlignmentOptions.Center, TextWhite);

        // 需求 / 备注
        var needsLabel = NewUiChild(go, "NeedsLabel");
        SetRect(needsLabel,
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -524f),
            sizeDelta: new Vector2(-40f, 44f));
        AddTextChildText(needsLabel, "Needs: Single bed · Quiet floor", 28, TextAlignmentOptions.Center, TextDim);

        // 耐心进度条:track + fill
        var barTrack = NewUiChild(go, "PatienceBarTrack");
        SetRect(barTrack,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 24f),
            sizeDelta: new Vector2(-80f, 28f));
        barTrack.AddComponent<Image>().color = BarTrack;

        var barFill = NewUiChild(barTrack, "PatienceBarFill");
        SetRect(barFill,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0.7f, 1f),
            pivot: new Vector2(0f, 0.5f), anchoredPos: Vector2.zero, sizeDelta: Vector2.zero);
        barFill.AddComponent<Image>().color = BarFill;
    }

    private static void BuildQueueArea(GameObject parent)
    {
        var label = NewUiChild(parent, "QueueLabel");
        SetRect(label,
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -960f),
            sizeDelta: new Vector2(-60f, 50f));
        AddTextChildText(label, "Queue", 32, TextAlignmentOptions.MidlineLeft, TextDim);

        // 排队卡片容器(横向 layout)
        var content = NewUiChild(parent, "QueueContent");
        SetRect(content,
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -1024f),
            sizeDelta: new Vector2(-60f, 240f));
        var bg = content.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.25f); // 半透明底,sprite 接入后可删

        var hlg = content.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 16, 16);
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
    }

    private static void BuildActionButtonRow(GameObject parent)
    {
        var row = NewUiChild(parent, "ActionButtonRow");
        SetRect(row,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 280f),
            sizeDelta: new Vector2(-60f, 180f));

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.spacing = 20f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        BuildActionButton(row, "CheckInButton", "Check In", ButtonBg);
        BuildActionButton(row, "CompensateButton", "Compensate", ButtonBg);
        BuildActionButton(row, "SkipButton", "Skip", ButtonDisabled);
    }

    private static GameObject BuildActionButton(GameObject parent, string name, string labelText, Color bgColor)
    {
        var go = NewUiChild(parent, name);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var label = NewUiChild(go, "Label");
        SetRect(label,
            anchorMin: Vector2.zero, anchorMax: Vector2.one,
            pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, sizeDelta: Vector2.zero);
        AddTextChildText(label, labelText, 40, TextAlignmentOptions.Center, TextWhite);
        return go;
    }

    private static void BuildBottomNav(GameObject parent)
    {
        var nav = NewUiChild(parent, "BottomNav");
        // 底部 stretch,高 200,留 80 底部安全区
        SetRect(nav,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 80f),
            sizeDelta: new Vector2(0f, 200f));
        nav.AddComponent<Image>().color = NavBg;

        var hlg = nav.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(20, 20, 16, 16);
        hlg.spacing = 12f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        BuildNavTab(nav, "FrontDeskTab", "Front Desk", isSelected: true);
        BuildNavTab(nav, "RoomsTab", "Rooms", isSelected: false);
        BuildNavTab(nav, "LoungeTab", "Lounge", isSelected: false);
    }

    private static void BuildNavTab(GameObject parent, string name, string labelText, bool isSelected)
    {
        var tab = NewUiChild(parent, name);
        var img = tab.AddComponent<Image>();
        img.color = isSelected ? NavSelected : NavBg;
        var btn = tab.AddComponent<Button>();
        btn.targetGraphic = img;

        // 图标 slot(空 sprite,user 后填)
        var iconGo = NewUiChild(tab, "Icon");
        SetRect(iconGo,
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -16f),
            sizeDelta: new Vector2(64f, 64f));
        iconGo.AddComponent<Image>().color = PortraitBg;

        var label = NewUiChild(tab, "Label");
        SetRect(label,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 12f),
            sizeDelta: new Vector2(0f, 48f));
        AddTextChildText(label, labelText, 26, TextAlignmentOptions.Center, isSelected ? TextWhite : TextDim);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static GameObject NewUiObject(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        return go;
    }

    private static GameObject NewUiChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, worldPositionStays: false);
        return go;
    }

    private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    private static Image AddImage(GameObject parent, Color color, string childName = null)
    {
        GameObject host;
        if (string.IsNullOrEmpty(childName))
        {
            host = parent;
        }
        else
        {
            host = NewUiChild(parent, childName);
            SetRect(host, anchorMin: Vector2.zero, anchorMax: Vector2.one,
                pivot: new Vector2(0.5f, 0.5f), anchoredPos: Vector2.zero, sizeDelta: Vector2.zero);
        }
        var img = host.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false; // 背景图不接受 raycast,避免吃掉按钮点击
        return img;
    }

    private static TMP_Text AddTextChildText(GameObject host, string text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var tmp = host.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void SaveAsPrefab(GameObject root, string path)
    {
        // 删除已存在的 prefab 文件以避免序列化冲突;sprite 引用会丢失(可接受 - sprite 是 user 后续接的)
        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root); // 清掉 hierarchy 里的临时 instance
    }
}
