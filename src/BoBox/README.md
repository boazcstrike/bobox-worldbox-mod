# BoBox

A WorldBox mod that runs a small local HTTP API, so scripts can read and drive the game.
It listens on `127.0.0.1` only (default port `8765`) and takes JSON.

## Install

1. Run `scripts/deploy.sh` from the repo root. It builds the mod and copies `BoBox.dll`, `BoBox_memload.dll`, and `BoBox_memload.pdb` to
   `worldbox_Data/StreamingAssets/mods/`. The game loads the `_memload` copy from memory, so the files stay
   writable and you can redeploy while the game runs; restart the game to load the new build. Set `WORLDBOX_DIR` if the game is not in
   `D:/SteamLibrary/steamapps/common/worldbox`.
2. In WorldBox, turn on **experimental mode** in the settings. The game loads mods only in that mode, and
   it switches itself off after each game update.
3. Restart the game and load a world. The log shows `[BoBox] listening on http://127.0.0.1:8765/`.

Set `BOBOX_PORT` (1024-65535) before starting the game to use another port.

## Endpoints

| Method and path | Body | Returns |
| --- | --- | --- |
| `GET /status` | none | `world_loaded`, `loading`, `unit_count`, `kingdom_count`, `speed`, `map_width`, `map_height`. `loading` is true while a world is being generated or loaded, and `world_loaded` is false then. The other fields appear only when `world_loaded` is true |
| `GET /kingdoms` | none | civilization kingdoms: `id`, `name`, `population`, `city_count`, `king` |
| `GET /actors/assets` | none | all actor asset ids |
| `GET /speeds` | none | speed ids and the current one |
| `POST /speed` | `{"id": "x3"}` | the new speed |
| `POST /spawn` | `{"asset": "human", "x": 100, "y": 100, "count": 5}` | spawn result. `count` defaults to 1, max 100. Returns 400 when a land actor's tile is water, lava or mountain |
| `GET /powers` | none | god powers, with `fireable` and `requires_premium` |
| `POST /power` | `{"id": "...", "x": 100, "y": 100}` | fires the power at the tile |
| `POST /scenario/settlers` | `{"asset": "human", "x": 100, "y": 100, "count": 5}` | spawns adult settlers, then turns on the `world_law_kingdom_expansion` law (only if at least one spawned) so they found a city. The law is saved with the world and stays on after the scenario. `count` defaults to 5, max 20 |
| `GET /colony` | none | `brain_enabled`, and per city: `id`, `name`, `adults`, `food`, `wood`, `stone`, and the last brain `plan` (`job`, `slots`, `reason` per line; `null` before the first check) |
| `GET /colony/log` | none | the last 200 brain decisions, oldest first: `time_utc`, `city_id`, `city`, `kind` (`plan` or `error`), `summary`. A plan is logged only when it changes |
| `POST /brain` | `{"enabled": false}` | turns the colony brain on or off. When off, cities use the game's own job logic. On by default |

## Colony brain

After a world loads, BoBox replaces the game's city job check with its own planner (see
`docs/colony-brain.md`). The log shows `[BoBox] colony brain installed: swapped N job steps`. If a city's
plan fails, that city keeps the game's jobs and the error appears in `GET /colony/log`.

Errors return `{"error": "..."}` with a 4xx or 5xx status. Requests that carry an `Origin` header (browsers)
are refused. `POST` requests must send `Content-Length` (otherwise 411). At most 16 requests are handled at once;
the 17th gets 503.

An endpoint that needs a world returns 409 while one is loading (`world is loading`) or when none exists. `GET /actors/assets`, `GET /speeds`, `POST /speed`, `GET /powers`, `GET /mapsizes` and `POST /world/new` return 409 `game not loaded yet` before the game has finished starting. `POST /spawn` and `POST /scenario/settlers` return 400 when the tile is water, lava or a mountain and the actor walks on land. A `POST` body that is not fully received within 5 seconds drops the connection. If the game main thread does not answer in time the client gets 504. An unexpected failure returns 500 with `internal error, see Player.log`; the detail is only in the log.

`POST /power` calls the same click delegates the game calls when you click a power. Powers that depend on
the mouse press state (some terrain drawing tools) can throw midway; the client then gets 422 `power failed mid-action` and the power may have taken
partial effect. The exception is in the game log.

## Examples

```bash
curl http://127.0.0.1:8765/status
curl http://127.0.0.1:8765/speeds
curl -X POST http://127.0.0.1:8765/speed -d '{"id":"x3"}'
curl -X POST http://127.0.0.1:8765/spawn -d '{"asset":"human","x":256,"y":256,"count":5}'
curl -X POST http://127.0.0.1:8765/scenario/settlers -d '{"asset":"human","x":256,"y":256}'
curl http://127.0.0.1:8765/colony
curl -X POST http://127.0.0.1:8765/brain -d '{"enabled":false}'
```

Or run the Python client: `uv run scripts/example.py`.

## Build only

```bash
bash scripts/verify.sh              # release build, then planner unit tests (no game needed)
```

The project targets `net472` (WorldBox runs Unity Mono on the .NET Framework profile) and references the
game's DLLs from `WorldBoxDir` (MSBuild property, default `D:\SteamLibrary\steamapps\common\worldbox`).
