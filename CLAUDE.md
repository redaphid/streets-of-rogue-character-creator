# Streets of Rogue — Character Creator

A **data-driven** mod for making custom playable Streets of Rogue characters with
no per-character code. One BepInEx plugin reads `character.json` files from a
folder and injects each as a full character (roster slot, unlock, name, stats,
special ability, Big Quest). Aimed at kids: the intended flow is *talk to Claude*
→ generate a character folder → run the installer on Windows.

Approach and Harmony-patch techniques are generalized from the single-character
**WizardMod** at `/home/redaphid/Projects/streets-of-rogue-multiplayer/WizardMod`
(see its `docs/WIZARD.md` for the original per-patch rationale). The decompiled
game source lives at `/home/redaphid/Projects/streets-of-rogue-multiplayer/decompiled`
— read it to confirm any game API before using it.

## Layout

- `CharacterCreator/` — the BepInEx 5 plugin (net472, Harmony). `Plugin.cs` loads
  characters then applies four patch classes: `RosterPatches` (select slot,
  unlocks, names, body-sprite aliasing), `StatsPatches`, `AbilityPatches` (the
  ability item + random-effect engine), `BigQuestPatches` (kill attribution,
  completion payoff, quest panel). `Model/CharacterDef.cs` is the JSON model +
  a runtime `CharacterRegistry` the static patches key off by agent name.
- `characters/<id>/` — each custom character: `character.json` + `assets/ability.png`
  + `README.md`. Examples: `wizard/` (full, 11 random Chaos Magic effects),
  `ninja/` (minimal). Format reference: `docs/CHARACTER_FORMAT.md`.
- `.claude/skills/create-character/` — the interview skill (kid → generated
  folder). `reference/effects.md` is the plain-language menu of valid bullets,
  status effects, bodies.
- `installer/` — `Install.bat` + `install-windows.ps1` (auto-detect game,
  install, pick slot) and `install-linux.sh`.
- `scripts/` — `dev-install.sh` (build + install to local game),
  `package.sh` (build `dist/*.zip`), `validate-character.py`, `make-icon.py`.
- `dist/` — committed prebuilt install zips + DLL (the release workflow attaches
  them; `verify-dist` in CI checks the zip's DLL matches `dist/CharacterCreator.dll`).
- `docs/` — `CHARACTER_FORMAT.md`, `BUILDING.md`, `INSTALL.md`.

## Build / test / run

- Toolchain: `~/.dotnet/dotnet` (.NET 8 SDK). Build **requires a local game
  install** — the mod links the game's copyrighted DLLs, which never leave the
  machine (this is why CI packages prebuilt artifacts instead of building).
- Game install (this PC): `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Streets of Rogue`.
  It's the **Windows depot** run via **Proton**; DLLs are in `StreetsOfRogue_Data/Managed`
  (the csproj auto-detects this vs the Linux `StreetsOfRogueLinux_Data/Managed`).
  BepInEx loads through `winhttp.dll` — no Steam launch options needed under Proton.
- Build: `~/.dotnet/dotnet build -c Release CharacterCreator/CharacterCreator.csproj`.
- Iterate: `scripts/dev-install.sh` (builds, copies DLL to `BepInEx/plugins/`,
  syncs `characters/` → `BepInEx/plugins/Characters/`), then launch from Steam
  (Proton) and check `BepInEx/LogOutput.log` for
  `Character Creator loaded: injected N character(s)`.
- Validate any character before installing: `scripts/validate-character.py characters/<id>`.
  It catches typos/bad fields that Unity's `JsonUtility` silently ignores at load.
- Repackage after changing the DLL or characters: `scripts/package.sh`, then
  commit `dist/`.

Note: `BepInEx/plugins/WizardMod.dll` in the game folder also registers a "Wizard"
and collides with `characters/wizard/`; move it aside when testing the wizard example.

## Conventions

- Mirror the vanilla code paths / verified WizardMod call patterns exactly when
  touching game APIs — check `decompiled/` rather than guessing signatures.
- JSON model is `JsonUtility`-shaped: public fields only, `[Serializable]` nested
  classes, no dictionaries, one flat `EffectDef` with a `kind` discriminator (no
  polymorphic arrays). Field initializers are the defaults.
- All ability/quest HUD and payoff touches are try/catch-guarded (headless and
  remote players have no `buffDisplay`).
- Ship only original code, procedurally-generated icons, and open-source loader
  binaries; reuse game sprites/effects **by name at runtime**, never copied in.
- Debug/exploratory scripts go in `scripts/test/` (no `test_` prefix); keep unit
  tests beside the code they test.
- Commit and push frequently. End commit messages with the Co-Authored-By trailer.
