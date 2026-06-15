# Lounge UI 美化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 Lounge 咖啡吧页面按已确认的设计稿做一次大幅视觉升级（保留复古温馨美术），确立可复用的设计 token。

**Architecture:** 全部在 Unity 编辑器内通过 **UnityMCP** 改 prefab / 场景 / sprite 导入设置（嵌套 prefab + override，禁止手改 YAML）。代码改动仅 `UITheme.cs`（新增 token）。每步以 `read_console`（无报错）+ Game 视图截图比对设计稿作为验证。

**Tech Stack:** Unity (Screen Space - Camera Canvas, CanvasScaler 1080×1920)、TextMeshPro、UnityMCP v3.4.2、mcp-for-unity 工具：`manage_scene` / `manage_gameobject` / `manage_asset` / `read_console` / `manage_editor`。

**Spec:** `docs/superpowers/specs/2026-06-15-lounge-ui-redesign-design.md`

---

## 前置条件（必须先满足）

- [ ] **UnityMCP 工具已加载**：UnityMCP 已注册并连接（`http://127.0.0.1:8080/mcp`），但需**重启 Claude Code** 后本会话才能调用 `manage_scene` 等工具。重启后先确认工具可用（`ToolSearch "select:manage_scene"` 或直接调用 `read_console`）。
- [ ] **Unity 已打开 `Hotel_Rooms_2D_Proto` 场景**，MCP 指向正确实例（多实例时 `set_active_instance`）。
- [ ] 设计稿可参考：浏览器 `lounge-layout.html` / `palette-type.html`（`.superpowers/brainstorm/.../content/`）。

## 关键目标值（来自 spec §3，基准 1080×1920）

颜色：inkDark `#3A2A1C`、inkSoft `#6B5840`、buttonGold `#C2872F`、buttonGoldShadow `#9C6A22`、creamPage `#FFF7E8`、cardWhite `#FFFFFF`。
字号：displayXl 48 / titleLg 40 / titleMd 34 / titleSm 28 / bodyMd 28 / bodySm 24 / caption 20。
形状：屏幕内边距 32、卡片圆角 28、卡片 gap 24、主按钮圆角 24 + 底边投影 6。

---

## Task 0: Game 视图竖屏 + 基线截图（零风险）

**Objects:** Editor Game view；无文件改动。

- [ ] **Step 1: 确认 MCP 可用 & 读控制台基线**
  调用 `read_console`（filter Error/Warning）。Expected: 记录当前已有报错（基线），无新增。
- [ ] **Step 2: 设置 Game 视图为 1080×1920 竖屏**
  通过 `manage_editor` / 菜单设置 Game 视图分辨率为 1080×1920（或 9:16 Portrait）。若 MCP 不支持直接设分辨率，记录为需用户在 Game 视图手动选 1080×1920 的一次性操作。
- [ ] **Step 3: 截图基线**
  截取 Lounge 页面当前状态。Expected: 灰边消失、内容呈完整竖屏（确认「灰边」确为视图比例问题）。保存为对照基线。
- [ ] **Step 4: 无需提交**（仅视图设置）。

---

## Task 1: UITheme token 更新（唯一代码改动）

**Files:**
- Modify: `Assets/Game/Scripts/UI/Theme/UITheme.cs`

- [ ] **Step 1: 新增颜色 + 字号字段并更新默认值**
  在 `UITheme` 中新增并设默认：
```csharp
[Header("Text colors")]
public Color inkDark = Hex("#3A2A1C");
public Color inkSoft = Hex("#6B5840");

[Header("Button")]
public Color buttonGold       = Hex("#C2872F");
public Color buttonGoldShadow = Hex("#9C6A22");
```
  并把字号默认值更新为：`titleLg=40, titleMd=34, titleSm=28, bodyMd=28, bodySm=24, aux=20`，新增 `public float displayXl = 48f;`。在 `Reset()` 中同步这些颜色默认值。
- [ ] **Step 2: 编译验证**
  `read_console`，轮询 `editor_state.isCompiling` 直到完成。Expected: 编译成功，无 Error。
- [ ] **Step 3: 提交**
```bash
git add "Assets/Game/Scripts/UI/Theme/UITheme.cs"
git commit -m "feat(ui): add ink/button colors and bump type-scale tokens in UITheme"
```

---

## Task 2: Hero Banner 重做（视觉主角）

**Objects:** `Assets/Game/Prefabs/UI/Common/Common_HeroBanner.prefab`，及其在 `UI_LoungeScreen` / 场景实例上的 override。

- [ ] **Step 1: 读现有结构**
  `manage_gameobject get_components`（include_properties=false 起步）读取 Common_HeroBanner 根与子物体（背景 Image、标题 TMP、信息位）的层级与 RectTransform。记录当前高度 96 与子物体路径。
- [ ] **Step 2: 抬高 banner 至 ~520**
  设根 RectTransform `m_SizeDelta.y = 520`（保持 AnchorMin.x=0/AnchorMax.x=1 全宽、顶部锚定）。同时检查 Lounge 实例是否对高度有 override，需在实例层一并改对。
- [ ] **Step 3: 加底部暗色渐变 + 标题**
  在 banner 内加（或复用）一个从下到上的暗色渐变 Image（`rgba(0,0,0,0)`→`rgba(42,28,18,0.6)`）；标题 TMP 文本 `☕ The Lounge`，字号 displayXl=48，色 `#FFE7BE`，带描边/阴影，左下角对齐（内边距 32）。
- [ ] **Step 4: 顶栏信息改为浮动胶囊**
  将 Day/Phase/Mood/Money 信息容器移到 banner 顶部，做成半透明胶囊（底 `rgba(42,28,18,0.55)`，圆角，文字 `#FFE7BE` 字号 caption~bodySm）。确认 `TopBarView` 绑定的字段引用不变（只改视觉，不改字段名/层级语义）。
- [ ] **Step 5: 验证**
  `read_console`（无新报错）+ 截图，比对 `lounge-layout.html` 的「建议新版」banner。Expected: banner 满铺、够高、信息清晰浮于其上。
- [ ] **Step 6: 提交**
```bash
git add "Assets/Game/Prefabs/UI/Common/Common_HeroBanner.prefab" "Assets/Game/Scenes/Hotel_Rooms_2D_Proto.unity" "Assets/Game/Prefabs/UI/UI_LoungeScreen.prefab"
git commit -m "feat(ui): rework hero banner into full-bleed hero with floating top-bar pills"
```

---

## Task 3: 库存卡片（Common_InventoryCard）

**Objects:** `Assets/Game/Prefabs/UI/Common/Common_InventoryCard.prefab`；网格容器 `InventoryGrid`（GridLayoutGroup）。

- [ ] **Step 1: 读现有卡片结构 + 网格设置**
  `manage_gameobject get_components` 读卡片（背景 Image、图标 Image、名称 TMP、数量 TMP、进度条）与 `InventoryGrid` 的 GridLayoutGroup（cellSize / spacing / padding）。
- [ ] **Step 2: 卡片底改圆角白卡 + 投影**
  卡片背景 Image 用圆角 sprite（圆角 28；若无圆角 sprite，用 9-slice 圆角图或加 `Shadow`/`Outline`）。底色 cardWhite，加柔和投影观感（底边色 `#E6DCC6`）。
- [ ] **Step 3: 放大图标 + 字号 + 文字色**
  图标 Image 尺寸增大；名称 TMP 字号 titleSm=28 色 inkSoft；数量 TMP 字号 bodyMd=28 色 inkDark。进度条已存在则配色：正常 `#6A9F5C`、低/脏 `#B85842`、轨道 `#ECE2CD`。
- [ ] **Step 4: 网格 = 3 列、gap 24、padding 32**
  GridLayoutGroup：cellSize 适配 3 列（(1080-32*2-24*2)/3 ≈ 320 宽），spacing 24，padding 左右 32。
- [ ] **Step 5: 验证**
  `read_console` + 截图，6 张卡片 3×2 排列、有层次、字清晰。比对设计稿。
- [ ] **Step 6: 提交**
```bash
git add "Assets/Game/Prefabs/UI/Common/Common_InventoryCard.prefab" "Assets/Game/Prefabs/UI/UI_LoungeScreen.prefab" "Assets/Game/Scenes/Hotel_Rooms_2D_Proto.unity"
git commit -m "feat(ui): restyle inventory cards (rounded, shadowed, larger icons/text, progress colors)"
```

---

## Task 4: Dishwasher 宽卡片（Common_DishwasherCard）

**Objects:** `Assets/Game/Prefabs/UI/Common/Common_DishwasherCard.prefab`。

- [ ] **Step 1: 读结构**（图标、状态 TMP、进度条/计时）。
- [ ] **Step 2: 统一为整行圆角白卡**：圆角 28 + 投影；图标 🫧 区、状态文字 bodyMd=28 色 inkDark、计时 bodySm=24 色 inkSoft；进度条配色同 Task 3。左右内边距 32。
- [ ] **Step 3: 验证** `read_console` + 截图比对。
- [ ] **Step 4: 提交**
```bash
git add "Assets/Game/Prefabs/UI/Common/Common_DishwasherCard.prefab" "Assets/Game/Prefabs/UI/UI_LoungeScreen.prefab" "Assets/Game/Scenes/Hotel_Rooms_2D_Proto.unity"
git commit -m "feat(ui): restyle dishwasher card to full-width rounded card"
```

---

## Task 5: 快捷操作按钮（Common_QuickActionButton）

**Objects:** `Assets/Game/Prefabs/UI/Common/Common_QuickActionButton.prefab`；`QuickActions` 容器（HorizontalLayoutGroup）。

- [ ] **Step 1: 读结构**（按钮 Image、图标、文字 TMP、Button/可交互态）。
- [ ] **Step 2: 主按钮果冻感**：启用态用 buttonGold `#C2872F` 面 + 底边 6px 投影 `#9C6A22`，圆角 24，文字 bodyMd=28 加粗、色 `#3A2410`。
- [ ] **Step 3: 禁用态明显灰掉**：`Welcome` / `Refill` 禁用态面 `#E8E0D0`、文字 `#B3A892`、底边投影 `#D6CCB8`（与 `QuickActionButtonView.SetInteractable(false)` 现有逻辑一致，仅改视觉色）。
- [ ] **Step 4: 容器**：HorizontalLayoutGroup 等宽（child force expand width）、spacing 24、padding 32、按钮高 ~84。
- [ ] **Step 5: 验证** `read_console` + 截图，3 按钮：Wash Cups 醒目、另两个明显禁用。
- [ ] **Step 6: 提交**
```bash
git add "Assets/Game/Prefabs/UI/Common/Common_QuickActionButton.prefab" "Assets/Game/Prefabs/UI/UI_LoungeScreen.prefab" "Assets/Game/Scenes/Hotel_Rooms_2D_Proto.unity"
git commit -m "feat(ui): juicy primary action button + clear disabled state"
```

---

## Task 6: 底部导航（Common_BottomNav）

**Objects:** `Assets/Game/Prefabs/UI/Common/Common_BottomNav.prefab`。

- [ ] **Step 1: 读结构**（3 个 tab：图标 + 文字 + 选中态）。
- [ ] **Step 2: 放大图标 + 选中高亮**：图标块增大并圆角；选中 tab 图标底 `#D8A24E` 渐变 + 文字 `#C2872F` 加粗 caption；未选中文字 `#9A8B73`。导航栏底白 `#FFFFFF`，顶部 1px 分隔 `#EFE6D4`。当前页 Lounge 高亮。
- [ ] **Step 3: 验证** `read_console` + 截图。
- [ ] **Step 4: 提交**
```bash
git add "Assets/Game/Prefabs/UI/Common/Common_BottomNav.prefab" "Assets/Game/Prefabs/UI/UI_LoungeScreen.prefab" "Assets/Game/Scenes/Hotel_Rooms_2D_Proto.unity"
git commit -m "feat(ui): bottom nav with larger icons + active highlight"
```

---

## Task 7: 文字色/字号全页扫尾

**Objects:** Lounge 页面下所有 TMP 文本（含 TopBar 残余、分区标签）。

- [ ] **Step 1: 列出 Lounge 下所有 TMP 组件**（`manage_scene get_hierarchy` 分页 + 逐个 `get_components`）。
- [ ] **Step 2: 统一文字色**：主文字 inkDark、次要 inkSoft；**消除任何米色叠米色**。分区标签（如 `INVENTORY`）字号 caption=20、字距 +、色 inkSoft。
- [ ] **Step 3: 复核字号**对齐 spec 阶梯（漏改的补上）。
- [ ] **Step 4: 验证** `read_console` + 整页截图：所有文字在米色底上清晰。
- [ ] **Step 5: 提交**
```bash
git add "Assets/Game/Prefabs/UI" "Assets/Game/Scenes/Hotel_Rooms_2D_Proto.unity"
git commit -m "feat(ui): unify text colors/sizes across Lounge for readability"
```

---

## Task 8: 头像 sprite 清晰度（GuestPortraits1）

**Files:** `Assets/Game/UI/Sprites/Portraits/GuestPortraits1.png`(.meta)

- [ ] **Step 1: 读真实导入设置 + 源尺寸**
  `manage_asset` 读取该 texture 的 importer：当前 maxTextureSize、compression、filterMode、源像素尺寸、是否 mipmap、sprite 切片模式。
- [ ] **Step 2: 调导入设置**
  设 `maxTextureSize` ≥ 源真实尺寸（如 2048）、`textureCompression = High Quality` 或 None、`mipmapEnabled = false`（UI）、`filterMode` 视风格（像素风=Point，插画=Bilinear）。Reimport。
- [ ] **Step 3: 判断是否源图过小**
  若源图本身分辨率不足（Step 1 显示尺寸很小），导入设置无法根治 → 记录并**暂停，向用户报告需重新导出更高清头像图**（不擅自替换美术）。
- [ ] **Step 4: 验证** 截图前台/队列头像清晰（Lounge 本页若无头像，则切到 FrontDesk 截图验证该图）。
- [ ] **Step 5: 提交**
```bash
git add "Assets/Game/UI/Sprites/Portraits/GuestPortraits1.png.meta"
git commit -m "fix(ui): improve guest portrait import settings for clarity"
```

---

## Task 9: 整页验收 + 推广决策

- [ ] **Step 1: 全页截图**对照 `lounge-layout.html` + `palette-type.html`，逐条核对 spec §7 验收标准。
- [ ] **Step 2: `read_console`** 确认无新增 Error/Warning；进入 Play 模式快速验证数据绑定（库存数字、洗碗计时、按钮点击）仍正常。
- [ ] **Step 3: 退出 Play，向用户展示截图**，确认是否将同一套 token/卡片样式推广到 FrontDesk / Rooms（下一份计划）。

---

## Self-Review 记录

- **Spec 覆盖**：§1 痛点→Task0(灰边)、Task2(banner)、Task8(头像)、Task1/3-7(字)；§3 token→Task1；§4 布局→Task2-7；§5 资源→Task8；§6 顺序→Task0-9；§7 验收→Task9。✔ 无遗漏。
- **占位符**：颜色/字号/间距均为具体值；唯一「待 live 读取」的是各对象现有 RectTransform/层级，已显式列为每个 Task 的 Step 1「读结构」，非占位符而是必需的现场勘察。
- **一致性**：颜色/字号 token 名称与 spec §3 一致；与代码现有 `QuickActionButtonView.SetInteractable` / `TopBarView` 字段语义保持不变（只改视觉）。
