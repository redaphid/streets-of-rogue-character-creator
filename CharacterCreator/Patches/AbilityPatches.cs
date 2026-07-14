using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace CharacterCreator
{
    // The custom special abilities. Each press picks one of the character's
    // configured effects at random and runs it through the game's own
    // projectile / teleport / status APIs (which already sync in multiplayer).
    // Modeled exactly on the vanilla MindControl ability and WizardMod's
    // ChaosMagic, but data-driven from EffectDef instead of a hard-coded switch.
    [HarmonyPatch]
    public static class AbilityPatches
    {
        private static readonly HashSet<string> spritesInjected = new HashSet<string>();

        // ---- item definition ----------------------------------------------------

        // Inject every ability icon as soon as the item dictionary is built,
        // instead of waiting for the first ability item's SetupDetails (i.e. the
        // first spawn of that character). Anything that reads gr.itemDic before
        // then - HUD warm-up, ability previews - would get a silent blank icon
        // (LoadItemSprite swallows the missing key).
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameResources), nameof(GameResources.SetupDics))]
        public static void InjectAllSprites(GameResources __instance)
        {
            if (__instance.itemDic == null) return;
            foreach (CharacterDef def in CharacterRegistry.All)
                if (def.HasAbility)
                    InjectSprite(def, __instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InvItem), nameof(InvItem.SetupDetails))]
        public static void SetupAbilityItem(InvItem __instance)
        {
            CharacterDef def = CharacterRegistry.ByAbilityId(__instance.invItemName);
            if (def == null) return;

            InjectSprite(def, null); // safety net if SetupDics never ran
            __instance.LoadItemSprite(def.abilityId);
            __instance.stackable = true;
            __instance.initCount = 0;         // starts ready
            __instance.lowCountThreshold = 100;
        }

        private static void InjectSprite(CharacterDef def, GameResources gr)
        {
            if (gr == null) gr = GameController.gameController?.gameResources;
            if (gr?.itemDic == null) return;
            // The HashSet only skips re-reading the PNG; still verify the key is
            // present, since SetupDics can rebuild the dictionaries.
            if (spritesInjected.Contains(def.abilityId) && gr.itemDic.ContainsKey(def.abilityId)) return;

            if (!gr.itemDic.ContainsKey(def.abilityId))
            {
                string path = CharacterLoader.AbilityIconPath(def);
                if (path != null)
                {
                    try
                    {
                        byte[] png = File.ReadAllBytes(path);
                        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        tex.LoadImage(png);
                        tex.filterMode = FilterMode.Point;
                        gr.itemDic[def.abilityId] = Sprite.Create(
                            tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 64f);
                    }
                    catch (System.Exception e)
                    {
                        Plugin.Log.LogWarning("Failed to load ability icon for '" + def.name + "': " + e.Message);
                    }
                }
                // If there is still no sprite, alias an existing item so the HUD slot
                // isn't blank (the game logs a missing-sprite error otherwise).
                if (!gr.itemDic.ContainsKey(def.abilityId) && gr.itemDic.ContainsKey("MindControl"))
                    gr.itemDic[def.abilityId] = gr.itemDic["MindControl"];
            }
            spritesInjected.Add(def.abilityId);
        }

        // ---- pressing the ability ------------------------------------------------

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StatusEffects), nameof(StatusEffects.PressedSpecialAbility))]
        public static bool Press(StatusEffects __instance, ref bool __result)
        {
            Agent agent = __instance.agent;
            if (agent == null) return true;
            CharacterDef def = CharacterRegistry.ByAbilityId(agent.specialAbility);
            if (def == null || !def.HasAbility)
            {
                // Safety net: if the spawn-time grant somehow didn't stick (agent renamed
                // after SetupAgentStats, level transition, etc.), recover by agent name
                // and grant the ability on the spot so the button always works.
                CharacterDef byName = CharacterRegistry.ByAgentName(agent.agentName);
                if (byName == null || !byName.HasAbility) return true; // genuinely not ours
                def = byName;
                Plugin.Log.LogWarning("Ability not equipped for '" + agent.agentName +
                    "'; granting '" + def.abilityId + "' on press.");
                try { agent.statusEffects.GiveSpecialAbility(def.abilityId); }
                catch (System.Exception e) { Plugin.Log.LogWarning("Late grant failed: " + e); }
            }

            __result = false;
            if (agent.ghost || agent.teleporting) return false;
            InvItem item = agent.inventory.equippedSpecialAbility;
            if (item == null) return false;
            GameController gc = GameController.gameController;

            if (item.invItemCount != 0)
            {
                gc.audioHandler.Play(agent, "CantDo");
                return false;
            }

            item.invItemCount = Mathf.Max(1, def.ability.cooldown);
            __instance.StartCoroutine(Recharge(__instance, item));
            SetSlotUsable(agent, false);

            try { CastRandom(agent, gc, def); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Ability cast failed for '" + def.name + "': " + e); }

            __result = true;
            return false;
        }

        // Mirrors the MindControl branch of StatusEffects.RechargeSpecialAbility2.
        private static IEnumerator Recharge(StatusEffects se, InvItem item)
        {
            Agent agent = se.agent;
            while (item.invItemCount > 0 && agent.inventory.equippedSpecialAbility == item)
            {
                yield return new WaitForSeconds(1f);
                if (!se.CanRecharge()) continue;
                item.invItemCount--;
                if (item.invItemCount == 0)
                {
                    try
                    {
                        se.CreateBuffText("Recharged", agent.objectNetID);
                        GameController.gameController.audioHandler.Play(agent, "Recharge");
                    }
                    catch { }
                    SetSlotUsable(agent, true);
                }
            }
        }

        // The HUD may be absent (headless tests, remote players) - never let it kill a cast.
        private static void SetSlotUsable(Agent agent, bool usable)
        {
            try
            {
                EquippedItemSlot slot = agent.inventory.buffDisplay?.specialAbilitySlot;
                if (slot == null) return;
                if (usable) slot.MakeUsable();
                else slot.MakeNotUsable();
            }
            catch { }
        }

        // ---- effects -------------------------------------------------------------

        // Picks one of the character's configured effects at random and dispatches it
        // through the EffectRegistry. Each "kind" is handled by an IAbilityEffect - the
        // built-ins in Abilities/, plus any per-character effect under
        // characters/<id>/src/. Unknown kinds fall back to "bolt".
        private static void CastRandom(Agent a, GameController gc, CharacterDef def)
        {
            EffectDef[] effects = def.ability.effects;
            if (effects == null || effects.Length == 0) return;
            EffectDef fx = effects[Random.Range(0, effects.Length)];
            IAbilityEffect effect = EffectRegistry.Resolve(fx.kind);
            if (effect == null) { Plugin.Log.LogWarning("No effect for kind '" + fx.kind + "'."); return; }
            effect.Run(new AbilityContext { Agent = a, Gc = gc, Fx = fx, Def = def });
            BigQuestPatches.NotifyAbility(a, fx.kind ?? "bolt");
        }
    }
}
