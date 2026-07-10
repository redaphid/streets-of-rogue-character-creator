using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CharacterCreator
{
    // Data-driven Streets of Rogue character loader. Instead of hard-coding one
    // character in C# (as WizardMod did), this scans a Characters/ folder for
    // character.json definitions and injects each one as a playable character
    // using the same Harmony-patch techniques, generalized to a registry.
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.hypnodroid.charactercreator";
        public const string NAME = "Character Creator";
        public const string VERSION = "1.0.0";

        public static ManualLogSource Log;

        public void Awake()
        {
            Log = Logger;

            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            CharacterLoader.LoadAll(pluginDir);

            if (CharacterRegistry.All.Count == 0)
            {
                Log.LogInfo("Character Creator loaded, but no characters to inject.");
                return;
            }

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(RosterPatches));
            harmony.PatchAll(typeof(StatsPatches));
            harmony.PatchAll(typeof(AbilityPatches));
            harmony.PatchAll(typeof(BigQuestPatches));

            Log.LogInfo("Character Creator loaded: injected " + CharacterRegistry.All.Count + " character(s).");
        }
    }
}
