# characters/

Each subfolder here is one custom character. The mod loads every
`characters/*/character.json` that gets installed into
`BepInEx/plugins/Characters/`.

```
characters/<id>/
  character.json     # the definition — see ../docs/CHARACTER_FORMAT.md
  assets/ability.png # 64x64 ability icon (optional)
  README.md          # description (optional)
```

## Make one

- **Easiest:** ask Claude *"let's make a character"* — the
  [`create-character`](../.claude/skills/create-character/SKILL.md) skill
  interviews you and writes the folder for you.
- **By hand:** copy an example folder, rename it, and edit `character.json`:
  ```sh
  cp -r characters/ninja characters/mydude
  # edit characters/mydude/character.json (change id + name!)
  scripts/make-icon.py characters/mydude/assets/ability.png -l M -c "#3498db"
  scripts/validate-character.py characters/mydude
  ```

## Examples

- [`wizard/`](wizard/) — full-featured, data-only: 11 random Chaos Magic effects + a Big Quest.
- [`cloner/`](cloner/) — code-backed: a custom `clone` effect and a custom Big Quest in `src/`.

Give each character a unique `id` and `name`.
