using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CharacterCreator
{
    // Finds and parses every character.json under the Characters/ folder and
    // registers the results. Called once from the plugin's Awake.
    public static class CharacterLoader
    {
        // Where character folders live. We look next to the DLL first
        // (BepInEx/plugins/Characters), then a couple of sensible fallbacks so a
        // player can drop the folder wherever is easiest.
        public static IEnumerable<string> CharacterRoots(string pluginDir)
        {
            yield return Path.Combine(pluginDir, "Characters");
            // game root (…/Streets of Rogue) is three levels up from BepInEx/plugins
            string gameRoot = Path.GetFullPath(Path.Combine(pluginDir, "..", ".."));
            yield return Path.Combine(gameRoot, "Characters");
        }

        public static void LoadAll(string pluginDir)
        {
            string root = CharacterRoots(pluginDir).FirstOrDefault(Directory.Exists);
            if (root == null)
            {
                Plugin.Log.LogWarning("No Characters/ folder found next to the plugin - no custom characters loaded. " +
                    "Create BepInEx/plugins/Characters/<name>/character.json to add one.");
                return;
            }

            Plugin.Log.LogInfo("Loading characters from " + root);
            foreach (string dir in Directory.GetDirectories(root).OrderBy(d => d))
            {
                string json = Path.Combine(dir, "character.json");
                if (!File.Exists(json)) continue;
                CharacterDef def = Parse(json, dir);
                if (def != null) CharacterRegistry.Register(def);
            }
            Plugin.Log.LogInfo("Loaded " + CharacterRegistry.All.Count + " custom character(s): " +
                string.Join(", ", CharacterRegistry.All.Select(c => c.name).ToArray()));
        }

        private static CharacterDef Parse(string jsonPath, string dir)
        {
            try
            {
                string text = File.ReadAllText(jsonPath, Encoding.UTF8);
                CharacterDef def = JsonUtility.FromJson<CharacterDef>(text);
                if (def == null) { Plugin.Log.LogWarning("Empty/invalid JSON: " + jsonPath); return null; }

                if (string.IsNullOrEmpty(def.name))
                {
                    Plugin.Log.LogWarning("Character in " + dir + " has no \"name\" - skipped.");
                    return null;
                }
                if (string.IsNullOrEmpty(def.id)) def.id = Sanitize(def.name);

                def.dir = dir;
                // The agent name the game uses IS def.name; derive stable internal ids
                // for the ability item and the big-quest unlock from the character id.
                def.abilityId = "CC_" + Sanitize(def.id) + "_Ability";
                def.bigQuestUnlock = def.name + "_BQ";

                if (CharacterRegistry.ByAgentName(def.name) != null)
                {
                    Plugin.Log.LogWarning("Duplicate character name '" + def.name + "' in " + dir + " - skipped.");
                    return null;
                }

                Plugin.Log.LogInfo("  + " + def.name + " (id=" + def.id + ", body=" + def.baseBody +
                    ", slot=" + def.slot + (def.HasAbility ? ", ability=" + def.ability.name : "") + ")");
                return def;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Failed to parse " + jsonPath + ": " + e.Message);
                return null;
            }
        }

        // Absolute path to the ability icon PNG, or null if none / missing.
        public static string AbilityIconPath(CharacterDef def)
        {
            if (def?.ability == null || string.IsNullOrEmpty(def.ability.icon)) return null;
            string p = Path.Combine(def.dir, def.ability.icon);
            return File.Exists(p) ? p : null;
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "Character";
        }
    }
}
