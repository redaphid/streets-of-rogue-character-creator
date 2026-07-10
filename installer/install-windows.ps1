<#
  Streets of Rogue - Character Creator installer (Windows)

  Double-click Install.bat (which calls this) after extracting the release zip.
  It finds your Streets of Rogue folder, installs the BepInEx mod loader and the
  Character Creator mod, copies in your characters, and lets you choose which
  built-in character slot each one replaces.

  Everything here is a plain file copy into the game folder - nothing is
  installed system-wide, and "Uninstall" below undoes it.
#>
[CmdletBinding()]
param(
    # Skip the game-folder auto-detection and use this path.
    [string]$GameDir = "",
    # Non-interactive slot for every character (e.g. "auto" or "GangbangerB").
    [string]$Slot = ""
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# The 32 built-in characters (Character Pack roster). Any of these can be the
# slot a custom character takes over. "auto" appends / displaces the least-missed
# duplicate (GangbangerB) instead of a character you might want to play.
$BuiltIn = @(
    "Hobo","Soldier","Gangbanger","Thief","Shopkeeper","GangbangerB","Bartender",
    "Hacker","Doctor","Scientist","Gorilla","Cop","Vampire","Wrestler","Assassin",
    "Comedian","Athlete","ShapeShifter","Businessman","Werewolf","Cannibal",
    "Slavemaster","Zombie","Firefighter","Mafia","RobotPlayer","Bouncer","Courier",
    "Alien","Guard","Demolitionist","MechPilot"
)

function Write-Head($t) { Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }

# ---- 1. Find the Streets of Rogue folder --------------------------------------

function Find-GameDir {
    if ($GameDir -and (Test-Path (Join-Path $GameDir "StreetsOfRogue.exe"))) { return $GameDir }

    $candidates = New-Object System.Collections.Generic.List[string]
    # Default Steam library plus any extra libraries listed in libraryfolders.vdf.
    $steamRoots = @(
        "${env:ProgramFiles(x86)}\Steam",
        "$env:ProgramFiles\Steam",
        "C:\Steam"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($steam in $steamRoots) {
        $candidates.Add((Join-Path $steam "steamapps\common\Streets of Rogue"))
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) {
                $lib = $m.Groups[1].Value -replace '\\\\','\'
                $candidates.Add((Join-Path $lib "steamapps\common\Streets of Rogue"))
            }
        }
    }
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "StreetsOfRogue.exe")) { return $c }
    }
    return $null
}

$game = Find-GameDir
if (-not $game) {
    Write-Head "Find your game folder"
    Write-Host "Couldn't auto-detect Streets of Rogue."
    Write-Host "In Steam: right-click Streets of Rogue -> Manage -> Browse local files,"
    Write-Host "then copy the folder path from the address bar and paste it here."
    while (-not $game) {
        $p = Read-Host "Game folder path"
        $p = $p.Trim('"')
        if (Test-Path (Join-Path $p "StreetsOfRogue.exe")) { $game = $p }
        else { Write-Host "  (No StreetsOfRogue.exe in there - try again.)" -ForegroundColor Yellow }
    }
}
Write-Host "Game folder: $game" -ForegroundColor Green

# ---- 2. Choose a slot for each character --------------------------------------

# Characters live next to this script (Characters\<name>\character.json).
$charsSrc = Join-Path $here "Characters"
if (-not (Test-Path $charsSrc)) { throw "No Characters folder found next to the installer ($charsSrc)." }
$charDirs = Get-ChildItem $charsSrc -Directory | Where-Object { Test-Path (Join-Path $_.FullName "character.json") }
if ($charDirs.Count -eq 0) { throw "No characters to install (no Characters\*\character.json)." }

function Choose-Slot($charName) {
    if ($Slot) { return $Slot }
    Write-Head "Slot for '$charName'"
    Write-Host "Where should '$charName' appear on the character-select screen?"
    Write-Host "  [Enter] = auto (adds a new slot, or replaces the duplicate GangbangerB)"
    Write-Host "  or type the name of a built-in character to replace:"
    Write-Host ("  " + ($BuiltIn -join ", ")) -ForegroundColor DarkGray
    while ($true) {
        $ans = (Read-Host "Slot").Trim()
        if (-not $ans) { return "auto" }
        $match = $BuiltIn | Where-Object { $_ -ieq $ans }
        if ($match) { return $match }
        if ($ans -ieq "auto") { return "auto" }
        Write-Host "  Not a built-in character name. Press Enter for auto, or copy one from the list." -ForegroundColor Yellow
    }
}

# Set the "slot" field in a character.json without a JSON library dependency-
# a targeted regex on the top-level "slot": "..." line keeps it PowerShell-only.
function Set-Slot($jsonPath, $slotValue) {
    $text = Get-Content $jsonPath -Raw
    if ($text -match '"slot"\s*:\s*"[^"]*"') {
        $text = [regex]::Replace($text, '"slot"\s*:\s*"[^"]*"', '"slot": "' + $slotValue + '"', 1)
    } else {
        # Insert a slot field right after the opening brace.
        $text = [regex]::Replace($text, '\{', "{`r`n  `"slot`": `"$slotValue`",", 1)
    }
    Set-Content -Path $jsonPath -Value $text -Encoding UTF8
}

# ---- 3. Install the BepInEx payload + mod + characters ------------------------

Write-Head "Installing"

# BepInEx loader files sit next to this script in the zip; merge them in.
foreach ($item in @("BepInEx","winhttp.dll","doorstop_config.ini")) {
    $src = Join-Path $here $item
    if (Test-Path $src) {
        Copy-Item $src -Destination $game -Recurse -Force
        Write-Host "  installed $item"
    }
}

$pluginDir = Join-Path $game "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

$dll = Join-Path $here "CharacterCreator.dll"
if (-not (Test-Path $dll)) { throw "CharacterCreator.dll is missing from the installer folder." }
Copy-Item $dll -Destination $pluginDir -Force
Write-Host "  installed CharacterCreator.dll"

$charsDest = Join-Path $pluginDir "Characters"
New-Item -ItemType Directory -Force -Path $charsDest | Out-Null
foreach ($cd in $charDirs) {
    $name = $cd.Name
    $destDir = Join-Path $charsDest $name
    if (Test-Path $destDir) { Remove-Item $destDir -Recurse -Force }
    Copy-Item $cd.FullName -Destination $charsDest -Recurse -Force

    # Read the display name for the slot prompt.
    $jsonPath = Join-Path $destDir "character.json"
    $disp = $name
    if ((Get-Content $jsonPath -Raw) -match '"name"\s*:\s*"([^"]+)"') { $disp = $Matches[1] }

    $chosen = Choose-Slot $disp
    Set-Slot $jsonPath $chosen
    Write-Host "  installed character '$disp' (slot: $chosen)" -ForegroundColor Green
}

# ---- 4. Done ------------------------------------------------------------------

Write-Head "Done!"
Write-Host "Launch Streets of Rogue from Steam. Your character(s) are on the"
Write-Host "character-select screen, already unlocked."
Write-Host ""
Write-Host "If something's off, open this file and look for 'Character Creator loaded':"
Write-Host "  $game\BepInEx\LogOutput.log"
Write-Host ""
Write-Host "Uninstall: delete $pluginDir\CharacterCreator.dll and the Characters folder"
Write-Host "next to it (or delete the whole BepInEx folder + winhttp.dll to remove all mods)."
