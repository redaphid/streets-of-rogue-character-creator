# Installing on another PC (the nephews' machines)

The short version lives in [`installer/INSTALL-README.txt`](../installer/INSTALL-README.txt)
(it ships inside the zip). This is the same thing with a bit more detail.

## Windows

1. Get the release zip `SoR-CharacterCreator-Windows.zip` (from a GitHub Release,
   or make one with `scripts/package.sh`).
2. If Windows flags it as downloaded: right-click the zip → **Properties** →
   tick **Unblock** → OK. (Do this before extracting.)
3. **Extract the whole zip** somewhere (Desktop is fine).
4. Double-click **`Install.bat`**. It will:
   - find your Steam *Streets of Rogue* folder automatically (or ask you to paste
     the path — Steam → right-click the game → Manage → Browse local files);
   - install the BepInEx loader + the mod + your characters;
   - ask, for each custom character, **which slot** it should take on the
     character-select screen. Press **Enter** for "auto", or type a built-in
     character's name (e.g. `GangbangerB`, `Cop`) to replace that one.
5. Launch the game from Steam. Your character is on the select screen, unlocked.

No Steam launch options are needed on Windows — BepInEx loads through
`winhttp.dll`.

## Linux (Steam / Proton)

1. Extract `SoR-CharacterCreator-Linux.zip`.
2. Run `./install-linux.sh` (set `SLOT=Cop` to choose a slot non-interactively).
3. In Steam → the game → Properties → General → **Launch Options**, set:
   ```
   ./run_bepinex.sh # %command%
   ```
   The trailing `#` matters — it comments out the rest of Steam's launch chain,
   which the loader can't parse. (Trade-off: no Steam overlay.)
4. Launch from Steam.

## Did it work?

Open `<game folder>/BepInEx/LogOutput.log` and look for:

```
Character Creator loaded: injected N character(s)
```

If a character is missing, the same log lists each one it loaded and any parse
errors.

## Playing together

**Every player needs the same characters installed** — host and clients. Share
the same zip. Effects go through the game's own synced mechanisms, so nothing
custom crosses the wire; a peer without the mod would see a stats-less agent with
a broken body (the game logs an error and continues — it does not crash).

Want more than 4 players? The independent `EightPlayers` mod drops into the same
`BepInEx/plugins/` folder alongside this one.

## Uninstall

Delete `BepInEx/plugins/CharacterCreator.dll` and the `Characters` folder next to
it. To remove every mod, delete the whole `BepInEx` folder plus `winhttp.dll`
(Windows) or `run_bepinex.sh` + `libdoorstop.so` (Linux).
