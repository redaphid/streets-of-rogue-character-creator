using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace CharacterCreator
{
    // Each custom character gets a Big Quest: "slay N foes with your special
    // ability." Kills are attributed by tagging the bolts the ability fires and
    // counting when a tagged victim dies. Counting is server-authoritative, like
    // vanilla big quests. Generalized from WizardMod's WizardBigQuest +
    // WizardQuestPanel, keyed off the agent so it works for any character.
    [HarmonyPatch]
    public static class BigQuestPatches
    {
        public const string BulletTag = "CC_Ability";

        // How long after a bolt strikes a foe a following kill still counts (covers
        // burn/freeze delays without crediting unrelated kills).
        private const float AttributionWindow = 6f;

        // Per-run progress on the agent instance: survives floor transitions (same
        // Agent streams across levels) and resets for a new run (new Agent).
        private static readonly ConditionalWeakTable<Agent, Progress> progress =
            new ConditionalWeakTable<Agent, Progress>();

        // Foes recently struck by a custom character's ability bolt, so the kill
        // that follows can be attributed to the ability rather than a gun/knife.
        private static readonly ConditionalWeakTable<Agent, HitTag> hits =
            new ConditionalWeakTable<Agent, HitTag>();

        private class Progress { public int kills; public bool completed; }
        private class HitTag { public Agent owner; public float time; }

        // Quest panel / NameDB description with live progress filled in.
        public static string QuestDescription(CharacterDef def)
        {
            int kills = LocalKills(def);
            string template = def.bigQuest.description;
            if (string.IsNullOrEmpty(template))
                template = "Slay {target} foes with your special ability.\nKills: {kills}/{target}";
            return template
                .Replace("{kills}", kills.ToString())
                .Replace("{target}", def.bigQuest.targetKills.ToString());
        }

        private static int LocalKills(CharacterDef def)
        {
            Agent a = LocalAgent(def);
            return a == null ? 0 : progress.GetOrCreateValue(a).kills;
        }

        private static Agent LocalAgent(CharacterDef def)
        {
            GameController gc = GameController.gameController;
            if (gc == null) return null;
            if (gc.playerAgent != null && gc.playerAgent.agentName == def.name) return gc.playerAgent;
            if (gc.playerAgentList != null)
                foreach (Agent a in gc.playerAgentList)
                    if (a != null && a.localPlayer && a.agentName == def.name) return a;
            return null;
        }

        // Tag a foe struck by a custom character's ability bolt.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(BulletHitbox), nameof(BulletHitbox.HitAftermath))]
        public static void TagVictim(BulletHitbox __instance, Agent agent)
        {
            Bullet b = __instance.myBullet;
            if (b == null || agent == null || agent.isPlayer != 0) return;
            if (b.cameFromWeapon != BulletTag || b.agent == null) return;
            if (CharacterRegistry.ByAgentName(b.agent.agentName) == null) return;

            HitTag tag = hits.GetOrCreateValue(agent);
            tag.owner = b.agent;
            tag.time = Time.time;
        }

        // The game fires AddBigQuestPoints(killer, victim, "Neutralize") once per
        // kill on the server - the canonical per-kill hook.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Quests), nameof(Quests.AddBigQuestPoints),
            new[] { typeof(Agent), typeof(Agent), typeof(InvItem), typeof(string) })]
        public static void CountKill(Agent myAgent, Agent otherAgent, string pointsType)
        {
            GameController gc = GameController.gameController;
            if (myAgent == null || otherAgent == null || gc == null || !gc.serverPlayer) return;
            if (pointsType != "Neutralize") return;

            CharacterDef def = CharacterRegistry.ByBigQuest(myAgent.bigQuest);
            if (def == null || !def.HasBigQuest) return;

            HitTag tag;
            if (!hits.TryGetValue(otherAgent, out tag)) return;
            hits.Remove(otherAgent);
            if (tag.owner != myAgent || Time.time - tag.time > AttributionWindow) return;

            Progress pr = progress.GetOrCreateValue(myAgent);
            if (pr.completed) return;
            pr.kills++;
            try { myAgent.Say("Kills: " + pr.kills + "/" + def.bigQuest.targetKills); } catch { }
            if (pr.kills >= def.bigQuest.targetKills)
            {
                pr.completed = true;
                Complete(myAgent, gc, def);
            }
        }

        private static void Complete(Agent w, GameController gc, CharacterDef def)
        {
            Plugin.Log.LogInfo("Big Quest '" + def.bigQuest.name + "' complete for " + def.name + "!");
            MarkUnlockComplete(gc, def);
            try
            {
                if (gc.sessionData.agentsCompletedBigQuest != null &&
                    !gc.sessionData.agentsCompletedBigQuest.Contains(w.isPlayer))
                    gc.sessionData.agentsCompletedBigQuest.Add(w.isPlayer);
            }
            catch { }
            try { gc.spawnerMain.SpawnStatusText(w, "BigQuestCompleted", "BigQuest", "Interface"); } catch { }
            try { w.Say(def.bigQuest.name + "!"); } catch { }
            GrantPayoff(w);
        }

        private static void MarkUnlockComplete(GameController gc, CharacterDef def)
        {
            try
            {
                if (gc.sessionDataBig?.unlocks == null) return;
                foreach (Unlock u in gc.sessionDataBig.unlocks)
                {
                    if (u.unlockName == def.bigQuestUnlock && u.unlockType == "BigQuest")
                    {
                        u.unlocked = true;
                        gc.unlocks.SaveUnlockData();
                        return;
                    }
                }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("BQ unlock mark failed: " + e); }
        }

        // A big in-run payoff, matching WizardMod: full heal, a power surge, XP, a
        // loot stash, and an instant ability recharge for the victory lap.
        private static void GrantPayoff(Agent w)
        {
            try { w.currentHealth = (int)w.healthMax; } catch { }
            try
            {
                w.statusEffects.AddStatusEffect("Giant", 20);
                w.statusEffects.AddStatusEffect("Fast", 30);
            }
            catch { }
            try { for (int i = 0; i < 3; i++) w.skillPoints.AddPoints("KillPoints"); } catch { }
            try
            {
                w.inventory.DontPlayPickupSounds(yesNo: true);
                w.inventory.AddItem("Money", 1000);
                w.inventory.AddRandWeapon();
                w.inventory.AddRandWeapon();
                w.inventory.DontPlayPickupSounds(yesNo: false);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("BQ loot failed: " + e); }
            try
            {
                InvItem ability = w.inventory.equippedSpecialAbility;
                if (ability != null) ability.invItemCount = 0;
            }
            catch { }
        }

        // Restore the quest text on the map screen. QuestSlotBig.GetQuestInfo runs
        // `switch (agent.bigQuest)`; a custom character's bigQuest has no case, so it
        // hits the default branch that blanks the text. This postfix puts it back.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(QuestSlotBig), nameof(QuestSlotBig.GetQuestInfo))]
        public static void RestoreQuestText(QuestSlotBig __instance)
        {
            try
            {
                Agent agent = __instance.agent;
                if (agent == null) return;
                CharacterDef def = CharacterRegistry.ByBigQuest(agent.bigQuest);
                if (def == null || !def.HasBigQuest) return;

                GameController gc = GameController.gameController;
                if (gc == null) return;
                // The Mayor's floor shows an end-of-run message before the switch;
                // leave that alone.
                if (gc.loadLevel != null && gc.loadLevel.LevelContainsMayor()) return;

                string title = null;
                try { title = gc.nameDB.GetName(def.bigQuestUnlock, "Unlock"); } catch { }
                if (string.IsNullOrEmpty(title)) title = def.bigQuest.name;

                string desc = null;
                try { desc = gc.nameDB.GetName("D2_" + def.bigQuestUnlock, "Unlock"); } catch { }
                if (string.IsNullOrEmpty(desc)) desc = QuestDescription(def);

                if (__instance.questTitle != null)
                {
                    __instance.questTitle.gameObject.SetActive(true);
                    __instance.questTitle.text = title;
                }
                if (__instance.questDescription != null)
                    __instance.questDescription.text = desc;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("Quest panel restore failed: " + e);
            }
        }
    }
}
