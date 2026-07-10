---
name: create-character
description: Interview someone (built for kids) about a Streets of Rogue character they want to invent, then generate a ready-to-install characters/<name>/ folder (character.json + ability icon) for the Character Creator mod. Use when the user says things like "let's make a character", "I want to create a new Streets of Rogue guy", "make me a character", or names a character concept to build.
---

# Create a Streets of Rogue character

Your job: turn a kid's imagination into a working custom character. Interview
them warmly and simply, then write out the files. Keep it fun and fast — they
are the designer, you are the helper.

Read `reference/effects.md` (next to this file) first — it is the menu of every
ability, stat, body, and color you can use. Only offer things that exist there.

## 1. Interview (one question at a time, kid-friendly)

Ask these in order. Keep questions short, give 2–3 concrete examples, and accept
whatever they say — map it to real options yourself. Never dump the whole menu.

1. **Name** — "What's your character called?" (→ `name`; make a lowercase,
   letters-only `id` from it, e.g. "Fire Ninja" → `firening` / `fireninja`).
2. **Vibe** — "What are they like? Super strong? Super fast? Sneaky? Tough?"
   (→ `stats`, 1–5 each. Strong→strength/endurance high; fast→speed high;
   sneaky→speed high + maybe a Shrunk/Invisible ability; a "glass cannon" is
   low health, high speed.)
3. **Special power** — "When they press their special button, what happens?"
   This is the fun one. Map it to `ability.effects`:
   - "shoots fire / ice / lightning" → one `bolt` effect.
   - "does something different every time / random / chaos" → many effects.
   - "turns huge / invisible / super fast" → a `buff`.
   - "teleports / blinks" → `blink`.
   - "heals" → `heal`. "makes weapons / money" → `spawn`.
   Give the power a `name` and a short `description`. Ask what they want to
   **shout** when they use it (great for kids) → `shout` on the effects.
4. **Looks** — "Who should they look like? A vampire? A robot? A gorilla?"
   (→ `baseBody` from the body list.) "Favorite color?" (→ `legsColor` /
   `bodyColor` as `[r,g,b]`).
5. **Mission (optional)** — "Want a special mission? Like 'beat 8 bad guys with
   your power'?" (→ `bigQuest` with `targetKills`). If they shrug, skip it.

Confirm the whole thing back in one excited sentence before writing files.

## 2. Generate the files

Create the folder `characters/<id>/` at the repo root with:

- `characters/<id>/character.json` — fill in every field you gathered. Follow
  the schema in `reference/effects.md` and copy the shape of
  `characters/wizard/character.json`. Set `"slot": "auto"` unless they asked to
  replace a specific character. Point `ability.icon` at `"assets/ability.png"`.
- `characters/<id>/assets/ability.png` — generate an icon:
  ```
  scripts/make-icon.py characters/<id>/assets/ability.png -l <FirstLetter> -c "<#hexcolor>"
  ```
  Use their favorite color. (If they'd rather draw their own, tell them to drop
  a 64×64 PNG there instead.)
- `characters/<id>/README.md` — one short paragraph describing the character, in
  the kid's own framing, like `characters/wizard/README.md`.

## 3. Check it

Run the validator and fix anything it flags:
```
scripts/validate-character.py characters/<id>
```
`warn` lines are OK to leave; fix every `ERROR`.

## 4. Hand it off

Tell them it's ready and offer, in plain terms:
- **"Try it in the game now"** → run `scripts/dev-install.sh` (this PC only) or
  point a Windows nephew to the installer.
- **"Make the download to share"** → run `scripts/package.sh`, which builds
  `dist/SoR-CharacterCreator-Windows.zip`. They extract it and double-click
  `Install.bat`. See the top-level `README.md`.

Celebrate the character. Then ask if they want to make another one.
