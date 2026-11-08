# AGENTS.md: BoBox

Read this before changing anything. The user-facing overview is [README.md](README.md).

## What this project is

BoBox is a WorldBox mod (`BoBox.dll`). The goal is a self-running colony:
start with a few settlers, and they decide farming, building, and job assignment on their own,
like a Dwarf Fortress colony. Every feature should serve that goal.

## Hard rules

1. **Never commit game code or assets.** `decompiled/` is the game's own code, decompiled
   for reference. It stays gitignored. Do not copy large chunks of it into `src/`. Call the
   game's types instead.
2. **Mod, not a clone.** BoBox runs inside the user's copy of WorldBox. Do not rebuild
   WorldBox as a standalone game, and do not ship its sprites or data.
3. **Unity main thread only.** Every call into game or Unity objects runs on the main thread
   (`Update`/`LateUpdate`). Background threads, such as the HTTP listener, only queue work.
4. **Never crash the game.** Wrap each tick and each request in `try/catch`, and log with the
   `[BoBox]` prefix. One failing city must not stop the others.
5. **Localhost only.** The HTTP API binds `127.0.0.1`. Never bind `0.0.0.0`, `+`, or `*`.
6. **Patch narrowly.** When game behavior must change, use one Harmony prefix or postfix on
   the exact method, and record it in the "Patched methods" table below.

## Build, deploy, verify

```bash
bash scripts/verify.sh              # gate: Release build (0 errors) + planner tests (all pass)
bash scripts/deploy.sh              # copies BoBox.dll, BoBox_memload.dll and BoBox_memload.pdb into the mods folder
uv run scripts/example.py           # runtime smoke check, needs the game running
```

- Deploy uses the loader's `_memload` path (`decompiled/ModLoader.cs`). The game loads
  `BoBox_memload.dll` from bytes, so the files stay writable while the game runs. `BoBox.dll`
  only anchors the name. Changes still need a game restart to load.
- The test project compiles `src/BoBox/Planner/**` and `src/BoBox/Rules/**` with
  `LangVersion 9.0`, the same as the mod. Do not use C# features newer than 9 anywhere in `src/`.
- Deploy also copies `rules/bobox-rules.json` to `StreamingAssets/mods/bobox-rules.json`, but only
  when that file is missing. Pass `--force-rules` to overwrite a file you edited. When it keeps
  a file that differs from the shipped one, it prints a notice.

- Game install: MSBuild property `WorldBoxDir`, default `D:\SteamLibrary\steamapps\common\worldbox`.
  `deploy.sh` and `verify.sh` take it from the `WORLDBOX_DIR` environment variable.
- Game references come from `worldbox_Data/Managed/` with `Private=false`. Never copy them
  into the repo or the mods folder.
- Game log: `%USERPROFILE%\AppData\LocalLow\mkarpenko\WorldBox\Player.log`.
- A clean build is not a runtime test. Only call something verified in game after the game
  has loaded it and the log or API shows it working.
- Python goes through `uv` only.

### Refreshing the decompiled code after a game update

```bash
ilspycmd -p -o decompiled -r "<WorldBox>/worldbox_Data/Managed" \
  "<WorldBox>/worldbox_Data/Managed/Assembly-CSharp.dll"
```

Then rebuild. Renamed or removed game methods show up as compile errors.

## Game map

These are the game's own types, in `decompiled/`. Paths are relative to that folder.

### Loading

| Symbol | File | Notes |
| --- | --- | --- |
| `ModLoader.Initialize` | `ModLoader.cs` | Loads `mods/<Name>.dll` and adds the `<Name>.WorldBoxMod` component. Needs `Config.experimental_mode` |
| `AssetModLoader` | `AssetModLoader.cs` | JSON/PNG asset loader. Nothing calls it, so it is dead code. Register assets from C# |
| `Config.game_loaded`, `Config.MODDED` | `Config.cs` | Wait for `game_loaded` before touching the world |

### World and units

| Symbol | File | Notes |
| --- | --- | --- |
| `World.world` | `World.cs` | The `MapBox` singleton |
| `MapBox.kingdoms`, `MapBox.units` | `MapBox.cs` | Kingdom manager and actor manager |
| `ActorManager.spawnNewUnit(assetId, tile, ...)` | `ActorManager.cs` | Spawn a unit on a `WorldTile` |
| `Config.setWorldSpeed`, `AssetManager.time_scales` | `Config.cs` | Game speed |
| `AssetManager.actor_library`, `AssetManager.powers` | `AssetManager.cs` | Asset registries |

### Colony automation: the systems BoBox takes over

WorldBox already runs a per-city labor system. BoBox's colony brain hooks into it instead of
replacing the AI.

| Symbol | File | What it does today |
| --- | --- | --- |
| `CityBehCheckCitizenTasks.execute(City)` | `ai.behaviours/CityBehCheckCitizenTasks.cs` | Recomputes `city.jobs` each check: counts fires, resources, roads, and food, then adds job slots with hard-coded weights (for example food under 20 gives 10x gatherer weight) |
| `CitizenJobs` | `CitizenJobs.cs` | `jobs` (open slots) and `occupied` per `CitizenJobAsset` |
| `City.setCitizenJob(Actor)` | `City.cs` (around line 1851) | Gives an idle citizen a job: warrior first, then the high-priority list, then high-food when starving, then a round-robin of the normal list |
| `CitizenJobLibrary` | `CitizenJobLibrary.cs` | Job ids: `builder`, `gatherer_bushes`, `gatherer_herbs`, `gatherer_honey`, `farmer`, `hunter`, `woodcutter`, `miner`, `miner_deposit`, `road_builder`, `cleaner`, `manure_cleaner`, `attacker` |
| `CitizenJobAsset` | `CitizenJobAsset.cs` | `priority`, `priority_no_food`, `only_leaders`, `should_be_assigned` |
| `CityBehCheckFarms` | `CityBehCheckFarms.cs` | Farm placement (`city.calculated_place_for_farms`) |
| `BuildOrderLibrary` | `BuildOrderLibrary.cs` | Build order lists: `build_order_basic`, `build_order_basic_2`, `build_order_advanced` |
| `ActorJobLibrary`, `CityJobLibrary`, `KingdomBehaviour*` | various | Actor, city, and kingdom behavior trees |

### Planned hook points

| Goal | Hook | State |
| --- | --- | --- |
| BoBox decides job counts | Swap the `CityBehCheckCitizenTasks` step in `tasks_city` `do_checks` and `do_initial_load_check` for `BoBoxCitizenTasks` (calls `base`, then rewrites `city.jobs`) | Done, no Harmony |
| BoBox decides who gets which job | Prefix on `City.setCitizenJob` | Planned, needs Harmony |
| BoBox decides what to build | Swap `CityBehBuild` in `tasks_city["build"]` for `BoBoxCityBuild`. It picks from the candidates that pass the game's own `canUseBuildAsset` and `hasResourcesForBuildAsset`, then builds with `tryToBuild` or `upgradeBuilding` | Done, no Harmony |
| Bigger maps | Add `MapSizeAsset` entries `bobox_10` to `bobox_16` to `AssetManager.map_sizes`, then `linkAssets()` | Done, no Harmony |

Harmony is not in the game's `Managed/` folder. Prefer patch-free hooks: swap assets or steps
in the game's public libraries. Bundle Harmony only for a hook that has no public seam, and
record that decision here.

## Patched methods

| Target | How | Why |
| --- | --- | --- |
| `tasks_city` `do_checks` and `do_initial_load_check` | Step swap in `BehaviourTaskCity.list` (`Brain/BrainInstaller.cs`) | Colony brain job counts |
| `tasks_city` `build` | Step swap of `CityBehBuild` for `BoBoxCityBuild` (`Brain/BuildInstaller.cs`). Matches the exact type, so a second install does not wrap BoBox's own step | Build brain |
| `AssetManager.map_sizes` | Adds `bobox_10` to `bobox_16`, plus their locale names, re-added when the language changes (`Game/MapSizes.cs`) | Map sizes up to 1024 × 1024 |
| `City` internals, in three groups (`Game/CityInternals.cs`) | Reflection, bound once to delegates or fields. Each group has its own `Is*Available` flag | If a game update renames a member, only that group's feature turns off and the game's own step stays |

`CityInternals` groups:

| Group | Members | Feature that stops when a member is missing |
| --- | --- | --- |
| Jobs (`IsAvailable`) | `hasStorageBuilding`, `hasBuildingToBuild`, `hasBuildingType` | Job planner |
| Signals (`IsSignalsAvailable`) | fields `kingdom`, `zones`, `neighbour_zones`, `border_zones`, `neighbours_cities`, `neighbours_kingdoms` | Scenario signals. Snapshot resource counts fall back to `city.tasks` |
| Build (`IsBuildAvailable`) | the jobs group plus `zones`, `under_construction_building`, `getBuildingListOfID`, `hasEnoughResourcesFor` | Build brain |

## Known issues

- **Size-16 explosions.** `ExplosionChecker` builds an integer key from a tile's x and y. On a
  1024-tall map, two tiles collide once y is above 999. The fix needs Harmony, so it is
  documented only.
- **Workshop upload.** `Config.maxMapSize` stays at the game's iceberg size on purpose. A world
  made at a bobox size cannot be uploaded to the Workshop.

## Code layout

One folder and one namespace per layer. Dependencies point one way:
`Api` and `Brain` may use `Game`, `Bridge`, and `Planner`.

| Path | Owns | May use |
| --- | --- | --- |
| `src/BoBox/WorldBoxMod.cs` | Entry point. Starts the bridge, installs the brain, pumps the main-thread queue | everything |
| `src/BoBox/Log.cs` | `Log.Info`, `Log.Warn`, `Log.Error` with the `[BoBox]` prefix | |
| `src/BoBox/Bridge/` | HTTP transport, request and response types, field parsing | no game types |
| `src/BoBox/Api/` | `Router`, then one `*Endpoints.cs` per feature area, each with its own route list | Game, Bridge, Brain, Planner |
| `src/BoBox/Game/` | Thin wrappers over WorldBox internals (`GameState`, `Spawner`, `PowerDispatcher`, `CityInternals`) | game types, `Bridge.BridgeException` |
| `src/BoBox/Brain/` | Hooks into the game's city behaviour: job step, build step, city snapshot, scenario cache and signal reads, active rules, decision log | Game, Planner, Rules |
| `src/BoBox/Planner/` | Pure decisions. No game or Unity types. Unit tested | Rules |
| `src/BoBox/Planner/Scenario/` | Signals, the 7 scenario tags, the classifier, pathways, pathway hysteresis (`PathwayHold`), the refresh budget, and the thread-safe all-due flag | Rules |
| `src/BoBox/Planner/Capacity/` | `CapacityModel`: min and max per job and building type, from area, population and pathway | Rules |
| `src/BoBox/Planner/Build/` | `BuildPlanner` (what to build next), `BuildFailureMemory` | Rules |
| `src/BoBox/Rules/` | `ColonyRules` model and defaults, `RulesLoader` (JSON, clamps, warnings) | Planner types, Newtonsoft |
| `rules/bobox-rules.json` | The shipped rules file. It mirrors `ColonyRules.Default`, and a test enforces that | |
| `tests/BoBox.Tests/` | xunit tests. Links `Planner/**` and `Rules/**` | |
| `scripts/` | `verify.sh` (gate), `deploy.sh`, `example.py` | |
| `docs/` | Findings, design notes, game research, smoke test. Generated Markdown goes here, not the root | |

To add an endpoint area, create `Api/<Area>Endpoints.cs` with a `Routes` list and register it
in `Router`. Do not add routes to another area's file.

Keep decisions in `Planner/` as pure functions: a snapshot in, a plan out. Game reads and
writes stay in `Brain/` and `Game/`. A new rule needs a planner test.

`Planner/` and `Rules/` form one pure layer. The planner reads the rule model types
(`ColonyRules`, `SignalRules`, `ScenarioThresholds`, `CapacityRules`). The rules model reuses
planner types such as `Pathway`. Planner code must never call `RulesLoader`, `RulesJson`, or
Newtonsoft. Only `Brain/ActiveRules.cs` loads the file.

## Rules file

- The live file is `StreamingAssets/mods/bobox-rules.json`. `POST /rules/reload` reloads it.
- A missing file means the defaults. Invalid JSON keeps the last good rules and reports the error.
- Unknown keys are ignored with a warning. Numbers outside their range are clamped with a warning.
- Ranges live in `Rules/RulesLimits.cs`. Signal ranges come from `Rules/SignalBounds.cs`, and the
  runtime clamp uses the same constants.
- A new numeric field needs a `RulesLimits` entry. A test fails when one is missing.
- After a good load, `ScenarioCache.MarkAllDue()` refreshes every city with the new rules.
- Capacity fields and the formula, including `max_per_pop`, are documented in `docs/colony-brain.md`.

## Commits

- Conventional Commits: `feat:`, `fix:`, `docs:`, `refactor:`, `chore:`, `test:`.
- Keep messages short and in first person: what changed and why.
- A coding agent that wrote the change adds a `Co-authored-by:` trailer naming itself.
- Contributor workflow, PR checklist and bug-report format: [CONTRIBUTING.md](CONTRIBUTING.md).
