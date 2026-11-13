using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Star Quiler's Med first aid box:
    ///  - genuine/fake sealed boxes are appraised with the vanilla magnifier drag;
    ///  - empty shells + hand-applied seals let the player assemble a box;
    ///  - blue blood bag upgrades the assembled box to the advanced variant;
    ///  - double-click/activation dumps the contents so they can be sold separately.
    /// </summary>
    internal static class FirstAidBox
    {
        internal const string BoxId = "star_quiler_first_aid_box";
        internal const string ShellId = "first_aid_box_shell";
        internal const string SealId = "first_aid_box_seal";
        internal const string AppraisalTypeId = "precision_first_aid_box";

        internal const string GenuineTag = "FIRST_AID_GENUINE_INT";
        internal const string SerialTag = "FIRST_AID_SERIAL_STRING";
        internal const string RedCrossTag = "FIRST_AID_RED_CROSS_INT";
        internal const string StickerTag = "FIRST_AID_STICKER_INT";
        internal const string BrandTag = "FIRST_AID_BRAND_INT";
        internal const string ManualTag = "FIRST_AID_MANUAL_INT";
        internal const string IncompleteTag = "FIRST_AID_INCOMPLETE_INT";
        internal const string AdvancedTag = "FIRST_AID_ADVANCED_INT";
        internal const string ValueTag = "FIRST_AID_VALUE_INT";
        internal const string ShellTag = "FIRST_AID_SHELL_INT";

        private static readonly string[] RequiredContents =
        {
            ItemIds.Pen, "makeshift_tourniquet", "large_purple_injector", "pure_white_injector",
            "rubbing_alcohol", "blood_bag"
        };

        internal static bool IsBox(GameItem item)
        {
            return item != null && item.identifier == BoxId;
        }

        internal static bool IsShell(GameItem item)
        {
            return item != null && item.identifier == ShellId;
        }

        internal static void EnsureRegistered()
        {
            FirstAidTier.EnsureRegistered();
        }

        // ------------------------------------------------------------------
        // Factories
        // ------------------------------------------------------------------

        internal static GameItem CreateBox()
        {
            GameItem box = CreateContainerBase(BoxId, SpriteKeys.FirstAidBox,
                "item.first_aid_box.name", "“Star Quiler’s Med”急救盒",
                "item.first_aid_box.desc", "标准 3×3 急救盒。放大镜拖动可鉴定真伪；双击拆开可单卖内部物品。",
                "item.first_aid_box.flavor", "Star Quiler's Med：前线急救标准配置。");
            ConfigureAuthenticity(box, true, false);
            FillContents(box, false);
            FirstAidTier.EnsureFeature(box);
            AttachBoxBehavior(box);
            Debug.Log("[PrecisionLab] first aid box created genuine=" + (GetInt(box, GenuineTag) == 1) + " contents=" + (box.contentWindow != null && box.contentWindow.child is GameInventory ? ((GameInventory)box.contentWindow.child).childItems.Count : -1));
            return box;
        }

        internal static GameItem CreateShell()
        {
            GameItem shell = CreateContainerBase(ShellId, SpriteKeys.FirstAidShell,
                "item.first_aid_shell.name", "急救盒外壳（空）",
                "item.first_aid_shell.desc", "3×3 空急救盒外壳。放入药品后，把密封贴拖到外壳上即可封装。",
                "item.first_aid_shell.flavor", "没有密封贴的急救盒只是一个铁皮盒子。");
            shell.EnableTag(ShellTag);
            shell.unitValue = 80L;
            AttachSealTargets(shell);
            AttachBoxBehavior(shell);
            return shell;
        }

        internal static GameItem CreateSeal()
        {
            GameItem seal = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(seal, SpriteKeys.FirstAidSeal, "Items/items_tool", "label");
            seal.name = Loc.Get("item.first_aid_seal.name", "急救盒密封贴");
            seal.shortDescription = Loc.Get("item.first_aid_seal.desc",
                "手工密封贴。拖到装有药品的急救盒外壳上，按内部物品价值封装；未配齐会标出“未配齐”。");
            seal.flavorText = Loc.Get("item.first_aid_seal.flavor", "原厂不会承认这个。");
            seal.SetShape(new GridShapeBuilder(1, 1).SetDataFill(1).Build());
            seal.unitValue = 60L;
            seal.SetGameItemType("CHEMICAL_SUPPLIES");
            seal.mayThisTargetItemFunc = delegate (GameItem source, GameItem target)
            {
                return source != null && source.identifier == SealId && target != null
                    && target.identifier == ShellId && GeneralHelper.IsItemOwned(source)
                    && GeneralHelper.IsItemOwned(target);
            };
            seal.canThisTargetItemFunc = delegate (GameItem source, GameItem target)
            {
                return source != null && source.identifier == SealId && target != null
                    && target.identifier == ShellId && GeneralHelper.IsItemOwned(source)
                    && GeneralHelper.IsItemOwned(target);
            };
            seal.onThisTargetItemFunc = delegate (GameItem source, GameItem target)
            {
                if (source != null && source.identifier == SealId && target != null && target.identifier == ShellId)
                {
                    SealBox(target, source);
                }
            };
            ModHelper.SetSource(seal, Loc.ModId, ModEntry.ModDisplayName);
            return seal;
        }

        private static GameItem CreateContainerBase(string id, string spriteKey, string nameKey, string nameFallback,
            string descKey, string descFallback, string flavorKey, string flavorFallback)
        {
            (PixelWindow window, GameInventory inventory) = DirectoryUtils.CreateInventoryWindow(6, 5);
            GameItem item = ItemDirectory.CreateEmptyItem().SetContentWindow(window);
            ModSprites.Apply(item, spriteKey, "Items/items_container", "med_box");
            item.name = Loc.Get(nameKey, nameFallback);
            item.shortDescription = Loc.Get(descKey, descFallback);
            item.flavorText = Loc.Get(flavorKey, flavorFallback);
            item.SetGameItemType("HOUSEHOLD_GOOD");
            item.SetGameItemType("MEDICAL");
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.EnableTag("NESTABLE_DEVICE_TAG");
            item.SetShape(new GridShapeBuilder(3, 3).SetDataFill(1).Build());
            item.SyncModifiedState();
            item.unitValue = 100L;
            if (item.contentWindow != null)
            {
                item.contentWindow.isLocked = false;
                inventory.mayInventoryAddItemFunc = delegate (GameItem added, GameInventory parent)
                {
                    // The seal sticker is applied by dragging it onto the shell, not stored inside it.
                    return added != null && added.identifier != SealId;
                };
            }
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreateStockBox()
        {
            GameItem box = DirectoryMaster.Item(BoxId);
            // DirectoryMaster clones may keep the fallback atlas sprite; re-apply the mod sprite
            // and the exact 3x3 footprint after cloning.
            ModSprites.Apply(box, SpriteKeys.FirstAidBoxSprite, "Items/items_container", "med_box");
            box.SetShape(new GridShapeBuilder(3, 3).SetDataFill(1).Build());
            box.SyncModifiedState();
            ConfigureAuthenticity(box, true, false);
            FillContents(box, false);
            FirstAidTier.EnsureFeature(box);
            AttachBoxBehavior(box);
            try
            {
                Debug.Log("[PrecisionLab] first aid box visual spritePath=" + box.spritePath
                    + " mod=" + box.useSpriteFromMod + " shape=" + box.shape.width + "x" + box.shape.height);
            }
            catch (Exception) { }
            return box;
        }

        private static void ConfigureAuthenticity(GameItem box, bool genuine, bool advanced)
        {
            int value = 500;
            string serial = genuine
                ? UnityEngine.Random.Range(100, 1000).ToString("000") + UnityEngine.Random.Range(1000, 10000).ToString("0000")
                : (UnityEngine.Random.Range(0, 100) < 50
                    ? UnityEngine.Random.Range(100000, 1000000).ToString("000000")
                    : UnityEngine.Random.Range(0, 1000).ToString("000") + UnityEngine.Random.Range(0, 100).ToString("00") + "X");
            int red = 1;
            int sticker = 1;
            int brand = 1;
            int bg = 1;
            SetInt(box, GenuineTag, bg);
            SetString(box, SerialTag, serial);
            SetInt(box, RedCrossTag, red);
            SetInt(box, StickerTag, sticker);
            SetInt(box, BrandTag, brand);
            SetInt(box, ManualTag, 0);
            SetInt(box, IncompleteTag, 0);
            SetInt(box, AdvancedTag, advanced ? 1 : 0);
            SetInt(box, ValueTag, value);
            box.unitValue = value;
            box.name = Loc.Get("item.first_aid_box.name", "“Star Quiler’s Med”急救盒");
            box.flavorText = Loc.Get("item.first_aid_box.flavor", "Star Quiler Med: frontline first aid standard.");
        }

        private static void FillContents(GameItem box, bool advanced)
        {
            if (box == null || box.contentWindow == null)
            {
                return;
            }
            GameInventory inventory = box.contentWindow != null ? box.contentWindow.child as GameInventory : null;
            if (inventory == null)
            {
                Debug.LogWarning("[PrecisionLab] first aid content window has no inventory");
                return;
            }
            if (inventory.childItems.Count > 0)
            {
                return;
            }
            try
            {
                GeneralHelper.SetItemOwned(box, true);
                inventory.mayInventoryAddItemFunc = delegate (GameItem added, GameInventory parent) { return GeneralHelper.IsItemOwned(added); };
                GameGridInventory grid = inventory as GameGridInventory;
                Debug.Log("[PrecisionLab] first aid inventory type=" + inventory.GetType().Name
                    + " shape=" + (grid != null && grid.inventoryShape != null ? grid.inventoryShape.width + "x" + grid.inventoryShape.height : "null"));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] preparing first aid inventory failed: " + e.Message);
            }
            List<GameItem> items = new List<GameItem>();
            AddItem(items, ItemIds.Pen);
            AddItem(items, "makeshift_tourniquet");
            AddItem(items, "large_purple_injector");
            AddItem(items, "pure_white_injector");
            AddItem(items, "rubbing_alcohol");
            AddItem(items, advanced ? "blue_blood_bag" : "blood_bag");
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null) continue;
                GeneralHelper.SetItemOwned(item, true);
                int result = GraphUtils.TryAcceptAll(inventory, item);
                if (result != 0)
                {
                    Debug.LogWarning("[PrecisionLab] first aid content rejected " + item.identifier + " code=" + result
                        + " itemShape=" + (item.shape != null ? item.shape.width + "x" + item.shape.height : "null")
                        + " owned=" + GeneralHelper.IsItemOwned(item)
                        + " inv=" + (inventory is GameGridInventory g && g.inventoryShape != null ? g.inventoryShape.width + "x" + g.inventoryShape.height : "null"));
                    item.Destroy();
                }
            }
        }

        private static void AddItem(List<GameItem> list, string id)
        {
            try
            {
                GameItem item = DirectoryMaster.Item(id);
                if (item != null)
                {
                    list.Add(item);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] first aid content missing " + id + ": " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Behaviour: open/unseal, seal, magnifier
        // ------------------------------------------------------------------

        private static void AttachBoxBehavior(GameItem box)
        {
            try
            {
                box.canActivateSlotItemFunc = delegate (GameItem self, GameInventory inv, SlotMarker slot)
                {
                    GameInventory content = self.contentWindow != null ? self.contentWindow.child as GameInventory : null;
                    return GeneralHelper.IsItemOwned(self) && content != null && content.childItems.Count > 0;
                };
                box.mayActivateSlotItemFunc = delegate (GameItem self, GameInventory inv, SlotMarker slot)
                {
                    GameInventory content = self.contentWindow != null ? self.contentWindow.child as GameInventory : null;
                    return GeneralHelper.IsItemOwned(self) && content != null && content.childItems.Count > 0;
                };
                box.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inv, SlotMarker slot)
                {
                    EjectContents(self);
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] attaching first aid behavior failed: " + e.Message);
            }
        }

        private static void EjectContents(GameItem box)
        {
            try
            {
                GameInventory content = box.contentWindow != null ? box.contentWindow.child as GameInventory : null;
                if (content == null)
                {
                    return;
                }
                List<GameItem> copy = new List<GameItem>(content.childItems);
                for (int i = 0; i < copy.Count; i++)
                {
                    GameItem child = copy[i];
                    if (child == null) continue;
                    content.Expel(child);
                    GraphUtils.EmporiumTryAdd(child);
                }
                bool isShell = box.identifier == ShellId;
                if (isShell)
                {
                    // An empty shell is reusable; opening it only dumps whatever was inside.
                    Debug.Log("[PrecisionLab] first aid shell emptied (" + copy.Count + " items)");
                    return;
                }
                GameInventory parent = box.parentInventory;
                GameItem shell = DirectoryMaster.Item(ShellId);
                if (shell != null)
                {
                    GeneralHelper.SetItemOwned(shell, true);
                }
                box.Destroy();
                if (shell != null)
                {
                    if (parent != null)
                    {
                        int left = GraphUtils.TryAcceptAll(parent, shell);
                        if (left != 0)
                        {
                            GraphUtils.EmporiumTryAdd(shell);
                        }
                    }
                    else
                    {
                        GraphUtils.EmporiumTryAdd(shell);
                    }
                }
                Debug.Log("[PrecisionLab] first aid box dismantled (" + copy.Count + " items), empty shell returned=" + (shell != null));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] dismantling first aid box failed: " + e.Message);
            }
        }

        private static void AttachSealTargets(GameItem shell)
        {
            // Sealing is done by dragging the SEAL onto the SHELL (the natural direction).
            // The shell content inventory rejects seal items, so the drag reaches the target action.
            // The reverse (shell onto seal) is intentionally not wired.
        }

        private static void SealBox(GameItem shell, GameItem seal)
        {
            try
            {
                if (seal == null || seal.IsTag("FIRST_AID_SEAL_USED"))
                {
                    return;
                }
                seal.EnableTag("FIRST_AID_SEAL_USED");
                GameInventory content = shell.contentWindow != null ? shell.contentWindow.child as GameInventory : null;
                if (content == null)
                {
                    return;
                }
                List<GameItem> items = new List<GameItem>(content.childItems);
                int sum = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem item = items[i];
                    if (item == null) continue;
                    sum += (int)item.unitValue;
                }
                int tier = FirstAidTier.ComputeTier(items);
                bool incomplete = tier == 0;
                bool advanced = tier == 2;
                // A seal turns the shell + contents into a sellable sealed first aid box. Complete
                // and advanced boxes get a 10% "sealed kit" premium; incomplete kits do not.
                double sealedMultiplier = incomplete ? 0.9 : (advanced ? 2.2 : 1.1);
                int value = (int)Math.Round(sum * sealedMultiplier);
                SetInt(shell, ManualTag, 1);
                SetInt(shell, IncompleteTag, incomplete ? 1 : 0);
                SetInt(shell, AdvancedTag, advanced ? 1 : 0);
                SetInt(shell, StickerTag, 1);
                SetInt(shell, RedCrossTag, 1);
                SetInt(shell, BrandTag, 0);
                SetInt(shell, GenuineTag, 0);
                SetString(shell, SerialTag, "MANUAL-" + UnityEngine.Random.Range(1000, 10000).ToString("0000"));
                SetInt(shell, ValueTag, value);
                shell.unitValue = value;
                shell.identifier = BoxId;
                shell.DisableTag(ShellTag);
                ModSprites.Apply(shell, SpriteKeys.FirstAidBoxSprite, "Items/items_container", "med_box");
                shell.SetShape(new GridShapeBuilder(3, 3).SetDataFill(1).Build());
                shell.SyncModifiedState();
                FirstAidTier.EnsureFeature(shell);
                shell.name = advanced
                    ? Loc.Get("item.first_aid_box.advanced_name", "高级急救盒")
                    : Loc.Get("item.first_aid_box.manual_name", "急救盒（手工密封）");
                shell.shortDescription = incomplete
                    ? Loc.Get("item.first_aid_box.incomplete_desc", "手工密封急救盒：未配齐。价格按内部物品价值的 90% 计算。")
                    : Loc.Get("item.first_aid_box.manual_desc", "手工密封急救盒。可直接双击拆开，把内部物品单卖。");
                seal.Destroy();
                // A sealed box is no longer a shell target.
                shell.mayThisTargetItemFunc = null;
                shell.canThisTargetItemFunc = null;
                shell.onThisTargetItemFunc = null;
                Debug.Log("[PrecisionLab] first aid box sealed (incomplete=" + incomplete + " advanced=" + advanced + " value=" + value + ")");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] sealing first aid box failed: " + e.Message);
            }
        }

        private static bool Contains(List<GameItem> items, string id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].identifier == id)
                {
                    return true;
                }
            }
            return false;
        }

        internal static int GetCorrectValue(GameItem box)
        {
            return box != null ? GetInt(box, ValueTag) : 0;
        }

        private static int GetInt(GameItem item, string tag)
        {
            try
            {
                return item.GetTagReadonly(tag).GetInt();
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void SetInt(GameItem item, string tag, int value)
        {
            item.ModifyTag(tag, delegate (TagState state)
            {
                state.Enable().SetInt(value);
            });
        }

        private static void SetString(GameItem item, string tag, string value)
        {
            item.ModifyTag(tag, delegate (TagState state)
            {
                state.Enable().SetString(value);
            });
        }

        // ------------------------------------------------------------------
        // Inspection window
        // ------------------------------------------------------------------

        internal static void ShowInspection(GameItem box)
        {
            try
            {
                if (box == null || !IsBox(box))
                {
                    return;
                }
                string serial = "";
                try
                {
                    serial = box.GetTagReadonly(SerialTag).GetString() ?? "";
                }
                catch (Exception)
                {
                    serial = "";
                }
                bool genuine = GetInt(box, GenuineTag) == 1;
                bool manual = GetInt(box, ManualTag) == 1;
                bool red = GetInt(box, RedCrossTag) == 1;
                bool sticker = GetInt(box, StickerTag) == 1;
                bool brand = GetInt(box, BrandTag) == 1;
                bool incomplete = GetInt(box, IncompleteTag) == 1;
                bool advanced = GetInt(box, AdvancedTag) == 1;
                bool serialValid = serial.Length == 7 && IsDigits(serial);
                bool pass = genuine && red && sticker && brand && serialValid;
                int value = GetCorrectValue(box);
                int contentCount = 0;
                try
                {
                    GameInventory inv = box.contentWindow != null ? box.contentWindow.child as GameInventory : null;
                    contentCount = inv != null ? inv.childItems.Count : 0;
                }
                catch (Exception) { }

                string title = Loc.Get("ui.first_aid.title", "急救盒鉴定");
                string conclusion;
                if (manual)
                {
                    conclusion = incomplete
                        ? Loc.Get("ui.first_aid.manual_incomplete", "手工密封，未配齐：不是 Star Quiler 原厂，无法鉴定真伪。")
                        : Loc.Get("ui.first_aid.manual", "手工密封：不是 Star Quiler 原厂，无法鉴定真伪。");
                }
                else
                {
                    conclusion = pass
                        ? Loc.Get("ui.first_aid.genuine", "结论：正品。“Star Quiler’s Med”包装、编号、红十字和贴纸全部一致。")
                        : Loc.Get("ui.first_aid.fake", "结论：赝品。至少一项防伪特征不一致，只能按假货价格出售。");
                }
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Loc.Get("ui.first_aid.item", "物品：") + box.name);
                sb.AppendLine(Loc.Get("ui.first_aid.serial", "编号：") + (string.IsNullOrEmpty(serial) ? "-" : serial)
                    + "  " + (serialValid ? Loc.Get("ui.first_aid.serial_ok", "（7 位：3 位批次 + 4 位序号）") : Loc.Get("ui.first_aid.serial_bad", "（编号格式不符）")));
                sb.AppendLine(Loc.Get("ui.first_aid.red_cross", "红十字：") + (red ? Loc.Get("ui.first_aid.yes", "一致") : Loc.Get("ui.first_aid.no", "不一致")));
                sb.AppendLine(Loc.Get("ui.first_aid.sticker", "包装贴纸：") + (sticker ? Loc.Get("ui.first_aid.yes", "一致") : Loc.Get("ui.first_aid.no", "不一致")));
                sb.AppendLine(Loc.Get("ui.first_aid.brand", "品牌字样“Star Quiler’s Med”：") + (brand ? Loc.Get("ui.first_aid.yes", "一致") : Loc.Get("ui.first_aid.no", "不一致")));
                sb.AppendLine(Loc.Get("ui.first_aid.contents", "内部物品数：") + contentCount);
                sb.AppendLine(Loc.Get("ui.first_aid.advanced", "高级（蓝血）：") + (advanced ? Loc.Get("ui.first_aid.yes", "是") : Loc.Get("ui.first_aid.no", "否")));
                sb.AppendLine(Loc.Get("ui.first_aid.value", "估值：") + value + Loc.Get("ui.first_aid.credits", " 信用点"));
                sb.AppendLine();
                sb.Append(conclusion);
                ShowWindow(title, sb.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] showing first aid inspection failed: " + e.Message);
                try
                {
                    StoreUIManager.Instance.Notify(Loc.Get("ui.first_aid.error", "鉴定失败，请重试。"), "default");
                }
                catch (Exception) { }
            }
        }

        private static bool IsDigits(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9') return false;
            }
            return true;
        }

        private static void ShowWindow(string title, string text)
        {
            if (CustomUIManager.Instance == null)
            {
                StoreUIManager.Instance.Notify(text, "default");
                return;
            }
            CustomUIManager.Instance.CloseWindow("first_aid_inspection");
            CustomUIWindow window = CustomUIManager.Instance.CreateWindow("first_aid_inspection", title, "overlay")
                .SetSize(530f, 560f)
                .SetDraggable(true)
                .SetCloseOnEscape()
                .AddHeader(title)
                .AddSeparator()
                .Show();
            CustomUIBuilder builder = new CustomUIBuilder(CustomUIManager.Instance, window);
            Sprite itemSprite = ModSprites.Get(SpriteKeys.FirstAidBoxSprite);
            if (itemSprite != null)
            {
                builder.BeginRow(12f, TextAnchor.MiddleLeft);
                builder.AddImage(itemSprite, 96f, 96f);
                builder.BeginColumn(4f, TextAnchor.MiddleLeft);
                builder.AddHeader(Loc.Get("ui.first_aid.title", "急救盒鉴定"));
                builder.AddLabel(Loc.Get("ui.first_aid.verify_hint", "四项防伪特征全部一致才是正品。"));
                builder.End();
                builder.End();
            }
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                {
                    builder.AddLabel(lines[i]);
                }
            }            builder.AddSpace(8f);
            builder.AddButton(Loc.Get("ui.first_aid.close", "关闭"),
                delegate { CustomUIManager.Instance.CloseWindow("first_aid_inspection"); });
        }
    }
}
