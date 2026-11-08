# BoBox

[![CI](https://github.com/boazcstrike/bobox-worldbox-mod/actions/workflows/ci.yml/badge.svg)](https://github.com/boazcstrike/bobox-worldbox-mod/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET Framework 4.7.2](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4?logo=dotnet)](src/BoBox/BoBox.csproj)
[![C# 9](https://img.shields.io/badge/C%23-9.0-239120?logo=csharp)](src/BoBox/BoBox.csproj)
[![WorldBox mod](https://img.shields.io/badge/WorldBox-mod-6aa84f)](https://store.steampowered.com/app/1206560/WorldBox__God_Simulator/)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows)](#requirements)

BoBox is a mod for **[WorldBox](https://store.steampowered.com/app/1206560/WorldBox__God_Simulator/)**
that turns the god simulator into a self-running colony game. You drop a handful of settlers on
empty land. They found a city, and from then on the colony decides for itself who farms, who
builds, and what gets built next and where, the way a Dwarf Fortress colony runs on its own labor
and priorities.

You watch, set high-level goals, and nudge. The colony does the planning.

> BoBox is a fan-made mod. It is not affiliated with or endorsed by the makers of WorldBox. It
> contains no WorldBox code or assets: it runs inside a copy of the game you own.

## Features

- **Colony brain.** Replaces the game's fixed job split per city with a planner: food stock sets
  the number of farmers and gatherers, waiting buildings set the builders, and every decision is
  logged with its reason.
- **Scenario brain.** Reads each city's land (trees, ore, farmland, coast, neighbours) and tags it
  with up to 7 scenarios, such as `forest_rich`, `rocky`, `coastal` or `scarce`. The tags pick a
  pathway that steers job weights and build order.
- **Build brain.** Chooses what each city builds next. It keeps every type between a minimum and a
  maximum that grow with the city's land and population.
- **Bigger maps.** Seven new map sizes, `bobox_10` to `bobox_16`, up to 1024 × 1024 tiles.
- **Settler start.** Spawns a few adult settlers on empty land and lets them found a city.
- **Rules file with live reload.** Every threshold, pathway and limit lives in one JSON file. Edit
  it while the game runs and reload it over the API.
- **Local HTTP API.** Read and drive the game from scripts on `127.0.0.1:8765`.

## Status

| Part | State |
| --- | --- |
| Mod loads through the game's built-in loader | Works in game |
| Local HTTP API (status, kingdoms, spawn, speed, powers) | Works in game |
| Colony, scenario and build brains | Work in game; tuning ongoing |
| Map sizes up to 1024 × 1024 | Work in game; performance not yet measured |
| Rules file with live reload | Works in game |
| Settler start scenario | Works in game |
| Player goals and priorities (more food, expand, fortify) | Planned |
| Custom buildings, jobs and sprites | Planned |

## How it works

WorldBox is a Unity game built on Mono, and it ships a DLL mod loader that is off by default:

1. On startup the game scans `worldbox_Data/StreamingAssets/mods/` for `*.dll` files.
2. For `BoBox.dll` it finds the class `BoBox.WorldBoxMod` and attaches it to the scene.
3. This happens only when **Experimental mode** is on in the game's settings.

BoBox then swaps two steps in each city's behaviour list: the job check and the build step. It
needs no Harmony patches. The decisions themselves live in a pure planner layer with no game
types, so they are unit tested outside the game. Design notes:
[docs/colony-brain.md](docs/colony-brain.md).

## Requirements

- WorldBox on Steam, for Windows
- [.NET SDK 8 or newer](https://dotnet.microsoft.com/download) (`dotnet --list-sdks`). The test
  project targets `net10.0`.
- [Git for Windows](https://gitforwindows.org/), for Git Bash and the scripts
- Optional: [uv](https://docs.astral.sh/uv/), for the Python example client

## Install

1. Clone the repo:
   ```bash
   git clone https://github.com/boazcstrike/bobox-worldbox-mod.git
   cd bobox-worldbox-mod
   ```
2. Point the build at your WorldBox folder, if it is not at
   `D:\SteamLibrary\steamapps\common\worldbox`:
   ```bash
   export WORLDBOX_DIR="C:/Program Files (x86)/Steam/steamapps/common/worldbox"
   ```
3. Build, test and copy the mod into the game:
   ```bash
   bash scripts/verify.sh
   bash scripts/deploy.sh
   ```
   Both scripts read `WORLDBOX_DIR`. A plain `dotnet build` takes
   `-p:WorldBoxDir="<path>"` instead.
4. Start WorldBox, open **Settings**, turn on **Experimental mode**, and restart the game.
5. Check the mod answers:
   ```bash
   curl -s 127.0.0.1:8765/status
   ```

The game turns Experimental mode off after every game update. Turn it back on after updates.
Deploy copies `BoBox.dll`, `BoBox_memload.dll` and `BoBox_memload.pdb`. The game loads the
memload copy into memory, so you can redeploy while it runs. Restart the game to load a new build.

## Quick start

```bash
# a 1024 x 1024 world; poll /status until "loading" is false
curl -s -X POST 127.0.0.1:8765/world/new -d '{"size":"bobox_16"}'

# five settlers on a land tile; a 400 answer means water or mountain, so pick another tile
curl -s -X POST 127.0.0.1:8765/scenario/settlers -d '{"asset":"human","x":400,"y":400,"count":5}'

# what each city's brain decided, and why
curl -s 127.0.0.1:8765/colony/scenarios
curl -s 127.0.0.1:8765/colony
```

A `POST` needs a body, even an empty one (`-d ''`), or the API answers 411. Full in-game check:
[docs/smoke-test.md](docs/smoke-test.md).

## API

BoBox listens on `http://127.0.0.1:8765`, this PC only. Set the `BOBOX_PORT` environment
variable before starting the game to change the port.

| Endpoint | What it does |
| --- | --- |
| `GET /status` | Whether a world is loaded or loading, unit and kingdom counts, speed, map size |
| `GET /mapsizes` | Map sizes with their tile width, including `bobox_10` to `bobox_16` |
| `POST /world/new {"size":"bobox_16"}` | Generates a new world at that size. 400 for an unknown id, 409 while loading |
| `POST /scenario/settlers` | Spawns adult settlers and lets them found a city |
| `GET /colony` | Each city's job plan, with reasons and idle adults |
| `GET /colony/scenarios` | Each city's signals, scenario tags, pathway, limits and last build choice |
| `GET /rules` / `POST /rules/reload` | The active rules, and a reload of the rules file |

The full endpoint list: [src/BoBox/README.md](src/BoBox/README.md). Example client:
`uv run scripts/example.py`.

## Rules file

The colony's thresholds, pathways and limits live in
`worldbox_Data/StreamingAssets/mods/bobox-rules.json`.

- Deploy copies the shipped [rules/bobox-rules.json](rules/bobox-rules.json) there only when the
  file is missing, so your edits survive a redeploy. Pass `--force-rules` to overwrite them.
- Edit the file, then run `curl -X POST -d '' 127.0.0.1:8765/rules/reload`. Every city refreshes on
  the next tick. A city switches pathway only after the new one wins 2 refreshes in a row, about
  8 seconds after a reload.
- A broken file keeps the last good rules. The reload response and `Player.log` say what is wrong.
- Out-of-range numbers are clamped and unknown keys are ignored, each with a warning.
- Field reference, tag rules and the capacity formula: [docs/colony-brain.md](docs/colony-brain.md).

## Troubleshooting

- **No answer on port 8765.** Experimental mode is off, or the game was not restarted after
  turning it on. Check `%USERPROFILE%\AppData\LocalLow\mkarpenko\WorldBox\Player.log` for lines
  starting with `[BoBox]`.
- **Build fails with missing `Assembly-CSharp`.** The build cannot find your WorldBox install.
  Set `WORLDBOX_DIR` or pass `-p:WorldBoxDir=...`.
- **Settlers never build farms.** The game gives farmer jobs only after a city has a windmill.

## Known issues

- **Explosions on size-16 maps.** The game's explosion check builds a key from tile x and y. On a
  1024-tall map, two tiles can share a key once y is above 999, so some explosions can misbehave
  near the top of the map. A fix would need Harmony, which BoBox does not use.
- **Workshop.** A world made at a bobox size cannot be uploaded to the Steam Workshop.
- **Performance.** A size-16 map has about 3 times the tiles of the game's largest size. Memory
  use and frame rate at that size are not measured yet.

## Repo layout

| Path | What |
| --- | --- |
| `src/BoBox/` | The mod: C# project that builds `BoBox.dll` |
| `src/BoBox/Planner/`, `src/BoBox/Rules/` | Pure decision and rules code, with no game types |
| `rules/` | The shipped rules file, `bobox-rules.json` |
| `tests/BoBox.Tests/` | xunit tests for the planner and rules code. They run without the game |
| `scripts/` | `verify.sh` (build and test), `deploy.sh`, and the example API client |
| `docs/` | Design notes, game research and the in-game smoke test |
| `AGENTS.md` | Architecture, game hook points and rules for contributors and coding agents |

## Contributing

Bug reports, ideas and pull requests are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) first.
The most important rule: never commit WorldBox's own code or assets.

## License

[MIT](LICENSE). WorldBox itself is not covered by this license and is not included.
