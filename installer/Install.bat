@echo off
REM Streets of Rogue - Character Creator installer.
REM Double-click this file. It runs the PowerShell installer next to it,
REM bypassing the execution-policy prompt for this one script only.
setlocal
echo Streets of Rogue - Character Creator installer
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-windows.ps1"
echo.
pause
