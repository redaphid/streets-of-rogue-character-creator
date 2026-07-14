# Building & releasing

## Requirements

- **.NET 8 SDK** (`dotnet`). This repo assumes `~/.dotnet/dotnet`; override with
  the `DOTNET` env var.
- **A local install of Streets of Rogue.** The mod links directly against the
  game's own DLLs (`Assembly-CSharp.dll`, `UnityEngine*.dll`), which are
  copyrighted and exist only on a machine that owns the game. This is why the
  build can't run in CI and why release zips ship prebuilt artifacts.

`CharacterCreator/CharacterCreator.csproj` locates the game via `<GameDir>`. It
defaults to the Flatpak Steam path; override per build:

```sh
~/.dotnet/dotnet build -c Release \
  -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Streets of Rogue"
```

It auto-picks whichever of `StreetsOfRogue_Data/Managed` (Windows depot) or
`StreetsOfRogueLinux_Data/Managed` (Linux depot) actually exists.

## Build

```sh
~/.dotnet/dotnet build -c Release CharacterCreator/CharacterCreator.csproj
# -> CharacterCreator/bin/Release/net472/CharacterCreator.dll
```

## Build + install to the local game (for testing)

```sh
scripts/dev-install.sh
```
Builds the DLL, copies it to `BepInEx/plugins/`, and syncs `characters/` to
`BepInEx/plugins/Characters/`. Then launch the game and check
`BepInEx/LogOutput.log` for `Character Creator loaded: injected N character(s)`.

## Package the release zips

```sh
scripts/package.sh
```
Assembles `dist/SoR-CharacterCreator-{Windows,Linux}.zip` (BepInEx loader payload
+ DLL + `characters/` + installer scripts) and `dist/CharacterCreator.dll`. The
BepInEx loader binaries are sourced from a reference zip (`PAYLOAD_WIN` /
`PAYLOAD_LIN` env vars) rather than kept in git.

Commit the updated `dist/*.zip` and `dist/CharacterCreator.dll` so the release
workflow can attach them.

## Release

`.github/workflows/release.yml` publishes a GitHub Release with the committed
`dist/*.zip` attached on every push to `main`. It does **not** build from source
(see the copyright note above) — it packages what's already in `dist/`.

## How the mod works

`CharacterCreator/` is small and mirrors the vanilla code paths found in the
decompiled game source:

- `Plugin.cs` — loads characters, then applies the Harmony patch classes.
- `Loading/CharacterLoader.cs` — scans folders, parses `character.json`.
- `Model/CharacterDef.cs` — the JSON model + a runtime registry the patches key off.
- `Patches/RosterPatches.cs` — select-screen slot, unlocks, names/descriptions,
  body-sprite aliasing.
- `Patches/StatsPatches.cs` — stats, items, ability, body tint on spawn.
- `Patches/AbilityPatches.cs` — the ability item + the random-effect engine.
- `Patches/BigQuestPatches.cs` — kill attribution, progress, completion payoff,
  quest-panel text.

The techniques are generalized from the single-character WizardMod
(`/home/redaphid/Projects/streets-of-rogue/multiplayer/WizardMod`) — see its
`docs/WIZARD.md` for the original per-patch rationale.
