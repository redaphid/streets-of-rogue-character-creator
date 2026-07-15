#!/usr/bin/env bash
# Launch Streets of Rogue as the custom Wizard and screen-record the engine
# running it (Chaos Magic auto-cast). Proves the Character Creator mod end-to-end
# as an INDEPENDENT plugin (no RogueLibs DLL) using the test harness.
#
# Two harness gotchas handled here:
#   1. The driver auto-picks a default character unless SOR_TEST_CHAR is set — we
#      set SOR_TEST_CHAR=Wizard so it selects/loads the Wizard, and SOR_TEST_CAST=1
#      so the engine auto-casts Chaos Magic on camera.
#   2. make_win_clone does `rm -f plugins/*.dll` on every (re)launch — so we install
#      the mod into a protected plugins/CharacterCreator/ SUBDIR (BepInEx scans
#      recursively; the top-level rm can't reach it), exactly like swamp-content.
#
# Usage: scripts/test/wizard-video.sh [instance] [record-seconds]
set -u

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
SIB="${SOR_REPO:-$HOME/Projects/streets-of-rogue/multiplayer}"
ENV_SH="$SIB/scripts/test/proton_env.sh"
SP="${SP:-/tmp/claude-1000/-home-redaphid-Projects-streets-of-rogue/7cd1f2be-c0fd-478e-8279-45f7638dd053/scratchpad}"
INST="${1:-wizvid}"
SECS="${2:-45}"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"

[ -f "$ENV_SH" ] || { echo "no harness at $ENV_SH — set SOR_REPO" >&2; exit 1; }
# shellcheck source=/dev/null
. "$ENV_SH"

echo "==> Building CharacterCreator (Release)"
"$DOTNET" build -c Release "$REPO/CharacterCreator/CharacterCreator.csproj" >"$SP/wizvid-build.log" 2>&1 \
  || { echo "build failed — see $SP/wizvid-build.log" >&2; tail -5 "$SP/wizvid-build.log"; exit 1; }
DLL="$REPO/CharacterCreator/bin/Release/net472/CharacterCreator.dll"
[ -f "$DLL" ] || { echo "no DLL at $DLL" >&2; exit 1; }

if sor_running_one "$INST"; then kill_sor_one "$INST" 2>/dev/null || kill_clone_force "$INST"; sleep 2; fi
claim_clone "$INST" || { echo "could not claim clone '$INST'" >&2; exit 2; }

echo "==> Materializing clone '$INST'"
make_win_clone "$INST" >/dev/null 2>&1 || { echo "make_win_clone failed" >&2; exit 1; }
B="$(bepinex_dir "$INST")"

echo "==> Deploying mod into PROTECTED subdir $B/plugins/CharacterCreator/"
DEST="$B/plugins/CharacterCreator"
rm -rf "$DEST"; mkdir -p "$DEST/Characters"
cp "$DLL" "$DEST/"
for d in "$REPO"/characters/*/; do
  [ -f "$d/character.json" ] || continue
  n="$(basename "$d")"; mkdir -p "$DEST/Characters/$n"; cp -r "$d"/. "$DEST/Characters/$n/"
done
echo "    characters: $(ls -1 "$DEST/Characters")"

: > "$B/LogOutput.log" 2>/dev/null || true
REPORT="$SP/wizvid-driver.log"; : > "$REPORT"

echo "==> Launching as Wizard (SOR_TEST_CHAR=Wizard, SOR_TEST_CAST=1)"
known="$(game_window_ids | tr '\n' ' ')"
launch_win "$INST" \
  --env=SOR_TEST_MODE=solo \
  --env=SOR_TEST_CHAR=Wizard \
  --env=SOR_TEST_CAST=1 \
  --env=SOR_TEST_ACCEPT_DELAY=6 \
  --env=SOR_TEST_REPORT="$REPORT" \
  -- -screen-fullscreen 0 -window-mode windowed -screen-width 1280 -screen-height 720 -popupwindow \
  >/dev/null 2>&1

printf "==> Waiting for in-game"
ready=0
for _ in $(seq 1 90); do
  grep -aq 'state=in-game' "$B/LogOutput.log" 2>/dev/null && { ready=1; break; }
  printf "."; sleep 2
done
echo
[ "$ready" = 1 ] || { echo "did not reach in-game; see $B/LogOutput.log" >&2; exit 1; }

WID="$(wait_new_window 20 $known || game_window_ids | head -1)"
echo "==> Window id: $WID  — recording ${SECS}s to $SP/wizard-engine.mp4"
RPID="$(start_x11_recording "$WID" "$SP/wizard-engine.mp4" 12)"

# Grab stills through the run while the driver auto-casts Chaos Magic every 5s.
for i in $(seq 1 $((SECS/6))); do
  sleep 6
  ffmpeg -y -f x11grab -window_id "$WID" -i "${DISPLAY:-:0}" -frames:v 1 "$SP/wizard-shot-$i.png" >/dev/null 2>&1 || true
done
stop_x11_recording "$RPID"

echo "==> Done. Artifacts in $SP:"
ls -la "$SP"/wizard-engine.mp4 "$SP"/wizard-shot-*.png 2>/dev/null
echo "==> Driver report tail:"; tail -8 "$REPORT" 2>/dev/null
echo "==> To stop the game: (. \"$ENV_SH\" && kill_sor_one $INST)"
