# 04 — Verification: launching, logging, mod loading, trust and item spawning

Status: **PASS (2026-10-30, game 0.46D / Unity 2021.3.45f2, Windows, Steam running)**
Verifier: agent "verifier". Game install: `F:\SteamLibrary\steamapps\common\Probably Stolen`, Steam appid **4348910**.
Final frozen mod build verified: `PrecisionLab.dll` 50688 B, sha256
`77f2720b4308928ec60f3e0ea7f6bea1279646c7515f5e3f08d2d6996f31c847`, combinedHash
`35687f61f5bfed9faa9380cc7861d329eda875899baca56043398ab3db6b0a0f` (input-box CustomUI
metering-window build, META-style element prefabs so the slider renders, 760×440 default frame on
the `overlay` layer; Harmony prefix on `ItemMouseDoubleClickHandler.DoubleClickAction`).
window on the `overlay` layer, default frame, 700×440, `Center()`).

This document is the reference for "did the mod actually load and work?" It covers the log paths,
the exact launch recipe, the mod-loader/trust pipeline, how to spawn items in-game, and the
repeatable verification harness in `tools/verify/`.

---

## 1. TL;DR verdict

- The game boots headless-of-human fine from PowerShell, writes a full Unity log to a chosen path,
  reaches the main menu in ~15 s, and can be closed cleanly (WM_CLOSE).
- `[ModLoader]` runs **at boot, before any save is selected** — a local mod's `Init`/`OnEnable`,
  Harmony patches and asset/locale/loot loading can all be verified without loading a save.
- A local mod is loaded only when **enabled in `SaveFile.es3` (`modOrder`) AND trusted
  (`modTrust` record)**. A populated trust record makes it load with **no user prompt**; with no
  record the mod is only *registered as disabled* (`trust=NotChecked`) until the user enables it in
  the in-game Mods menu (first enable runs the capability scanner and may prompt for approval).
- **Steam Cloud overwrites `SaveFile.es3` at launch** if it differs from the synced copy. Seed the
  file and launch immediately with it temporarily **read-only**, then clear the attribute — see §5.
- Dev tools are unlockable with an empty file `dev.txt` next to `Probably Stolen.exe`:
  **F9 = Debug Panel** (in store scene). Mods can add entries there; the real mod contributes
  *"PrecisionLab: spawn extractor"* / *"…: spawn purifier"*, and the panel has **Open Debug Spawner**
  to spawn any item id into the store.
- **Final build window verification (PASS):** the CustomUI metering window is now **visibly rendered**
  (screenshot `evidence/ingame-10-customui-visible-window.png`): title 精确取液器 · 量程设置,
  量程 input field, 滑条 slider, six preset buttons (1 µL / 10 µL / 100 µL / 1 mL / 5 mL / 10 mL),
  内容物 line, 关闭/应用 buttons. Window id `precision_lab:extractor_settings`, layer `overlay`,
  `IsAlive/IsVisible true`, root rect `sizeDelta=(700,440)`, native fallback not opened; end-to-end
  input test: `3333` → Apply → tag `PRECISION_EXTRACTOR_DOSE_UL=3333`, reopen `250` → Apply → `250`,
  Apply closes the window. Cosmetic note: the 700×440 window is anchored slightly right of centre,
  so its right edge/close button sits at the viewport edge (no content lost).
- Historical note: making the window visible required fixing two separate things found in earlier
  rounds — (1) store-scene CustomUI layer must be `overlay` (the `meta` layer is not rendered on top
  in `InventoryScene`), and (2) the 4th `CreateWindow` `windowPrefabOverride` argument must NOT be
  `CustomUIManager.optionUIPrefab` (it produced a 10×10 invisible root); the default frame +
  `SetSize(700,440)` renders correctly (`evidence/ingame-09-default-overlay-test.png` control window
  proved the overlay canvas itself was fine).

---

## 2. Log locations

| What | Path / how |
|---|---|
| Unity log (default) | `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\Player.log` (rotated to `Player-prev.log`) |
| Unity log (captured run) | `Probably Stolen.exe -logFile "<path>"` writes **only** that file; `Player.log` is left untouched. Paths with spaces work when the whole argument is passed as one quoted string (see §3). |
| Game's own log | same LocalLow folder, `GameLog_YYYY-MM-DD_HH-MM-SS.txt` (timestamped mod/game lines incl. exceptions) |
| Mod trust cache | same folder, `mod-trust-list.json` (signed payload: `blocked[]`, `verified[]`, `killSwitch`) |
| Saves | same folder: `save_<slot>.es3`, `save_<slot>_day<N>_<stamp>.es3` (history), `saves_index.es3`, `SaveFile.es3` (options + `modOrder` + `modTrust`) |
| Mod settings | same folder, `ModConfigs\<mod_id>.json` |

Keep captures in `tools/verify/logs/`, screenshots in `tools/verify/evidence/`, backups in
`tools/verify/snapshots/`. **Never test with the user's real save slots** — copy one to a scratch
file first (see §8).

---

## 3. Exact launch recipe (PowerShell 5.1, proven)

```powershell
$game = 'F:\SteamLibrary\steamapps\common\Probably Stolen'
$log  = 'C:\Temp\psmod\run.log'                 # no-space path is safest
$argsString = '-logFile "' + $log + '"'         # ONE string, quoted path
$p = Start-Process -FilePath "$game\Probably Stolen.exe" -ArgumentList $argsString `
                   -WorkingDirectory $game -PassThru
```

Facts learned the hard way:

- **Steam must be running** (`D:\Steam\steam.exe` here). The exe is Steam-DRM wrapped:
  `Start-Process` returns a process that **exits immediately with code 53**, and Steam relaunches
  the real game as a child of `steam.exe`, **preserving the `-logFile` argument**. Do not treat the
  starter's exit code as a failure.
- **Argument quoting matters.** In Windows PowerShell 5.1 passing `-ArgumentList @('-logFile','"C:\p a\x.log"')`
  (array with embedded quotes) double-escapes the path; Unity then quietly writes no log and can
  exit early. Use a **single argument string** as above.
- Timeline observed: real game process ≈ +4 s, log file created ≈ +5 s, `[ModLoader]`/audit sentinel
  ≈ +15 s, main menu ready (window title `Probably Stolen`) ≈ +18 s.
- **Graceful quit**: post `WM_CLOSE` (0x0010) to the game window handle; it exits within seconds and
  writes Unity's shutdown block. Never `Stop-Process` Steam or unrelated processes.
- `-batchmode`/`-nographics` are **not** viable (the game needs D3D + Steam overlay path).
- Boot sentinels that mean "loader ran / main menu reached":
  `[ModLoader] audit: game 0.46D` and `[AchievementTracker] SteamManager.Initialized = True`.
  There is no explicit "main menu" line in the log.

`tools/verify/run-game.ps1` wraps all of this: it refuses to start if a game instance is already
running, launches with the log path, waits for the sentinel / `-WaitForMod <id>` audit line, and can
optionally quit (`-Quit`) or leave the game running for UI work. Example:

```powershell
& .\run-game.ps1 -LogPath C:\Temp\psmod\run.log -WaitForMod precision_lab -TimeoutSeconds 120
& .\run-game.ps1 -LogPath C:\Temp\psmod\run.log -Quit          # boot then close
```

> Windows may block unsigned `.ps1` files from the workspace drive. Run with
> `powershell -NoProfile -ExecutionPolicy Bypass -File .\run-game.ps1 …`, or
> `Set-ExecutionPolicy -Scope Process Bypass -Force` inside the current session.

---

## 4. Expected-good log evidence (real captured excerpt)

From the verified frozen build (`tools/verify/logs/final3.log`, short scratch-copy save):

```
[ModLoader] 2 mod(s) enabled
[ModLoader] Found 1 mod folder(s) in F:/…/Probably Stolen/Probably Stolen_Data\..\Mods
[ModLoader] Loading: PrecisionLab.dll
[ModLoader]   Harmony: 0 method(s) patched
[Precision Lab Equipment / 精密实验室设备]: v1.0.0 initializing (game 0.46D)
[Precision Lab Equipment / 精密实验室设备]: config loaded: dose_ul=1000
[Precision Lab Equipment / 精密实验室设备]: network upgrades deferred until a run is loaded
[Precision Lab Equipment / 精密实验室设备]: enabled
[ModLoader]   Initialized: Precision Lab Equipment / 精密实验室设备 v1.0.0
[ModLoader] …: 3 item sprite(s), 2 locale(s) [68–72 entries], 1 loot file(s): 2 patch(es) loaded
[ModLoader] audit: game 0.46D, trust list: issued … kill switch off
[ModLoader] audit: precision_lab v1.0.0 source=Local workshop=0 owner=0 running=True trust=Match caps=none hash=1fd55ebc…
[DevTools] Developer tools enabled: F9 debug panel and F8 console.       (only when dev.txt exists)
[AchievementTracker] SteamManager.Initialized = True
```

Item registration (fires when the game's mod item directory wakes, main-menu/UI stage):

```
[Precision Lab Equipment / 精密实验室设备]: items registered in mod item directory: extractor=True purifier=True
```

After loading a save (self-test + Wild Network):

```
[Precision Lab Equipment / 精密实验室设备]: network upgrades registered (tab precision_lab)
[PrecisionLab] SELFTEST items extractor='精确取液器' capacity=10000µL dose=1000µL window=True | purifier='提纯机器' power=6 qualityCap=9900bp
[PrecisionLab] SELFTEST exact transfer: moved=1234µL targetTotal=1234µL … sourceTotal=7066µL …
[PrecisionLab] SELFTEST exact-or-nothing: overRequest=0µL targetVolume=1234µL (expect unchanged 1234)
[PrecisionLab] SELFTEST purify/clean water: mode=CleanWater removed=1037µL total=7066->7066µL water=5959->6996µL purity=8433->9900bp cap=9900bp
[PrecisionLab] SELFTEST purify/concentrate: mode=Concentrate removed=500µL total=7500->7000µL product=4000->4000µL purity=4000->4285bp contaminants=500->0µL
[PrecisionLab] SELFTEST metering dial: window=True slider=True sliderWritesDose=True doseNow=1000µL
[PrecisionLab] SELFTEST OK exact=True refuseOverdose=True window=True slider=True cleanWater=True concentrate=True
```

Native extractor content window (pipette settings) — final build uses a **CustomUI input window**,
not the machinery window: double-clicking (or the equivalent native `DoubleClickAction`) opens
`precision_lab:extractor_settings` via a Harmony prefix. Verified in-game:
`WINDOW/ CUSTOMWINDOW opened mgr=True windowNull=False IsAlive=True IsVisible=True
id='precision_lab:extractor_settings' layer='meta' nativeFallbackVisible=False`, then
`elements input=True slider=True apply=True(button=True) close=True`;
`tagTest#1 inputSet='3333' inputEcho='3333' doseTag=3333`, reopen, `tagTest#2 inputSet='250'
inputEcho='250' doseTag=250`. The machinery PixelWindow remains only as a fallback (and now uses
`MachineryPanelRow.SetColumns(2)` = 2×2 grid if it ever opens).
Screenshot capture caveat: in our automated store-scene runs (loaded save at day-start with the
tablet/report overlay) the CustomUI window was never visible in screenshots even though the window
object was alive/visible and its input+apply controls worked; after sending ESC (closed the tablet)
the screenshot still showed no window. Likely the `meta` layer anchor/canvas used for the window in
`InventoryScene` is not rendered on top (it is the options-menu layer) — recommend opening the window
on the `overlay`/`front` layer in the store scene, or verify visually in normal play with the tablet
closed. Evidence: `evidence/ingame-05a-customui-before-esc.png`,
`evidence/ingame-05-customui-window.png`.

Failures to look for: `"Registered (disabled, Local): <name>"` + `trust=NotChecked` (mod installed but
not enabled/seeded), `Not loaded: Unknown` / `Changed` (trust record missing/mismatched),
`[ModScanner]` refusal, `Harmony: … method(s) patched` followed by an exception inside `Init`, and any
`NullReferenceException` / `Exception:` stack trace mentioning `PrecisionLab`.

Known benign quirks in a good log:
- `Invalid sprite entry. Ensure all keys and sprites are assigned.` / `Duplicate key detected: …` —
  pre-existing base-game warnings.
- Missing-glyph warnings (`The character with Unicode value \uXXXX was not found …`) when the game
  UI font lacks CJK glyphs; cosmetic only.
- `[PrecisionLab] water feature init deferred: Object reference not set to an instance of an object`
  may appear once right after a save load; the mod retries and the SELFTEST/items still behave
  correctly (tracked as a cosmetic warning).
- If `SaveFile.es3` is read-only during boot (Steam-Cloud workaround), ES3 throws
  `IOException`/`UnauthorizedAccessException` for `SaveFile.es3.tmp.bak`. Harmless, but clear the
  attribute ASAP (see §5) and delete stale `.tmp` files before the next run.

---

## 5. Enabling a local mod without the in-game prompt (pre-seeded trust) + the Steam-Cloud trap

`SaveFile.es3` (Easy Save 3 JSON) stores:

- `modOrder` — array of enabled mod ids,
- `modTrust` — JSON **string** `{"records":[{id, source, workshopId, manifestHash, files:[
  "Assembly.dll|<sha256>"], combinedHash, declared, detected, gameVersion, approvedAt, updatedAt,
  developerMode}]}`.

The loader (`ModLoader.EvaluateTrust/ModTrust.CompareFiles`) loads a mod when:
`source` matches (`Local`, `workshopId="0"`), and `combinedHash` equals the current
`ModFingerprint` (`Match`) — or differs but the IL scan passes and capabilities are unchanged
(`Updated`). `developerMode=true` forces a load for local mods even if the scanner refuses.

**Steam Cloud trap:** Steam auto-cloud restores the cloud copy of `SaveFile.es3` ~5–10 s after the
game starts, overwriting a seeded file. Symptom: the mod shows
`[ModLoader] Registered (disabled, Local)` and `trust=NotChecked` even though you seeded it.
Working procedure used by `seed-local-mod.ps1` + runs:

1. `snapshot.ps1 -Label before-seed` (backup).
2. Copy the mod folder into `<game>\Mods\<ModFolder>`.
3. `.\seed-local-mod.ps1 -ModFolder '<game>\Mods\PrecisionLab'` (adds id to `modOrder`, writes the
   exact-hash trust record; backs up `SaveFile.es3` automatically; idempotent; `-DeveloperMode` sets
   the dev flag; `-Restore` rolls back the newest backup).
4. Set `SaveFile.es3` **read-only**, launch with `-logFile`, wait for the log/sentinel (~10 s), then
   clear read-only and delete stale `SaveFile.es3.tmp*` files.
5. Verify the audit line `running=True trust=Match`. If the user later rebuilds the DLL the record
   becomes `Updated`; re-run the seed script (or approve once in the Mods menu).

`seed-local-mod.ps1` computes the game's exact fingerprint: `manifestHash = sha256(manifest.xml)`,
`combinedHash = sha256(utf8(manifestHash + "\n" + "<rel dll>|<sha256 dll>"))`.

**Trust-prompt behaviour observed for a new local mod:**
- Not in `modOrder` → `[ModLoader] Registered (disabled, Local): <name> v1.0.0`, `running=False
  trust=NotChecked`; nothing loads until the user enables it.
- In `modOrder` but no record → `Not loaded: Unknown`; wrong source/hash → `Changed`.
- In `modOrder` + matching record → loads silently, no UI prompt; audit shows `trust=Match`.
- A first-time enable through the in-game **Mods** menu runs `ModScanner`; if the manifest declares
  the capabilities it needs it loads/prompts, otherwise the loader reports `Not loaded: <state>` /
  scanner refusal. For this mod the manifest has `<Capabilities/>` and the build scanned clean
  (`caps=none`), so approval is the only interaction.
- The user's older `developerMode=false` records keep working; `developerMode=true` is only needed
  to bypass a scanner refusal during iteration.

---

## 6. Dev tools, dev console and item spawning

`DevTools.cs` (decompiled) reads marker files next to the executable:

- empty **`dev.txt`** → `[DevTools] Developer tools enabled: F9 debug panel and F8 console.`
- **`1dev.txt`** → full dev tools (`DevTools.IsFullEnabled`, extra Marketing/Story panels).
- neither → `[DevTools] Developer tools disabled: no dev.txt next to the executable (<path>).`
  and `DevConsoleStripper` destroys the QFSW Quantum Console objects.

With `dev.txt` present (verified, screenshots in `tools/verify/evidence/`):

- **F9 (in the store scene, i.e. with a save loaded)** opens the **Debug Panel**
  (`ingame-01-F9-panel.png`): tabs **Store / Command / Spawn / Special**. Mods register entries in
  it via `ModHelper.AddDebugFunction`; the real mod contributes **"PrecisionLab: spawn extractor"**
  and **"PrecisionLab: spawn purifier"** in the *Spawn* column.
- **"Open Debug Spawner"** opens `CustomUIItemSpawner`: it lists every `DirectoryMaster` item id
  (search box), and a left-click spawns 1 (Shift+click ×10) into the store inventory
  (`GraphUtils.EmporiumTryAdd`). Modded ids appear because the list is built from the item directory;
  clicking logs `[CustomUI] Spawned '<id>' x1`.
- **F8** is announced as the console key. At the main menu it visibly toggles a dark scrollable
  overlay; inside the loaded store scene our F8 screenshot showed no console
  (`ingame-02-F8-console.png`). Treat the F9 Debug Panel as the reliable dev spawn path. The QFSW
  console's game commands (registered via `[Command]` attributes, e.g. `loot-list`, `loot-roll`,
  `mod-trust`, `mod-scan`, `paper-item`, `client-faces`) only work if a console object exists in the
  current scene.
- Delete `dev.txt` afterwards for a clean (non-dev) install; the mod's normal acquisition path does
  not need it.

**How a human obtains a modded item:**

1. Normal play path (no dev tools): in-game **phone → Wild Network → "Precision Lab" tab**, buy
   *精确取液器 / Precision Liquid Extractor* (1200 credits) or *提纯机器 / Purification Machine*
   (2400) — delivered to the store inventory. Also low chance in `medicalTable` / `junkTable` loot
   (the mod patches those loot tables), and existing saves get the items added on load.
2. Verification/dev path: add empty `dev.txt` next to `Probably Stolen.exe`, load a save, press
   **F9 → Spawn → "PrecisionLab: spawn extractor"** (or *Open Debug Spawner* and search
   `precision_liquid_extractor` / `precision_purifier`). Both were confirmed spawnable in a loaded
   game. Remove `dev.txt` when finished.

**Extractor pipette window (double-click):** double-clicking a *Precision Liquid Extractor* opens its
native content window via `ItemMouseDoubleClickHandler.OpenContentAction` (title = item name;
verified `WINDOW openVisible=True title='精确取液器'`). Layout is a single horizontal
`MachineryPanelRow` — 量程 (slider 1–10000 µL + live `当前值` text), 微调 (±1/±10), 预设
(1/10/100/1 mL/5 mL/10 mL), 状态 (contents) — but because `MachineryPanelRow.columns` defaults to
the panel count, the row is wider than the screen and the right-hand panels are cut off
(screenshot `ingame-03-extractor-window.png`). Opening the window does not change the dose.
The slider writes the per-item `PRECISION_EXTRACTOR_DOSE_UL` tag: the mod self-test asserts
`SetValue(4321, notify:true)` → dose 4321 (`SELFTEST metering dial … sliderWritesDose=True`).

---

## 7. Fully automated in-game self-test (no human, no clicks)

Two mechanisms, both proven here:

1. **Mod-side logging (part of PrecisionLab itself).** On `ModHook.OnGameLoadedInit` the mod runs a
   one-time `SELFTEST` that:
   - logs both items from the item directory (`SELFTEST items …`),
   - creates test liquid containers and asserts exact 1234 µL transfer, exact-or-nothing refusal,
     clean-water and concentrate purification modes,
   - ends with `[PrecisionLab] SELFTEST OK exact=True refuseOverdose=True cleanWater=True
     concentrate=True` (or `SELFTEST exception: …` on failure).

   Because it runs inside `PlayerStore.LoadGame` (immediately after
   `ModHook.FireOnGameLoadedInit()`), a verifier only has to get one save loaded — no gameplay needed.

2. **Throwaway UI-automation mod (`tools/verify/autoload-mod/`, NOT shipped).** Reaches gameplay
   without manual clicks:
   - Harmony-postfix on `MainMenuUIController.Update`; after ~150 frames it calls the game's own
     private `LoadGameUI.LoadSlot(slot, file)` via reflection (same code path as the "Load game"
     button: sets `NewGameData.saveSlot/saveFile`, starts the black-screen transition, loads
     `InventoryScene`).
   - loads **a scratch copy** of a save (e.g. copy `save_43.es3` → `save_98.es3`, then
     `LoadSlot(98, "save_98.es3")`) — never the user's real slot; never saves in-game.
   - subscribes `ModHook.OnGameLoadedInit` and probes `DirectoryMaster.Has<GameItem>(id)`,
     `ItemSpawner.Spawn(id)` + `ModHelper.MoveItemToFrontCounter`, and the Wild Network registry
     (`NetworkUpgrade.GetDefinitionsInOrder()` filtered by `tab=="precision_lab"`,
     `GetDefinition(id)`, `PlayerStore.Instance.networkUpgrade`).

   Build: Roslyn `csc` against `<game>\Probably Stolen_Data\Managed\{mscorlib,netstandard,
   Assembly-CSharp,UnityEngine.CoreModule,UnityEngine,0Harmony}.dll` (see the .cs header/commands in
   `tools/verify/autoload-mod/`). Install into `<game>\Mods\zz_verify_autoload`, seed its trust with
   `-DeveloperMode`, remove afterwards.

Observed Wild Network evidence for the frozen build:

```
scene=InventoryScene networkUpgrade dict count=2
WILDNET def id='PRECISION_LAB_EXTRACTOR' tab='precision_lab' cost=1200 inRunDict=True
WILDNET def id='PRECISION_LAB_PURIFIER' tab='precision_lab' cost=2400 inRunDict=True
WILDNET precision_lab defs in registry=2 GetTabLabel=精密实验室设备
GetDefinition('PRECISION_LAB_EXTRACTOR')!=True  GetDefinition('PRECISION_LAB_PURIFIER')!=True
```

Hard-won detail: `ModHelper.AddNetworkUpgrade` (and anything touching `PlayerStore.Instance`) throws
`NullReferenceException` when called from `IMod.Init` — `PlayerStore.get_Instance` runs `new
PlayerStore()` before the game state exists. Register upgrades from `OnGameLoadedInit` /
`OnNewGameStarted` instead (the frozen build does this: logs *"network upgrades deferred until a run
is loaded"* at Init and *"network upgrades registered (tab precision_lab)"* after load).

---

## 8. Verification protocol / checklist

Run from `F:\deepseek harness workbench\probably-stolen-mod\tools\verify`.

**A. Before any launch/test (always):**
1. No `Probably Stolen` process already running (`run-game.ps1` enforces this).
2. `.\snapshot.ps1 -Label before-<test>` — backs up `Player*.log`, `GameLog_*.txt`, all `*.es3`
   (saves, index, `SaveFile.es3`), `mod-trust-list.json`, `ModConfigs\`. Never delete/save over the
   user's slots; copy a slot to a scratch file for load tests (`save_98.es3`) and remove the copy
   afterwards.
3. Know the current mod folder + hashes (see `dist\PrecisionLab`); seed trust with
   `seed-local-mod.ps1` if the folder/hash changed.

**B. Static load verification (main-menu only, fastest):**
1. Copy `dist\<Mod>` → `<game>\Mods\<Mod>`, seed trust, set `SaveFile.es3` read-only, launch with
   `run-game.ps1 -LogPath … -WaitForMod precision_lab`, clear read-only after ~10 s.
2. `.\check-modlog.ps1 -LogPath <log> -RequireMod precision_lab` → exit code 0 + `RESULT: PASS`.
   Look for `Loading: <Mod>.dll`, `Initialized`, `3 item sprite(s)`, `2 locale(s)`, `1 loot file(s)`,
   `audit: … running=True trust=Match caps=none`.
3. Quit gracefully (WM_CLOSE), then confirm `SaveFile.modOrder` still contains the mod and
   `save_43.es3`/`saves_index.es3` hashes are unchanged vs the snapshot.

**C. In-game / gameplay verification (needs a loaded save):**
1. Install the throwaway `zz_verify_autoload` mod (seed with `-DeveloperMode`), copy a save to
   `save_98.es3`, launch (read-only trick), wait ~40 s.
2. `.\check-modlog.ps1 -LogPath <log> -RequireMod precision_lab -RequireSelfTestOk` → PASS requires
   `items registered in mod item directory`, `network upgrades registered (tab precision_lab)`, the
   `SELFTEST … OK` line, and item-spawn evidence. Any `NullReferenceException`/`Exception:` in the
   log fails the run.
3. Optional visual/dev check: with `dev.txt` present, F9 → Spawn shows the mod buttons; screenshot it.
4. Quit without saving; remove helper mod + its `modOrder`/`modTrust` entries; remove scratch save.

**D. Ship-clean state:**
- `<game>\Mods` contains only the real mod folder(s); no `dev.txt` unless intentionally left;
  `SaveFile.es3` writable with `modOrder = ["item_details","precision_lab"]` and matching trust
  records (`precision_lab` source `Local`, `workshopId "0"`, combinedHash of the shipped DLL,
  `developerMode=false`); user save slots byte-identical to the pre-test snapshot.
- Final clean-boot log (`tools/verify/logs/final-clean-boot.log`) shows precision_lab
  `running=True trust=Match`, `[DevTools] Developer tools disabled`, and no exceptions.

---

## 9. Harness files (all under `probably-stolen-mod/tools/verify/`)

| File | Purpose |
|---|---|
| `run-game.ps1` | Launch with isolated `-logFile`, wait for boot/mod sentinels, optional graceful quit; refuses double launches; reports PID/log path. |
| `check-modlog.ps1` | Parse a log: loader ran, per-mod audit (`running/trust`), exceptions, `SELFTEST OK`, required markers; exit code 0/1/2 (also `-Json`). |
| `snapshot.ps1` | Back up logs/saves/configs with sha256 manifest into `snapshots/`; read-only w.r.t. game files. |
| `seed-local-mod.ps1` | Pre-seed `modOrder` + exact-hash `modTrust` record into `SaveFile.es3` (auto-backup, idempotent, `-DeveloperMode`, `-Restore`). |
| `hello-mod/` | Minimal hand-built `IMod` mod (source + compiled `build/zz_verify_hello.dll`) used to prove local mods load; throwaway. |
| `autoload-mod/` | Throwaway UI-automation mod (`zz_verify_autoload`) that loads a scratch save and probes items/Wild Network; not shipped. |
| `logs/` | Captured logs: `launch-test-*.log`, `hello-mod-boot.log`, `final3.log`…`final12.log` (final build: `final12.log` = Harmony 1 + SELFTEST OK + visible CustomUI window layer overlay + tag tests 3333/250), `final-clean-boot5.log` (shipped state), `cloud-purge*.log`. |
| `evidence/` | Screenshots: main menu, F8 panel at menu, in-game store, F9 Debug Panel with mod spawn buttons, F8 in store, `ingame-03-extractor-window.png` (old machinery window), `ingame-05a`/`ingame-05` (meta-layer attempt), `ingame-07`/`ingame-08` (prefab-override invisible attempt), `ingame-09-default-overlay-test.png` (control window proves overlay renders), **`ingame-10-customui-visible-window.png` (FINAL: visible 精确取液器 · 量程设置 window with input+slider+presets+Apply)**, `ingame-11-customui-visible-window2.png`. |
| `snapshots/` | Save/log backups + full `.es3` backup taken before any save load (`presaveload-20261030-022841`); `SaveFile_*_preseed.es3` pre-seed copies. |

Rebuild the throwaway helper mods (optional, for a future re-run):

```powershell
$csc='C:\Program Files\dotnet\sdk\9.0.318\Roslyn\bincore\csc.dll'
$m='F:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
& dotnet $csc -nologo -nostdlib -target:library -out .\autoload-mod\build\zz_verify_autoload.dll `
  -r:"$m\mscorlib.dll" -r:"$m\netstandard.dll" -r:"$m\Assembly-CSharp.dll" `
  -r:"$m\UnityEngine.CoreModule.dll" -r:"$m\UnityEngine.dll" -r:"$m\0Harmony.dll" `
  .\autoload-mod\src\AutoLoadMod.cs
```

---

## 10. Final shipped state (2026-10-30)

- `<game>\Mods\`: **only** `PrecisionLab\` (manifest.xml, final build **PrecisionLab.dll 50688 B
  sha256 `77f2720b4308928ec60f3e0ea7f6bea1279646c7515f5e3f08d2d6996f31c847`**, localization
  47 keys/locale = 94 entries, loot table, textures, README).
- `dev.txt`: **removed** (dev tools disabled; log line `[DevTools] Developer tools disabled`).
- `SaveFile.es3` (writable): `modOrder = ["item_details","precision_lab"]`; `modTrust` records:
  `item_details` (Workshop, workshopId `3828868413`, combinedHash `9af2d05a…`, dev false) and
  `precision_lab` (source `Local`, workshopId `"0"`, combinedHash
  `35687f61f5bfed9faa9380cc7861d329eda875899baca56043398ab3db6b0a0f`, dev false).
  Final boot (`final-clean-boot6.log`): `Loading: PrecisionLab.dll`, `Harmony: 1 method(s) patched`,
  `running=True trust=Match caps=none hash=35687f61…`, 3 item sprites, 94 locale entries,
  `[DevTools] Developer tools disabled`.
- Final window verification (`final13.log`/`final14.log`, screenshot
  `evidence/ingame-12-customui-meta-window.png`): visible 精确取液器 · 量程设置 window with real
  slider (`dose_slider` root alive, rect 300×24), slider→input sync (2000→"2000"), input→slider sync
  via a real edit commit (onEndEdit 1500→slider 1500; plain `SetString(notify)` alone does not fire
  TMP onEndEdit), real preset-button clicks ("1 mL"→1000 both, "5 mL"→5000 both), Apply → tag 5000 +
  window closes; SELFTEST OK, no exceptions.
- User saves untouched: `save_43.es3` (2873771 B, mtime 2026-10-30 01:35:27, sha256 `D55FD86A…`) and
  `saves_index.es3` byte-identical to `snapshots/presaveload-20261030-022841`; no in-game save was
  performed. The scratch test save (`save_98.es3`, byte copy of slot 43) was loaded only via the
  helper; Steam Cloud re-restored it during the final boot and it was deleted while the game was
  running so the deletion syncs (verified absent after quit).
- Delivery status: **PASS / delivery-ready** — loader/Harmony/trust/self-test, both items
  spawnable, Wild Network tab+upgrades, and the visible CustomUI metering window with working slider,
  sync, preset buttons and Apply→tag pipeline. Cosmetic only: window centre sits slightly right of
  the viewport centre (right border near the screen edge; no content cut).

## v1.1.0 功能验证（2026-10-31）

新增：淀粉水解反应、微生物培养机（含营养果/休眠机制）、退休化学家补购移液枪。
构建：`PrecisionLab.dll` 79,360 B，sha256 `601dbc48…`，combinedHash `c41955a8…`（manifest 1.0.0 时）。

- **加载/信任**：`Harmony: 6 method(s) patched`（双击 + Jackson×2 + DirectoryMaster.Item + 淀粉水解 + 退休化学家）；
  5 个物品贴图；2 语言 162 条本地化（81 键/语言）；`running=True trust=Match`。
- **SELFTEST**：`OK ... hydrolysis=True digestion=True cultivator=True`；
  `starch hydrolysis converted=20µL（50°C）/ coldTick=0`；`digest digested=2000µL organicWaste=400µL total -1600`；
  `cultivator machine window=True power=8 manual=True nutrientSlot=True fruitAccepted=True pressed=+60% activityAfterFeed=60% fruitConsumed=True`。
- **真实化学 tick**：50 °C 淀粉 1000→980、糖 0→20、酸不变、总体积 4200 不变；70 °C 不反应；回到 50 °C 继续 → 960/40。
- **培养机实测**：6 面板单行（电源｜模块｜容器｜营养果｜菌群活性｜便签）；便签索引 4 有“微生物培养机说明”；
  营养果槽拒绝非营养果；巨型营养果压榨 +60%（0→60）并被消耗；
  活性 60 时消化 1400 µL 糖、+280 有机废物、总体积 −1120、耗电 8、面板 `65% · 1.475 mL/night`；
  无糖 3 夜：活性 65→40→15→0，面板 `休眠 · 需要营养果`；补充营养果+糖后恢复 0→60→65 并继续消化。
- **杰克逊博士**：培养机解锁后 `stocks 1 microbial cultivator`（特殊库存 2）；未解锁无供货。
- **退休化学家（数值验证）**：用 instrumented 计数探针确认——解锁且不拥有取液器 → 恰好 1 次添加尝试
  （原版 endAction 的 blender/heating_plate/evaporator/chem_finisher 库存同时保留）；已拥有一把 → 0 次；
  未解锁 → 0 次。临时存档前柜台处于引擎“不可接收”状态，物品被原版 “front counter full” 流程丢弃，
  因此只验证了尝试次数；正常柜台会出现在他的柜台上。
- **回归**：提纯机器便签索引 3、单行窗口、提纯上限面板；取液器窗口/即时生效/方向模式；旧存档加载正常。
- 截图：`tools/verify/evidence/ingame-28-cultivator-window.png`、`ingame-29-cultivator-dormant.png`。
### v1.1.0 最终门禁（2026-10-31）

- manifest 升级到 **1.1.0**（DLL 不变）：manifestHash `eeecb49f…`，combinedHash `2aba4619…`；
- 最终启动日志：`Initialized: Precision Lab Equipment v1.1.0`、`Harmony: 6`、5 贴图、2 语言 162 条、
  `running=True trust=Match hash=2aba4619…`、`SELFTEST OK ... hydrolysis=True digestion=True cultivator=True`；
- 最终交付状态：`Mods` 仅 PrecisionLab（manifest 1.1.0 + DLL 79,360 B），SaveFile.es3 可写且信任匹配、
  dev=false；无 dev.txt/helper/scratch；slot 43 用户存档未被测试写入；
- 日志：`tools/verify/logs/final-gate-v110.log`、`final-clean-boot16.log`。
## v1.2.0 功能验证（2026-10-31，肾上腺素注射笔生产线）

新增 10 种自定义液体、6 步肾上腺素反应链、肾上腺素原料盒/原料药/注射笔/右旋废料物品、
化学制剂成型机 2 条新配方、“更多药品”升级、退休化学家原料盒、提纯机废料输出位。

- 构建：`PrecisionLab.dll` 106,496 B，sha256 `5d7e5415…`，manifestHash `72adaa1a…`，
  combinedHash `2e5edaee…`（manifest v1.2.0）。
- 加载：`Harmony: 6 method(s) patched`；9 个物品贴图；2 语言 × 128 键；`registered 46 liquids (custom chain=10)`；
  两条成型机配方注册；`running=True trust=Match`；0 异常、0 dummy effect 警告。
- 自检：`SELFTEST OK ... adrenaline=True adrenalineItems=True`；
  `amination=3000µL catecholLeft=0 lipobondLeft=0 | racemic=3000 reducingLeft=0 |
  levoSalt=1500 dextro=1500 acidLeft=1500 | freebase=1500 hcl=1500 baseLeft2=0 acidLeft2=0 |
  wasteCollected=1500 dextroLeft=0 wasteItem='右旋废料' | solution=1500 hclLeft=0 waterLeft=0 |
  recipes=True items=True`。
- 独立链式测试（逐步隔离）：resolution levo/dextro = 1500/1500（Δ=0）；freebase 1500/base 0；
  hcl 1500/acid 0；注射笔效果为真实 buff（命中 +20%/3 回合、暴击 +33%/3 回合，无 dummy）。
- 提纯机：6 面板单行（power|module|container|waste|purity|note），便签索引 4；废料输出位收集右旋废料。
- 配方纸条：handnote 文档，`HANDNOTE_BODY` 存在，署名已清空（`signature=''`）。
- 原料盒：拆开得到 7 瓶试剂（catechol/hmb/resolving 各 3000 已观测；lipobond/reducing 3000、base/acid 1500 由代码常量保证）。
- 退休化学家：MEDS 解锁后原料盒添加尝试 >0；未解锁 0；取液器逻辑不变。
- 未决/说明：自定义液体存档持久化测试因测试存档无法挂载可保存容器而未能确认（游戏内逻辑无异常）。
- 最终交付状态：`Mods` 仅 PrecisionLab（manifest v1.2.0 + DLL），SaveFile.es3 可写且信任匹配、dev=false；
  无 dev.txt/helper/scratch；slot 43 用户存档未被测试写入。
  日志：`tools/verify/logs/final39.log`、`final-clean-boot20.log`。
## v1.2.1 小修（2026-10-31）

用户反馈提纯机“废料输出”空槽位看起来像塞了一张纸条。原因：该槽位空背景使用了原版 chemical2
化学品图标（外形像橙色纸条），并非真的插入了物品（说明便签仍在 note 槽位）。修复：移除该槽位背景图标，
空槽显示为普通暗色槽位；面板标题“废料输出”不变。版本号 1.2.1。
## v1.2.2 文本修复（2026-10-31）

配方纸条里用的“→”箭头在原版手写便签字体中没有字形，游戏内显示为空白。改为文字表述“会生成”（英文用 gives），六步流程更易读；同时确认物品名“盐酸肾上腺素原料药”为规范用词（原料药=API），非拼写错误（全项目搜索“原科药”0 处）。
## v1.2.3 文本调整（2026-10-31）

原版手写便签字体的“米”字旁会把“料”写得像“科”，导致配方纸条里的“原料药/原料配比”看着像错字。
为避免误读，纸条措辞改为“压制成盐酸肾上腺素成品”“把盐酸肾上腺素成品溶于等量水”“每盒试剂配比”；
物品名（普通 UI 字体）仍为“盐酸肾上腺素原料药”（规范术语）。另加 `RefreshRecipeNotesInStore()`：
读档时若“更多药品”已解锁，会把旧版本生成的手写便签正文刷新为新措辞并清空署名。
## v1.2.4 / v1.2.5 右旋废料回收（2026-10-31）

- v1.2.4：新增回收反应 `StepDextroRecycle`——右旋废料 + 还原剂，60–80°C，按 1:1 再消旋成消旋肾上腺素，
  可回到第 3 步重新拆分；配方纸条追加“回收再消旋”段落；右旋废料描述提示回收路线；
  自检新增 `adrenaline recycle: racemic=1000µL dextroLeft=0µL reducingLeft=0µL` 与 `recycle=True`。
  游戏内实测：1000+1000 @70°C → 消旋 1000；再 +拆分剂 1000 +酸 1000 @50°C → 左旋 500 / 右旋 500，重新进入拆分。
- v1.2.5：按用户要求去掉面向玩家的“方案B/plan B”字样（纸条、物品描述、README），机制不变；
  旧纸条读档自动刷新为新文案。
## v1.2.6 试剂瓶满瓶显示（2026-10-31）

玩家反馈原料盒里的试剂瓶只装了几毫升、看起来几乎空瓶。修复：`TryAddBottle` 在加液前把瓶子的
`LIQUID_CONTAINER_CAPACITY` 覆盖为该批用量，使每个瓶子正好装满（catechol/hmb/lipobond/reducing/resolving 各 3000/3000，
base/acid 各 1500/1500 µL），水位显示 100%、提示写明“满瓶…正好一份配比量”；纸条补充“每瓶试剂都是满瓶”一句。
配方/数值不变（一盒=一份配比）。
## v1.2.7 等比例放大耗材（2026-10-31）

玩家要求“一支肾上腺素注射笔消耗 5 毫升”并等比例增加原料盒耗材：
试剂瓶改为满瓶 20 mL/20 mL/20 mL/20 mL/20 mL/10 mL/10 mL（catechol/hmb/lipobond/reducing/resolving/base/acid）；
成型机两条配方 requiredVolume 750 → 5000 µL；一盒原料仍然产出 2 支注射笔（每支 5000 µL 药液）。
纸条配比、物品描述同步更新；旧纸条读档自动刷新。
## v1.2.8 注射笔奢侈品分类（2026-10-31）

玩家指出肾上腺素注射笔单支 1200+ 信用点，应视为奢侈品。修复：注射笔同时拥有 `MEDICAL` 与 `LUXURY_ITEM`
物品类型/tag（原版大型注射器等也有此分类），可被上层奢侈品客户（upperLowerVisitorLuxury，购买 tag LUXURY_ITEM）
与展示台系统识别；提示与物品描述改为“医疗物 / 奢侈品”，价值不变。自检新增奢侈品类型/tag 断言。
## v1.2.9 涡轮增压提示（2026-11-01）

玩家反馈“高级涡轮增压机不能对提纯机用”。诊断结论：机器侧无问题——提纯机具备 STANDARD_MACHINE_TAG，
`IsTurboBoostEligible/CanBoostNow=true`，高级/超级涡轮增压器 `MayTarget/CanTarget=true`，真实 Target 路径
可正常触发额外周期（电池 100→94、污染物 microbe 1000→38，日志 "purifier removed 1732 µL"）；与净水器行为一致。
常见原因是增压器自身未充能到 READY（`IsTurboReady` 读 modifiedState，需 15/15 就绪）时拖拽无目标高亮。
v1.2.9 增加：提纯机/培养机显式 EnableTag("TURBO_BOOST_ELIGIBLE_TAG")（冗余保险），机器提示新增
“涡轮加速：拖涡轮增压器到本机器，增压器需显示就绪”说明。
## v1.2.10 废料瓶累积（2026-11-01）

玩家确认增压器已就绪，但增压后右旋杂质没被收集。原因：废料输出位只在**空槽**时收集；槽里已有右旋废料瓶时，
后续周期（含涡轮加速）会静默跳过。修复：`CollectDextroWaste` 在槽内已有废料瓶时，用
`ChemicalHelper.AddChemContentUL` 把新废料累加进同一瓶，并 `GradeProduct` 刷新品质；液体中的废料照常清空；
移除瓶子后下一轮会新建一瓶。说明文案补充“多批废料会累积在同一个废料瓶里”。
## v1.3.0 简易止血带（2026-11-01）

玩家提出：用「韧棉织物」+「废金属」拖拽合成「简易止血带」，并要求在购买「更多药品」升级后才收到配方纸条、解锁前不能合成。

实现：
- 新物品 `makeshift_tourniquet`（简易止血带）：MEDICAL + ITEM_MEDICAL_TAG + 医疗熟练度，价值 24；
  使用效果镜像止血绷带（稳定伤势 + 伤势 -1，单次消耗）；16x16 程序生成像素贴图。
- 合成：把 `kotton_fabric`（韧棉织物）拖到 `scrap_metal`（废金属）上 → 消耗各 1、在目标所在库存生成 1 条止血带；
  放置失败则不消耗；通过 `DirectoryMaster.Item` postfix 给织物实例挂接目标委托（幂等防重复），存档加载时同样生效。
- 解锁门控：`CanCraft` 检查 `PRECISION_LAB_MEDS`，未解锁时 may/can 返回 false（无拖拽高亮），无法合成。
- 配方纸条：解锁时/读档时额外发放「简易止血带配方」手写便签（署名空、正文含合成方式与解锁条件），旧存档自动补发。
- 自检新增：物品类型/激活委托/挂接检查，日志 `tourniquet=True`。
## v1.3.1 简易止血带新图标（2026-11-01）

玩家提供止血带图片，要求去掉背景并作为游戏内物品图标、占 3×2 格。
- 去背景：按绿色背景色抠图（含阴影）、清除右下角 AI 水印、修补抠图小洞，输出全分辨率透明 PNG；
  另导出 48×32 游戏图标。
- `makeshift_tourniquet` 改用 48×32 贴图并**强制 3×2 矩形 GridShape**（因为透明图角会导致按 alpha 推导的形状残缺）；
  读档时把旧 1×1 版物品迁移为 3×2（try/catch 保护）。
- 自检增加 shape 3×2 / useSpriteFromMod 断言。
## v1.3.2 止血带图标改 2×2（2026-11-01）

玩家反馈 3×2 版把圆形旋钮横向拉伸得怪。改为 **32×32（2×2 格）**，按原图比例缩放（不拉伸旋钮），
强制 2×2 矩形形状；旧 3×2 / 1×1 物品读档时迁移为 2×2 新外观。自检断言 shape 2×2。
## v1.3.3 止血带合成可达性修复（2026-11-01）

真实玩家存档诊断：MEDS 已解锁、配方纸条已在 store.mainInventory、织物实例已挂接委托且 CanCraft=True，
但 `fabric.Target(scrap)` 无结果——原因是产物 2×2 在目标所在库存放不下，旧代码静默放弃。
修复：
- 放置顺序：目标库存 → 来源库存 → `GraphUtils.EmporiumTryAdd`（遍历其他店内库存）；全部放满才放弃，
  且**不消耗材料**，并写入夜间报告提示。
- 新增金属锭 `metal_ingot` 作为替代目标（织物侧检查，金属锭自身委托不动）。
- 织物/废金属/金属锭在解锁后显示合成提示 tooltip；纸条发放时写夜间报告提示避免错过。
## v1.3.4 止血带材料品质标签（2026-11-01）

按玩家设计：材料决定成品标签与价值。
- 废金属 → 破旧（value −50%）；金属锭纯度 ≥ 中等（INGOT_PURITY_FAIR，level 7）→ 极佳（+25%）；
  金属锭低于中等（LOW 6 / VERY_LOW 5 / 未知）→ 陈旧（−25%）。
- 实现：合成时给产物加自定义 `ItemFeature`（CATEGORY_TOURNIQUET_CONDITION，useCondition=true，
  fake=real 自定义 ItemCondition，modValue -50/-25/+25，isFeatureMatch=true），由游戏价格管线按百分比修正；
  协商界面显示条件，物品提示新增“标签：破旧（价值 -50%）”行。
- 纸条正文/物品描述/README 补充材料-标签-价值对照表；自检断言 worn=-50、excellent=25、aged=-25。
## v1.3.5 配方纸条可见投放（2026-11-01）

玩家建议：纸条放不下时可以放到柜台外/开门就能看到。实现 `NoteDelivery`：
- 新发放的配方纸条（肾上腺素 + 简易止血带）优先放进**前台柜台**（frontInvinvElement，开门可见），
  其次后柜台 → 主库存 → 展示台 → 打烊库存 → 原 EmporiumTryAdd；
- 全部放满时不静默丢失：本次放弃并在次日 `OnWakingUpLate` / 下次读档时重试（按标题查重，不重复发）；
- 已存在的纸条不自动搬家，避免意外移动玩家物品；旧存档读档刷新正文但保持位置。
## v1.3.6 纸条投放时机修正（2026-11-01）

验证发现 `OnGameLoadedNormal` 在 `PlayerStore.LoadGame` 的 DecodeSaveItem **之前**触发，v1.3.5 在此时补发纸条
可能先创建新纸条、随后存档纸条又被解码，造成重复/被移动的风险。修正：所有纸条补发与正文刷新移到新的
`OnGameLoadedLate`（解码与 feature 恢复之后）；已有纸条按标题先查到→只刷新正文，不重建、不移动。
前台柜台仍是优先投放位置（开门可见），日终 `SwapInv()` 会把前台物品移入可存档的后柜台，因此不会丢失；
全部放满时仅警告并在下次读档/次日重试（按标题查重，不会重复发）。
## v1.3.7 培养机活性面板实时刷新（2026-11-02）

玩家反馈：培养机“菌群活性”面板只有放入营养果（槽位事件触发刷新）时才显示真实状态；一直不喂会停留在初始 100%。
原因：活性 tag 每夜会衰减，但面板文本只在槽位添加/夜间循环末尾用捕获的 MachineryText 更新，从未被玩家触碰的机器显示会一直停在旧字符串。
修复：给培养机挂 `onOneSecondItemFunc`，每秒用 `FormatCulturePanel` 生成实时文本并仅在变化时 SetText（避免布局抖动）；
新增自检断言：onOneSecondItemFunc 存在、喂食后显示 60%、活性 0 时显示休眠文本（cultureDisplay=True）。
## v1.3.8 拆分剂补充装 + 肾上腺素价格 ×0.1（2026-11-02）

玩家指出右旋废料回收循环会持续消耗拆分剂，一盒原料的拆分剂不足；并希望原料与产品价格都缩到 0.1。
- 新增 **`resolving_agent_refill`（拆分剂补充装（20 毫升））**：密封的非液体 CHEMICAL_SUPPLIES 物品，
  unitValue 32；激活后在可用库存中生成一瓶满瓶 `resolving_agent_bottle`（20000 µL），放不下时**不消耗补充装**并夜间提示。
- 退休化学家在「更多药品」解锁后每次来访：原料盒 + 拆分剂补充装各 1 个到前台柜台。
- 价格整体 ×0.1：原料盒 150、拆分剂补充装 32、拆分剂瓶 12、盐酸肾上腺素原料药 50、肾上腺素注射笔 120（保留奢侈品标签）。
- 自检：resolvingBottle / resolvingRefill / box 150 / api 50 / pen 120 断言。