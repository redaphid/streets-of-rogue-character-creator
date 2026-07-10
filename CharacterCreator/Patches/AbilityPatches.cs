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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InvItem), nameof(InvItem.SetupDetails))]
        public static void SetupAbilityItem(InvItem __instance)
        {
            CharacterDef def = CharacterRegistry.ByAbilityId(__instance.invItemName);
            if (def == null) return;

            InjectSprite(def);
            __instance.LoadItemSprite(def.abilityId);
            __instance.stackable = true;
            __instance.initCount = 0;         // starts ready
            __instance.lowCountThreshold = 100;
        }

        private static void InjectSprite(CharacterDef def)
        {
            if (spritesInjected.Contains(def.abilityId)) return;
            GameResources gr = GameController.gameController?.gameResources;
            if (gr?.itemDic == null) return;

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
            if (def == null || !def.HasAbility) return true; // not one of ours

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

        private static void CastRandom(Agent a, GameController gc, CharacterDef def)
        {
            EffectDef[] effects = def.ability.effects;
            if (effects == null || effects.Length == 0) return;
            EffectDef fx = effects[Random.Range(0, effects.Length)];
            Run(a, gc, fx, def);
        }

        private static void Run(Agent a, GameController gc, EffectDef fx, CharacterDef def)
        {
            switch ((fx.kind ?? "bolt").ToLowerInvariant())
            {
                case "blink": Blink(a, gc, fx); break;
                case "buff": Buff(a, fx); break;
                case "heal": Heal(a, fx); break;
                case "spawn": Spawn(a, fx); break;
                default: Bolt(a, gc, fx, def); break;
            }
        }

        // Fires a bulletStatus projectile where the player is aiming, tagged so the
        // big quest can credit the kills it causes.
        private static void Bolt(Agent a, GameController gc, EffectDef fx, CharacterDef def)
        {
            if (string.IsNullOrEmpty(fx.bullet)) return;
            if (!System.Enum.TryParse(fx.bullet, out bulletStatus type))
            {
                Plugin.Log.LogWarning("Unknown bullet type '" + fx.bullet + "' for '" + def.name + "'.");
                return;
            }
            Say(a, fx.shout);
            gc.audioHandler.Play(a, "MindControlFire");
            Bullet bolt = a.gun.spawnBullet(type, null, -1, specialAbility: true);
            if (bolt != null) bolt.cameFromWeapon = BigQuestPatches.BulletTag;
            gc.spawnerMain.SpawnNoise(a.tr.position, 2f, null, null, a);
            try
            {
                if (a.isPlayer > 0 && a.localPlayer) gc.ScreenBump(2f, 30, a);
                gc.playerControl.Vibrate(a.isPlayer, 0.25f, 0.2f);
            }
            catch { }
        }

        private static void Blink(Agent a, GameController gc, EffectDef fx)
        {
            float near = fx.near <= 0 ? 3f : fx.near;
            float far = fx.far <= near ? near + 5f : fx.far;
            Vector2 spot = gc.tileInfo.FindLocationNearLocation(
                a.tr.position, a, near, far,
                accountForObstacles: true, notInside: false,
                dontCareAboutDanger: true, teleporting: true, accountForWalls: false);
            if (spot != Vector2.zero)
            {
                Say(a, fx.shout);
                a.Teleport(spot, bringOthers: false, immediate: true);
            }
            else
            {
                a.Say("...the spell fizzles.");
                gc.audioHandler.Play(a, "CantDo");
            }
        }

        private static void Buff(Agent a, EffectDef fx)
        {
            if (string.IsNullOrEmpty(fx.status)) return;
            Say(a, fx.shout);
            a.statusEffects.AddStatusEffect(fx.status, Mathf.Max(1, Mathf.RoundToInt(fx.seconds)));
        }

        private static void Heal(Agent a, EffectDef fx)
        {
            Say(a, fx.shout);
            if (fx.healAmount <= 0) a.currentHealth = (int)a.healthMax;
            else a.currentHealth = (int)Mathf.Min(a.healthMax, a.currentHealth + fx.healAmount);
        }

        private static void Spawn(Agent a, EffectDef fx)
        {
            Say(a, fx.shout);
            try
            {
                a.inventory.DontPlayPickupSounds(yesNo: true);
                if (string.IsNullOrEmpty(fx.item))
                    a.inventory.AddRandWeapon();
                else
                    a.inventory.AddItem(fx.item, Mathf.Max(1, fx.count));
            }
            finally
            {
                try { a.inventory.DontPlayPickupSounds(yesNo: false); } catch { }
            }
        }

        private static void Say(Agent a, string line)
        {
            if (!string.IsNullOrEmpty(line)) { try { a.Say(line); } catch { } }
        }
    }
}
