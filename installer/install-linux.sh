#!/usr/bin/env bash
# Streets of Rogue - Character Creator installer (Linux, Steam/Proton or native).
# Run after extracting the release zip:  ./install-linux.sh
#
# Finds the game folder, installs the BepInEx loader + the mod + your characters.
# Set the slot for a character non-interactively with:  SLOT=GangbangerB ./install-linux.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"

BUILTIN="Hobo Soldier Gangbanger Thief Shopkeeper GangbangerB Bartender Hacker Doctor Scientist Gorilla Cop Vampire Wrestler Assassin Comedian Athlete ShapeShifter Businessman Werewolf Cannibal Slavemaster Zombie Firefighter Mafia RobotPlayer Bouncer Courier Alien Guard Demolitionist MechPilot"

find_game() {
  [ -n "${GAMEDIR:-}" ] && { echo "$GAMEDIR"; return; }
  local paths=(
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Streets of Rogue"
    "$HOME/.local/share/Steam/steamapps/common/Streets of Rogue"
    "$HOME/.steam/steam/steamapps/common/Streets of Rogue"
  )
  for p in "${paths[@]}"; do
    if [ -e "$p/StreetsOfRogue.exe" ] || [ -e "$p/StreetsOfRogueLinux.x86_64" ]; then echo "$p"; return; fi
  done
}

GAME="$(find_game || true)"
if [ -z "$GAME" ]; then
  echo "Couldn't auto-detect Streets of Rogue."
  echo "In Steam: right-click the game -> Manage -> Browse local files, copy that path."
  read -r -p "Game folder path: " GAME
fi
[ -e "$GAME/StreetsOfRogue.exe" ] || [ -e "$GAME/StreetsOfRogueLinux.x86_64" ] || {
  echo "No game executable in '$GAME'." >&2; exit 1; }
echo "Game folder: $GAME"

# Copy BepInEx loader payload (Linux: run_bepinex.sh + libdoorstop.so + BepInEx/).
for item in BepInEx run_bepinex.sh libdoorstop.so winhttp.dll doorstop_config.ini; do
  [ -e "$HERE/$item" ] && cp -rf "$HERE/$item" "$GAME/" && echo "  installed $item"
done
[ -f "$GAME/run_bepinex.sh" ] && chmod +x "$GAME/run_bepinex.sh"

PLUGINS="$GAME/BepInEx/plugins"
mkdir -p "$PLUGINS"
cp -f "$HERE/CharacterCreator.dll" "$PLUGINS/" && echo "  installed CharacterCreator.dll"

set_slot() {  # $1 json path, $2 slot value
  local json="$1" slot="$2"
  if grep -qE '"slot"[[:space:]]*:' "$json"; then
    sed -i -E "0,/\"slot\"[[:space:]]*:[[:space:]]*\"[^\"]*\"/s//\"slot\": \"$slot\"/" "$json"
  else
    sed -i -E "0,/\{/s//{\n  \"slot\": \"$slot\",/" "$json"
  fi
}

mkdir -p "$PLUGINS/Characters"
for cd in "$HERE"/Characters/*/; do
  [ -f "$cd/character.json" ] || continue
  name="$(basename "$cd")"
  rm -rf "$PLUGINS/Characters/$name"
  cp -r "$cd" "$PLUGINS/Characters/$name"
  json="$PLUGINS/Characters/$name/character.json"
  disp="$(grep -oE '"name"[[:space:]]*:[[:space:]]*"[^"]+"' "$json" | head -1 | sed -E 's/.*"name"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/')"
  if [ -n "${SLOT:-}" ]; then
    chosen="$SLOT"
  else
    echo
    echo "Where should '$disp' appear? [Enter]=auto, or type a built-in to replace:"
    echo "  $BUILTIN"
    read -r -p "Slot: " chosen
    [ -z "$chosen" ] && chosen="auto"
  fi
  set_slot "$json" "$chosen"
  echo "  installed character '$disp' (slot: $chosen)"
done

echo
echo "Done! On Steam/Proton set Launch Options to:  ./run_bepinex.sh # %command%"
echo "Then launch from Steam. Check BepInEx/LogOutput.log for 'Character Creator loaded'."
