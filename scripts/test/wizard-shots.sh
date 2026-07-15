#!/usr/bin/env bash
# Clean Wizard proof shots using the game's OWN framebuffer screenshot verb (the
# reliable path under Wayland - host-side x11grab lands on loading frames and the
# in-game verb reads the real render). Launches with the auto-cast OFF so the
# wizard stands still (no self-Shrink/Camouflage), and captures:
#   - the character-SELECT screen (custom bearded wizard portrait, slot 5)
#   - the in-world body, standing, full size
# Copies results to the shared scratchpad as wizard-body-*.png / wizard-ability-*.png.
set -u
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
SIB="${SOR_REPO:-$HOME/Projects/streets-of-rogue/multiplayer}"
ENV_SH="$SIB/scripts/test/proton_env.sh"
SP="${SP:-/tmp/claude-1000/-home-redaphid-Projects-streets-of-rogue/7cd1f2be-c0fd-478e-8279-45f7638dd053/scratchpad}"
INST="${1:-wizvid}"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
. "$ENV_SH"
GAMEDATA="$GAME/StreetsOfRogue_Data"

"$DOTNET" build -c Release "$REPO/CharacterCreator/CharacterCreator.csproj" >"$SP/shots-build.log" 2>&1 \
  || { echo "build failed"; tail -3 "$SP/shots-build.log"; exit 1; }
DLL="$REPO/CharacterCreator/bin/Release/net472/CharacterCreator.dll"

if sor_running_one "$INST"; then kill_sor_one "$INST" 2>/dev/null || kill_clone_force "$INST"; sleep 2; fi
claim_clone "$INST"; make_win_clone "$INST" >/dev/null 2>&1
B="$(bepinex_dir "$INST")"
DEST="$B/plugins/CharacterCreator"; rm -rf "$DEST"; mkdir -p "$DEST/Characters"
cp "$DLL" "$DEST/"
for d in "$REPO"/characters/*/; do [ -f "$d/character.json" ] || continue
  n="$(basename "$d")"; mkdir -p "$DEST/Characters/$n"; cp -r "$d"/. "$DEST/Characters/$n/"; done
: > "$B/LogOutput.log"

cmd(){ : > "$B/ep_out.txt"; printf '%s\n' "$*" > "$B/ep_cmd.txt"
  for _ in $(seq 1 15); do [ -s "$B/ep_out.txt" ] && grep -q '>' "$B/ep_out.txt" && { sleep 0.4; cat "$B/ep_out.txt"; return; }; sleep 1; done; echo TIMEOUT; }
shoot(){ # shoot <gamename> <destname>
  cmd "screenshot $1" >/dev/null; sleep 1.5
  for _ in 1 2 3; do [ -f "$GAMEDATA/$1" ] && { cp "$GAMEDATA/$1" "$SP/$2"; echo "  saved $SP/$2"; return; }; sleep 1; done
  echo "  MISSING $GAMEDATA/$1"; }

echo "==> launch (CAST off, long select delay)"
launch_win "$INST" --env=SOR_TEST_MODE=solo --env=SOR_TEST_CHAR=Wizard --env=SOR_TEST_CAST=0 \
  --env=SOR_TEST_ACCEPT_DELAY=16 --env=SOR_TEST_REPORT="$SP/shots-driver.log" \
  -- -screen-fullscreen 0 -window-mode windowed -screen-width 1280 -screen-height 720 -popupwindow >/dev/null 2>&1

echo "==> wait for character select"
for _ in $(seq 1 60); do grep -aq 'char-select-open' "$B/LogOutput.log" && break; grep -aq 'state=in-game' "$B/LogOutput.log" && break; sleep 2; done
sleep 2; shoot wizsel.png wizard-body-select.png

echo "==> wait for in-game + Wizard"
for _ in $(seq 1 90); do grep -aqE 'agent=Wizard' "$B/LogOutput.log" && break; sleep 2; done
sleep 3
# clear any self-buffs on the player just in case, face down, then shoot
UID=$(cmd state | grep -oE "uid=[0-9]+ 'Wizard'" | grep -oE '[0-9]+' | head -1)
echo "  player uid=$UID"
for s in Shrunk Camouflage CamouflageLimited InvisibleLimited Fast Giant; do cmd "status $UID $s off" >/dev/null; done
sleep 1; shoot wizbody.png wizard-body-ingame.png
cmd state
echo "==> done. artifacts:"; ls -la "$SP"/wizard-body-select.png "$SP"/wizard-body-ingame.png 2>/dev/null
echo "==> stop: (. \"$ENV_SH\" && kill_sor_one $INST)"
