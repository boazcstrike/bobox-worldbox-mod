# Research: map sizes above iceberg

Read-only research against `decompiled/`, 2026-11-08.

## Size and memory

- Map size N means N × 64 tiles per side. The largest built-in size is iceberg (9), which is
  576 × 576 tiles. Size 16 is 1024 × 1024 tiles, about 3.2 times iceberg's area.
- Expected memory for a size 16 map is roughly 0.5 to 1 GB or more. Load time and save size grow
  about 3 times.

## Adding sizes

1. Add `MapSizeAsset` entries with `AssetManager.map_sizes.add(...)`. Each needs `id`, `size`,
   `path_icon` and `show_warning`.
2. Call `AssetManager.map_sizes.linkAssets()`. This rebuilds the private `_mapSizes` array, which
   the menu reads. Mods load after the array was built, so this step is required.
3. Add the display name with `LocalizedTextManager.add("map_size_<id>", text, true)`.
   - A language switch rebuilds the text table and drops these names.
   - After a switch, missing names show as their key. They do not crash.

## Limits that do not block us

- The new-world menu cycles every registered size.
- Loading a save does not check its size.
- `Config.maxMapSize` ("iceberg") only gates Workshop uploads. Leave it alone.

## Starting a new world from code

1. Set `Config.customMapSize = id`.
2. Call `World.world.generateNewMap()`.

An unknown id makes the menu throw. Never remove a size while it is selected.

## Known break at size 16

The `ExplosionChecker.cs:14` key `range*10000000 + x*1000 + y` collides once y reaches 1000. The
effect is wrong explosion deduplication, not a crash. Sizes 10 to 15 (max 960 tiles) are safe.

Fixing it needs a Harmony patch. Region-link hash collisions (`MapRegion.cs:148`) also grow with
map size. They already exist at every size.
