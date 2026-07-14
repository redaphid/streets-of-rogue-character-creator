#!/usr/bin/env bash
# Assemble the distributable install zips into dist/:
#   dist/SoR-CharacterCreator-Windows.zip
#   dist/SoR-CharacterCreator-Linux.zip
#   dist/CharacterCreator.dll         (the plain plugin, for manual installs)
#
# Each zip contains the BepInEx loader payload + CharacterCreator.dll + the
# characters/ folder + the installer scripts, so a player just extracts and runs
# the installer. The BepInEx loader files are copyrighted third-party binaries we
# don't keep in git; this script sources them from a "payload" repo of already-
# packaged zips (defaults to the sibling streets-of-rogue-multiplayer repo, whose
# release zips carry a known-good BepInEx 5.4.23 build for this game).
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
PAYLOAD_WIN="${PAYLOAD_WIN:-$HOME/Projects/streets-of-rogue/multiplayer/dist/SoR-WizardMod-Windows.zip}"
PAYLOAD_LIN="${PAYLOAD_LIN:-$HOME/Projects/streets-of-rogue/multiplayer/dist/SoR-WizardMod-Linux.zip}"

DIST="$REPO/dist"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$DIST"

echo "==> Building CharacterCreator (Release)"
"$DOTNET" build -c Release "$REPO/CharacterCreator/CharacterCreator.csproj" >/dev/null
DLL="$REPO/CharacterCreator/bin/Release/net472/CharacterCreator.dll"
[ -f "$DLL" ] || { echo "no DLL built" >&2; exit 1; }
cp "$DLL" "$DIST/CharacterCreator.dll"

# Common content added to every platform's staging folder.
stage_common() {
  local dst="$1"
  cp "$DLL" "$dst/CharacterCreator.dll"
  cp "$REPO/installer/INSTALL-README.txt" "$dst/"
  mkdir -p "$dst/Characters"
  for d in "$REPO"/characters/*/; do
    [ -f "$d/character.json" ] || continue
    cp -r "$d" "$dst/Characters/$(basename "$d")"
  done
}

# Pull the BepInEx loader payload out of a reference zip, dropping its plugin.
extract_loader() {
  local zip="$1" dst="$2"
  [ -f "$zip" ] || { echo "payload zip not found: $zip" >&2; exit 1; }
  unzip -q "$zip" -d "$dst"
  rm -rf "$dst/BepInEx/plugins"       # drop the reference mod's plugin(s)
  rm -f  "$dst/INSTALL-README.txt"    # replaced by ours
  mkdir -p "$dst/BepInEx/plugins"
}

# `zip -r` adds to an existing archive rather than replacing it, so a removed
# character would linger in the committed zip. Delete the targets first for a
# deterministic build.
rm -f "$DIST/SoR-CharacterCreator-Windows.zip" "$DIST/SoR-CharacterCreator-Linux.zip"

echo "==> Packaging Windows zip"
WIN="$WORK/win"; mkdir -p "$WIN"
extract_loader "$PAYLOAD_WIN" "$WIN"
stage_common "$WIN"
cp "$REPO/installer/install-windows.ps1" "$WIN/"
cp "$REPO/installer/Install.bat" "$WIN/"
( cd "$WIN" && zip -qr "$DIST/SoR-CharacterCreator-Windows.zip" . )

echo "==> Packaging Linux zip"
LIN="$WORK/lin"; mkdir -p "$LIN"
extract_loader "$PAYLOAD_LIN" "$LIN"
stage_common "$LIN"
cp "$REPO/installer/install-linux.sh" "$LIN/"
chmod +x "$LIN/install-linux.sh"
( cd "$LIN" && zip -qr "$DIST/SoR-CharacterCreator-Linux.zip" . )

echo "==> Done:"
ls -la "$DIST"
