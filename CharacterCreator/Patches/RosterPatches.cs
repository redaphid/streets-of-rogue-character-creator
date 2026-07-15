using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CharacterCreator
{
    // Puts each custom character on the character-select screen, registers its
    // unlocks, supplies its name/description/quest strings, and points its body
    // sprites at an existing character's art. Generalized from WizardMod's
    // WizardCharacter, driven by the registry instead of one hard-coded agent.
    [HarmonyPatch]
    public static class RosterPatches
    {
        // Slots 0-31 are the built-in character page; 32+ are treated as custom
        // ("Create Character") slots. With the Character Pack DLC the base roster
        // fills all 32, so an appended character lands in slot 32 and behaves like
        // an empty custom slot. When full we displace a named agent instead.
        private const int BuiltInSlots = 32;
        private const string DefaultDisplace = "GangbangerB"; // a palette-swap duplicate, least-missed

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterSelect), nameof(CharacterSelect.RealAwake))]
        public static void AddToRoster(CharacterSelect __instance)
        {
            foreach (CharacterDef def in CharacterRegistry.All)
                AddOne(__instance, def);

            AliasPortraitSprites();
            Plugin.Log.LogInfo("Custom characters added to roster (roster size " +
                __instance.slotAgentTypes.Count + ")");
        }

        private static void AddOne(CharacterSelect cs, CharacterDef def)
        {
            if (!cs.slotAgentTypes.Contains(def.name))
            {
                int idx = ResolveSlot(cs, def);
                if (idx >= 0 && idx < cs.slotAgentTypes.Count)
                {
                    Plugin.Log.LogInfo("Placing '" + def.name + "' into slot " + idx +
                        " (replacing '" + cs.slotAgentTypes[idx] + "')");
                    cs.slotAgentTypes[idx] = def.name;
                    // The select screen sets each slot up once and caches it
                    // (setSlotAgent[n]); if it already rendered this slot under its old
                    // type, the name/portrait stay stale. Clear the cache so it re-renders
                    // as our character.
                    if (cs.setSlotAgent != null && idx < cs.setSlotAgent.Length)
                        cs.setSlotAgent[idx] = false;
                }
                else
                {
                    cs.slotAgentTypes.Add(def.name);
                }
            }
            if (!cs.slotAgentTypesComplete.Contains(def.name))
                cs.slotAgentTypesComplete.Add(def.name);
        }

        // Returns the slot index to overwrite, or -1 to append.
        private static int ResolveSlot(CharacterSelect cs, CharacterDef def)
        {
            string slot = string.IsNullOrEmpty(def.slot) ? "auto" : def.slot.Trim();

            if (int.TryParse(slot, out int num))
                return (num >= 0 && num < cs.slotAgentTypes.Count) ? num : -1;

            if (slot != "auto")
            {
                int named = cs.slotAgentTypes.IndexOf(slot);
                if (named >= 0) return named; // replace the requested character
                // requested character not present; fall through to auto behavior
            }

            // auto: append while there is room; once the built-in roster is full,
            // displace the throwaway duplicate, then walk from the end for any slot not
            // already taken by one of our own characters. Walking past our own slots is
            // what keeps two "auto" characters from colliding on the same tail slot
            // (which left slots showing the wrong name).
            if (cs.slotAgentTypes.Count < BuiltInSlots) return -1;
            int drop = cs.slotAgentTypes.IndexOf(DefaultDisplace);
            if (drop >= 0) return drop;
            for (int i = cs.slotAgentTypes.Count - 1; i >= 0; i--)
                if (CharacterRegistry.ByAgentName(cs.slotAgentTypes[i]) == null)
                    return i; // a vanilla slot we haven't claimed yet
            return cs.slotAgentTypes.Count - 1;
        }

        // The select screen looks up portrait sprites by "<agentName>S"; alias each
        // custom character to its base body so the lookup succeeds.
        private static void AliasPortraitSprites()
        {
            GameResources gr = GameController.gameController?.gameResources;
            if (gr == null) return;
            foreach (CharacterDef def in CharacterRegistry.All)
            {
                // A character with its own body art injects its real "<Name>S" sprite
                // (BodyArtPatches); don't overwrite it with the baseBody alias.
                if (BodyArtPatches.HasCustomBody(def)) continue;
                string src = def.baseBody + "S";
                string dst = def.name + "S";
                if (gr.bodyDic != null && gr.bodyDic.ContainsKey(src) && !gr.bodyDic.ContainsKey(dst))
                    gr.bodyDic[dst] = gr.bodyDic[src];
                if (gr.bodyGDic != null && gr.bodyGDic.ContainsKey(src) && !gr.bodyGDic.ContainsKey(dst))
                    gr.bodyGDic[dst] = gr.bodyGDic[src];
            }
        }

        // The aliased portrait is the base body's raw sprite, so on the select
        // screen a custom character was indistinguishable from its base. The game
        // already solves this for its own in-game "Custom" characters by setting
        // the Body Image's color (SetupSlotAgent tints Custom, then resets every
        // other agent to Color.white) - so after it runs, re-tint our slots the
        // same way with the character's configured color.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CharacterSelect), nameof(CharacterSelect.SetupSlotAgent))]
        public static void TintPortrait(CharacterSelect __instance, int n, string mySlotAgentType)
        {
            CharacterDef def = CharacterRegistry.ByAgentName(mySlotAgentType);
            if (def == null) return;
            // Custom body art already carries its own colours; a portrait tint would
            // recolour it (e.g. wash the wizard purple), so leave it untinted.
            if (BodyArtPatches.HasCustomBody(def)) return;
            int[] rgb = (def.bodyColor != null && def.bodyColor.Length >= 3) ? def.bodyColor
                      : (def.legsColor != null && def.legsColor.Length >= 3) ? def.legsColor
                      : null;
            if (rgb == null) return;
            try
            {
                __instance.slotAgent[n].transform.Find("Body").GetComponent<Image>().color =
                    new Color32((byte)Mathf.Clamp(rgb[0], 0, 255),
                                (byte)Mathf.Clamp(rgb[1], 0, 255),
                                (byte)Mathf.Clamp(rgb[2], 0, 255), 255);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Portrait tint failed for '" + def.name + "': " + e.Message);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Unlocks), nameof(Unlocks.LoadInitialUnlocks))]
        public static void AddUnlocks(Unlocks __instance)
        {
            GameController gc = GameController.gameController;
            foreach (CharacterDef def in CharacterRegistry.All)
            {
                Unlock unlock = __instance.AddUnlock(def.name, "Agent", isUnlocked: true);
                unlock.unlocked = true;
                // LoadInitialUnlocks fans unlocks into per-type lists before this
                // postfix runs, so the agent has to be added to agentUnlocks by hand.
                if (gc.sessionDataBig.agentUnlocks != null && !gc.sessionDataBig.agentUnlocks.Contains(unlock))
                {
                    gc.sessionDataBig.agentUnlocks.Add(unlock);
                    Unlock.agentCount++;
                }
                if (def.HasBigQuest && !gc.unlocks.IsUnlocked(def.bigQuestUnlock, "BigQuest"))
                    __instance.AddUnlock(def.bigQuestUnlock, "BigQuest", isUnlocked: false);

                Plugin.Log.LogInfo("Unlock registered for '" + def.name + "'.");
            }
        }

        // Names/descriptions for the agent, its ability item, and its big quest.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NameDB), nameof(NameDB.GetName))]
        public static bool Names(string myName, string type, ref string __result)
        {
            // Agent name & description
            CharacterDef byAgent = CharacterRegistry.ByAgentName(myName);
            if (byAgent != null)
            {
                if (type == "Agent") { __result = byAgent.name; return false; }
                if (type == "Description") { __result = byAgent.description ?? ""; return false; }
            }

            // Ability item name & description (myName is the internal ability id)
            CharacterDef byAbility = CharacterRegistry.ByAbilityId(myName);
            if (byAbility != null && byAbility.HasAbility)
            {
                if (type == "Item") { __result = byAbility.ability.name; return false; }
                if (type == "Description") { __result = byAbility.ability.description ?? ""; return false; }
            }

            // Big quest title ("<name>_BQ") and description ("D2_<name>_BQ", live progress)
            foreach (CharacterDef def in CharacterRegistry.All)
            {
                if (!def.HasBigQuest) continue;
                if (myName == def.bigQuestUnlock && type == "Unlock")
                {
                    __result = def.bigQuest.name;
                    return false;
                }
                if (myName == "D2_" + def.bigQuestUnlock && type == "Unlock")
                {
                    __result = BigQuestPatches.QuestDescription(def);
                    return false;
                }
            }
            return true;
        }

        // In-world body sprites are looked up as "<agentName><Direction>"; rewrite
        // them to the base body's names.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AgentHitbox), nameof(AgentHitbox.SetupBodyStrings))]
        public static void Body(AgentHitbox __instance)
        {
            Agent agent = __instance.agent;
            if (agent == null) return;
            CharacterDef def = CharacterRegistry.ByAgentName(agent.agentName);
            if (def == null) return;

            // With custom body art we keep the character's own front sprite ("<Name>S")
            // so the in-world front view is the real wizard, and only redirect the rest
            // of the walk rig to the baseBody (the walk frames aren't custom-drawn).
            bool custom = BodyArtPatches.HasCustomBody(def);
            string keep = BodyArtPatches.PortraitKey(def);
            for (int i = 0; i < __instance.agentBodyStrings.Count; i++)
            {
                string s = __instance.agentBodyStrings[i];
                if (!s.StartsWith(def.name)) continue;
                if (custom && s == keep) continue; // keep the custom "<Name>S"
                __instance.agentBodyStrings[i] = def.baseBody + s.Substring(def.name.Length);
            }
        }
    }
}
