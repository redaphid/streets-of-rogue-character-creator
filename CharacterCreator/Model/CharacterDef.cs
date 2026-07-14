using System;
using System.Collections.Generic;

namespace CharacterCreator
{
    // The data model for a custom character, deserialized from a folder's
    // character.json with the game's bundled Newtonsoft.Json (NOT Unity's
    // JsonUtility - it silently leaves nested objects from plugin assemblies
    // null; see CharacterLoader). The flat shape is deliberate and kept simple:
    //  - every field is a public field (no properties),
    //  - nested types are [Serializable] classes with public fields,
    //  - there are no dictionaries or polymorphic arrays (EffectDef is one flat
    //    class with a "kind" discriminator instead of a subclass per effect).
    // Field initializers double as defaults: the deserializer constructs the
    // object (running these initializers) and only overwrites keys present in
    // the JSON; unknown JSON keys are ignored.
    [Serializable]
    public class CharacterDef
    {
        public string id;                 // unique, filename-safe, e.g. "wizard"
        public string name;               // shown on the character-select screen
        public string description;         // flavor text under the name
        public string baseBody = "Vampire"; // existing character whose sprites we reuse
        public string slot = "auto";       // "auto" | an agent name to replace | a slot number
        public int[] legsColor;            // optional [r,g,b] tint on the "legs"/robe slot
        public int[] bodyColor;            // optional [r,g,b] tint on the body/shirt slot
        public StatBlock stats = new StatBlock();
        public StartItem[] startingItems;
        public AbilityDef ability;
        public BigQuestDef bigQuest;

        // ---- runtime-only, populated after load; never read from JSON ----
        [NonSerialized] public string dir;            // folder this def came from
        [NonSerialized] public string abilityId;      // internal special-ability token
        [NonSerialized] public string bigQuestUnlock; // "<name>_BQ"

        public bool HasAbility => ability != null && !string.IsNullOrEmpty(ability.name);
        public bool HasBigQuest => bigQuest != null && !string.IsNullOrEmpty(bigQuest.name);
    }

    [Serializable]
    public class StatBlock
    {
        // Streets of Rogue rates each stat 1..5 (2 is average). Skill mods are small ints.
        public int strength = 2;
        public int endurance = 2;
        public int accuracy = 2;
        public int speed = 2;
        public int meleeSkill = 0;
        public int gunSkill = 0;
        public int toughness = 0;
        public int vigilant = 0;
    }

    [Serializable]
    public class StartItem
    {
        public string name;   // game item name, e.g. "Knife"
        public int count = 1;
    }

    [Serializable]
    public class AbilityDef
    {
        public string name;         // display name, e.g. "Chaos Magic"
        public string description;
        public string icon;         // path to a PNG, relative to the character folder
        public int cooldown = 4;    // seconds
        public EffectDef[] effects; // one is picked at random on each press
    }

    // One possible outcome of pressing the ability. "kind" selects which fields matter:
    //   bolt   -> fires bullet (a bulletStatus name) where the player is aiming
    //   blink  -> teleports the caster to a valid tile between near..far units away
    //   buff   -> gives the caster a status effect for `seconds`
    //   heal   -> heals the caster by healAmount (0 = full heal)
    //   spawn  -> gives the caster `count` of item `item` (or a random weapon if item empty)
    //   clone  -> duplicates the furniture the player is aiming at (Hacker-style targeting)
    [Serializable]
    public class EffectDef
    {
        public string kind = "bolt";
        public string bullet;      // bolt: a bulletStatus enum name (Fireball, FreezeRay, Taser, ...)
        public string status;      // buff: a status-effect name (Giant, Fast, Shrunk, ...)
        public float seconds = 10; // buff duration
        public string item;        // spawn: item name; empty => random weapon
        public int count = 1;      // spawn count
        public float near = 3;     // blink min distance
        public float far = 8;      // blink max distance
        public int healAmount = 0; // heal: 0 => full
        public float range = 6;    // clone: how far the player can reach to target furniture
        public string shout;       // optional line the character says
    }

    [Serializable]
    public class BigQuestDef
    {
        public string name;                 // quest title, e.g. "Chaos Ascendant"
        public string description;           // may contain {progress}/{kills} and {target}
        public int targetKills = 8;          // goal count for the default "kills" quest
        public string kind = "kills";        // which BigQuest type tracks it (registry key)
        public int target = 0;               // generic goal count; 0 => fall back to targetKills

        // The goal count a quest checks against - `target` if set, else `targetKills`.
        public int Goal => target > 0 ? target : targetKills;
    }

    // Runtime registry of every loaded character, keyed for the Harmony patches
    // (which are static and must look a character up by the agent's name / ability id).
    public static class CharacterRegistry
    {
        public static readonly List<CharacterDef> All = new List<CharacterDef>();
        private static readonly Dictionary<string, CharacterDef> byName = new Dictionary<string, CharacterDef>();
        private static readonly Dictionary<string, CharacterDef> byAbilityId = new Dictionary<string, CharacterDef>();

        public static void Register(CharacterDef def)
        {
            All.Add(def);
            byName[def.name] = def;
            if (!string.IsNullOrEmpty(def.abilityId)) byAbilityId[def.abilityId] = def;
        }

        public static CharacterDef ByAgentName(string agentName)
        {
            if (agentName == null) return null;
            byName.TryGetValue(agentName, out CharacterDef d);
            return d;
        }

        public static CharacterDef ByAbilityId(string abilityId)
        {
            if (abilityId == null) return null;
            byAbilityId.TryGetValue(abilityId, out CharacterDef d);
            return d;
        }

        // A character's bigQuest string equals its agent name (the game defaults
        // Agent.bigQuest to agentName); this is the lookup the quest patches use.
        public static CharacterDef ByBigQuest(string bigQuest) => ByAgentName(bigQuest);
    }
}
