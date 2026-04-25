Old Town Hotel
Codex Project Brief / Technical Design Document
Version 0.1
Language: English & Chinese
Engine: Unity URP
Target Platform: Mobile Portrait
Style: 2.5D, Chibi, Cartoon, Easy-to-Develop
1. 项目概述
1.1 项目名称

暂定名：Old Town Hotel
中文暂定名：老城旅行酒店

1.2 项目类型

手机端竖屏经营模拟游戏。
核心为酒店运营经理视角的实时/半实时调度与管理。
玩家通过安排房态周转、前台接待、保洁、查房、Lounge 补给、异常事件处理等操作，使酒店从混乱低分逐步走向高评分、高收益。

1.3 核心卖点
前台排队压力可视化
客房周转链条清晰且具策略性
成长不是纯数值增长，而是逐步解锁更高级的管理工具
酒店运营中的真实矛盾被游戏化
Q版卡通 + 2.5D + 手机竖屏，适合单人独立开发
1.4 核心一句话

玩家扮演一家老旧市中心酒店的运营经理，在有限员工与不断增加的运营压力中，通过更好的调度与管理，让正确的房间在正确的时间变成 Ready，从而降低排队、减少投诉、提升评分和收益。

2. 开发边界与目标
2.1 当前阶段目标

当前阶段不是做完整商业游戏，而是做 第一章节的可玩原型。

2.2 第一阶段原型的成功标准

只要实现以下闭环，即视为成功：

退房
→ 房间变脏
→ 保洁清扫
→ 查房主管检查
→ 房间变 Ready
→ 客人到店
→ 可以直接入住或因为房没好而等待 / 寄存行李
→ 前台排队与压力可视化

2.3 当前明确不做

以下内容当前阶段不做或仅做极简占位：

多酒店章节切换
贷款与完整资产投资系统
复杂财务报表
OTA/渠道管理
完整剧情系统
复杂角色动画
大量特效
完整 3D 高精度美术
真正复杂 AI 自主决策
联机功能
多语言本地化
复杂存档系统
完整新手教程系统
3. 设计支柱（Design Pillars）
3.1 真实运营张力

游戏系统基于真实酒店运营中的矛盾，而不是抽象数值。

主要矛盾包括：

客人到店时间 vs 房间未准备好
前台排队 vs 行李寄存占用前台
快速周转 vs 清洁质量
当前收益 vs 长期评分
员工效率 vs 员工满意度
设施老旧 vs 装修投资
低成本处理投诉 vs 服务补救成本
3.2 成长体现在管理能力

玩家不是开局就全知全能。
成长体现为逐步解锁：

ETA 时间显示
预分配房间
库存查看
员工能力表
更多任务队列能力
更灵活的客诉补救权限
更高级的规则化管理能力
3.3 单店循环先做扎实

第一阶段只做一家酒店，重点是把这家酒店的一天做有趣。
不追求“规模大”，追求“单店循环强”。

3.4 压力必须可视化

玩家要肉眼看到压力：

前台排队长度
客人耐心下降
行李被缓慢拖走
房间状态颜色变化
保洁在走廊行走
查房状态
Lounge 缺杯和补给不足
员工状态卡变化
4. 第一章节定义
4.1 章节名称

Chapter 1：老旧的市中心旅行酒店

4.2 章节主题

在混乱中建立秩序

4.3 酒店基本信息
10 层楼
每层 9 间房
共 90 间房
4.4 当前原型缩放版本

为了开发和验证，当前原型只做：

3 层楼
每层 4 间房
共 12 间房

后续验证成功后再扩展到 10 x 9。

4.5 酒店特征
老旧
市中心
客流较稳定
check-in 高峰明显
房态周转压力大
设施问题较多
偶发虫害与卫生投诉
整体评分有上升空间
5. 美术与视角要求
5.1 美术风格
2.5D
Q版 Chibi
卡通化
低模 / 易开发
暖色系
UI 需清晰、圆角、手机友好
5.2 技术美术方向

建议采用：

Unity 3D 项目
URP
正交相机或轻透视相机
低模场景
低模角色
大量屏幕空间 UI
少量世界空间状态标签
5.3 目标感受

画面应该像“可交互的手机经营游戏截图”，而不是写实模拟器。

6. 平台与输入
6.1 目标平台
Android
iOS
竖屏
6.2 当前开发输入

先支持：

鼠标左键点击（编辑器中模拟触摸）

后续扩展为：

单指点击
可能的拖动视角/轻微镜头移动
6.3 当前不做
多指手势
缩放
长按复杂菜单
7. 单日循环设计
7.1 每日可玩时段

建议运营日为：

10:00 - 22:00

7.2 时段划分
10:00 - 12:00：退房准备期
部分在住房变为待退房
今日退房房间逐步释放
玩家开始安排保洁顺序
还没有大量到店客人
适合做前置规划
12:00 - 15:00：房态转换期
脏房大量出现
保洁开始集中工作
查房主管开始成为瓶颈
ETA 近的客人带来压力
需要优先处理关键房型
15:00 - 18:00：入住高峰期
客人大量到店
前台排队明显
房间若未准备好，则客人等待或寄存行李
行李寄存占用前台人力
客人耐心下降
前期决策在这里结算
18:00 - 22:00：补救与收尾期
剩余晚到客继续入住
Lounge 可能缺货
随机事件更容易出现
玩家处理投诉与补救
做日结算前的调整
8. 三个主要视角
8.1 前台视角

这是压力可视化视角。

视觉要求
固定角度或轻微可旋转
两名前台员工
排队客人
行李拖向寄存间的过程
客人头上偶尔出现耐心下降提示
UI 显示当前排队数量、当前客人状态
玩法功能
查看客人是否能立即入住
若房间未就绪，则可建议寄存行李
处理等待 / 不满 / 补偿
观察前台员工是否被占用
关键体验
看到排队变长
看到行李被慢慢拖走
看到客人耐心下降
感受到“后台没准备好，前台会爆炸”
8.2 客房视角

这是最核心的操作视角。

视觉要求
酒店剖面图
房间按楼层排列
走廊可见
保洁在楼层间行走
查房主管在清洁完成后检查房间
房间状态用颜色和标签表示
房间状态颜色建议
Dirty：红色
Cleaning：蓝色
AwaitingInspection：黄色
Ready：绿色
Blocked：灰色/深红
玩法功能
指派保洁去打扫
观察清洁进度
观察预计完成时间
查看哪些房间今日退房
查看查房状态
后续支持预分配房间
8.3 Lounge / 仓库视角

这是辅助运营冲突视角。

视觉要求
Lounge 休息区
茶/咖啡服务台
洗碗机
脏杯回收区
小仓库
补货与清洗操作区
玩法功能
查看干净杯数量
查看脏杯数量
把脏杯放入洗碗机
从洗碗机取出干净杯
补充牛奶 / 茶包 / 咖啡等
处理缺杯导致的服务中断
9. 核心系统列表
9.1 房态系统
9.2 客人系统
9.3 前台系统
9.4 保洁系统
9.5 查房系统
9.6 Lounge / 库存系统
9.7 事件系统
9.8 评分系统
9.9 成长系统
9.10 员工系统（后期增强）
10. 房态系统设计
10.1 RoomState 枚举

必须包含：

Dirty
Cleaning
AwaitingInspection
Ready

后续可扩展：

Occupied
CheckOutDue
Blocked
DeepCleanDue
10.2 当前原型要求

原型阶段只做以下四种：

Dirty
Cleaning
AwaitingInspection
Ready
10.3 正常状态流转

Dirty
→ Cleaning
→ AwaitingInspection
→ Ready

10.4 后续扩展流转

Occupied
→ CheckOutDue
→ Dirty
→ Cleaning
→ AwaitingInspection
→ Ready
→ Occupied

10.5 失败 / 异常流转

AwaitingInspection
→ InspectionFailed
→ Dirty 或 ReworkNeeded
→ Cleaning
→ AwaitingInspection
→ Ready

10.6 视觉要求

每个房间都必须具备以下视觉信息：

房号
当前状态颜色
必要时的小图标
后续可能有 ETA 紧急标识
后续可能有“已预分配”标记
11. 客人系统设计
11.1 当前原型客人目标

原型阶段的客人系统可以非常简化。
只需要支持“到店 / 等待 / 可入住 / 房未好”这条基本逻辑。

11.2 后续完整客人数据结构

每位客人可具备：

id
名字
人数
ETA
房型要求
偏好
价值等级
耐心值
当前满意度
是否已分配房间
是否已入住
是否在等待
是否已寄存行李
11.3 客人价值分层

后续分四类：

预算客
标准客
高价值客
关键客 / VIP
11.4 客人偏好

后续支持：

高楼层
靠电梯
远离电梯
安静
无要求

当前原型可先不做。

12. 前台系统设计
12.1 前台人数

第一章固定两名前台。

12.2 当前原型前台目标

当前不做完整前台流程，只做以下逻辑准备：

未来支持客人到店
未来支持排队数
未来支持“房间未就绪”提示
12.3 前台后续职责
办理入住
处理等待
寄存行李
处理投诉
补偿客人
必要时支援 Lounge
12.4 关键体验

前台系统不是单独玩，而是承接后台失败的后果。

13. 保洁系统设计
13.1 当前原型必须做
场景里有保洁角色占位物
能指派去处理 Dirty 房间
房间状态变成 Cleaning
一段时间后变成 AwaitingInspection
13.2 当前原型简化规则
只要点中 Dirty 房间，再点“开始清洁”或保洁处理，即可进入 Cleaning
暂时不做复杂路径
可先做瞬移或简化移动
13.3 后续完整设计

每名保洁具备：

清洁速度
质量
疲劳
行走速度
深度清洁能力
当前任务
后续任务队列
13.4 任务队列成长

初始：

当前任务 + 1 个后续任务

成长后：

当前任务 + 2 个
当前任务 + 3 个
更高级自动排序
14. 查房系统设计
14.1 当前原型必须做
房间从 Cleaning 完成后，不直接变 Ready
先进入 AwaitingInspection
必须有一个检查步骤后才变 Ready
14.2 当前原型简化方式
可以先用点击主管按钮 / 点击房间检查
不必先做真实移动
14.3 后续完整设计
查房主管有空闲/忙碌状态
检查耗时
有概率打回返工
返工增加额外时间
15. Lounge / 库存系统设计
15.1 当前原型优先级

低于房态闭环。
当前阶段只需设计文档，不要求立刻实现。

15.2 未来最低可玩版本

支持以下物资：

干净杯
脏杯
牛奶
茶包
咖啡
15.3 杯子循环逻辑

干净杯
→ 被使用
→ 脏杯
→ 放入洗碗机
→ 清洗完成
→ 取出成为干净杯
→ 回到 Lounge

15.4 设计作用

该系统的目的不是做仓储游戏，而是制造运营干扰和资源冲突。

16. 随机事件系统设计
16.1 当前重点事件

蟑螂投诉

16.2 蟑螂事件背景

老旧酒店更容易出现虫害问题。
该事件用于强化：

设施老旧
深度清洁价值
前台安抚能力
补偿成本与评分风险的选择
16.3 触发条件（后续）
老旧度高
深度清洁不足
某些楼层风险更高
入住率高
卫生值偏低
16.4 处理流程

客人投诉
→ 弹出事件面板
→ 玩家选择补偿方案
→ 根据前台能力与客人类型结算结果
→ 原房可能封房
→ 产生后续深度清洁/虫害处理任务

16.5 补偿方案
减免房费
额外服务
礼品
代金券
换房/升级房型（后续可加）
16.6 当前原型状态

先不实现逻辑，只保留设计与未来 UI 目标。

17. 评分系统设计
17.1 不采用单一总分

后续总评分由四维构成：

入住体验
清洁度
设施状态
服务满意度
17.2 当前原型阶段

先不做完整评分，只需在数据结构上预留扩展思路。

17.3 各维影响因素

入住体验：

等待时间
是否准时入住
是否频繁寄存行李

清洁度：

房间清洁是否及时
是否通过查房
是否有虫害/污渍事件

设施状态：

老旧度
故障率
装修改善

服务满意度：

偏好是否满足
投诉处理质量
Lounge 服务状态
18. 成长系统设计
18.1 核心理念

成长不是简单加钱，而是逐步变成更专业的经理。

18.2 第一章节成长解锁内容
ETA 时间显示
预分配房间
库存查看
员工能力表
更高级任务队列
更灵活补偿权限
基础规则化管理
18.3 第一章节建议解锁顺序
基础房态与 check-in 概念
保洁调度
ETA 显示
查房流程
房间偏好与预分配
Lounge 与库存查看
员工能力标签
异常事件处理
轻度装修与长期经营
19. 当前技术目标（最重要）
19.1 当前开发唯一核心目标

实现 客房视角闭环原型

19.2 当前必须实现的最小闭环
房间可视化
房间状态切换
点击房间识别
Dirty 房可以进入 Cleaning
Cleaning 经过一段时间变 AwaitingInspection
AwaitingInspection 经过检查变 Ready
19.3 当前不要求的内容
真正客人排队系统
真正前台入住系统
真正 Lounge 逻辑
真正随机事件
真正存档
真正数值平衡
20. 当前项目文件结构（约定）
Assets/
  Game/
    Art/
    Audio/
    Materials/
    Models/
    Prefabs/
    Scenes/
      Boot.unity
      MainMenu.unity
      Hotel_FrontDesk.unity
      Hotel_Rooms.unity
      Hotel_Lounge.unity
    Scripts/
      Core/
      Data/
      Gameplay/
      UI/
    ScriptableObjects/
    UI/
  Art_External/
  Plugins/

Docs/
  GDD/
  Flowcharts/
  References/
  AI_CONTEXT.md
  TASK_LOG.md
  CODEX_PROJECT_BRIEF.md
21. 脚本架构约定
21.1 当前已经存在或计划存在的脚本
Gameplay
RoomState.cs
RoomController.cs
RoomClickManager.cs
HousekeeperController.cs
InspectorController.cs
RoomPrototypeManager.cs
Core
GameBootstrap.cs
SceneLoader.cs
GameTimeManager.cs
UI
HotelStatusPanelUI.cs
EmployeeStatusPanelUI.cs
BottomNavUI.cs
21.2 当前最重要脚本职责
RoomState.cs

定义房间状态枚举。

RoomController.cs

挂在每个房间上，管理当前状态与材质表现。

必须支持：

currentState
ApplyStateVisual()
CycleState() 或 SetState()
RoomClickManager.cs

负责射线点选房间，找到 RoomController 并调用状态切换逻辑。

HousekeeperController.cs

后续用于表示保洁员工。
当前原型阶段只需要支持：

是否空闲
当前目标房间
接到任务后开始清洁
InspectorController.cs

后续用于表示查房主管。
当前原型阶段只需要支持：

是否空闲
检查房间
将 AwaitingInspection 转为 Ready
RoomPrototypeManager.cs

建议创建一个简单原型管理器，用于：

场景初始化
统一管理测试按钮
统一管理保洁和检查简易逻辑
22. 当前代码规范

Codex 必须遵守以下约定：

22.1 总体原则
初期代码必须可读
不能过度设计
不要一开始就上复杂架构
不要为了“专业”而加太多抽象层
每次只改当前任务相关文件
22.2 命名规则
类名 PascalCase
方法 PascalCase
字段 camelCase 或 public PascalCase 风格按 Unity 常规处理
文件名必须和类名一致
22.3 可维护性要求
方法简短
注释只写必要信息
初期优先易懂，不优先最优雅
22.4 修改边界

Codex 每次只允许修改明确指定的文件。
不得擅自大范围重构。
不得随便改项目设置或场景结构，除非明确要求。

23. 当前开发状态说明（非常重要）
23.1 已完成
Unity URP 项目已创建
基础目录结构已创建
基础场景已创建
Hotel_Rooms 场景已搭出 3 层 4 房方块布局
RoomState.cs 已存在
RoomController.cs 已存在
Dirty / Cleaning / AwaitingInspection / Ready 材质已创建
RoomController 能在 Start/Awake 中运行
23.2 当前问题

点击房间的交互目前存在问题。
OnMouseDown() 未正常触发，因此当前不应再继续依赖 OnMouseDown()。
需要改用更稳的 Raycast 点击方案。

23.3 当前优先级

第一优先级：
让点击房间 → 命中房间 → 切换房间状态 成功。

第二优先级：
让保洁去处理 Dirty 房，自动进入 Cleaning，再变 AwaitingInspection。

第三优先级：
让检查步骤把 AwaitingInspection 变为 Ready。

24. 当前立即要做的任务（给 Codex）

以下是当前最重要的连续任务链。

任务 1：修复点击识别

创建或修正 RoomClickManager.cs

目标：

在 Update 中检测鼠标左键
从 Camera.main 发射射线
如果射线命中带有 RoomController 的物体，则调用 CycleState()
需要输出清晰日志

验收标准：

点击房间时 Console 能打印
房间颜色能切换
任务 2：把状态切换从手动改为流程化

修改 RoomController.cs

目标：

保留 ApplyStateVisual()
支持 SetState(RoomState newState)
允许外部调用而不是只有循环切换

验收标准：

脚本可被其他系统调用设置状态
状态变化后颜色同步更新
任务 3：创建简单保洁原型

创建 HousekeeperController.cs

目标：

场景里有一个占位保洁物体
点击 Dirty 房可将其设为目标房间
保洁进入工作状态
房间状态从 Dirty 变为 Cleaning
等待固定秒数后变 AwaitingInspection

验收标准：

Dirty 房能进入清洁流程
不要求复杂移动
可以先瞬移或简化逻辑
任务 4：创建简单检查原型

创建 InspectorController.cs

目标：

场景里有一个占位主管物体
点击 AwaitingInspection 房间后可执行检查
检查后房间变 Ready

验收标准：

AwaitingInspection 房能变 Ready
可先不做返工概率
任务 5：创建一个最简 UI 面板

可选，在 Hotel_Rooms 场景上创建屏幕 UI

目标：

显示当前选中房间名称
显示当前房间状态
显示保洁是否忙碌
显示主管是否忙碌

验收标准：

只要能实时显示基础信息即可
25. 房间原型数据设计
25.1 每个房间当前原型需要有的字段
roomId（如 101, 102）
currentState
roomRenderer
4 个材质引用
是否可被点击
是否正在被保洁处理
是否等待检查
25.2 后续扩展字段
floor
roomType
guestPreferenceFit
cleanlinessValue
pestRisk
deepCleanValue
allocatedGuestId
etaPriority
26. 保洁原型数据设计
26.1 当前原型字段
isBusy
currentTargetRoom
cleanDurationSeconds
26.2 当前原型行为
若空闲，可接单
接到 Dirty 房后，房间进入 Cleaning
等待固定时长
完成后房间进入 AwaitingInspection
自己恢复为空闲
27. 查房原型数据设计
27.1 当前原型字段
isBusy
currentTargetRoom
inspectDurationSeconds
27.2 当前原型行为
若空闲，可接 AwaitingInspection 房
检查完成后，房间进入 Ready
自己恢复为空闲
28. 场景搭建要求
28.1 当前 Hotel_Rooms 场景最低要求
3x4 房间格
背景墙
Main Camera
Directional Light
一个保洁占位物体
一个查房主管占位物体
28.2 相机要求
竖屏构图优先
能完整看到 12 个房间
后续可微调
不要求现在就有平滑镜头
28.3 当前房间对象要求

每个房间是独立 GameObject。
必须具备：

Mesh Renderer
Collider
RoomController
29. UI 原型要求
29.1 当前最低 UI

当前原型可只做最少的屏幕文本：

当前模式：Room Prototype
选中房间名
当前房间状态
保洁状态
主管状态
29.2 后续第一批正式 UI
左上酒店状态
右上员工状态卡
底部导航栏
右侧房间详情卡

但当前阶段不要求一次做完。

30. Debug 要求
30.1 所有核心点击与状态变化必须打印日志

例如：

Mouse click detected
Hit object: Room_1_1
Cycling room state for Room_1_1
Assigned housekeeper to Room_1_1
Cleaning complete for Room_1_1
Inspection complete for Room_1_1
30.2 当前阶段日志比“优雅”更重要

因为项目还在原型阶段，先跑通再说。

31. Codex 操作原则

Codex 在这个项目里必须遵守以下工作方式：

31.1 每次开始先读文档

先读：

Docs/AI_CONTEXT.md
Docs/TASK_LOG.md
Docs/CODEX_PROJECT_BRIEF.md
31.2 每次只做一个小任务

不要一次生成一整套复杂系统。
必须拆分。

31.3 每次输出时必须说明
改了哪些文件
每个文件的作用
在 Unity 里需要挂到哪个物体
在 Inspector 里要配置什么
如何测试
如果失败怎么排查
31.4 禁止行为
不允许擅自重构全项目
不允许擅自引入复杂第三方架构
不允许擅自修改无关文件
不允许为了“完整性”写太多未来还用不到的系统
32. 给 Codex 的固定提示模板

以后每次可以这样发：

Read these files first:
- Docs/AI_CONTEXT.md
- Docs/TASK_LOG.md
- Docs/CODEX_PROJECT_BRIEF.md

Current task:
[在这里写当前具体任务]

Rules:
1. Only modify the files I explicitly ask for.
2. Keep the implementation beginner-friendly.
3. Do not over-engineer.
4. Explain clearly how to use the code inside Unity.
5. Add debug logs if needed for testing.
33. 当前下一步建议（按顺序）
修复点击房间的 Raycast 方案
让房间能稳定切状态
加一个保洁占位物体
让保洁处理 Dirty 房 → Cleaning → AwaitingInspection
加一个主管占位物体
让主管处理 AwaitingInspection → Ready
做一个极简右侧信息面板
再考虑前台视角原型
34. 重要提醒

当前阶段的目标不是“做得漂亮”，而是：

用最少的系统证明这个玩法是成立的。

只要以下事情成立，这个项目就值得继续：

玩家能看懂房间状态
玩家能通过调度影响房间何时 Ready
房间状态的延迟会带来真实压力
玩家能开始感受到“管理”的乐趣
35. 当前结论

这是一个手机端 2.5D Q版酒店管理模拟游戏原型项目。
当前第一优先级是实现客房周转闭环，而不是前台 UI 或完整经营系统。
所有代码与结构都必须围绕“房态闭环原型”服务。
先验证玩法，再扩展系统。