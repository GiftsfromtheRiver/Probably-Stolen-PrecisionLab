# 02 — 机器 / 物品 API 手册（Machine & Item API）

> 游戏：**Probably Stolen 0.46D**（Unity 2021.3.45f2，Mono）
> 反编译源码：`probably-stolen-mod/decompiled/Assembly-CSharp/`（下文 `File.cs:line` 均指该目录，行号为 `read` 工具行号，与 `grep` 一致）
> 真实 mod 参考：`decompiled/ItemDetails/`（IMod/ModHook/ModConfig/tooltip 管线）；官方骨架：`skeleton/`（mod 工程、manifest、构建/信任脚本）
> 本文覆盖：物品模型 → 目录注册 → 机器工厂模板（窗口/电池槽/模块槽/周期/精灵切换）→ 周期/电量/模块加成 → 精灵（含自定义 PNG）→ tooltip → 获取途径 → 数值设置 UI 方案 → 陷阱清单
> 本文的结论已用于 `src/PrecisionLab/`（精确取液器 + 提纯机器），第 10 节是 review 得到的修正项。

---

## 0. TL;DR（给实现者的最短路径）

1. **注册**：IMod 里订阅 `ModHook.OnModItemDirectoryInit`，在回调里 `directory.Add(id, factory)`（`ModHook.cs:10,211-214`；`Directory.Add` 见 `Directory.cs:59-70`）。该事件在 Boot 场景晚于 mod 的 `Init/OnEnable` 触发（有真实运行日志证据，见 §3.3），所以这一条就够；再加一个"兜底目录"子类用于事件万一错过时补注册（§3.4）。
2. **机器工厂**：照抄 `MachinePurifier.WaterPurifier()`（`MachinePurifier.cs:30-112`）：
   `ItemDirectory.CreateEmptyItem().SetContentWindow(win).SetSpriteAndShape(atlas,sprite)` → 名字/描述/价格 → `EnableTag("CONTAINER_TAG"|"manufacture"|"STANDARD_MACHINE_TAG")` + `SetGameItemType("MACHINE")` → `MachineryHelper.InitMachinery(machine, 耗电)` → `MachineHelper.SetupBatterySlot(power)` → `MachineHelper.SetupModuleBay(grid, machine)` → `MachineHelper.SetupManualSlot(note)` → `machine.onCycleEndSlotItemFunc = ...`；完整模板见 §4.2。
3. **周期**：只在 **EndNight()** 遍历玩家商店内 owned 物品并触发 `onCycleEndSlotItemFunc`（`PlayerStore.cs:1654,1719-1723`；扫描范围 `FindAllItem` `PlayerStore.cs:1745-1764`）。电量：`MachineryHelper.GetMachinePowerUsage` + `PowerHelper.CanDrawPowerSource/DrawPowerSource` 直接扣电池（`PowerHelper.cs:653-707`），和原版净水器一致。
4. **获取**：推荐 **Wild Network 网络升级**：`ModHelper.AddNetworkUpgrade(...)` + `ModHelper.SetNetworkTabLabel`，购买时 `Unlock()` 会调用 `NetworkUpgradeList.UnlockActions[id]`（`NetworkUpgrade.cs:841-857`，`WildNetworkUI.cs:582`），在回调里 `ItemSpawner.Spawn(id)` + `GraphUtils.EmporiumTryAdd(item)` 发到玩家商店。调试期用 `ModHelper.AddDebugFunction(... "Spawn" ...)`。
5. **数值设定 UI（µL 剂量）**：机器内参数用 **`MachinerySlider` + `MachineryText` 放进机器窗口的 control panel**，数值存物品 tag（自动存档）；全局默认值用 `ModConfig.Define<int>(min,max)`。**不要**为单个机器实例做自定义窗口（§9）。
6. **精灵**：`Textures/Items/*.png` 自定义图（16 的倍数、点过滤、alpha 决定占格）；**原版机器不是 1×1 而是 48×48（3×3）+ 带容器时 48×64（3×4）**，证据见 §6.3；要视觉一致就画 48×48 / 48×64。

---

## 1. 物品模型：`GameItem`

抽象基类 `GameItem : GameItemFunc, GraphNodeStorage, DirectoryEntry`（`GameItem.cs:6`）。`CreateEmptyItem()` 实际构造 `GameItemElement(identifier, "Items/itematlas", "unknown")`（`ItemDirectory.cs:10-13`）。

### 1.1 关键字段 / 属性（`GameItem.cs:48-154`）

| 成员 | 行 | 说明 |
|---|---|---|
| `identifier` / `identifierName` | 64-66 | 目录 ID（`Directory.Create` 在工厂返回**之后**赋值，工厂内部为 null！不要依赖） |
| `uniqueId` | 68 | 由 `DirectoryMaster.Item` 分配 |
| `name` / `shortDescription` / `longDescription` / `flavorText` | 70-76 | tooltip 文案 |
| `unitCount` / `unitValue` | 78-80 | 数量/单价 |
| `contentWindow` (`PixelWindow`) | 108 | 容器/机器窗口；`SetContentWindow` `GameItem.cs:481-489` |
| `examineWindow` | 110 | 放大镜窗口，可不设 |
| `spritePath` / `spriteAtlasPath` / `useSpriteFromMod` | 120-126 | 精灵解析；`SetSpriteAndShape` `338-345`；`SetSpriteAndShapeFromMod` `385-416` |
| `itemTypes: List<string>` | 12 | `SetGameItemType` `2229-2237`、`IsGameItemType` `2238-2241`、`GetGameItemType` `2272-2276` |
| `state` / `modifiedState: TagSystem` | 60-62 | 所有自定义数据都存 tag（随存档序列化，见 §5.5） |

### 1.2 回调（`GameItemFunc.cs`，全部是 `public ... { get; set; }`）

| 回调 | 行 | 用途 |
|---|---|---|
| `mayInventoryAddItemFunc` / `mayInventoryRemoveItemFunc` | 6/8 | 库存接收/移除检查（`(item, inventory) => bool`） |
| `onSlotAddItemFunc` / `onSlotAddItemEndFunc` / `onSlotRemoveItemEndFunc` | 20-24 | 槽位事件（inventory 级回调，见 `GameInventoryFunc`；机器窗口里用） |
| `onCycleEndEarly/Slot/LateSlotItemFunc` | 90-94 | **EndNight 周期回调**（含 turbo booster 手动触发） |
| `onTurnEndSlotItemFunc` | 96 | 回合结束（ship/战斗回合用，机器一般不用） |
| `onOneSecondItemFunc` | 98 | 每秒 tick（`EmporiumEntry.cs:540-553` 对商店内所有物品调用；机器窗口控件刷新常用） |
| `onLoaded` | 100 | 读档后回调（`PlayerStore.cs:1095-1098`） |
| `maySelect/canSelectSlotItemFunc` | 44-46 | 点击选中（拖拽目标/激活前置） |
| `mayActivate/canActivateSlotItemFunc`, `onActivateSlotItemFunc` | 78-82 | "激活"（双击/单击选项） |
| `mayThis/canThisTargetItemFunc`, `onThisTargetItemFunc` | 54-58 | **"本物品拖到别的物品上"**（`GameItem.cs:1036-1055 Target()` 调用 `onThisTargetItemFunc(this,target)`） |
| `mayItemTargetThisFunc` 等 | 60-64 | "别的物品拖到本物品上"（返回 false 可禁止别人作用于自己） |
| `tooltipRichTextFunc` | 42 | tooltip 完全接管（一般不用，改用 ModHook） |

交互触发链（重要，决定 UX）：
- 拖拽 A 到 B → `ItemMouseDragHandler.EndDrag` 判定 `currentItem.MayTarget(lastItem)&&CanTarget` → `GameItem.Target()`（`ItemMouseDragHandler.cs:589-594`；`GameItem.cs:942-1055`）。
- 双击物品 → `ItemMouseDoubleClickHandler.ClickAction`：有 `contentWindow` 则开关窗口；然后若 `activate && MayActivateSlotItem()` 就 `ActivateSlotItem()`（`ItemMouseDoubleClickHandler.cs:125-175`）。**所以双击可激活物品（切值/循环）**。
- 单击（可选 `EmporiumOption.SingleClickActivate/SingleClickInteract` 时）也可能触发，`ItemMouseDoubleClickHandler.cs:85-102`。
- `activateDefault=true` 的物品走 `ItemDefaultActivateHandler`（按键激活，`ItemDefaultActivateHandler.cs:29-65`）。

### 1.3 Tag 系统（`TagState.cs` / `TagSystem.cs`）

```csharp
// 写入（action 同步执行；skipValidate=true 跳过 ValidateShapeState，热路径/批量写用）
item.EnableTag("MY_FLAG");                       // GameItem.cs:691-703
item.DisableTag("MY_FLAG");                      // GameItem.cs:705-717
item.ModifyTag("MY_INT", s => s.Enable().SetInt(42));   // GameItem.cs:678-689
// 读取
bool on  = item.IsTag("MY_FLAG");                // GameItem.cs:656-659
int  v   = item.GetTagReadonly("MY_INT").GetInt();      // GameItem.cs:673-676（返回 Clone）
```
`TagState` 存储：`valueEnabled` + `valueInt/valueFloat/valueLong/valueDouble/valueBool/valueString`，链式 `Enable()/Disable()/SetInt()/SetBool()/SetString()/SetDouble()`（`TagState.cs:64-186`）。未设置的 tag 读出来是默认 `TagState`（0/false/""），**不需要预初始化**（但初始化一下更可读）。

### 1.4 目录 & 注册：`Directory<T>` / `ItemDirectory` / `ModItemDirectory` / `DirectoryMaster` / `ItemSpawner`

- `Directory<T> : MonoBehaviour`（`Directory.cs:5`）：`Awake()` 里 `factoryDictionary = new ...; if (DirectoryMaster.AddDirectory(this)) InitDirectory();`（`Directory.cs:9-17`）。**注意 Awake 是 private 且定义在泛型基类里，Unity 仍会调用**——原版所有 `*ItemDirectory` 都这样工作（运行日志里的 duplicate 警告就是它打出来的，见 §3.3）。
- `ItemDirectory : Directory<GameItem>`（`ItemDirectory.cs:3`），`ModItemDirectory : ItemDirectory`，其 `InitDirectory()` 只做一件事：`ModHook.FireOnModItemDirectoryInit(this)`（`ModItemDirectory.cs:3-6`）。
- `Directory<T>.Add(string, Func<T>)`（`Directory.cs:59-70`）→ 若同 ID 已在 `DirectoryMaster` 会先 `Remove` 再 `TryAdd`。`Has` `86-93`，`Remove` `95-102`。
- `DirectoryMaster`（`DirectoryMaster.cs`）：静态、跨场景；`Create<GameItem>` 依次询问所有已注册 `Directory<GameItem>`（`82-103`）；`Has<T>(id)` `105-122`；`Remove<T>(id)` `143-160`；**`Item(string id, bool isOwned = true)` `246-273`**：`Create<GameItem>` → 清子物品警告 → `GeneralHelper.SetItemOwned(item,isOwned)` → 分配 `uniqueId` → `PermitHelper.OnNewItemCreated`；找不到时返回 dummy `CreateEmptyItem(id)` 并打 warning。
- `ItemSpawner.Spawn(id)`（`ItemSpawner.cs:3-10`）：先查 `PreBuiltItemHelper.preBuiltItems`，否则 `DirectoryMaster.Item(id)`。
- 注册时机由**场景里的 `ModItemDirectory` 组件**驱动，事件 `ModHook.OnModItemDirectoryInit` 定义 `ModHook.cs:10`，触发 `211-214`。参考实现：`PaperBookRegistry.cs:585-606`（官方自己就是这么注册 mod 纸本书物品的）。

---

## 2. 所有权 / 可放置性 / 存档

- 所有权：`GeneralHelper.SetItemOwned(item,bool)`（`GeneralHelper.cs:176`）、`IsItemOwned`（`188`）、`IsItemEffectivelyOwned`（含父物品链）。`DirectoryMaster.Item` 默认 `isOwned:true`（`DirectoryMaster.cs:246,262`）。
- **商店可放置性没有任何机器白名单**：主库存/展示柜/隐藏格的接收规则就是 `GeneralHelper.IsItemOwned(item)`（`EmporiumEntry.cs:1412-1415`，绑定于 `EmporiumEntry.cs:360-362`）；前台/后柜台只额外排除"室外+危险废料"（`363-364`）。所以任何 owned 物品都能放柜台——原版机器就是这样"放置"的。
- 机器不能塞进背包/容器：`ContainerHelper.InitContainerItem` / `CanContainerStoredItem` 明确拒绝 `IsGameItemType("MACHINE")`（`ContainerHelper.cs:159-162,190-193`）。
- 存档：`SaveManager.EncodeNodes/DecodeNodes`（`SaveManager.cs:49-111,139-245`）保存 `identifier/state(TagSystem)/shape/values/spriteAtlasPath/spritePath/children`；读档用 `DirectoryMaster.Item(savedItem.identifier)` 重新跑工厂 → 恢复 state/shape → 再用 `UncheckedAccept` 把子物品塞回窗口（`SaveManager.cs:157-186,187-243`）。**结论：工厂必须用固定顺序构造窗口/槽位**（读档按 storage 顺序回填）。
- 读档后处理：`PlayerStore.DecodeSaveItem` 对每个物品调用 `MachineHelper.LoadUpgrade`、`MachineHelper.OnLoad`、`onLoaded` 等（`PlayerStore.cs:1078-1112`）；机器不需要额外接线。
- 周期前后顺序（`PlayerStore.LoadGame`）：`ES3.LoadInto(playerStore)` → `NetworkUpgrade.AddMissingAfterLoad()`（`PlayerStore.cs:980`）→ `ModHook.FireOnGameLoadedNormal()`（`997`，**在 DecodeSaveItem 之前**）→ `DecodeSaveItem(...)`（`998+`）。

---

## 3. 注册 mod 物品（精确配方）

### 3.1 事件式注册（主路径，已由真实运行日志验证）

```csharp
public sealed class ModEntry : IMod
{
    private const string ModId = "precision_lab";

    public void Init(ModManifest manifest) { /* ModConfig / 网络升级注册（见 §8） */ }

    public void OnEnable()
    {
        ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
        // 其它 hook…
    }

    public void OnDisable()
    {
        ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
    }

    private void OnModItemDirectoryInit(ModItemDirectory directory)
    {
        if (directory == null) return;
        if (!directory.Has("precision_purifier"))
            directory.Add("precision_purifier", ItemFactory.CreatePurifier);
        if (!directory.Has("precision_liquid_extractor"))
            directory.Add("precision_liquid_extractor", ItemFactory.CreateExtractor);
    }
}
```
- 回调在 `ModHook.SafeInvoke` 里逐个 try/catch（`ModHook.cs:109-128,211-214`），抛异常不会拖垮别的 mod。
- **`OnEnable` 订阅时机够早**（见 §3.3 证据）。也可在 `Init` 里订阅，但 `OnEnable` 是骨架示例的惯例且 `Init` 也可能被别的东西提前调用，两者都行。

### 3.2 工厂函数签名

```csharp
public static GameItem CreatePurifier();           // 每次生成新实例都会被调用
```
工厂只负责"新建一个空物品 + 配好结构和回调"，**不要**在工厂里读 `item.identifier`（此时还是 null）、不要往世界里放东西、不要引用已销毁对象。

### 3.3 真机证据：`ModItemDirectory` 会在主菜单/Boot 场景醒来，且晚于 mod 初始化

真实 `Player.log`（`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log`，2026-10-30 的运行）：
- 第 20-46 行：`[ModLoader] 1 mod(s) enabled` → 载入 `ItemDetails.dll` → `Init`/`OnEnable` 日志。**此时场景里的目录组件还没 Awake**。
- 第 80-107 行：出现一整批
  `Trying to add a duplicate Directory 'MiscItemDirectory' … 'ModItemDirectory' … in DirectoryMaster, ignoring duplicated directory.`
  —— 说明场景里的 `*ItemDirectory`（含 `ModItemDirectory`）在这一刻第一次 Awake；重复的是第二个场景的副本（`Directory<T>.Create` 只查 `DirectoryMaster` 里第一个注册的实例，所以后续副本被拒，事件也只 fire 一次）。
- 因此：**mod 在 `OnEnable` 订阅 `OnModItemDirectoryInit` 一定能收到第一次 fire**；物品在进入游戏前就已注册，`DirectoryMaster.Item(id)`（读档、调试生成、网络升级发奖）都能拿到真物品。

旁证：`Directory<T>.Awake` 虽然是 private，但原版派生目录（如 `FurnitureItemDirectory`，`FurnitureItemDirectory.cs:9-39`）都依赖它注册工厂；日志里的 duplicate 警告正来自这段代码。

### 3.4 兜底注册（可选，但强烈建议保留）

场景/事件顺序变化、或其它 mod 破坏了 UI 根时，可自建一个目录组件兜底。**做法**：派生子类（类型不同，`DirectoryMaster` 的"重复类型"检查不会拒绝），在第一个可用时机挂到 `ModLoader.Instance.gameObject`（它 `DontDestroyOnLoad`，`ModLoader.cs:130-138`）**或**新建一个 `DontDestroyOnLoad` GameObject：

```csharp
internal sealed class ModItemDirectoryFallback : ModItemDirectory { } // 继承 InitDirectory → 触发同一事件

// 确保可用（在 OnGameLoadedNormal / OnNewGameStarted / 发奖前调用）：
private static void EnsureItemsRegistered()
{
    if (DirectoryMaster.Has<GameItem>("precision_purifier")) return;      // DirectoryMaster.cs:105-122
    var host = ModLoader.Instance;
    if (host == null) return;
    host.gameObject.AddComponent<ModItemDirectoryFallback>();
    // Awake 立即执行 → AddDirectory → InitDirectory → OnModItemDirectoryInit → 上面的事件回调完成注册
}
```
- 安全前提已确认：Unity 会调用基类里 private 的 `Awake`（原版就靠它，§3.3）；`AddComponent` 是同步的。
- 触发点建议：`ModHook.OnGameLoadedNormal`（读档；`PlayerStore.cs:997` 在 `DecodeSaveItem` 之前，兜底仍能救旧存档里的 mod 物品）**加上** `ModHook.OnNewGameStarted`（新游戏不触发 `OnGameLoadedNormal`！`ModHook.cs:82,391-394`）+ 发奖函数入口再检查一次。
- 事件回调里也可用 `DirectoryMaster.Has<GameItem>(id)` 判重，避免把工厂在目录之间搬来搬去（会有 warning 日志，但功能正常）。

---

## 4. 机器工厂模板（copy-ready）

### 4.1 一台"正常商店机器"必须有的东西（对照 vanilla `MachinePurifier.cs:30-112`）

| 要素 | 做法 | 依据 |
|---|---|---|
| 物品类型 | `SetGameItemType("MACHINE")`（`InitMachinery` 也会设） | `MachinePurifier.cs:51`；`MachineryHelper.cs:119` |
| 内容窗口 | `SetContentWindow(MachineHelper.CreateMachineryWindow(...))`，样式 `WindowStyle.Machinery` | `MachinePurifier.cs:42,143-150`；`MachineHelper.cs:56-68` |
| 窗口面板顺序 | **power(0) → module(1) → container(2) → note(3)**：`MachineHelper.GetBatterySlot/GetModuleInv/GetSlot(index)` 依赖顺序 | `MachineHelper.cs:119-166`；`MachineryPanelRow.GetStorage` `MachineryPanelRow.cs:168-185`（按面板 attach 顺序收集） |
| 电池槽 | `MachineHelper.SetupBatterySlot(powerSlot, machine)`：只收 `power_source_item`+owned，加换电池音效 | `MachineHelper.cs:41-54` |
| 模块槽 | `MachineHelper.SetupModuleBay(moduleGrid, machine)`：只收 `MODULE_TYPE_UNIVERSAL`+owned，自动 Compute/Apply 模块加成、刷新耗电 | `MachineHelper.cs:307-338`（= `MachinePurifier.cs:57-78` 的手写版） |
| 便签槽 | `MachineHelper.SetupManualSlot(noteSlot)`（只收 postit） | `MachineHelper.cs:340-344` |
| 机器 tag | `EnableTag("CONTAINER_TAG")`、`EnableTag("manufacture")`、`EnableTag(MachineHelper.STANDARD_MACHINE_TAG)`；`InitMachinery` 补 `machinery`/`IMPORTANT_TAG`/耗电 tag | `MachinePurifier.cs:48-52`；`MachineryHelper.cs:117-153` |
| 初耗电 | `MachineryHelper.InitMachinery(machine, drawnPower)`，`drawnPower` = 每次动作从电池扣的电 | `MachinePurifier.cs:52`；`GetMachinePowerUsage` `MachineryHelper.cs:194-197` |
| 质量比 | `ModifyTag("MACHINE_QUALITY_RATIO_INT", s => s.Enable().SetInt(2))`（tooltip 里显示 ratio 1/2；原版净水器=2） | `MachinePurifier.cs:53-56` |
| 周期回调 | `machine.onCycleEndSlotItemFunc = (self,inv,slot) => Cycle(...)` | `MachinePurifier.cs:79-102`；`PlayerStore.cs:1719-1723` |
| 外观切换 | 容器放入/取出时 `SetSpriteAndShape`（或 `SetSpriteAndShapeFromMod`） | `MachinePurifier.cs:103-110` |
| 声音（可选） | `AudioHelper.InitMetalMachine(machine)`（原版净水器没调，蒸发器调了） | `MachineEvaporator.cs:22`；`AudioHelper.cs:638` |

> **不需要**：额外的家具/放置白名单、`MachineHelper.LoadUpgrade` 手动接线（读档时 `DecodeSaveItem` 自动调，`MODULE_UPGRADE_STAGE_INT` 缺省=0 → no-op，`MachineHelper.cs:267-287`）、`onLoaded` 手工恢复（容器/模块/电池子物品在读档时经 `UncheckedAccept` 回填，会触发 inventory 的 `onSlotAddItemFunc`/`onSlotAddItemEndFunc`：`GameSlotInventory.cs:499-541`、`GameGridInventory.cs:712-744`）。

### 4.2 完整机器模板（带电池/模块/容器/便签 + 周期 + 精灵切换）

```csharp
using System;
using UnityEngine;

internal static class MachineFactoryExample
{
    internal static GameItem CreatePurifierLikeMachine()
    {
        // 1) 槽位（顺序 = 面板顺序 = MachineHelper.GetSlot 的下标）
        var powerSlot     = new GameSlotInventory();
        var moduleGrid    = new GameGridInventory(MachineHelper.DEFAULT_MODULE_BAY_CONFIG,
                                                  MachineHelper.DEFAULT_MODULE_BAY_WIDTH);
        var containerSlot = new GameSlotInventory();
        var noteSlot      = new GameSlotInventory();

        // 2) 机器窗口（WindowStyle.Machinery、可拖动、居中）
        PixelWindow window = MachineHelper.CreateMachineryWindow(
            true,
            MachineHelper.PowerPanel(powerSlot),                                        // index 0
            MachineHelper.Panel("MODULES",   moduleGrid,    "module"),                  // index 1
            MachineHelper.Panel("CONTAINER", containerSlot, "container"),               // index 2
            MachineHelper.NotePanel(noteSlot));                                         // index 3

        // 3) 物品本体
        GameItem machine = ItemDirectory.CreateEmptyItem()
            .SetContentWindow(window)
            .SetSpriteAndShape("Items/items_tool2", "purifier");   // 或 SetSpriteAndShapeFromMod(...)
        machine.name = "Purification Machine";
        machine.shortDescription = "…";
        machine.flavorText = "…";
        machine.unitValue = 2400L;
        machine.SetGameItemType("MACHINE");
        machine.EnableTag("CONTAINER_TAG");
        machine.EnableTag("manufacture");
        machine.EnableTag(MachineHelper.STANDARD_MACHINE_TAG);
        MachineryHelper.InitMachinery(machine, 6);                  // 6 电/次（电池 kJ）
        machine.ModifyTag("MACHINE_QUALITY_RATIO_INT", s => s.Enable().SetInt(2));

        // 4) 电池 / 模块 / 便签
        MachineHelper.SetupBatterySlot(powerSlot, machine);
        MachineHelper.SetupModuleBay(moduleGrid, machine);
        MachineHelper.SetupManualSlot(noteSlot);

        // 5) 容器槽规则 + 外观切换
        containerSlot.mayInventoryAddItemFunc = (item, inv) =>
            GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
        containerSlot.SetBackgroundFadeSprite("Items/water_container4", "xl");
        containerSlot.onSlotAddItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
            containerSlot.onSlotAddItemFunc, delegate
            {
                machine.SetSpriteAndShape("Items/items_tool2", "purifier_w");
            });
        containerSlot.onSlotRemoveItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
            containerSlot.onSlotRemoveItemFunc, delegate
            {
                machine.SetSpriteAndShape("Items/items_tool2", "purifier");
            });

        // 6) 周期：EndNight（+ turbo booster）触发
        machine.onCycleEndSlotItemFunc = delegate (GameItem self, GameInventory parent, SlotMarker slot)
        {
            GameItem container = containerSlot.childItem;
            if (container == null) return;
            int cost = MachineryHelper.GetMachinePowerUsage(self);
            if (powerSlot.childItem == null ||
                !PowerHelper.CanDrawPowerSource(powerSlot.childItem, cost)) return;   // 没电什么都不做
            // …做机器的事（用自己的算法/调 LiquidTools 之类）…
            PowerHelper.DrawPowerSource(powerSlot.childItem, cost);                   // 扣电池
            MachineHelper.OnMachineActioned(self, moduleGrid);                       // 触发模块"使用"效果
        };

        return machine;
    }
}
```

要点：
- `MachineHelper.Panel(label, content, role)` 会自动把库存包进 `MachineryIntake`（`MachineHelper.cs:70-97`）；`role` 只是调试/样式用，但按原版习惯写 `"module"/"container"/"control"`。
- 想读第 2 个槽位（例如 tooltip 里显示容器信息）：`MachineHelper.GetSlot(machine, 2)`（`MachineHelper.cs:148-151`；`GetBodyStorage` `119-131`）。
- `machine_bay_ext` 等"模块仓升级"物品对 `STANDARD_MACHINE_TAG` 机器自动生效（`MachineHelper.ExpandModule/LoadUpgrade` `245-287`），不用自己实现。

### 4.3 最简机器（没有模块槽 / 只有电池+容器）

```csharp
var power = new GameSlotInventory();
var container = new GameSlotInventory();
PixelWindow win = MachineHelper.CreateMachineryWindow(true,
    MachineHelper.PowerPanel(power),                                     // index 0
    MachineHelper.Panel("CONTAINER", container, "container"));           // index 1
GameItem m = ItemDirectory.CreateEmptyItem().SetContentWindow(win)
    .SetSpriteAndShape("Items/items_tool2", "evaporator");
m.SetGameItemType("MACHINE");
m.EnableTag("CONTAINER_TAG");
m.EnableTag(MachineHelper.STANDARD_MACHINE_TAG);
MachineryHelper.InitMachinery(m, 4);
MachineHelper.SetupBatterySlot(power, m);
m.onCycleEndSlotItemFunc = delegate { /* 自己检查容器+电池并干活 */ };
```
注意：没有模块槽就没有 `TOTAL_PERCENTAGE_*` 加成（`GetCurrentPerformanceBonus`/`GetCurrentQualityBonus` 恒为 0，`MachineryHelper.cs:532-541`）；`MachineHelper.GetModuleInv(machine)`（`GetBodyStorage(1)`）会返回 null，别硬取。

### 4.4 带数值控制面板的机器（µL 剂量 / 温度 / 百分比）

```csharp
// 面板里放：标题文本 + 滑块 + 当前值文本
var valueText = new MachineryText("1000 µL", 6, TMPro.HorizontalAlignmentOptions.Center, uppercase: false);
var slider    = new MachinerySlider(1000f).SetRange(1f, 10000f, 1f);   // MachinerySlider.cs:67,165-172
var controlPanel = MachineHelper.Panel("METERING",
    MachineryStack.Vertical(valueText, slider), "control");
win = MachineHelper.CreateMachineryWindow(true, MachineHelper.PowerPanel(power),
    MachineHelper.Panel("MODULE", moduleGrid, "module"),
    MachineHelper.Panel("CONTAINER", containerSlot, "container"), controlPanel);

slider.onChanged = delegate (float v)
{
    int dose = Mathf.RoundToInt(v);
    machine.ModifyTag("MY_DOSE_UL", s => s.Enable().SetInt(dose));   // 每个机器实例独立，随存档保存
    valueText.SetText(dose + " µL");
};
machine.onLoaded = (Action<GameItem>)Delegate.Combine(machine.onLoaded, delegate (GameItem loaded)
{
    int saved = loaded.GetTagReadonly("MY_DOSE_UL").GetInt();
    if (saved <= 0) saved = 1000;                                    // 旧档/新档兜底
    slider.SetInt(saved);
    valueText.SetText(saved + " µL");
});
```
- `MachinerySlider`：`SetRange(min,max,step)`、`SetValue(v, notify)`、`SetInt/GetInt`、`onChanged/onReleased`（`MachinerySlider.cs:165-201`）。`MachineryText`：`new MachineryText(text, size, alignment, uppercase)`、`SetText`（`MachineryText.cs:30-79`）。`MachineryButton`：`new MachineryButton(text, callback, width, height)` / `SetCallback`（`MachineryButton.cs:43-70,119-123`）。`MachineryStack.Vertical/Horizontal`（`MachineryStack.cs:57-75`）。`MachineryRadioList` 见 `MachineHeatingPlate.cs:210-217`。
- 这些都是 `PixelElement`，直接放进 `MachineHelper.Panel` 的 content 即可；机器窗口里已有官方样式（不需要自定义 UI）。
- 同上模式的原版实例：`MachineFeedDispenser.CreateFeedDispenser`（滑块+文本+`onLoaded` 恢复，`MachineFeedDispenser.cs:58-97`）、`MachineHeatingPlate.HeatingPlate`（+/- 按钮+`onOneSecondItemFunc` 刷新，`MachineHeatingPlate.cs:16-133`）。

---

## 5. 周期 / 电量 / 模块加成

### 5.1 周期触发点

| 触发 | 时机/条件 | 代码 |
|---|---|---|
| `onCycleEndSlotItemFunc` | **EndNight()**：`FindAllItem()`（默认只扫 owned 物品，范围=前台/后柜/后柜-柜台/主库存/展示柜/垃圾桶/文件/隐藏/夜间/自动售货窗口）+ `StolenHelper.UpdateStolenItem` 后触发 | `PlayerStore.cs:1654-1743`；`FindAllItem` `1745-1764` |
| `onCycleEndLateSlotItemFunc` | 同 EndNight 再后面（`HopperHelper.RunWindow(2)` 之后） | `PlayerStore.cs:1725-1728` |
| `onCycleEndEarlySlotItemFunc` | EndNight 最开头 + turbo booster | `PlayerStore.cs:1668-1671`；`MachineTurboBoosterAdv.cs:143-147` |
| turbo booster 手动加周期 | 玩家把 turbo booster 拖到机器上 → `BoostMachine(machine)` 直接调用机器的 early+normal 周期回调 | `MachineTurboBoosterAdv.cs:33-40,143-147`；`ToolDirectory.cs:497-503` |
| `onOneSecondItemFunc` | 商店内每 1 秒（刷新 UI 用） | `EmporiumEntry.cs:540-553` |

> `STANDARD_MACHINE_TAG` 的机器自动是 `TURBO_BOOST_ELIGIBLE`（`IsTurboBoostEligible` 对 STANDARD 机器直接返回 true，`MachineTurboBoosterAdv.cs:33-40`），**不需要**再 `EnableTag("TURBO_BOOST_ELIGIBLE_TAG")`（该 tag 只给非标准机器如蒸发器/冰箱用，`MachineEvaporator.cs:21`）。

### 5.2 电量（电池槽模式，原版净水器/蒸发器同款）

- 机器 tag：`machinery_drawn_power`（当前每次耗电）与 `MACHINERY_DRAWN_POWER_BASE`（基础值），由 `InitMachinery(machine, n)` 写入（`MachineryHelper.cs:122-129`）。
- 取当前耗电：`MachineryHelper.GetMachinePowerUsage(machine)`（`MachineryHelper.cs:194-197`），模块效率会通过 `ModuleHelper.ApplyBasicModuleEffect` 改写 `machinery_drawn_power`（`ModuleHelper.cs:235-277`）。
- 电池槽接口：`MachineHelper.SetupBatterySlot(slot, machine)`；电池物品是 `power_source_item`（能量/带宽 tag：`power_source_item_energy/bandwidth/max_energy`，`PowerHelper.cs:6-30`）。示例：`battery100` = 100 kJ 能量、10 带宽（`MiscItemDirectory.cs:145-150`）；所以耗电 6 的机器满电≈16 个周期。
- 扣电：`PowerHelper.CanDrawPowerSource(battery, amount)`（`PowerHelper.cs:681-706`）→ 干事 → `PowerHelper.DrawPowerSource(battery, amount)`（`653-679`；会刷新电池图标）。**原版净水器也是同一套**（`MachinePurifier.cs:87-99`）。
- 充电：电池只能靠电网/充电器充（`PowerHelper.ChargeBattery` `540-589`；`MachineHelper.RechargeMachineBattery` `295-305`）。机器本身不会给电池充电（原版也没有）。
- 商店电网（`PowerHelper.HandlePower`，`54-86`）只作用于 `machinery` + `power_source_furniture` 的船用设备；`is_machinery_powered` 开关对电池槽机器无实际影响，`InitMachinery` 默认 true 即可。

### 5.3 模块加成

- 每个模块物品带 `TEMP_PERCENTAGE_PERFORMANCE_INT / TEMP_PERCENTAGE_EFFICIENCY_INT / TEMP_PERCENTAGE_QUALITY_INT`；`ModuleHelper.ComputeModuleEffect` 汇总到机器的：
  `TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT`、`TOTAL_PERCENTAGE_POWER_BONUS_INT`、`TOTAL_PERCENTAGE_QUALITY_BONUS_INT`（`ModuleHelper.cs:201-233`）。
- `ModuleHelper.ApplyBasicModuleEffect` 用这些值改写 `CURRENT_PROCESSING_SPEED_TAG` 和 `machinery_drawn_power`（`235-277`）。
- 读加成：`MachineryHelper.GetCurrentPerformanceBonus / GetCurrentQualityBonus`（`532-541`），原版净水器即用它们算处理量/纯度上限（`MachinePurifier.cs:121-138`）。
- 机器可用的模块类型由 `SetupModuleBay` 的 `allowedModuleTypes` 决定，默认 `MODULE_TYPE_UNIVERSAL`（`MachineHelper.cs:307-313`；常量 `ModuleHelper.cs:17`）。若想用化学模块，用 `MachineBlender.SetupChemModuleGrid` 风格 `AllowOnlyTaggedItemsOr(grid, new[]{"MODULE_TYPE_CHEM"}, true)`（`MachineBlender.cs:61-89`）。
- 做事完成后调用 `MachineHelper.OnMachineActioned(machine, moduleGrid)`：触发模块的"使用"效果并重算临时加成（`MachineHelper.cs:368-378`）。

### 5.4 机器状态 tag 速查

| tag | 写入处 | 作用 |
|---|---|---|
| `machinery`, `is_machinery_powered`, `machinery_drawn_power`, `MACHINERY_DRAWN_POWER_BASE`, `machinery_received_power` | `InitMachinery` | 机器基础/耗电/开关 |
| `STANDARD_MACHINE_TAG` | 工厂 | 标准机器：模块仓升级、turbo booster 资格、成就判定 |
| `MACHINE_QUALITY_RATIO_INT` | 工厂 | 质量加成显示比例 |
| `MODULE_UPGRADE_STAGE_INT` | 模块仓升级 | 0/1/2 级扩展模块仓 |
| `LOCKED_WORKING_TAG` | 可选 | 机器工作时锁模块仓（`MachineHelper.LockModuleInv` `169-179`） |
| `CONTAINER_TAG` | 工厂 | 容器语义（tooltip、禁止嵌套进容器） |
| `manufacture` | 原版工厂写法 | **反编译中未发现消费者**（grep 全库只有写入），保留以对齐原版即可 |
| `IS_PROCESSING_TYPE_MACHINE` 系列 | `MachineryHelper.InitProgressTypeMachinery` `155-192` | 进度条型机器（逐回合推进，`PROCESSING_*` tag + tooltip 自动显示进度） |

### 5.5 自定义数值的持久化

`TagState` 全部随物品 `state` 序列化（`SaveItemNode.cs:14,56-77`；`SaveManager.cs:56`）。所以把每台机器的参数（剂量、模式、计数器）写成 tag 就会自动存档/读档；读档后想刷新 UI 控件就在 `machine.onLoaded` 里读 tag 回填（`PlayerStore.cs:1095-1098`）。

---

## 6. 精灵：原版图集 + 自定义 PNG

### 6.1 解析链

- `SetSpriteAndShape(atlasPath, spriteName)`（`GameItem.cs:338-345`）→ `RenderHandler.LoadFromAtlas` = `Resources.LoadAll<Sprite>(atlasPath)`（`RenderHandler.cs:331-368`）→ 用 sprite 的 `rect` 按 `UISettings.current.gridSize`（默认 **16**，`UISettings.cs:12`）切出物品形状（`GameItem.cs:347-371`）；sprite 尺寸不是 16 的倍数会在 log 里告警。
- 自定义 mod 图：`GameItem.SetSpriteAndShapeFromMod(modId, spriteKey)`（`GameItem.cs:385-416`）：设 `spriteAtlasPath=modId`、`spritePath=spriteKey`、`useSpriteFromMod=true`，按 alpha 采样生成形状；渲染时 `GameItemElement.ResolveSpriteByName` 从 `LoadedMod.Sprites` 取（`GameItemElement.cs:197-215`）。
- mod PNG 装载：`<mod>/Textures/Items/*.png` → `ModLoader.LoadTexturesFromFolder(..., pixelArt:true)`：`Texture2D` + `filterMode=Point` + `Sprite.Create(rect=全图, pivot=(0.5,0.5), ppu=100)`（`ModLoader.cs:1117-1158`）。子目录 `Textures/` 根下的 PNG 进 `LoadedMod.Textures`（无 Items/ 前缀），`Textures/Paper` 进 `PaperSprites`。

### 6.2 自定义 PNG 要求与回退

- **尺寸**：必须是 **16 的倍数且 ≥16**（`SetSpriteAndShapeFromMod` 用 `w/16` 且**没有** `Math.Max(1,...)`，`GameItem.cs:397-400`；<16 会得到 0 格非法形状）。48×48 → 3×3 格；32×32 → 2×2；16×16 → 1×1。
- **内容**：alpha 决定占格（按每 16px 格子中心采样，`GameItem.cs:406-411`）；画满整张图 = 占满该矩形；四周留 1px 透明也无妨（中心仍采样到）。点过滤/像素风，建议与 16px 网格对齐。
- **回退模式**（必备，防止美术缺失时形状/图标坏掉）：
```csharp
internal static void ApplySprite(GameItem item, string modSpriteKey, string fallbackAtlas, string fallbackSprite)
{
    var mod = ModLoader.Mods.Find(m => m != null && m.Manifest != null && m.Manifest.ID == "precision_lab");
    if (mod != null && mod.Sprites != null && mod.Sprites.ContainsKey(modSpriteKey))
        item.SetSpriteAndShapeFromMod("precision_lab", modSpriteKey);
    else
        item.SetSpriteAndShape(fallbackAtlas, fallbackSprite);      // 原版图集兜底
}
```
- 形状（占格）会随存档保存（`SaveManager.cs:163`），所以日后改图片尺寸不会把已放好的机器变形。
- ⚠️ **不要把 mod 物品 id 交给 `SpriteHelper.UpdateWaterContainerSprite`**：它对未知 id 会提前 return（`SpriteHelper.cs:80-89`），所以安全；但不要给 mod 液体容器实现 `getItemSpriteModifierForItemFunc` 去引用不存在的图集叠加层，否则 `RenderHandler.LoadFromAtlas` 会对每个空图片段打 warning 并返回 unknown。

### 6.3 原版机器实际占格（重要修正）

从 `Probably Stolen_Data/resources.assets` 中直接解码 Sprite 对象的 `m_Rect`（像素）得到：

| 图集/名字 | Sprite 名称 | 像素矩形 | 占格（grid=16） |
|---|---|---|---|
| `Items/items_tool2` | `purifier` | 48×48 @(336,160) | **3×3** |
| `Items/items_tool2` | `purifier_w` | 48×64 @(384,240) | **3×4**（放入水容器后变高） |
| `Items/items_tool2` | `evaporator` / `blender` / `chem_finisher` / `heating_plate` / `furnace` | 48×48 | 3×3 |
| `Items/items_tool2` | `bottle_printer` | 32×48 | 2×3 |
| `Items/water_container3` | `1ml` | 16×32 | 1×2（滴管） |
| `Items/water_container4` | `xl`（水桶） | 64×96 | 4×6 |

**结论**：原版净水器 **不是 1×1**；16×16 的自定义图做出来的机器是 1×1（能跑、不崩，但视觉上只有原版机器 1/9 大）。若要与原版一致，请画 **48×48（idle）+ 48×64（active）** 的 PNG；若接受紧凑的 1×1 机器则保持 16×16（设计上要明确）。

### 6.4 可复用的原版机器/工具精灵（候选）

来自全部 `SetSpriteAndShape` 调用的枚举（图集 → 精灵名）：

- **机器类** `Items/items_tool2`：`purifier`, `purifier_w`, `chem_finisher`, `chem_finisher_b`, `evaporator`, `evaporator_b`, `blender`, `blender_b`, `furnace`, `heating_plate`, `heating_plate_b`, `printer_off`, `printer_on`, `projector`, `water_tank`, `machine_bay0`, `machine_bay_ext`, `hypertech_bay`, `current_converter`, `toolbox_old`, `gibber`, `battery_hyp`, `sbattery0/10/20/40/60/80/100`
- `Items/items_tool`：`bottle_printer`, `dealmaker`, `desequencer`, `desequencer_r`, `moisture_farm`, `moisture_farm_w`, `recharger_base`, `storage_coordinator`, `turbo_booster`, `turbo_booster_adv`, `turbo_booster_adv_charging`, `turbo_booster_adv_ready`, `labeler`, `labeler_blue`, `magnifier`, `spectral_analyser`, `alarm_system`, `alarm_system_r`, `mind_reprogrammer`, `postit`, `battery0/10/20/40/60/80/100`, `battery_ext*`, `battery_dis*`, `printer_chip*`, `*_module`
- `Items/items_machine`：`armor_conditionner`, `automatic_meat_processor`, `battery_hub`, `industrial_fridge`, `meat_culture_machine`, `power_analyser`, `recycler`, `speaker`
- `Items/items_amenities`：`portable_water_purifier`, `water_filter`, `water_filter_adv`, `chem_scanner`, `water_test_container`, `uv`
- **液体计量容器（取液器输入/输出）**：`Items/water_container3` → `1ml`, `5ml`, `25ml`, `50ml`, `100ml`, `large`, `mini`, `normal`, `small`；`Items/water_container4` → `xl`, `pitcher`；`Items/items_food` → `water_bottle_full_premium` 等。
- **"取液/移液"感觉的机器图**：`purifier`/`purifier_w`（净水）、`chem_finisher`（化学终端）、`spectral_analyser`（仪器）、`bottle_printer`（瓶子机）；`Items/items_module` 里还有 `module_extractor` / `module_extractor_adv`（提取模块图标，做小图标可用）。若画自定义图，建议取液器用滴管+刻度造型、提纯机器沿用 `purifier` 轮廓 + 滤芯。

> 原版液体容器的定义（容量/是否刻度/颜色叠加）参考 `FoodItemDirectory.cs:252-321`（`1ml_dropper` 1 mL、`5ml_dropper` 5 mL、`25/50/100ml_cylinder`、`large_bottled_water` 2000 mL、`water_jug` 6000 mL 等），它们的 sprite 用 `Items/water_container3`。

---

## 7. Tooltip

### 7.1 管线

`GameItemElement.GetTooltipBasic()` 是所有物品 tooltip 的唯一入口（`GameItemElement.cs:960-1138`）：
1. 通用行 → 2. **原版专用行**（一行一个静态方法；机器相关自动调用：`MachineryHelper.CreateMachineryTooltip` `1070`、`MachineProgressHelper` `1071`、`WaterHelper.CreateWaterContainerTooltip` `1082` 等）→ 3. `ModInternal.CreateTooltipEarly` → `ModHook.OnCreateTooltipEarly`（`1114` 处是 Late）→ 4. `ModInternal.CreateTooltipLate` → **`ModHook.OnCreateTooltipLate`**（`1114`，定义 `ModHook.cs:64`）→ 5. `shortDescription`/`flavorText` 等。

- **机器通用行自动就有**：只要物品带 `machinery` tag，就会显示 "Required energy / 模块加成 / ratio"（`MachineryHelper.CreateMachineryTooltip` `305-393`、`AddTooltipModuleBonus` `395-474`）——mod 机器不用自己写耗电/加成的 tooltip。
- **液体容器通用行自动就有**：带 `LIQUID_CONTAINER_TAG` 的物品会显示容量/液量/纯度条（`WaterHelper.CreateWaterContainerTooltip` `517-555`；`GRADUATED_CONTAINER_TAG` 显示精确 "x ml / y ml"，否则显示 "Almost Empty" 之类）。
- 想追加自定义行 → 订阅 `OnCreateTooltipLate`（或 Early，在通用行之前）。

### 7.2 Hook 代码

```csharp
public void OnEnable()  { ModHook.OnCreateTooltipLate += OnTooltipLate; }
public void OnDisable() { ModHook.OnCreateTooltipLate -= OnTooltipLate; }   // 静态方法组 -= 有效

private static void OnTooltipLate(RichTextBuilder builder, GameItem item)
{
    if (builder == null || item == null) return;
    if (item.identifier == "precision_liquid_extractor")
    {
        int dose = item.GetTagReadonly("MY_DOSE_UL").GetInt();
        builder.AddLine("Metering: " + dose + " µL",   // (text, ignoreEmpty, color, bold, …)
            false, RenderHandler.ColorPalette.LightCyan);
    }
    else if (item.identifier == "precision_purifier")
    {
        int cap = /* … */ 9900;
        builder.AddLine("Purity cap: " + (cap / 100.0).ToString("0.##") + "%",
            true, RenderHandler.ColorPalette.LightCyan);
    }
}
```
- 签名：`RichTextBuilder.AddLine(string text = null, bool ignoreEmpty = true, RenderHandler.ColorPalette color = None, bool bold=false, bool italic=false, bool underline=false, bool strikethrough=false, …)`（`RichTextBuilder.cs:328-345`）；`Add(...)` `360-408`；`AddProgressBar` `288-327`；颜色 `RenderHandler.ColorPalette`（`RenderHandler.cs:10-…`，常用 `LightCyan/LightGreen/LightRed/Gray`）。
- 注意参数 2 是 **ignoreEmpty**：传 `false` = 即使空串也保留该行；`true` = 空串就跳过。
- 图片里可用 `<link>`/`<sprite name="info">` 信息图标：`MachineryText.WithInfoLink`（`MachineryText.cs:42-54`）是机器窗口内的写法；tooltip 里可用 `RichTextBuilder.AddIcon`（`409-413`）。
- 本地化：`LocHelper.GetLocalizedMechanic(key, args)` / `GetLocalizedMechanicOrDefault(key, en, args)`（`LocHelper.cs:104-108,168`）、`GetLocalizedItem`（163）、`GetLocalizedUIOrDefault`（99）。mod 自己的本地化用 `ModHelper.GetLocalized(modId,key,args)` 读 `Localization/<locale>.csv`（`ModLoader.cs:1183-1227`；`ModHelper.cs:91-134`）。
- 注意 hook 对**每个物品每次 hover** 都会跑：保持轻量、别做布局/IO、用 try/catch 包住（`ModHook.SafeInvoke` 已兜异常，但每帧报错会刷 log）。

---

## 8. 获取方式（acquisition）

### 8.1 推荐：Wild Network 网络升级（黑网购买 → 直接发到玩家商店）

vanilla 的解锁动作就是"给物品"（如 `RETIRED_CHEMIST` 给 blender/1ml 量筒，`NetworkUpgradeList.cs:375-397`；`WATER_TRADER` 给 portable purifier，`398-413`）。mod API：

```csharp
// Init(manifest) 里注册（此时 PlayerStore 可能为 null，AddNetworkUpgrade 内部已处理）
ModHelper.SetNetworkTabLabel("precision_lab", () => Loc.Get("upgrade.tab", "Precision Lab"));

ModHelper.AddNetworkUpgrade(
    modId: "precision_lab",
    id: "PRECISION_LAB_PURIFIER",
    cost: 2400,
    subtitle: "Precision Lab",                      // 卡片分组 key（同 subtitle 的升级排在同一张卡）
    title: () => Loc.Get("upgrade.purifier.title", "Purification Machine"),
    description: () => Loc.Get("upgrade.purifier.desc", "…"),
    onUnlock: () => GiveItem("precision_purifier"),
    tab: "precision_lab",                           // 自定义页签
    iconKey: null,                                  // null → 通用图标兜底；可写 "precision_lab:my_icon"
    prerequisites: null,
    cooldownDays: 0,
    subtitleLabel: () => Loc.Get("upgrade.purifier.subtitle", "…"),  // 卡片标题（本地化）
    alreadyBought: () => Loc.Get("upgrade.purifier.bought", "Ordered."),
    recommended: false);

private static void GiveItem(string id)
{
    if (!DirectoryMaster.Has<GameItem>(id)) return;              // 防御：别发 dummy
    GameItem item = ItemSpawner.Spawn(id);                       // ItemSpawner.cs:3-10
    if (item == null) return;
    GeneralHelper.SetItemOwned(item, true);
    GraphUtils.EmporiumTryAdd(item);                             // GraphUtils.cs:12-18,106-109（会找空位，满了才销毁）
}
```

工作链（已验证）：
- `ModHelper.AddNetworkUpgrade` → `NetworkUpgrade.RegisterModUpgrade`（`ModHelper.cs:253-281`；`NetworkUpgrade.cs:263-285`）→ 定义存 `modDefinitions` + 回调进 `NetworkUpgradeList.UnlockActions`。
- **新游戏**：`PlayerStore.StartNewGame` → `networkUpgrade = NetworkUpgrade.InitUpgradeDict()`，会把 modDefinitions 合并进去（`PlayerStore.cs:787`；`NetworkUpgrade.cs:601-611`）。
- **旧存档**：`LoadGame` → `ES3.LoadInto` → `NetworkUpgrade.AddMissingAfterLoad()` 把缺的（含 mod 的）补进运行时字典（`PlayerStore.cs:971,980`；`NetworkUpgrade.cs:615-640`）；`WildNetworkUI.Open` 还会再补一次（`WildNetworkUI.cs:100`）。
- **UI**：`WildNetworkUI.BuildTabs` 只挑"字典里存在 + `!hidden` + `!IsHiddenForRun`"的升级（`WildNetworkUI.cs:164-166`；`NetworkUpgrade.IsHiddenForRun` `658-669` 只对 `tab=="Advanced"` 且未买店面的情况隐藏）；自定义 tab 由 `NetworkUpgradeLayout.Build` 自动建（`NetworkUpgradeLayout.cs:43-57`），页签标题来自 `SetNetworkTabLabel`（`NetworkUpgrade.cs:306-319,357-373`）。
- **购买**：`WildNetworkUI.OnUseClicked` 扣现金/黑市 favor → `value.Unlock()`（`WildNetworkUI.cs:560-590` 的 `582`）→ `Unlock()` 里 `NetworkUpgradeList.UnlockActions[id]?.Invoke()`（`NetworkUpgrade.cs:841-857`）→ 你的 onUnlock 发奖。
- 注意：`AddNetworkUpgrade` 参数顺序见 `ModHelper.cs:253`；`hidden` 不暴露（默认 false），`lockInDemo` 默认 false，`cooldownDays=0` → 一次性购买。

**坑（必看）**：
1. 发奖别只用 `ModHelper.MoveItemToInventory`（= `GraphUtils.TryAcceptAll(invElement)`，主库存满了物品就悬空丢失，`ModHelper.cs:56-69`）；用 `GraphUtils.EmporiumTryAdd` 或自己按 主库存→后柜→前台 顺序兜底。
2. 发奖前确认物品已注册（`DirectoryMaster.Has<GameItem>(id)`），否则 `ItemSpawner.Spawn` 返回 dummy（`DirectoryMaster.cs:271-272`），玩家花了钱拿不到东西。
3. 卡片标题：同一 `subtitle` 的多个升级排在同一张卡，卡片标题取第一格的 `subtitleLabel`（`WildNetworkUI.cs:299-316`）——两个升级若给不同的 `subtitle` 会分卡；若想同卡且标题统一，给两者同一个 `subtitleLabel`。
4. `iconKey=null` 有兜底（`WildNetworkUI.ResolveIcon` `371-391` 最终用 `BUY_REFERAL` 图标）；自定义图标 key 支持 `"modId:spriteKey"`（`CustomUIManager.GetSprite` `776-809`，mod 图来自 `LoadedMod.Sprites`）。

### 8.2 其它途径（按可靠性排序）

| 途径 | 代码 | 说明 |
|---|---|---|
| **调试生成（开发/测试首选）** | `ModHelper.AddDebugFunction(modId, ModHelper.DEBUG_CATEGORY_SPAWN, "spawn purifier", () => GiveItem("precision_purifier"))`（`ModHelper.cs:176-179`） | 出现在 F9 调试面板 "Spawn" 分类（`CustomUIDebugPanel.cs:78-80,198-222`；需要 `dev.txt`，见骨架 README） |
| 物品生成器窗口 | `CustomUIItemSpawner.Open()`（public static，`CustomUIItemSpawner.cs:181-244`） | 列出 `DirectoryMaster.GetIdentifierList<GameItem>()` 的全部 ID（含 mod 物品），点击即 `GraphUtils.EmporiumTryAdd`（`631-654`） |
| **起始场景** | `ModHelper.AddStartingScenario(..., startWith, onStart, ...)`（`ModHelper.cs:325-351`） | ⚠️ `startWith` 只是 UI 里显示的说明文字（`NewGameSpecializationUI.cs:276-280`），**不会真的发物品**；真正发物品要在 `onStart`（新游戏流程里调用，`NewGameData.cs:356-374`）。示例：新开一局给玩家一台机器：`onStart: () => { GiveItem(id); }` |
| 战利品表 | `ModHelper.RegisterLootTable(modId, tableId, (itemId, weight)…)` / `AddLootEntry`（`ModHelper.cs:248-251,832-845`；`LootRegistry.cs:217-320`）；也可放 `<mod>/LootTables/*.json` 由 `ModLootFile.Load` 自动应用（`ModLoader.cs:1110`） | 反编译里出现的 vanilla 表 id：`accessCardTable`, `allModuleTable`, `householdTable`, `junkTable`, `makeshiftWeaponTable`, `materialTable`, `medicalTable`, `packedFoodTable`, `scavInspectable`, `toolTable`（来自源码字符串；`LootRegistry.TableIds` `64-71` 可运行时枚举）。适合"低概率拾荒获得"的补充，不适合作为唯一获取途径 |
| 店面购买/网络升级之外 | — | 没有"商店直接进货 mod 物品"的 API；`StoreClientList*` 的购买逻辑按 tag/type 匹配，给物品加 `TOOL`/`WATER` 等 type 后顾客可能主动买它，但不是"获得"途径 |

**推荐组合**：网络升级（正式获取）+ 起始场景/调试按钮（测试）+ 战利品低概率（可选）。

---

## 9. 数值设置 UI：结论与方案（µL 精确剂量）

### 9.1 判定

| 方案 | 可行性 | 适用 | 结论 |
|---|---|---|---|
| **机器窗口内 `MachinerySlider`+`MachineryText`**（原版 `MachineFeedDispenser` 做法） | ✅ 官方控件、零额外窗口生命周期 | 每台机器实例的参数 | **推荐**（§4.4 模板） |
| 物品 `onActivateSlotItemFunc` 循环预设值（双击物品切换） | ✅ 已实现（`ItemFactory.CycleDose`），交互最省事 | 手持工具的快速切换 | 作为辅助 |
| **`ModConfig.Define<int>(key, def, section, label, min, max)`** | ✅ 持久化到 `%persistentDataPath%/ModConfigs/<mod>.json`，在 Mods 面板的齿轮设置窗口显示；有范围时渲染为 Slider（`ModConfig.cs:84-126`；`ConfigEntry.cs:34-39`；`ModConfigWindow.cs:97-115`） | **全局默认值**（新造物品的剂量） | 推荐作为默认值 |
| `ModHelper.AddGameOptionSlider(modId, section, optionId, label, get, set, min, max, wholeNumbers, tooltip, valueText)` | ✅ 进游戏设置菜单（`ModHelper.cs:217-222`；`GameOptionRegistry.cs:26-67`） | 全局数值（不想放在 mod 设置窗口时） | 可用，但语义是"游戏选项"，不是物品参数 |
| 自定义窗口 `CustomUIManager.Window/CreateWindow` + `CustomUIBuilder.AddSlider/AddInput/AddButton` | ✅ 能跑（游戏自带的 F9 物品生成器就是这样做的：`CustomUIItemSpawner.cs:192-244`；API 见 `CustomUIBuilder.cs:70-564`、`CustomUIWindow.cs:26-180`） | 复杂多控件设置面板 | **不推荐**用于单台机器：要自己记录"当前选中的机器实例"、管理窗口开合/层级/重建，收益低 |
| 游戏选项里做输入框 | ❌ 只有 toggle/slider/dropdown/keybind 四类（`GameOptionRegistry.Kind` `7-13`），没有自由文本 | — | 用 slider 取整即可 |

> 注意：`ModConfig.Define` 的 int/float 重载**不传 min/max 时**在设置窗口里是文本框（`ModConfigWindow.cs:103-115`），传了才是滑块；读取侧仍要自己 `Mathf.Clamp`。

### 9.2 推荐实现（组合）

```csharp
// (a) 全局默认剂量：ModConfig（mod 设置窗口，滑块 1..10000 µL）
_doseEntry = ModConfig.For(ModId).Define("dose_ul", 1000, "Precision Lab",
    () => ModHelper.GetLocalized(ModId, "config.dose.label"),
    1, 10000,                                   // ← 传范围才会渲染成 slider
    () => ModHelper.GetLocalized(ModId, "config.dose.tip"));

// (b) 每台机器/工具实例：item tag 存值；机器窗口用 MachinerySlider（§4.4）；
//     手持工具用双击 Activate 循环预设值（ItemFactory.CycleDose），tooltip 显示当前值。
```

如果想把工具做成"双击打开一个小窗口调数值"：`GameItem` 本身可以带 `contentWindow`（普通 `new PixelWindow(true, "METERING", WindowStyle.Machinery)` + 一个 `MachinerySlider` + `MachineryText`），双击即开关（`ItemMouseDoubleClickHandler.cs:141-158`）。但这会让工具带一个机器风格窗口，体验上不如"窗口内 slider 的机器 + 快捷循环的工具"组合，除非需求明确要独立窗口。

---

## 10. 陷阱清单（编译通过但运行时出事 / 行为不对）

1. **发奖用 `ModHelper.MoveItemToInventory` 会丢物品**（主库存满时物品悬空、钱已扣）：改 `GraphUtils.EmporiumTryAdd(item)`，并检查 `item.parentInventory != null`。
2. **新游戏不触发 `OnGameLoadedNormal`**：兜底/自检只有读档才跑；要额外挂 `ModHook.OnNewGameStarted`（`ModHook.cs:82,391-394`），并在发奖函数入口做 `DirectoryMaster.Has` 检查。
3. **物品未注册时发奖 = 发 dummy**（`DirectoryMaster.cs:271-272`）：所有发奖/生成入口先 `DirectoryMaster.Has<GameItem>(id)`。
4. **工厂里读 `item.identifier` 是 null**：ID 由 `Directory.Create` 在工厂返回后赋值（`Directory.cs:24-37`）。需要 ID 的逻辑（按 identifier 分支/tooltip/精灵判断）放到回调或运行时。
5. **读档依赖工厂的固定结构顺序**：窗口/面板/槽位顺序必须每次一致（`SaveManager.cs:187-243` 按 children 索引回填子物品）。不要按存档内容动态改面板数量/顺序。
6. **`GetBodyStorage/GetSlot(index)` 依赖面板顺序**：power=0、module=1、container=2（`MachineHelper.cs:119-166`；`MachineryPanelRow.cs:168-185`）。工具/提示里硬取 index 时务必和工厂保持一致。
7. **周期做的事要先检查电池**：`powerSlot.childItem == null` 或 `CanDrawPowerSource` 返回 false 时直接 return（原版净水器同款逻辑，`MachinePurifier.cs:85-101`），否则会出现"无电也生效"。
8. **`onCycleEndSlotItemFunc` 只在 EndNight 跑**：游戏内白天不会自动跑；想"随时可点"的机器（如搅拌机）要像 `MachineBlender` 那样在窗口里放 `MachineryButton` 手动触发（`MachineBlender.cs:27-46,96`）。turbo booster 是"额外一次周期"的官方手段（§5.1）。
9. **`ModifyTag` 的闭包捕获是安全的**：action 同步执行（`GameItem.cs:678-689`），循环里写 tag 不需担心延迟捕获；但若把 `TagState`/局部变量**存起来稍后用**再 ModifyTag，记得重新 `GetTagReadonly`。
10. **批量/热路径写 tag 用 `skipValidate:true`**（跳过 `ValidateShapeState`，液体 part 写入的原版做法见 `WaterHelper.cs:305-312`）；但**形状相关**（`SetSpriteAndShape` 后）不要 skip，否则布局不刷新。
11. **读档后 UI 控件要回填**：机器窗口内的 slider/text 不会自动恢复，在 `machine.onLoaded` 里读 tag 设置（`MachineFeedDispenser.cs:91-97`；触发点 `PlayerStore.cs:1095-1098`）。
12. **自定义精灵最小 16×16、16 的倍数**（§6.2），否则形状 0 格（物品不可见/布局异常）。图集兜底一定要写。
13. **mod 液体容器不要再走 `SpriteHelper.UpdateWaterContainerSprite`**：它对未知 id 提前 return（安全），但如果给 mod 物品实现 `getItemSpriteModifierForItemFunc` 引用不存在的图集切片会刷 warning/unknown 图。
14. **Tooltip 不要重复原版行**：机器耗电/模块加成、液体容量/纯度都已自动加（`GameItemElement.cs:1070,1082`）；`GRADUATED_CONTAINER_TAG` 会显示精确 "x ml / y ml"（`WaterHelper.cs:529-534`），你的 µL 行应表述为更精确的信息而不是再复制一份容量。
15. **`ModHook` 事件是静态、跨场景**：`OnDisable` 里务必退订（`ModLoader.OnApplicationQuit` 才调用，`ModLoader.cs:1280-1297`）；handler 抛异常会被 `SafeInvoke` 吞掉并打 error（`ModHook.cs:109-209`），不要依赖异常传播。
16. **manifest/扫描器**：`manifest.xml` 必须含 `<Assemblies><Assembly>X.dll</Assembly></Assemblies>` + `<Capabilities/>`（无能力也要写空元素，`ModLoader.cs:658-712`）；引用第三方 NuGet 会被 `ModScanner` 硬拒（见骨架 README）。
17. **注册目录的时机**：`OnModItemDirectoryInit` 只会在**第一次**目录 Awake 时 fire（后续场景副本被当重复目录拒绝），不要指望每次场景切换都收到事件。
18. **兜底目录子类的类型名**：必须新类型（如 `ModItemDirectoryFallback : ModItemDirectory`），否则 `DirectoryMaster.AddDirectory`（`DirectoryMaster.cs:21-43`）会以"重复目录类型"拒绝；挂到 `DontDestroyOnLoad` 的对象上。

---

## 11. 不确定项 / 已知未验证

| 项 | 状态 | 说明 |
|---|---|---|
| `ModItemDirectory` 实例存在并初始化 | **高置信（运行日志实证）** | `Player.log` 里的 duplicate 警告包含 `ModItemDirectory`（§3.3）；未见其 GameObject 名字/场景层级（构建后场景只存脚本引用，不存类名） |
| 原版机器精灵尺寸 | **中高置信（直接解码 resources.assets）** | Sprite `m_Rect` 由二进制手工解析：`purifier` 48×48、`purifier_w` 48×64；未解析"图集↔sprite"的 Texture2D 归属（按名字唯一性+同批机器全 48×48 推断） |
| 电池/电网单位 | 高置信 | `battery100` = `InitPowerSourceItem(item,100,10)`（`MiscItemDirectory.cs:145-150`）；机器每次动作扣 `machinery_drawn_power` kJ |
| `manufacture` tag 作用 | 未发现消费者 | 全库 grep 仅有写入；保留对齐原版 |
| `MachineHelper.GetSlot(machine,2)` 在 tooltip 中可用 | 高置信 | `GetBodyStorage` → `MachineryPanelRow.GetStorage` 按 attach 顺序；与原版布局一致 |
| 自定义目录子类在 AddComponent 时 Awake 被调 | 高置信（间接证据） | 原版全部目录都在 private 基类 Awake 里注册（§3.3） |
| 官方是否提供"公开获取 live ModItemDirectory"的 API | **否** | `ModHook.OnModItemDirectoryInit` 是唯一入口；`PaperBookRegistry` 也是缓存 + live 引用（`PaperBookRegistry.cs:585-650`） |
| 原版净水器是否可被 turbo booster 加速 | 可以（机制上） | 但原版没 EnableTag；mod 机器只要 `STANDARD_MACHINE_TAG` 就自动符合 `IsTurboBoostEligible`，所以可被加速（设计上算优势/差异，需产品确认） |

---

## 12. verifier 可自动化检查清单

1. 启动日志：`[ModLoader] audit: precision_lab … running=True trust=… caps=none`；无 `harmony` 补丁数也不是必须（本 mod 不 patch）。
2. `Player.log` 搜 `Trying to add a duplicate Directory 'ModItemDirectory'`（证明目录初始化路径正常）；搜 `[PrecisionLab] items registered`（证明事件注册成功）。
3. 新游戏/读档后，调试面板（F9 → Spawn）或网络升级购买后：物品出现在主库存/前台；`Player.log` 搜 `granted item: precision_purifier`。
4. 机器：放到后柜台 → 双击开窗（power/module/container/note 四面板）→ 放电池+模块+液体容器 → 按 `EndNight`（睡觉）后：电池减少 `machinery_drawn_power`、容器污染物减少、tooltip 出现 "Purity cap"；turbo booster 拖到机器上应立即多跑一次周期。
5. 存档往返：放好机器/电池/模块/容器 → 保存 → 读档 → 窗口内容、外观（active 精灵）、模块加成、剂量 tag 全部恢复。
6. 取液器：双击物品切换剂量；拖到液体容器上精确抽取 N µL（tooltip "Contents" 的 µL 精确变化）；再拖回另一容器注入 N µL；不足 N µL 时应"整笔失败"而不是部分转移。
7. 网络升级：旧存档打开手机黑网 → 出现 "Precision Lab" 页签与两个商品 → 购买后钱扣、物品到货、按钮变为已购。
