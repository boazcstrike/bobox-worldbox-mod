# In-game smoke test

Checks the runtime criteria that unit tests cannot reach: the mod loads, map sizes work, the
scenario and build brains run, and the rules file reloads. About 10 minutes.

Run the `curl` lines in Git Bash. A `POST` needs a body, even an empty one (`-d ''`), or the
bridge answers 411.

## Steps

1. Start WorldBox. Open **Settings**, turn on **Experimental mode**, and restart the game.
2. Check the mod answers:

   ```bash
   curl -s 127.0.0.1:8765/status
   ```

   Expect JSON with `world_loaded` and `loading`. No answer means the mod did not load: check
   `Player.log` (step 9).
3. List the map sizes:

   ```bash
   curl -s 127.0.0.1:8765/mapsizes
   ```

   Expect `bobox_10` to `bobox_16`, with `bobox_16` at 1024 tiles.
4. Make a size-16 world:

   ```bash
   curl -s -X POST 127.0.0.1:8765/world/new -d '{"size":"bobox_16"}'
   ```

   Repeat `curl -s 127.0.0.1:8765/status` until `loading` is `false`. Then `map_width` must be
   `1024`.
5. Drop settlers on land (pick a land tile; water or mountain answers 400):

   ```bash
   curl -s -X POST 127.0.0.1:8765/scenario/settlers -d '{"asset":"human","x":512,"y":512,"count":5}'
   ```

   Expect `spawned: 5`. Let the game run at speed x5 for a few minutes until they found a city.
6. Check the scenario brain:

   ```bash
   curl -s 127.0.0.1:8765/colony/scenarios
   ```

   Each city shows signals, tags, a pathway, limits, `rules_loaded_at`, and a `last_build`
   choice with a reason and `at`.
7. Check the job brain:

   ```bash
   curl -s 127.0.0.1:8765/colony
   ```

   Each city shows a plan with reasons and an `idle` entry.
8. Reload the rules:

   1. `curl -s 127.0.0.1:8765/rules` and note `loaded_at`.
   2. Edit `worldbox_Data/StreamingAssets/mods/bobox-rules.json`, for example set
      `"refresh_seconds": 6.0`, and save.
   3. `curl -s -X POST -d '' 127.0.0.1:8765/rules/reload`. Expect `ok: true`.
   4. About 8 seconds later, `GET /colony/scenarios` shows the new `rules_loaded_at` per city.
9. Check the log, `%USERPROFILE%\AppData\LocalLow\mkarpenko\WorldBox\Player.log`:
   - `[BoBox] build brain installed` is present.
   - `[BoBox]` lines carry no repeating error stack traces.

## What to report back

- Which step failed, and the first `[BoBox]` error line from `Player.log`.
- On a pass: the frame rate on the size-16 map, and whether cities sit idle or build only houses.
