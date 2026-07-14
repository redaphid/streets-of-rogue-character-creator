# Streets of Rogue — Character Creator

Make your own playable **Streets of Rogue** characters — no coding. You describe
the character (out loud, to Claude, or by editing one small file), and this turns
it into a real character you can pick on the character-select screen: custom
stats, a custom special ability, its own body and colors, and a Big Quest.

Built for kids: the easiest path is to just **talk to Claude** and answer a few
questions. Under the hood it's one BepInEx mod that reads simple `character.json`
files, so there's no new code to compile per character.

## The 3-minute path (recommended)

1. In this folder, ask Claude: **"Let's make a character!"**
   Claude runs the [`create-character`](.claude/skills/create-character/SKILL.md)
   skill — it interviews you (name, powers, looks, a mission) and writes a new
   folder under [`characters/`](characters/) with an ability icon, ready to go.
2. Make the shareable download:
   ```
   scripts/package.sh
   ```
   That produces `dist/SoR-CharacterCreator-Windows.zip`.
3. On the Windows PC: extract the zip, double-click **`Install.bat`**, pick which
   character slot to use, and launch the game. Your character is there, unlocked.

## What a character can have

- **Stats** — strength, endurance, accuracy, speed (1–5), plus skill mods.
- **A special ability** — press the special button to fire a spell, buff
  yourself, blink, heal, or spawn an item. List several effects and it picks one
  at random each press (that's how the example Wizard's "Chaos Magic" works).
- **A look** — reuse any existing character's body sprites, tinted your colors.
- **A Big Quest** — "defeat N foes with your power" for an in-run reward.

Full menu of abilities/bodies/colors: [`docs/CHARACTER_FORMAT.md`](docs/CHARACTER_FORMAT.md).

## Examples

- [`characters/wizard/`](characters/wizard/) — the full Wizard (11 random Chaos
  Magic spells + a Big Quest), rebuilt as pure data.
- [`characters/ninja/`](characters/ninja/) — a simpler Fire Ninja (fireball /
  invisible / blink).

Copy either folder, rename it, and edit `character.json` to make your own.

## The one-file updater (easiest way to share with family)

Send people **one file** they keep on their Desktop:
[`Update-SoR-Mods.bat`](https://github.com/redaphid/streets-of-rogue-character-creator/releases/latest/download/Update-SoR-Mods.bat).
Double-clicking it always downloads and installs the **latest** release of every
mod (EightPlayers co-op + Character Creator + all characters) into their Steam
game — and re-running it is how they update. No zips, no versions, no re-sending
files. The bat just fetches and runs
[`installer/update-sor-mods.ps1`](installer/update-sor-mods.ps1) from this
repo's `main`, so improving the install logic here reaches everyone
automatically.

Windows will show a "protect your PC" SmartScreen warning the first time —
click **More info → Run anyway** (the script is plain text in this repo;
nothing is installed outside the game folder).

## Installing (details)

The installer bundles the [BepInEx](https://github.com/BepInEx/BepInEx) mod
loader, the Character Creator mod, and your characters. Extract the release zip,
then:

- **Windows** — double-click `Install.bat`. It finds Steam's *Streets of Rogue*
  folder, installs everything, and asks which built-in character slot each of
  your characters should take (press Enter for "auto").
- **Linux (Steam/Proton)** — run `./install-linux.sh`, then set the game's Steam
  Launch Options to `./run_bepinex.sh # %command%`.

See [`installer/INSTALL-README.txt`](installer/INSTALL-README.txt) and
[`docs/INSTALL.md`](docs/INSTALL.md). **Everyone playing together needs the same
characters installed.**

## For developers

- The mod: [`CharacterCreator/`](CharacterCreator/) — a BepInEx 5 plugin
  (net472, Harmony patches) that scans `BepInEx/plugins/Characters/*/character.json`
  and injects each character. See [`docs/BUILDING.md`](docs/BUILDING.md).
- Build + install to the local game for testing: `scripts/dev-install.sh`.
- Validate a character definition: `scripts/validate-character.py`.
- Building requires the .NET 8 SDK **and a local install of the game** (the mod
  links against the game's own DLLs, which are copyrighted and never
  redistributed — this is why CI packages prebuilt artifacts instead of building
  from source).

## Credits & assets

The mod ships only original code, procedurally-generated icons, and third-party
open-source loader binaries (BepInEx / HarmonyX) under their own licenses. It
reuses the game's existing sprites/effects **by name at runtime** — no game
assets are copied into this repo. Approach and patch techniques are generalized
from the hand-written WizardMod.
