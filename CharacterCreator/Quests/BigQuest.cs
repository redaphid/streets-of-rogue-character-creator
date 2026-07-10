using System;
using System.Collections.Generic;
using System.Reflection;

namespace CharacterCreator
{
    // A character's Big Quest, as code. One instance is created per player Agent
    // running that character. Override the event hooks you care about, expose
    // Progress, and the framework handles completion, the quest panel, and the payoff.
    //
    // This is what makes non-vanilla playstyles possible: a quest can count anything
    // it's told about - ability uses, custom events an effect emits, specific kills -
    // not just the game's built-in pointsTypes. Built-in quest types live in Quests/;
    // a character adds its own by dropping a [BigQuestKind]-tagged subclass under
    // characters/<id>/src/.
    public abstract class BigQuest
    {
        public CharacterDef Def;   // set by the framework before OnStart
        public Agent Agent;        // the player running this quest
        public bool Completed;

        // ---- event hooks (override what you need) ----
        public virtual void OnStart() { }
        // The game scored a big-quest point of `pointsType` for this agent
        // (Neutralize, ServeDrink, Research, ArrestGuilty, SellItem, Destruction, ...).
        public virtual void OnPoints(string pointsType) { }
        // This character used their special ability - `effectKind` is the effect that ran.
        public virtual void OnAbility(string effectKind) { }
        // A named event an effect emitted via ctx.QuestEvent(name) - e.g. "clone".
        public virtual void OnEvent(string name) { }
        // This character killed `victim`; viaAbility = the kill came from their ability.
        public virtual void OnKill(Agent victim, bool viaAbility) { }

        // ---- progress / completion ----
        public abstract int Progress { get; }
        public virtual int Target => Def.bigQuest.Goal;
        public virtual bool IsComplete => Progress >= Target;

        // Quest-panel text. Default fills {progress}/{kills}/{target}/{name} in the
        // character's description; override for fully custom text.
        public virtual string Describe()
        {
            string t = Def.bigQuest.description;
            if (string.IsNullOrEmpty(t)) t = "{name}\n{progress}/{target}";
            return t
                .Replace("{kills}", Progress.ToString())
                .Replace("{progress}", Progress.ToString())
                .Replace("{target}", Target.ToString())
                .Replace("{name}", Def.bigQuest.name ?? "");
        }
    }

    // Marks a BigQuest subclass as the handler for a quest `kind`. Reference it from
    // character.json with "bigQuest": { "kind": "<kind>", ... }.
    [AttributeUsage(AttributeTargets.Class)]
    public class BigQuestKindAttribute : Attribute
    {
        public readonly string Kind;
        public BigQuestKindAttribute(string kind) { Kind = kind; }
    }

    // Maps each quest `kind` to its BigQuest subclass, discovered by scanning the
    // assembly at startup (built-ins plus per-character src/). "kills" is the default.
    public static class BigQuestRegistry
    {
        private static readonly Dictionary<string, Type> byKind =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string kind, Type questType)
        {
            if (!string.IsNullOrEmpty(kind) && questType != null) byKind[kind] = questType;
        }

        // A fresh quest instance for `agent` running `def`, or null if the kind is
        // unknown and there is no "kills" fallback.
        public static BigQuest Create(CharacterDef def, Agent agent)
        {
            string kind = def?.bigQuest?.kind;
            Type type = null;
            if (!string.IsNullOrEmpty(kind)) byKind.TryGetValue(kind, out type);
            if (type == null) byKind.TryGetValue("kills", out type);
            if (type == null) return null;
            try
            {
                BigQuest q = (BigQuest)Activator.CreateInstance(type);
                q.Def = def;
                q.Agent = agent;
                q.OnStart();
                return q;
            }
            catch (Exception e) { Plugin.Log.LogWarning("BigQuest '" + kind + "' create failed: " + e.Message); return null; }
        }

        public static void RegisterFromAssembly(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (t.IsAbstract || !typeof(BigQuest).IsAssignableFrom(t)) continue;
                BigQuestKindAttribute attr = (BigQuestKindAttribute)Attribute.GetCustomAttribute(t, typeof(BigQuestKindAttribute));
                if (attr == null || t.GetConstructor(Type.EmptyTypes) == null) continue;
                Register(attr.Kind, t);
            }
            Plugin.Log.LogInfo("Big-quest kinds registered: " + string.Join(", ", new List<string>(byKind.Keys).ToArray()));
        }
    }
}
