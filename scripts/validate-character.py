#!/usr/bin/env python3
"""Validate a character.json (or every characters/*/character.json) against the
Character Creator format. Catches the mistakes Unity's JsonUtility would silently
ignore at load time (misspelled fields, unknown effect kinds, bad bullet names).

Usage:
    scripts/validate-character.py                     # all characters/*
    scripts/validate-character.py characters/wizard   # one folder or file
"""
import json
import sys
from pathlib import Path

# Authoritative bulletStatus enum values (from the game's own source) usable as
# projectile "bolt" effects. Non-damaging/utility ones are allowed too.
BULLETS = {
    "None", "Normal", "Shotgun", "Fire", "Fireball", "GhostBlaster", "Rocket",
    "Tranquilizer", "Taser", "Dart", "Shrink", "FreezeRay", "Water", "Water2",
    "WaterPistol", "LeafBlower", "ZombieSpit", "FireExtinguisher", "ResearchGun",
    "Revolver", "MindControl", "Laser",
}

# A curated set of fun, working self-buff status effects. Not exhaustive — the
# game accepts many more — so an unknown one is a warning, not an error.
KNOWN_STATUS = {
    "Giant", "Fast", "Shrunk", "InvisibleLimited", "Ghost", "Accurate",
    "Bloodlust", "FeelingGood", "ResistDamageMed", "Electrocuted", "Dizzy",
    "Acid", "Nicotine", "WerewolfEffect", "AlwaysCrit", "BigMelee", "BigBullets",
}

KINDS = {"bolt", "blink", "buff", "heal", "spawn"}

errors = []
warnings = []


def err(where, msg):
    errors.append(f"{where}: {msg}")


def warn(where, msg):
    warnings.append(f"{where}: {msg}")


def check_int(where, obj, key, lo=None, hi=None):
    if key not in obj:
        return
    v = obj[key]
    if not isinstance(v, int) or isinstance(v, bool):
        err(where, f'"{key}" must be a whole number, got {v!r}')
        return
    if lo is not None and v < lo:
        warn(where, f'"{key}"={v} is below the usual minimum {lo}')
    if hi is not None and v > hi:
        warn(where, f'"{key}"={v} is above the usual maximum {hi}')


def validate(path: Path):
    where = str(path)
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception as e:
        err(where, f"not valid JSON: {e}")
        return

    if not isinstance(data, dict):
        err(where, "top level must be an object")
        return

    if not data.get("name"):
        err(where, '"name" is required (this is what shows on the select screen)')

    stats = data.get("stats", {})
    if isinstance(stats, dict):
        for k in ("strength", "endurance", "accuracy", "speed"):
            check_int(f"{where} stats", stats, k, 1, 5)

    for arr_key in ("legsColor", "bodyColor"):
        c = data.get(arr_key)
        if c is not None:
            if not (isinstance(c, list) and len(c) == 3 and all(isinstance(x, int) for x in c)):
                err(where, f'"{arr_key}" must be [r, g, b] with three 0-255 numbers')

    ab = data.get("ability")
    if ab is not None:
        if not ab.get("name"):
            err(where, 'ability has no "name"')
        icon = ab.get("icon")
        if icon:
            icon_path = path.parent / icon
            if not icon_path.exists():
                err(where, f'ability icon not found: {icon}')
        check_int(f"{where} ability", ab, "cooldown", 0, 60)
        effects = ab.get("effects") or []
        if not effects:
            warn(where, "ability has no effects — pressing it will do nothing")
        for i, fx in enumerate(effects):
            w = f"{where} effect[{i}]"
            kind = (fx.get("kind") or "bolt").lower()
            if kind not in KINDS:
                err(w, f'unknown kind "{kind}" (use one of {sorted(KINDS)})')
            if kind == "bolt":
                b = fx.get("bullet")
                if not b:
                    err(w, 'bolt effect needs a "bullet"')
                elif b not in BULLETS:
                    warn(w, f'bullet "{b}" is not a known bulletStatus ({sorted(BULLETS)})')
            if kind == "buff":
                s = fx.get("status")
                if not s:
                    err(w, 'buff effect needs a "status"')
                elif s not in KNOWN_STATUS:
                    warn(w, f'status "{s}" is not in the curated list (may still work)')

    bq = data.get("bigQuest")
    if bq is not None:
        if not bq.get("name"):
            err(where, 'bigQuest has no "name"')
        check_int(f"{where} bigQuest", bq, "targetKills", 1, 100)
        if ab is None:
            warn(where, "bigQuest is set but the character has no ability to get kills with")


def targets(argv):
    if not argv:
        root = Path(__file__).resolve().parent.parent / "characters"
        return sorted(root.glob("*/character.json"))
    out = []
    for a in argv:
        p = Path(a)
        if p.is_dir():
            out.append(p / "character.json")
        else:
            out.append(p)
    return out


def main():
    files = targets(sys.argv[1:])
    if not files:
        print("No character.json files found.")
        return 1
    for f in files:
        if not f.exists():
            err(str(f), "file does not exist")
            continue
        validate(f)

    for w in warnings:
        print(f"  warn  {w}")
    for e in errors:
        print(f"  ERROR {e}")

    n = len(files)
    if errors:
        print(f"\n{len(errors)} error(s), {len(warnings)} warning(s) across {n} character(s).")
        return 1
    print(f"\nAll {n} character(s) valid. {len(warnings)} warning(s).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
