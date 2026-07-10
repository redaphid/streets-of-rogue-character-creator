Streets of Rogue - Character Creator
====================================

This adds custom playable characters to Streets of Rogue.

WINDOWS - the easy way
----------------------
1. Extract this whole zip somewhere (Desktop is fine).
2. Double-click  Install.bat
3. It finds your game, installs everything, and asks which character slot
   each custom character should take. Press Enter to accept the default.
4. Start the game from Steam. Your character is on the select screen,
   already unlocked.

If Windows warns about the zip being from the internet: right-click the zip
BEFORE extracting -> Properties -> tick "Unblock" -> OK, then extract.

LINUX (Steam/Proton)
--------------------
1. Extract the zip.
2. In a terminal:  ./install-linux.sh
3. In Steam, set the game's Launch Options (Properties -> General) to:
      ./run_bepinex.sh # %command%
4. Launch from Steam.

DID IT WORK?
------------
Open  <game folder>\BepInEx\LogOutput.log  and look for:
      Character Creator loaded: injected N character(s)

UNINSTALL
---------
Delete  BepInEx\plugins\CharacterCreator.dll  and the  Characters  folder
next to it. To remove every mod, delete the whole BepInEx folder plus
winhttp.dll (Windows) or run_bepinex.sh + libdoorstop.so (Linux).

MULTIPLAYER
-----------
Everyone in the game needs the same characters installed. Share this zip.
