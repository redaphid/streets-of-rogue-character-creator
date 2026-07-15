using HarmonyLib;
using UnityEngine;

namespace CharacterCreator
{
    // Applies each custom character's stats, starting items, ability and body
    // tint when the game builds that agent. Generalized from WizardMod's
    // WizardStats postfix.
    [HarmonyPatch]
    public static class StatsPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Agent), nameof(Agent.SetupAgentStats))]
        public static void ApplyStats(Agent __instance)
        {
            CharacterDef def = CharacterRegistry.ByAgentName(__instance.agentName);
            if (def == null) return;
            Agent a = __instance;

            StatBlock s = def.stats ?? new StatBlock();
            a.SetStrength(Clamp(s.strength));
            a.SetEndurance(Clamp(s.endurance));
            a.SetAccuracy(Clamp(s.accuracy));
            a.SetSpeed(Clamp(s.speed));
            a.modMeleeSkill = s.meleeSkill;
            a.modGunSkill = s.gunSkill;
            a.modToughness = s.toughness;
            a.modVigilant = s.vigilant;

            if (def.HasAbility)
                AbilityPatches.EquipAbility(a, def.abilityId);

            // A hat/headpiece the game draws on the head per-direction (a pointy hat
            // reads as a wizard from every facing without a full custom body rig).
            // Set defaultArmorHead DIRECTLY: the game's AddStartingHeadPiece only
            // installs a non-permanent hat before the level finishes loading, so it's
            // a no-op when we grant on an already-loaded level (the harness transform).
            // AgentHitbox renders defaultArmorHead.invItemName + playerDir every frame.
            if (!string.IsNullOrEmpty(def.headPiece))
            {
                try
                {
                    InvItem hat = new InvItem();
                    hat.invItemName = def.headPiece;
                    hat.SetupDetails(notNew: false);
                    a.inventory.defaultArmorHead = hat;
                    a.agentHitboxScript.MustRefresh();
                }
                catch (System.Exception e) { Plugin.Log.LogWarning("headPiece '" + def.headPiece + "' failed: " + e.Message); }
            }

            if (def.startingItems != null)
                foreach (StartItem item in def.startingItems)
                    if (item != null && !string.IsNullOrEmpty(item.name))
                        a.inventory.AddItemPlayerStart(item.name, 0);

            if (def.legsColor != null && def.legsColor.Length >= 3)
                a.agentHitboxScript.legsColor = ToColor(def.legsColor);
            if (def.bodyColor != null && def.bodyColor.Length >= 3)
                a.agentHitboxScript.bodyColor = ToColor(def.bodyColor);
        }

        private static int Clamp(int stat) => stat < 0 ? 0 : (stat > 5 ? 5 : stat);

        private static Color32 ToColor(int[] rgb)
        {
            byte r = (byte)Mathf.Clamp(rgb[0], 0, 255);
            byte g = (byte)Mathf.Clamp(rgb[1], 0, 255);
            byte b = (byte)Mathf.Clamp(rgb[2], 0, 255);
            return new Color32(r, g, b, 255);
        }
    }
}
