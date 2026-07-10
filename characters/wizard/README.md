# Wizard — worked example

The Wizard is a full example of a custom character, written entirely as data
(`character.json` + one icon). It reproduces the original hand-coded WizardMod
without any C#.

- **Glass cannon**: Strength 1, Endurance 1, Accuracy 3, Speed 3. Starts with a Knife.
- **Body**: reuses the Vampire's sprites, tinted purple on the legs (`legsColor`).
- **Chaos Magic** (`ability`): every press picks one of 11 random effects — six
  projectile spells (`bolt`), a `blink` teleport, and four self `buff` "Wild
  Surge" outcomes.
- **Big Quest — "Chaos Ascendant"**: slay 8 foes with Chaos Magic. The panel
  shows live progress via the `{kills}`/`{target}` placeholders.

Copy this folder, rename it, and edit `character.json` to make your own — see
[`../README.md`](../README.md) and [`../../docs/CHARACTER_FORMAT.md`](../../docs/CHARACTER_FORMAT.md).
