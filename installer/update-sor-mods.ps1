<#
  Streets of Rogue - family mod updater (Windows).

  This is the script Update-SoR-Mods.bat downloads and runs. It is fully
  self-contained (it gets piped through Invoke-Expression, so it must not
  reference $MyInvocation or files next to itself):

    1. finds the Steam "Streets of Rogue" folder,
    2. downloads the LATEST release zip of each mod listed in $Mods,
    3. installs/updates them all (BepInEx loader + plugins + characters),
       preserving any character-slot choices from a previous install.

  Re-running it is always safe - that's how you update.
#>

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# Every mod to install: the /releases/latest/download/ URLs always point at the
# newest published build, so this list only changes when a NEW mod is added.
$Mods = @(
    @{ Name = "EightPlayers (play together online)";
       Url  = "https://github.com/redaphid/streets-of-rogue-multiplayer/releases/latest/download/SoR-EightPlayers-Windows.zip" },
    @{ Name = "Character Creator (custom characters)";
       Url  = "https://github.com/redaphid/streets-of-rogue-character-creator/releases/latest/download/SoR-CharacterCreator-Windows.zip" }
)

function Write-Head($t) { Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }

# ---- 0. The game must be closed (Windows locks loaded DLLs) -------------------

if (Get-Process "StreetsOfRogue" -ErrorAction SilentlyContinue) {
    Write-Host ""
    Write-Host "Streets of Rogue is running! Close the game first, then run me again." -ForegroundColor Yellow
    return
}

# ---- 1. Find the Streets of Rogue folder --------------------------------------

function Find-GameDir {
    $candidates = New-Object System.Collections.Generic.List[string]
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
        $p = (Read-Host "Game folder path").Trim('"')
        if ($p -and (Test-Path (Join-Path $p "StreetsOfRogue.exe"))) { $game = $p }
        else { Write-Host "  (No StreetsOfRogue.exe in there - try again.)" -ForegroundColor Yellow }
    }
}
Write-Host "Game folder: $game" -ForegroundColor Green

# ---- 2. Install one extracted mod payload -------------------------------------

# Zips are game-folder overlays: BepInEx\, winhttp.dll and doorstop_config.ini
# merge into the game folder; loose root DLLs are plugins; a root Characters\
# folder holds Character Creator characters. Installer/readme files are skipped.
function Install-Payload($src, $game) {
    foreach ($item in @("BepInEx", "winhttp.dll", "doorstop_config.ini")) {
        $p = Join-Path $src $item
        if (Test-Path $p) { Copy-Item $p -Destination $game -Recurse -Force }
    }

    $pluginDir = Join-Path $game "BepInEx\plugins"
    New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null

    foreach ($dll in (Get-ChildItem $src -Filter "*.dll" -File)) {
        if ($dll.Name -ieq "winhttp.dll") { continue }
        Copy-Item $dll.FullName -Destination $pluginDir -Force
        Write-Host "  updated $($dll.Name)"
    }

    $charsSrc = Join-Path $src "Characters"
    if (Test-Path $charsSrc) {
        $charsDest = Join-Path $pluginDir "Characters"
        New-Item -ItemType Directory -Force -Path $charsDest | Out-Null
        foreach ($cd in (Get-ChildItem $charsSrc -Directory)) {
            $destDir = Join-Path $charsDest $cd.Name

            # Keep the character-select slot a previous install chose.
            $prevSlot = $null
            $installedJson = Join-Path $destDir "character.json"
            if ((Test-Path $installedJson) -and
                ((Get-Content $installedJson -Raw) -match '"slot"\s*:\s*"([^"]*)"')) {
                $prevSlot = $Matches[1]
            }

            if (Test-Path $destDir) { Remove-Item $destDir -Recurse -Force }
            Copy-Item $cd.FullName -Destination $charsDest -Recurse -Force

            if ($prevSlot -and $prevSlot -ne "auto") {
                $jsonPath = Join-Path $destDir "character.json"
                $text = Get-Content $jsonPath -Raw
                if ($text -match '"slot"\s*:\s*"[^"]*"') {
                    $text = [regex]::Replace($text, '"slot"\s*:\s*"[^"]*"', '"slot": "' + $prevSlot + '"', 1)
                } else {
                    $text = [regex]::Replace($text, '\{', "{`r`n  `"slot`": `"$prevSlot`",", 1)
                }
                Set-Content -Path $jsonPath -Value $text -Encoding UTF8
            }
            Write-Host "  updated character '$($cd.Name)'"
        }
    }
}

# ---- 3. Download and install every mod ----------------------------------------

$tmp = Join-Path $env:TEMP ("sor-mods-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    foreach ($mod in $Mods) {
        Write-Head $mod.Name
        $zip = Join-Path $tmp (Split-Path $mod.Url -Leaf)
        Write-Host "  downloading..."
        Invoke-WebRequest -Uri $mod.Url -OutFile $zip -UseBasicParsing
        $extracted = Join-Path $tmp ([IO.Path]::GetFileNameWithoutExtension($zip))
        Expand-Archive -Path $zip -DestinationPath $extracted -Force
        Install-Payload $extracted $game
    }
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

# ---- 4. Done -------------------------------------------------------------------

Write-Head "All done!"
Write-Host "Start Streets of Rogue from Steam like normal." -ForegroundColor Green
Write-Host ""
Write-Host " - Custom characters (like the Wizard) are on the character-select screen."
Write-Host " - To play together: everyone runs this updater, then everyone presses F9"
Write-Host "   in the game and types the SAME room code."
Write-Host ""
Write-Host "Run this updater again any time to get the newest stuff."
