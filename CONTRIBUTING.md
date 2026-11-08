# Contributing to BoBox

Thanks for helping. Bug reports, ideas and pull requests are all welcome. This guide covers setup,
the rules the code follows, and how to send a change.

## The one rule that matters most

**Never commit WorldBox's own code or assets.** That covers decompiled source, sprites, sounds
and data files. BoBox calls the game's types; it does not copy them. Pull requests that include
game code are closed.

## Set up

1. Install WorldBox (Steam, Windows), the [.NET SDK 8 or newer](https://dotnet.microsoft.com/download),
   and [Git for Windows](https://gitforwindows.org/).
2. Fork the repo on GitHub, then clone your fork:
   ```bash
   git clone https://github.com/<you>/bobox-worldbox-mod.git
   cd bobox-worldbox-mod
   ```
3. If WorldBox is not at `D:\SteamLibrary\steamapps\common\worldbox`, point the build at it:
   ```bash
   export WORLDBOX_DIR="C:/Program Files (x86)/Steam/steamapps/common/worldbox"
   ```
   For a plain `dotnet build`, pass `-p:WorldBoxDir="<path>"` instead.
4. Build, test, and deploy into the game:
   ```bash
   bash scripts/verify.sh
   bash scripts/deploy.sh
   ```
5. In WorldBox, turn on **Experimental mode** in Settings and restart the game.

### Tests without the game

The planner and rules code (`src/BoBox/Planner/`, `src/BoBox/Rules/`) has no game types, so its
tests run anywhere:

```bash
dotnet test tests/BoBox.Tests
```

This is also what CI runs on every pull request, together with
`dotnet format whitespace tests/BoBox.Tests/BoBox.Tests.csproj --verify-no-changes`.

### Reading the game's code locally

To find hook points, decompile the game into `decompiled/`. The folder is gitignored, so the code
stays on your machine:

```bash
dotnet tool install -g ilspycmd
ilspycmd -p -o decompiled -r "<WorldBox>/worldbox_Data/Managed" \
  "<WorldBox>/worldbox_Data/Managed/Assembly-CSharp.dll"
```

[AGENTS.md](AGENTS.md) maps the game types BoBox uses and where it hooks in.

## Code rules

Read [AGENTS.md](AGENTS.md) before a larger change. In short:

- **C# 9 only.** WorldBox runs Unity Mono on .NET Framework 4.7.2. The test project uses the same
  language version, so newer syntax fails there too.
- **Main thread only.** Every call into game or Unity objects runs on the Unity main thread.
  The HTTP listener thread only queues work.
- **Never crash the game.** Wrap each tick and each request in `try/catch`, and log with
  `Log.Info`, `Log.Warn` or `Log.Error` (the `[BoBox]` prefix). One failing city must not stop
  the others.
- **Localhost only.** The API binds `127.0.0.1`. Never `0.0.0.0`, `+` or `*`.
- **No Harmony unless there is no other way.** Prefer swapping steps or assets in the game's
  public libraries. Record any hook in the "Patched methods" table in `AGENTS.md`.
- **Decisions stay pure.** Planner code takes a snapshot in and returns a plan. It never touches
  game or Unity types, and every new planner rule gets a test.
- **Small files.** Keep each file under 300 lines. Split by responsibility.
- **Rules file.** A new numeric field in the rules needs a range in `Rules/RulesLimits.cs`, and
  `rules/bobox-rules.json` must keep mirroring `ColonyRules.Default`. Tests enforce both.

## Send a change

1. Create a branch from `main`: `git checkout -b fix/short-description`.
2. Make the change, with tests for any planner or rules logic.
3. Run `bash scripts/verify.sh`. It must end with 0 errors and every test passing.
4. If the change affects behaviour in the game, deploy it and check it there. The steps in
   [docs/smoke-test.md](docs/smoke-test.md) cover the main paths.
5. Commit with [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`,
   `docs:`, `refactor:`, `test:`, `chore:`, `perf:`, `ci:`.
6. Push and open a pull request against `main`. Say what changed, why, and how you tested it,
   including in-game results where they apply.

## Report a bug

Open an issue with:

- What you did, what you expected, and what happened.
- Your WorldBox version and map size.
- The `[BoBox]` lines from `%USERPROFILE%\AppData\LocalLow\mkarpenko\WorldBox\Player.log`,
  especially the first error.
- The output of `curl -s 127.0.0.1:8765/status` and, for colony problems,
  `curl -s 127.0.0.1:8765/colony/scenarios`.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
