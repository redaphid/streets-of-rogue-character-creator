using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace CharacterCreator
{
    // Drives each custom character's Big Quest. The quest itself is a BigQuest instance
    // (default "kills", or a per-character code quest under characters/<id>/src/); this
    // class is the plumbing that feeds it game events, checks completion, grants the
    // payoff, and paints the quest panel. Generalized from WizardMod's single quest.
    [HarmonyPatch]
    public static class BigQuestPatches
    {
        public const string BulletTag = "CC_Ability";

        // How long after a bolt strikes a foe a following kill still counts (covers
        // burn/freeze delays without crediting unrelated kills).
        private const float AttributionWindow = 6f;

        // One BigQuest per player Agent; survives floor transitions (same Agent streams
        // across levels) and resets for a new run (new Agent).
        private static readonly ConditionalWeakTable<Agent, BigQuest> quests =
            new ConditionalWeakTable<Agent, BigQuest>();

        // Foes recently struck by a custom character's ability bolt, so the kill that
        // follows can be attributed to the ability rather than a gun/knife.
        private static readonly ConditionalWeakTable<Agent, HitTag> hits =
            new ConditionalWeakTable<Agent, HitTag>();

        private class HitTag { public Agent owner; public float time; }

        // The live quest for an agent, created on first need if the agent is one of our
        // characters and has a Big Quest.
        private static BigQuest GetQuest(Agent a)
        {
            if (a == null) return null;
            if (quests.TryGetValue(a, out BigQuest existing)) return existing;
            CharacterDef def = CharacterRegistry.ByAgentName(a.agentName);
            if (def == null || !def.HasBigQuest) return null;
            BigQuest q = BigQuestRegistry.Create(def, a);
            if (q != null) quests.Add(a, q);
            return q;
        }

        private static void CheckComplete(Agent a, BigQuest q)
        {
            if (q == null || q.Completed || !q.IsComplete) return;
            GameController gc = GameController.gameController;
            if (gc == null || !gc.serverPlayer) return; // completion + payoff are host-authoritative
            q.Completed = true;
            Complete(a, gc, q.Def);
        }

        // ---- event sources feeding the quest ----

        // Called by AbilityPatches after a custom character's ability effect runs.
        public static void NotifyAbility(Agent a, string effectKind)
        {
            BigQuest q = GetQuest(a);
            if (q == null) return;
            q.OnAbility(effectKind);
            CheckComplete(a, q);
        }

        // Called by an effect via ctx.QuestEvent(name) - a custom signal a quest can count.
        public static void NotifyEvent(Agent a, string name)
        {
            BigQuest q = GetQuest(a);
            if (q == null) return;
            q.OnEvent(name);
            CheckComplete(a, q);
        }

        // Tag a foe struck by a custom character's ability bolt.
        //
        // This MUST run as a prefix: BulletHitbox.HitAftermath applies the bullet's
        // damage inside its own body (agent.Damage(...)), so a bolt that kills on
        // impact triggers the victim's death - and the game's
        // AddBigQuestPoints(killer, victim, "Neutralize") call that CountPoints below
        // consumes - *before* HitAftermath returns. A postfix here would record the
        // hit too late, after that Neutralize had already been scored with no tag, so
        // impact kills (Taser, direct GhostBlaster, the finishing bolt on a weakened
        // foe, ...) never counted and the quest crept along on delayed burn/freeze
        // kills alone - never reaching its target. Tagging in the prefix credits both.
        [HarmonyPrefix]
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

        // The game fires AddBigQuestPoints(actor, target, pointsType) on the server for
        // every big-quest-relevant action - "Neutralize" per kill, plus "ServeDrink",
        // "Research", "ArrestGuilty", "SellItem", "Destruction", etc. Forward them all.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Quests), nameof(Quests.AddBigQuestPoints),
            new[] { typeof(Agent), typeof(Agent), typeof(InvItem), typeof(string) })]
        public static void CountPoints(Agent myAgent, Agent otherAgent, string pointsType)
        {
            GameController gc = GameController.gameController;
            if (myAgent == null || gc == null || !gc.serverPlayer) return;
            BigQuest q = GetQuest(myAgent);
            if (q == null) return;

            if (pointsType == "Neutralize" && otherAgent != null)
            {
                bool viaAbility = false;
                if (hits.TryGetValue(otherAgent, out HitTag tag))
                {
                    hits.Remove(otherAgent);
                    viaAbility = tag.owner == myAgent && Time.time - tag.time <= AttributionWindow;
                }
                q.OnKill(otherAgent, viaAbility);
            }
            q.OnPoints(pointsType);
            CheckComplete(myAgent, q);
        }

        // ---- completion + payoff ----

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

        // ---- quest panel ----

        // Panel / NameDB text with live progress. Uses the live quest if the local
        // player is running this character, else renders the template at zero progress.
        public static string QuestDescription(CharacterDef def)
        {
            BigQuest q = LocalQuest(def);
            if (q != null) return q.Describe();
            string t = def.bigQuest?.description;
            if (string.IsNullOrEmpty(t)) t = "{name}\n{progress}/{target}";
            int target = def.bigQuest != null ? def.bigQuest.Goal : 0;
            return t
                .Replace("{kills}", "0").Replace("{progress}", "0")
                .Replace("{target}", target.ToString())
                .Replace("{name}", def.bigQuest?.name ?? "");
        }

        private static BigQuest LocalQuest(CharacterDef def)
        {
            Agent a = LocalAgent(def);
            return a == null ? null : GetQuest(a);
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
