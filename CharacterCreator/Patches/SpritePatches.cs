using HarmonyLib;

namespace CharacterCreator
{
    // Captures each tk2d sprite collection as the game loads it, so CustomSprite can
    // append its definitions to the right one (and flush any it prepared before the
    // collection existed). This is the RogueLibs collection-capture point
    // (Patches_Sprites: tk2dEditorSpriteDataUnloader.Register) reimplemented
    // standalone.
    [HarmonyPatch]
    public static class SpritePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(tk2dEditorSpriteDataUnloader), nameof(tk2dEditorSpriteDataUnloader.Register))]
        public static void CaptureCollection(tk2dSpriteCollectionData data)
        {
            CustomSprite.RegisterCollection(data);
        }
    }
}
