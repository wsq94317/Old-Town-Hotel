using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class WorldManagementHud
{
    private RectTransform _sessionMenu, _sessionContent, _sessionMenuButton;
    private bool _sessionInitialized, _sessionWasSuspended;
    private float _sessionTimeScale = 1f;
    private int _roomFloor = 1, _roomFilter, _roomTypeFilter = -1, _restoreBatchSize = 4;
    private bool _showRoomBatch;

    private void BuildSessionButton()
    {
        RectTransform rect; TextMeshProUGUI text;
        CreateButton("SessionMenu", _safeRoot, out rect, out text, L("MENU / HELP", "菜单 / 帮助"), Walnut,
            () => ShowSessionMenu("main"));
        SetAnchors(rect, Vector2.up, Vector2.up, new Vector2(10, -168), new Vector2(76, -132));
        text.text=L("MENU","菜单");
        _sessionMenuButton = rect;
        text.fontSize = 12;
    }

    private bool TickSessionMenu()
    {
        if (!_sessionInitialized)
        {
            _sessionInitialized = true;
            ShowSessionMenu("main");
        }
        if (_sessionMenu != null && _sessionMenu.gameObject.activeSelf) return true;
        if (!HotelSessionState.Current.reopeningCelebrated && CanCelebrateReopening())
        {
            HotelSessionState.Current.reopeningCelebrated = true;
            ShowSessionMenu("victory");
            FindFirstObjectByType<SaveCoordinator>()?.SaveGame();
            return true;
        }
        return false;
    }

    private void ShowSessionMenu(string page)
    {
        if (_sessionMenu == null)
        {
            _sessionMenu = CreatePanel("SessionMenuOverlay", _safeRoot, Glass);
            Stretch(_sessionMenu, 0);
            AddMenuArtwork();
            var panel = CreatePanel("MenuCard", _sessionMenu, Ledger);
            _menuCard = panel;
            SetAnchors(panel, new Vector2(.04f, .10f), new Vector2(.96f, .90f), Vector2.zero, Vector2.zero);
            AddOutline(panel, BrassLine, 1.5f);
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            var viewport = CreatePanel("Viewport", panel, Color.clear);
            Stretch(viewport, 16);
            viewport.gameObject.AddComponent<RectMask2D>();
            _sessionContent = CreatePanel("MenuContent", viewport, Color.clear);
            _sessionContent.anchorMin = new Vector2(0, 1); _sessionContent.anchorMax = Vector2.one;
            _sessionContent.pivot = new Vector2(.5f, 1); _sessionContent.sizeDelta = Vector2.zero;
            var layout = _sessionContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            _sessionContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = _sessionContent;
        }
        if (!_sessionMenu.gameObject.activeSelf || Time.timeScale > 0)
        {
            _sessionTimeScale = Time.timeScale > 0 ? Time.timeScale : 1f;
            _sessionWasSuspended = HotelSimSceneBridge.Instance != null && HotelSimSceneBridge.Instance.IsSuspended;
        }
        _safeRoot.gameObject.SetActive(true);
        Time.timeScale = 0;
        if (HotelSimSceneBridge.Instance != null) HotelSimSceneBridge.Instance.IsSuspended = true;
        _sessionMenu.gameObject.SetActive(true); _sessionMenu.SetAsLastSibling();
        foreach (Transform child in _sessionContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        bool compact = page == "main";
        SetAnchors(_menuCard, new Vector2(.04f, .035f), new Vector2(.96f, compact ? .43f : .73f), Vector2.zero, Vector2.zero);
        if (!compact) MenuText("OLD TOWN HOTEL", 24, Gold, 36);
        MenuText(page == "victory" ? L("THE GRAND REOPENING", "老酒店，正式重开！") : L("A HOTEL WORTH SAVING", "让老酒店重获新生"), 18, Cream, 30);
        if (page == "help")
        {
            MenuText(L("1. Run the business", "1. 先经营，再扩张"), 17, Gold, 30);
            MenuText(L("Set rates and channels, cover reception and cleaning, restock supplies. Guests pay at checkout; collect receipts from the safe and pay wages. Interest increases the inherited $183,000 debt.",
                "调整房价与渠道，保障前台、清洁和验房，及时补货。客人在退房时结账；回款进入保险箱，记得收钱和付工资。继承的 $183,000 债务会产生利息。"), 14, Cream, 114);
            MenuText(L("2. Restore and furnish", "2. 修复房间，诚实定价"), 17, Gold, 30);
            MenuText(L("Use ROOMS to find any of the 100 rooms. Clearing ruins uses housekeeping; paid restoration needs materials and days. Yellow barriers show construction. Clean and inspect after handover. Repairs restore health; renovation replaces worn furniture. Price promises must match delivered quality.",
                "在客房作战室查看全部100间房。免费清理占用客房部，付费复原消耗材料与工期；黄黑围挡表示施工中。完工后还要清洁验房。维修恢复健康度，装修更换老家具；挂牌档应符合实际品质。"), 14, Cream, 136);
            MenuText(L("3. Inspect and handle incidents", "3. 巡查与操作"), 17, Gold, 30);
            MenuText(L("Director mode: tap a whole room to manage it, drag to pan, scroll or pinch to zoom, right-drag or twist two fingers to orbit. The camera panel includes rotation and top view. Use floor and room navigation to reach any wing. Switch to Walk to move your manager personally. MENU pauses everything.",
                "经营指挥：点整间房处理事务，拖动平移，滚轮/双指缩放，右键拖动/双指旋转。镜头菜单可转向和俯视；楼层与房间导航可直达远翼。切换亲自巡视后可点地面行走。菜单暂停全酒店。"), 14, Cream, 114);
            MenuButton(L("BACK", "返回菜单"), () => ShowSessionMenu("main"));
        }
        else if (page == "slots")
        {
            MenuText(L("Loading replaces the current unsaved session. Save first if needed.", "读档会替换当前未保存的进度，需要时请先保存。"), 14, InkSoft, 58);
            for (int i = 1; i <= SaveSlots.Count; i++)
            {
                int slot = i;
                MenuButton(L("LOAD SLOT ", "读取槽位 ") + slot + (SaveSlots.Exists(slot) ? L(" · SAVED", " · 已存档") : L(" · EMPTY", " · 空")), () => {
                    if (!SaveSlots.Exists(slot)) return;
                    SaveSlots.RequestLoad(slot); CloseSessionMenu();
                    SceneManager.LoadScene(SceneManager.GetActiveScene().path);
                }, SaveSlots.Exists(slot));
                if (!SaveSlots.Exists(slot)) MenuButton(L("NEW HOTEL IN SLOT ", "在空槽开新酒店 ") + slot, () => StartNewHotel(slot));
            }
            MenuButton(L("BACK", "返回菜单"), () => ShowSessionMenu("main"));
        }
        else if (page == "settings")
        {
            MenuButton(L("SAVE SLOTS / NEW HOTEL", "读取存档 / 新开酒店"), () => ShowSessionMenu("slots"));
            MenuButton(L("SOUND ", "音效 ") + (HotelSessionState.Current.soundEnabled ? L("ON", "开") : L("OFF", "关")), () => { HotelSessionState.Current.soundEnabled = !HotelSessionState.Current.soundEnabled; ShowSessionMenu("settings"); });
            MenuButton(L("MUSIC ", "音乐 ") + (HotelSessionState.Current.musicEnabled ? L("ON", "开") : L("OFF", "关")), () => { HotelSessionState.Current.musicEnabled = !HotelSessionState.Current.musicEnabled; ShowSessionMenu("settings"); });
            MenuButton(L("RESTART CURRENT SLOT", "重新开始当前槽位"), () => ShowSessionMenu("confirm"));
            MenuButton(L("BACK", "返回菜单"), () => ShowSessionMenu("main"));
        }
        else if (page == "confirm")
        {
            MenuText(L("The current slot will be backed up beside the save file before a fresh hotel starts. Other slots remain available.",
                "新开酒店前会将当前槽位保存一份备份。当前槽位会重新开始，其他槽位不受影响。"), 15, Cream, 100);
            MenuButton(L("BACK UP AND START NEW HOTEL", "备份旧档并重新开局"), () => StartNewHotel(SaveSlots.ActiveSlot));
            MenuButton(L("CANCEL", "取消"), () => ShowSessionMenu("main"));
        }
        else
        {
            if (page == "victory")
                MenuText(L("Debt cleared. Every room restored and renovated. Wages paid. Guests rate the hotel at least three stars. Your grand old hotel is back. Keep building your empire in endless play.",
                    "债务已还清，100间房全部恢复并翻新，工资已结清，评分达到三星。老酒店正式重开！你可以继续经营，收购更多酒店。"), 16, Cream, 108);
            else MenuText(L("100 rooms. One inherited debt. Your decisions turn ruins into a hotel empire. The hotel is paused while this menu is open.",
                "修复客房，照顾员工，迎接下一位客人。\n从百间破屋，经营出你的老城酒店。"), 13, Cream, 48);
            var saveStatus = FindFirstObjectByType<SaveCoordinator>()?.StatusText;
            if (saveStatus != null && (saveStatus.StartsWith("已保存") || saveStatus.StartsWith("Saved") || saveStatus.StartsWith("保存失败")))
                MenuText(saveStatus,12,TealSoft,32);
            MenuButton(HotelSessionState.Current.tutorialCompleted ? L("CONTINUE HOTEL", "继续经营") : L("ENTER / DIRECTOR MODE", "进入酒店 · 经营指挥"), () => {
                HotelSessionState.Current.tutorialCompleted = true; CloseSessionMenu();
            });
            MenuButton(L("HOW TO PLAY", "经营手册 / 操作帮助"), () => ShowSessionMenu("help"));
            MenuButton(L("SAVE PROGRESS", "保存当前进度"), () => {
                var save = FindFirstObjectByType<SaveCoordinator>(); save?.SaveGame();
                ShowSessionMenu("main");
            });
            MenuButton(L("SETTINGS & SAVE SLOTS", "设置 / 存档管理"), () => ShowSessionMenu("settings"));
        }
        Canvas.ForceUpdateCanvases();
        _menuCard.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
    }

    private void CloseSessionMenu()
    {
        if (_sessionMenu != null) _sessionMenu.gameObject.SetActive(false);
        Time.timeScale = _sessionTimeScale;
        if (HotelSimSceneBridge.Instance != null) HotelSimSceneBridge.Instance.IsSuspended = _sessionWasSuspended;
        ForceAllRefresh();
    }

    private void StartNewHotel(int slot)
    {
        var save = FindFirstObjectByType<SaveCoordinator>();
        string oldPath = SaveSlots.PathOf(slot);
        if (File.Exists(oldPath)) File.Copy(oldPath, oldPath + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"), false);
        SaveSlots.SetActiveSlot(slot); save?.NewGame();
        CloseSessionMenu(); SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    private void MenuText(string message, float size, Color color, float height)
    {
        var text = CreateText("MenuText", _sessionContent, size, color, TextAlignmentOptions.TopLeft, true);
        text.text = message; text.textWrappingMode = TMPro.TextWrappingModes.Normal;
        text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private void MenuButton(string message, Action action, bool enabled = true)
    {
        RectTransform rect; TextMeshProUGUI text;
        var button = CreateButton("MenuAction", _sessionContent, out rect, out text, message, Teal, action);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
        button.interactable = enabled; text.fontSize = 15;
    }

    private string RoomTypeName(Room2DRoomCategory category) => category == Room2DRoomCategory.Single ? L("SINGLE", "单人房")
        : category == Room2DRoomCategory.Twin ? L("TWIN", "双床房") : L("FAMILY", "家庭房");

    private void BuildWarehouseExpansion()
    {
        int current = Sim.Warehouse.Capacity;
        int next = WarehouseExpansion.NextCapacity(current);
        if (next == 0) return;
        int cost = WarehouseExpansion.Cost(current);
        AddPlanButton(L("EXPAND WAREHOUSE ", "扩建材料仓库 ") + current + " -> " + next + " · $" + cost,
            L("More stock supports larger floor projects. The lobby rack grows with capacity; the upgrade is saved.",
                "提高仓容以支持更大批量施工；大堂货架随扩建增加层数，升级会保存。"), () => {
                bool ok = Sim.TryExpandWarehouse(out string reason);
                ShowToast(ok ? L("Warehouse expanded. Inspect the new rack in the lobby.", "仓库扩建完成，可在大堂查看新货架。") : GameText.T(reason));
                ForceAllRefresh();
            }, Gold, Sim.Cash >= cost);
    }

    private bool CanCelebrateReopening()
    {
        var economy = FindFirstObjectByType<EconomySystem>();
        return economy != null && HotelCampaign.CanReopen(economy.Loan.Balance, Sim.Rooms.Count,
            Sim.Rooms.CountOf(RoomSimState.Ruined), Sim.Rooms.CountOf(RoomSimState.Blocked),
            Sim.Renovations.RoomsUnderRenovation, Sim.Journey.State.renovated.Count, Sim.Payroll.Owed, Sim.Reputation.Stars);
    }

    private void BuildCampaignStatus()
    {
        var economy = FindFirstObjectByType<EconomySystem>();
        AddSection(L("THE GRAND REOPENING", "最终目标 · 老酒店正式重开"),
            L("Clear the debt, restore and renovate every room, pay wages, reach three stars. Continue expanding afterward.",
                "还清债务，恢复并翻新全部客房，结清工资，达到三星。完成后可继续扩张。"));
        AddInfoCard(L("YOUR LEGACY", "翻身进度"),
            L("Debt $", "待还债务 $") + (economy != null ? economy.Loan.Balance : 0)
            + "\n" + L("Open rooms ", "已开放 ") + Sim.Rooms.OpenRoomCount + "/" + Sim.Rooms.Count
            + L(" · renovated ", " · 已翻新 ") + Sim.Journey.State.renovated.Count + "/" + Sim.Rooms.Count
            + "\n" + L("Rating ", "评分 ") + Sim.Reputation.Stars.ToString("0.0") + "/3.0"
            + L(" · unpaid wages $", " · 待付工资 $") + Sim.Payroll.Owed, Gold, 114);
    }

    private System.Collections.Generic.List<int> ReclaimBatchCandidates()
    {
        var result = new System.Collections.Generic.List<int>();
        for (int i = 0; i < Sim.Rooms.Count && result.Count < _restoreBatchSize; i++) {
            var room = Sim.Rooms.Peek(i);
            if (room.floor == _roomFloor && room.state == RoomSimState.Ruined
                && !Sim.Renovations.IsRenovating(room.number) && !Sim.Clearing.IsClearing(room.number)) result.Add(room.number);
        }
        return result;
    }

    private void BuildReclaimBatch()
    {
        AddActionButton(L("BATCH LIMIT ", "批量上限 ") + _restoreBatchSize + L(" ROOMS · CHANGE", " 间 · 点击切换"), () => {
            _restoreBatchSize = _restoreBatchSize >= 50 ? 1 : Mathf.Min(50, _restoreBatchSize * 2); ForceDrawerRefresh();
        }, Ink, true);
        var selected = ReclaimBatchCandidates();
        if (selected.Count == 0) return;
        AddSection(L("RESTORE THIS WING", "本层批量恢复营业"), L("Next eligible rooms: ", "本批房号：") + string.Join(", ",selected));
        AddActionButton(L("CLEAR THESE ROOMS · NO CASH", "指派客房部免费清理本批"), () => {
            int started = 0;
            foreach (int room in selected) if (Sim.TryStartJunkClearing(room,out _)) started++;
            ShowToast(L("Rooms assigned: ", "已安排清理：") + started);
            ForceAllRefresh();
        }, Teal, true);
        foreach (ReclaimPlanKind kind in Enum.GetValues(typeof(ReclaimPlanKind))) {
            var captured = kind; var plan = ReclaimPlan.For(kind);
            int cash = Sim.QuoteReclaim(kind,selected.Count);
            int materials = ReclaimPricing.MaterialCostFor(plan,selected.Count);
            int days = ReclaimPricing.BlockDaysFor(plan,selected.Count);
            AddPlanButton(ReclaimName(kind) + " · $" + cash,
                selected.Count + L(" rooms · ", " 间房 · ") + materials + L(" materials · ", " 材料 · ") + days + L(" days closed", " 天停业")
                + "\n" + L("Cash after work starts $", "开工后现金 $") + (Sim.Cash-cash), () => {
                    bool ok = Sim.TryStartReclaim(captured,selected,out string reason);
                    ShowToast(ok ? L("Restoration started for this batch.", "本批房间已开工复原。") : GameText.T(reason));
                    ForceAllRefresh();
                }, Gold, Sim.Cash >= cash && Sim.Materials.Stock >= materials);
        }
    }

    private void BuildRoomOperations()
    {
        AddSection(L("100-ROOM OPERATIONS", "100间房 · 聚合管理"),
            L("Tap a room to inspect it. Yellow means construction. Work is billed once and progresses at day close.",
                "点击任一房间查看现场与操作；黄色为施工中。工程只扣款一次，进度随每日结算推进。"));
        BuildRoomFilterPills();
        var rooms = new System.Collections.Generic.List<RoomRecord>();
        for (int i = 0; i < Sim.Rooms.Count; i++)
        {
            var room = Sim.Rooms.Peek(i); bool building = Sim.Renovations.IsRenovating(room.number);
            if (room.floor != _roomFloor || (_roomTypeFilter >= 0 && (int)room.category != _roomTypeFilter) || (_roomFilter == 1 && (room.state != RoomSimState.Ruined || building))
                || (_roomFilter == 2 && !building) || (_roomFilter == 3 && !room.NeedsCleaning)
                || (_roomFilter == 4 && !room.IsSellable)) continue;
            rooms.Add(room);
        }
        AddActionButton(_showRoomBatch ? L("HIDE BATCH PLANS", "收起批量恢复方案") : L("PLAN A RESTORATION BATCH", "展开批量恢复方案"), () => {
            _showRoomBatch = !_showRoomBatch; _drawerScrollReset = true; ForceDrawerRefresh();
        }, Teal, true);
        if (_showRoomBatch) BuildReclaimBatch();
        rooms.Sort((a,b) => a.number.CompareTo(b.number));
        for (int start = 0; start < rooms.Count; start += 2)
        {
            var row = CreatePanel("RoomGridRow", _drawerContent, Color.clear);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 100;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8; layout.childControlWidth=true;layout.childForceExpandWidth = true;
            for (int j = start; j < Mathf.Min(start + 2, rooms.Count); j++) AddRoomVisualCard(row,rooms[j]);
        }
        if (rooms.Count == 0) AddInfoCard(L("NO ROOMS", "没有符合条件的房间"), L("Try another filter.", "请切换筛选条件。"), Slate, 68);
        AddActionButton(L("PLAN FLOOR INVESTMENT", "前往楼层批量投资"), () => SelectTab(DeskTab.Assets), Gold, true);
    }
}
