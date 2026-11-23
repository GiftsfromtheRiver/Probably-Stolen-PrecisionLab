# 鉴定界面视觉资产 / Inspection UI asset map

## 1. 原版调用链

```
放大镜 ToolDirectory.CreateMagnifier()
  → item.onThisTargetItemFunc(target)
  → InspectionUIManager.OnInspectItem(target)
      → EmporiumEntry.frontInvinvElement.ClearHighlight()
      → ResetUI()                      // 关闭所有小/大鉴定子面板
      → OpenUI()                       // 打开总 panel、摆放 magnifyingGlass / blockerTray
      → 按 tag 分流：
          CIGARETTE_TAG   → HandleCigarette → CigaretteUI.OnInit
          PLASMA_CELL_TAG → HandlePlasma    → FuelUI.OnInit
          FOOD_STAMP_TAG  → HandleStamp     → StampUI.OnInit
          INS_INJECTOR_TAG→ HandleInjector  → InjectorUI.OnInit
          RIBWICH_TAG     → HandleRibwich   → RibwichUI.OnInit
          HAND_TAG        → HandleAppHand   → AppHandUI.OnInit
          BERRY_TAG       → HandleInsBerry  → InsBerryUI.OnInit
          APPRAISAL_ITEM  → appraisalPanel.SetActive(true)
      → 未拥有物品 + 有 confront 条件 → 打开 confrontPanel
```

关键点：**总入口和 panel 是共用的；各物品的鉴定画面是各自独立的 MonoBehaviour 预制体**（CigaretteUI / InjectorUI / StampUI…），没有共同父类，也没有可复用的通用子面板。

### 代码里的“动作”
- `OpenUI()`：`panel.SetActive(true)`，`magnifyingGlass.localPosition=(-20,0,0)`，`blockerTray.localPosition=(0,0,0)`，各子 UI 复位。
- `CloseUI()`：`panel.SetActive(false)`，播放 `EmporiumAudioPlayer.Instance.audioButton1`。
- 子 UI：`OnInit` 读 tag，`DisableAll` 后按索引启用 sprite；没有 Animator/AnimationClip 驱动，纯静态切换。
- 世界放大镜模式（不是物品鉴定）：`StoreUIManager.EnterMagnifyingMode()` 会 `StartBlackscreenSequence`、切到 shootingCamera、隐藏 inventory/world UI、显示 `magnifyingScreen`/`zoomUI`，播放 `click_vintage`；退出时反切。

## 2. 背景 / 放大镜资产（可直接替换）

| 资产 | 尺寸 | 用途 |
|---|---|---|
| `magnifying_screen` | 960×540 | 全屏放大镜背景 |
| `magnifying_mask` | 242×228 | 放大镜遮罩 |
| `magnifying glass` | 242×228 | 手持放大镜前景 |
| `MagnifyingGlass` / `MagnifyingGlass.png` | 894×894 / 1024×1024 atlas | 放大镜大图 |
| `magnify` / `magnify_hl` / `load_magnify_*` | 32×32 | 按钮/光标图标 |
| `inspection_receipt` | 103×133 | 检查回执 |
| `items_inspectable` | 512×512 atlas | 手部等可鉴定小图 |
| `cigarette_color_card_item` | 110×126 | 海明威烟盒颜色卡 |
| `cigarette_color_guide` | 16×16 | 颜色指南图标 |
| `food_stamp` | 32×32 | 食品券 |

## 3. 免疫宁 InjectorUI 专用资产

`InjectorUI` 字段：`serialTMP`、`companyTMP`、`modelTMP`、`display[]`、`color[]`、`broken[]`、`body`。

| 资产 | 尺寸 |
|---|---|
| `injector_background` | 16×64 |
| `injector_base` | 231×39 |
| `injector_color1/2` | 231×39 |
| `injector_dam1/2/3` | 231×39 |
| `injector_red/green/white/black/pink/pink_alt/turquoise/blue` | 64×16 |
| `injector_purple_large` | 64×16 |
| `injector_pure_white / _s` | 64×16 / 48×16 |
| `injector_blood_red` | 64×16 |
| `injector_pouch` | 32×48 |
| `pouch_injector_large` | 48×48 |

音频：`injector_use`、`injector_move`、`injector_move2`、`injector_select`。

## 4. 急救盒当前鉴定面板

- 入口仍是原版 `ToolDirectory` 放大镜 → `InspectionUIManager.OnInspectItem`。
- 面板由 `CustomUIManager.CreateWindow("first_aid_inspection", ..., "overlay")` 创建：
  - 物品图标：`Textures/Items/first_aid_box.png`（48×48）；
  - 勾/叉图标：`CustomUIManager.Instance.spriteGreenCheck` / `spriteRedXMark`；
  - 背景：CustomUI 默认 overlay 窗口（无专用全屏背景、无 AnimationClip）；
  - 按钮：`关闭`。
- 如果 Seedance 要出替换图，优先做：
  1. 急救盒物品图标 **48×48 像素风**（白底红十字药箱，透明底）；
  2. 鉴定背景 **960×540**（可用原版 `magnifying_screen` 当构图参考）；
  3. 勾/叉 UI 图标（绿色对勾/红色叉，18×18 或相近）；
  4. 如需更长远的“原版风格”，可以新做一个 960×540 的急救盒检查台预制体，把上面的图标放进去。

## 5. 已导出的参考图

`tools/assets/inspection_refs/` 下已导出同名 PNG，包含：

- `Texture2D_magnifying_screen.png`
- `Texture2D_magnifying_mask.png`
- `Texture2D_magnifying glass.png`
- `Texture2D_MagnifyingGlass.png`
- `Texture2D_inspection_receipt.png`
- `Texture2D_injector_background.png` / `injector_base` / `injector_color*` / `injector_dam*`
- `Sprite_injector_*.png`
- `Sprite_food_stamp.png`
- `Sprite_cigarette_color_guide.png`
- `Texture2D_items_inspectable.png`

可直接用这些图给 Seedance 当构图/尺寸/色调参考。
