# 精密实验室设备 / Precision Lab Equipment

《深空当铺：可能是偷的》/ *Probably Stolen* 0.46D 的 mod。新增 3 台可获取、可正常使用的实验设备，以及 1 个淀粉水解化学反应。

---

## 物品

### 1. 精确取液器 / Precision Liquid Extractor

**移液枪式设计：量程可调、双击物品打开量程设置面板。**

- 物品类型：手持工具，占 **1×3 格**（竖握的移液枪外观，使用作者提供的像素图），本身是一个容量 **10 000 µL（10 毫升）** 的液体容器。
- **量程设置（双击打开）**：双击物品弹出“精确取液器 · 量程设置”窗口：
  - **数值输入框**：直接输入精确量程（1 – 10 000 µL，任意整数，1 µL 步进），**改动即时生效，无需点应用**；
  - **滑条**：粗调量程并与输入框联动（同样即时生效）；
  - **预设按钮**：1 / 10 / 100 µL、1 mL / 5 mL / 10 mL；
  - **模式下拉框**：自动（空枪取液 / 有液注液）｜强制取液｜强制注液；
    —— 移液枪里已有液体、想继续取同种液体（例如 980 µL 补回 1 mL）时，把模式切到“强制取液”即可；
  - 窗口内显示当前内容物体积；关闭窗口不会丢失当前设置。量程与模式**保存在物品本身上**（随存档保存，每把取液器可不同）。
- 用法（把取液器拖到目标液体容器上）：
  - **取液器为空时** → 从目标容器中**精确抽取 N µL** 到取液器；
  - **取液器内有液体时** → 向目标容器**精确注入 N µL**。
  - **整取整放**：如果可转移量不足 N µL（源液体不够/目标容量不够），本次操作**整体拒绝、一滴不移**，
    工具提示会显示失败原因与可转移量，避免“取了半管”破坏配比。
- 转移严格按整数 µL 计算：**不会多取、不会少取、不会损耗**；液体成分按比例保留（纯度不变）。
- 与游戏水系统一致：会刷新容器的“水的品相/纯度/估价”，并与化学温度系统联动。
- 交互限制：只对**已拥有的液体容器**生效；**酒类容器拒绝**（酒有独立品质系统）；
  取液器拒绝其他容器向它“倒液”（阻断原版免费倾倒路径，保证计量语义）；
  它只从液体容器取液/注液，**不用于水龙头加水或倒进排水口**；也**不经过滤网**（保证计量精确，
  需要过滤请先用原版过滤器处理，再用取液器计量）。
- 适用场合：药剂/配方/酿酒/水培等需要精确配比的场合，或从大桶里精确分装样品。

### 2. 提纯机器 / Purification Machine

**去除液体中的物理 / 化学 / 生物污染物，但绝不破坏有价值的有效成分。**

- 物品类型：可放置在店内的机器（带电池槽、模块槽、液体容器槽、便签槽）。
- 运转方式：和原版机器一样在**夜间结算时运转一个周期**（或用“涡轮增压器”类道具追加一次周期）。
- 处理对象：**任意液体容器**（水瓶、量筒、化学容器、药剂……但酒类容器除外——酒有独立的发酵/品质系统）。
- 会去除的污染物：
  - **物理污染**：物理污染物、微塑料、碎屑、矿物
  - **化学污染**：化学污染物、重金属
  - **生物污染**：微生物、有机废物
- **不会**去除：水、酒精、咖啡因、蛋白质、糖/淀粉/脂肪、dream_cap_extract、nightmare_extract、nomore、synaprest、wine_extract 等有效成分。
- **两种会计模式自动切换**：
  - 如果容器里**只有水和污染物** → **净水模式**：被去除的污染物转化为清水，**总体积不变**（与原版净水器一致）；
  - 如果容器里**含有贵重液体** → **浓缩模式**：被去除的污染物**直接销毁**（总体积减少），有效成分绝不变成水。
- 每周期处理量与原版净水器同公式：`(min(体积, 10 升) × 3‰ + 25 000 µL) × 脏污加成 × 1.25`，再乘模块性能加成；液体越脏，单周期去除越多。
- **模块效果**：性能模块提高每周期处理量（清得更快）、效率模块降低耗电、**质量模块提高提纯上限**（纯度 +x%，x 由质量节点决定）。
  机器窗口右起第二个面板实时显示 **提纯上限 xx.xx%（+x.xx%）**；工具提示同样显示上限、质量加成与模块总加成。
- **使用说明纸条**：机器到手时右侧便签槽里已附带“提纯机器使用说明”，写明模块效果与运行方式。
- **提纯上限**：基础 99.00%，质量模块可将上限逐步提升到 100.00%（由品质模块按原版曲线决定）。
- 每周期耗电 6，需要电池槽中有电；放入容器后会切换为工作外观，工具提示会显示容器纯度、三类污染量与当前会计模式。

---

### 3. 淀粉水解（新增化学反应） / Starch hydrolysis

游戏原本无法处理淀粉。现在新增真实反应：

```
淀粉 --(酸催化, 40–60 °C)--> 糖
```

- 把含 **淀粉 + 酸** 的液体容器放在 **加热板** 上，把目标温度调到 40–60 °C 并保温；
- 每秒会把一部分淀粉转化为等量糖（酸是催化剂、不消耗，总体积不变）；
- 提示：加热板需要至少 2 个化学模块才能自由调节温度区间（0/1 个模块时固定 99/160 °C）。

### 4. 微生物培养机 / Microbial Cultivator

培养“吃糖”微生物的机器：机器窗口单行六格：电源｜模块｜液体容器｜**营养果**｜**菌群活性**｜便签。

- 把含糖液体容器放进液体槽，把 **营养果**（小 / 正常 / 大 / 巨型）放进营养果槽；
- 每夜机器会**压榨一个营养果成汁水**喂菌群（小 +15 / 正常 +25 / 大 +40 / 巨型 +60 活性），
  然后分解糖：约 20% 的糖变成有机废物（其余被微生物消耗掉），总体积下降；
- **低糖分 → 休眠**：容器里没有糖时菌群会进入休眠（活性掉到 0，面板显示“休眠 · 需要营养果”），
  不再分解；重新放入营养果并补充糖即可唤醒；
- **性能模块**：提高每夜糖分解量；**质量模块**：减少残留的有机废物（20% → 最低约 5%）；**效率模块**：降低耗电；
- 菌群活性面板实时显示 `xx% · y mL/夜`，休眠时显示提示；
- 耗电 8/周期，需要电池；机器右侧便签槽自带「微生物培养机说明」；
- 解锁后杰克逊博士处也会供货（普通来访 +1、特殊库存夜 +2）。

### 5. 肾上腺素注射笔生产线 / Adrenaline injection pen（“更多药品”解锁）

黑网新增升级 **更多药品：肾上腺素**（1600 信用点）。解锁后：
- 店内库存获得一张**配方纸条**（可翻页阅读，六步流程）；
- **退休化学家**开始出售「**肾上腺素原料盒**」（激活拆开得到 7 瓶试剂：邻苯二酚 3000 / N-羟甲基碱溶液 3000 / 脂合剂 3000 / 还原剂 3000 / 拆分剂 3000 / 碱液 1500 / 酸液 1500 µL）；
- 化学制剂成型机（Chem Finisher）新增两个配方：**盐酸肾上腺素原料药**、**肾上腺素注射笔**。

六步流程（中间体全部由反应自动生成，反应每秒按容器温度结算）：
1. **胺化**（26–60 °C）：邻苯二酚 + N-羟甲基碱溶液 + 脂合剂 → 胺化中间体
2. **还原**（60–80 °C）：胺化中间体 + 还原剂 → 消旋肾上腺素
3. **拆分**（40–60 °C，酸催化）：消旋肾上腺素 + 拆分剂 + 酸 → 左旋肾上腺素盐 + **右旋废料**
4. **成盐**（26–60 °C）：左旋盐 + 碱 → 游离碱；游离碱 + 酸 → 盐酸肾上腺素；
   干燥后放入化学制剂成型机 → **盐酸肾上腺素原料药**（品质按有效成分比例分级）
5. **配液**：原料药溶于等量水 → 肾上腺素药液（多余水用蒸发器除去）
6. **灌装**：干燥药液放入化学制剂成型机 → **肾上腺素注射笔**（每支需要 5000 µL＝5 毫升药液；一盒原料可做 2 支）

其他：
- 提纯机器新增**废料输出位**：液体里的右旋废料会在周期结束时被收集成「右旋废料」物品，清空输出位可继续收集；
- **右旋废料回收**：把右旋废料溶回容器，加入还原剂并加热到 **60–80 °C**，会生成消旋肾上腺素，
  可以回到第 3 步再次拆分——提高总收率（配方纸条里也写了这条回收路线）；
- 注射笔是**医疗物 + 奢侈品**（ITEM_MEDICAL_TAG / MEDICAL / LUXURY_ITEM 类型，单支价值 1200+，会吸引上层奢侈客户与展示台系统），战斗中使用：3 回合内命中 +20%、暴击率 +33%（蓝色自动注射笔外观）；
- 注射笔/原料药也支持溶解回收（可重新溶入容器继续加工）。

## 获取方式
1. **黑市/暗网（游戏内手机 → Wild Network）新增“精密实验室”页签**：
   - 精确取液器：1200 信用点
   - 微生物培养机：1800 信用点
   - 提纯机器：2400 信用点
   - 更多药品：肾上腺素（解锁肾上腺素注射笔生产线 + 配方纸条）：1600 信用点
   - 购买后物品直接进入**店内库存**。老存档也会自动出现这些商品。
2. **杰克逊博士（Dr. Jackson）**：解锁对应机器后，他每次来访柜台上会多出 1 台（“特殊库存”夜晚为 2 台），可重复购买更多。
3. **退休化学家**（来店里的供应商客人）：解锁取液器后，如果你当前**不拥有**取液器（丢了），他柜台会卖一把替补充；最多同时拥有一把。解锁“更多药品”后他开始出售肾上腺素原料盒。
4. **战利品**：低概率出现在医疗 / 废品战利品池中，可以随进货或拾取获得。
5. **调试生成**（仅测试用）：游戏根目录放一个空的 `dev.txt` 后启动，F9 调试面板 → “Spawn” 分类里有多个生成按钮（含原料盒/原料药/注射笔/右旋废料）。

## 安装 / 卸载

- 安装：把整个 `PrecisionLab` 文件夹复制到
  `<游戏安装目录>\Mods\`（例如 `F:\SteamLibrary\steamapps\common\Probably Stolen\Mods\PrecisionLab`），
  然后启动游戏，在 **Mods 菜单**中启用并信任本 mod（游戏会显示一次确认）。
- 卸载：从 `Mods` 文件夹删除 `PrecisionLab` 文件夹即可。已存在于存档中的本 mod 物品会变成
  普通无效物品（建议卸载前先卖掉/丢弃）。
- 兼容性：不修改任何游戏文件，全部通过游戏自带的 mod 框架（IMod / ModHook / ModItemDirectory）实现；
  与使用同一框架的其他 mod 兼容。

## 已知说明

- 机器与原版机器一样在夜间结算时工作，白天可用涡轮增压器追加速度。
- 精确取液器不会经过滤网（保证计量精确），如需过滤请使用原版过滤器后再计量。
- 提纯机器只去除污染物，不会去除糖/淀粉/酒精等有效成分；糖请用微生物培养机处理，淀粉请先酸水解成糖。
- 本 mod 的数值是为单机娱乐平衡的：取液器价格低、提纯机器价格约为原版净水器的 10 倍但功能更强。

---

## English quick reference

**Precision Liquid Extractor** — handheld metering tool (10 mL tank, 1 µL resolution). Double-click to open the metering window: exact number input, slider and presets; changes apply immediately. Direction modes: auto (empty draws / filled injects), force draw, force inject. Refuses when less than the requested volume is available. The first one comes with the Wild Network upgrade; the retired chemist sells a single replacement after you lose yours.

**Purification Machine** — placeable machine with battery/module/container/note slots. Runs one cycle per night (or via turbo booster), removes physical (physical contaminant, microplastic, debris, mineral), chemical (chemical contaminant, heavy metals) and biological (microbe, organic waste) contamination from any liquid container (wine containers excluded), never touches valuable ingredients. Water-only vessels are cleaned into water (volume unchanged); vessels with valuable liquids are concentrated (volume drops). Purity cap 99.00% base, higher with quality modules; live purity panel + instruction note in the machine window. Uses 6 power per cycle.

**Microbial Cultivator** — placeable machine that runs sugar-eating microbes. Six panels in one row: power | modules | liquid container | **nutrifruit** | culture | note. Put a sugary container in the liquid slot and nutrifruit (small/normal/large/huge) in the nutrient slot: each night the machine presses one fruit into juice (+15/+25/+40/+60 activity) and digests sugar into a little organic waste (about 20%, down to ~5% with quality modules). When the liquid runs out of sugar the culture goes **dormant** (activity 0, panel shows "Dormant · needs nutrifruit") and stops until fed nutrifruit and given more sugar. Performance modules digest more per night, efficiency modules cut power draw. Uses 8 power per cycle.

**Starch hydrolysis** — new chemistry: starch in an acidic liquid hydrolyses into sugar at 40–60 °C. Heat the container on a heating plate (needs 2+ chem modules to reach the low temperature range). Acid is a catalyst and is not consumed.

**Obtaining** — Wild Network (in-game phone) → "Precision Lab" tab: extractor 1200, cultivator 1800, purifier 2400 credits, delivered to your store inventory. Dr. Jackson stocks extra machines after the corresponding unlock (1 per regular visit, 2 during special stock nights). The retired chemist sells a replacement extractor only when you own none. Low chance in medical/junk loot. Mod id: `precision_lab`.
