# Ability & appearance catalog

Everything you can put in a `character.json` without writing code. Use this to
translate what a kid describes into the right fields.

## Ability effects (the `ability.effects` array)

Each press of the special ability picks **one effect at random** from the array.
For a simple ability, use a single effect. For a "chaos"/"random" ability, add
many. Each effect is one object with a `kind` and a few fields:

### `bolt` — shoot a magic projectile where the player aims
```json
{ "kind": "bolt", "bullet": "Fireball", "shout": "FIREBALL!" }
```
`bullet` must be one of the game's projectile types:

| bullet | feels like |
|---|---|
| `Fireball` | explosive fire |
| `Fire` | flamethrower flame |
| `FreezeRay` | freezes enemies |
| `Taser` | lightning / electrocute |
| `Shrink` | shrinks the target |
| `GhostBlaster` | spooky spirit blast |
| `Tranquilizer` | puts them to sleep |
| `Rocket` | big explosion |
| `Laser` | sci-fi beam |
| `Dart` | poison dart |
| `ZombieSpit` | zombie goo |
| `Water` / `WaterPistol` | shove with water |
| `MindControl` | take control of an enemy |

Kills from `bolt` effects count toward the Big Quest.

### `buff` — give yourself a power-up for a few seconds
```json
{ "kind": "buff", "status": "Giant", "seconds": 12, "shout": "I AM MIGHTY!" }
```
Good `status` values: `Giant` (huge & strong), `Fast` (super speed),
`Shrunk` (tiny & sneaky), `InvisibleLimited` (invisible), `Ghost` (walk through
walls), `Accurate` (perfect aim), `Bloodlust`, `AlwaysCrit`, `BigMelee`,
`ResistDamageMed` (tough), `FeelingGood`.

### `blink` — teleport a short distance
```json
{ "kind": "blink", "near": 3, "far": 8, "shout": "Blink!" }
```

### `heal` — heal yourself (`healAmount` 0 = full heal)
```json
{ "kind": "heal", "healAmount": 0, "shout": "All better!" }
```

### `spawn` — get an item or a random weapon (leave `item` empty for random)
```json
{ "kind": "spawn", "item": "Money", "count": 100, "shout": "Cha-ching!" }
```

## Stats (`stats`, each 1–5, 2 is average)

`strength` (melee power/health), `endurance` (max health), `accuracy` (aim),
`speed` (move speed). Plus small skill mods: `meleeSkill`, `gunSkill`,
`toughness`, `vigilant`. A glass cannon is `strength:1, endurance:1, speed:3`.

## Appearance

- `baseBody`: which existing character's sprites to reuse. Any built-in name
  works. Fun picks: `Vampire`, `Cop`, `Scientist`, `Gorilla`, `Werewolf`,
  `Zombie`, `Wrestler`, `Assassin`, `RobotPlayer`, `Alien`.
- `legsColor` / `bodyColor`: optional `[r, g, b]` tint (0–255) to recolor the
  pants/robe or shirt, e.g. purple robe = `[94, 0, 148]`.

## Slot

`slot` decides where the character shows on the select screen:
- `"auto"` — adds a new slot (or quietly replaces the duplicate `GangbangerB`).
- a built-in name like `"Cop"` — takes over that character's slot.
The installer can also set this per machine, so `"auto"` is a fine default.

## Big Quest (optional)

```json
"bigQuest": {
  "name": "Chaos Ascendant",
  "description": "Slay {target} foes with your power!\nKills: {kills}/{target}",
  "targetKills": 8
}
```
`{kills}` and `{target}` are filled in live on the quest screen. Completing it
gives a big in-run reward (heal, power surge, money, weapons).
