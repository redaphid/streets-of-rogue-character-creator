# `character.json` format

A character is one folder under `characters/`:

```
characters/<id>/
  character.json     # the definition (this file)
  assets/
    ability.png      # 64x64 ability icon (optional but nice)
  README.md          # human description (optional)
```

The mod loads every `characters/*/character.json` it finds installed under
`BepInEx/plugins/Characters/`. The definition is parsed with Unity's
`JsonUtility`, so: only the fields below are read, unknown fields are ignored,
and any missing field falls back to the default shown.

## Top-level fields

| field | type | default | meaning |
|---|---|---|---|
| `id` | string | from `name` | filename-safe id; only letters/digits are kept |
| `name` | string | **required** | shown on the character-select screen; this is the in-game agent name |
| `description` | string | `""` | flavor text under the name |
| `baseBody` | string | `"Vampire"` | existing character whose body sprites are reused |
| `slot` | string | `"auto"` | `"auto"`, a built-in character name to replace, or a slot number |
| `legsColor` | `[r,g,b]` | none | tint the pants/robe (0–255 each) |
| `bodyColor` | `[r,g,b]` | none | tint the shirt/body |
| `stats` | object | see below | the four stats + skill mods |
| `startingItems` | array | none | items placed in inventory at start |
| `ability` | object | none | the special ability (see below) |
| `bigQuest` | object | none | the character's Big Quest |

### `stats`

Each of the four stats is 1–5 (2 is average); skill mods are small integers.

```json
"stats": { "strength": 1, "endurance": 1, "accuracy": 3, "speed": 3,
           "meleeSkill": 0, "gunSkill": 1, "toughness": 0, "vigilant": 1 }
```

### `startingItems`

```json
"startingItems": [ { "name": "Knife", "count": 1 } ]
```
`name` is a game item name (e.g. `Knife`, `Pistol`, `Money`).

### `ability`

```json
"ability": {
  "name": "Chaos Magic",
  "description": "Casts a random spell.",
  "icon": "assets/ability.png",
  "cooldown": 4,
  "effects": [ ... ]
}
```
| field | default | meaning |
|---|---|---|
| `name` | — | ability display name |
| `description` | `""` | shown in tooltips |
| `icon` | none | PNG path relative to the character folder; if missing/absent the game's MindControl icon is reused |
| `cooldown` | `4` | seconds between uses |
| `effects` | none | one is chosen at random on each press |

#### effect objects

Every effect has a `kind`; the other fields depend on it. Unused fields are ignored.

| kind | fields | does |
|---|---|---|
| `bolt` | `bullet`, `shout` | fires a `bulletStatus` projectile where the player aims; its kills count toward the Big Quest |
| `blink` | `near`, `far`, `shout` | teleports the caster to a valid tile `near`–`far` units away |
| `buff` | `status`, `seconds`, `shout` | gives the caster a status effect for `seconds` |
| `heal` | `healAmount`, `shout` | heals the caster (`0` = full heal) |
| `spawn` | `item`, `count`, `shout` | gives `count` of `item` (empty `item` = a random weapon) |
| `clone` | `range`, `shout` | duplicates the nearest world object (furniture: chair, wall, shelf…) within `range` tiles, spawning a copy one tile beside it |

`shout` is an optional line the character says. `clone` never causes kills, so
don't pair it with a `targetKills` Big Quest. Valid `bullet` and `status`
values, and good `baseBody` picks, are listed in
[`.claude/skills/create-character/reference/effects.md`](../.claude/skills/create-character/reference/effects.md).

### `bigQuest`

```json
"bigQuest": {
  "name": "Chaos Ascendant",
  "description": "Slay {target} foes with your power.\nKills: {kills}/{target}",
  "targetKills": 8
}
```
The objective is always "defeat `targetKills` foes with the special ability".
`{kills}` and `{target}` in the description are replaced with live numbers on the
quest screen. Completing it grants a big in-run payoff (full heal, Giant + Fast,
XP, 1000 money + two random weapons, and an instant ability recharge). Counting
is server-authoritative, like the game's own Big Quests.

## Validate

```
scripts/validate-character.py characters/<id>
```
Catches misspelled fields, unknown effect kinds, and bad bullet names before the
game loads the character. (`character.json` is parsed with the game's bundled
Newtonsoft.Json — not Unity's `JsonUtility`, which silently drops nested objects
from a plugin assembly.)

## Custom abilities (code)

The built-in `kind`s cover a lot, but a genuinely new power can bring its own C#.
Drop a class under `characters/<id>/src/` that implements `IAbilityEffect`:

```csharp
using CharacterCreator;
using UnityEngine;

namespace CharacterCreator.Characters.MyGuy   // unique per character
{
    public class ZapAllEffect : IAbilityEffect
    {
        public string Kind => "zapall";        // the "kind" this handles
        public void Run(AbilityContext ctx)    // ctx: Agent, Gc, Fx (EffectDef), Def
        {
            ctx.Say(ctx.Fx.shout);
            // ... use the game APIs (check decompiled/ for signatures) ...
        }
    }
}
```

Then reference it from `character.json` like any other effect:
`{ "kind": "zapall", "shout": "ZAP!" }`. The csproj compiles
`characters/*/src/**/*.cs` into the mod DLL and `EffectRegistry` auto-discovers
every `IAbilityEffect` at startup — no registration call, no edit to shared code.
`cloner/src/CloneEffect.cs` is a worked example (the `clone` kind). Need a new
`EffectDef` field for your parameters? Add a public field to `EffectDef` in
`Model/CharacterDef.cs` (that part is shared). Rebuild with `scripts/dev-install.sh`.

## How the slot works

- `"auto"` — the character is appended as a new select-screen slot. When the
  roster is already full (the Character Pack DLC fills all 32 built-in slots),
  it displaces `GangbangerB`, a palette-swap duplicate that shares another
  character's kit — the least-missed slot.
- a built-in name (e.g. `"Cop"`) — takes over that character's slot exactly.
- a number — overwrites that slot index if it's in range.

The installer can set `slot` per machine, so `"auto"` is a safe default in the repo.
