# 设计文档 — 精密实验室设备 mod（PrecisionLab）

游戏：深空当铺：可能是偷的 / Probably Stolen 0.46D（Unity 2021.3.45f2，Mono）
Mod ID：`precision_lab`　名称：精密实验室设备 / Precision Lab Equipment
框架：游戏自带 mod 框架（IMod / ModHook / ModItemDirectory），不修改任何游戏文件。

本 mod 新增两个物品（均为可获取、可正常游玩的完整物品）。

## 1. 精确取液器 / Precision Liquid Extractor

- 物品 ID：`precision_liquid_extractor`
- 形态：手持式取液工具（本质是 `LIQUID_CONTAINER_TAG` 液体容器，容量 **10 000 µL = 10 mL**）；占 **1×3 格**，外观为用户提供的像素移液枪图（`yiyeqiang.png`，经 `tools/assets/import_extractor_sprite.ps1` 缩放/调色板量化/居中为 16×48 的 1×3 精灵）
- 核心能力：**以 1 µL（0.001 毫升）为最小计量单位定量转移液体**
  - 空容器时对它使用（拖拽到目标液体容器上）：从目标容器精确**抽取 N µL** 到取液器；
  - 取液器内有液体时对它使用：向目标容器精确**注入 N µL**。
  - **整取整放（exact-or-nothing）**：可转移量不足 N µL（源液体不足/目标容量不足）时，
    本次操作整体拒绝、不做任何修改，并在工具提示显示失败原因与可转移量，
    避免“取了半管”破坏配方；转移严格为整数 µL，无损耗、无浮点误差。
  - 转移按比例保留液体成分（水/有效成分/污染物比例不变），
    与游戏水系统一致（`HandleReceiveLiquidTemp` 温度混合 + `OnLiquidContainerPourInto/OutOf`
    刷新水的品相/纯度/估价/贴图）；有意不经过滤网（保证计量精确）。
- N（量程）的调节 —— **移液枪式双击输入窗口**：
  - Harmony Prefix 补丁在 `ItemMouseDoubleClickHandler.DoubleClickAction` 上拦截“双击精确取液器”，
    打开基于游戏自带 **CustomUI 框架** 的“精确取液器 · 量程设置”窗口
    （`CustomUIManager.LAYER_META`，店内可用；与 ModConfig 窗口同一套 UI 框架）；
  - 窗口内容：**精确数值输入框**（1–10000 µL，任意整数，1 µL 步进）+ 粗调滑条（与输入框联动）+
    预设按钮 1/10/100 µL、1 mL/5 mL/10 mL + **方向模式下拉框**（自动/强制取液/强制注液）+
    内容物体积显示 + 关闭按钮；**所有改动即时生效**（输入框失焦/回车、滑条拖动、预设点击、模式选择
    立刻写入物品 tag），没有“应用”按钮，关闭窗口不会丢失设置；
  - 量程写入物品 tag `PRECISION_EXTRACTOR_DOSE_UL`、方向模式写入 `PRECISION_EXTRACTOR_MODE`
    （均随存档保存，每把取液器独立）；
  - **方向模式**解决“枪里有液体时无法继续取液”的问题：自动 = 空枪取液/有液注液；
    强制取液 = 总是从目标容器取液（可把 980 µL 的枪补回 1 mL）；强制注液 = 总是注入；
  - 为避免“双击打开窗口时顺带触发激活逻辑而改变量程”，物品 may/canActivate = false；
  - 物品同时保留一个原生机器风格 `PixelWindow`（滑条/微调/预设）作为**备用**：
    若 CustomUI 不可用（异常场景）则回退由游戏原生双击打开该窗口；
  - mod 设置中的“取液器默认量程”（1–10000 任意整数 µL）仍作为**新建取液器**的初始值；
  - 工具提示显示当前量程、**当前模式**、内容物、上次操作结果。
- 交互限制：目标必须是已拥有的液体容器；**酒类容器（WINE_CONTAINER_TAG）拒绝**（酒有独立品质系统）；
  取液器拒绝其他容器向它“倒液”（阻断原版免费倾倒路径，保证计量语义）。

## 2. 提纯机器 / Purification Machine

- 物品 ID：`precision_purifier`
- 形态：店内机器（`MACHINE` / `STANDARD_MACHINE_TAG` / `manufacture` 标签；电池槽 + 模块槽 + 液体容器槽 + 便签槽；
  与原版机器一致，在**夜间结算**时运转一个周期，可用涡轮增压器追加周期）
- 核心能力：**只去除物理/化学/生物三类污染物，绝不破坏有价值的液体成分**
  - 物理污染：`physical_contaminant`、`microplastic`、`debris`、`mineral`
  - 化学污染：`chemical_contaminant`（工具提示单独列出）
  - **重金属**：`heavy_metal`（随化学类一起去除，工具提示中作为独立一类显示毫升数）
  - 生物污染：`microbe`、`organic_waste`、`plant_matter`
  - 其他所有液体（水、酒精、咖啡因、dream_cap_extract、nomore、synaprest、糖/淀粉/脂肪/蛋白……）一律保留。
- **两种会计模式自动切换**（工具提示显示当前模式与三类污染量）：
  - 容器只有水+污染物 → **净水模式**：去除的污染物转化为清水，**总体积不变**（原版净水器语义）；
  - 容器含贵重液体 → **浓缩模式**：去除的污染物**直接销毁**（总体积减少），
    有效成分绝不变成水（用于化学/药剂链提纯，提升有效成分比例）。
- 去除量：与原版净水器同公式
  `budget = (min(体积,10 000 000 µL)×3‰ + 25 000 µL) × 脏污加成 × 1.25`，
  脏污加成 = `MachinePurifier.GetDirtBonus(当前纯度) + 性能模块加成/100`（越脏越快）。
- 提纯上限：`MachinePurifier.GetMaxPurityCap(质量模块加成)` —— 基础 **99.00%**，
  质量模块可逐步提升至 **100.00%**；每周期先按“达到上限所需去除量”计算，若因贵重液体导致上限不可达，
  则**去除全部所选污染物**（受预算限制），不会留下永久 1% 残留。
- 每周期耗电 6（`InitMachinery(machine, 6)`），需电池槽电量；模块槽为万能模块
  （**性能=每周期处理量，质量=提纯上限，效率=耗电**）；放入容器切换工作外观。
- 机器窗口共 5 个面板，**单行排列**（与蒸发器等原版机器一致）：电源、模块、液体容器、**提纯上限**
  （MachineryText 实时显示“xx.xx%（+x.xx%）”，+x 为质量模块相对 99.00% 基准的提升，模块增减时刷新）、**便签**（最右侧）；
- 便签槽里附带“提纯机器说明”纸条（`DirectoryMaster.Item("postit")` + MACHINE_MANUAL_TAG，`DOCUMENT` 类型），
  内容仿照原版蒸发器说明文档的版式：`[模组影响]: 效率影响功耗 / 性能影响每晚处理量 / 质量影响提纯上限（纯度 +x%）` +
  运行方式与去除范围；
- **便签插入时机**：不能在物品工厂内插入——`DirectoryMaster.Item` 会在创建完成后销毁新物品上的所有子物品；
  正确做法是 Harmony postfix 到 `DirectoryMaster.Item(string, bool)`，在创建返回后再把纸条放进便签槽
  （`PurifierManualHelper.TryEnsure`，存储索引：电源 0 / 模块 1 / 容器 2 / 便签 3；提纯上限面板只是显示元素、不占存储索引），
  并在 `OnGameLoadedNormal` / `OnWakingUpLate` / 黑网发放物品后做补偿扫描。
- 获取扩展：黑网解锁后仍赠送 1 台；**杰克逊博士**（Dr. Jackson）的柜台会开始供货——
  普通来访 +1 台、特殊库存夜晚 +2 台（Harmony postfix 到 `StoreClientList.PlaceInventorInventory` /
  `PlaceInventorSpecialStock`，仅当本 mod 的黑网升级已解锁时生效），可重复购买。
- 处理完成后调用 `OnLiquidContainerPourOutOf` 刷新估价/品相/贴图并写入“最近扫描”结果，
  与原版液体变化的副作用一致。
- 拒绝酒类容器（`WINE_CONTAINER_TAG`，酒有独立发酵/品质模拟）。

## 获取方式（游戏内）

1. **黑网（Wild Network，游戏内手机打开）新增页签“精密实验室”**：
   - `PRECISION_LAB_EXTRACTOR`：1200 信用点 → 直接放进玩家店内库存；
   - `PRECISION_LAB_PURIFIER`：2400 信用点 → 直接放进玩家店内库存。
   - 新开档（PlayerStore 初始化）与读档（`NetworkUpgrade.AddMissingAfterLoad`）都会自动补齐这两个商品。
2. **战利品池**：`medicalTable` 低概率出取液器、`junkTable` 低概率出提纯机器（随进货/拾荒获得）。
3. **开发者调试**：游戏根目录放空 `dev.txt` 后 F9 调试面板 → “Spawn” 分类有两个生成按钮。

## 技术要点

- 目标框架 `netstandard2.1`，仅引用游戏自带 `Assembly-CSharp.dll` / `UnityEngine.CoreModule.dll` /
  `UnityEngine.AudioModule.dll`，无 NuGet 运行时依赖；
- **能力声明 `<Capabilities/>` 为空**：mod-framework 用游戏真实 `ModScanner.Scan` 离线验证
  （methods=98，capabilities=none，hard flags=0，Refused=False）；
- 物品注册：`ModHook.OnModItemDirectoryInit` → `ModItemDirectory.Add(id, factory)`；
  若该事件已错过，读档时会附加一个 `ModItemDirectoryFallback`（`ModItemDirectory` 子类）兜底注册；
- 自定义像素图：`Textures/Items/*.png` → `GameItem.SetSpriteAndShapeFromMod`（取液器 16×48=1×3 格；提纯机器 48×48=3×3、48×64=3×4 格），
  贴图缺失时回退到原版图集贴图；
- 本地化：`Localization/en.csv` + `zh.csv`（UTF-8，42 键/语言），
  代码内英文兜底，`ModHelper.GetLocalized` 读取；
- 自检：读档后一次性在 Player.log 打印 `[PrecisionLab] SELFTEST ...`：
  物品创建、精确转移 1234 µL、超量请求拒绝、净水模式、浓缩模式数值与断言结果（OK/FAILED）。

## 关键运行时坑（已修复，供后续维护参考）

1. **绝不能在 `IMod.Init` 里注册网络升级**：`ModHelper.AddNetworkUpgrade` → `NetworkUpgrade.RegisterModUpgrade`
   会读取 `PlayerStore.Instance`，而该属性的 getter 在实例不存在时会**惰性 new 一个 PlayerStore**；
   在 `ModLoader.Awake`（任何游戏场景加载前）构造 PlayerStore 会抛 NullReferenceException。
   现在改为在 `ModHook.OnGameLoadedInit`（读档，`NetworkUpgrade.AddMissingAfterLoad` 之前）/
   `OnNewGameStarted` / `OnGameLoadedNormal` 中延迟注册，并用 `PlayerStore.HasInstance` 兜底。
2. `PlayerStore.Instance` 的其他隐式访问同样要避免在启动期发生（例如不要在 Init 里生成物品/查询玩家状态）。
3. `WaterHelper.AddLiquid` 的容量检查是按单种液体做的（可能总量超容量），精确分液必须自己做容量预检并直接写 tag；
   本 mod 的 `LiquidTools.MoveExact` 采用“先校验、后写入、整取整放”，批次写入后显式 `ValidateShapeState()`。
4. 液体变更后的刷新必须走 `WaterFeatureHelper.OnLiquidContainerPourInto/OutOf`（估价、品相、贴图、空瓶状态），
   不要手写 `UpdateWaterFeature(false)`（会清掉“已检测”状态且不更新 unitValue）。
5. `ItemMouseDragHandler.current` 在 UI 就绪前为 null，`WaterFeatureHelper.InitWaterFeature` 直接解引用它会 NRE；
   mod 物品工厂里已加 try/catch 兜底。
6. 原版机器精灵是 48×48（3×3 格），带容器外观 48×64（3×4 格）；mod 精灵按 16 像素网格切片，
   尺寸必须是 16 的整数倍，否则 shape 计算为 0 格。

## 交付物

- `dist/PrecisionLab/`：可直接复制进 `<游戏目录>\Mods\` 的成品 mod
  （manifest.xml / PrecisionLab.dll / Localization / Textures / LootTables / README）
- `src/PrecisionLab/`：完整可编译源码（`dotnet build -c Release`）
- `tools/install-mod.ps1`：安装/卸载/可选写入信任记录
- `tools/verify/`：启动、日志校验、信任记录写入脚本（verifier）
- `docs/`：设计 + 各系统 API 分析（01/02/03）+ 验证记录（04）


## 验证结果（verifier，2026-10-30）

- **最终冻结构建（移液枪输入框版，含 UI 修复）**：PrecisionLab.dll 50,688 字节，
  sha256 = `77f2720b4308928ec60f3e0ea7f6bea1279646c7515f5e3f08d2d6996f31c847`，
  combinedHash = `35687f61f5bfed9faa9380cc7861d329eda875899baca56043398ab3db6b0a0f`
  （manifestHash = `e1a4fd5b98935456b923df6f191b830eb5118958bc2289e5467a9e2bb83b7f3a`）。
- **双击量程设置窗口（CustomUI 输入框版）实测 PASS**：游戏内双击取液器 → 屏幕可见的
  `精确取液器 · 量程设置` 窗口（overlay 层，700×440，含数值输入框/滑条/6 个预设按钮/内容物/关闭/应用；
  原生备用窗口不弹出），截图 `tools/verify/evidence/ingame-12-customui-meta-window.png`（UI 修复后：滑条可见、预设按钮会同时更新输入框与滑条、输入框回车提交会同步滑条、应用写入 tag）；
  输入框写入 3333 → 应用 → 物品 tag `PRECISION_EXTRACTOR_DOSE_UL` == 3333；再写 250 → 250；应用后窗口关闭。
  Harmony 补丁生效：ModLoader 日志 `Harmony: 1 method(s) patched`。
- 注意：游戏原生机器风格窗口组件没有文本输入控件；第一版所以用了滑条+预设，后按用户反馈改用
  CustomUI 框架的输入框窗口（双击打开），滑条/预设保留为辅助。
- **游戏 ModScanner（真实游戏代码离线运行）**：methods=114，capabilities=none，hard flags=0，Refused=False。
- **游戏内运行（Player.log）**：`[ModLoader] Loading: PrecisionLab.dll` → `Initialized: Precision Lab Equipment / 精密实验室设备 v1.0.0`，
  audit `running=True trust=Match caps=none`；无任何异常堆栈；
  `items registered in mod item directory: extractor=True purifier=True`；
  读档后 `network upgrades registered (tab precision_lab)`，Wild Network 运行期字典中
  两个商品（PRECISION_LAB_EXTRACTOR 1200 / PRECISION_LAB_PURIFIER 2400）均存在，页签标签“精密实验室设备”。
- **mod 自检（游戏内）**：`[PrecisionLab] SELFTEST OK exact=True refuseOverdose=True cleanWater=True concentrate=True`
  （精确转移 1234 µL；超量请求 0 转移；净水模式总体积不变、纯度 8433→9900 bp；浓缩模式体积 7500→7000、有效成分不变、污染物 500→0 µL）。
- **获取路径实测**：F9 调试面板 Spawn 页显示 mod 注册的两个生成按钮并成功生成物品；物品可在游戏中生成。
- **用户存档保护**：验证过程从未加载用户主存档 slot 43（只用复制出的临时存档），
  save_43.es3 与测试前快照逐字节一致（sha256 D55FD86A…），saves_index 与历史存档均未变。
- **移液枪贴图替换 + 1×3 占地（asset-only 变更）实测**：用户提供的 `yiyeqiang.png`（327×1024）经
  `tools/assets/import_extractor_sprite.ps1` 去透明边距 → 等比缩放到 12×48 → 24 色调色板量化 →
  居中到 16×48 画布，生成 `Textures/Items/precision_extractor.png`（492 B，sha256 `ae3844f3…`）；
  游戏内实测 `shape=1x3 global=1x3 modified=1x3`、`Item sprite loaded: precision_extractor`、
  0 条 `SPRITE NOT ON GRID` 警告；DLL/manifest 未变 → 信任记录不变（trust=Match）；
  自检、量程窗口、拖动交互全部保持通过。8× 预览：`tools/assets/preview_extractor_x8.png`。
  如需改为 1×2：运行 `import_extractor_sprite.ps1 -CellsTall 2` 重新生成 16×32 贴图即可。
- **最终游戏状态**：`<游戏目录>\Mods` 仅保留 `PrecisionLab`；dev.txt 已删除；
  SaveFile.es3 可写，modOrder=["item_details","precision_lab"]，precision_lab 信任记录 hash 匹配、developerMode=false
  → 用户直接启动游戏即可生效，无需任何确认。
- **方向模式 + 即时生效（用户实测反馈修复）**：用户反馈“枪里 1 mL、取 20 µL 剩 980 µL 后无法再取同种液体补回 1 mL”
  与“没必要点应用”。修复后实测：强制取液模式下 1000→1020 µL（目标 500→480）、强制注液 1020→1000、
  自动模式非空枪仍注入；窗口无“应用”按钮，点预设 1 mL 立即 tag=1000、拖滑条立即 2500、输入框回车提交 333、
  关闭后保持 333；模式下拉 SetIndex 立即写入 PRECISION_EXTRACTOR_MODE=1/2/0；
  SELFTEST 新增 direction modes: autoEmptyDraws=True forcedDraw=True forcedInject=True 且 modes=True；
  最终构建 52,224 B / sha256 `07f53909…` / combinedHash `1c77048d…`。
  截图：`tools/verify/evidence/ingame-18-modes-window.png`。- **提纯机器增强（用户实测反馈）**：单行 5 面板窗口（电源｜模块｜液体容器｜提纯上限｜便签，版式同蒸发器）；
  便签槽附带“提纯机器说明”文档（DOCUMENT，仿蒸发器版式：`[模组影响]: 效率影响功耗 / 性能影响每晚处理量 /
  质量影响提纯上限（纯度 +x%，最高 100%）` + 运行方式）；`DirectoryMaster.Item(string,bool)` Harmony postfix 在创建后
  插入便签（存储索引 3；不能在工厂内插入，否则被 DirectoryMaster 清子物品）；提纯上限面板实时显示 `99.00% (+0.00%)`
  → 2 个质量模块 `99.15% (+0.15%)`；工具提示 4 类污染（物理/化学/重金属/生物）与模块加成；重金属于化学类中显式单列；
  解锁黑网升级后杰克逊博士柜台 +1 台/特殊库存夜 +2 台（锁定时不供货，购买路径机器自带说明）。
  最终构建 58,368 B / sha256 `1785da97…` / combinedHash `7982709f…`；日志 `tools/verify/logs/final23.log` +
  `final-clean-boot12.log`；截图 `tools/verify/evidence/ingame-24-purifier-cap9915.png`；
  SELFTEST `OK ... manual=True ...`，`Harmony: 4 method(s) patched`。- 证据与详细报告：`docs/04-verification.md`、`tools/verify/logs/final-clean-boot7.log`、`tools/verify/evidence/`。

## 新增内容（2026-10-31，用户需求第二批）

### 淀粉水解反应 / Starch hydrolysis

- Harmony postfix 挂到 `ChemicalHelper.HandleTemperatureReaction(GameItem)`（容器每秒 tick：
  `containerTempTick → HandleAmbientTempDrop + HandleTemperatureReaction`）。
- 条件：`CONTAINER_TEMP_TAG` 在 **40–60 °C**、容器内 **starch > 0** 且 **acid > 0**；
- 每 tick 转化 `max(5, 2% × 剩余淀粉)` µL，单 tick 上限 5000 µL，1:1 转成 sugar；
- **酸不消耗**（催化剂），总体积不变；离开温度窗口立即停止；换算后刷新 WaterFeature（纯度/贴图/估价）；
- 工具提示：含“淀粉+酸”的液体容器会显示提示（加热板保温 40–60 °C）；
- 温度来源：原版 `MachineHeatingPlate`（升级 2 个化学模块后可调 26–? 目标温度；0/1 个模块固定 99/160 °C）。

### 微生物培养机 / Microbial Cultivator

- 新物品 ID `precision_microbe_cultivator`（贴图 `precision_cultivator` 48×64 3×4，工作态 48×64 3×4）；
- 黑网升级 `PRECISION_LAB_CULTIVATOR`（1800 信用点，precision_lab 页签）→ 赠送 1 台；
  解锁后杰克逊博士柜台 +1 台/普通来访、+2 台/特殊库存夜（Harmony postfix 到原版两个库存方法）；
  废品战利品池低概率（weight 8）；
- 机器窗口单行 6 面板：电源｜模块｜液体容器｜**营养果**｜**菌群活性**｜便签；
  存储索引 power=0 / module=1 / container=2 / nutrient=3 / note=4（显示型文本面板不占存储索引）；
- **营养果槽**只接受 `nutrifruit`（原版营养果：小/正常/大/巨型，按 unitValue 区分 8/12/18/32）；
  每夜压榨一个果子成汁水喂菌群：活性 +15/+25/+40/+60（`ItemFactory.PressNutrientFruit`，果子被销毁）；
- **低糖分 → 休眠**：容器糖分为 0 时菌群活性每夜 −25，降到 0 进入休眠（面板显示“休眠 · 需要营养果”），
  休眠时 `DigestSugar` 直接返回 0；重新喂营养果可恢复活性，补糖后继续分解；
- 夜间周期（`onCycleEndSlotItemFunc`，耗电 8）：容器糖分被消耗，其中约 20% 生成 organic_waste
  （质量模块把副产物比例从 20% 降到最低 5%），其余体积被微生物消耗掉（总体积下降）；
  性能模块提高每夜处理量（基础 2000 µL/夜 ×（1+性能%））；有糖但未处理/无果子时活性 −10/夜；
- 菌群活性 tag `PRECISION_CULTIVATOR_ACTIVITY_INT`：新机器 100；分解成功 +5（上限 100）；
  活性按 25%–100% 缩放处理量（活性面板实时显示 `xx% · y mL/night`）；
- 便签槽自动附带“微生物培养机说明”（DOCUMENT，仿蒸发器版式），由
  `PurifierManualHelper.TryEnsure`（`DirectoryMaster.Item` postfix，创建后再插入）统一处理两种机器；
- 工具提示：菌群活性/每夜处理量、糖→有机废物规则、昨夜处理量、容器内糖量；
- 每次周期后调用 `MachineHelper.OnMachineActioned`（模块磨损/经验），并刷新液体 UI。

### 退休化学家处补购移液枪

- 原版 `StoreClientListSpec.CreateRetiredChemist` 的库存是在 `mainDialogue.endAction` 里硬编码添加的；
  Harmony postfix 包装 `endAction`：先执行原版库存，再调用 `RetiredChemistStock.TryAddExtractor`；
- 条件：`PRECISION_LAB_EXTRACTOR` 已解锁 **且** `PlayerStore.IsPlayerOwnThisItem` 为 false（当前不拥有取液器）；
- 加物品到柜台（`AddDirectSellingItemToTable`），因此最多同时拥有一把；黑网升级仍赠送第一把。

### 其他

- 本地化：en/zh 各 76 键（新增培养机、水解提示、升级卡等）；
- 自检扩展：starch hydrolysis（50 °C 转化 / 30 °C 不反应 / 酸不变 / 总体积不变）、
  糖消化（2000 µL 处理、400 µL 有机废物、总体积 −1600）、培养机工厂 + 说明纸条 + 活性 100；
- 静态检查：ModScanner methods=204、capabilities=none；本地化键集一致；战利品表 junkTable 新增培养机；
- 构建：`PrecisionLab.dll` 75,264 B / sha256 `4d06f67b…` / combinedHash `94f41094…`（待游戏内最终验证）。
