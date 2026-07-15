using System;

namespace CharacterCreator
{
    // Test-harness autoload. Completely inert unless the SOR_TEST_MODE test driver
    // set SOR_TEST_CHAR to one of OUR custom characters. The driver's char-select
    // automation force-selects the slot but the game still spawns a default agent
    // and only transforms it into the picked character via TransformAgent on load;
    // when driving a custom character that transform doesn't happen, so the run
    // shows the default (a Hobo) instead of, say, the Wizard.
    //
    // This closes that gap the same way the game itself does: it calls the vanilla
    // spawnerMain.TransformAgent(playerAgent, name), which sets the agent's name and
    // re-runs SetupAgentStats - firing StatsPatches, which applies the custom stats
    // and grants the special ability. Result: the driver's requested custom
    // character is actually loaded and playable on camera.
    public static class TestHarness
    {
        private static string target;
        private static bool active;
        private static float nextTry;

        public static void Init()
        {
            target = Environment.GetEnvironmentVariable("SOR_TEST_CHAR");
            active = !string.IsNullOrEmpty(target) && CharacterRegistry.ByAgentName(target) != null;
            if (active)
                Plugin.Log.LogInfo("Test harness autoload armed for custom character '" + target + "'.");
        }

        public static void Tick()
        {
            if (!active) return;
            GameController gc = GameController.gameController;
            if (gc == null || !gc.loadComplete || gc.playerAgent == null) return;
            // GiveSpecialAbility (and much of SetupAgentStats) is a no-op on the home
            // base, so transforming there loses the ability. Wait for a real level.
            if (gc.levelType == "HomeBase") return;

            if (UnityEngine.Time.realtimeSinceStartup < nextTry) return;
            nextTry = UnityEngine.Time.realtimeSinceStartup + 2f;

            Agent a = gc.playerAgent;
            CharacterDef def = CharacterRegistry.ByAgentName(target);
            try
            {
                if (a.agentName != target)
                {
                    Plugin.Log.LogInfo("Test harness: transforming player '" + a.agentName + "' -> '" + target + "'.");
                    gc.spawnerMain.TransformAgent(a, target);
                    return;
                }
                // Already the character; make sure the ability actually equipped (a
                // transform that first happened on the home base would have skipped it).
                if (def != null && def.HasAbility && string.IsNullOrEmpty(a.specialAbility))
                {
                    Plugin.Log.LogInfo("Test harness: granting ability '" + def.abilityId + "' to " + target + ".");
                    a.statusEffects.GiveSpecialAbility(def.abilityId);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Test harness step failed: " + e.Message);
            }
        }
    }
}
