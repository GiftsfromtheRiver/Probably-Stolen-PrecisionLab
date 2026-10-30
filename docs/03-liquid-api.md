# 03 — Liquid / Purity / Contaminant API and Mod Design

**Task:** task-3 (liquid-api). **Status:** complete. §12 is a delta review of the current `src/PrecisionLab` implementation against this API (bugs / gaps / NPE guards / self-test corrections).
**Scope:** exact semantics + copy-paste C# for two mod items:
1. **精确取液器** (Precision Liquid Extractor): move exactly N µL (N ≥ 1) from one liquid container to another.
2. **提纯机器** (Purification Machine): remove physical / chemical / biological contaminant classes from liquid containers without destroying valuable product liquids.

**Sources of truth (read-only):**
- Decompiled game C#: `<workspace>\decompiled\Assembly-CSharp\` (928 files). All `file.cs:line` references below are relative to that directory.
- Game install: `<game>\` (never modified; game never launched by this task).
- Corroboration only (not authority): decompiled real mod `decompiled\ItemDetails\ItemDetails\WaterInfo.cs`, `WaterAdvice.cs`, `PurifySources.cs`.

**Verification of the helper code:** the two C# files in §10 were compiled with Roslyn (`csc` from .NET SDK 9.0.318) against the game's real `Probably Stolen_Data\Managed\Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, `mscorlib.dll`, `netstandard.dll`, `System.Runtime.dll`, `System.Core.dll`, with `-langversion:latest` and `-warnaserror+`: **0 errors, 0 warnings** (`-target:library`, output 18 KB). They reference only types that exist in the shipped assemblies.

**How to read this doc:** §0 is the decision sheet. §1–§5 are ground truth. §6–§7 are the two mod designs with code. §8 is the economy justification. §9 is containers/test setup. §10 is copy-paste code. §11 is the source map + open questions.

---

## 0. Decision sheet (TL;DR)

| Topic | Decision | Evidence |
|---|---|---|
| Volume unit | **µL (microlitre)**, stored as `int` in the tag `CURRENT_PART_<ID>`; 1 ml = 1 000 µL, 1 L = 1 000 000 µL | `WaterHelper.cs:73` (`capacityInMl * 1000`), `Liquid.cs:533-534` (tag naming) |
| Capacity | `int` µL in tag `LIQUID_CONTAINER_CAPACITY`. Init clamps to 2 147 483 ml (= 2 147 483 000 µL) | `WaterHelper.cs:51,68-77` |
| Purity | **Water purity only**: `bp = waterµL * 10000 / totalµL` (basis points, 0…10000). Displayed as `bp/100` with 2 decimals. 9900 = 99.00%. There is **no generic “liquid purity”**: every non-water liquid lowers it | `WaterHelper.cs:557-574`; `WaterFeatureHelper.cs:441-449`; `WaterInfo.cs:84` (mod corroboration) |
| Purity tiers | 9990 pure · 9900 high-quality · 9800 base · 9600 ghostwater · 9000 rustwater · 3500 gutterflow; below → unknown | `WaterFeatureHelper.cs:61-73,343-353` |
| Purity affects value via | `ItemFeature` condition `CATEGORY_WATER_PURITY` modValue: pure **+100 %**, HQ **+50 %**, base **0 %**, ghost **−33 %**, rust **−75 %**, gutterflow **−90 %** (applied to `unitValue`), plus client acceptance gating | `ItemConditionList.cs:325-395`; `GameItem.cs:1661-1688,1721-1764` |
| Water base price | `unitValue = round(40 × totalµL / 1 000 000)` = **40 credits per litre of liquid volume, quality-independent**; per-tier price arrays exist but are not used by this formula | `WaterFeatureHelper.cs:47-59,369-387` |
| Second purity metric (chemical products) | `CHEMICAL_PRODUCT_PURITY_TAG` = **active ingredient µL / total µL × 100** (a percent, not bp); grade condition modValue lab_pure **+135 %** … starch_grade **−95 %** | `ChemicalProductHelper.cs:103-145`; `ItemConditionList.cs:682-757` |
| Contaminant classes (recommended) | Physical: `physical_contaminant`, `microplastic`, `debris`, `mineral`; Chemical: `chemical_contaminant`, `heavy_metal`; Biological: `microbe`, `organic_waste`, `plant_matter`. Everything else is a **product/reagent and protected by default** | §4, `Liquid.cs:129-482` |
| Exact extraction API (recommended) | `int TransferExact(GameItem source, GameItem target, int volumeUL, TransferMode mode, string singleLiquidId, WaterLabelMode targetLabelMode)`; **exact-or-nothing**, returns µL moved or 0; 3 modes: proportional mixture / water only / single liquid | §6, code §10.1 |
| Transfer side effects | write `CURRENT_PART_*` both sides → `ChemicalHelper.HandleReceiveLiquidTemp` → `ValidateShapeState()` → `WaterFeatureHelper.OnLiquidContainerPourInto` + `OnLiquidContainerPourOutOf` (do **not** re-implement `unitValue`/sprite/sound) | `WaterHelper.cs:267-316`; `WaterFeatureHelper.cs:77-147` |
| Purifier water mode | Reuse vanilla budget curve + `MachinePurifier.GetMaxPurityCap`; **cap 9800 bp base machine, +quality bonus, ceiling 10000**; removed volume **converted to water** (vanilla parity) | §5, §7.2; `MachinePurifier.cs:114-215` |
| Purifier concentrate mode | Removed volume **deleted** (no water added); target = active-fraction / grade bp; used before `pH`-dry recipes; never touches protected liquids | §7.2, §8.3 |
| Cycle budget | `budget = (min(total,10 000 000) × 3‰ + 25 000) × dirtBonus × 1.25`, `dirtBonus = MachinePurifier.GetDirtBonus(purity) + performanceBonus/100` (clamped ≥ 0); concentrate mode × 0.5 | `MachinePurifier.cs:8-28,114-141,193-210` |
| Power | Water mode `drawnPower = 4` (vanilla purifier parity), concentrate mode 6; module quality curve identical (`MACHINE_QUALITY_RATIO_INT = 2`) | `MachinePurifier.cs:30-56`; `MachineryHelper.cs:117,532-537` |
| Never do | never write a “purity” tag (purity is derived); never use `WaterHelper.AddLiquid` for exact fills (capacity check is per-liquid, overfill bug); never purify `CHEM_ITEM`/wine/`WINE_CONTAINER_TAG` containers with this helper | §2.5, §6.4 |

---

## 1. Data model

### 1.1 Units and scale

| Quantity | Storage | Unit | Notes |
|---|---|---|---|
| Liquid amount per species | tag `CURRENT_PART_<ID>` (`int`) | **µL** | tag names built in `Liquid.SetIdentifier`, `Liquid.cs:530-535` |
| Container capacity | tag `LIQUID_CONTAINER_CAPACITY` (`int`) | **µL** | `WaterHelper.cs:8,73-77`; init args are ml, multiplied by 1000 |
| Total volume | sum over `Liquid.Liquids` of `CURRENT_PART_*` | µL | `WaterHelper.cs:576-584` |
| Purity | derived | basis points 0…10000 | `WaterHelper.cs:557-574` |
| Container fill % (UI) | derived | integer 0…100 | `WaterHelper.GetCurrentCapacity`, `WaterHelper.cs:440-453` |
| Temperature | tag `CONTAINER_TEMP_TAG` (`double`) | °C, default 24, ambient 26 | `ChemicalHelper.cs:1156-1165,172-189` |
| Chemical product grade | tag `CHEMICAL_PRODUCT_PURITY_TAG` (`double`) | percent 0…100 | `ChemicalProductHelper.cs:103-145` |
| Filter pore size | tag `FILTER_SIZE_TAG` (`int`) | abstract “particle size” units, comparable to `Liquid.particleSize` | `Liquid.cs:121`; `WaterHelper.cs:333-395` |

Hard ceilings: `LIQUID_CONTAINER_CAPACITY` is an `int`, so max = `int.MaxValue` µL ≈ 2 147 483.647 ml; `InitLiquidContainerItem` explicitly clamps capacity to 2 147 483 ml (`WaterHelper.cs:68-72`). Gold rule: **do all sums in `long`** when you mix, split or compare volumes; int sum of parts could overflow on a malformed save.

### 1.2 Where liquid data lives (tag snapshot)

Every liquid container is an ordinary `GameItem` whose `state`/`modifiedState` (`TagSystem`) holds:

```
LIQUID_CONTAINER_TAG          = true          // marker: this is a liquid container
LIQUID_CONTAINER_CAPACITY     = 750000        // µL  (750 ml bottle)
LIQUID_CONTAINER_BASE_PRICE_TAG = 5           // credits, container itself
CURRENT_PART_WATER            = 735000        // µL
CURRENT_PART_MICROBE          = 5000
CURRENT_PART_ORGANIC_WASTE    = 10000
CURRENT_PART_HEAVY_METAL      = 0             // every liquid has a CURRENT_PART_* tag, initialized to 0
... one tag per Liquid.Liquids entry, all enabled
CONTAINER_TEMP_TAG            = 24.0
```

Purity example: `735000 * 10000 / 750000 = 9800` → 98.00 % → “Basewater”.

Tags are **derived from `Liquid.Liquids` order** — `WaterHelper.GetLiquidParts` returns `int[]` aligned with `Liquid.Liquids[i]` (`WaterHelper.cs:257-265`). The `part` tag (`PART_<ID>`) is the *blueprint* marker while `currentPart` (`CURRENT_PART_<ID>`) is the live amount (`Liquid.cs:11-13,533-534`). Use `currentPart`.

### 1.3 The Liquid catalogue (36 entries)

All fields below are `public` on `Liquid` (`Liquid.cs:5-47`). Table order = `Liquid.Liquids` order (`Liquid.cs:129-482`); this order is what `GetLiquidParts`/`SplitProportionally` indices refer to, so **never sort/reorder this list at runtime**.

Legend: `pSize` = `particleSize`; `maxPurity` = `baseMaxPurityFromMachine` (basis-point-ish value used only by plant/aquarium “floor” logic, *not* by the purifier budget); `removeAmt` = `basePurifierRemoveAmount` (only used as a “removable or not” flag by plants — `PlantWaterQuality.cs:36-43` — the vanilla purifier never calls `GetPurifierRemoveAmount`); `removable` = `canBeRemovedByPurifier`; `filterMin` = `filterMinimumPurity` (fractional residual floor for filters); `filterDur`/`filterVol` = filter durability use / µL per use; `b.p.` = boiling point, `evap` = evaporative.

| # | identifier | display key | class (mod) | pSize | maxPurity | removeAmt | removable | filterMin | filterDur/Vol | b.p./evap | notes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | `water` | liquid_water | solvent (never removed) | 10 | 0 | 0 | **false** | – | – | 100/yes | only “pure” liquid |
| 1 | `heavy_metal` | liquid_heavy_metal | chemical | 12 | 25 | 2000 | true | – | – | – | REMOVED_LAST by vanilla |
| 2 | `organic_waste` | liquid_organic_waste | biological | 40 | 10 | 5000 | true | 0.0016 | 1 / – | – | heat ⇒ physical_contaminant |
| 3 | `microplastic` | liquid_microplastic | physical | 25 | 18 | 2500 | true | 0.002 | 2 / 1500 | – | filterable |
| 4 | `microbe` | liquid_microbes | biological | 50 | 9 | 5000 | true | – | – | – | filters pass it; UV/heat/alcohol/chlorine kill |
| 5 | `chemical_contaminant` | liquid_chemical_contaminant | chemical | 2 | 16 | 3000 | true | – | – | – | REMOVED_LAST; chlorine/tablet target |
| 6 | `physical_contaminant` | liquid_physical_contaminant | physical | 100 | 7 | 5000 | true | 0.0016 | 2 / 1500 | – | biggest particle, removed first |
| 7 | `mineral` | liquid_mineral | physical | 30 | 7 | 5000 | true | 0.0016 | 1 / 2000 | – | REMOVED_LAST; also a hydroponic nutrient |
| 8 | `dream_cap_mush` | liquid_dream_cap_mush | **product** (protected) | 200 | 12 | 4000 | true | – | – | – | degrades >50 °C into plant_matter |
| 9 | `dream_cap_extract` | liquid_dream_cap_extract | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | active of dream_dust |
| 10 | `plant_matter` | liquid_plant_matter | biological | 200 | 12 | 4000 | true | – | – | – | waste from mushroom degradation |
| 11 | `alcohol` | liquid_alcohol | **product/reagent** (protected) | 10 | 12 | 4000 | true | – | – | 78.37/yes | kills microbes ≥40 % |
| 12 | `caffeine` | liquid_caffeine | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | active of caffeine_pill |
| 13 | `protein` | liquid_protein | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | acid/base precipitates ⇒ debris |
| 14 | `fat` | liquid_fat_oil | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | lipobond sediments ⇒ debris |
| 15 | `starch` | liquid_starch | **filler** (protected) | 5 | 12 | 4000 | true | – | – | – | “cut” in chem recipes |
| 16 | `sugar` | liquid_sugar | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | |
| 17 | `coffee_mush` | liquid_coffee_bean_mush | **product** (protected) | 200 | 12 | 4000 | true | – | – | – | extraction feedstock |
| 18 | `contraceptive` | liquid_contraceptive_hormones | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | active of birth_control_pill |
| 19 | `zyanide` | liquid_zyanide | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | lethality 1.66 |
| 20 | `nightmare_extract` | liquid_nightmare_extract | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | active of nightmare_dust |
| 21 | `nightmare_cap_mush` | liquid_nightmare_cap_mush | **product** (protected) | 200 | 12 | 4000 | true | – | – | – | degrades ⇒ plant_matter |
| 22 | `chlorine` | liquid_chlorine | **reagent** (protected) | 5 | 12 | 4000 | true | – | – | – | kills microbes, self-degrades |
| 23 | `synaprest` | liquid_synaprest | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | |
| 24 | `fanta` | liquid_fanta | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | |
| 25 | `methylol` | liquid_n_methylol_base | **reagent** (protected) | 5 | 12 | 4000 | true | – | – | – | synthesis reagent |
| 26 | `reducing_agent` | liquid_reducing_agent | **reagent** (protected) | 5 | 12 | 4000 | true | – | – | – | synthesis reagent |
| 27 | `acid` | liquid_acid | **reagent** (protected) | 5 | 12 | 4000 | true | – | – | – | pH reagent; kills microbes |
| 28 | `base` | liquid_base | **reagent** (protected) | 5 | 12 | 4000 | true | – | – | – | pH reagent; kills microbes |
| 29 | `stabilizing_agent` | liquid_stabilizing_agent | **reagent** (protected) | 5 | 12 | 4000 | true | – | – | – | sedimentation ⇒ debris |
| 30 | `debris` | liquid_insoluble_debris | physical | 200 | 0 | 6000 | true | – | – | – | chemistry byproduct |
| 31 | `lipobond_agent` | liquid_lipobond_agent | **reagent** (protected) | 5 | 12 | 4000 | true | – | – | 55/yes | sediments fat ⇒ debris |
| 32 | `livestock_steroid` | liquid_livestock_steroid | **additive** (protected) | 5 | 12 | 4000 | true | – | – | – | animal buffs via `OnEnterSystem` |
| 33 | `livestock_immunity` | liquid_livestock_immunity | **additive** (protected) | 5 | 12 | 4000 | true | – | – | – | animal buffs |
| 34 | `wine_extract` | liquid_wine_extract | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | wine system |
| 35 | `nomore` | liquid_nomore | **product** (protected) | 5 | 12 | 4000 | true | – | – | – | active of pink_alt_injector |

`Liquid.Liquids` count = 36; `IngestableLiquids` = those with `metabolismRatePerDay > 0` (`Liquid.cs:127`). `Liquid.LiquidDict` maps identifier → object (`Liquid.cs:125,484-492`). Look-ups: `Liquid.GetLiquidFromId` logs an error for unknown ids (`Liquid.cs:504-512`) — prefer checking `Liquid.LiquidDict.ContainsKey` first (helpers in §10 do this).

### 1.4 Two purity notions (critical)

1. **Water purity** — derived from the container; only water counts as “pure”. Product liquids are NOT exempt: a container of 50 % `dream_cap_extract` + 50 % water has water purity **5000**. `GetContaminantPercentage` measures a contaminant relative to water on the same 1/10000 scale, allowing up to `1 000 000` = 100× water volume (`WaterHelper.cs:32,1399-1417`).
2. **Chemical product purity** — only on items with `CHEMICAL_PRODUCT_TAG`: stored percent = active-ingredient µL / total µL × 100 (`ChemicalProductHelper.GradeProduct`, `ChemicalProductHelper.cs:103-145`; tag const line 7). It is refreshed **only** by calling `GradeProduct` (or `InitChemicalProduct`). Injectors/dust/pills are `CHEM_ITEM`s, not containers.

Consequence: “purify liquid X” is ambiguous. Water mode optimizes (1); concentrate mode optimizes acting ingredient fraction (2). Design covers both (§7).

### 1.5 GameItem tag API and the `modifiedState` trap

`GameItem` (`GameItem.cs:60-62`) keeps two `TagSystem`s: `state` (authoritative storage) and `modifiedState` (a cached snapshot computed from `state` + parent item modifiers by `ComputeModifiedShapeState`, `GameItem.cs:1343-1409`). Reads (`IsTag`, `GetTagReadonly`) use **`modifiedState`** (`GameItem.cs:656-676`). Writes via `ModifyTag` mutate the `TagState` object that both dictionaries share (TagSystem copy ctor is shallow, `TagSystem.cs:13-16`), so values are visible immediately — **but tags created for the first time after the last snapshot are not in `modifiedState`** until `ValidateShapeState()`/`SyncModifiedState()` is called (`GameItem.cs:1314-1341`).

Practical rules for the mod:
- All liquid part tags exist on any container created through `WaterHelper.InitLiquidContainerItem` (`WaterHelper.cs:86-92`), so ordinary writes are visible.
- During batch writes use `skipValidate: true` for speed, then call **one** `container.ValidateShapeState()` per container at the end (also refreshes UI/inventory state via `Validate()` + `OverlayHandler`).
- Never call `ModifyTag` on a freshly invented tag and immediately read it back with `GetTagReadonly` while `skipValidate: true` was used.

### 1.6 Two container systems — use the WaterHelper one

| System | Tags | Purpose | Who uses it |
|---|---|---|---|
| **WaterHelper** (current, 2024+) | `LIQUID_CONTAINER_TAG`, `LIQUID_CONTAINER_CAPACITY`, `CURRENT_PART_*` | water/food/chem containers, purification, selling, features | water bottles, jugs, cylinders, droppers, tanks, pitcher, RAS tank, wine bottles |
| `LiquidContainerHelper` (legacy, simple) | `LIQUID_CONTAINER_CURRENT`, `LIQUID_CONTAINER_MAX`, `LIQUID_CONTAINER_SIPPING` | simple drinkable volumes, cup noodles, ship water logistics | `LiquidContainerHelper.cs:4-308`; used by `UnusedDirectory.cs:429`, `FoodItemHelper.cs:59-63` (cup_noodle 375), `ShipHelper.cs:232-270`, `StationMachinery.cs:79`; also contributes to item value via `GameItem.cs:435` |

Do not mix them. An item is a *real* liquid container iff it has `LIQUID_CONTAINER_TAG` and a `LIQUID_CONTAINER_CAPACITY` tag. Legacy items (e.g. cup noodle) have neither; `WaterHelper.GetTotalVolume` on them returns 0.

### 1.7 CHEM items share the tag layout

`ChemicalHelper.InitChemItem` initializes all `CURRENT_PART_*` tags to 0, and `SetChemContentML` writes `volume*1000` (`ChemicalHelper.cs:44-97`) — same µL tags, **no capacity tag**. `ChemicalHelper.GetTotalVolume` guards on `CHEM_ITEM_TAG` (`ChemicalHelper.cs:196-208`). If the mod ever mutates a `CHEM_ITEM_TAG`/`CHEMICAL_PRODUCT_TAG` item, it must call `ChemicalProductHelper.GradeProduct` afterwards, or the stored grade/condition is stale (§7.9, helper method `ConcentrateChemicalProduct`).

---

## 2. Volume & purity API (answers (a))

### 2.1 Read APIs (all `public static`, `WaterHelper` unless noted)

| API | Returns | Unit | Behaviour / edge cases |
|---|---|---|---|
| `WaterHelper.GetTotalVolume(GameItem)` `:576-584` | sum of all part tags | µL | **Does not check the tag** — call only on containers (returns 0 for empty item, but also sums CHEM-item compositions if misused) |
| `WaterHelper.GetTotalCapacity(GameItem)` `:412-419` | `LIQUID_CONTAINER_CAPACITY` | µL | 0 if no tag |
| `WaterHelper.GetTotalCapacityML(GameItem)` `:421-428` | capacity / 1000 | ml | integer division |
| `WaterHelper.GetFreeCapacity(GameItem)` `:397-410` | capacity − total | µL | may be negative if save is corrupt; clamp for display |
| `WaterHelper.GetCurrentCapacity(GameItem)` `:440-453` | `total*100/capacity` | fill % 0…100 | **divides by capacity → DivideByZeroException if capacity tag is 0/missing**; guard |
| `WaterHelper.GetCurrentCapacityML/UL` `:455-481` | total/1000, total | ml / µL | UL == `GetTotalVolume` for containers |
| `WaterHelper.GetLiquidParts(GameItem)` `:257-265` | `int[36]` aligned with `Liquid.Liquids` | µL | best primitive for composition math |
| `WaterHelper.GetWaterPurity(GameItem)` `:557-574` | `water*10000/total` | bp 0…10000 | 0 if empty or not container |
| `WaterHelper.GetContaminantAmount(item, id)` `:1399-1402` | one part | µL | thin wrapper |
| `WaterHelper.GetContaminantPercentage(item, id)` `:1404-1417` | contaminant*10000/water | 1/10000 vs water, capped 1 000 000 | 1 000 000 means “≥100× water” |
| `WaterHelper.IsEmpty/IsFull/IsWet` `:641-655,1568-1590` | bool | – | `IsWet` = evaporative (water/alcohol/lipobond) volume **> 100 µL** — matters for dry chem recipes |
| `WaterHelper.GetContainerPrice(item)` `:609-616` | `LIQUID_CONTAINER_BASE_PRICE_TAG` | credits | part of item value, not purity-scaled |
| `WaterHelper.GetLiquidContainerColor(item)` `:1619-1645` | hex RGB | – | sprite tint from volume-weighted liquid colours |
| `WaterFeatureHelper.GetWaterFeature(item)` `:169-197` | `ItemFeature` | – | returns `null` for non-containers; creates + adds the feature if missing; **wine containers get a synthetic feature** (`customIntValue1 = 9850`, line 182) |
| `WaterFeatureHelper.GetWaterDisplayName(int bp)` `:343-353` | localized tier | – | pure/HQ/base/ghost/rust/gutterflow/unknown |
| `WaterFeatureHelper.FormatPurityPercentage(int bp)` `:441-444` | `"98.00%"` | – | |
| `WaterFeatureHelper.GetCurrentWaterDisplay(item)` `:315-327` | tier string | – | exposed → actual purity, else the *presumed* (fake) condition value |
| `WaterFeatureHelper.IsLabelledHazardousWater(item)` `:329-341` | bool | – | true if labelled purity < 9800; clients with `refusesHazardousWaterLabel` reject these (`StoreClient.cs:384`) |
| `WaterFeatureHelper.GetWaterPrice(...)` `:369-387` | credits | – | volume-only base price (see §8.1) |
| `ItemFeature.customIntValue1` (public field) | int | bp | the container's *presumed* purity label; use `SetPresumedPurity` (`WaterFeatureHelper.cs:266-275`) rather than writing directly |
| `ChemicalProductHelper.GetPurity(item)` `:147-154` | active fraction | % 0…100 | chem products only |

Canonical read snippet (compiles; this is exactly what §10 helper wraps):

```csharp
// purity (bp) + composition of any liquid container
int totalUL = WaterHelper.GetTotalVolume(container);
int capUL   = WaterHelper.GetTotalCapacity(container);
int freeUL  = WaterHelper.GetFreeCapacity(container);
int purityBP = WaterHelper.GetWaterPurity(container);       // 0..10000
int[] parts = WaterHelper.GetLiquidParts(container);        // aligned with Liquid.Liquids
for (int i = 0; i < Liquid.Liquids.Count; i++)
{
    if (parts[i] > 0) /* Liquid.Liquids[i].identifier, parts[i] µL */;
}
```

### 2.2 Write APIs (vanilla)

| API | Effect | Safety |
|---|---|---|
| `WaterHelper.AddLiquid(container, liquidId, addAmount)` `:893-917` | adds to one part; clamps to `capacity − currentAmountOfThatLiquid`; updates temp, water feature, sprite | **capacity clamp is per-liquid, not per-total → can overflow total volume** when adding a liquid that is low while other liquids fill the container. Never use for exact dosing |
| `WaterHelper.AddMaxLiquid(container, liquidId)` `:919-939` | fills one liquid to capacity | same caveat |
| `WaterHelper.AddWater(container, grade, addAmount=-1, ...)` `:941-1070` | fills free capacity with premade grade water (0 pure 9990-9999, 1 HQ 9900-9989, 2 base 9800-9899, 3 ghost 9600-9799, 4 rust 9000-9599, 5 gutterflow 3500-8999 bp) + appends contaminant distribution; applies attached filter; re-inits water feature | amount is clamped to free capacity; **fills full when addAmount = -1** |
| `WaterHelper.Remove(container, volumeInPart)` `:1483-1517` | removes µL proportionally from mixture; refreshes feature | lossy (volume destroyed) |
| `WaterHelper.RemoveAndReturn(container, volumeInPart)` `:1519-1566` | proportional removal, returns `List<KeyValuePair<string,int>>` of removed species | **the vanilla primitive behind “move a volume”**; returns/exports composition |
| `WaterHelper.RemoveMl(container, ml)` `:1446-1481` | `Remove(ml*1000)` | |
| `WaterHelper.RemoveExcessLiquid(item)` `:430-438` | removes overflow proportionally | can silently delete product liquids if you overfilled |
| `WaterHelper.EmptyContainer(item)` `:618-639` | zeroes all parts, feature exposed false, sprite refresh | |
| `WaterHelper.RemoveContaminantFromContainer(container, id, vol, minPercentage, floorBaseVolume=-1)` `:853-891` | removes up to `vol`, keeps a floor = `minPercentage/10000 × (floorBaseVolume ?? water)`; converts nothing; returns removed | used by tablets/plants; **refuses if water == 0** and if `!canBeRemovedByPurifier` |
| `WaterHelper.OnUVUsed(container)` `:838-851` | microbe → water | all-or-nothing |
| `WaterHelper.OnWaterPurificationTabletAdded(container)` `:686-704` | microbe −75 000 (min 0), chemical_contaminant −10 000 (min 5 % of water), removed → water | fixed effects |
| `WaterHelper.PurifyToBaseWater(container)` `:739-790` | **destructive** “portable purifier”: shrinks all non-water liquids (except chemical_contaminant/heavy_metal which are shrunk last) until water ≥ 9800 bp; products included! | do not reuse for a product-safe machine |
| `WaterHelper.SetLastScanResult(item)` `:1592-1609` | writes `LAST_SCAN_RESULT_TAG` string = `"[Last Scan]: 98.00% Basewater"` | display-only; does not change label/value economics |
| `ChemicalHelper.InitContainerTemp(item)` `:1156-1165` | ensures `CONTAINER_TEMP_TAG = 24` | |
| `ChemicalHelper.HandleReceiveLiquidTemp(target, volume, source)` `:1125-1143` | mixes receiver temperature toward source temperature | call once per transfer |

### 2.3 Refresh / side-effect APIs (must run after any mutation)

| Call | Effect |
|---|---|
| `WaterFeatureHelper.OnLiquidContainerPourInto(target, source, wasEmpty)` `:94-147` | when target was empty: copies source exposure + presumed purity, sets public/actual display, price, exposure; refreshes sprite/sound. When not empty: marks `STRIP_TESTED_TAG` off (mixture untested), recomputes real condition/value |
| `WaterFeatureHelper.OnLiquidContainerPourOutOf(source)` `:77-92` | re-prices, updates feature with its own exposure state, hides feature when empty, disables `STRIP_TESTED_TAG`, sprite + sound |
| `WaterFeatureHelper.UpdateWaterFeature(item, isExpose)` `:227-264` | recompute displays/conditions from current liquid; `isExpose=true` sets presumed purity := actual; `false` **keeps the old label (fake condition) and sets exposed=false**; always resets `LAST_SCAN_RESULT_TAG`; calls `ChemicalHelper.HandleEmptyContainerTemp` |
| `WaterFeatureHelper.InitWaterFeature(item, isExpose=false)` `:199-220` | full re-init: both conditions from actual purity, display, unitValue, good-type, temp; used by `InitLiquidContainerItem` |
| `WaterHelper.UpdateContainerGoodType(item)` `:586-607` | flips item type `WATER` ↔ `HOUSEHOLD_GOOD` based on emptiness, re-adds event features if in back inventory |
| `SpriteHelper.UpdateWaterContainerSprite(item)` `:3-105` | recomputes sprite variant (`0/10/25/50/75/100` capacity buckets) + water feature + good type + RAS hook + sound |
| `WaterHelper.UpdateWaterBottleSound(item)` `:36-49` | full/empty plastic bottle audio |
| `GameItem.ValidateShapeState()` `:1320-1341` | recompute modified state + `Validate()` + parent inventory validate + overlay UI refresh |
| `MachineRAS.OnWaterTankLiquidChanged(item)` | called inside sprite helper; keeps RAS tanks in sync |
| Audio: `AudioHelper.PourWater()`, `DropperDraw()`, `DropperDrop()`, `UseFaucet()`, `DumpWater()` | polish; vanilla chooses by container/tool |

**Ordering that matches vanilla `TransferLiquid`** (`WaterHelper.cs:267-316`): write tags → temp mixing per species → `OnLiquidContainerPourInto(target, source, wasEmpty)` → `OnLiquidContainerPourOutOf(source)`. The two pour calls already do sprite/sound/feature/value work; do not duplicate.

### 2.4 Worked read examples

Container: `large_bottled_water` (2 000 ml → capacity 2 000 000 µL), filled 1 500 000 µL base water (9800-9899 bp) then 5 000 µL `microbe` added:

- total = 1 505 000 µL; free = 495 000 µL
- purity bp = 1 500 000 × 10 000 / 1 505 000 = **9966** (99.66 %) — display tier flips to “High Quality Water” (≥9900) even though microbes are present, because purity only measures water fraction.
- microbe “contaminant percentage” = 5 000×10 000/1 500 000 = **33** (0.33 % of water volume).
- If instead you want a *safety* metric, use classes: biological = 5 000 µL; class purity (`GetCategoryPurityBP`) = 1 500 000/(1 500 000+5 000) = 9966 as well.

This illustrates why the mod machine must expose both raw purity and per-class contaminant volumes (§7.8).

### 2.5 Known vanilla bugs / traps in read/write APIs

1. `AddLiquid` capacity check is per-liquid (`WaterHelper.cs:904-909`) → total can exceed capacity. Always pre-check `GetFreeCapacity` yourself and write tags directly for exact dosing.
2. `GetCurrentCapacity` divides by capacity with no guard (`:446-452`) → guard `capacity > 0` (helper `GetFreeVolumeUL`/`GetCapacityUL` does).
3. `RemoveExcessLiquid` removes proportionally — if you overfill with a valuable liquid you lose products.
4. `Remove`/`RemoveAndReturn`/`RemoveMl` **destroy** the removed volume (they are pour-to-drain semantics), and they call `OnLiquidContainerPourOutOf` themselves.
5. `GetTotalVolume` ignores the container tag (see §2.1).
6. `PurifyToBaseWater` destroys all non-water liquids (including dream_cap_extract, alcohol, mineral); it is the “portable water purifier” (325-credit shortcut) and it is deliberately strong. Our machine must not copy it.
7. Water purity can be printed as “99.66 %” while 5 000 µL of microbes are present — purity is not safety. Any mod UI should show contaminant classes separately.
8. `WaterHelper.AddWater` re-inits the water feature; if called after direct tag writes it overwrites label state (fine for fill, wrong for delicate operations).

---

## 3. Vanilla liquid movement and the precision gap

### 3.1 Every vanilla path that moves liquid

| Path | Code | Volume semantics |
|---|---|---|
| Container → container drag/pour | `WaterHelper.InitLiquidContainerItem` closure `:98-143` → `WaterHelper.TransferLiquid` `:267-316` | moves `min(targetFree, sourceTotal)` — **all-or-max**, proportional composition, honours target filter |
| Dropper (`LIQUID_CONTAINER_DROPPER_TAG`, 1 ml/5 ml droppers) | `:119-142` | full transfer only; special audio; still all-or-max |
| Faucet | `:150-163` | `AddWater(container, 1, 9999999)` → **fills to capacity** with today's rustwater or gutterflow purity |
| Drain | `:144-149` | `EmptyContainer` (destroys all) |
| Water filter in filter container | `WaterHelper.GetFilterItem` `:318-331`, `HandleFilter` `:333-395` | applied during pour/fill only; removes species with `filterSize <= particleSize` down to `filterMinimumPurity` floor, consumes durability, `uv_filter` deletes microbes completely |
| Purification tablet | `:686-704` | fixed −75 000 microbe, −10 000 chemical (floor 5 %) → water |
| UV filter tool | `:838-851` | microbe → water (all) |
| Portable water purifier | `:739-790` | shrink all non-water to reach 9800 bp |
| Hydroponic nutrient tablet | `:792-836` | +15 000 mineral, displaces water if needed |
| Evaporator | `MachineEvaporator.cs:61-147` | up to **300 000 µL per cycle** of *evaporative* liquids only (water/alcohol/lipobond); distillate recovered = `volume × qualityBonus/100` into another container |
| Blender | `MachineBlender.cs:27-46` → `ChemicalHelper.BlendInto` `:99-125` | dissolves whole chem item into container over 10 passes, with loss % (35/20/10/5/0) |
| Moisture farm | `MachineMoistureFarm.cs:237-269` | `AddWater(farmJug, grade, volume)` then `TransferLiquid(jug, container)` — fills whole jug, pours all |
| Wine bottle | `WineHelper.cs:226-272,274-283` | 750 000 µL; own state machine; water feature functions early-return |
| Water ration (consumable) | `WineHelper.cs:137-160` | destroys ration, adds 750 ml (or 375 ml small) base water to container |
| Chem finisher | `MachineChemFinisher.cs:66-80` | **exact**: moves `ChemFinisherRecipe.requiredVolume` µL from container into a chem item via `ChemicalHelper.TransferLiquidToChem` |
| Meds injectors (premade) | `MedsItemDirectory.cs:354-359,436-446` | exact composition: 4 000 µL total, active 45-60 % (`NOMORE_INJECTOR_VOLUME = 4000`, `ChemFinisherRecipe.cs:16`) |
| RAS tank | `MachineRAS.cs:238-254` | 12 000 ml tank; remediation via `WaterHelper.RemoveContaminantFromContainer` (`AquacultureHelper.cs:1788-1816`) |
| Vending fountain / drop | `VendingFountainHelper.cs` | machine-side refill logic |
| Debug panel | `DebugPanel.cs:998-1000` | `AddLiquid(item, "acid", 250000)` etc. — useful reference for test setup |

### 3.2 Does the game ever require precise volumes?

Yes, but only **internally**, never as player-facing dosing:

- `ChemFinisherRecipe.requiredVolume`: 100 (`birth_control_pill`), 500 (`fanta_pill`), 600 (`zyanide_pill`), 2 000 (`dream_dust`), 4 000 (`nightmare_dust`, `pink_alt_injector`), 5 000 (`caffeine_pill`) µL (`ChemFinisherRecipe.cs:20-87`). The finisher transfers **exactly** that volume (proportional to mixture) and requires `total >= requiredVolume` + dry (`CanOutput`, `:89-100`).
- Injector recipes: 4 000 µL total (`MedsItemDirectory.cs:354-359`).
- Evaporator: 300 000 µL/cycle, scaled by performance bonus.
- `WaterHelper.CanPourVolume(container, ml)` exists (`:1304-1311`) but is only used by wine/ration pouring, not by a generic player volume picker.

### 3.3 The gap = product definition of 精确取液器

What vanilla **cannot** do:
- move an arbitrary N µL (1 µL resolution) — pour/dropper move all-or-capacity;
- pick a subset of the mixture (only water, or one species) when pouring — only filters do that, and they remove instead of transfer;
- separate an exact aliquot into a calibrated container (1/5/25/50/100 ml cylinders exist as *containers* but have no dosing interaction);
- preserve exact composition proportional to a specified volume (needed for reproducible chem batches where `requiredVolume` is 100-6 000 µL).

So the precision extractor is *not* a convenience reskin: it is the missing dosing primitive that the existing calibrated containers (droppers/cylinders) were clearly designed for (§9.2).

Design requirements derived from the gap:
1. exact N µL (N ≥ 1) or fail atomically — no partial surprise;
2. three composition modes: proportional mixture (default; matches `SplitProportionally`), water-only, single-liquid;
3. capacity validation with reason strings for UI;
4. ownership validation (both containers effectively owned) to avoid draining store/world items;
5. all vanilla side effects (temp, features, sprite, sound, labels) so items behave normally after extraction;
6. refuse wine containers (own simulation) and non-containers;
7. UI hooks: ±1 µL stepper + ml/µL switch + preset chips (1 µL, 100 µL, 1 ml, 5 ml, “half”, “all”), live “requested vs possible” readout.

---

## 4. Contaminant classification (answers the classification part of (c))

### 4.1 Three gameplay classes (recommended mapping)

Rationale grounded in vanilla mechanics: particle size determines filterability and vanilla purifier order; `REMOVED_LAST` marks the three “hard” species; production/removal routes come from `ChemicalHelper` reactions and water premades. Classification is a **mod policy**, not an engine concept — but it is chosen to align with how vanilla treats each liquid.

| Class | Liquids | Why this class | Vanilla signals |
|---|---|---|---|
| **Physical** (particulates) | `physical_contaminant`, `microplastic`, `debris`, `mineral` | insoluble solids / grit / plastic; largest particle sizes; the things a mechanical filter bed catches | pSize 100 / 25 / 200 / 30; all `filterMinimumPurity` in 0.0016–0.002 (filters can remove ~99.84 % but not all); `debris` is the universal chemistry sediment (see §4.4); `mineral` is REMOVED_LAST + also a hydroponic nutrient (`PlantWaterQuality.cs:36-43` deliberately excludes mineral from plant purification) |
| **Chemical** (dissolved/non-particulate) | `chemical_contaminant`, `heavy_metal` | dissolved salts/metals, smallest particle sizes; not caught by mechanical filters; vanilla treats them as hardest (REMOVED_LAST) | pSize 2 / 12; both in `MachinePurifier.REMOVED_LAST` (`MachinePurifier.cs:28`); targets of purification tablets (`WaterHelper.cs:691,676-680`) and chlorine chemistry; heavy metals come from rustwater/gutterflow/bad-water distributions |
| **Biological** (living/organic) | `microbe`, `organic_waste`, `plant_matter` | living organisms and their organic residue; killed/transformed by heat, alcohol, chlorine, acid/base, UV; special-cased by filters (standard filters let microbes through; `uv_filter` deletes them) | pSize 50 / 40 / 200; `microbe` explicitly bypasses standard filter (`WaterHelper.cs:359-362`) and is the only thing `uv_filter` removes (`:343-350`); organic_waste is produced by livestock/plants and mineralized by acid/base; plant_matter is the degradation product of mushrooms |

**Neutral / valuable liquids — never classified, protected by default:** alcohol, caffeine, protein, fat, starch, sugar, coffee_mush, contraceptive, zyanide, dream_cap_mush/extract, nightmare_cap_mush/extract, chlorine, synaprest, fanta, methylol, reducing_agent, acid, base, stabilizing_agent, lipobond_agent, livestock_steroid, livestock_immunity, wine_extract, nomore. Also `water` itself is never removed. Full set in helper `DefaultProtectedLiquids` (§10.1) — this is a **mod-side constant**, easy to edit without touching game code.

Design notes:
- `mineral` is in Physical per the assignment, but it is also the hydroponic nutrient (nutrient solution is 75 % mineral, `WaterHelper.FillWithNutrientSolution` `:1269-1302`). Expose a `ProtectNutrients` toggle in the machine UI (pass `"mineral"` in `extraProtectedIds`) for players feeding hydroponics/aquaculture. Default: Physical includes mineral (drinking-water use case).
- `debris` and `plant_matter` are *byproducts* of chemistry/biology (never intentionally valuable). Removing them is the machine's core chemistry value (§7.2 mode B).
- `starch` is a filler/cut, not a “contaminant” class. For injecting/dust purity, optional “cut removal” could strip starch (it would raise active fraction), but keep it out of v1 to avoid destroying legitimate food/chem inputs.

### 4.2 Per-liquid removal matrix (what each class needs)

| Liquid | Suggested method | Vanilla evidence |
|---|---|---|
| physical_contaminant | mechanical filter / purifier (first) | pSize 100, filterMin 0.0016 |
| microplastic | filter (needs finer filter, pSize 25 vs water_filter 22 → caught only if filter size ≥ 25? logic below) | `HandleFilter` semantics §4.3 |
| debris | purifier / filter (pSize 200 = caught by every filter) | chemistry sediment |
| mineral | purifier (last) / NOT plants | REMOVED_LAST; plant filter excludes it |
| chemical_contaminant | purifier (last), purification tablet, chlorine side-reaction | REMOVED_LAST; tablet removes 10 000 µL |
| heavy_metal | purifier (last) — no filter/tablet route | REMOVED_LAST; premade distributions |
| microbe | UV, heat >70 °C, alcohol ≥40 %, chlorine, acid/base, purifier | `HandleMicrobeHeat` `ChemicalHelper.cs:1030-1055`; `HandleMicrobeAlcohol` `:1057-1081`; `HandleChlorine` `:673-710`; `HandleAcidBaseMicrobeKill` `:790-835` |
| organic_waste | purifier; heat >80 °C converts up to 15 % → physical_contaminant; acid/base mineralizes it | `HandleOrganicWasteHeat` `:1083-1113`; `HandleOrganicWasteMineralization` `:737-788` |
| plant_matter | purifier/filter (pSize 200) | mushroom degradation product |

### 4.3 Filter mechanics (why physical is a real class)

`WaterHelper.HandleFilter(identifier, amount, sourceContainer, filter)` (`WaterHelper.cs:333-395`):
- no filter → unchanged;
- `uv_filter` → microbe becomes 0, everything else unchanged (`:343-350`);
- ordinary `WATER_FILTER` with `FILTER_SIZE_TAG = 22`:
  - microbes pass (`:359-362`);
  - for a matching liquid: **if `filterSize > liquid.particleSize` the liquid passes** (`:369-372`) — so water (pSize 10) always passes; a filter of size 150 (kotton fabric) passes everything; filter 22 catches species with pSize ≥ 22 (`microplastic` 25, mineral 30, organic_waste 40, microbe 50 but microbe bypasses, physical 100, debris/plant_matter 200);
  - caught volume is reduced in `filterVolumeRemoved` (1 000-2 000 µL) chunks per durability point (`:377-389`), down to a residual floor = `amount × filterMinimumPurity` (`:375`);
  - hard cap of 10 000 loop iterations, then `Mathf.Clamp(amount, floor, original)`.
- Filters are applied only when pouring into a filtered container (`water_pitcher`, 3 000 ml) or filling it (`AddWater`). Items: `water_filter` (22, dura 400, value 25), `water_filter_adv` (22, dura 400, self-drains 50/cycle, value 350), `kotton_fabric` (150, dura 1000, value 65), `hydroreed_filter` (22, dura 200, plant-grown), `uv_filter` (special). `FILTER_SIZE_TAG` also const in `Liquid.cs:121`.
- Implication for the extractor: an exact transfer should **not** silently filter (it would break exactness); document that filtered pours remain the filter containers' job. If the mod wants filtering, do it explicitly and report the residual.

### 4.4 Chemistry interactions that create/destroy contaminants (why purification matters for products)

`ChemicalHelper.HandleTemperatureReaction` (`ChemicalHelper.cs:242-249`) runs 21 reactions every second on any non-empty container with `onOneSecondItemFunc` (all containers install it via `InitContainerTempFunction`, `:191-194`). Selected interactions:

| Reaction | Effect | Ref |
|---|---|---|
| `HandleDreamCapMushReaction` / `HandleNightmareCapMushReaction` | mushroom + solvent → extract (product) while consuming solvent | `:458-535` |
| `HandleDreamCapMushDegradation` / nightmare variant | mushroom + heat ⇒ `plant_matter` (waste) | `:317-360,628-671` |
| `HandleDreamCapExtractDegradation` / nightmare | extract + heat ⇒ destroyed volume | `:251-289,588-626` |
| `HandleNomoreReaction` | extract + reducing_agent + lipobond @45-55 °C ⇒ `nomore` (60 %) + **`debris`** (40 %) | `:536-586` |
| `HandleStabilizingAgentSedimentation` | stabilizing_agent + acid/base ⇒ **`debris`** | `:837-876` |
| `HandleProteinPrecipitation` | protein + acid/base ⇒ **`debris`** | `:878-921` |
| `HandleLipobondAgentSedimentation` | fat + lipobond ⇒ **`debris`** | `:923-956` |
| `HandleOrganicWasteHeat` | ≥80 °C: 15 % of organic_waste ⇒ physical_contaminant | `:1083-1113` |
| `HandleOrganicWasteMineralization` | acid/base converts organic_waste ⇒ **mineral** | `:737-788` |
| `HandleAcidBaseMicrobeKill` | acid/base kills microbes (consumes reagent) | `:790-835` |
| `HandleChlorine` | chlorine destroys microbes and chemical_contaminant (self-degrades) | `:673-710` |
| `HandleMicrobeHeat` | ≥70 °C kills 80 %; ≥100 °C all | `:1030-1055` |
| boiling | evaporative liquids above boiling point lose 2 %/s (min 100 µL) per liquid | `HandleBoilingReaction` `:291-315` |

The chemo-synthesis chain therefore inevitably builds up `debris`, `plant_matter`, and leftover reagents in the reaction vessel. Vanilla has **no** way to remove those without also destroying the actives (see §5.4/§8.3), which is exactly the niche for 提纯机器 in **concentrate mode**.

### 4.5 Water grade contaminant profiles (for tests/balance)

`WaterPremadeHelper.cs:6-144` distributions (fractions of the non-water volume; the rest of the free capacity is filled with water at grade purity 3500-9999):

| grade | organic_waste | microbe | physical | chemical | mineral | heavy_metal |
|---|---|---|---|---|---|---|
| ghostwater | 0.25 | 0.30 | 0.10 | 0.15 | 0.05 | 0.15 |
| water (default/rust fallback) | 0.20 | 0.15 | 0.10 | 0.30 | 0.15 | 0.10 |
| rustwater | 0.22 | 0.15 | 0.10 | 0.30 | 0.15 | 0.08 |
| gutterflow | 0.299 | 0.15 | 0.10 | 0.30 | 0.15 | 0.001 |
| basewater | 0.20 | 0.10 | 0.10 | 0.30 | 0.15 | 0.15 |

`AddWater(grade)` picks target purity range (`WaterHelper.cs:981-989`): grade 0 → 9990-9999 bp; 1 → 9900-9989; 2 → 9800-9899; 3 → 9600-9799; 4 → 9000-9599; 5 → 3500-8999. This gives cheap deterministic fixtures for testing (§9.4).

---

## 5. Vanilla purification semantics (ground truth for (c))

### 5.1 `MachinePurifier.PurifyContainer(machine, waterContainer, ignoreBonus=false)` — `MachinePurifier.cs:114-141`

1. Requires `LIQUID_CONTAINER_TAG`, otherwise returns 0.
2. `perf = MachineryHelper.GetCurrentPerformanceBonus(machine)`, `quality = MachineryHelper.GetCurrentQualityBonus(machine)` (`MachineryHelper.cs:532-540`; tag values `TOTAL_PERCENTAGE_*_BONUS_INT`); `ignoreBonus` zeroes both.
3. `dirtBonus = GetDirtBonus(purityBP) + perf/100`, clamped ≥ 0, × 1.25.
4. `budget = (min(total,10 000 000) × 3 / 1000 + 25 000) × dirtBonus` (µL).
5. `removed = RemoveLargestFirst(container, GetMaxPurityCap(quality), budget)`.
6. `WaterFeatureHelper.UpdateWaterFeature(container, isExpose:false)`.
7. Returns µL removed (the machine stores it in `TOTAL_CONTAMINANT_REMOVED_TAG`, `:79-101`; tooltip divides by 1000 → ml, `:285-298`).

Guards/rules of `RemoveLargestFirst` (`:217-283`):
- if total volume ≤ 0 or `water ≤ 0` → **0** (a container with no water cannot be purified);
- `allowedResidualNonWater = total × (10000 − cap) / 10000`;
- `removable = min(budget, total − water − allowedResidualNonWater)`; if ≤ 0 → 0 (already at/above cap).
- Iterates non-water liquids with `canBeRemovedByPurifier` (i.e. everything except water; **all 35 other liquids including products**), sorted by a key `(REMOVED_LAST ? 1 000 000 : 0) − particleSize`:
  - normal species sorted by **descending particle size** (debris/plant_matter/mushrooms 200 first, physical 100, microbe 50, organic 40 … down to actives pSize 5);
  - then the three `REMOVED_LAST` species (mineral, chemical_contaminant, heavy_metal) last;
- removes proportionally inside each particle-size group, converts **all removed volume to water** (`water += removed`, `:278-281`), so total volume is preserved and purity rises to the cap.

### 5.2 Dirt bonus curve `GetDirtBonus(purity)` — `MachinePurifier.cs:193-210`, table at `:18-26`

| purity bp | dirtscore | note |
|---|---|---|
| ≥ 9800 | 1.0 | clean water still gets modest budget |
| 9400 | 2.0 | |
| 8850 | 5.0 | |
| 8000 | 10.0 | |
| 6250 | 15.0 | |
| ≤ 3750 | 20.0 | worst water gets 20× budget |

### 5.3 Cap curve `GetPurityMult(quality)` / `GetMaxPurityCap(quality)` — `MachinePurifier.cs:164-215`

| quality bonus q | `GetPurityMult` | `GetMaxPurityCap` (bp) | display tier |
|---|---|---|---|
| ≥ 200 | 0.0 | **10 000** | 100 % |
| ≥ 100 | 0.1 | 9 990 | pure (99.9 %+) |
| ≥ 75 | 0.2 | 9 980 | high quality |
| ≥ 50 | 0.35 | 9 965 | high quality |
| ≥ 25 | 0.65 | 9 935 | high quality |
| ≥ 15 | 0.85 | 9 915 | high quality |
| < 15 | 1.0 | **9 900** | high quality (99 %+) |

So an unmodded vanilla purifier can only reach 99.00 % purity; 100 % requires quality bonus ≥ 200 (quality modules). `BASE_MAX_PURITY = 9900` constant at `:16` encodes this. Machine quality ratio: `MACHINE_QUALITY_RATIO_INT = 2` (`:53-56`). Power draw 4 (`MachineryHelper.InitMachinery(machine, 4)`, `:52`).

### 5.4 Why the vanilla purifier is unsafe for product liquids

- It removes **any** non-water liquid with `canBeRemovedByPurifier` (all of them) once bigger particles are gone; `REMOVED_LAST` protects only mineral/chemical_contaminant/heavy_metal — i.e. **not** the actives.
- Budget on dirty liquid is huge (dirt score up to 20 × 1.25 = 25×), caps allow near-total removal, and conversion to water keeps total constant, so running it on an extract-rich vessel will eat `dream_cap_extract`, `nomore`, etc. and replace them with water.
- Measured against the *chemical product* purity metric, convert-to-water does **not** improve active fraction (total constant, active constant) — it can only destroy actives. So using the vanilla purifier on a drug intermediate is strictly bad.
- Other vanilla “purifiers”: portable water purifier (`PurifyToBaseWater`) destroys all non-water above floors; purification tablets are microbe/chemical only; UV only microbes; filters mostly physical. None can clean chemistry waste from a product liquid.

This is the defensible niche of 提纯机器: **category-scoped, product-protecting purification**, with two mutually exclusive operating modes (§7).

### 5.5 Reading `TOTAL_CONTAMINANT_REMOVED_TAG`

Vanilla purifier stores removed µL per cycle (`MachinePurifier.cs:6,79-101`) and shows it as ml in the tooltip (`:289-296`). The mod machine should keep the same tag name for HUD parity, and may add a second tag for category/or history (e.g. `MOD_PURIFIER_LAST_CYCLE_TAG`); do not rename vanilla tags.

---

## 6. Design — 精确取液器 (Precision Liquid Extractor)

### 6.1 Semantics (decision-ready)

1. **Exact or nothing.** If the requested N µL cannot be delivered (capacity/volume/liquid availability), perform **zero** mutation and return 0 with a reason for the UI. Rationale: dosing must be reproducible; a partial transfer on a chem batch silently breaks the recipe.
2. **Resolution 1 µL**, range 1 … free capacity (up to int.MaxValue µL).
3. **Modes**:
   - `Proportional` (default): sample the mixture exactly like pouring — allocate N across species with `WaterHelper.SplitProportionally` (integer-exact, largest-remainder rounding, `WaterHelper.cs:206-255`). This is the only mode that guarantees “the aliquot has exactly the source composition”.
   - `WaterOnly`: move only `water`, N ≤ water volume.
   - `SingleLiquid`: move only one chosen `Liquid` id, N ≤ that species' volume (allows e.g. extracting pure alcohol from a mixed vessel).
4. **Both items must be `LIQUID_CONTAINER_TAG`, not `WINE_CONTAINER_TAG`** (wine has a separate fermentation/quality simulation, `WineHelper.cs:274-309`; water-feature functions early-return for wine, `WaterFeatureHelper.cs:96-99`).
5. **Ownership**: both must be `GeneralHelper.IsItemOwned`/`IsItemEffectivelyOwned` (`GeneralHelper.cs:176-216`) and the tool owned. Vanilla drag handlers gate on `WaterHelper.CanUseWaterContainer` (`:657-664`) because otherwise players can drain unowned store/world containers.
6. Side effects must equal a normal pour: temp mixing, water-feature refresh on both sides, sprite/sound/UI. Preserve the target's label semantics by default (vanilla pour rules) with an opt-in “certified” relabel.
7. Filters are **not** applied by an exact transfer (they would break exactness); use filter containers for filtered pours.

### 6.2 Recommended public API (`LiquidApiHelper`, §10.1)

```csharp
bool CanTransferExact(GameItem source, GameItem target, int volumeUL,
                      TransferMode mode, string singleLiquidId, out string error);
int  GetTransferableUL(GameItem source, GameItem target, int requestedUL,
                       TransferMode mode = TransferMode.Proportional, string singleLiquidId = null);
int  TransferExact(GameItem source, GameItem target, int volumeUL,
                   TransferMode mode = TransferMode.Proportional, string singleLiquidId = null,
                   WaterLabelMode targetLabelMode = WaterLabelMode.PreserveLabel);
```

`TransferExact` returns the µL actually moved (`== volumeUL` on success, `0` on refusal) so the tool's `onItemTargetItemFunc` can play dry/empty audio and show a toast.

Implementation order (inside `TransferExact`, mirroring `WaterHelper.TransferLiquid` `:267-316`):
1. `CanTransferExact` validation (container tags, wine rejected, N>0, free capacity, mode-specific volume).
2. `wasEmpty = WaterHelper.IsEmpty(target)`.
3. Build per-species delta array:
   - proportional → `WaterHelper.SplitProportionally(WaterHelper.GetLiquidParts(source), N)` (sum is guaranteed N);
   - water/single → one slot = N.
4. Write tags with `skipValidate: true` on both containers (`CURRENT_PART_*`; use `state.GetInt() ± amount` inside the lambda).
5. `ChemicalHelper.HandleReceiveLiquidTemp(target, N, source)` (mixes °C; one call for the whole aliquot, `:1139-1143`).
6. `source.ValidateShapeState(); target.ValidateShapeState();` (recompute modified state + UI).
7. `WaterFeatureHelper.OnLiquidContainerPourInto(target, source, wasEmpty)` then `WaterFeatureHelper.OnLiquidContainerPourOutOf(source)`.
8. Optional `WaterLabelMode.RelabelAccurate` → `WaterFeatureHelper.InitWaterFeature(target, isExpose:false)` (certified label; only if the tool is meant to measure/certify; default off to preserve the information economy — see §8.4).

### 6.3 Pitfalls checklist (all verified in code)

1. **Do not use `WaterHelper.AddLiquid`** to add the aliquot: per-liquid capacity clamp can overfill (`:904-909`). Write tags directly after the explicit free-space check (§10.1 does).
2. **Do not use `WaterHelper.TransferLiquid`**: it moves `min(free, sourceTotal)` — cannot restrict volume or composition (`:294`).
3. **Do not mutate one side and then fail on the other**: validate *everything* first (`CanTransferExact`), then write. Vanilla has no transaction; the helper is atomic by construction.
4. **`SplitProportionally` returns all positive parts when `amount >= sum`** (`:222-229`); the helper pre-checks so N is always < sum for proportional mode, and verifies `sum(delta)==N` before writing (`return 0` on mismatch).
5. **Int overflow**: never sum part volumes in `int`; use `long`. Free capacity can be up to ~2.1e9 µL (int max) — `GetFreeVolumeUL` clamps to `int.MaxValue`.
6. **`GetCurrentCapacity` div-by-zero** if capacity tag missing; don't call it on mod-created containers before setting capacity.
7. **Water feature refresh is mandatory**: skipping it leaves wrong sprite/label/price; `OnLiquidContainerPourInto` copies the source's *presumed* purity when target was empty (vanilla scam semantics). If the tool UI promises accuracy, use `RelabelAccurate`.
8. **`STRIP_TESTED_TAG`** is cleared by any `UpdateWaterFeature(..., false)`; clients that refuse “labelled hazardous water” (`purify < 9800` label) will then re-evaluate. Intended behavior after mixing — don't restore the tag manually.
9. **Ownership**: gate on `IsItemEffectivelyOwned`; otherwise a tool can drain items inside locked/unowned parent containers (`GeneralHelper.cs:202-216`).
10. **Wine containers**: reject (`WINE_CONTAINER_TAG`); mixing plain water into wine breaks `WineHelper` state machine and quality display.
11. **Temperature**: extractor must call `HandleReceiveLiquidTemp` once with the total N; the receiver's `CONTAINER_TEMP_TAG` exists on all WaterHelper containers (`ChemicalHelper.InitContainerTemp` called in `InitLiquidContainerItem`, `WaterHelper.cs:166`). Source temperature is not reduced (vanilla behaves the same; volume is a temperature sink in reality but the model ignores it).
12. **Emptying a source**: when N == source total, the last part may reach 0; `WaterFeatureHelper.OnLiquidContainerPourOutOf` already handles `IsEmpty` (hides feature, disables strip tag) — no extra handling needed.
13. **UI lists**: if the tool has a container-picker list, filter with `item.IsTag("LIQUID_CONTAINER_TAG") && !item.IsTag("WINE_CONTAINER_TAG") && GeneralHelper.IsItemOwned(item)`; recompute the list when slots change.
14. **Audio/feedback**: `AudioHelper.DropperDraw/DropperDrop` for a dropper-like tool, `PourWater` otherwise (`WaterHelper.cs:112-139` shows vanilla choices).
15. **Machines/containers in slots**: don't transfer from a container that is currently inside another machine's `GameSlotInventory` while that machine may run (`onCycleEndSlotItemFunc`); safest is to require both containers in the player's regular inventory/window slots.

### 6.4 Interaction wiring (two viable shapes)

**A. Item-on-item tool (cheapest, similar to water purification tablet / UV filter).** Register in the item factory:

```csharp
tool.maySelectSlotItemFunc = (GameItem s, GameInventory _, SlotMarker _) => GeneralHelper.IsItemOwned(s);
tool.canSelectSlotItemFunc  = (GameItem s, GameInventory _, SlotMarker _) => GeneralHelper.IsItemOwned(s);
tool.mayThisTargetItemFunc  = (GameItem s, GameItem t) => LiquidApiHelper.IsLiquidContainer(t) && !LiquidApiHelper.IsWineContainer(t) && GeneralHelper.IsItemOwned(s, t);
tool.canThisTargetItemFunc  = tool.mayThisTargetItemFunc;
tool.onThisTargetItemFunc   = (GameItem s, GameItem t) => { /* open amount dialog; then */ LiquidApiHelper.TransferExact(t, otherContainer, amountUL); };
```

**B. Machine with two container slots + amount UI** (matches ChemFinisher/Blender patterns): `GameSlotInventory` source/target with `mayInventoryAddItemFunc = item => GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG")`; a `MachineryRangeSlider`/`MachineryIntake` for µL; a `MachineryButton` to extract. Machine API details are machine-api's deliverable — this doc supplies only the liquid semantics + validated helper calls.

For a *tool* UX, the amount can default to the last-used value and clamp to `GetTransferableUL`; offer quick chips (1 µL, 10 µL, 100 µL, 1 ml, 5 ml, 10 ml, max) and a ml/µL toggle. Store the last value in a `static` field (not on the item) to avoid save-format churn.

---

## 7. Design — 提纯机器 (Purification Machine)

### 7.1 Goals / non-goals

Goals:
- remove selected contaminant **classes** from a liquid container;
- never remove protected liquids (products/reagents) unless the player explicitly enables an “aggressive” mode (not in v1);
- support two economically meaningful outputs: **clean water** (drink/sell/plants/RAS) and **concentrated product** (chemistry/drugs);
- respect vanilla power/cycle/module economy and UI patterns;
- be predictable: show before/after purity and exactly what will be removed.

Non-goals: filtering particulates only (filters already do that), replacing evaporation/boiling (water removal), processing `CHEM_ITEM`s (extension in §7.9), wine.

### 7.2 Two modes (the central design decision)

| | **Mode A — Water purification** | **Mode B — Product concentration** |
|---|---|---|
| Topology | container → container (same item, in-place) | container → container (same item, in-place) |
| Removed classes | Physical / Biological / Chemical (any subset selectable) | same, typically Physical+Biological+Chemical |
| Removed volume | **converted to water** (`water += removed`) — vanilla parity, mass-preserving | **deleted** (discarded as sludge; total volume decreases) |
| Purity metric | raw water purity bp (water fraction) | active/target fraction (concentrate measure); also raw bp rises |
| Cap semantics | stop at `purityCapBP` (bp) or when no selected contaminant remains | stop when remaining volume reaches `activeVolume / cap`; if cap unreachable, remove all selected contaminants |
| Water requirement | require water > 0 (vanilla guard) | **not required** (dry extract vessels work) |
| Budget | vanilla purifier formula (§5.1) | × 0.5 of the same formula |
| Power draw | 4 | 6 |
| Main use | drinking water, water selling, plants/aquaculture, boiler feed | pre-ChemFinisher cleanup, raising `CHEMICAL_PRODUCT_PURITY_TAG`, removing `debris`/`plant_matter` after synthesis |
| Side effect on recipes | adding water may make a vessel “wet” and break `ChemFinisherRecipe.isRequireDry` (`WaterHelper.IsWet` > 100 µL evaporatives) | none — no water added |

Why deletion is the *only* useful mode for chemistry: `ChemFinisher` transfers exactly `requiredVolume` µL of the container mixture and grades the product as active fraction × (1−loss) (§3.2, §8.3). Convert-to-water keeps both total and active volumes constant → grade unchanged. Deletion of non-active waste raises active fraction → grade rises. Deletion also **reduces** total volume; the machine UI must warn if the vessel is close to a recipe's `requiredVolume` (100-6 000 µL).

Recommended defaults: Mode A selected by default; Mode B requires toggling a visible “concentrate (discard sludge)” switch; both modes share the class checkboxes and “protect nutrients” option.

### 7.3 Removal accounting: convert vs delete (recommendation)

- Keep **both** modes; they are different products (water/laundry vs chemistry). The existing vanilla purifier already covers Mode A approximately, but at higher caps; the mod machine's Mode A differentiates by falling back to class-agnostic caps only when quality modules are installed, and by being safer (products protected).
- Convert-to-water is vanilla-consistent and avoids free-mass questions: the removed contaminants are “cleaned into water”, exactly what `RemoveLargestFirst` (`:278-281`), the purification tablet, UV treatment, and the portable purifier do. Do **not** invent free mass beyond the containers' capacity (total unchanged in Mode A, so no exploit).
- Deletion in Mode B destroys mass (mostly waste). If the mod later wants realism, add a “sludge” byproduct item; not needed for v1. Do not convert to water in Mode B — that silently defeats the chemistry use case.
- Never move removed volume into another container automatically (would surprise and could overfill).

### 7.4 Recommended caps and budget numbers

**Mode A purity cap** = `min(9800 + qualityBonus, MachinePurifier.GetMaxPurityCap(qualityBonus), 10000)`:

| quality bonus q | cap bp | display tier reached | comment |
|---|---|---|---|
| 0 (no module) | 9 800 | base water | intentionally **below** vanilla purifier's 9 900 — compensates for product protection |
| 15 | 9 815 | base water | |
| 50 | 9 850 | base water | |
| 75 | 9 875 | base water | |
| 100 | 9 900 | high-quality | matches vanilla unmodded purifier |
| 150 | 9 950 | high-quality | |
| 190 | 9 990 | pure (99.9 %+) | requires strong module investment |
| 200 | 10 000 | 100 % | end-game |

**Mode B grade target** is a parameter (recommend UI presets 6000/8000/9000/9900 “street/near-pharma/pharma/lab”) used by `ConcentrateChemicalProduct`-style math; for containers the helper's `convertToWater:false` path deletes selected contaminants until the raw-purity cap equivalent is reached, which for product-rich vessels degenerates to “remove all selected contaminants” (desired).

**Budget** (`ComputeCycleBudgetUL` in §10.1, identical math to vanilla): `budget = (min(total,10 000 000) × 3‰ + 25 000) × (GetDirtBonus(purity) + performance/100, clamped ≥0) × 1.25`. Mode A = budget as-is; Mode B = budget / 2.

Worked numbers (Mode A, no performance modules):
- 750 ml bottle, gutterflow 3500 bp: dirt score 20 → ×1.25 = 25; budget = (750 000×3/1000 + 25 000)×25 = **681 250 µL**. To reach 9800 bp it must remove `nonWater − 2 % total` = 487 500 − 15 000 = 472 500 µL → **1 cycle** (budget > need).
- 750 ml base water 9800 bp already at cap → 0 µL removed.
- 6 L jug base water with 5 % contaminants: budget = (6 000 000×3/1000 + 25 000)×1.25 = 53 750 µL/cycle; to reach 9800 needs removing 300 000−120 000 = 180 000 µL → **4 cycles** (or fewer with performance modules: each +100 % performance doubles budget).
- 2 000 ml bottle rustwater 9000 bp: dirt score ≈ 10.66 (interpolated between 8850→5 and 9800→1: at 9000, (9800−9000)/(9800−8850)=800/950=0.842 → 1+0.842×4=4.37) → budget=(25000+6000)×4.37×1.25≈169 000 µL; need ≈ 200 000×0.02=4 000 µL to reach 9800? Wait rustwater at 9000 bp → non-water 10 % = 200 000 µL; cap 9800 → allowed residual 2 % = 40 000 → remove 160 000 µL ≈ 1 cycle.

Reality check against vanilla purifier: mode A numbers are in the same order of magnitude (same formula, same cap curve at q ≥ 100, −100 bp at q = 0). The mod machine's advantage is class selection + product protection; its cost is machine price + modules + (optionally) one extra power.

**Mode B example (chemistry)**: 100 ml cylinder at 100 000 µL with `dream_cap_extract` 40 000, `plant_matter` 50 000, `debris` 10 000, no water. `requireWater` must be false (helper parameter). `ComputeRemovalLimit` with cap 9900, convert=false, W=0 → reachable purity 0 ⇒ removes **all** selected candidates = 60 000 µL (budget permitting; budget mode B = 0.5 × ([100 000×3/1000+25 000] × dirtBonus(purity 0→20)×1.25 = 0.5×68 750 = 34 375 µL) → needs 2 cycles). Result: 40 000 µL of extract, 100 % active fraction → ChemFinisher (dry requirement met: no evaporatives) can now yield pharma/lab grade instead of cut product. This is the machine's headline use case.

### 7.5 Machine wiring (vanilla patterns to copy)

Reference implementation `MachinePurifier.WaterPurifier()` (`MachinePurifier.cs:30-112`) and `PurifierMachineSkeleton` in §10.2:

```csharp
// machine creation
machine.SetGameItemType("MACHINE");
machine.EnableTag("CONTAINER_TAG");
machine.EnableTag("manufacture");
machine.EnableTag("STANDARD_MACHINE_TAG");
MachineHelper.SetupBatterySlot(powerInv, machine);          // adds MACHINE_RECHARGABLE_TAG
MachineryHelper.InitMachinery(machine, 4);                  // drawnPower 4 (6 for concentrate-capable)
machine.ModifyTag("MACHINE_QUALITY_RATIO_INT", st => st.Enable().SetInt(2));
ContainerHelper.AllowOnlyTaggedItemsOr(moduleInv, new[] { "MODULE_TYPE_UNIVERSAL" }, ownedOnly: true);
containerSlot.mayInventoryAddItemFunc = (item, _) => GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
// per cycle
machine.onCycleEndSlotItemFunc = (item, inv, slot) => { int removed = PurifierMachineSkeleton.RunPurifyCycle(...); ... };
```

Per-cycle requirements: powered battery slot (`MachineHelper.TryDrawCyclePower` draws `GetMachinePowerUsage(machine)` = `machinery_drawn_power`), non-empty container, then `MachineHelper.OnMachineActioned(machine, moduleInv)` to trigger module effects (vanilla purifier does this, `MachinePurifier.cs:98`). Persist the removed total in `TOTAL_CONTAMINANT_REMOVED_TAG` exactly like vanilla; show `removed/1000 ml` in the tooltip.

Also mirror vanilla guards:
- don't act when container empty (`WaterHelper.IsEmpty`);
- don't act if nothing removable (`PurifyByCategory` returns 0);
- don't consume power when result is 0 (check removal before drawing, or refund — vanilla checks removal then draws, `MachinePurifier.cs:88-99`).

### 7.6 UI / tooltip (decision-ready)

Show in the machine window and/or container tooltip (`RichTextBuilder` patterns):
1. `Purity: 91.24 % (92.1 % after 1 cycle)` — raw bp/100.
2. Per-class volumes: `Physical 12.4 ml · Chemical 3.1 ml · Biological 0.8 ml` (use `GetContaminantVolumeUL`/`GetRemainingContaminantsUL`).
3. Protected liquids that will **not** be touched: list species with volume (e.g. `dream_cap_extract 40 ml (protected)`).
4. Mode and cap: `Mode: water (cap 98.00 %)` or `Mode: concentrate (target ≥ 60 %)`.
5. `WaterHelper.SetLastScanResult(container)` after a cycle so the standard tooltip shows a “Last Scan” line (vanilla scanner semantics, does not falsify the label).
6. Post-cycle audio + `MachineHelper.OnMachineActioned`; optional toast when the container becomes “dry”/“empty”.

### 7.7 Edge cases

| Case | Recommended behavior |
|---|---|
| Container is `WINE_CONTAINER_TAG` | refuse (helper returns 0) — wine quality is separate (`WineHelper`) |
| No water (dry chemical vessel) | Mode B allowed (`requireWater:false`); Mode A returns 0 |
| No removable contaminants | return 0, no power consumed |
| All volume is protected product | `GetRemainingContaminantsUL == 0` → 0 removed; never eat product |
| Cap unreachable (product-rich) | remove all selected contaminants only; document “cap unreachable” in UI |
| Raw purity already ≥ cap | 0 removal (no waste of budget) |
| Container in another machine slot | refuse/disable UI while slotted elsewhere (avoid double-machine mutation) |
| Very large tank (20-50 L water tank) | budget caps at `10 000 000` µL base volume per cycle — many cycles; that's intended pacing |
| Corrupt/over-capacity save | `GetFreeVolumeUL` clamps; Mode A preserves total so no overfill |
| Modded liquid unknown to `Liquid.LiquidDict` | ignored (not classified, not removable) — safe default |

### 7.8 Balance justification (short version; numbers in §8)

- Water Mode A: purifying gutterflow→base raises sale value from ≈39 → ≈255 credits for a 6 L jug (6.5×); reaching pure (>495 credits) needs quality modules ≈ 190 bonus, i.e. an end-game investment. Vanilla purifier cannot do product-safe chemistry; the mod machine's lower base cap (9800 vs 9900) is the price of safety.
- Mode B: a cartridge of drug intermediate that grades `cut_product` (−45 % value) can be concentrated into `pharma_grade` (+85 %) or better — a 2.4× value step on the finished product — and cartel weekly quotas require average product purity 40 → 70 % (§8.3). That is the machine's economic engine; it is gated by power (6/cycle), budget halving, machine price, and the fact it cannot create active ingredient (only remove waste).
- Both modes require power + cycles; both avoid vanilla exploits (no free mass, no label falsification in default mode).

---

## 8. How purity / contamination affects value and selling (answers (d))

### 8.1 Water pricing formula (volume-only base, purity multiplier via feature)

`unitValue = (int)Math.Round(40.0 * totalLiquidParts / 1_000_000.0, MidpointRounding.AwayFromZero)` — `WaterFeatureHelper.cs:369-377`; 40 credits = `BASE_WATER_PRICE_PER_LITER` (`:51`). Per-tier price constants (`WATER_PRICES = {200,100,40,27,20,10}`, `:59`) and `WATER_VALUE_DISPLAY` (`:45`) exist but are **not** used by the price computation — the tier effect enters only through the feature condition modValue:

| tier | purity ≥ | `modValue` | effective multiplier on `unitValue` | source |
|---|---|---|---|---|
| pure | 9 990 | **+100** | ×2.00 | `ItemConditionList.cs:325-335` |
| high quality | 9 900 | +50 | ×1.50 | `:337-347` |
| base | 9 800 | 0 | ×1.00 | `:349-359` |
| ghostwater | 9 600 | −33 | ×0.67 | `:361-371` |
| rustwater | 9 000 | −75 | ×0.25 | `:373-383` |
| gutterflow | 3 500 | −90 | ×0.10 | `:385-395` |
| below 3500 | – | −90 (index 5) | ×0.10 | `GetPurityArrayIndex` fallback `WaterFeatureHelper.cs:451-461` |

Full item value composition (`GameItem.GetCurrentValue`, `:1721-1764`): `final = round(((unitValue + childValue) × (1 + innateMult/100) + containerBasePrice) × (1 + marketMult/100) × finalFactor) + misc`, where the water feature is an **Innate** stage feature (`ItemFeature.cs:116-130`; `valueStage` defaults to `Innate`), `innateMult` accumulates its `GetCurrentValueMod()` = `fakeCondition.modValue` when unexposed, `realCondition.modValue` when exposed (`ItemFeature.cs:380-399`). `WaterHelper.GetContainerPrice` is added as `marketScaledValue` after the innate scaling (`GameItem.cs:1699,1749`), i.e. the jar itself is not purity-scaled.

Worked values (market modifiers 0, no child items, `unitValue` from formula):

| container | capacity | unitValue at full | base | HQ | pure | rust | gutter |
|---|---|---|---|---|---|---|---|
| `bottled_water` | 750 ml | 30 | 35 | 50 | 65 | ~13 | ~8 |
| `water_jug` | 6 L | 240 | 255 | 375 | 495 | ~75 | ~39 |
| `water_tank` | 20 L | 800 | 1 150 | 1 550 | 1 950 | ~550 | ~430 |

(Container base price: bottle 5, jug 15, tank 350 — `FoodItemDirectory.cs:169,318`; `WaterTankHelper.cs:83`.)

### 8.2 Client acceptance gating (the real economic multiplier)

Water buyers accept only specific display tiers (uses `GetCurrentWaterDisplay`, i.e. the label — fake condition when untested). `StoreClient.willAcceptItem` logic in `StoreClientListTier.cs`:

| client | budget | accepts |
|---|---|---|
| Water Scavenger `:47-101` | 60-100 | base **or** ghostwater (`:82`) |
| Thirsty Spacer `:104-157` | 90-110 | base / HQ / ghost (`:137`) |
| Hydration Seeker `:159-209` | 120-150 | base / HQ / **pure** (`:190`) |
| Water Shopper `:212-266` | 145-195 + day/4 | base / HQ (`:247`) |
| Water Stockpiler `:269-327` | **500-600 + day** | **pure / HQ** (`:306`) |

`WATER_RATION` item type is accepted by most water clients regardless of purity (`StoreClientListTier.cs:69,124`, `StoreClientList.cs:2844`), and water in a ration isn't a container. So purity gates both *which* clients and *how rich* they are: gutterflow sells to scavengers for a few credits; pure water unlocks the 500-credit stockpiler. Also: clients with `refusesHazardousWaterLabel` reject containers labelled < 9800 bp (`StoreClient.cs:384`; `WaterFeatureHelper.IsLabelledHazardousWater` `:329-341`), and luxury-water buyers exist (`IsRefusingLuxuryItem`, `StoreClient.cs:405-424`).

Item type flip: a container with a base-price tag switches between `WATER` and `HOUSEHOLD_GOOD` based on emptiness (`WaterHelper.UpdateContainerGoodType` `:586-607`) — this affects which clients look at it, so always refresh after mutation.

### 8.3 Chemical-product value (drug chain, the Mode B engine)

`ChemicalProductHelper.GradeProduct` computes `purityRatio = active / total × 100` and sets condition (`:103-145`, thresholds at `ChemicalProductHelper.cs:9-21`):

| grade | active fraction | modValue (`ItemConditionList.cs:682-757`) | value multiplier | client acceptance (`WillAcceptNarcotic`, `:216-236`) |
|---|---|---|---|---|
| lab_pure | ≥ 99 % | **+135** | ×2.35 | accepted by all narcotic clients |
| pharma_grade | ≥ 90 % | +85 | ×1.85 | all |
| near_pharma | ≥ 80 % | +40 | ×1.40 | all |
| street_standard | ≥ 60 % | 0 | ×1.00 | grade ≥ 2 clients ok |
| cut_product | ≥ 45 % | −45 | ×0.55 | rejected for `acceptedChemicalProductGrade ≥ 2` |
| junk_grade | ≥ 30 % | −75 | ×0.25 | rejected by all narcotic clients |
| starch_grade | < 30 % | −95 | ×0.05 | rejected |

- Client refusal also uses `StoreClient.IsClientRefusingItem` (`StoreClient.cs:354-403`) with per-client `acceptedChemicalProductGrade`.
- **Cartel weekly quota** requires average product purity (exactly `CHEMICAL_PRODUCT_PURITY_TAG`) ≥ `PlayerStore.cartelRequiredPurity` (starts **40**, +5/week, cap **70**; `PlayerStore.cs:439`; `StoreClientListCartel.cs:9-11,273-278,387-395,334-380`). Missing the purity target costs cartel reputation (or worse). Reaching 70 % average requires input fractions ~70 %+ and low-loss ChemFinisher modules (loss 12/6/3/1/0 % by upgrade count, `MachineChemFinisher.cs:109-119`; `IntroduceImpurity` adds `physical_contaminant` equal to the loss %, `:85-91`).
- Sample item values (unitValue): `dream_dust` 18 (`MedsItemDirectory.cs:197`), `nightmare_dust` 36 (`:214`), `pink_alt_injector` 50 (`:439`), `fanta_pill` 12 (`:114`), `zyanide_pill` 33 (`:97`), `caffeine_pill` 10 (`:231`), `birth_control_pill` 15 (`:58`). Grade multipliers apply on top via the Innate feature.
- Premade injector compositions are 45-60 % active (`SetNomoreInjectorPurity`, `MedsItemDirectory.cs:354-359`), i.e. `cut_product` — an obvious “improve with the mod machine” target, if the mod later extends to CHEM items (§7.9).

### 8.4 Information economy: labels vs reality (why refresh mode matters)

- The label (`fakeCondition`, `ItemFeature.customIntValue1`) is what clients see when the container is untested (`WaterFeatureHelper.GetCurrentWaterDisplay` `:315-327`); the actual purity (`realCondition`) needs a test strip (`WaterTestHelper.HandleWaterCheckTestingStrip` `:11-22`; `WaterFeatureHelper.HandleWaterCheckTestingStrip` `:426-439`) or AquaScan tool.
- Vanilla purifier deliberately keeps labels stale (`UpdateWaterFeature(..., isExpose:false)`): purified-but-untested water still sells at its old tier, and old “hazardous” labels still block picky clients. This preserves the value of test strips (a sold commodity) and the game's scam mechanics (`UpdateWaterFeatureFake`, `SetPresumedPurity`).
- Our default should match vanilla: **PreserveLabel + `SetLastScanResult`** (the tooltip honestly shows the machine's last reading without certifying the label). Offer a config/UI “certify output” toggle calling `InitWaterFeature` (RelabelAccurate) for players who want immediate correct pricing; this is a quality-of-life buff that should be balanced by machine price/power, not free.
- Never set `customIntValue1` manually; always go through `InitWaterFeature`/`UpdateWaterFeature`/`SetPresumedPurity` so `fakeCondition`, `realCondition`, `isFeatureMatch`, displays and price stay consistent.

### 8.5 Economic justification summary for balance choices

- Extraction tool: enables reproducible dosing for chem recipes whose inputs are 100-6 000 µL (ChemFinisher) and for calibrated containers; value = saving failed batches (a `dream_dust` batch is 2 000 µL of often-expensive extract; wrong dose wastes input + power). Price it as a precision instrument (100-200 credits) with a small power/pump cost or durability; don't make it free-scaling (it can also be used to fractionate valuable mixtures → gate by ownership, cycle time, and maybe a consumable tip).
- Purifier Mode A: matches vanilla purifier value-per-litre only after module investment (cap 9800 vs 9900 baseline); its edge is product safety + class choice + dry-vessel/nutrient protection. Good mid/late game purchase (300-500 credits + modules).
- Purifier Mode B: premium capability (2.4×+ product value, cartel progression). Balance with 6 power, half budget, and no active ingredient creation. Consider making Mode B require a chemistry module (`MODULE_TYPE_CHEM`) so it is not available on day 1.

---

## 9. Containers, `LIQUID_CONTAINER_TAG`, capacities, test setup (answers (e))

### 9.1 What `LIQUID_CONTAINER_TAG` means and everything `InitLiquidContainerItem` does

`WaterHelper.InitLiquidContainerItem(item, capacityInMl, basePrice, isFilterContainer=false, isGraduated=false, isDropper=false, isTransparentContainer=false, sheetPage=3)` (`WaterHelper.cs:51-204`):

1. enables `LIQUID_CONTAINER_TAG`;
2. sets `LIQUID_CONTAINER_BASE_PRICE_TAG = basePrice` (container value added unscaled to item value);
3. optional `LIQUID_CONTAINER_DROPPER_TAG` (dropper transfers + special sprites/audio);
4. `ChemicalFeedbackHelper.InitChemicalIntentItem(item, HYDRATION)` (chemical intent tag used by feedback/appraisal);
5. optional `FILTER_CONTAINER_TAG` (makes it a filter host — pitcher);
6. `ChemicalHelper.InitContainerTemp(item)` → `CONTAINER_TEMP_TAG = 24`;
7. clamps capacity (ml) to 2 147 483 and stores `capacity × 1000` µL in `LIQUID_CONTAINER_CAPACITY`;
8. optional `GRADUATED_CONTAINER_TAG` (shows exact ml in tooltip; else “almost empty/half full” wording);
9. initializes **every** `CURRENT_PART_*` tag to 0 (all 36);
10. `UpdateContainerGoodType`, drag/target interaction closures (pour, dropper, drain, faucet);
11. `ChemicalHelper.InitContainerTempFunction(item)` → per-second temperature/reaction tick;
12. `WaterFeatureHelper.InitWaterFeature(item)`;
13. sprite modifier for dropper/transparent containers; bottle sound.

Consequences: **never create a liquid container by only enabling the tag** — without capacity/all-part tags/feature it will throw or misbehave (e.g. `GetCurrentCapacity` div-by-zero, `GetWaterFeature` creation on read, tooltip NREs through `GetTagReadonly` are actually safe but prices/sprite are wrong).

### 9.2 Capacity examples (ml → µL)

| item | capacity ml | µL | evidence |
|---|---|---|---|
| `1ml_dropper` | 1 | **1 000** | `FoodItemDirectory.cs:258` |
| `5ml_dropper` | 5 | **5 000** | `:268` |
| `25ml_cylinder` | 25 | **25 000** | `:278` |
| `50ml_cylinder` | 50 | **50 000** | `:288` |
| `100ml_cylinder` | 100 | **100 000** | `:298` |
| `med_bottle_small_pink` | 150 | 150 000 | `:247` |
| `mini_bottle`, `med_bottle_*` | 250 | 250 000 | `:189,201,213,224,236` |
| `small_bottled_water` | 450 | 450 000 | `:179` |
| `bottled_water` | 750 | 750 000 | `:169` |
| `large_bottled_water` | 2 000 | 2 000 000 | `:308` |
| `water_pitcher` (filter container) | 3 000 | 3 000 000 | `AmenitiesItemDirectory.cs:561` |
| `water_jug` | 6 000 | 6 000 000 | `FoodItemDirectory.cs:318` |
| `ras_water_tank` | 12 000 | 12 000 000 | `MachineRAS.cs:243` |
| `water_tank` | 20 000 (upgradeable) | 20 000 000 | `WaterTankHelper.cs:10,83` |
| `water_tank` max upgrade | 50 000 | 50 000 000 | `WaterTankHelper.cs:12,63-67` |
| wine bottle (`WINE_CONTAINER_TAG`) | 750 | 750 000 | `WineHelper.cs:280-283` |
| `water_ration` | *not a container* (consumable: 750/375 ml poured on use) | — | `WineHelper.cs:137-160` |

Useful fixtures: droppers/cylinders are the calibrated 1-100 ml containers the precision extractor is designed to pair with; `water_pitcher` demonstrates filter-hosting; `water_tank`/`ras_water_tank` are the big-tank cases where long cycles matter.

### 9.3 Creation & fill in code (verified pattern)

```csharp
using System.Collections.Generic;

// 1) create an owned, fully initialized container (DirectoryMaster.Item sets IS_OWNED_TAG, unique id, etc.)
GameItem bottle = DirectoryMaster.Item("large_bottled_water");            // 2 000 ml = 2 000 000 µL

// 2) fill with a premade water grade: 0 pure / 1 HQ / 2 base / 3 ghost / 4 rust / 5 gutterflow
WaterHelper.AddWater(bottle, 2, 500_000);                                  // +500 ml base water (9800-9899 bp)

// 3) add specific contaminants / products (µL)
WaterHelper.AddLiquid(bottle, "heavy_metal", 2_500);
WaterHelper.AddLiquid(bottle, "microbe", 5_000);
WaterHelper.AddLiquid(bottle, "dream_cap_extract", 10_000);                // "valuable product" for protection tests

// 4) rebuild the label/feature/price consistently (AddWater already does this; explicit re-init is safe)
// Do NOT call WaterHelper.InitLiquidContainerItem again - the container is already initialized.
// Reference only: WaterFeatureHelper.InitWaterFeature(bottle, isExpose: false);

// 5) put it in the world / player inventory
GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, bottle);
```

One-liners from §10 helper (optional convenience, identical semantics):

```csharp
GameItem test = LiquidApiHelper.CreateTestContainer(
    "water_jug", 6_000_000, 5,
    new[] { new KeyValuePair<string,int>("heavy_metal", 30_000),
            new KeyValuePair<string,int>("dream_cap_extract", 200_000) });
LiquidApiHelper.SpawnTestContainer("100ml_cylinder", 100_000, 2); // spawns into the open Emporium grid
```

Rules for tests:
- `DirectoryMaster.Item(id, isOwned: true)` — always pass owned=true in dev/test; `ItemSpawner.Spawn(id)` also works and uses the directory factory.
- Never fill by writing tags only — call `WaterFeatureHelper.InitWaterFeature`/`RefreshWaterState` afterwards or the tooltip/price/sprite desync.
- `PlayerStore.Instance.devMode` makes the vanilla water tooltip print real vs fake condition and the liquid colour base (`WaterHelper.CreateWaterContainerTooltip` `:548-554`) — useful for verifying tests in-game.
- `WaterHelper.SpawnWaterTestingKit()` (`:1334-1382`) is a ready-made debug helper that spawns every water fixture (bottles of each grade, filters, purifier, scanner, etc.) — copy/adapt it for mod dev builds.

### 9.4 Creating a new modded liquid container item correctly

```csharp
GameItem item = ItemDirectory.CreateEmptyItem().SetSpriteAndShape("Items/water_container3", "100ml");
item.identifier = "mod_250ml_beaker";
item.name = "250 ml Beaker";
item.shortDescription = "A graduated beaker.";
item.unitValue = 20;
WaterHelper.InitLiquidContainerItem(item, 250, 20, isFilterContainer: false, isGraduated: true,
                                    isDropper: false, isTransparentContainer: true);
AudioHelper.InitPlasticBottle(item);            // or any appropriate audio init
```
This single call gives you: tag, capacity 250 000 µL, all part tags, water feature, pour/dropper/drain interactions, temp tick, sprites & sound. For a *filter* host add `isFilterContainer: true` and provide the child filter slot (see `AmenitiesItemDirectory.WaterPitcher` `:549-565`). For a machine-internal tank see `MachineRAS.CreateWaterTank` `:238-254` (12 000 ml, no value, keeps `RAS_WATER_TANK_TAG`).

---

## 10. Copy-paste helper code (verified against game assemblies)

The two files below compile as-is against `Assembly-CSharp.dll` (+ Unity core module, mscorlib/netstandard). Namespace is `ProbablyStolen.LiquidApi` — rename as needed; **no Harmony/BepInEx dependency**; all game types are referenced directly, so place them in the same assembly as the rest of the mod and let the mod loader handle injection.

Design contract:
- `LiquidApiHelper` is read/write only; it does **not** patch anything.
- All mutating methods validate first, write tags, refresh features, and return 0/no-op on refusal.
- Protected liquids are a mod-side constant; override per call with `extraProtectedIds`.
- `PurifierMachineSkeleton` is the optional machine-cycle wiring; delete it if the machine layer is implemented elsewhere.

### 10.1 `LiquidApiHelper.cs`

```csharp
using System;
using System.Collections.Generic;

namespace ProbablyStolen.LiquidApi
{
	[Flags]
	public enum ContaminantCategory
	{
		None = 0,
		Physical = 1,
		Chemical = 2,
		Biological = 4,
		All = 7
	}

	public enum TransferMode
	{
		Proportional = 0,
		WaterOnly = 1,
		SingleLiquid = 2
	}

	public enum WaterLabelMode
	{
		PreserveLabel = 0,
		RelabelAccurate = 1
	}

	public static class LiquidApiHelper
	{
		public const string LIQUID_CONTAINER_TAG = "LIQUID_CONTAINER_TAG";
		public const string LIQUID_CONTAINER_CAPACITY = "LIQUID_CONTAINER_CAPACITY";
		public const string WINE_CONTAINER_TAG = "WINE_CONTAINER_TAG";
		public const string WATER_IDENTIFIER = "water";
		public const int FULL_PURITY = 10000;
		public const int PERMILLE_VOLUME_BUDGET = 3;
		public const int FLAT_VOLUME_BUDGET_UL = 25000;
		public const int VOLUME_BUDGET_CAP_UL = 10000000;

		public static readonly string[] PhysicalContaminantIds = { "physical_contaminant", "microplastic", "debris", "mineral" };
		public static readonly string[] ChemicalContaminantIds = { "chemical_contaminant", "heavy_metal" };
		public static readonly string[] BiologicalContaminantIds = { "microbe", "organic_waste", "plant_matter" };

		private static readonly HashSet<string> PhysicalSet = new HashSet<string>(PhysicalContaminantIds, StringComparer.Ordinal);
		private static readonly HashSet<string> ChemicalSet = new HashSet<string>(ChemicalContaminantIds, StringComparer.Ordinal);
		private static readonly HashSet<string> BiologicalSet = new HashSet<string>(BiologicalContaminantIds, StringComparer.Ordinal);

		private static readonly HashSet<string> DefaultProtectedLiquids = new HashSet<string>(StringComparer.Ordinal)
		{
			"dream_cap_mush", "dream_cap_extract", "nightmare_cap_mush", "nightmare_extract",
			"coffee_mush", "caffeine", "alcohol", "wine_extract", "protein", "fat", "starch", "sugar",
			"contraceptive", "zyanide", "chlorine", "synaprest", "fanta", "methylol", "reducing_agent",
			"acid", "base", "stabilizing_agent", "lipobond_agent", "livestock_steroid", "livestock_immunity",
			"nomore"
		};

		public static bool IsLiquidContainer(GameItem item)
		{
			return item != null && item.IsTag(LIQUID_CONTAINER_TAG);
		}

		public static bool IsWineContainer(GameItem item)
		{
			return item != null && item.IsTag(WINE_CONTAINER_TAG);
		}

		public static bool IsEvaporativeLiquid(string liquidId)
		{
			Liquid liquid = FindLiquid(liquidId);
			return liquid != null && liquid.isEvaporative;
		}

		public static Liquid FindLiquid(string liquidId)
		{
			if (string.IsNullOrEmpty(liquidId))
			{
				return null;
			}
			Liquid liquid;
			if (Liquid.LiquidDict.TryGetValue(liquidId, out liquid))
			{
				return liquid;
			}
			return null;
		}

		public static int GetLiquidVolumeUL(GameItem container, string liquidId)
		{
			Liquid liquid = FindLiquid(liquidId);
			if (container == null || liquid == null)
			{
				return 0;
			}
			return container.GetTagReadonly(liquid.currentPart).GetInt();
		}

		public static int GetTotalVolumeUL(GameItem container)
		{
			return container == null ? 0 : WaterHelper.GetTotalVolume(container);
		}

		public static int GetCapacityUL(GameItem container)
		{
			if (!IsLiquidContainer(container))
			{
				return 0;
			}
			return container.GetTagReadonly(LIQUID_CONTAINER_CAPACITY).GetInt();
		}

		public static int GetFreeVolumeUL(GameItem container)
		{
			if (!IsLiquidContainer(container))
			{
				return 0;
			}
			long free = (long)GetCapacityUL(container) - WaterHelper.GetTotalVolume(container);
			if (free <= 0L)
			{
				return 0;
			}
			return free > int.MaxValue ? int.MaxValue : (int)free;
		}

		public static int GetWaterPurityBP(GameItem container)
		{
			if (!IsLiquidContainer(container))
			{
				return 0;
			}
			return WaterHelper.GetWaterPurity(container);
		}

		public static Dictionary<string, int> GetComposition(GameItem container)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.Ordinal);
			if (container == null)
			{
				return dictionary;
			}
			int[] liquidParts = WaterHelper.GetLiquidParts(container);
			for (int i = 0; i < Liquid.Liquids.Count; i++)
			{
				if (liquidParts[i] > 0)
				{
					dictionary[Liquid.Liquids[i].identifier] = liquidParts[i];
				}
			}
			return dictionary;
		}

		public static string GetPurityLabel(int purityBP)
		{
			return WaterFeatureHelper.GetWaterDisplayName(purityBP);
		}

		public static int GetContaminantVolumeUL(GameItem container, ContaminantCategory categories)
		{
			if (container == null)
			{
				return 0;
			}
			int[] liquidParts = WaterHelper.GetLiquidParts(container);
			long num = 0L;
			for (int i = 0; i < Liquid.Liquids.Count; i++)
			{
				if (liquidParts[i] > 0 && CategoryOf(Liquid.Liquids[i].identifier) != ContaminantCategory.None && (CategoryOf(Liquid.Liquids[i].identifier) & categories) != 0)
				{
					num += liquidParts[i];
				}
			}
			return num > int.MaxValue ? int.MaxValue : (int)num;
		}

		public static int GetRemainingContaminantsUL(GameItem container, ContaminantCategory categories, IEnumerable<string> extraProtectedIds = null)
		{
			if (container == null)
			{
				return 0;
			}
			HashSet<string> protectedLiquids = BuildProtectedSet(extraProtectedIds);
			int[] liquidParts = WaterHelper.GetLiquidParts(container);
			long num = 0L;
			for (int i = 0; i < Liquid.Liquids.Count; i++)
			{
				Liquid liquid = Liquid.Liquids[i];
				if (liquidParts[i] <= 0 || protectedLiquids.Contains(liquid.identifier) || !liquid.canBeRemovedByPurifier)
				{
					continue;
				}
				if ((CategoryOf(liquid.identifier) & categories) != 0)
				{
					num += liquidParts[i];
				}
			}
			return num > int.MaxValue ? int.MaxValue : (int)num;
		}

		public static ContaminantCategory CategoryOf(string liquidId)
		{
			if (liquidId == null)
			{
				return ContaminantCategory.None;
			}
			if (PhysicalSet.Contains(liquidId))
			{
				return ContaminantCategory.Physical;
			}
			if (ChemicalSet.Contains(liquidId))
			{
				return ContaminantCategory.Chemical;
			}
			if (BiologicalSet.Contains(liquidId))
			{
				return ContaminantCategory.Biological;
			}
			return ContaminantCategory.None;
		}

		public static bool IsProtectedLiquid(string liquidId, IEnumerable<string> extraProtectedIds = null)
		{
			if (liquidId == null || string.Equals(liquidId, WATER_IDENTIFIER, StringComparison.Ordinal))
			{
				return true;
			}
			if (DefaultProtectedLiquids.Contains(liquidId))
			{
				return true;
			}
			if (extraProtectedIds != null)
			{
				foreach (string extraProtectedId in extraProtectedIds)
				{
					if (string.Equals(extraProtectedId, liquidId, StringComparison.Ordinal))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static HashSet<string> BuildProtectedSet(IEnumerable<string> extraProtectedIds)
		{
			HashSet<string> hashSet = new HashSet<string>(DefaultProtectedLiquids, StringComparer.Ordinal);
			hashSet.Add(WATER_IDENTIFIER);
			if (extraProtectedIds != null)
			{
				foreach (string extraProtectedId in extraProtectedIds)
				{
					if (!string.IsNullOrEmpty(extraProtectedId))
					{
						hashSet.Add(extraProtectedId);
					}
				}
			}
			return hashSet;
		}

		public static int GetTransferableUL(GameItem source, GameItem target, int requestedUL, TransferMode mode = TransferMode.Proportional, string singleLiquidId = null)
		{
			if (!IsLiquidContainer(source) || !IsLiquidContainer(target) || requestedUL <= 0)
			{
				return 0;
			}
			if (IsWineContainer(source) || IsWineContainer(target))
			{
				return 0;
			}
			long num = Math.Min((long)requestedUL, (long)GetFreeVolumeUL(target));
			if (num <= 0L)
			{
				return 0;
			}
			if (mode == TransferMode.WaterOnly)
			{
				num = Math.Min(num, (long)GetLiquidVolumeUL(source, WATER_IDENTIFIER));
			}
			else if (mode == TransferMode.SingleLiquid)
			{
				if (FindLiquid(singleLiquidId) == null)
				{
					return 0;
				}
				num = Math.Min(num, (long)GetLiquidVolumeUL(source, singleLiquidId));
			}
			else
			{
				num = Math.Min(num, (long)GetTotalVolumeUL(source));
			}
			return num > int.MaxValue ? int.MaxValue : (int)num;
		}

		public static bool CanTransferExact(GameItem source, GameItem target, int volumeUL, TransferMode mode, string singleLiquidId, out string error)
		{
			error = null;
			if (!IsLiquidContainer(source))
			{
				error = "source is not a liquid container";
				return false;
			}
			if (!IsLiquidContainer(target))
			{
				error = "target is not a liquid container";
				return false;
			}
			if (IsWineContainer(source) || IsWineContainer(target))
			{
				error = "wine containers use WineHelper, not supported";
				return false;
			}
			if (volumeUL <= 0)
			{
				error = "volume must be >= 1 uL";
				return false;
			}
			if (GetFreeVolumeUL(target) < volumeUL)
			{
				error = "target does not have " + volumeUL + " uL free capacity";
				return false;
			}
			if (mode == TransferMode.WaterOnly)
			{
				if (GetLiquidVolumeUL(source, WATER_IDENTIFIER) < volumeUL)
				{
					error = "source does not hold " + volumeUL + " uL of water";
					return false;
				}
			}
			else if (mode == TransferMode.SingleLiquid)
			{
				if (FindLiquid(singleLiquidId) == null)
				{
					error = "unknown liquid id " + singleLiquidId;
					return false;
				}
				if (GetLiquidVolumeUL(source, singleLiquidId) < volumeUL)
				{
					error = "source does not hold " + volumeUL + " uL of " + singleLiquidId;
					return false;
				}
			}
			else if (GetTotalVolumeUL(source) < volumeUL)
			{
				error = "source does not hold " + volumeUL + " uL";
				return false;
			}
			return true;
		}

		public static int TransferExact(GameItem source, GameItem target, int volumeUL, TransferMode mode = TransferMode.Proportional, string singleLiquidId = null, WaterLabelMode targetLabelMode = WaterLabelMode.PreserveLabel)
		{
			string error;
			if (!CanTransferExact(source, target, volumeUL, mode, singleLiquidId, out error))
			{
				return 0;
			}
			bool wasEmpty = WaterHelper.IsEmpty(target);
			int[] liquidParts = WaterHelper.GetLiquidParts(source);
			int[] array = null;
			int num = -1;
			if (mode == TransferMode.WaterOnly)
			{
				num = IndexOfLiquid(WATER_IDENTIFIER);
			}
			else if (mode == TransferMode.SingleLiquid)
			{
				num = IndexOfLiquid(singleLiquidId);
			}
			if (num >= 0)
			{
				array = new int[liquidParts.Length];
				array[num] = volumeUL;
			}
			else
			{
				array = WaterHelper.SplitProportionally(liquidParts, volumeUL);
			}
			long num2 = 0L;
			for (int i = 0; i < array.Length; i++)
			{
				num2 += array[i];
			}
			if (num2 != volumeUL)
			{
				return 0;
			}
			for (int j = 0; j < array.Length; j++)
			{
				if (array[j] > 0)
				{
					string currentPart = Liquid.Liquids[j].currentPart;
					int amount = array[j];
					source.ModifyTag(currentPart, delegate(TagState state)
					{
						state.Enable().SetInt(state.GetInt() - amount);
					}, skipValidate: true);
					target.ModifyTag(currentPart, delegate(TagState state)
					{
						state.Enable().SetInt(state.GetInt() + amount);
					}, skipValidate: true);
				}
			}
			ChemicalHelper.HandleReceiveLiquidTemp(target, volumeUL, source);
			source.ValidateShapeState();
			target.ValidateShapeState();
			WaterFeatureHelper.OnLiquidContainerPourInto(target, source, wasEmpty);
			WaterFeatureHelper.OnLiquidContainerPourOutOf(source);
			if (targetLabelMode == WaterLabelMode.RelabelAccurate)
			{
				RelabelAccurate(target);
			}
			return volumeUL;
		}

		public static long ComputeCycleBudgetUL(int totalVolumeUL, int waterPurityBP, int performanceBonus = 0)
		{
			double dirtBonus = MachinePurifier.GetDirtBonus(waterPurityBP) + (double)performanceBonus / 100.0;
			if (dirtBonus < 0.0)
			{
				dirtBonus = 0.0;
			}
			dirtBonus *= 1.25;
			long num = (long)((double)((long)Math.Min(totalVolumeUL, VOLUME_BUDGET_CAP_UL) * (long)PERMILLE_VOLUME_BUDGET / 1000L + FLAT_VOLUME_BUDGET_UL) * dirtBonus);
			return num < 0L ? 0L : num;
		}

		public static int GetPurityCapForQuality(int qualityBonus)
		{
			return MachinePurifier.GetMaxPurityCap(qualityBonus);
		}

		public static int GetCandidateVolumeUL(GameItem container, ContaminantCategory categories, IEnumerable<string> extraProtectedIds = null)
		{
			return GetRemainingContaminantsUL(container, categories, extraProtectedIds);
		}

		public static int GetCategoryPurityBP(GameItem container, ContaminantCategory categories, IEnumerable<string> extraProtectedIds = null)
		{
			if (!IsLiquidContainer(container))
			{
				return 0;
			}
			int liquidVolumeUL = GetLiquidVolumeUL(container, WATER_IDENTIFIER);
			int remainingContaminantsUL = GetRemainingContaminantsUL(container, categories, extraProtectedIds);
			long num = (long)liquidVolumeUL + (long)remainingContaminantsUL;
			if (num <= 0L)
			{
				return 0;
			}
			return (int)((long)liquidVolumeUL * (long)FULL_PURITY / num);
		}

		public static int GetReachablePurityBP(GameItem container, ContaminantCategory categories, bool convertToWater = true, IEnumerable<string> extraProtectedIds = null)
		{
			if (!IsLiquidContainer(container))
			{
				return 0;
			}
			int totalVolumeUL = WaterHelper.GetTotalVolume(container);
			if (totalVolumeUL <= 0)
			{
				return 0;
			}
			int liquidVolumeUL = GetLiquidVolumeUL(container, WATER_IDENTIFIER);
			int num = GetRemainingContaminantsUL(container, categories, extraProtectedIds);
			int num2 = totalVolumeUL - liquidVolumeUL - num;
			if (convertToWater)
			{
				return (int)((long)(liquidVolumeUL + num) * (long)FULL_PURITY / (long)totalVolumeUL);
			}
			long num3 = (long)liquidVolumeUL + (long)num2;
			if (num3 <= 0L)
			{
				return FULL_PURITY;
			}
			return (int)((long)liquidVolumeUL * (long)FULL_PURITY / num3);
		}

		public static int ComputeRemovalLimitUL(GameItem container, int purityCapBP, bool convertToWater, ContaminantCategory categories = ContaminantCategory.All, IEnumerable<string> extraProtectedIds = null)
		{
			if (!IsLiquidContainer(container))
			{
				return 0;
			}
			int totalVolumeUL = WaterHelper.GetTotalVolume(container);
			if (totalVolumeUL <= 0)
			{
				return 0;
			}
			int num = GetRemainingContaminantsUL(container, categories, extraProtectedIds);
			if (num <= 0)
			{
				return 0;
			}
			int purityCap = Math.Min(FULL_PURITY, Math.Max(0, purityCapBP));
			if (purityCap <= 0)
			{
				return num;
			}
			int liquidVolumeUL = GetLiquidVolumeUL(container, WATER_IDENTIFIER);
			int reachablePurityBP = GetReachablePurityBP(container, categories, convertToWater, extraProtectedIds);
			int num2 = Math.Min(purityCap, reachablePurityBP);
			if (num2 <= 0)
			{
				return num;
			}
			long num3;
			if (convertToWater)
			{
				long num4 = (long)totalVolumeUL * (long)(FULL_PURITY - num2) / (long)FULL_PURITY;
				num3 = (long)(totalVolumeUL - liquidVolumeUL) - num4;
			}
			else
			{
				long num5 = (long)liquidVolumeUL * (long)FULL_PURITY / (long)num2;
				num3 = (long)totalVolumeUL - num5;
			}
			if (num3 <= 0L)
			{
				return 0;
			}
			if (num3 > num)
			{
				num3 = num;
			}
			return num3 > int.MaxValue ? int.MaxValue : (int)num3;
		}

		public static int PurifyByCategory(GameItem container, int purityCapBP, ContaminantCategory categories, int budgetUL, bool convertToWater = true, IEnumerable<string> extraProtectedIds = null, WaterLabelMode labelMode = WaterLabelMode.PreserveLabel, bool requireWater = true)
		{
			if (!IsLiquidContainer(container) || IsWineContainer(container) || categories == ContaminantCategory.None)
			{
				return 0;
			}
			int totalVolumeUL = WaterHelper.GetTotalVolume(container);
			if (totalVolumeUL <= 0)
			{
				return 0;
			}
			int liquidVolumeUL = GetLiquidVolumeUL(container, WATER_IDENTIFIER);
			if (requireWater && liquidVolumeUL <= 0)
			{
				return 0;
			}
			HashSet<string> protectedLiquids = BuildProtectedSet(extraProtectedIds);
			int[] liquidParts = WaterHelper.GetLiquidParts(container);
			int[] array = new int[liquidParts.Length];
			long num = 0L;
			for (int i = 0; i < Liquid.Liquids.Count; i++)
			{
				Liquid liquid = Liquid.Liquids[i];
				if (liquidParts[i] > 0 && !protectedLiquids.Contains(liquid.identifier) && liquid.canBeRemovedByPurifier && (CategoryOf(liquid.identifier) & categories) != 0)
				{
					array[i] = liquidParts[i];
					num += liquidParts[i];
				}
			}
			if (num <= 0L)
			{
				return 0;
			}
			long num2 = Math.Min((long)Math.Max(0, budgetUL), (long)ComputeRemovalLimitUL(container, purityCapBP, convertToWater, categories, extraProtectedIds));
			long num3 = Math.Min(num2, num);
			if (num3 <= 0L)
			{
				return 0;
			}
			int[] array2 = WaterHelper.SplitProportionally(array, (int)num3);
			int num4 = 0;
			for (int j = 0; j < array2.Length; j++)
			{
				if (array2[j] > 0)
				{
					int amount = array2[j];
					container.ModifyTag(Liquid.Liquids[j].currentPart, delegate(TagState state)
					{
						state.Enable().SetInt(state.GetInt() - amount);
					}, skipValidate: true);
					num4 += amount;
				}
			}
			if (num4 > 0)
			{
				if (convertToWater)
				{
					string currentPart = Liquid.GetCurrentPartFromId(WATER_IDENTIFIER);
					container.ModifyTag(currentPart, delegate(TagState state)
					{
						state.Enable().SetInt(state.GetInt() + num4);
					}, skipValidate: true);
				}
				container.ValidateShapeState();
				RefreshWaterState(container, labelMode);
			}
			return num4;
		}

		public static void RefreshWaterState(GameItem container, WaterLabelMode labelMode = WaterLabelMode.PreserveLabel)
		{
			if (!IsLiquidContainer(container) || IsWineContainer(container))
			{
				return;
			}
			WaterHelper.UpdateContainerGoodType(container);
			if (labelMode == WaterLabelMode.RelabelAccurate)
			{
				RelabelAccurate(container);
				return;
			}
			ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(container);
			bool exposed = waterFeature != null && waterFeature.IsExposed();
			WaterFeatureHelper.UpdateWaterFeature(container, exposed);
			WaterHelper.SetLastScanResult(container);
			SpriteHelper.UpdateWaterContainerSprite(container);
			container.ValidateShapeState();
		}

		public static void RelabelAccurate(GameItem container)
		{
			if (!IsLiquidContainer(container) || IsWineContainer(container))
			{
				return;
			}
			WaterFeatureHelper.InitWaterFeature(container, isExpose: false);
			WaterHelper.SetLastScanResult(container);
			SpriteHelper.UpdateWaterContainerSprite(container);
			container.ValidateShapeState();
		}

		public static void SetWaterVolumeUL(GameItem container, string liquidId, int volumeUL)
		{
			if (!IsLiquidContainer(container))
			{
				return;
			}
			Liquid liquid = FindLiquid(liquidId);
			if (liquid == null)
			{
				return;
			}
			int num = Math.Max(0, volumeUL);
			container.ModifyTag(liquid.currentPart, delegate(TagState state)
			{
				state.Enable().SetInt(num);
			});
			WaterHelper.RemoveExcessLiquid(container);
			RefreshWaterState(container);
		}
		public static int ConcentrateChemicalProduct(GameItem chemItem, int targetPurityBP, ContaminantCategory categories, int maxVolumeUL, IEnumerable<string> extraProtectedIds = null)
		{
			if (chemItem == null || !chemItem.IsTag("CHEM_ITEM_TAG") || !chemItem.IsTag("CHEMICAL_PRODUCT_TAG"))
			{
				return 0;
			}
			if (!chemItem.IsTag("ACTIVATE_INGREDIENT_TAG"))
			{
				return 0;
			}
			string @string = chemItem.GetTagReadonly("ACTIVATE_INGREDIENT_TAG").GetString();
			Liquid liquid = FindLiquid(@string);
			if (liquid == null)
			{
				return 0;
			}
			int totalVolume = ChemicalHelper.GetTotalVolume(chemItem);
			int activeVolume = chemItem.GetTagReadonly(liquid.currentPart).GetInt();
			if (totalVolume <= 0 || activeVolume <= 0)
			{
				return 0;
			}
			int num = Math.Min(FULL_PURITY, Math.Max(1, targetPurityBP));
			long num2 = (long)activeVolume * (long)FULL_PURITY / (long)num;
			long num3 = (long)totalVolume - num2;
			if (num3 <= 0L)
			{
				return 0;
			}
			long num4 = Math.Min(num3, Math.Max(0, maxVolumeUL));
			HashSet<string> protectedLiquids = BuildProtectedSet(extraProtectedIds);
			protectedLiquids.Add(liquid.identifier);
			int[] liquidParts = WaterHelper.GetLiquidParts(chemItem);
			int[] array = new int[liquidParts.Length];
			long num5 = 0L;
			for (int i = 0; i < Liquid.Liquids.Count; i++)
			{
				Liquid liquid2 = Liquid.Liquids[i];
				if (liquidParts[i] > 0 && !protectedLiquids.Contains(liquid2.identifier) && liquid2.canBeRemovedByPurifier && (CategoryOf(liquid2.identifier) & categories) != 0)
				{
					array[i] = liquidParts[i];
					num5 += liquidParts[i];
				}
			}
			if (num5 <= 0L)
			{
				return 0;
			}
			long num6 = Math.Min(num4, num5);
			if (num6 <= 0L)
			{
				return 0;
			}
			int[] array2 = WaterHelper.SplitProportionally(array, (int)num6);
			int num7 = 0;
			for (int j = 0; j < array2.Length; j++)
			{
				if (array2[j] > 0)
				{
					int amount = array2[j];
					chemItem.ModifyTag(Liquid.Liquids[j].currentPart, delegate(TagState state)
					{
						state.Enable().SetInt(state.GetInt() - amount);
					}, skipValidate: true);
					num7 += amount;
				}
			}
			if (num7 > 0)
			{
				chemItem.ValidateShapeState();
				ChemicalProductHelper.GradeProduct(chemItem);
			}
			return num7;
		}
		public static GameItem CreateTestContainer(string identifier, int fillUL = -1, int grade = 2, IEnumerable<KeyValuePair<string, int>> contaminants = null, bool owned = true)
		{
			GameItem gameItem = DirectoryMaster.Item(identifier, owned);
			if (!IsLiquidContainer(gameItem))
			{
				return gameItem;
			}
			if (fillUL < 0)
			{
				fillUL = GetFreeVolumeUL(gameItem);
			}
			fillUL = Math.Min(fillUL, GetFreeVolumeUL(gameItem));
			if (fillUL > 0)
			{
				WaterHelper.AddWater(gameItem, grade, fillUL);
			}
			if (contaminants != null)
			{
				foreach (KeyValuePair<string, int> contaminant in contaminants)
				{
					int num = Math.Min(contaminant.Value, GetFreeVolumeUL(gameItem));
					if (num > 0)
					{
						WaterHelper.AddLiquid(gameItem, contaminant.Key, num);
					}
				}
			}
			RelabelAccurate(gameItem);
			return gameItem;
		}

		public static GameItem SpawnTestContainer(string identifier, int fillUL = -1, int grade = 2, IEnumerable<KeyValuePair<string, int>> contaminants = null)
		{
			GameItem gameItem = CreateTestContainer(identifier, fillUL, grade, contaminants);
			if (EmporiumEntry.Instance != null && EmporiumEntry.Instance.invElement != null)
			{
				GraphUtils.TryAcceptAll(EmporiumEntry.Instance.invElement, gameItem);
			}
			return gameItem;
		}

		private static int IndexOfLiquid(string liquidId)
		{
			for (int i = 0; i < Liquid.Liquids.Count; i++)
			{
				if (Liquid.Liquids[i] != null && string.Equals(Liquid.Liquids[i].identifier, liquidId, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}
	}
}
```

### 10.2 `PurifierMachineSkeleton.cs`

```csharp
using System;

namespace ProbablyStolen.LiquidApi
{
	public static class PurifierMachineSkeleton
	{
		public const string TOTAL_CONTAMINANT_REMOVED_TAG = "TOTAL_CONTAMINANT_REMOVED_TAG";
		public const int DRAWN_POWER = 4;
		public const int BASE_CAP_BP = 9800;
		public const int CAP_PER_QUALITY_POINT = 1;
		public const int MAX_CAP_BP = 10000;

		public static int GetPurityCapBP(GameItem machine)
		{
			int currentQualityBonus = MachineryHelper.GetCurrentQualityBonus(machine);
			int num = BASE_CAP_BP + currentQualityBonus * CAP_PER_QUALITY_POINT;
			int val = MachinePurifier.GetMaxPurityCap(currentQualityBonus);
			return Math.Min(MAX_CAP_BP, Math.Min(num, val));
		}

		public static int RunPurifyCycle(GameItem machine, GameItem container, GameSlotInventory powerSlot, GameInventory moduleGrid, ContaminantCategory categories, bool convertToWater = true)
		{
			if (machine == null || container == null || moduleGrid == null)
			{
				return 0;
			}
			if (!LiquidApiHelper.IsLiquidContainer(container) || LiquidApiHelper.IsWineContainer(container))
			{
				return 0;
			}
			if (WaterHelper.IsEmpty(container))
			{
				return 0;
			}
			if (!MachineHelper.TryDrawCyclePower(machine, powerSlot))
			{
				return 0;
			}
			int purityCapBP = GetPurityCapBP(machine);
			int totalVolumeUL = WaterHelper.GetTotalVolume(container);
			int waterPurityBP = WaterHelper.GetWaterPurity(container);
			int currentPerformanceBonus = MachineryHelper.GetCurrentPerformanceBonus(machine);
			long num = LiquidApiHelper.ComputeCycleBudgetUL(totalVolumeUL, waterPurityBP, currentPerformanceBonus);
			int budgetUL = num > int.MaxValue ? int.MaxValue : (int)num;
			int num2 = LiquidApiHelper.PurifyByCategory(container, purityCapBP, categories, budgetUL, convertToWater, null, WaterLabelMode.PreserveLabel);
			if (num2 > 0)
			{
				int num3 = machine.GetTagReadonly(TOTAL_CONTAMINANT_REMOVED_TAG).GetInt();
				int removed = num2;
				machine.ModifyTag(TOTAL_CONTAMINANT_REMOVED_TAG, delegate(TagState state)
				{
					state.Enable().SetInt(num3 + removed);
				});
				MachineHelper.OnMachineActioned(machine, moduleGrid);
			}
			return num2;
		}
	}
}
```

### 10.3 Compile verification (reproducible)

```powershell
$managed = "<game>\Probably Stolen_Data\Managed"
$csc     = "C:\Program Files\dotnet\sdk\9.0.318\Roslyn\bincore\csc.dll"
dotnet $csc -nologo -target:library -langversion:latest -nostdlib+ -warnaserror+ `
  -r:"$managed\mscorlib.dll" -r:"$managed\netstandard.dll" -r:"$managed\System.dll" `
  -r:"$managed\System.Core.dll" -r:"$managed\System.Runtime.dll" `
  -r:"$managed\Assembly-CSharp.dll" -r:"$managed\UnityEngine.CoreModule.dll" `
  -out:"$env:TEMP\LiquidApiHelper.dll" LiquidApiHelper.cs PurifierMachineSkeleton.cs
# exit 0, 0 errors/0 warnings (verified 2026-10-30)
```

Note: compiling against the game's *shipped* `Assembly-CSharp.dll` proves API compatibility (names/signatures/accessibility). Runtime behavior still needs the in-game smoke test (§11.2, test list).

### 10.4 Usage examples

```csharp
// --- precision extraction (exact 250 µL of the mixture) ---
int moved = LiquidApiHelper.TransferExact(sourceBottle, targetCylinder, 250);
if (moved == 0) { /* show reason via CanTransferExact(out error) */ }

// --- water-only aliquot (e.g. draw 1 ml water from a soup) ---
LiquidApiHelper.TransferExact(soup, cylinder, 1_000, TransferMode.WaterOnly);

// --- single species (e.g. 2 ml of alcohol from a mixed vessel) ---
LiquidApiHelper.TransferExact(vessel, bottle, 2_000, TransferMode.SingleLiquid, "alcohol");

// --- purification, water mode (cap 98.00 %, convert removed contamination into water) ---
bool exposed = WaterFeatureHelper.GetWaterFeature(container).IsExposed();
int quality  = MachineryHelper.GetCurrentQualityBonus(machine);
int cap      = Math.Min(9_800 + quality, MachinePurifier.GetMaxPurityCap(quality));
int budget   = (int)Math.Min(int.MaxValue, LiquidApiHelper.ComputeCycleBudgetUL(
                   WaterHelper.GetTotalVolume(container),
                   WaterHelper.GetWaterPurity(container),
                   MachineryHelper.GetCurrentPerformanceBonus(machine)));
int removed  = LiquidApiHelper.PurifyByCategory(container, cap,
                   ContaminantCategory.All, budget, convertToWater: true,
                   extraProtectedIds: null,
                   labelMode: exposed ? WaterLabelMode.PreserveLabel : WaterLabelMode.PreserveLabel);

// --- purification, concentrate mode (dry drug vessel: no water added) ---
int removed2 = LiquidApiHelper.PurifyByCategory(container, 9_900,
                   ContaminantCategory.Physical | ContaminantCategory.Biological,
                   budget / 2, convertToWater: false, requireWater: false);

// --- optional: improve a CHEM_ITEM (injector/dust) grade by stripping cut/waste ---
int stripped = LiquidApiHelper.ConcentrateChemicalProduct(injector, 9_000,
                   ContaminantCategory.Physical | ContaminantCategory.Chemical);
```

---

## 11. Source map & open questions

### 11.1 Key file:line index (for reviewers)

| Concern | Where |
|---|---|
| Liquid class, tags, catalogue | `Liquid.cs:5-47, 49-119, 121, 123-127, 129-482, 484-492, 530-535` |
| Container init + capacity + drag handlers | `WaterHelper.cs:51-204` |
| Proportional split / composition | `WaterHelper.cs:206-265` |
| Pour transfer + filters + temp + features | `WaterHelper.cs:267-395` |
| Read volume/capacity/purity | `WaterHelper.cs:397-481, 557-584` |
| Empty/IsEmpty/IsFull/IsWet | `WaterHelper.cs:618-655, 1568-1590` |
| Tablets / UV / portable purify | `WaterHelper.cs:666-704, 739-790, 838-891` |
| AddLiquid/AddWater/Remove/RemoveAndReturn/RemoveMl | `WaterHelper.cs:893-1070, 1446-1566` |
| Last scan + tooltip + container colour | `WaterHelper.cs:517-555, 1592-1645` |
| Water feature lifecycle | `WaterFeatureHelper.cs:77-167, 169-294, 369-461` |
| Purity tiers/prices | `WaterFeatureHelper.cs:33-73` |
| Water conditions (modValue) | `ItemConditionList.cs:325-395` |
| Chemical purity grades | `ItemConditionList.cs:682-757`; `ChemicalProductHelper.cs:9-21, 103-154, 216-241` |
| Vanilla purifier | `MachinePurifier.cs:6-28, 30-112, 114-283, 285-298` |
| Machinery bonuses/power | `MachineryHelper.cs:117-140, 194-196, 532-540`; `MachineHelper.cs:41-52, 358-378` |
| Filters | `Liquid.cs:23-27, 121`; `WaterHelper.cs:318-395`; `AmenitiesItemDirectory.cs:407-470, 529-565` |
| Chemistry reactions (contaminants in/out) | `ChemicalHelper.cs:242-249, 251-360, 362-535, 536-586, 588-671, 673-788, 790-956, 958-1113` |
| Wat premade distributions/grades | `WaterPremadeHelper.cs:6-146`; `WaterHelper.cs:941-1070` |
| Chem finisher exact volumes | `MachineChemFinisher.cs:66-91, 109-119`; `ChemFinisherRecipe.cs:16-139` |
| Injectors / chem products | `MedsItemDirectory.cs:19-21, 188-237, 354-359, 361-461` |
| Evaporator / blender / moisture farm / tanks | `MachineEvaporator.cs:61-147`; `MachineBlender.cs:27-46, 118-137`; `MachineMoistureFarm.cs:237-269`; `WaterTankHelper.cs:10-86`; `MachineRAS.cs:238-254` |
| Wine | `WineHelper.cs:137-160, 226-309` |
| Value computation | `GameItem.cs:418-462, 1661-1688, 1690-1764, 1810-1815`; `ItemFeature.cs:116-130, 371-399, 446-460` |
| Client acceptance | `StoreClient.cs:354-424`; `StoreClientListTier.cs:47-327`; `StoreClientListCartel.cs:9-11, 273-380, 387-395`; `PlayerStore.cs:439` |
| Test container sources | `FoodItemDirectory.cs:164-321`; `AmenitiesItemDirectory.cs:549-565`; `WaterHelper.cs:1334-1382` |
| Mod corroboration (non-authoritative) | `ItemDetails\ItemDetails\WaterInfo.cs:43-118`; `WaterAdvice.cs:37-162, 391-423`; `PurifySources.cs:51-126` |

### 11.2 Open questions / suggestions for Lead

1. **Tool vs machine for 精确取液器**: item-on-item tool (cheap, quick) vs two-slot machine (better UI, matches ChemFinisher). The helper supports both; UI work lives with machine-api. Recommend tool v1 + optional machine upgrade later.
2. **Label policy after purification**: default PreserveLabel (vanilla information economy) with `SetLastScanResult` readout; optional “certify output” toggle. Needs a product decision (affects water economy/test-strip value).
3. **Mineral protection default**: include mineral in Physical (task listing) but expose “protect nutrients” toggle; test with hydroponics/aquaculture flows before shipping.
4. **Mode B item sink**: deletion is simplest; if reviewers want realism/anti-exploit, add a “purification sludge” junk item per cycle (needs item/art/localization — not in this scope).
5. **Cartel pacing risk**: if Mode B (or optional CHEM-item concentration) makes 70 % cartel purity trivial, the cap on `ConcentrateChemicalProduct` should be ~90 % and/or budget halved again. Numbers in §8.3 assume no active-ingredient creation.
6. **In-game smoke test list** (needs game launch, not done here per task rules): fill bottle → extract exact volumes across modes incl. failures; verify label/sprite/price after each op; purify gutterflow→base; purify product-rich vessel and assert product volumes unchanged; concentrate dry vessel and feed ChemFinisher; check save/load roundtrip of mutated tags; check clients accept/reject per label; check `TOTAL_CONTAMINANT_REMOVED_TAG` tooltip.
7. **Localization keys**: new machine/item names & UI strings need keys (mod-framework's area); this doc uses English identifiers (`mod_precision_extractor`, `mod_purification_machine` placeholders only).
---

## 12. Appendix — review of the current `src/PrecisionLab` implementation (2026-10-30)

This appendix is the actionable delta list for the reference implementation (`src/PrecisionLab/LiquidTools.cs`, `ItemFactory.cs`, `SelfTest.cs`) against the game API documented above. It mirrors the review message sent to the Lead.

### 12.1 Verdict

Architecture is correct (µL tags, `CURRENT_PART_*`, `SplitProportionally`, vanilla pour refresh functions). No save-format or container-capacity invariant is broken by the design. Four real bugs and three gaps need local fixes.

### 12.2 Bugs (fix before shipping)

| # | Where | Problem | Fix |
|---|---|---|---|
| 1 | `LiquidTools.MoveExact` (`LiquidTools.cs:109-113`) | Clamps to `min(requested, source, free)` → **partial transfers**, contradicting the item's "exact N µL" contract | Return 0 unless `deliverable == requested`; for injection also require extractor content ≥ dose. Add `CanMove(...)` used by tooltip/UI |
| 2 | `LiquidTools.MoveExact` after write loop | Missing `ValidateShapeState()` on both items (vanilla pours get validation from the drag frame; machine/self-test paths have none) and no `source == target` guard | `source.ValidateShapeState(); target.ValidateShapeState();` after writes; early-return if `ReferenceEquals(source, target)` |
| 3 | `LiquidTools.PurifyContaminants` (`:250-251`) | Uses the **convert-to-water cap formula** while deleting volume → under-removal. Selftest example: water 5959 / total 7066 / cap 9900 removes 1037 (purity 9883) instead of 1047 (purity 9900) | Deletion: `t = largest t with cap*t <= water*10000; removeNeeded = total - t`. Convert: `water+r >= ceil(cap*total/10000)` |
| 4 | `LiquidTools.PurifyContaminants` (`:344-352`) | `UpdateWaterFeature(container,false)` forces unexposed (kills a prior test), keeps the stale public label, and does **not** recompute `unitValue` for the smaller volume | Call `WaterFeatureHelper.OnLiquidContainerPourOutOf(container)` (price/feature/empty/sprite/sound) then optional `WaterHelper.SetLastScanResult(container)` + `container.ValidateShapeState()` |

### 12.3 Gaps

1. **`plant_matter` missing from biological list** (mushroom/plant degradation product, `ChemicalHelper.cs:317-360`). Add it. Do **not** add `acid`/`base`/`chlorine` (reagents) or `starch`/`alcohol`/mush/extract (products). Keep `mineral` but expose a "protect nutrients" option (hydroponic nutrient, `PlantWaterQuality.cs:36-43`).
2. **Cap unreachable with protected products** — leaving ~1 % residual contamination forever when valuable liquids keep raw purity below cap. If `reachablePurity < cap`, remove **all** selected contaminants (bounded by budget).
3. **Wine containers accepted** by `CanUseOn` (`ItemFactory.cs:60-71`): wine has `LIQUID_CONTAINER_TAG` (`WineHelper.cs:279`) but the water-feature functions early-return for wine → desync. Reject `WINE_CONTAINER_TAG`. Also block incoming vanilla pours into the extractor: `GameItem.MayTarget` ORs `s.mayThisTargetItemFunc || t.mayItemTargetThisFunc` (`GameItem.cs:966-969`); set `mayItemTargetThisFunc`/`canItemTargetThisFunc = (s,t) => false` on the extractor.

### 12.4 NPE / robustness guards

1. `WaterFeatureHelper.InitWaterFeature` calls `ItemMouseDragHandler.current.GetCurrentItem()` unguarded (`WaterFeatureHelper.cs:216`). Guard `ItemFactory.InitLiquidContainer` (`if (ItemMouseDragHandler.current != null)`), or wrap in try/catch with `WaterHelper.UpdateContainerGoodType` fallback.
2. `AudioHelper.DropperDraw/DropperDrop/PourWater` use `AudioManager.Instance` unguarded (`AudioHelper.cs:535-553`); guard or keep inside try/catch (already wrapped).
3. `Liquid.LiquidDict[...]` direct index in `PurifyContaminants` (`LiquidTools.cs:324`) → use `TryGetValue`.
4. `UpdateContainerGoodType` can call `AddFeatureRelatedToEvent` → `StoreStation.Instance` unguarded (`GameItem.cs:2218`); only triggers for back-inventory items, but don't call it before `StoreStation` exists.
5. `ItemFactory.CreateExtractor` sets `unitValue = ExtractorBasePrice` before `InitLiquidContainer`; `InitWaterFeature` overwrites it to `GetWaterPrice(empty) = 0` (`WaterFeatureHelper.cs:215`). Current total item value still equals the `LIQUID_CONTAINER_BASE_PRICE_TAG` (1200, `GameItem.cs:1810-1815`) — decide if that is intended; setting it after init would double to 2400.
6. The filled extractor gains item type `WATER` via `UpdateContainerGoodType` — if it should not be sellable to water clients, add `NOT_FOR_RESALE`.

### 12.5 Design deltas (balance / UX, not correctness)

1. Purify budget uses `(50000 + total/20) * 1.25 * (1 + perf/100)` — 8-24× vanilla on large tanks. Vanilla formula: `(min(total,10M)*3/1000 + 25000) * GetDirtBonus(purity) * 1.25` (`MachinePurifier.cs:137,8-10,193-210`, `GetDirtBonus` is public).
2. `GetPurityCap` hand table is more generous than vanilla at quality ≥ 25 (e.g. q100 → 9995 vs 9990). Reuse `MachinePurifier.GetMaxPurityCap(quality)` for no-drift parity.
3. "Exact N" needs composition modes for future use (proportional / water-only / single-species) — see `TransferMode` in §10.1; the current implementation only moves proportional mixture.
4. `MoveExact` intentionally skips filter handling (`WaterHelper.GetFilterItem`/`HandleFilter`, `WaterHelper.cs:295-302,318-395`); document this in the tooltip (pouring into a filter container with the extractor does not filter).

### 12.6 SelfTest corrections

| step | expected (correct) | current code result |
|---|---|---|
| fill | 8300 µL total (7000 water + 600 microbe + 400 heavy_metal + 300 physical), purity **8433 bp** | same ✓ |
| `MoveExact(...,1234)` | target 1234 µL (water 1041 / microbe 89 / heavy 59 / physical 45, purity 8435); source 7066 µL (5959/511/341/255, purity 8433) | same ✓ |
| purify cap 9900, deletion | remove **1047**, leftover 60, total 6019, purity **9900** | removes 1037 → total 6029, purity **9883**, leftover 70 (log comment "~1036 leaving ~70 at cap 9900" is self-inconsistent) |
| convert-to-water alternative | remove 1037, total unchanged, purity 9900; must change the `totalAfter == totalBefore - removed` assertion | n/a |

Add assertions: `purityAfter >= cap` when cap reachable; failure paths (empty source, target full, inject > content); assert `targetPurity` (currently computed and logged but not asserted).
