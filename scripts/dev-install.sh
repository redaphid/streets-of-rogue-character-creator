#!/usr/bin/env bash
# Build the CharacterCreator mod and install it (plus the characters/ folder)
# into the local Streets of Rogue install, for development/testing on this PC.
#
# Usage: scripts/dev-install.sh
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
GAMEDIR="${GAMEDIR:-$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Streets of Rogue}"
PLUGINS="$GAMEDIR/BepInEx/plugins"

echo "==> Building CharacterCreator (Release)"
"$DOTNET" build -c Release "$REPO/CharacterCreator/CharacterCreator.csproj"

DLL="$REPO/CharacterCreator/bin/Release/net472/CharacterCreator.dll"
[ -f "$DLL" ] || { echo "build produced no DLL at $DLL" >&2; exit 1; }
[ -d "$PLUGINS" ] || { echo "no BepInEx plugins dir at $PLUGINS" >&2; exit 1; }

echo "==> Installing CharacterCreator.dll -> $PLUGINS"
cp "$DLL" "$PLUGINS/"

echo "==> Syncing characters/ -> $PLUGINS/Characters"
rm -rf "$PLUGINS/Characters"
mkdir -p "$PLUGINS/Characters"
for d in "$REPO"/characters/*/; do
  [ -f "$d/character.json" ] || continue
  name="$(basename "$d")"
  mkdir -p "$PLUGINS/Characters/$name"
  cp -r "$d"/. "$PLUGINS/Characters/$name/"
done

echo "==> Done. Installed characters:"
ls -1 "$PLUGINS/Characters"
