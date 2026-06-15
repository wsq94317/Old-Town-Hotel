using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Story 3.6 (Front Desk first):一键生成 Front Desk UI prefab 骨架 —— 信息密集型布局。
//
// 1080×1920 portrait 分区(从上到下填满,最小留白):
//   [安全区 40]
//   HeaderBar (130h)       — Day | Phase | Score | Money 四段
//   MidSection (1000h)     — 左 ActiveGuestCard(60%)+ 右 RoomStatusPanel(40%)
//   QueueStrip (330h)      — 横向 upcoming guest 卡列表
//   ActionButtonRow (150h) — Check In | Compensate | Skip
//   BottomNav (170h)       — Front Desk | Rooms | Lounge tab
//   [安全区 40]
//
// 关键:节点路径名与 Room2DFrontDeskScreenController.FindUiElements 严格对应,改名要同步改 controller。
//   HeaderBar/DayLabel, HeaderBar/ScoreLabel
//   ActiveGuestCard/GuestTypeLabel, ActiveGuestCard/NeedsLabel
//   ActiveGuestCard/PatienceBarTrack/PatienceBarFill
//   QueueContent
//   ActionButtonRow/{CheckInButton,CompensateButton,SkipButton}
//   BottomNav/{FrontDeskTab,RoomsTab,LoungeTab}
//
// Image sprite 字段保持空(玩家后续 Inspector 拖入美术);颜色用占位灰阶。
// 使用:Unity 菜单 Tools → Old Town Hotel → Build Front Desk UI Prefabs
public static class FrontDeskUiBuilder
{
    private const string PrefabsRoot = "Assets/Game/Prefabs/UI";
    private const string FrontDeskScreenPath = PrefabsRoot + "/UI_FrontDeskScreen.prefab";
    private const string GuestQueueCardPath = PrefabsRoot + "/UI_GuestQueueCard.prefab";

    // 占位颜色（sprite 接入后自动被覆盖)
    private static readonly Color PanelBg = new Color(0.09f, 0.11f, 0.14f, 1f);
    private static readonly Color HeaderBg = new Color(0.05f, 0.07f, 0.11f, 1f);
    private static readonly Color HeaderCell = new Color(0.08f, 0.11f, 0.16f, 1f);
    private static readonly Color CardBg = new Color(0.15f, 0.17f, 0.21f, 1f);
    private static readonly Color SubCardBg = new Color(0.12f, 0.14f, 0.18f, 1f);
    private static readonly Color PortraitBg = new Color(0.34f, 0.37f, 0.42f, 1f);
    private static readonly Color ButtonBg = new Color(0.24f, 0.40f, 0.62f, 1f);
    private static readonly Color ButtonAlt = new Color(0.55f, 0.40f, 0.20f, 1f);
    private static readonly Color ButtonDisabled = new Color(0.22f, 0.24f, 0.28f, 1f);
    private static readonly Color NavBg = new Color(0.04f, 0.06f, 0.09f, 1f);
    private static readonly Color NavSelected = new Color(0.28f, 0.48f, 0.76f, 1f);
    private static readonly Color BarTrack = new Color(0.18f, 0.20f, 0.24f, 1f);
    private static readonly Color BarFill = new Color(0.85f, 0.55f, 0.25f, 1f);
    private static readonly Color BadgeReady = new Color(0.30f, 0.62f, 0.36f, 1f);
    private static readonly Color BadgeDirty = new Color(0.62f, 0.40f, 0.28f, 1f);
    private static readonly Color BadgeNeutral = new Color(0.28f, 0.31f, 0.36f, 1f);
    private static readonly Color TextWhite = new Color(0.95f, 0.96f, 0.97f, 1f);
    private static readonly Color TextDim = new Color(0.62f, 0.66f, 0.72f, 1f);
    private static readonly Color TextAccent = new Color(0.95f, 0.78f, 0.42f, 1f);

    [MenuItem("Tools/Old Town Hotel/Build Front Desk UI Prefabs")]
    public static void BuildFrontDeskPrefabs()
    {
        EnsurePrefabFolder();
        BuildGuestQueueCardPrefab();
        BuildFrontDeskScreenPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Front Desk UI Built (Dense Layout)",
            "已生成:\n  " + FrontDeskScreenPath + "\n  " + GuestQueueCardPath +
            "\n\n场景里已有 UI_FrontDeskScreen instance 的话:\n" +
            "  删掉旧 instance,重新从 Project 拖入 prefab(布局变了),\n" +
            "  再 Add Component → Room2DFrontDeskScreenController + 拖 GuestQueueCard prefab。",
            "OK");
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Game"))
            AssetDatabase.CreateFolder("Assets", "Game");
        if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Game", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PrefabsRoot))
            AssetDatabase.CreateFolder("Assets/Game/Prefabs", "UI");
    }

    // ── UI_GuestQueueCard.prefab ────────────────────────────────────────────
    // 紧凑排队卡 140×190。Background + Portrait(100×100) + TypeLabel + BedLabel
    private static void BuildGuestQueueCardPrefab()
    {
        GameObject root = NewUiObject("UI_GuestQueueCard");
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(140f, 190f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

        AddImage(root, CardBg, "Background");

        var portrait = NewUiChild(root, "PortraitImage");
        SetRect(portrait, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(100f, 100f));
        portrait.AddComponent<Image>().color = PortraitBg;

        var typeLabel = NewUiChild(root, "TypeLabel");
        SetRect(typeLabel, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(-8f, 40f));
        AddText(typeLabel, "Business", 22, TextAlignmentOptions.Center, TextWhite);

        var bedLabel = NewUiChild(root, "BedLabel");
        SetRect(bedLabel, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-8f, 32f));
        AddText(bedLabel, "Single", 18, TextAlignmentOptions.Center, TextDim);

        SaveAsPrefab(root, GuestQueueCardPath);
    }

    // ── UI_FrontDeskScreen.prefab ───────────────────────────────────────────
    private static void BuildFrontDeskScreenPrefab()
    {
        GameObject root = NewUiObject("UI_FrontDeskScreen");
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        AddImage(root, PanelBg, "Background");

        BuildHeaderBar(root);       // top
        BuildMidSection(root);      // big middle: ActiveGuestCard + RoomStatusPanel
        BuildQueueArea(root);       // below mid
        BuildActionButtonRow(root); // above nav
        BuildBottomNav(root);       // bottom

        SaveAsPrefab(root, FrontDeskScreenPath);
    }

    // HeaderBar: 顶部 130h,4 段(Day | Phase | Score | Money)
    private static void BuildHeaderBar(GameObject parent)
    {
        var go = NewUiChild(parent, "HeaderBar");
        SetRect(go, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(-30f, 130f));
        go.AddComponent<Image>().color = HeaderBg;

        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 14, 14);
        hlg.spacing = 12f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        BuildStatCell(go, "DayCell", "DAY", "1", "DayLabel");
        BuildStatCell(go, "PhaseCell", "PHASE", "Prep", "PhaseLabel");
        BuildStatCell(go, "ScoreCell", "SCORE", "0", "ScoreLabel");
        BuildStatCell(go, "MoneyCell", "MONEY", "$0", "MoneyLabel");
    }

    // 单个 HUD 统计单元:小标题(上)+ 大数值(下)
    private static void BuildStatCell(GameObject parent, string cellName, string caption, string value, string valueLabelName)
    {
        var cell = NewUiChild(parent, cellName);
        cell.AddComponent<Image>().color = HeaderCell;

        var cap = NewUiChild(cell, "Caption");
        SetRect(cap, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-8f, 34f));
        AddText(cap, caption, 18, TextAlignmentOptions.Center, TextDim);

        var val = NewUiChild(cell, valueLabelName);
        SetRect(val, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(-8f, -36f));
        AddText(val, value, 40, TextAlignmentOptions.Center, TextAccent);
    }

    // MidSection: 中部大区,左 ActiveGuestCard(60%)+ 右 RoomStatusPanel(40%)
    private static void BuildMidSection(GameObject parent)
    {
        var mid = NewUiChild(parent, "MidSection");
        SetRect(mid, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(-30f, 1000f));

        BuildActiveGuestCard(mid);
        BuildRoomStatusPanel(mid);
    }

    private static void BuildActiveGuestCard(GameObject parent)
    {
        var go = NewUiChild(parent, "ActiveGuestCard");
        // 左 60%
        SetRect2(go, new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(0f, 0f), new Vector2(-8f, 0f));
        go.AddComponent<Image>().color = CardBg;

        var caption = NewUiChild(go, "Caption");
        SetRect(caption, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-16f, 40f));
        AddText(caption, "NOW CHECKING IN", 20, TextAlignmentOptions.Center, TextDim);

        var portrait = NewUiChild(go, "PortraitImage");
        SetRect(portrait, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(320f, 320f));
        portrait.AddComponent<Image>().color = PortraitBg;

        var typeLabel = NewUiChild(go, "GuestTypeLabel");
        SetRect(typeLabel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(-24f, 52f));
        AddText(typeLabel, "Business · Single bed", 34, TextAlignmentOptions.Center, TextWhite);

        var needsLabel = NewUiChild(go, "NeedsLabel");
        SetRect(needsLabel, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -456f), new Vector2(-24f, 40f));
        AddText(needsLabel, "Needs: Single bed · Quiet floor", 24, TextAlignmentOptions.Center, TextDim);

        // 耐心进度条标题
        var patienceCaption = NewUiChild(go, "PatienceCaption");
        SetRect(patienceCaption, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(-24f, 30f));
        AddText(patienceCaption, "Patience", 20, TextAlignmentOptions.MidlineLeft, TextDim);

        var barTrack = NewUiChild(go, "PatienceBarTrack");
        SetRect(barTrack, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(-40f, 32f));
        barTrack.AddComponent<Image>().color = BarTrack;

        var barFill = NewUiChild(barTrack, "PatienceBarFill");
        SetRect(barFill, new Vector2(0f, 0f), new Vector2(0.7f, 1f),
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        barFill.AddComponent<Image>().color = BarFill;
    }

    private static void BuildRoomStatusPanel(GameObject parent)
    {
        var go = NewUiChild(parent, "RoomStatusPanel");
        // 右 40%
        SetRect2(go, new Vector2(0.6f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(0f, 0f));
        go.AddComponent<Image>().color = SubCardBg;

        var caption = NewUiChild(go, "Caption");
        SetRect(caption, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(-16f, 40f));
        AddText(caption, "ROOMS", 22, TextAlignmentOptions.Center, TextDim);

        // 状态计数行容器(竖直 layout)
        var counts = NewUiChild(go, "StateCounts");
        SetRect(counts, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(-24f, 300f));
        var vlg = counts.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 4, 4);
        vlg.spacing = 6f;
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = true;

        BuildCountRow(counts, "ReadyRow", "Ready", "0", "ReadyCount", BadgeReady);
        BuildCountRow(counts, "DirtyRow", "Dirty", "0", "DirtyCount", BadgeDirty);
        BuildCountRow(counts, "CleaningRow", "Cleaning", "0", "CleaningCount", BadgeNeutral);
        BuildCountRow(counts, "InspectRow", "Awaiting Insp", "0", "InspectCount", BadgeNeutral);
        BuildCountRow(counts, "OccupiedRow", "Occupied", "0", "OccupiedCount", BadgeNeutral);
        BuildCountRow(counts, "BlockedRow", "Blocked", "0", "BlockedCount", BadgeNeutral);

        // 12 房 mini badge 网格(4×3)
        var grid = NewUiChild(go, "RoomBadgeGrid");
        SetRect(grid, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(-24f, 320f));
        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.padding = new RectOffset(8, 8, 8, 8);
        glg.cellSize = new Vector2(88f, 88f);
        glg.spacing = new Vector2(8f, 8f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;
        // 12 个占位 badge(controller runtime 更新颜色 + 房号)
        for (int i = 0; i < 12; i++)
        {
            var badge = NewUiChild(grid, "RoomBadge_" + i);
            badge.AddComponent<Image>().color = BadgeNeutral;
            var num = NewUiChild(badge, "Num");
            SetRect(num, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddText(num, (101 + i).ToString(), 22, TextAlignmentOptions.Center, TextWhite);
        }
    }

    // 单个状态计数行:左标签 + 右数值
    private static void BuildCountRow(GameObject parent, string rowName, string label, string count, string countLabelName, Color accent)
    {
        var row = NewUiChild(parent, rowName);
        row.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.22f);

        var lbl = NewUiChild(row, "Label");
        SetRect(lbl, new Vector2(0f, 0f), new Vector2(0.7f, 1f),
            new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(-14f, 0f));
        AddText(lbl, label, 24, TextAlignmentOptions.MidlineLeft, TextWhite);

        var cnt = NewUiChild(row, countLabelName);
        SetRect(cnt, new Vector2(0.7f, 0f), new Vector2(1f, 1f),
            new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(-8f, 0f));
        AddText(cnt, count, 28, TextAlignmentOptions.MidlineRight, TextAccent);
    }

    private static void BuildQueueArea(GameObject parent)
    {
        var label = NewUiChild(parent, "QueueLabel");
        SetRect(label, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(30f, 720f), new Vector2(-60f, 40f));
        AddText(label, "UPCOMING QUEUE", 22, TextAlignmentOptions.MidlineLeft, TextDim);

        var content = NewUiChild(parent, "QueueContent");
        SetRect(content, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 540f), new Vector2(-30f, 200f));
        content.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

        var hlg = content.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 12, 12);
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
    }

    private static void BuildActionButtonRow(GameObject parent)
    {
        var row = NewUiChild(parent, "ActionButtonRow");
        SetRect(row, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 380f), new Vector2(-30f, 140f));

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        BuildActionButton(row, "CheckInButton", "Check In", ButtonBg);
        BuildActionButton(row, "CompensateButton", "Compensate", ButtonAlt);
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
        SetRect(label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        AddText(label, labelText, 36, TextAlignmentOptions.Center, TextWhite);
        return go;
    }

    private static void BuildBottomNav(GameObject parent)
    {
        var nav = NewUiChild(parent, "BottomNav");
        SetRect(nav, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(0f, 180f));
        nav.AddComponent<Image>().color = NavBg;

        var hlg = nav.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 14, 14);
        hlg.spacing = 10f;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

        BuildNavTab(nav, "FrontDeskTab", "Front Desk", true);
        BuildNavTab(nav, "RoomsTab", "Rooms", false);
        BuildNavTab(nav, "LoungeTab", "Lounge", false);
    }

    private static void BuildNavTab(GameObject parent, string name, string labelText, bool isSelected)
    {
        var tab = NewUiChild(parent, name);
        var img = tab.AddComponent<Image>();
        img.color = isSelected ? NavSelected : NavBg;
        var btn = tab.AddComponent<Button>();
        btn.targetGraphic = img;

        var icon = NewUiChild(tab, "Icon");
        SetRect(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(56f, 56f));
        icon.AddComponent<Image>().color = PortraitBg;

        var label = NewUiChild(tab, "Label");
        SetRect(label, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(0f, 44f));
        AddText(label, labelText, 24, TextAlignmentOptions.Center, isSelected ? TextWhite : TextDim);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static GameObject NewUiObject(string name) => new GameObject(name, typeof(RectTransform));

    private static GameObject NewUiChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = sizeDelta;
    }

    // 用 offsetMin/offsetMax 形式(stretch anchor 时更直观)
    private static void SetRect2(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    private static Image AddImage(GameObject parent, Color color, string childName)
    {
        var host = NewUiChild(parent, childName);
        SetRect(host, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var img = host.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text AddText(GameObject host, string text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        var tmp = host.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private static void SaveAsPrefab(GameObject root, string path)
    {
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
}
