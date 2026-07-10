using System;
using System.Collections.Generic;
using System.Reflection;

namespace CharacterCreator
{
    // Everything a special-ability effect needs. One AbilityContext is built per
    // button press and handed to the chosen effect's Run().
    public class AbilityContext
    {
        public Agent Agent;
        public GameController Gc;
        public EffectDef Fx;       // the effect's JSON fields (kind + its parameters)
        public CharacterDef Def;   // the whole character (name, ability, etc.)

        // Speak a line if one is set. Guarded: headless and remote players have no
        // voice/HUD, and a failed line must never kill the cast.
        public void Say(string line)
        {
            if (!string.IsNullOrEmpty(line)) { try { Agent.Say(line); } catch { } }
        }

        // Emit a named event the character's Big Quest can count (e.g. "clone" when a
        // clone actually happens). Lets a quest track ability outcomes the base game
        // never exposes. No-op if the character has no matching quest.
        public void QuestEvent(string name)
        {
            try { BigQuestPatches.NotifyEvent(Agent, name); } catch { }
        }
    }

    // One kind of ability outcome (bolt, buff, blink, heal, spawn, clone, ...). The
    // built-ins live in this assembly; a character folder can add its own by dropping a
    // class under characters/<id>/src/ that implements this interface. The csproj
    // compiles characters/*/src/**/*.cs into the DLL, and EffectRegistry auto-discovers
    // every implementation at startup - so a new power is a new class in that
    // character's own folder, never a new case in a shared switch.
    public interface IAbilityEffect
    {
        string Kind { get; }             // the "kind" discriminator this handles, e.g. "clone"
        void Run(AbilityContext ctx);
    }

    // Maps each effect "kind" to its handler. Populated once at startup by scanning the
    // assembly. Lookups are case-insensitive; "bolt" is the default when a kind is
    // unknown or missing.
    public static class EffectRegistry
    {
        private static readonly Dictionary<string, IAbilityEffect> byKind =
            new Dictionary<string, IAbilityEffect>(StringComparer.OrdinalIgnoreCase);

        public static void Register(IAbilityEffect effect)
        {
            if (effect == null || string.IsNullOrEmpty(effect.Kind)) return;
            byKind[effect.Kind] = effect;
        }

        // The handler for a kind, falling back to "bolt" if the kind is unknown.
        public static IAbilityEffect Resolve(string kind)
        {
            if (!string.IsNullOrEmpty(kind) && byKind.TryGetValue(kind, out IAbilityEffect e)) return e;
            byKind.TryGetValue("bolt", out IAbilityEffect fallback);
            return fallback;
        }

        // Instantiate and register every IAbilityEffect in the assembly - the built-ins
        // plus any per-character ability.cs compiled in. Each needs a public no-arg ctor.
        public static void RegisterFromAssembly(Assembly asm)
        {
            foreach (Type t in asm.GetTypes())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                if (!typeof(IAbilityEffect).IsAssignableFrom(t)) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                try { Register((IAbilityEffect)Activator.CreateInstance(t)); }
                catch (Exception e) { Plugin.Log.LogWarning("Effect '" + t.Name + "' failed to register: " + e.Message); }
            }
            Plugin.Log.LogInfo("Effect kinds registered: " + string.Join(", ", new List<string>(byKind.Keys).ToArray()));
        }
    }
}
