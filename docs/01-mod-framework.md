# 01 — Probably Stolen mod framework: loading, trust gate, data files, build target

**Game:** Probably Stolen / 深空当铺：可能是偷的 — Questing Goose Studio
**AppID:** 4348910 · **Installed version analysed:** `0.46D` · **Unity:** `2021.3.45f2` (Mono)
**Sources of truth:** ILSpy output of `Assembly-CSharp.dll` in
`probably-stolen-mod/decompiled/Assembly-CSharp` (all `File.cs:line` refs below are that folder),
a **real installed Steam Workshop mod** (`F:\SteamLibrary\steamapps\workshop\content\4348910\3828868413`,
*Item Details / 物品详情* 0.6.2, decompiled to `probably-stolen-mod/decompiled/ItemDetails`), the live
`Player.log` of this machine, and an open-source mod project (see §9).

**Companion files:** compiled skeleton DLL at `probably-stolen-mod/skeleton/ExampleMod/ExampleMod.dll`,
trust-seeding script `probably-stolen-mod/skeleton/seed-trust.ps1`, build script `.../skeleton/build.ps1`,
and validation tooling in `skeleton/tools/`: `verify-mod-folder.ps1` (static compliance checker) and
`scanner-check/` (runs the game’s real `ModScanner.Scan` offline — see §13).

---

## 0. TL;DR / HANDOFF

* The manifest is **`manifest.xml` (XML, not JSON)** with children `<ID> <Name> <Author> <ModVersion>
  <Description> <GameVersions> <Prerequisites> <Assemblies> <Capabilities>`; `<Assemblies>` and
  `<Capabilities>` are mandatory elements (Capabilities may be empty).
* Mods live in `<game>\Mods\<any folder name>\`; the **manifest `<ID>` is the identity**, not the folder
  name. Scan is **top-level only, no recursion, no tilde/underscore filtering**. A folder without a valid
  `manifest.xml` is skipped with a warning.
* Load order: the loader registers every folder, then for **enabled + trusted** mods only it
  `Assembly.LoadFrom`, finds the **first** `IMod` class, calls `harmony.PatchAll(assembly)`, then
  `Init(manifest)` → `OnEnable()`. `OnDisable()` runs on game quit only.
* A brand-new local mod is **registered as disabled** (trust state `Unknown`) until it is approved once
  (Mods panel “Activate”, or pre-seeded `modTrust`/`modOrder` in `SaveFile.es3`). Rebuilds then load as
  `Updated` automatically **unless the scanner detects a capability the manifest/approval does not cover**;
  per-mod `developerMode` (one toggle, or one boolean in the JSON) makes every future change trusted.
* Data files are plain files inside the mod folder: `Localization/<locale>.csv`, `LootTables/*.json`,
  `Books/*.json`, `Textures/*.png|jpg`, `Textures/Items/*.png`, `Textures/Paper/*.png`,
  `Sounds/*.wav|ogg|mp3`, `preview.png`. **No asset bundles.**
* Build target that works: **`netstandard2.1`**, explicit `<Reference>`+`HintPath` to the game’s
  `Probably Stolen_Data\Managed\*.dll`, `<Private>false</Private>`, no NuGet runtime deps. This is what
  both known public mods use and what was verified with `dotnet build` here.
* Fingerprint/trust formula **verified against the real Item Details trust record**: 
  `manifestHash = sha256hex(manifest.xml)`;
  `combinedHash = sha256hex(utf8(manifestHash + "\n" + "<Assembly text>|<sha256hex(dll)>"))` (repeat for
  each listed assembly). Data files are *not* part of the fingerprint.
* **Blockers:** none for authoring/loading. This analysis could not launch the game (verifier owns that),
  so the skeleton was build-verified and static-checked (references are all game assemblies, `IMod\0`
  byte present) but not runtime-verified. Nexus pages are Cloudflare-403 from this environment.

---

## 1. Folder layout, discovery and `manifest.xml`

### 1.1 Mods folder

`ModLoader.ModsPath` (`ModLoader.cs:50`) = `Path.Combine(Application.dataPath, "..", "Mods")` →
**`F:\SteamLibrary\steamapps\common\Probably Stolen\Mods`** (the game log prints it as
`.../Probably Stolen_Data\..\Mods`). If missing, `LoadAllMods` creates it (`ModLoader.cs:722-728`).

Discovery (`ModLoader.cs:722-738`, `:874-1014`):

* `Directory.GetDirectories(ModsPath)` — **one level only, not recursive**.
* **There is no filtering at all** for tilde/underscore/dot folder names; the only requirement is a
  parsable `manifest.xml` in that folder. (Mod *IDs* cannot start with `.`, `ModLoader.IsValidModId`,
  `ModLoader.cs:66-84`.)
* Folder name is irrelevant to identity. Duplicate `<ID>` handling (`ModLoader.cs:881-899`): a local
  folder loaded before a Workshop item wins (“the local copy wins over the Workshop copy”); between two
  local folders the first one enumerated wins (with a warning).
* Workshop mods are discovered separately through SteamUGC (`ModSource.Workshop`, `ModWorkshop.cs:62-93`)
  from the Steam Workshop content folder; local mods use `ModSource.Local`. Source is part of the trust
  record — moving a mod between Workshop and `Mods` re-triggers trust (`Changed`).

### 1.2 Folder layout (all known file types)

```
Mods\MyMod\                          <- any folder name
  manifest.xml                       REQUIRED  (ModLoader.ParseManifestCore, ModLoader.cs:610-720)
  MyMod.dll                          REQUIRED (listed in <Assemblies>; root or e.g. Assemblies/MyMod.dll)
  Assemblies\*.dll                   optional extra listed DLLs (resolved by ModLoader.ResolveModDependency)
  Localization\en.csv, zh.csv ...    optional  locale tables      (ModLoader.LoadLocalization, :1183)
  LootTables\*.json                  optional  loot tables/groups/patches (ModLootFile.Load, :10)
  Books\*.json                       optional  paper books        (PaperBookRegistry.LoadMod, :112)
  Sounds\*.wav|*.ogg|*.mp3           optional  audio clips        (ModLoader.LoadSoundsCoroutine, :1239)
  Textures\*.png|*.jpg               optional  raw textures keyed by file name (:1137 prefix "")
  Textures\Items\*.png               optional  item sprites, point-filtered, LoadedMod.Sprites (:1137)
  Textures\Paper\*.png               optional  paper page sprites, LoadedMod.PaperSprites (:1160)
  preview.png                        optional  Mods-panel preview  (ModUIManager.GetLocalPreview, :997-1030)
  (anything else)                    ignored by the loader; .dll files not listed in <Assemblies> are
                                     reported and can break trust — see §3.5
```

> The ONLY files hashed for trust are `manifest.xml` + the DLLs listed in `<Assemblies>`.
> Editing CSVs/JSON/PNG/audio does not change trust; editing the manifest or a listed DLL does.

### 1.3 `manifest.xml` — exact schema and parser rules

Parsed with `XDocument.Load`; the **root element name is not validated** (real mods on this machine use
both `<Manifest>` and `<ModManifest>`). Only direct child elements of the root are read.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Manifest>
  <ID>example_mod</ID>                                <!-- required -->
  <Name>Example Mod</Name>                            <!-- optional -->
  <Author>someone</Author>                            <!-- optional -->
  <ModVersion>0.1.0</ModVersion>                      <!-- optional -->
  <Description>Free text.</Description>               <!-- optional; XML-escape or CDATA -->
  <GameVersions>
    <Version>0.46</Version>                           <!-- 0..N entries; matching is numeric-prefix -->
  </GameVersions>
  <Prerequisites />                                   <!-- optional; <Mod>other_id</Mod> children -->
  <Assemblies>                                        <!-- REQUIRED element, >=1 child -->
    <Assembly>MyMod.dll</Assembly>
  </Assemblies>
  <Capabilities />                                    <!-- REQUIRED element; may be empty -->
</Manifest>
```

`ModManifest` fields (`ModManifest.cs:3-21`):

| XML element | Field | Rules / parser behaviour (`ModLoader.ParseManifestCore`, `ModLoader.cs:610-720`) |
|---|---|---|
| `<ID>` | `ID` | required, trimmed; `IsValidModId`: non-empty, ≤64 chars, first char ≠ `.`, chars `[A-Za-z0-9_.-]`, not `.`/`..` |
| `<Name>` `<Author>` `<ModVersion>` `<Description>` | same | trimmed, optional; `Name` falls back to ID in UI (`GetModLabel`, `:166`) |
| `<GameVersions><Version>` | `GameVersions` | trimmed, non-empty; **UI-only check**: `ModUIManager.VersionMatchesGame` normalizes to leading digits/dots, so `0.46` matches `Application.version` `0.46D`; mismatch only adds a warning row, never blocks loading |
| `<Prerequisites><Mod>` | `Prerequisites` | mod IDs; enforced only by a warning (`ValidatePrerequisites`, `:1085-1100`); affect recommended order, not loading |
| `<Assemblies><Assembly>` | `Assemblies` | required element; trimmed, `\`→`/`; empty entries dropped; duplicates removed by full path (`:668-681`); each must end `.dll`, must not be rooted or contain `..` (`:687-694`); every path is resolved relative to the mod folder and must exist (`IsWellFormed`) |
| `<Capabilities><Capability>` | `Capabilities` | required element (empty allowed); lowercased, distinct; every value must be one of `ModCapability.All` = `files, network, process, dynamic-code, native, reflection-external` (`ModCapability.cs:7-19`) |

If any rule fails, the **whole folder is skipped** with `[ModLoader] ... skipping folder.` /
`... skipping.` in the log; a missing manifest is only a warning.

Two further structural requirements are enforced *after* parsing, in `IsWellFormed`
(`ModLoader.cs:843-872`) — these are the classic “registered but not loaded” causes:

1. every listed `<Assembly>` path is inside the mod folder and the file exists;
2. at least one listed DLL **contains bytes `IMod\0`** (the mod owns a class implementing `IMod`;
   `AnyReferencesIMod`/`ContainsBytes`, `:808-841`). A data-only “mod” with no DLL is rejected.

### 1.4 Capabilities: what the scanner detects

`<Capabilities>` is not decorative: `ModScanner` runs a Mono.Cecil pass over the listed DLLs
(`ModScanner.Scan`, `ModScanner.cs:970-1000`) and derives capability IDs from IL. The mapping
(`ModScanner.cs:31-78, 307-629`):

| Capability | Triggered by (examples) |
|---|---|
| `files` | `System.IO.File/FileStream/Directory/FileInfo/…`, `System.IO.Compression.ZipFile`, `ES3`/`ES3File`/`ES3Settings` file APIs, `XDocument.Load(path)`, `StreamReader/Writer(path)` |
| `network` | `System.Net.*`, `UnityWebRequest*`, `WWW`, `Ping`, `Steamworks.SteamHTTP/Networking*` |
| `process` | `System.Diagnostics.Process`, `Application.OpenURL` |
| `dynamic-code` | `System.Reflection.Emit.*`, `Assembly.Load*/LoadFrom`, `AppDomain.CreateInstance*`, `Activator.CreateInstance(string)`, `Expression.Compile`, runtime serializers |
| `native` | `DllImport`/P-Invoke, pointer types/locals, `Marshal`, `NativeLibrary`, `UnsafeUtility` |
| `reflection-external` | `HarmonyLib.AccessTools/Traverse` or `Type.GetMethod/GetField/…` on **non-game** types, dynamic call sites |

Hard flags (always refuse enable / mark `Changed`, `ModScanner.cs:950-968`):
`payload-resource` (embedded EXE), `opaque-resource`, `stealer-strings`, `opaque-string` (≥1024-char
base64 constant), `unpatch-all` (`Harmony.UnpatchAll()` with null id), `loader-target` (`[HarmonyPatch]`
or reflection on **guard types** `ModLoader, ModTrust, ModTrustRecord, ModFingerprint, ModTrustList,
ModScanner, ModCapability, ModSecurityDebug`), `bundled-framework` (assembly named `0Harmony`,
`MonoMod*`, `Mono.Cecil*`, or such types compiled into the mod), `unreadable` (bad DLL/attribute),
`unlisted-reference` (assembly reference that is neither a game assembly nor a DLL listed in
`<Assemblies>`).

Practical rule for a clean mod (`<Capabilities/>`, no prompts): do not use `System.IO`, reflection on
external types, networking, processes, P/Invoke, dynamic code and do not bundle framework DLLs in the
DLL. Game APIs (`Assembly-CSharp`), Unity APIs and `0Harmony` patching of game methods are fine.
Localization/asset reading should go through `ModHelper` (game-side IO), not your own `File` calls.
If you must use one, declare it in `<Capabilities>` and expect the player-facing capability prompt.

---

## 2. Load flow in detail

`ModLoader` is a scene `MonoBehaviour` present in the boot scene (`level0` contains the
`ModLoader`, `ModHook`, `ModUIManager` objects; `Player.log` shows loading immediately after engine
init, before the menu). `ModLoader.Awake` (`ModLoader.cs:130-156`) runs:

1. collect game assembly names (`CollectGameAssemblyNames`, `:86-128`) = every DLL name under
   `Probably Stolen_Data\Managed`; register `AppDomain.AssemblyResolve` for mod dependencies;
2. `ModTrust.EnsureLoaded()` → read `modTrust` from the ES3 save;
3. `ModTrustList.LoadCache()` → signed remote list cache; then `StartCoroutine(ModTrustList.Fetch())`
   (5 s timeout, `ModTrustList.cs:184-202`);
4. `LoadModOrder()` → `EnabledMods` from ES3 key `modOrder` (`:198-206`);
5. `LoadAllMods()` → local folders; `LoadWorkshopMods()` → SteamUGC subscribed items;
6. `ApplySavedOrder()` → sorts `ModLoader.Mods` by `modOrder`;
7. `ValidatePrerequisites()` → warnings only;
8. `LoadModAssets()` → textures, localization, loot tables, books, sounds, then `OnModAssetsLoaded`;
9. `LogAudit()` → one `[ModLoader] audit: ...` line per mod (great for debugging).

For an **enabled** mod with a passing trust gate, each listed DLL goes through `LoadModDll`
(`ModLoader.cs:1016-1083`):

1. `AssemblyName.GetAssemblyName(dllPath)`; **if the assembly name is a game assembly name it throws**
   (“a mod cannot ship its own copy”, `:1020-1024`) → name your assembly uniquely;
2. `Assembly.LoadFrom(dllPath)`;
3. find all non-abstract classes implementing `IMod`; **if none → warning and not loaded**; if several,
   **only the first one is used** (warning lists the others);
4. `new Harmony("mod." + manifest.ID)`; `harmony.PatchAll(assembly)` (this is why `[HarmonyPatch]`
   classes work with zero boilerplate); log `Harmony: N method(s) patched`;
5. `mod.Init(manifest)` then `mod.OnEnable()`. On exception: `UnpatchAll`, error recorded, `Init failed`;
6. a `LoadedMod` row is added to `ModLoader.Mods` (fields: `Instance, Manifest, Assembly,
   HarmonyInstance, FolderPath, DllPath, IsEnabled, LoadError, Source, WorkshopId, TrustState,
   TrustNote, Fingerprint, TrustRecord, ListVerdict, LastScan, Textures, Sprites, PaperSprites, Sounds,
   Localization` — `LoadedMod.cs:7-54`).

At quit `OnApplicationQuit` (`:1280-1297`) calls `OnDisable()` and `UnpatchAll(id)` for every mod that
loaded a Harmony instance. There is **no runtime disable/enable without restart** — enabling/disabling in
the UI only sets `modOrder` + `RestartRequired` (`SetModEnabled`, `:208-226`).

`IMod` (`IMod.cs:1-8`) is in the **global namespace**:

```csharp
public interface IMod
{
    void Init(ModManifest manifest);
    void OnEnable();
    void OnDisable();
}
```

It is a plain object (created via `Activator.CreateInstance`, so needs a public parameterless ctor); it is
not a `MonoBehaviour`. Use `ModHook` events, Harmony patches, or `ModLoader.Instance.StartCoroutine`
for time-based work.

Dependency loading: `ResolveModDependency` (`:488-538`) resolves a missing assembly by short name against
every enabled/loaded mod’s **listed** assemblies (file name must equal assembly short name) — so ship
extra DLLs inside the mod folder and list each in `<Assemblies>` (also satisfies the scanner’s
`unlisted-reference` flag). DLLs in the folder root or `Assemblies/` that are *not* listed are reported
(`UnlistedAssemblyFiles`, `:790-806`) and make the mod `Changed` (no load) once it has a trust record.

---

## 3. Trust gate — exactly what a new local mod must satisfy

### 3.1 Where state is stored

| ES3 key (in `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\SaveFile.es3`) | Type | Meaning |
|---|---|---|
| `modOrder` | `List<string>` | enabled mod IDs, in load order (`ModLoader.LoadModOrder`, `:198`) |
| `modTrust` | JSON **string** (`Store{records:[ModTrustRecord…]}`) | per-mod approval records (`ModTrust.cs:15,30-58`) |
| `modUiWarningAcceptedVersion` | int | one-time DLL warning dialog accepted ≥2 (`ModWarningUI.cs:6-30`) |
| `locale` | string | current Unity Localization code (`zh` on this machine) |

`ModTrustRecord` (`ModTrustRecord.cs:5-30`) fields, all JSON strings/lists:
`id, source ("Local"|"Workshop"), workshopId, manifestHash, files ["MyMod.dll|<sha256>", …],
combinedHash, declared[], detected[], gameVersion, approvedAt, updatedAt, developerMode`.

Real record from this machine (Workshop mod Item Details; the current 0.6.2 files hash to these values,
and its older v0.5.4 build is visible in `Player.log` loading with `trust=Updated`):

```json
{"records":[{"id":"item_details","source":"Workshop","workshopId":"3828868413",
 "manifestHash":"24b4ac27c00ad947fd2480ba986b0948c4295655e166a6f716f9856a15ba0e08",
 "files":["ItemDetails.dll|14469fa1e0bdc4fccbfdab5818b776180090d0dfdc8a81060be53ce79d99b374"],
 "combinedHash":"9af2d05a4adf2f0597c613363bd1b1dad98186172cafcbb5fcab3530a2e87cf0",
 "declared":[],"detected":[],"gameVersion":"0.46D",
 "approvedAt":"2026-10-28T15:08:01.0980382Z","updatedAt":"2026-10-29T18:07:19.4249815Z",
 "developerMode":false}]}
```

### 3.2 Fingerprint (exact algorithm, `ModFingerprint.cs:15-44`)

```
manifestHash = hex(SHA256(bytes of <mod folder>/manifest.xml))
for each <Assembly> A in manifest order (deduped by full path):
    entry = A + "|" + hex(SHA256(bytes of <folder>/A))      // "missing" if absent
combinedHash = hex(SHA256(UTF8(manifestHash + "\n" + entry1 + "\n" + entry2 …)))
```

All hashes lowercase hex; separators are literal `\n`. **Verified**: applying this formula to the record
above reproduces `combinedHash 9af2d05a…` exactly, and the current Workshop folder hashes to the same
values (i.e. the formula and file set are right). Note it covers only `manifest.xml` + listed DLLs.

### 3.3 Decision table (`ModLoader.EvaluateTrust`, `ModLoader.cs:272-360`)

| Condition | `ModTrustState` | Loaded? | UI note / consequence |
|---|---|---|---|
| remote list `blocked` id/hash, blocked workshop item or publisher → `ModListVerdict.Blocked` | `Blocked` | **no** | trust record revoked, silently disabled, note = reason |
| remote `killSwitch.allMods` (and not verified if `exceptVerified`) | `Paused` | **no** | “mod loading is paused by the developers” |
| no trust record for ID | `Unknown` | **no** | registered disabled; row in Mods panel |
| record exists, `source` or `workshopId` differs | `Changed` | **no** | “comes from a different place / different Workshop item” |
| combined hash matches, no unlisted DLLs | `Match` | **yes** | normal load |
| combined hash differs (rebuild/updated files) and: unlisted DLLs present | `Changed` | **no** | “folder contains DLLs the manifest does not list” |
| … `developerMode==true` and `source==Local` | `DeveloperMode` | **yes** (even if scan refused; warning only) | record refreshed with new fingerprint |
| … scanner refused (hard flag) | `Changed` | **no** | “refused by the mod scanner” |
| … detected capability not declared in manifest | `Changed` | **no** | “uses capabilities it does not declare” |
| … detected capability not in the approved record’s `detected` | `Changed` | **no** | “asks for new capabilities” |
| … otherwise | `Updated` | **yes** | record refreshed silently |

Also: `ModTrust.CompareFiles` never compares `gameVersion`, so **a game update does not invalidate
approvals**. The remote list’s cached payload is only used if signature-verified; fetch failure keeps the
cache; list older than 30 days only shows a UI hint.

### 3.4 First-run normal gameplay flow (no pre-seeding)

1. Drop the folder into `<game>\Mods`. Launch. The mod appears in the Mods panel **inactive**, trust
   `Unknown`, and is not loaded. Log: `Registered (disabled, Local): …` then `Not loaded: Unknown.`
2. Open the Mods panel. First time it shows the DLL warning (`ModWarningUI`, “I understand” /
   “Don’t show this again” → stores `modUiWarningAcceptedVersion=2`).
3. Select the mod → **Activate** (`ModUIManager.ToggleSelectedMod`, `:1046-1070` →
   `BeginEnable` `:728-764` → `ModLoader.CheckEnable` `:385-457` → `ApproveAndEnable` `:459-472`).
   * If the manifest declares **zero capabilities** (or the mod is on the verified list):
     silent approve — a trust record is written with the current fingerprint and `detected` from the scan.
   * If it declares capabilities: a confirm prompt lists them (`NeedsPrompt`); on accept, approve as above.
   * Refusals (blocked, paused, scan hard flag, undeclared capability, missing/shadowing DLL, unreadable)
     are shown instead and the mod stays off.
4. **Restart the game** (`RestartRequired`); the mod loads (`trust=Match` in the audit log).
5. Disabling a mod later (`SetModEnabled(id,false)`) calls `ModTrust.Revoke(id)` — the approval record is
   deleted (`ModLoader.cs:208-226`), so re-enabling needs the approve click again (or dev mode/seed).

### 3.5 Developer-mode variants (the practical recipes)

**A. Approve once in UI, keep iterating.** After step 4, each rebuild changes the DLL hash →
`Updated` path → the loader re-scans and **auto-loads** (`Load=true`) as long as
(a) no unlisted DLLs are in the folder, and (b) every detected capability is declared in
`<Capabilities>` *and* was already in the approved record’s `detected` list. The record is refreshed on
every successful load, so the cycle “edit → build → relaunch” needs no UI. Adding a new capability
(e.g. first `System.IO` use) breaks the cycle → the mod becomes `Changed` and needs the approve click.

**B. Per-mod developer mode (recommended for heavy iteration).** In the Mods panel detail pane, a local
mod with an existing record shows a **“Developer mode: off/on”** button (`ModUIManager.ShowTrust`
`:671-677`, `ToggleDeveloperMode` `:792-804` → `ModLoader.SetDeveloperMode` `:474-486` →
`ModTrust.SetDeveloperMode`). Turning it on writes `developerMode:true` into the record; from then on any
folder change loads as `DeveloperMode` (fingerprint refreshed, scan results ignored/only logged). It still
requires the first approve click to create the record — or pre-seed it (recipe C).

**C. Pre-seed the trust record (fully scripted, no game UI).** Edit `SaveFile.es3` while the game is
closed, adding the ID to `modOrder` and a `modTrust` record. Two useful properties:
* a **correct** `combinedHash` gives `Match` → loads directly, no scan even;
* with `developerMode:true` a *stale/placeholder* `combinedHash` gives `Updated` → developer branch →
loads and refreshes the record. So for iteration you can seed with fake hashes + dev flag, or with real
hashes via `skeleton/build.ps1`, which prints both.

Provided scripts (tested):
```powershell
# build + print hashes + a ready modTrust record for skeleton\ExampleMod
powershell -ExecutionPolicy Bypass -File skeleton\build.ps1
# write modOrder + modTrust (developerMode=true) into SaveFile.es3, with .bak backup
powershell -ExecutionPolicy Bypass -File skeleton\seed-trust.ps1
powershell -ExecutionPolicy Bypass -File skeleton\seed-trust.ps1 -DryRun
powershell -ExecutionPolicy Bypass -File skeleton\seed-trust.ps1 -NoDeveloperMode
powershell -ExecutionPolicy Bypass -File skeleton\seed-trust.ps1 -Unseed
```
The ES3 file is single-line JSON whose top-level values are wrappers of the form
`"key":{"__type":"string","value":"…"}`, and `modOrder` uses `__type` =
`System.Collections.Generic.List` + backtick + `1[[System.String, mscorlib, Version=4.0.0.0,
Culture=neutral, PublicKeyToken=b77a5c561934e089]],mscorlib`. The scripts do targeted regex
replacement (PowerShell’s `ConvertTo-Json` drops `__type` properties), preserving every other key
byte-for-byte. Always back up: the game rewrites the file on exit, and Steam Cloud may sync it
(`steam_autocloud.vdf` in the same folder).

**D. Debug tooling.** An empty file `dev.txt` next to `Probably Stolen.exe` (or `1dev.txt` for full mode)
enables developer tools (`DevTools.cs:6-34`): log line “F9 debug panel and F8 console”, `QuantumConsole`
is not stripped (`DevConsoleStripper.cs:8-27`). Useful console commands (`ModSecurityDebug.cs`,
`LootDebug.cs`):

| Command | Purpose |
|---|---|
| `mod-scan <idOrFolder>` | prints detected/declared capabilities, hard flags, fingerprint — run this after every build |
| `mod-trust [id]` | prints local trust records + this session’s per-mod trust state |
| `mod-trust-list`, `mod-trust-list-fetch` | remote list state / force refresh |
| `mod-trust-list-load <path>` | loads an unsigned test list (requires `PlayerStore.devMode`; dev switch is the `D` key in gameplay, `StoreUIManager.cs:251-255`) |
| `loot-list [id]`, `loot-roll <id> [count]` | loot tables/groups and their effective odds |

---

## 4. Data files loaded from a mod folder

`ModLoader.LoadModAssets` (`ModLoader.cs:1102-1115`) runs once after all mods are registered, iterating
`ModLoader.Mods` **in saved order** (so `modOrder` decides JSON patch order):

for each enabled mod: `LoadTextures` → `LoadLocalization` → `ModLootFile.Load` →
`PaperBookRegistry.LoadMod` → `StartCoroutine(LoadSoundsCoroutine)`; the coroutine ends with
`ModHook.FireOnModAssetsLoaded(mod)` (`:1277`).

| Folder / file | Loader | Destination | Format |
|---|---|---|---|
| `Textures\*.(png\|jpg)` (top level) | `LoadTexturesFromFolder` `:1137-1158` | `LoadedMod.Textures["name"]` | raw `Texture2D.LoadImage` |
| `Textures\Items\*.(png\|jpg)` | same, `pixelArt:true` | `LoadedMod.Sprites["name"]` | point filter, clamp, 100 PPU sprite |
| `Textures\Paper\*.(png\|jpg)` | `LoadPaperTextures` `:1160-1181` | `LoadedMod.PaperSprites["name"]` + `Textures["Paper/name"]` | bilinear, FullRect |
| `Localization\*.csv` | `LoadLocalization` `:1183-1227` | `LoadedMod.Localization[<file base name>][key]` | see §5 |
| `LootTables\*.json` | `ModLootFile.Load` (`ModLootFile.cs:10-72`) | `LootRegistry` tables/groups/patches | see §6 |
| `Books\*.json` | `PaperBookRegistry.LoadMod` (`PaperBookRegistry.cs:112-132`) | `PaperBookRegistry` entries (+ optional item) | Newtonsoft `PaperBookData` |
| `Sounds\{*.wav,*.ogg,*.mp3}` | `LoadSoundsCoroutine` `:1239-1278` | `LoadedMod.Sounds["name"]`, `AudioDirectory.RegisterModClip("id:name")` | UnityWebRequest from `file://` |

Notes and usage:

* **Item sprites**: `GameItem.SetSpriteAndShapeFromMod(modId, spriteKey)` (`GameItem.cs:385-416`) sets
  `spriteAtlasPath = modId`, `useSpriteFromMod = true` and derives the item’s grid shape from the sprite’s
  alpha; rendering resolves through `GameItemElement.ResolveSpriteByName` (`GameItemElement.cs:197-210`).
  PNG should be pixel art, dimensions multiples of the UI grid (`UISettings.current.gridSize`), and the
  sprite key is the file name without extension.
* **Sounds**: `ModHelper.PlaySound(modId, key)` / `PlaySound2` / `PlaySound3` and
  `ModHelper.GetSoundIdentifier(modId, key)` = `modId + ":" + key` (`ModHelper.cs:136-154`); load is
  asynchronous — first availability is `OnModAssetsLoaded`. `ModHelper.RegisterAfterhourAmbience` requires
  the clip to be loaded already.
* **Paper books (`Books\*.json`)**: parsed by Newtonsoft.Json into `PaperBookData`
  (`PaperBookData.cs`; camelCase fields, `[JsonExtensionData]` tolerates unknown keys, missing fields keep
  defaults) and registered per `<ID>:<book id>` (`PaperBookRegistry.LoadMod/Register`,
  `PaperBookRegistry.cs:112-244`). If `id` is absent the file name is used; ids are validated as safe
  tokens (`[A-Za-z0-9_.-]`). Sketch of the shape (authoritative schema: `PaperBookData.cs`,
  `PaperPageData.cs`, `PaperTemplateData.cs`, `PaperElementData.cs`; the in-game paper editor writes the
  same format):

  ```json
  {
    "id": "lab_notes",
    "title": "Lab Notes",
    "pages": [
      { "name": "Page 1",
        "elements": [
          { "type": "text", "x": 40, "y": 40, "w": 400, "h": 80,
            "text": "First page text.", "style": "body", "align": "left", "valign": "top" }
        ] }
    ],
    "item": { "id": "precision_lab_notes", "name": "Lab Notes", "description": "Readable note.",
              "sprite": "items_misc/manual1", "value": 50, "important": true }
  }
  ```

  The optional `item` object makes the book spawnable/readable as an item (created through the vanilla
  item directory mechanism and `ModItemDirectory`; `loc`/`nameLoc`/`descriptionLoc` keys resolve through
  `ModHelper.GetLocalized`). Open it with `ModHelper.OpenPaper(modId, bookId)`; `Textures\Paper\*.png`
  are its page backgrounds/paper sprites (`LoadedMod.PaperSprites`). Loaded at the same asset stage as
  the other data files.
* **No AssetBundle/Addressables loading exists in the loader** — mods ship loose files only.
* `preview.png` (root) is used by the Mods panel only.

---

## 5. Localization for mods

* Files: `<mod>\Localization\<localeCode>.csv`. The **file name without extension is the locale code**;
  loader stores `LoadedMod.Localization[fileNameWithoutExtension]` with a case-insensitive comparer
  (`LoadedMod.cs:53`, `ModLoader.cs:1191-1222`).
* Game locale codes (Unity Localization + `LocHelper`): `en, ja, de, ru, zh, fr, es, pl, ptBR, ko, it`
  (`LocHelper.cs:22, 312-376`). The machine here runs `zh`. Real Example: Item Details ships
  `de, es, fr, it, ja, ko, pl, ptbr, ru, zh` — note **`ptbr.csv` lowercase works** because lookups are
  case-insensitive. There is **no `en.csv`** in it; see fallback below.
* CSV format (read line-by-line, `ModLoader.LoadLocalization` + `ParseCsvValue`):
  * blank lines and lines whose trimmed start is `#` are comments;
  * first `,` separates key and value; a line without a comma is skipped with a warning;
  * key = trimmed text before the comma (empty key → warning, skipped);
  * value = trimmed; if it starts **and** ends with `"`, the quotes are stripped and `""` becomes `"`;
  * literal `\n` in the value becomes a newline.
* Lookup: `ModHelper.GetLocalized(string modId, string key, params object[] args)`
  (`ModHelper.cs:91-134`): find `LoadedMod` by ID → try `Localization[<current locale code>]` → try
  `Localization["en"]` → if absent, warn once and return **the key itself**; then `string.Format(args)`
  (format errors return the raw text).
* **English handling**: the engine only falls back to a table literally named `en`. Since the English
  game text must come from somewhere, two workable conventions (both used in the wild):
  1. ship `Localization/en.csv` with English and `Localization/<other>.csv` per language — simple;
  2. follow Item Details (`decompiled/ItemDetails/ItemDetails/Loc.cs`): hardcode the **English default in
     code** (`Loc.T("key", "English text {0}", arg)`) and ship only non-English CSVs; the helper ignores
     CSV when locale is `en`. This needs your own tiny `Loc.T` wrapper (patched at use time after assets
     load) but gives you compile-time-safe English text.
* Load order caveat: localization is loaded **after** all `Init`/`OnEnable` calls
  (`LoadModAssets` runs later), so `GetLocalized` inside `OnEnable` returns the key. Localize lazily
  (on tooltip/UI draw, `Func<string>` labels for `ModConfig`) or from `OnModAssetsLoaded`.
* Manifest `Name`/`Author`/`Description` are shown verbatim (players often put both languages in
  `<Name>`, as Item Details does).
* Config-window labels/tooltips use `Func<string>` so they can call `GetLocalized` when the window opens.

---

## 6. `LootTables\*.json` (loot tables, table groups, patches)

Loaded by `ModLootFile.Load` (`ModLootFile.cs:10-72`); files are `*.json` in `LootTables`, sorted by name
case-insensitively, parsed with `JsonUtility` into (`ModLootFileJson.cs`):

```json
{
  "tables": [
    { "id": "myTable", "entries": [ { "id": "bandage_item", "weight": 2.0 },
                                    { "id": "ammo_box", "weight": 1.0 } ] }
  ],
  "groups": [
    { "id": "myGroup", "entries": [ { "id": "myTable", "weight": 1.0 } ] }
  ],
  "patches": [
    { "table": "medicalTable", "add": [ { "id": "bandage_item", "weight": 25 } ] },
    { "table": "junkTable", "remove": [ "old_item_id" ], "setWeight": [ { "id": "x", "weight": 5 } ] },
    { "group": "dumpingGroundTG", "add": [ { "id": "myTable", "weight": 3 } ] },
    { "table": "toolTable", "clear": true, "add": [ { "id": "bandage_item", "weight": 100 } ] }
  ]
}
```

Semantics (`ModLootFile.cs:74-171`, `LootRegistry.cs`):

* `tables[].entries[].id` = **item identifier** (`DirectoryMaster.Has<GameItem>(id)` or
  `PreBuiltItemHelper.preBuiltItems`, e.g. `bandage_item`, `ammo_box`, `slip`);
  `groups[].entries[].id` = **loot-table id**. Weights are positive floats.
* Vanilla tables are normalized to a total weight of **1000** at load (`LootRegistry.VANILLA_TOTAL_WEIGHT`;
  `Flatten`, `LootRegistry.cs:141-187`), so a mod adding `weight: 25` to `medicalTable` takes ≈2.4% of the
  table. Use `loot-list <id>` in the dev console to see effective odds/share.
* Patch object requires **exactly one of `table` / `group`** (both or neither → warning, patch skipped).
  `clear` first, then `add[]`, `remove[]` (item/table ids), `setWeight[]`.
  **Caveat:** `add` on an id that already exists **overwrites its weight** (`LootRegistry.Apply`, Add case),
  so use `add` only for new ids and `setWeight` for existing ones.
* Registering a table/group id that already exists is ignored with a warning (“To replace an existing one,
  clear it and add entries instead”).
* Writes are journaled and applied on a lazy rebuild, so **filesystem load order vs vanilla load order
  does not matter**; the order among mods is `modOrder` (later mods’ patches apply later).
* `ModHook.OnLootTablesLoaded` fires once per scene after both `TableMaster.Start` and
  `TableGroupMaster.Start` have registered vanilla data (`LootRegistry.OnVanillaLoaded`, `:108-119`).
  `ModHook.OnLootRolled(LootRollContext)` fires on every roll and can rewrite `context.itemId`.
* C# equivalents on `ModHelper`: `RegisterLootTable`, `RegisterLootGroup`, `AddLootEntry`,
  `RemoveLootEntry`, `SetLootWeight`, `ClearLootTable`, `AddLootGroupEntry`, `RemoveLootGroupEntry`,
  `SetLootGroupWeight`, `ClearLootGroup`, `GetLootEntries`, `GetLootGroupEntries`, `RollLootTable`,
  `RollLootGroup`, `SpawnFromLootTable`, `SpawnFromLootGroup`.
* Vanilla table ids (`TableMaster.cs:6-26`): `junkTable, accessCardTable, allModuleTable,
  makeshiftWeaponTable, materialTable, householdTable, packedFoodTable, t1moduleTable, t2moduleTable,
  toolTable, medicalTable`; vanilla groups (`TableGroupMaster.cs:6-7`): `dumpingGroundTG,
  dumpingGroundFreshTG`.
* Empty/zero weights, unknown ids, missing tables → warnings (once each), not fatal.

---

## 7. `ModConfig` (settings) — format and API

* Storage: `%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen\ModConfigs\<modId>.json`
  (`ModConfig.RootFolder`, `ModConfig.cs:29-33`), pretty-printed with `JsonUtility`:

  ```json
  {"entries":[{"key":"show_item_appraisal","value":"true"},
              {"key":"greeting_count","value":"3"}]}
  ```
  (real file from this machine: `ModConfigs\item_details.json`, 23 entries, all values strings).
* API (`ModConfig.cs`, `ConfigEntry.cs`): call `ModConfig.For(manifest.ID)` (or `ModHelper.GetConfig(id)`)
  from `Init`; declare entries with
  `Define(bool|int|float|string|enum)` overloads — parameters: `key, defaultValue, section,
  Func<string> label, [min,max | choices], Func<string> tooltip = null, bool requiresRestart = false`.
  Read live values with `ConfigEntry<T>.Value` (`.Set(v)` persists immediately, `.OnChanged` event,
  `.Reset()`); `config.Entries`/`Sections` for UI; `Save()` / `ResetToDefaults()`.
* Player-facing UI: Mods panel gear icon (interactable only when `ModConfig.HasSettings(id)`) →
  `ModConfigWindow.Open` (`ModConfigWindow.cs:21-83`) builds the window from the declared entries.
  Apply writes the file and fires `ModHook.OnModConfigApplied(modId)` (`:200-239`); entries flagged
  `requiresRestart` show the restart note. Labels/tooltips are `Func<string>` evaluated at window build
  (localize there), so config text can follow the game language.
* Only your own `Init` should define entries; define before the window is ever opened. Invalid persisted
  values fall back to defaults with a warning; ranges clamp.

Related per-save/per-mod storage helpers on `ModHelper`: `GetModDataFolder(id)` =
`%persistentDataPath%\ModData\<id>\`, `ReadModFileText/WriteModDataFileText/ListModDataFiles`, plus
`SetModData/GetModData(Int|Float|Bool)` stored in the save (`ModHelper.cs:907-1128, 1025-1128`).

---

## 8. Build requirements, target framework, and the skeleton

### 8.1 Runtime facts (why these choices)

| Fact | Evidence |
|---|---|
| Unity **2021.3.45f2**, **Mono** backend (not IL2CPP) | `UnityPlayer.dll` version info `2021.3.45f2`; `Player.log` “Initialize engine version: 2021.3.45f2”, `Mono path[0] = …/Managed`; `MonoBleedingEdge` folder |
| Mono API level: .NET Framework 4.x (`mscorlib 4.0.0.0`) **with** `netstandard 2.1.0.0` present | assembly versions in `Probably Stolen_Data\Managed` |
| Ship Harmony: **0Harmony 2.4.2.0**, built for `.NETFramework,Version=v4.7.2` | assembly metadata |
| Game version `0.46D` (`Application.version`) | `Player.log` audit line, real `modTrust.gameVersion` |
| Real mods target **netstandard2.1** | ItemDetails.dll references only `netstandard 2.1.0.0`, `0Harmony`, game/Unity assemblies; its ILSpy project + GitHub sibling project both declare `netstandard2.1` |

### 8.2 Recommended project setup (verified: builds with `dotnet build` here)

* **TargetFramework `netstandard2.1`.** It is what the game’s own `Assembly-CSharp` targets, what Unity
  2021.3 Mono loads, and it builds out-of-the-box with the installed .NET SDK 9 (no targeting-pack/NuGet).
  `net472` also loads (0Harmony is net472) but needs the .NET Framework targeting pack /
  `Microsoft.NETFramework.ReferenceAssemblies` package and buys nothing here. Both can reference
  `0Harmony.dll`; if you use `[HarmonyPatch]`, add the `0Harmony` reference explicitly.
* **Reference game DLLs by `HintPath`, never copy them** (`<Private>false</Private>`):
  `<GameDir>\Probably Stolen_Data\Managed\`. Minimal reference set: `Assembly-CSharp` +
  `UnityEngine.CoreModule`; add the module owning any Unity type you touch (e.g.
  `UnityEngine.AudioModule` for `AudioClip`, `UnityEngine.UI`, `UnityEngine.InputLegacyModule`,
  `Unity.TextMeshPro`, `Newtonsoft.Json`, `Unity.Localization`). `Assembly-CSharp` transitively exposes
  most game types, but the compiler needs the assembly that *defines* each used type.
* **Never add NuGet runtime dependencies** (`System.Text.Json`, `Microsoft.Extensions.*`, …): their DLLs
  are not game assemblies, so the scanner raises `unlisted-reference` and the mod cannot be enabled
  unless you list them in `<Assemblies>` — which still hands the DLL to the scanner as a mod assembly.
  In-box alternatives: `Newtonsoft.Json` (already in `Managed`), Unity APIs, `System.*` from the
  Managed facades. Use `Private=false` (or `CopyLocalLockFileAssemblies=false`) so nothing extra is copied.
* **Assembly name** must be unique and not equal to any DLL under `Managed` (the loader refuses a mod
  whose assembly name belongs to the game, `ModLoader.cs:1020-1024`), and must not be `0Harmony` /
  `MonoMod*` / `Mono.Cecil*`. Also **file name should equal assembly name**, because dependency
  resolution matches by file-name-without-extension (`ResolveModDependency`, `:520-537`).
* **C# language version:** use `9.0`–`11.0`. Unity 2021.3 compiles C# 9; the two public mods were built
  with `LangVersion latest`/`11.0` and their IL loads fine (compile-time-only features are safe:
  file-scoped namespaces, nullable annotations, pattern matching, global usings). Avoid runtime-dependent
  new features: `record`/`init` (need `IsExternalInit`, absent from the game’s profile), default interface
  methods, `static abstract` interface members, `ref` fields.
* **Output location:** a folder that gets copied into `<game>\Mods\<folder>\`, with the DLL next to
  `manifest.xml` (or an `Assemblies/` subfolder that you list in the manifest).

Working `.csproj` (`skeleton/src/ExampleMod/ExampleMod.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <AssemblyName>ExampleMod</AssemblyName>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <GenerateDependencyFile>false</GenerateDependencyFile>
    <DebugType>none</DebugType>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <GameDir Condition="'$(GameDir)' == ''">F:\SteamLibrary\steamapps\common\Probably Stolen</GameDir>
    <ManagedDir>$(GameDir)\Probably Stolen_Data\Managed</ManagedDir>
    <OutputPath>..\..\ExampleMod\</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(ManagedDir)\Assembly-CSharp.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(ManagedDir)\UnityEngine.CoreModule.dll</HintPath><Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.AudioModule">
      <HintPath>$(ManagedDir)\UnityEngine.AudioModule.dll</HintPath><Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### 8.3 Build command / output (verified)

```powershell
cd F:\deepseek harness workbench\probably-stolen-mod\skeleton
powershell -ExecutionPolicy Bypass -File .\build.ps1
# or: dotnet build .\src\ExampleMod\ExampleMod.csproj -c Release
```

* Result: `Build succeeded. 0 Warning(s) 0 Error(s)`; DLL:
  **`F:\deepseek harness workbench\probably-stolen-mod\skeleton\ExampleMod\ExampleMod.dll`** (8192 bytes).
* Static checks done on the artifact: assembly references = `netstandard 2.1.0.0, Assembly-CSharp,
  UnityEngine.CoreModule, UnityEngine.AudioModule` — **all present in `Managed`** → no `unlisted-reference`
  hard flag; bytes contain `IMod\0` → passes `IsWellFormed`; no `System.IO`/reflection/network/process
  calls in IL → no capabilities. Manifest fingerprint: `manifestHash f8989327…`, DLL `852cfe9c…`,
  `combinedHash 48080ab3…` (recomputed by `build.ps1`; changed values after any rebuild).
* Deployed layout and expected log lines are in `skeleton/README.md`; trust pre-seed in §3.5.

### 8.4 Harmony specifics

* Version shipped: `0Harmony 2.4.2.0` (net472). The game calls `harmony.PatchAll(assembly)` for you with
  instance ID **`"mod." + manifest.ID`**, so `[HarmonyPatch]` annotations are automatic — no `Harmony`
  instance creation needed unless you patch manually at runtime.
* Manual patching also works (`new Harmony(...)` is available; Item Details does this via helper classes);
  always `UnpatchAll(<your id>)` (a null-arg `UnpatchAll()` is a scanner hard flag) and unsubscribe
  `ModHook` handlers in `OnDisable`.
* Do **not** patch the guard types listed in §1.4 (`loader-target` hard flag), and don’t bundle
  Harmony/MonoMod/Cecil.

---

## 9. Real-world evidence (folder layouts, manifests)

1. **Steam Workshop — Item Details / 物品详情 v0.6.2** by Nwflower, installed at
   `F:\SteamLibrary\steamapps\workshop\content\4348910\3828868413` (AppID 4348910 per
   `ModWorkshop.MAIN_APP_ID`, `ModWorkshop.cs:15`). Real layout:

   ```
   manifest.xml          (root <Manifest>, ID item_details, GameVersions 0.46, <Capabilities />)
   ItemDetails.dll       (netstandard2.1 assembly; refs 0Harmony 2.4.2.0, Assembly-CSharp, Unity modules)
   preview.png
   Localization\de|es|fr|it|ja|ko|pl|ptbr|ru|zh.csv
   ```
   Confirms: XML manifest, `.dll` in folder root, CSV locale files, no `en.csv` (English in code),
   `<Capabilities />` empty, `<GameVersions><Version>0.46</Version>`, `ptbr.csv` lowercase, preview.png at
   root. Its decompiled source (`decompiled/ItemDetails`) is the canonical real `IMod`/`ModConfig`/`Loc`
   pattern; `Player.log` proves it loads as `trust=Updated`.
   Workshop item: <https://steamcommunity.com/sharedfiles/filedetails/?id=3828868413> (page not retrievable
   from this environment; folder itself inspected on disk).

2. **GitHub — `Codesoulme/shenkongdangpuMOD`** (“自由聊天&议价&交换”, assembly `FreeChatBargainBarter.dll`):
   <https://github.com/Codesoulme/shenkongdangpuMOD>. Inspected `manifest.xml` (root `<ModManifest>`,
   `<ID>FreeChatBargainBarter</ID>`, `GameVersions 0.46D`, declared capabilities `files, network,
   dynamic-code, reflection-external`) and `AIWorldPawnshop.csproj` (`netstandard2.1`, `LangVersion
   latest`, `PlatformTarget x64`, `Reference`+`HintPath` into `…/Probably Stolen_Data/Managed` with
   `<Private>false</Private>`, `GameDir` overridable). README documents the same local deploy layout
   `<game>\Mods\FreeChatBargainBarter\{manifest.xml, FreeChatBargainBarter.dll}` and Steam Workshop
   subscription workflow. Confirms: root-name tolerance, real reference style, and that declaring
   capabilities produces the player prompt instead of blocking.

3. **Signed developer trust list** (live): <https://raw.githubusercontent.com/dragonpan2/probably-stolen-modding/main/trust-list.json>
   — envelope `{payload: base64(signed JSON), signature: base64(RSA-SHA256)}`; payload
   `{issued, blocked[], verified[], blockedWorkshopItems[], blockedPublishers[], killSwitch{allMods,
   exceptVerified, message}}`. The game’s public key/caching/fetch logic is in §3 (`ModTrustList.cs:81-246`).

4. Nexus mods (ItemForge #68, PSApi #374, …) exist per search results
   <https://www.nexusmods.com/probablystolen/mods/68> but the pages return HTTP 403 (Cloudflare) from this
   environment, so no file contents were inspected; the authoritative layout evidence above comes from the
   installed Workshop mod and the GitHub project.

---

## 10. Gotchas / failure-mode cheat sheet

| Log line / symptom | Cause | Fix |
|---|---|---|
| `No manifest.xml found in … skipping folder.` | folder lacks manifest.xml | add it; folder must be directly under `Mods` |
| `manifest.xml … is missing a required <ID> element` / `has no <Assemblies> element` / `has no <Capabilities> element` | mandatory elements | see §1.3 |
| `… has an invalid <ID>` | chars/leading dot/length | use `[A-Za-z0-9_.-]`, ≤64 |
| `… lists an invalid assembly path` | rooted/`..`/not `.dll` | relative path inside folder ending `.dll` |
| `none of the listed DLLs contains a class implementing IMod` | no `IMod` implementation in listed DLLs | implement `IMod` (global interface) in a class with public parameterless ctor |
| `… has the assembly name '<game dll>', which belongs to the game` | DLL named like a Managed DLL | rename the assembly |
| `No IMod implementation found in X.dll, skipping.` | DLL loaded but type missing | check class visibility/interface name |
| `Not loaded: Unknown.` | first run, never approved | approve in Mods panel or pre-seed trust (§3.5) |
| `Not loaded: Changed. (… new version uses capabilities it does not declare …)` | new API use not in `<Capabilities>` | declare capability (prompt) or avoid API |
| `Not loaded: Changed. (… folder contains DLLs the manifest does not list …)` | stray/unlisted DLL | list it in `<Assemblies>` or delete it |
| `Not loaded: Changed. (… the new version was refused by the mod scanner)` | hard flag | run `mod-scan <id>` with dev tools, remove flagged code |
| `Not loaded: Blocked/Paused.` | remote trust list | cannot bypass (intended) |
| `Harmony: 0 method(s) patched` | no `[HarmonyPatch]` in assembly | fine if you don’t patch; manual patching still works |
| `Localization key 'x' not found for mod 'y'` | called too early or missing CSV row | localize after `OnModAssetsLoaded` / add key |
| `Mod sprite 'x' from mod 'y' not found` | `Textures/Items` file name/key mismatch | key = file name without extension |
| Mod appears but nothing happens after a rebuild | still old DLL in `Mods` (copy order) or restart not done | re-copy DLL, restart; mods load only at startup |

---

## 11. HANDOFF

**Status: complete for task-1 scope; nothing blocking authoring or the skeleton.**

Verified directly:
* manifest = `manifest.xml`, exact element schema + all parser/validation rules (`ModLoader.cs:610-720`,
  `ModManifest.cs`), folder scan semantics (`:722-738`, `:874-1014`), `IMod` lifecycle + Harmony flow
  (`:1016-1083`, `IMod.cs`), `LoadedMod` shape (`LoadedMod.cs`).
* Trust gate in full (`ModLoader.cs:272-360, 367-486`, `ModTrust.cs`, `ModFingerprint.cs`,
  `ModTrustList.cs`, `ModScanner.cs`, `ModTrustRecord/State`, `ModUIManager.cs:656-804`) and the
  **fingerprint formula was verified against the live Item Details trust record** in `SaveFile.es3`.
* Data pipeline and formats: Textures/Items/Paper, Localization CSV, LootTables JSON (+ live registry
  semantics), Books JSON, Sounds, ModConfig JSON/API, ModHook event catalogue (§4–§7, §12).
* Build recipe chosen from runtime facts (Unity 2021.3.45f2 Mono, 0Harmony 2.4.2.0, real netstandard2.1
  mods) and **the skeleton was compiled successfully** with `dotnet build`:
  `skeleton/ExampleMod/ExampleMod.dll` (8192 bytes, refs only game assemblies, contains `IMod\0` fix
  passes, no capability triggers). Build harness `skeleton/build.ps1`, trust pre-seed
  `skeleton/seed-trust.ps1` (both exercised; scripts tested against a synthetic ES3 save).
* Real-world layouts/manifests corroborated (§9) and cited.

Not verified here (belongs to the verifier):
* actually launching the game with `skeleton/ExampleMod` deployed (expected log lines documented in
  `skeleton/README.md` §“Deploy”); the trust-seed script was only run against test copies, never the live
  `SaveFile.es3`;
* the Chinese UI wording of the enable/dev-mode buttons (button ids are `review`, `activate`, `devmode`,
  `settings`; English fallbacks documented in `ModUIManager.cs`);
* Nexus file contents (403).

**For the team:** the mod skeleton in `probably-stolen-mod/skeleton` is the base to build the real mod
from — rename `AssemblyName`/`ID`/manifest, keep `<Capabilities />` empty while the code avoids
`System.IO`/reflection/net/process, use `ModHelper` for file/data access, and verify with `mod-scan` after
each significant feature. The trust pre-seed script gives one-launch iteration with `developerMode=true`.

**Validation tooling (built for the team mod, reusable):**
* `skeleton/tools/scanner-check/` — a .NET console harness that loads the game assemblies from
  `Managed\` via an `AssemblyResolve` hook and calls the **real** `ModScanner.Scan(dllPaths,
  allowedRefs)`. Run:
  `dotnet run -c Release -- "<game dir>" "<mod dll>" <assembly name>`
  Exit code 0 = scanner would not refuse. It reproduced `Refused: False, 0 capabilities, 0 hard flags`
  for the shipped Item Details mod, the skeleton, and the team’s `PrecisionLab` mod (§13).
* `skeleton/tools/verify-mod-folder.ps1` — checks a dist folder against the manifest/IsWellFormed/
  fingerprint/localization/loot/texture rules and prints the exact `modTrust` record for seeding.
  `-SourceFolder` additionally cross-checks `Loc.Get/Loc.Fmt` keys against the CSVs.

---

## 12. ModHook event catalogue

All handlers are invoked through `SafeInvoke`; an exception in one handler is logged against its owning
mod (`ModHook.LogHandlerError`, `ModHook.cs:202-209`) and does not stop other mods. Subscribe in
`OnEnable`, unsubscribe in `OnDisable`.

| Event (signature) | Fires when | Fire site |
|---|---|---|
| `OnModItemDirectoryInit(ModItemDirectory)` | item directory initialised | `ModItemDirectory.InitDirectory` |
| `OnGameLoadedInit/Early/Normal/Late()` | save loaded into gameplay, staged | `PlayerStore.cs:968, 986, 997, 1066` |
| `OnNewGameStarted(string scenarioId)` | new game starts | `NewGameData.cs:401` |
| `OnDifficultyApplied(string difficultyId)` | difficulty applied | `PlayerStore.cs:790, 984` |
| `OnGenerateCustomerVeryEarly/Early/Normal/Late/VeryLate(StoreClientManager)` | customer generation pipeline; clients added by a handler are attributed to it | `StoreClientManager.cs:151-187` |
| `OnShutterOpenedEarly/Late`, `OnShutterClosedEarly/Late()` | store shutter | `StoreShutterButton.cs:110,190,214,250` |
| `OnLeavingStoreEarly/Late`, `OnReturningStoreEarly/Late()` | player leaves/returns | `EmporiumEntry.cs:655,787,817,837` |
| `OnGoingSleepEarly/Late`, `OnWakingUpEarly/Late()` | day transition | `PlayerStore.cs:1663,1742`, `StartOfDayUIManager.cs:49,55` |
| `OnHandlingNightlyServicesEarly/Late()` | nightly services | `PlayerStore.cs:1686,1706` |
| `OnPlaceInventorInventoryItemEarly/Late(List<GameItem>)` | inventor inventory placement | `StoreClientList.cs:493,553,587,1120` |
| `OnCreateTooltipEarly/Late(RichTextBuilder, GameItem)` and `OnCreateDevTooltipEarly/Late` | item tooltip build | `GameItemElement.cs:1030-1117` via `ModInternal` |
| `OnModAssetsLoaded(LoadedMod)` | after this mod’s textures/localization/loot/books/sounds loaded | `ModLoader.cs:1277` |
| `OnModConfigApplied(string modId)` | config window Apply | `ModConfigWindow.cs:233` |
| `OnLootTablesLoaded()` | vanilla tables+groups registered this scene | `LootRegistry.cs:117` |
| `OnLootRolled(LootRollContext)` | every loot roll (`context.itemId` mutable) | `LootRegistry.cs:364` |
| `OnPaperOpened(string fullId, PaperUI)` | paper book opened | `PaperBookRegistry.cs:332` |
| `OnNetworkUpgradeUnlocked(string id)` | network upgrade bought | `NetworkUpgrade.cs:856` |
| `OnAfterhourArriveEarly/Late`, `OnAfterhourLeaveEarly/Late`, `OnAfterhourEventStarted/Ended` | after-hours map/events | `MapUIManager.cs:174-253`, `AfterhourEvents.cs:176,226` |

Other useful API surfaces: `ModHelper` (large static facade: `SpawnItem`, `AddCredit`,
`MoveItemTo*`, `AddDebugFunction`, `AddGameOption*`, `RegisterLootTable*`, `AddNetworkUpgrade`,
`AddStartingScenario`, `AddStartingPerk`, `AddDifficulty`, `AddAppraisalItem`, `AddAfterhour*`,
`OpenPaper/RegisterPaperBook`, `GetModFolder`, `GetModDataFolder`, `ReadModFileText`, `SetModData*`);
`ModLog` (`[<Name>]: …` logging); `ModHelper.SetSource(item, modId, modName)` tags mod-made items so
tooltips/recorder attribute them; `ModSourceHelper` maps assemblies/clients to mods.

---

## 13. Case study: static validation of the team mod (`dist/PrecisionLab`)

The team’s real mod (`probably-stolen-mod/dist/PrecisionLab`, ID `precision_lab`) was validated against
the game’s actual code paths **without launching the game**, using the tooling from §11:

1. **Real scanner run** — `skeleton/tools/scanner-check` loads the game’s `Assembly-CSharp` and calls the
   actual `ModScanner.Scan(new[]{PrecisionLab.dll}, new[]{"PrecisionLab"})` (the same call
   `ModLoader.CheckEnableCore` / `EvaluateTrust` make). Result on the current build (36,352 B DLL,
   built 02:22):
   `methods scanned: 98 · error: none · capabilities: none · hard flags: 0 · findings: 0 ·
   Refused: False` → the game would accept and enable the mod with `<Capabilities />` empty and no
   capability prompt. (Harness sanity: the shipped Workshop mod ItemDetails.dll scans the same way,
   452 methods, Refused: False.)
2. **Reference check** — `PrecisionLab.dll` references only `netstandard 2.1.0.0`, `Assembly-CSharp`,
   `UnityEngine.CoreModule` — all present under `Probably Stolen_Data\Managed`, so the scanner’s
   `unlisted-reference` rule cannot fire; assembly name `PrecisionLab` is not a game assembly name; the
   `<Assembly>` entry matches the file name.
3. **Source audit** (consistent with the scanner result): no `System.IO`, `System.Net`,
   `System.Diagnostics.Process`, `System.Reflection`/`Assembly.Load`/`Activator.CreateInstance`,
   P/Invoke/`Marshal`, `AccessTools`/`Traverse`, `Expression.Compile`, `Application.OpenURL`, no
   `[HarmonyPatch]` (so no `loader-target`), no bundled `0Harmony`/`MonoMod`/`Mono.Cecil`, no long base64
   constants. Only game/Unity APIs (`ModHelper`, `ModHook`, `ModLoader.Instance`, `GameItem`,
   `MachineHelper`, `WaterHelper`, `Liquid`, …) are used.
4. **Manifest** — parses with `XDocument`; root `<Manifest>` (accepted); `<ID>precision_lab</ID>` valid;
   `<Assemblies><Assembly>PrecisionLab.dll</Assembly></Assemblies>` matches the file;
   `<Capabilities />` present/empty; `<GameVersions><Version>0.46</Version>` matches `Application.version`
   `0.46D` under `ModUIManager.VersionMatchesGame`; `IsWellFormed` passes (IMod bytes present; no
   unlisted DLLs in root/`Assemblies`).
5. **Fingerprint** (computed with the §3.2 algorithm; `skeleton/tools/verify-mod-folder.ps1 -ModFolder
   dist\PrecisionLab` reproduces it):
   `manifestHash e1a4fd5b…`, `PrecisionLab.dll|8997c034…`, `combinedHash b40efc5a…` → the trust record to
   seed is `modOrder += "precision_lab"` plus a `modTrust` record with those hashes and
   `source:"Local", workshopId:"0"` (`skeleton/seed-trust.ps1 -ModFolder dist\PrecisionLab` computes and
   writes it; `developerMode:true` keeps rebuilds trusted).
6. **LootTables** — `precision_lab_loot.json` matches `ModLootFileJson`/`ModLootPatchJson` exactly
   (top-level `tables`/`groups`/`patches`, each patch has one of `table`/`group`, `add` entries with
   `id`+positive `weight`). Patch targets are real vanilla table ids (`medicalTable`, `junkTable` from
   `TableMaster`). The added item ids (`precision_liquid_extractor`, `precision_purifier`) do not collide
   with any game/DirectoryMaster id and are registered by the mod in `ModHook.OnModItemDirectoryInit` via
   `ModItemDirectory.Add` — the same `DirectoryMaster.Item` path loot spawning uses. Residual (runtime,
   low risk): a loot roll that happens *before* `ModItemDirectory.Awake` would create a dummy item with a
   `Could not find GameItem identifier` warning; normal scene order initialises the directory before
   gameplay loot rolls.
7. **Localization** — `Localization/en.csv` and `zh.csv`: 30 entries each, UTF-8 (no BOM), parse clean
   under the `ModLoader.LoadLocalization` rules (comment/`#` lines, first-comma split, quote unescaping,
   `\n`), identical key sets, no duplicate/empty keys/values, `{0}`/`{1}` placeholders consistent across
   locales, and all 30 `Loc.Get`/`Loc.Fmt` keys used in the source exist in both files. `en.csv` is not
   strictly required (`ModHelper.GetLocalized` falls back to the `en` table and `Loc.Get` has hardcoded
   English fallbacks) but shipping it is the clean choice; the running locale here is `zh`.
8. **Textures** — `Textures/Items/*.png` are 16×16 → each becomes a 1×1 grid-cell item shape through
   `SetSpriteAndShapeFromMod` (grid size 16), which is what `ItemFactory` expects.
9. **Loader rules** — exactly one `IMod` implementation (`PrecisionLab.ModEntry`, public sealed with an
   implicit public parameterless ctor); DLL/folder layout per §1.2; no stray files that affect loading.

**Concrete deployment items found (for the verifier):**

* `<game>\Mods\PrecisionLab\` currently holds a **stale build** (32,280 B DLL,
  `combinedHash 33dc15ab…`) while `dist\PrecisionLab` has the newer build (36,352 B,
  `combinedHash b40efc5a…`). Re-copy `dist\PrecisionLab\*` over the deployed folder.
* The live `SaveFile.es3` (`modOrder` = `item_details, zz_verify_hello` at the time of writing) has **no
  `precision_lab` record**, so a fresh launch would register it inactive (`Unknown`). Seed it with
  `skeleton/seed-trust.ps1 -ModFolder "F:\...\dist\PrecisionLab"` **while the game is closed** (it backs
  the file up), or enable it once in the Mods panel.
* The game must be restarted after copying/seeding (mods load only in `ModLoader.Awake`); expected log
  lines: `[ModLoader] Loading: PrecisionLab.dll`, `[ModLoader]   Harmony: 0 method(s) patched`,
  `[ModLoader]   Initialized: Precision Lab Equipment …`, `trust=…`, plus the mod’s own
  `[PrecisionLab]: …` lines and `[PrecisionLab] SELFTEST …` after a save loads.
* In-game double-check with dev tools (`dev.txt` present): console command `mod-scan precision_lab`
  should print the same “declared: none / detected: none / no hard flags” result, and
  `mod-trust precision_lab` shows the session trust state.

Residual, non-blocking runtime risks (framework-level, independent of scanner conformance): the network
upgrade tab is registered with `iconKey = null` (`ModHelper.AddNetworkUpgrade`) so the Wild Network tab
may show a blank icon; the fallback `ModItemDirectoryFallback` path only runs when the normal
`OnModItemDirectoryInit` registration was missed.