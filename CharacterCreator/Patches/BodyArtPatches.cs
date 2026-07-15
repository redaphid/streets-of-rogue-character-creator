using System.IO;
using HarmonyLib;

namespace CharacterCreator
{
    // Gives a custom character its OWN body art instead of aliasing an existing
    // character's sprites. When a character ships assets/body/<Name>S.png, we inject
    // it through the tk2d Bodies path (CustomSprite): it writes gr.bodyDic["<Name>S"]
    // - the sprite the character-select portrait Image reads (CharacterSelect line
    // 1502) - AND appends a real tk2dSpriteDefinition named "<Name>S" to the "Bodies"
    // collection, which is what the in-world agent body resolves by name. Writing the
    // Bodies dictionary is exactly what RogueLibs left commented out, so this is the
    // gap the independent approach closes.
    //
    // Characters WITHOUT custom body art keep the existing baseBody reskin (RosterPatches
    // aliasing), so nothing regresses.
    [HarmonyPatch]
    public static class BodyArtPatches
    {
        private static readonly System.Collections.Generic.HashSet<string> injected =
            new System.Collections.Generic.HashSet<string>();

        public static bool HasCustomBody(CharacterDef def) => CharacterLoader.BodyPortraitPath(def) != null;

        // Sprite key the game uses for a character's front/portrait sprite.
        public static string PortraitKey(CharacterDef def) => def.name + "S";

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameResources), nameof(GameResources.SetupDics))]
        public static void InjectAll(GameResources __instance)
        {
            foreach (CharacterDef def in CharacterRegistry.All)
            {
                Inject(def);
                InjectHeadPiece(def);
            }
        }

        // A custom hat: the directional sprites live in assets/headpiece/<HatName><Dir>.png
        // (e.g. WizardHatS, WizardHatSE, ...). Injected into the tk2d HeadPieces
        // collection so the game's head/hat layer renders "<HatName>"+playerDir on the
        // head every frame (StatsPatches sets it as the character's defaultArmorHead).
        public static void InjectHeadPiece(CharacterDef def)
        {
            if (string.IsNullOrEmpty(def.headPiece)) return;
            string dir = Path.Combine(def.dir, "assets", "headpiece");
            if (!Directory.Exists(dir)) return;
            foreach (string path in Directory.GetFiles(dir, def.headPiece + "*.png"))
            {
                string key = Path.GetFileNameWithoutExtension(path); // "WizardHatS" etc.
                if (injected.Contains("hp:" + key)) { CustomSprite.Redefine(key); continue; }
                try
                {
                    CustomSprite.Create(key, SpriteScope.HeadPieces, File.ReadAllBytes(path));
                    injected.Add("hp:" + key);
                    Plugin.Log.LogInfo("Injected head-piece sprite '" + key + "' for '" + def.name + "'.");
                }
                catch (System.Exception e) { Plugin.Log.LogWarning("head-piece '" + key + "' failed: " + e.Message); }
            }
        }

        public static void Inject(CharacterDef def)
        {
            string portrait = CharacterLoader.BodyPortraitPath(def);
            if (portrait == null) return;
            string key = PortraitKey(def);

            if (injected.Contains(key))
            {
                CustomSprite.Redefine(key); // retry GameResources/tk2d writes if a rebuild dropped them
                return;
            }
            try
            {
                byte[] png = File.ReadAllBytes(portrait);
                CustomSprite.Create(key, SpriteScope.Bodies, png);
                injected.Add(key);
                Plugin.Log.LogInfo("Injected custom body portrait '" + key + "' for '" + def.name + "'.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Failed to inject body portrait for '" + def.name + "': " + e.Message);
            }
        }
    }
}
