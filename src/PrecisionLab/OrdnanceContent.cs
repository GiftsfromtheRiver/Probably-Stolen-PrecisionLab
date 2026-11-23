using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Military ordnance chain built around the vanilla 3D printer:
    ///
    ///   printer chips print EMPTY shells (printer plastic + scrap metal)
    ///   chemistry produces the active fillings (flash / smoke / plastic explosive)
    ///   drag a filling onto the matching printed shell -> final item
    ///
    ///   flash shell + flash filling   -> vanilla flashbang_grenade
    ///   smoke shell + smoke filling   -> vanilla smoke_grenade
    ///   charge shell + explosive fill -> C4-like plastic explosive charge (no exact vanilla item)
    /// </summary>
    internal static class OrdnanceContent
    {
        internal const string VanillaFlashbangId = "flashbang_grenade";
        internal const string VanillaSmokeId = "smoke_grenade";
        internal const string VanillaC4Id = "c4";
        internal const int PressVolume = 5000;

        internal const string ShellTag = "ORDNANCE_SHELL_TAG";
        internal const string ShellKindTag = "ORDNANCE_SHELL_KIND";
        internal const string FillingTag = "ORDNANCE_FILLING_TAG";
        internal const string FillingKindTag = "ORDNANCE_FILLING_KIND";

        internal static void EnsureRecipes()
        {
            try
            {
                RegisterRecipe(VanillaFlashbangId, CustomLiquids.FlashPowder,
                    "item.flashbang.recipe_name", "闪光弹");
                RegisterRecipe(VanillaSmokeId, CustomLiquids.SmokeComposition,
                    "item.smoke_grenade.recipe_name", "烟雾弹");
                RegisterRecipe(VanillaC4Id, CustomLiquids.PlasticPutty,
                    "item.plastic_explosive.recipe_name", "塑性炸药");
                Debug.Log("[PrecisionLab] ordnance direct-press recipes registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering ordnance recipes failed: " + e.Message);
            }
        }

        private static void RegisterRecipe(string identifier, string activeLiquid, string nameKey, string fallback)
        {
            if (!DirectoryMaster.Has<GameItem>(identifier))
            {
                Debug.LogWarning("[PrecisionLab] ordnance: missing vanilla item " + identifier);
                return;
            }
            ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(identifier);
            if (recipe == null)
            {
                recipe = new ChemFinisherRecipe { identifier = identifier };
                ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
            }
            recipe.displayName = Loc.Get(nameKey, fallback);
            recipe.requiredVolume = PressVolume;
            recipe.isRequireDry = true;
            recipe.activeIngredientId = activeLiquid;
        }
        // ------------------------------------------------------------------ printer chips

        internal static GameItem CreateFlashbangChip()
        {
            return CreateChip(ItemIds.FlashbangShell, "item.flashbang_chip.name", "闪光弹外壳蓝图",
                "item.flashbang_chip.desc", "Put this chip in a 3D printer to print an empty flashbang shell. Consumes printer plastic and scrap metal.");
        }

        internal static GameItem CreateSmokeChip()
        {
            return CreateChip(ItemIds.SmokeShell, "item.smoke_chip.name", "烟雾弹外壳蓝图",
                "item.smoke_chip.desc", "Put this chip in a 3D printer to print an empty smoke grenade shell. Consumes printer plastic and scrap metal.");
        }

        internal static GameItem CreateChargeChip()
        {
            return CreateChip(ItemIds.ExplosiveShell, "item.charge_chip.name", "塑性炸药外壳蓝图",
                "item.charge_chip.desc", "Put this chip in a 3D printer to print an empty explosive charge shell. Consumes printer plastic and scrap metal.");
        }

        private static GameItem CreateChip(string shellId, string nameKey, string nameFallback,
            string descKey, string descFallback)
        {
            GameItem chip = ItemDirectory.CreateEmptyItem();
            chip.SetSpriteAndShape("Items/items_tool", "printer_chip2");
            chip.name = Loc.Get(nameKey, nameFallback);
            chip.shortDescription = Loc.Get(descKey, descFallback);
            chip.unitValue = 150L;
            chip.EnableTag("PRINTER_CHIP_TAG");
            MachineProgressHelper.SetSingleAdditionalRequiredItem(chip, "printer_plastic", 2);
            MachineProgressHelper.InitProgressSourceItem(chip, 20, shellId);
            AudioHelper.InitSmallModule(chip);
            ModHelper.SetSource(chip, Loc.ModId, ModEntry.ModDisplayName);
            return chip;
        }

        // ------------------------------------------------------------------ shells

        internal static GameItem CreateFlashbangShell()
        {
            return CreateShell(ItemIds.FlashbangShell, "flash", "item.flashbang_shell.name", "闪光弹空壳",
                "item.flashbang_shell.desc", "An empty printed flashbang shell. Drag a flash filling onto it to finish the grenade.",
                "Items/itematlas", "small_flash");
        }

        internal static GameItem CreateSmokeShell()
        {
            return CreateShell(ItemIds.SmokeShell, "smoke", "item.smoke_shell.name", "烟雾弹空壳",
                "item.smoke_shell.desc", "An empty printed smoke grenade shell. Drag a smoke filling onto it to finish the grenade.",
                "Items/itematlas", "small_smoke");
        }

        internal static GameItem CreateExplosiveShell()
        {
            return CreateShell(ItemIds.ExplosiveShell, "charge", "item.charge_shell.name", "塑性炸药空壳",
                "item.charge_shell.desc", "An empty printed explosive charge shell. Drag an explosive filling onto it to finish the charge.",
                "Items/itematlas", "c4");
        }

        private static GameItem CreateShell(string shellId, string kind, string nameKey, string nameFallback,
            string descKey, string descFallback, string atlas, string sprite)
        {
            GameItem shell = ItemDirectory.CreateEmptyItem();
            shell.SetSpriteAndShape(atlas, sprite);
            shell.name = Loc.Get(nameKey, nameFallback);
            shell.shortDescription = Loc.Get(descKey, descFallback);
            shell.SetGameItemType("MATERIAL");
            shell.EnableTag(ShellTag);
            shell.ModifyTag(ShellKindTag, delegate (TagState state)
            {
                state.Enable().SetString(kind);
            });
            shell.unitValue = 40L;
            AttachShellTargets(shell);
            ModHelper.SetSource(shell, Loc.ModId, ModEntry.ModDisplayName);
            return shell;
        }

        // ------------------------------------------------------------------ fillings

        internal static GameItem CreateFlashFilling()
        {
            return CreateFilling("flash", "item.flash_filling.name", "闪光药剂芯",
                "item.flash_filling.desc", "The active filling for a printed flashbang shell. Drag it onto the shell.");
        }

        internal static GameItem CreateSmokeFilling()
        {
            return CreateFilling("smoke", "item.smoke_filling.name", "发烟药剂芯",
                "item.smoke_filling.desc", "The active filling for a printed smoke grenade shell. Drag it onto the shell.");
        }

        internal static GameItem CreateExplosiveFilling()
        {
            return CreateFilling("charge", "item.explosive_filling.name", "塑性炸药药芯",
                "item.explosive_filling.desc", "The active filling for a printed explosive charge shell. Drag it onto the shell.");
        }

        private static GameItem CreateFilling(string kind, string nameKey, string nameFallback,
            string descKey, string descFallback)
        {
            GameItem filling = ItemDirectory.CreateEmptyItem();
            string spriteKey;
            switch (kind)
            {
                case "flash":
                    spriteKey = SpriteKeys.FillingFlash;
                    break;
                case "smoke":
                    spriteKey = SpriteKeys.FillingSmoke;
                    break;
                default:
                    spriteKey = SpriteKeys.FillingExplosive;
                    break;
            }
            ModSprites.Apply(filling, spriteKey, "Items/items_material", "chemical2");
            filling.SetShape(new GridShapeBuilder(1, 1).SetDataFill(1).Build());
            filling.name = Loc.Get(nameKey, nameFallback);
            filling.shortDescription = Loc.Get(descKey, descFallback);
            filling.SetGameItemType("MATERIAL");
            ChemicalHelper.InitChemItem(filling, isBlendable: true, isSoluble: true);
            filling.EnableTag(FillingTag);
            filling.ModifyTag(FillingKindTag, delegate (TagState state)
            {
                state.Enable().SetString(kind);
            });
            filling.unitValue = 60L;
            AttachFillingTargets(filling);
            ModHelper.SetSource(filling, Loc.ModId, ModEntry.ModDisplayName);
            return filling;
        }

        /// <summary>
        /// The game invokes both the dragged item's and the target's onItemTargetItemFunc. Attach the
        /// assembly action to the SHELL only, so dragging either way assembles exactly once; attach
        /// only may/can target checks to the filling so the drag is accepted.
        /// </summary>
        private static void AttachShellTargets(GameItem shell)
        {
            shell.mayItemTargetItemFunc = delegate (GameItem s, GameItem t)
            {
                return GeneralHelper.IsItemOwned(s) && TryGetAssembly(s, t, out _, out _);
            };
            shell.canItemTargetItemFunc = delegate (GameItem s, GameItem t)
            {
                return GeneralHelper.IsItemOwned(s) && TryGetAssembly(s, t, out _, out _);
            };
            shell.onItemTargetItemFunc = delegate (GameItem s, GameItem t)
            {
                GameItem realShell;
                GameItem realFilling;
                if (TryGetAssembly(s, t, out realShell, out realFilling))
                {
                    Assemble(realShell, realFilling);
                }
            };
        }

        private static void AttachFillingTargets(GameItem filling)
        {
            filling.mayItemTargetItemFunc = delegate (GameItem s, GameItem t)
            {
                return GeneralHelper.IsItemOwned(s) && TryGetAssembly(s, t, out _, out _);
            };
            filling.canItemTargetItemFunc = delegate (GameItem s, GameItem t)
            {
                return GeneralHelper.IsItemOwned(s) && TryGetAssembly(s, t, out _, out _);
            };
        }

        private static bool TryGetAssembly(GameItem s, GameItem t, out GameItem shell, out GameItem filling)
        {
            shell = null;
            filling = null;
            if (s == null || t == null)
            {
                return false;
            }
            if (s.IsTag(ShellTag) && t.IsTag(FillingTag))
            {
                shell = s;
                filling = t;
            }
            else if (t.IsTag(ShellTag) && s.IsTag(FillingTag))
            {
                shell = t;
                filling = s;
            }
            else
            {
                return false;
            }
            string shellKind = shell.GetTagReadonly(ShellKindTag).GetString();
            string fillingKind = filling.GetTagReadonly(FillingKindTag).GetString();
            return !string.IsNullOrEmpty(shellKind) && shellKind == fillingKind;
        }

        /// <summary>Rejects a filling whose actual chemical content does not match its shell.</summary>
        private static bool HasCorrectComposition(GameItem filling, string kind)
        {
            string expected;
            switch (kind)
            {
                case "flash":
                    expected = CustomLiquids.FlashPowder;
                    break;
                case "smoke":
                    expected = CustomLiquids.SmokeComposition;
                    break;
                default:
                    expected = CustomLiquids.PlasticPutty;
                    break;
            }
            string part = Liquid.GetCurrentPartFromId(expected);
            int active = part != null ? filling.GetTagReadonly(part).GetInt() : 0;
            int total = ChemicalHelper.GetTotalVolume(filling);
            if (total <= 0)
            {
                total = WaterHelper.GetTotalVolume(filling);
            }
            if (active <= 0 || total <= 0)
            {
                return false;
            }
            return active * 100.0 / total >= 99.0;
        }

        private static void Assemble(GameItem shell, GameItem filling)
        {
            string kind = shell.GetTagReadonly(ShellKindTag).GetString();
            if (!HasCorrectComposition(filling, kind))
            {
                try
                {
                    StoreUIManager.Instance.Notify(
                        Loc.Get("mech.ordnance.wrong_composition",
                            "成分不对：这个药芯与外壳不匹配，拒绝组装。"),
                        "default");
                }
                catch (Exception)
                {
                    // Cosmetic only.
                }
                Debug.LogWarning("[PrecisionLab] ordnance assembly rejected: wrong composition for " + kind);
                return;
            }
            string finalId;
            switch (kind)
            {
                case "flash":
                    finalId = VanillaFlashbangId;
                    break;
                case "smoke":
                    finalId = VanillaSmokeId;
                    break;
                default:
                    finalId = VanillaC4Id;
                    break;
            }
            GameItem finalItem = DirectoryMaster.Item(finalId);
            if (finalItem == null)
            {
                return;
            }
            GameInventory parent = shell.parentInventory ?? filling.parentInventory;
            if (parent != null)
            {
                GraphUtils.EmporiumTryAdd(parent, finalItem);
            }
            if (finalItem.parentInventory == null)
            {
                // Parent full / too small: try the emporium root instead of deleting the result.
                GraphUtils.EmporiumTryAdd(finalItem);
            }
            if (finalItem.parentInventory != null)
            {
                filling.Destroy();
                shell.Destroy();
                Debug.Log("[PrecisionLab] assembled ordnance: " + finalId);
            }
            else
            {
                finalItem.Destroy();
            }
        }

        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.ReagentKitOrdnance, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item.ordnance_box.name", "军需原料盒");
            item.shortDescription = Loc.Get(
                "item.ordnance_box.desc",
                "激活拆开得到一批军需原料：氧化剂 20 毫升 ×2、金属粉末 20 毫升、硝化酸 20 毫升、糖 20 毫升、酒精 20 毫升、脂合剂 20 毫升 ×2、稳定剂 20 毫升，。");
            item.flavorText = Loc.Get("item.ordnance_box.flavor", "高违禁品。没有军队会承认这个箱子存在。");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            ContrabandHelper.InitContrabandItem(item, 3);
            item.unitValue = 450L;
            item.canActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self);
            };
            item.mayActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self);
            };
            item.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                try
                {
                    GameItem[] bottles =
                    {
                        AdrenalineContent.CreateBottle("med_bottle_blue", CustomLiquids.Oxidizer, 20000, "item.oxidizer_bottle.name", "Oxidizer Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", CustomLiquids.Oxidizer, 20000, "item.oxidizer_bottle.name", "Oxidizer Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_orange", CustomLiquids.MetalPowder, 20000, "item.metal_powder_bottle.name", "Metal Powder Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", CustomLiquids.NitratingAcid, 20000, "item.nitrating_acid_bottle.name", "Nitrating Acid Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", "sugar", 20000, "item.sugar_bottle.name", "Sugar (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_small_pink", "lipobond_agent", 20000, "item.lipobond_bottle.name", "Lipobond Agent Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_small_pink", "lipobond_agent", 20000, "item.lipobond_bottle.name", "Lipobond Agent Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_violet", "stabilizing_agent", 20000, "item.stabilizing_bottle.name", "Stabilizing Agent Bottle (20 mL)")
                    };
                    int placed = 0;
                    for (int i = 0; i < bottles.Length; i++)
                    {
                        GameItem bottle = bottles[i];
                        if (bottle == null) continue;
                        GeneralHelper.SetItemOwned(bottle, true);
                        if (inventory != null) GraphUtils.EmporiumTryAdd(inventory, bottle);
                        else GraphUtils.EmporiumTryAdd(bottle);
                        if (bottle.parentInventory != null) placed++;
                    }
                    self.Destroy();
                    Debug.Log("[PrecisionLab] ordnance kit unpacked (" + placed + "/" + bottles.Length + " items)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] unpacking ordnance kit failed: " + e.Message);
                }
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        // ------------------------------------------------------------------ raw reagent bottles

        internal static GameItem CreateOxidizerBottle()
        {
            return CreateBottle("med_bottle_blue", CustomLiquids.Oxidizer, 20000,
                "item.oxidizer_bottle.name", "氧化剂瓶（20 毫升）",
                "item.oxidizer_bottle.desc", "A 20 mL bottle of oxidizer used by flash and smoke compositions.", 45L);
        }

        internal static GameItem CreateMetalPowderBottle()
        {
            return CreateBottle("med_bottle_orange", CustomLiquids.MetalPowder, 20000,
                "item.metal_powder_bottle.name", "金属粉末瓶（20 毫升）",
                "item.metal_powder_bottle.desc", "A 20 mL suspension of finely divided metal powder for flash compositions.", 60L);
        }

        internal static GameItem CreateNitratingAcidBottle()
        {
            return CreateBottle("med_bottle_red", CustomLiquids.NitratingAcid, 20000,
                "item.nitrating_acid_bottle.name", "硝化酸瓶（20 毫升）",
                "item.nitrating_acid_bottle.desc", "A 20 mL bottle of nitrating acid. Keep below 40 C or the batch runs away.", 90L);
        }

        private static GameItem CreateBottle(string spriteKey, string liquidId, int volume, string nameKey,
            string nameFallback, string descKey, string descFallback, long value)
        {
            GameItem bottle = AdrenalineContent.CreateBottle(spriteKey, liquidId, volume, nameKey, nameFallback);
            if (bottle == null)
            {
                return null;
            }
            bottle.name = Loc.Get(nameKey, nameFallback);
            bottle.shortDescription = Loc.Get(descKey, descFallback);
            bottle.unitValue = value;
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }
    }
}
