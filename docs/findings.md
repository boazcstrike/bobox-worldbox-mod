# WorldBox reverse-engineering findings

Install: the Steam folder `steamapps/common/worldbox`, written `<WorldBox>` below.

## Build type
- Unity **Mono** build (not IL2CPP). Game code is `worldbox_Data/Managed/Assembly-CSharp.dll`, shipped with its `.pdb`.
- Decompiled with `ilspycmd` 11.2 into `decompiled/` (2441 `.cs` files). That folder is gitignored: it is the game's code, kept local only.
- Re-run after a game update:
  `ilspycmd -p -o decompiled -r <WorldBox>/worldbox_Data/Managed <WorldBox>/worldbox_Data/Managed/Assembly-CSharp.dll`

## Built-in DLL mod loader (`ModLoader.cs`)
- Folder: `worldbox_Data/StreamingAssets/mods/`.
- Loads every `*.dll` there. A DLL named `Foo.dll` must contain a `MonoBehaviour` class `Foo.WorldBoxMod`; the game adds it to a new GameObject.
- `Foo_memload.dll` (+ optional `.pdb`) is loaded from bytes instead, so the file is not locked while the game runs.
- Only runs when `Config.experimental_mode` is true. That is the "experimental mode" option in the game's settings. It switches itself off whenever the game version changes.
- Loading a mod sets `Config.MODDED = true`, which skips crash metadata upload.

## Asset JSON loader (`AssetModLoader.cs`)
- Would read `mods/<mod>/<powers|buildings>/<name>/*.json|*.png`.
- Nothing in the game calls `AssetModLoader.load()`. It is dead code, so new assets have to be registered from a DLL mod (e.g. `AssetManager.powers.add(...)`).
