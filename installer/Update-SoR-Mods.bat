@echo off
title Streets of Rogue - Mod Updater
REM Keep this file anywhere (Desktop is great) and double-click it whenever
REM you want the newest Streets of Rogue mods. It downloads the latest
REM updater script and runs it - you never need a newer copy of this file.
echo.
echo  Getting the newest Streets of Rogue mods (this can take a minute)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-Expression (Invoke-RestMethod 'https://raw.githubusercontent.com/redaphid/streets-of-rogue-character-creator/main/installer/update-sor-mods.ps1') } catch { Write-Host $_ -ForegroundColor Red; Write-Host ''; Write-Host 'Something went wrong - send a photo of this window to your uncle!' -ForegroundColor Yellow }"
echo.
pause
