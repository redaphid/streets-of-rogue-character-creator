# Release artifact format (for maintainers + CI)

The family updater (`update-sor-mods.ps1`) installs every mod in its `$Mods`
list by downloading one zip per mod from that repo's **latest GitHub Release**
and merging it into the Streets of Rogue game folder. This file defines the zip
so a new mod "just works" in the updater with no updater code change.

## The zip is a game-folder overlay

Each release zip mirrors the layout of the game folder. The updater copies the
whole `BepInEx\` tree recursively and (if present) a root `Characters\` folder,
so anything placed at these paths lands correctly:

```
SoR-<Mod>-Windows.zip
├── BepInEx/
│   ├── core/            # loader (only the Character Creator / EightPlayers zips ship this)
│   ├── patchers/
│   │   └── RogueLibsPatcher.dll     # preloader, if the mod uses RogueLibs
│   └── plugins/
│       └── <Mod>/                    # a SUBDIR, never loose in plugins/
│           ├── RogueLibsCore.dll     # bundle it here if the mod needs RogueLibs
│           ├── <Mod>.dll
│           └── assets/               # objects/*.png, item-*.png, tiles/… preserved
├── winhttp.dll          # loader shim (loader-carrying zips only)
├── doorstop_config.ini  # (loader-carrying zips only)
└── Characters/          # Character Creator only: <name>/character.json + assets/
    └── <name>/
```

Why the `plugins/<Mod>/` subdir: BepInEx scans `plugins/` recursively, and the
game clone-refresh wipes only loose top-level `plugins/*.dll`, so a subdir keeps
a mod's DLLs and its `assets/` intact across relaunches. This mirrors
`swamp-content/deploy.sh` exactly. Each RogueLibs-based mod bundles its own
`RogueLibsCore.dll` + `RogueLibsPatcher.dll`, so there is no separate RogueLibs
download and installing two such mods just overwrites identical copies.

Only ONE mod needs to carry the BepInEx loader (`core/`, `winhttp.dll`,
`doorstop_config.ini`); the updater installs mods in list order and later zips
merge over it harmlessly.

## Expected release assets, per repo

| Repo | Windows asset | Linux asset |
|---|---|---|
| `redaphid/streets-of-rogue-multiplayer` | `SoR-EightPlayers-Windows.zip` | `SoR-EightPlayers-Linux.zip` |
| `redaphid/streets-of-rogue-character-creator` | `SoR-CharacterCreator-Windows.zip` | `SoR-CharacterCreator-Linux.zip` |
| `streets-of-rogue-montzters/swamp-content` | `SoR-SwampContent-Windows.zip` | `SoR-SwampContent-Linux.zip` |
| `streets-of-rogue-montzters/swamp-biome` | `SoR-SwampBiome-Windows.zip` | `SoR-SwampBiome-Linux.zip` |
| `redaphid/wizard-content` | `SoR-WizardContent-Windows.zip` | `SoR-WizardContent-Linux.zip` |

The updater points at `/releases/latest/download/<asset>`, which always resolves
to the newest published build. A mod marked `Optional = $true` in `$Mods` is
skipped (not an error) until its first release exists — so adding a repo to the
list before its CI is live does not break anyone's updater.

## What each mod's CI must publish

CI can NOT build these mods: they link the game's copyrighted DLLs, which exist
only on a machine that owns the game. So the model is the one Character Creator
already uses:

1. A maintainer runs the repo's `scripts/package.sh` locally (it builds the mod
   against the local game install and writes `dist/SoR-<Mod>-Windows.zip` +
   `-Linux.zip` in the overlay layout above), then commits `dist/`.
2. `.github/workflows/release.yml` (on push to `main`) verifies the committed
   zip matches the committed DLL and attaches the zips to a new GitHub Release.

`swamp-content` and `swamp-biome` ship a ready-to-run `scripts/package.sh` +
`.github/workflows/release.yml` following this pattern; a maintainer only needs
to run `package.sh` once, commit `dist/`, and push.
