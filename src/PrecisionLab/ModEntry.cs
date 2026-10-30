using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// The mod entry point. ModLoader finds this class (IMod), calls Init(manifest), then
    /// OnEnable() (and OnDisable() on game exit). The game's Harmony instance already ran
    /// PatchAll on the mod assembly before Init.
    ///
    /// Everything this mod adds:
    ///   * two items registered into ModItemDirectory (precision_liquid_extractor,
    ///     precision_purifier),
    ///   * two Wild Network / black-market upgrades that deliver the items into the player's
    ///     inventory,
    ///   * loot table patches (low chance in medical/junk loot),
    ///   * tooltip lines and a dev-panel spawn helper,
    ///   * a diagnostic self-test written to Player.log after a save is loaded.
    /// </summary>
    public sealed class ModEntry : IMod
    {
        internal const string ModId = "precision_lab";
        internal const string ModDisplayName = "Precision Lab Equipment";

        internal const int ExtractorUpgradeCost = 1200;
        internal const int PurifierUpgradeCost = 2400;
        internal const string UpgradeTab = "precision_lab";

        private static ModManifest _manifest;
        private static ModLog _log;
        private static ModConfig _config;
        private static ConfigEntry<int> _doseEntry;
        private static bool _selfTestDone;

        internal static int ConfigDoseUl
        {
            get
            {
                int value = _doseEntry != null ? _doseEntry.Value : 1000;
                return Mathf.Clamp(value, 1, LiquidTools.MaxDoseUl);
            }
        }

        internal static ModLog Log
        {
            get { return _log; }
        }

        // ------------------------------------------------------------------
        // IMod
        // ------------------------------------------------------------------
        public void Init(ModManifest manifest)
        {
            _manifest = manifest;
            ModIdCompat(manifest);
            _log = new ModLog(manifest);
            _log.Log("v" + manifest.ModVersion + " initializing (game " + Application.version + ")");

            try
            {
                _config = ModConfig.For(manifest.ID);
                _doseEntry = _config.Define(
                    "dose_ul",
                    1000,
                    "Precision Lab / 精密实验室",
                    () => Loc.Get("config.dose.label", "Default extractor metering (µL, 1-10000)"),
                    null,
                    null,
                    () => Loc.Get("config.dose.tooltip", "How many microlitres a newly crafted Precision Liquid Extractor draws/injects per use. You can also cycle presets in-game by activating the item."),
                    false);
                _config.Save();
                _log.Log("config loaded: dose_ul=" + ConfigDoseUl);
            }
            catch (Exception e)
            {
                _log.Warning("config setup failed: " + e.Message);
            }

            try
            {
                // Network upgrades must NOT be registered here: NetworkUpgrade.RegisterModUpgrade
                // touches PlayerStore.Instance, whose lazy getter *constructs* a PlayerStore if
                // none exists yet (PlayerStore.cs) — doing that during ModLoader.Awake makes the
                // game build its whole run state at boot and throws. We register the upgrades on
                // the first "game loaded / new game started" hook instead, when the real store
                // exists and before NetworkUpgrade.AddMissingAfterLoad runs.
                _log.Log("network upgrades deferred until a run is loaded");
            }
            catch (Exception e)
            {
                _log.Warning("deferred upgrade setup failed: " + e.Message);
            }
        }

        private static void ModIdCompat(ModManifest manifest)
        {
            if (manifest.ID != ModId)
            {
                UnityEngine.Debug.LogWarning("[PrecisionLab] manifest ID '" + manifest.ID + "' does not match the id compiled into the mod ('" + ModId + "'); localization and resources will not resolve.");
            }
        }

        public void OnEnable()
        {
            ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
            ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
            ModHook.OnGameLoadedInit += OnGameLoadedInit;
            ModHook.OnGameLoadedNormal += OnGameLoadedNormal;
            ModHook.OnNewGameStarted += OnNewGameStarted;
            ModHook.OnWakingUpLate += OnWakingUpLate;
            ModHook.OnCreateTooltipLate += OnCreateTooltipLate;
            ModHook.OnModConfigApplied += OnModConfigApplied;
            _log.Log("enabled");
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
            ModHook.OnGameLoadedInit -= OnGameLoadedInit;
            ModHook.OnGameLoadedNormal -= OnGameLoadedNormal;
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            ModHook.OnWakingUpLate -= OnWakingUpLate;
            ModHook.OnCreateTooltipLate -= OnCreateTooltipLate;
            ModHook.OnModConfigApplied -= OnModConfigApplied;
        }

        // ------------------------------------------------------------------
        // Item registration
        // ------------------------------------------------------------------
        private void OnModItemDirectoryInit(ModItemDirectory directory)
        {
            if (directory == null)
            {
                return;
            }
            try
            {
                // If another directory instance already carries the factories, keep them there
                // instead of registering a second factory set.
                if (DirectoryMaster.Has<GameItem>(ItemIds.Extractor) && DirectoryMaster.Has<GameItem>(ItemIds.Purifier))
                {
                    return;
                }
                bool extractor = directory.Has(ItemIds.Extractor) || directory.Add(ItemIds.Extractor, ItemFactory.CreateExtractor);
                bool purifier = directory.Has(ItemIds.Purifier) || directory.Add(ItemIds.Purifier, ItemFactory.CreatePurifier);
                if (extractor || purifier)
                {
                    _log.Log("items registered in mod item directory: extractor=" + extractor + " purifier=" + purifier);
                }
            }
            catch (Exception e)
            {
                _log.Error("item registration failed: " + e);
            }
        }

        private void OnModAssetsLoaded(LoadedMod mod)
        {
            if (mod == null || mod.Manifest == null || mod.Manifest.ID != ModId)
            {
                return;
            }
            _log.Log("assets loaded: item sprites=" + mod.Sprites.Count + " locales=" + mod.Localization.Count);
            RegisterDebugFunctions();
        }

        private void RegisterDebugFunctions()
        {
            try
            {
                ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_SPAWN, "PrecisionLab: spawn extractor",
                    () => GiveItem(ItemIds.Extractor));
                ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_SPAWN, "PrecisionLab: spawn purifier",
                    () => GiveItem(ItemIds.Purifier));
            }
            catch (Exception e)
            {
                _log.Warning("debug functions registration failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Acquisition
        // ------------------------------------------------------------------
        private static bool _upgradesRegistered;

        /// <summary>
        /// Registers the two black-market upgrades. MUST only run once a real PlayerStore
        /// exists: NetworkUpgrade.RegisterModUpgrade reads PlayerStore.Instance, whose lazy
        /// getter constructs a PlayerStore when none exists yet — and that constructor throws
        /// NullReferenceException during ModLoader.Awake (before any game scene is up).
        /// </summary>
        private static void TryRegisterUpgrades()
        {
            if (_upgradesRegistered)
            {
                return;
            }
            if (!PlayerStore.HasInstance)
            {
                _log.Log("network upgrades: player store not ready yet, will retry");
                return;
            }
            try
            {
                RegisterUpgrades();
                _upgradesRegistered = true;
            }
            catch (Exception e)
            {
                _log.Warning("network upgrade registration failed (will retry later): " + e);
            }
        }

        private static void RegisterUpgrades()
        {
            ModHelper.SetNetworkTabLabel(UpgradeTab, () => Loc.Get("upgrade.tab", "Precision Lab"));

            ModHelper.AddNetworkUpgrade(
                ModId,
                "PRECISION_LAB_EXTRACTOR",
                ExtractorUpgradeCost,
                "Precision Lab",
                () => Loc.Get("upgrade.extractor.title", "Precision Liquid Extractor"),
                () => Loc.Get("upgrade.extractor.desc", "A lab-grade metering tool that transfers liquid with 1 µL precision. Delivered to your inventory."),
                () => GiveItem(ItemIds.Extractor),
                UpgradeTab,
                null,
                null,
                0,
                () => Loc.Get("upgrade.cardtitle", "Precision Lab"),
                () => Loc.Get("upgrade.extractor.bought", "Ordered. Check your store inventory."),
                false);

            ModHelper.AddNetworkUpgrade(
                ModId,
                "PRECISION_LAB_PURIFIER",
                PurifierUpgradeCost,
                "Precision Lab",
                () => Loc.Get("upgrade.purifier.title", "Purification Machine"),
                () => Loc.Get("upgrade.purifier.desc", "Removes physical, chemical and biological contamination from liquid containers while the store is closed, without destroying valuable ingredients. Delivered to your inventory."),
                () => GiveItem(ItemIds.Purifier),
                UpgradeTab,
                null,
                null,
                0,
                () => Loc.Get("upgrade.cardtitle", "Precision Lab"),
                () => Loc.Get("upgrade.purifier.bought", "Ordered. Check your store inventory."),
                false);

            _log.Log("network upgrades registered (tab " + UpgradeTab + ")");
        }

        private void OnGameLoadedInit()
        {
            try
            {
                // Fires inside PlayerStore.LoadGame before NetworkUpgrade.AddMissingAfterLoad,
                // so anything registered here ends up in the loaded run's upgrade dictionary.
                TryRegisterUpgrades();
            }
            catch (Exception e)
            {
                _log.Warning("upgrade registration on game load failed: " + e.Message);
            }
        }

        private void OnNewGameStarted(string scenarioId)
        {
            try
            {
                TryRegisterUpgrades();
                EnsureItemsRegisteredForGameplay();
            }
            catch (Exception e)
            {
                _log.Warning("new game setup failed: " + e.Message);
            }
        }

        private void OnWakingUpLate()
        {
            // Catch-up for purifiers that were loaded/created without their instruction note.
            PurifierManualHelper.EnsureAllInStore();
        }

        private static void GiveItem(string itemId)
        {
            try
            {
                if (!DirectoryMaster.Has<GameItem>(itemId))
                {
                    // Defensive: a fresh run may reach the black market before any save-load
                    // hook ran. Make sure the factories are attached before spawning.
                    EnsureItemsRegisteredForGameplay();
                }
                GameItem item = ItemSpawner.Spawn(itemId);
                if (item == null)
                {
                    _log.Error("could not create item '" + itemId + "' (not registered?)");
                    return;
                }
                GeneralHelper.SetItemOwned(item, true);
                // EmporiumTryAdd spills into back inventory / showcase / front counter when the
                // regular inventory is full, instead of silently losing the purchased item.
                GraphUtils.EmporiumTryAdd(item);
                if (item == null || item.parentInventory == null)
                {
                    _log.Error("granted '" + itemId + "' but every store inventory was full; item was lost");
                }
                else
                {
                    // Purifier machines ship with their instruction paper in the note slot.
                    PurifierManualHelper.TryEnsure(item);
                    // The item is attached to a live inventory element now, so the water feature
                    // (purity display for the extractor) can be initialised safely.
                    try
                    {
                        if (item.IsTag(Tags.LiquidContainer))
                        {
                            WaterFeatureHelper.InitWaterFeature(item);
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Warning("water feature init after granting '" + itemId + "' failed: " + e.Message);
                    }
                    _log.Log("granted item: " + itemId);
                }
            }
            catch (Exception e)
            {
                _log.Error("granting '" + itemId + "' failed: " + e);
            }
        }

        // ------------------------------------------------------------------
        // Tooltips
        // ------------------------------------------------------------------
        private static void OnCreateTooltipLate(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null)
            {
                return;
            }
            try
            {
                if (item.identifier == ItemIds.Extractor)
                {
                    int dose = LiquidTools.GetDose(item);
                    int volume = WaterHelper.GetTotalVolume(item);
                    int capacity = WaterHelper.GetTotalCapacity(item);
                    int mode = LiquidTools.GetMode(item);
                    string modeName = mode == LiquidTools.ModeDraw
                        ? Loc.Get("ui.extractor.mode_draw", "Always draw")
                        : mode == LiquidTools.ModeInject
                            ? Loc.Get("ui.extractor.mode_inject", "Always inject")
                            : Loc.Get("ui.extractor.mode_auto", "Auto (empty draws / filled injects)");
                    builder.AddLine(
                        Loc.Fmt("tooltip.extractor.dose", "Metering: {0} µL (double-click to change)", dose),
                        false,
                        RenderHandler.ColorPalette.LightCyan);
                    builder.AddLine(
                        Loc.Fmt("tooltip.extractor.mode", "Mode: {0}", modeName),
                        false,
                        RenderHandler.ColorPalette.LightGreen);
                    builder.AddLine(
                        Loc.Fmt("tooltip.extractor.content", "Contents: {0} / {1} µL", volume, capacity),
                        false,
                        RenderHandler.ColorPalette.LightBlue);
                    string last = item.GetTagReadonly(Tags.ExtractorLastResult).GetString();
                    if (!string.IsNullOrEmpty(last))
                    {
                        builder.AddLine(last, true, RenderHandler.ColorPalette.LightGreen);
                    }
                    builder.AddLine(
                        Loc.Get("tooltip.extractor.hint", "Double-click the item to open the metering window (changes apply immediately). Auto mode: empty -> draws exactly N µL, filled -> injects exactly N µL; switch the mode to draw/inject to force a direction (e.g. top a half-full pipette back up). If less than N µL is available the attempt is refused (nothing moves)."),
                        true,
                        RenderHandler.ColorPalette.Gray);
                }
                else if (item.identifier == ItemIds.Purifier)
                {
                    int cap = LiquidTools.GetPurityCap(item);
                    int quality = MachineryHelper.GetCurrentQualityBonus(item);
                    int baseCap = MachinePurifier.GetMaxPurityCap(0);
                    double capDelta = (cap - baseCap) / 100.0;
                    builder.AddLine(
                        Loc.Fmt("tooltip.purifier.cap_detail", "Purity cap: {0:0.00}% (quality modules +{1}% -> +{2:0.00}%)",
                            cap / 100.0, quality, capDelta),
                        false,
                        RenderHandler.ColorPalette.LightCyan);
                    builder.AddLine(
                        Loc.Get("tooltip.purifier.cats", "Removes physical / chemical (including heavy metals) / biological contamination only."),
                        true,
                        RenderHandler.ColorPalette.LightGreen);
                    int removed = item.GetTagReadonly(Tags.PurifierRemoved).GetInt();
                    if (removed > 0)
                    {
                        builder.AddLine(
                            Loc.Fmt("tooltip.purifier.removed", "Contaminants removed this cycle: {0:0.###} mL", removed / 1000.0),
                            true,
                            RenderHandler.ColorPalette.LightGreen);
                    }
                    AppendPurifierSlotInfo(builder, item);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] tooltip hook failed: " + e.Message);
            }
        }

        private static void AppendPurifierSlotInfo(RichTextBuilder builder, GameItem machine)
        {
            GameSlotInventory slot = MachineHelper.GetSlot(machine, 2) as GameSlotInventory;
            GameItem container = slot != null ? slot.childItem : null;
            if (container == null)
            {
                builder.AddLine(
                    Loc.Get("tooltip.purifier.empty", "Insert a liquid container into the machine to start a purification cycle."),
                    true,
                    RenderHandler.ColorPalette.Gray);
                return;
            }
            int total = WaterHelper.GetTotalVolume(container);
            int purity = WaterHelper.GetWaterPurity(container);
            int physical = 0;
            int chemical = 0;
            int heavyMetal = 0;
            int biological = 0;
            for (int i = 0; i < LiquidTools.PhysicalContaminants.Length; i++)
            {
                physical += LiquidTools.GetLiquidAmount(container, LiquidTools.PhysicalContaminants[i]);
            }
            for (int i = 0; i < LiquidTools.ChemicalContaminants.Length; i++)
            {
                int amount = LiquidTools.GetLiquidAmount(container, LiquidTools.ChemicalContaminants[i]);
                if (LiquidTools.ChemicalContaminants[i] == "heavy_metal")
                {
                    heavyMetal += amount;
                }
                else
                {
                    chemical += amount;
                }
            }
            for (int i = 0; i < LiquidTools.BiologicalContaminants.Length; i++)
            {
                biological += LiquidTools.GetLiquidAmount(container, LiquidTools.BiologicalContaminants[i]);
            }
            builder.AddLine(
                Loc.Fmt("tooltip.purifier.container", "Container: {0}% water purity, {1} mL", (purity / 100.0).ToString("0.##"), LiquidTools.FormatUl(total)),
                false,
                RenderHandler.ColorPalette.LightCyan);
            builder.AddLine(
                Loc.Fmt("tooltip.purifier.classes", "Contamination: physical {0} mL / chemical {1} mL / heavy metals {2} mL / biological {3} mL",
                    LiquidTools.FormatUl(physical), LiquidTools.FormatUl(chemical), LiquidTools.FormatUl(heavyMetal), LiquidTools.FormatUl(biological)),
                true,
                RenderHandler.ColorPalette.LightTurquoise);
            int quality = MachineryHelper.GetCurrentQualityBonus(machine);
            int performance = MachineryHelper.GetCurrentPerformanceBonus(machine);
            int power = machine.GetTagReadonly("TOTAL_PERCENTAGE_POWER_BONUS_INT").GetInt();
            builder.AddLine(
                Loc.Fmt("tooltip.purifier.modules", "Module bonuses: performance +{0}% (more per cycle) / power +{1}% / quality +{2}% (purity cap)",
                    performance, power, quality),
                true,
                RenderHandler.ColorPalette.LightCyan);
            bool cleanWater = LiquidTools.IsWaterAndContaminantsOnly(container);
            builder.AddLine(
                cleanWater
                    ? Loc.Get("tooltip.purifier.mode_clean", "Mode: clean water (removed contamination becomes water, volume unchanged).")
                    : Loc.Get("tooltip.purifier.mode_conc", "Mode: concentrate (removed contamination is discarded, valuable liquids are kept)."),
                true,
                RenderHandler.ColorPalette.LightGreen);
        }

        private void OnModConfigApplied(string modId)
        {
            if (modId == ModId)
            {
                _log.Log("config applied: dose_ul=" + ConfigDoseUl);
            }
        }

        // ------------------------------------------------------------------
        // Save loaded: fallback registration + diagnostics
        // ------------------------------------------------------------------
        private void OnGameLoadedNormal()
        {
            try
            {
                TryRegisterUpgrades();
                EnsureItemsRegisteredForGameplay();
            }
            catch (Exception e)
            {
                _log.Warning("game-loaded setup failed: " + e.Message);
            }

            // Purifiers loaded from a save (or spawned before the spawn patch ran) get their
            // instruction note here; spawns after this are handled by PurifierSpawnPatch.
            PurifierManualHelper.EnsureAllInStore();

            if (_selfTestDone)
            {
                return;
            }
            _selfTestDone = true;
            SelfTest.Run(_log);
        }

        /// <summary>
        /// The normal path registers items when ModItemDirectory wakes up. If that event was
        /// missed (e.g. another mod replaced the UI root, or the directory object was
        /// destroyed), attaching our own directory component keeps item creation working.
        /// A subclass is used so DirectoryMaster does not treat it as a duplicate of the
        /// vanilla ModItemDirectory type.
        /// </summary>
        private static void EnsureItemsRegisteredForGameplay()
        {
            if (DirectoryMaster.Has<GameItem>(ItemIds.Extractor) && DirectoryMaster.Has<GameItem>(ItemIds.Purifier))
            {
                return;
            }
            MonoBehaviour host = ModLoader.Instance;
            if (host == null)
            {
                _log.Warning("no ModLoader instance to host the fallback item directory");
                return;
            }
            _log.Warning("mod items are missing from DirectoryMaster; attaching a fallback ModItemDirectory");
            host.gameObject.AddComponent<ModItemDirectoryFallback>();
        }
    }

    /// <summary>
    /// Separate type (not ModItemDirectory exactly) so DirectoryMaster's duplicate-type check
    /// accepts it even if a vanilla ModItemDirectory instance is still registered. It inherits
    /// ModItemDirectory.InitDirectory, which fires ModHook.OnModItemDirectoryInit, so the
    /// normal registration path runs for this instance too.
    /// </summary>
    internal sealed class ModItemDirectoryFallback : ModItemDirectory
    {
    }
}
