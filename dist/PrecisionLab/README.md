# 精密实验室设备 / Precision Lab Equipment

《深空当铺：可能是偷的》/ *Probably Stolen* 0.46D 的 mod。新增两件可获取、可正常使用的实验设备。

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

## 获取方式

1. **黑市/暗网（游戏内手机 → Wild Network）新增“精密实验室”页签**：
   - 精确取液器：1200 信用点
   - 提纯机器：2400 信用点
   - 购买后物品直接进入**店内库存**。老存档也会自动出现这两件商品。
2. **杰克逊博士（Dr. Jackson）**：解锁黑网的提纯机器后，他每次来访的柜台上会多出 1 台提纯机器（“特殊库存”夜晚为 2 台），可重复购买更多。
3. **战利品**：低概率出现在医疗 / 废品战利品池中，可以随进货或拾取获得。
3. **调试生成**（仅测试用）：游戏根目录放一个空的 `dev.txt` 后启动，F9 调试面板 → “Spawn” 分类里有两个生成按钮。

## 安装 / 卸载

- 安装：把整个 `PrecisionLab` 文件夹复制到
  `<游戏安装目录>\Mods\`（例如 `<游戏安装目录>\Mods\PrecisionLab`），
  然后启动游戏，在 **Mods 菜单**中启用并信任本 mod（游戏会显示一次确认）。
- 卸载：从 `Mods` 文件夹删除 `PrecisionLab` 文件夹即可。已存在于存档中的本 mod 物品会变成
  普通无效物品（建议卸载前先卖掉/丢弃）。
- 兼容性：不修改任何游戏文件，全部通过游戏自带的 mod 框架（IMod / ModHook / ModItemDirectory）实现；
  与使用同一框架的其他 mod 兼容。

## 已知说明

- 机器与原版机器一样在夜间结算时工作，白天可用涡轮增压器追加速度。
- 精确取液器不会经过滤网（保证计量精确），如需过滤请使用原版过滤器后再计量。
- 本 mod 的数值是为单机娱乐平衡的：取液器价格低、提纯机器价格约为原版净水器的 10 倍但功能更强。

---

## English quick reference

**Precision Liquid Extractor** — handheld metering tool (10 mL tank). Drag onto a liquid container: empty → draws exactly N µL; filled → injects exactly N µL. N is shown in the tooltip; activate the item to cycle presets (1–10000 µL) or set the default in the mod options. 1 µL resolution, no loss, composition preserved.

**Purification Machine** — placeable machine with battery/module/container/note slots. Runs one cycle per night (or via turbo booster), removes physical (physical contaminant, microplastic, debris, mineral), chemical (chemical contaminant, heavy metal) and biological (microbe, organic waste) contamination from **any** liquid container (wine containers excluded), and never touches valuable ingredients. Accounting is automatic: water-only vessels are cleaned into water (volume unchanged), vessels with valuable liquids are concentrated by discarding the removed contamination (volume drops). Purity cap 99.00% base, up to 100.00% with quality modules. Uses 6 power per cycle.

**Obtaining** — Wild Network (in-game phone) → "Precision Lab" tab: extractor 1200 credits, purifier 2400 credits, delivered to your store inventory. Also low chance in medical/junk loot. Mod id: `precision_lab`.
