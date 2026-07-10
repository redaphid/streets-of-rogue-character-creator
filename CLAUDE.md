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
  ability item + press/recharge plumbing), `BigQuestPatches` (feeds game events to
  the active quest, completion payoff, quest panel). `Model/CharacterDef.cs` is the
  JSON model + a runtime `CharacterRegistry` the static patches key off by agent
  name. `Abilities/` holds the effect engine: `IAbilityEffect` + `EffectRegistry`
  (`AbilityEffect.cs`) and the built-in kinds (`BuiltinEffects.cs`: bolt, blink,
  buff, heal, spawn). `Quests/` holds the quest engine: the `BigQuest` base +
  `[BigQuestKind]` + `BigQuestRegistry` (`BigQuest.cs`) and the built-in `kills`
  quest (`KillsQuest.cs`). Both are registered by assembly-scan at startup and
  resolved by `kind` — there is no per-kind switch anywhere.
- `characters/<id>/` — each custom character: `character.json` + `assets/ability.png`
  + `README.md`, and **optional `src/*.cs`** for a code-backed ability. A class in
  `src/` implementing `IAbilityEffect` registers a new `kind` just by existing:
  the csproj globs `characters/*/src/**/*.cs` into the DLL and `EffectRegistry`
  auto-discovers it at startup. So a novel power is a class in the character's own
  folder, never an edit to shared code. Example: `cloner/src/CloneEffect.cs` adds
  the `clone` kind. Examples: `wizard/` (full, 11 random Chaos Magic effects),
  `ninja/` (minimal), `cloner/` (custom `clone` effect in `src/`). Format
  reference: `docs/CHARACTER_FORMAT.md`.
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
  It catches typos/bad fields (unknown effect kinds, bad bullet names) before load.
- Repackage after changing the DLL or characters: `scripts/package.sh`, then
  commit `dist/`.
- **A code change only takes effect on a full game restart** — BepInEx loads plugins
  once at process start; there is no hot reload. Launch under Proton with
  `flatpak run com.valvesoftware.Steam steam://rungameid/512900`, but do it **once**:
  repeated `steam://rungameid` calls stack pending launchers and can jam Steam.

Note: `BepInEx/plugins/WizardMod.dll` in the game folder also registers a "Wizard"
and collides with `characters/wizard/`; move it aside when testing the wizard example.

## Conventions

- Mirror the vanilla code paths / verified WizardMod call patterns exactly when
  touching game APIs — check `decompiled/` rather than guessing signatures.
- `character.json` is parsed with the game's bundled **Newtonsoft.Json**, not Unity's
  `JsonUtility`: `JsonUtility` silently leaves nested `[Serializable]` fields (`stats`,
  `ability`, `effects`) at their defaults when the type lives in a plugin assembly,
  which drops every ability. Keep the model Newtonsoft-friendly: public fields, plain
  `[Serializable]` classes, one flat `EffectDef` with a `kind` discriminator (no
  polymorphic arrays). Field initializers are the defaults.
- Custom ability logic goes in `characters/<id>/src/*.cs` as an `IAbilityEffect`,
  and custom mission logic as a `[BigQuestKind]` `BigQuest` subclass in the same
  `src/` (unique namespace per character), **not** in the shared engine. Add a new
  effect/quest `kind` there; register nothing by hand — startup assembly-scan finds
  it. Effects can feed quests via `ctx.QuestEvent("name")` → `BigQuest.OnEvent`.
- All ability/quest HUD and payoff touches are try/catch-guarded (headless and
  remote players have no `buffDisplay`).
- Ship only original code, procedurally-generated icons, and open-source loader
  binaries; reuse game sprites/effects **by name at runtime**, never copied in.
- Debug/exploratory scripts go in `scripts/test/` (no `test_` prefix); keep unit
  tests beside the code they test.
- Commit and push frequently. End commit messages with the Co-Authored-By trailer.
