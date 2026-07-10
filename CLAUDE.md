# Mod generator for streets of rogue custom characters.

Generate a project that uses a simmple, conventional folder structure for making custom characters for Streets of rogue, and code for the mod that uses that folder to load up new characters.
/
Something like:
 ```
 ./characters
    character_name/
      ./assets/
      ./big_quest/
      ./code
```

Generate any Claude skills that will allow us to quickly make a character.
Look in `/home/redaphid/Projects/streets-of-rogue-multiplayer/WizardMod`
That folder, and it's parent folder, was used to generate a Wizard character for Streets of Rogue
The skills and docs to make it very easy for my nephews to describe to claude the character they want, and Claude interviews them on the name, what the special ability does, etc until we have a good one, then generate the character in `./characters/<name>/` with an installer for windows. Give them options re: which character slot to replace. Read from the WizardMod to learn what I mean about this stuff. Look at that mod and parent mods to learn how to run the game and test etc. This will be done on Windows computers for my newphews.
