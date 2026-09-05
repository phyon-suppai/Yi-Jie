---
name: 溢界-相克战斗框架
overview: 在 Godot 4.7 C# 项目「溢界」中实装完整的相消相克战斗框架：三种烦恼与三种武器、相克表仲裁（消散/分裂/debuff）、精力与成就双条及 2×2 状态矩阵、烦恼生成器、传送门通关，并用「烦恼生成比例 + 初始精力」区分学校/工作/老年三个场景。
todos:
  - id: define-types-table
    content: 新建 Types.cs 与 ReactionTable.cs，定义枚举与 3×3 相克表及全部数值常量
    status: in_progress
  - id: implement-worry
    content: 实现 Worry.cs 基类与 worry.tscn，派生 doubt/overload/loss 三个继承场景
    status: completed
    dependencies:
      - define-types-table
  - id: implement-weapons
    content: 实现 Weapon.cs 基类与软锁定，派生笔（单体直线）、纸（群体回旋）、橡皮（群体范围）三个武器场景
    status: completed
    dependencies:
      - define-types-table
  - id: implement-energy-debuff
    content: 实现 EnergySystem 双条与 2×2 状态矩阵、DebuffSystem 三种 debuff、VisionOverlay 视线遮罩与 HUD
    status: in_progress
    dependencies:
      - define-types-table
  - id: refactor-character
    content: 改造 Character.cs 为 8 方向朝向与 Energy，实现崩溃逻辑，更新 player.tscn 并加朝向指示器
    status: pending
    dependencies:
      - implement-energy-debuff
  - id: wire-gamemanager
    content: 改造 GameManager 为唯一裁决点，接入 WorrySpawner 与 Portal，补充 project.godot 输入映射
    status: pending
    dependencies:
      - implement-worry
      - implement-weapons
      - refactor-character
  - id: build-three-levels
    content: 新建 base_level.tscn 公共基底，装配 school/work/family 三关的烦恼比例与初始精力
    status: pending
    dependencies:
      - wire-gamemanager
---

## 产品概述

在 Godot 项目「溢界」中实装完整的「相消相克」战斗框架。游戏讲述一个人从学校、工作到老年家庭的成长过程，路上会遇到三种「烦恼」，玩家用三种「应对武器」处理；**用对方法烦恼消散，用错方法烦恼分裂或给自己加 debuff**。

## 核心玩法

**三种烦恼（敌人）**
- **疑**：自我攻击，原地打转，不主动追人
- **压**：过载，拖着任务箱缓慢移动
- **孤**：失落，半透明、远离玩家，会悄悄贴到身后

**三种武器（无强弱之分，只有攻击方式不同）**
- **笔·行动**：一条直线，射程远，**单体**命中，速度快；盲区是侧面与背后
- **纸·表达**：回旋镖，去程 + 回程弧线，射程中，**群体**命中；盲区是贴脸与超远
- **橡皮·接纳**：以自身为圆心的圆，射程近，**群体**命中，瞬发有冷却；盲区是远距离

三者的盲区互补，节奏差异来自弹道本身（瞬时到达 / 飞一个来回 / 需贴脸），不是强度补偿。

**相克矩阵**（只有消散 / 分裂 / debuff 三种结果，无变异）

| 武器 ↓ / 烦恼 → | 疑 | 压 | 孤 |
|---|---|---|---|
| 笔·行动 | 消散 | 分裂 +1 | debuff「疏离」 |
| 纸·表达 | 分裂 +1 | debuff「怨气」 | 消散 |
| 橡皮·接纳 | debuff「认命」 | 消散 | 分裂 +1 |

- **消散**：烦恼清除，精力 +12，成就 +15，解 1 层 debuff
- **分裂**：烦恼数 +1（同类上限 8），成就 -8
- **debuff**：数量不变，上 1 层（上限 2 层，第 2 层翻倍），重复触发刷新时长。疏离＝视距 -40%/20s；怨气＝流失 +0.2/秒/15s；认命＝移速 -30%/20s

## 操作

纯左手、无鼠标：**WASD** 移动并决定朝向（8 方向，静止时保持最后朝向）；**1 / 2 / 3** 直接使用对应武器（技能槽式，按下即释放，受各自冷却限制，不按就不打，保留「选择不打」的权利）。发射时有软锁定辅助（朝向 ±30° 锥形内修正，强度 0.85），只修正方向，不会拐向玩家没指的目标。

## 双条与状态

- **精力条**：每秒流失 0.5 + 场上烦恼数 × 1.0。精力越低视线越模糊（画面四周渐暗、可视范围收缩）。精力归零触发「崩溃」：失控 3 秒，之后精力回到 35%、成就 -20，不直接死亡
- **成就条**：消散 +15、分裂 -8（下限 0），满 100 时传送门开启，可通往下一关

**精力 × 成就的 2×2 状态矩阵**

|  | 低成就 | 高成就 |
|---|---|---|
| 高精力 | 日常（无增减，开局） | 顺境（移速 +20%，冷却 -30%） |
| 低精力 | 倦怠（视距 -60%，移速 -40%，每 5 秒僵直 2 秒） | 透支（移速 +20%、判定 +50%，但流失 ×1.5、视距 -30%） |

「透支」是刻意的混合态，让玩家在低精力高成就时抉择「见好就收去开门」还是「再赌一把」。状态切换带滞后区间，避免阈值边缘抖动闪烁。

## 关卡

三个场景结构完全一致，**差异仅为烦恼生成比例与初始精力**：学校（疑居多，初始精力 100）→ 工作（压居多，85）→ 老年（孤居多，70）。初始精力递减体现「越走越疲惫」。烦恼由场景中的不可摧毁生成器（靠近激活、定时产出）提供，玩家要做的是跑赢生成速度，而非清零。


## 技术栈

沿用现有项目：Godot 4.7 + C# / .NET（Forward Plus，Windows / d3d12），`window/stretch/mode="canvas_items"`、`aspect="expand"`。不引入任何第三方依赖。

## 实现方案

### 总体策略
以**最小改动现有代码 + 新增独立模块**的方式搭框架。现有 `Character.cs` / `GameManager.cs` / `Heart.cs` 均做「语义升级」而非重写，三个 `.tscn` 关卡通过**继承公共基底场景**复用结构。

### 关键技术决策

**1. 基类策略：按「数据差异」还是「行为差异」划分**

三种烦恼的差异主要在**数据**（移速、AI 模式、生成权重），行为骨架相同；三种武器的差异主要在**行为**（直线 / 回旋 / 范围）。因此：

- 烦恼 = 1 个 `Worry.cs` 脚本 + 1 个 `worry.tscn` 基底 + 3 个**继承场景**（`doubt.tscn` / `overload.tscn` / `loss.tscn`）只覆盖 `[Export]` 参数 —— 符合 Godot 惯用法，也满足「三种都是 scene」
- 武器 = 1 个 `Weapon.cs` 基类（管飞行、软锁定、命中上报、生命周期）+ 3 个子类 + 3 个独立场景

**2. 碰撞方向：武器检测烦恼，裁决交给 GameManager**

武器是高速运动的 `Area2D`，烦恼是慢速 `CharacterBody2D` —— 运动方检测更自然。但**不让武器直接调用烦恼的方法**，而是上报给 `GameManager` 统一裁决，因为相克表必须只有一份：

```mermaid
flowchart LR
    A["按键 1/2/3"] --> B["Character 生成 Weapon"]
    B --> C["Weapon 飞行<br/>软锁定修正方向"]
    C --> D["Area2D.body_entered"]
    D --> E["GameManager.OnHit()<br/>唯一裁决点"]
    E --> F["ReactionTable 查表"]
    F --> G["消散 / 分裂 / debuff"]
    G --> H["EnergySystem 结算"]
    H --> I["状态矩阵 + 视线遮罩 + HUD"]
```

武器只管「我碰到了谁」，烦恼只管「我怎么动、怎么生成」，二者互不知道对方规则。

**3. 碰撞层规划**（`player.tscn` 当前 `collision_mask=2` 需重新分配）

```
Layer 1 = 玩家    Layer 2 = 烦恼    Layer 3 = 武器    Layer 4 = 门
武器 Area2D：collision_layer = 0，collision_mask = 2，monitoring = true
烦恼 CharacterBody2D：collision_layer = 2，collision_mask = 1
```
注意：武器是 Area2D、烦恼是 Body，所以必须用 **`body_entered`** 而非 `area_entered`。

**4. 双条与状态解耦**

精力/成就/状态矩阵抽成独立的 `EnergySystem`（纯规则，不依赖场景），debuff 抽成 `DebuffSystem`，`GameManager` 只做协调。这样调数值只改一处，也便于后续加存档。

**5. 视线模糊：不用 shader**

`CanvasLayer`(layer=100) + 全屏 `TextureRect`（径向渐变：中心透明 → 边缘黑），`Modulate.a` 由 `1 - Energy/100` 驱动，叠加 debuff「疏离」的额外收缩。改动小、效果直接、好调参。

**6. 关卡用继承场景复用结构**

新建 `base_level.tscn` 承载公共骨架（Player + Camera2D + GameManager + HUD + VisionOverlay + Portal + Spawners 容器），`school` / `work` / `family` 三个场景**继承**它，只覆盖 Spawner 的类型配置、`[Export]` 的初始精力，以及各自的 `TileMapLayer` 地板。这直接对应「三个场景只是比例和初始精力不同」的需求，避免三份重复结构。

### 性能要点

- **维护烦恼列表**：`GameManager` 持有 `List<Worry>`，生成/消散/分裂时增删，**不要每帧 `GetNodesInGroup()` 遍历**（精力流失速率每帧都要读烦恼数量，是热路径）
- **命中去重**：纸是回旋镖，同一烦恼在去程与回程可能重复触发；每次飞行用 `HashSet<Worry>` 记录已命中目标，命中过就跳过
- **精力流失改为按 delta 累加**：现有 `Character.Bleed()` 用 1 秒 Timer，改为 `_Process` 中按 `delta` 连续扣减，数值更平滑，也便于 debuff 与「透支」实时修正速率
- **视觉遮罩按需更新**：只在 Energy 或 debuff 层数变化时写 `Modulate.a`，不每帧赋值
- **软锁定遍历**：发射瞬间执行一次（遍历烦恼列表算角度差），不在飞行中持续计算

### 风险控制

- 保留 `Heart.cs` 的五档帧动画逻辑不动，只改语义为精力条（后续换贴图）
- `Character.Bleed()` 里第 36 行的 `throw new NotImplementedException()` 替换为「崩溃」逻辑（失控 3 秒后恢复），不直接死亡
- `school.tscn` 的 `HpText` 调试文本框保留，改为显示精力/成就数值，不出错即可
- 新增脚本在 Godot 4.4+ 会生成 `.cs.uid`，实现后需在编辑器中确认生成，否则场景引用会失败

## 目录结构

```
script/
├── Types.cs              [NEW] 集中定义枚举与数据结构：WorryType / WeaponType / Reaction / DebuffType / PlayerState / ReactionResult。纯 C#，不继承 Godot 类型
├── ReactionTable.cs      [NEW] 静态相克表（3×3 拉丁方）与全部数值常量（消散/分裂/debuff 的精力成就增减、冷却、射程、滞后阈值）。集中配置，便于调参
├── Worry.cs              [NEW] 烦恼基类（CharacterBody2D）。负责移动 AI（不追人/缓慢移动/远离玩家三种模式）、WorryType 暴露、Dissolve() 与 Split() 自身行为。不参与相克判定
├── Weapon.cs             [NEW] 武器基类（Area2D）。负责飞行驱动、软锁定辅助（±30° 锥形、强度 0.85）、命中去重（HashSet）、生命周期回收、命中时上报信号。不查表、不结算
├── ActWeapon.cs          [NEW] 笔：直线弹道、单体命中（命中即销毁）、速度快、短冷却
├── ExpressWeapon.cs      [NEW] 纸：回旋镖弹道（去程到射程末端后弧线折返，往返期间不能再发）、群体命中、路径上目标全部上报
├── AcceptWeapon.cs       [NEW] 橡皮：以玩家自身为圆心的范围判定、群体命中、瞬发、中冷却。无朝向概念
├── WorrySpawner.cs       [NEW] 烦恼生成器（Node2D）。玩家进入 ActivationRange 才激活，按 Interval 定时生成，受该源 MaxAlive 限制。不可摧毁
├── EnergySystem.cs       [NEW] 精力/成就双条与 2×2 状态矩阵。含流失速率计算（0.5 + 烦恼数 × 1.0）、消散回精力、分裂扣成就、崩溃处理、带滞后的状态判定
├── DebuffSystem.cs       [NEW] debuff 层管理。三种 debuff 的层数（上限 2，第 2 层翻倍）、剩余时长、刷新逻辑，对外提供「当前视距系数 / 移速系数 / 额外流失」聚合查询
├── VisionOverlay.cs      [NEW] 视线遮罩（CanvasLayer + TextureRect 径向渐变）。按精力与「疏离」层数驱动不透明度
├── FacingIndicator.cs    [NEW] 朝向指示器（Node2D，随 Facing 旋转的小三角/短线）。因现有角色素材只有侧面 + FlipH，看不出 8 向朝向，必须有此反馈玩家才能瞄准
├── Portal.cs             [NEW] 传送门（Area2D）。成就未满时隐藏/关闭，满 100 时激活并显示 End Portal Open.png，玩家触碰后切换到下一关
└── (改造) Character.cs / GameManager.cs / Heart.cs  [MODIFY] 见下

scenes/object/
├── worry.tscn            [NEW] 烦恼基底场景（CharacterBody2D + CollisionShape2D + Sprite2D），挂 Worry.cs
├── doubt.tscn            [NEW] 继承 worry.tscn，WorryType = Doubt，配置疑的移速与外观
├── overload.tscn         [NEW] 继承 worry.tscn，WorryType = Overload
├── loss.tscn             [NEW] 继承 worry.tscn，WorryType = Loss（半透明）
├── act_weapon.tscn       [NEW] 笔（Area2D + Sprite2D + CollisionShape2D），挂 ActWeapon.cs
├── express_weapon.tscn   [NEW] 纸（同上），挂 ExpressWeapon.cs
├── accept_weapon.tscn    [NEW] 橡皮（范围特效），挂 AcceptWeapon.cs
├── spawner.tscn          [NEW] 生成器（Node2D + Sprite2D），挂 WorrySpawner.cs
├── portal.tscn           [NEW] 传送门（Area2D + End Portal Open.png），挂 Portal.cs
├── vision_overlay.tscn   [NEW] 视线遮罩，挂 VisionOverlay.cs
├── hud.tscn              [NEW] 精力条 + 成就条 + 当前状态名 + 三武器冷却（CanvasLayer）
├── player.tscn           [MODIFY] 重设碰撞层（layer=1）、新增朝向指示器子节点、初始精力参数
└── heart.tscn            [MODIFY] 语义改为精力条（五档逻辑不动，可选换贴图）

scenes/theme/
├── base_level.tscn       [NEW] 关卡公共基底：Player + Camera2D + GameManager + HUD + VisionOverlay + Portal + Spawners 容器
├── school.tscn           [MODIFY] 继承 base_level：疑居多生成器、初始精力 100、学校地板
├── work.tscn             [MODIFY] 继承 base_level：压居多生成器、初始精力 85（当前为空场景，需补全）
├── family.tscn           [MODIFY] 继承 base_level：孤居多生成器、初始精力 70（当前为空场景，需补全）
└── game_manager.tscn     [MODIFY] 按需要补充子节点引用

project.godot             [MODIFY] 新增输入映射 weapon_act(1) / weapon_express(2) / weapon_accept(3)；方向输入 deadzone 由 0.2 降至 0.15 以改善 8 方向识别
```

## 关键代码结构

**1. 核心枚举与反应结果**（`script/Types.cs`）

```csharp
public enum WorryType   { Doubt, Overload, Loss }          // 疑 / 压 / 孤
public enum WeaponType  { Act, Express, Accept }           // 笔 / 纸 / 橡皮
public enum Reaction    { Dissolve, Split, Debuff }        // 消散 / 分裂 / 上debuff（无变异）
public enum DebuffType  { Estrangement, Resentment, Resignation }  // 疏离 / 怨气 / 认命
public enum PlayerState { Normal, Flourish, Burnout, Overdraw }    // 日常 / 顺境 / 倦怠 / 透支

// 一次命中的裁决结果，由 GameManager 产出并交给各系统执行
public readonly struct ReactionResult
{
    public Reaction Kind { get; }        // 消散 / 分裂 / debuff
    public DebuffType Debuff { get; }    // 仅 Kind == Debuff 时有效
}
```

**2. 相克表查询与唯一裁决点**

```csharp
// script/ReactionTable.cs —— 静态数据，不做成节点
public static class ReactionTable
{
    // 3×3 对称拉丁方：每把武器各 1 消散/1 分裂/1 debuff，每种烦恼各吃一次
    public static ReactionResult Resolve(WorryType worry, WeaponType weapon);
}

// script/GameManager.cs —— 唯一裁决点，武器与烦恼都不知道这张表
public partial class GameManager : Node
{
    // 由 Weapon 的 body_entered 回调触发
    public void OnHit(WeaponType weapon, Worry target);

    // 维护场上烦恼列表（精力流失速率的热路径数据源，避免每帧遍历场景树）
    public IReadOnlyList<Worry> Worries { get; }
}
```

**3. 双条与状态系统的对外接口**

```csharp
// script/EnergySystem.cs —— 纯规则，不依赖具体场景
public partial class EnergySystem : Node
{
    public float Energy  { get; }   // 0 ~ MaxEnergy，每秒流失 0.5 + 烦恼数 × 1.0
    public float Achieve { get; }   // 0 ~ 100，消散 +15、分裂 -8
    public PlayerState State { get; }  // 带滞后区间的 2×2 状态判定

    public void ApplyDissolve();   // 精力 +12、成就 +15、解 1 层 debuff
    public void ApplySplit();      // 成就 -8
    public void Tick(double delta, int worryCount, DebuffSystem debuffs);
}
```

## 实施注意事项

- **Godot uid**：新增 `.cs` 后需在编辑器中导入一次以生成 `.cs.uid`，否则 `.tscn` 中的脚本引用会失效
- **8 方向朝向**：用 `Input.GetVector()` 取输入向量，非零时更新 `Facing`，零时保持上一次值；现有 `Character._PhysicsProcess` 的分轴 `if/else if` 结构可替换为 `GetVector()`，天然支持斜向
- **日志**：沿用 Godot 的 `GD.Print`，仅在生成/消散/分裂/状态切换等关键节点打印，严禁每帧输出
- **向后兼容**：`Heart.cs` 的五档判定逻辑原样保留；`school.tscn` 的 `HpText` 暂留作调试显示，不删除以免破坏 `GameManager._Ready()` 的 `GetNode` 引用

