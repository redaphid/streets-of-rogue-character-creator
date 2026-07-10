using CharacterCreator;
using UnityEngine;

// Cloner's custom power lives here in the character's own src/ folder - not in the
// shared engine. Because the csproj compiles characters/*/src/**/*.cs into the mod DLL
// and the EffectRegistry auto-discovers every IAbilityEffect at startup, this class
// registers the "clone" kind just by existing. Keep the namespace unique per character
// so class names never collide across folders.
namespace CharacterCreator.Characters.Cloner
{
    // Copycat: duplicate the furniture you're standing next to, the way the Hacker's
    // tool acts on the object you're facing. Finds the nearest spawnable world object
    // within `range` tiles and drops a fresh copy one tile beside it.
    public class CloneEffect : IAbilityEffect
    {
        public string Kind => "clone";

        public void Run(AbilityContext ctx)
        {
            Agent a = ctx.Agent; GameController gc = ctx.Gc; EffectDef fx = ctx.Fx;

            // Speak the instant the button works, even when there's nothing to copy.
            ctx.Say(string.IsNullOrEmpty(fx.shout) ? "Copycat!" : fx.shout);

            float reach = fx.range <= 0 ? 3f : fx.range;
            ObjectReal target = FindNearest(a, gc, reach);
            if (target == null)
            {
                a.Say("Nothing to copy!");
                try { gc.audioHandler.Play(a, "CantDo"); } catch { }
                return;
            }
            try
            {
                Vector3 at = target.tr.position + new Vector3(0.64f, 0f, 0f);
                gc.spawnerMain.spawnObjectReal(at, a, target.objectName);
                gc.spawnerMain.SpawnNoise(a.tr.position, 1f, null, null, a);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Clone failed: " + e); }
        }

        // Closest real, spawnable world object within `reach` tiles - the game's own
        // OverlapCircle lookup, so it works the same for keyboard and gamepad.
        private static ObjectReal FindNearest(Agent a, GameController gc, float reach)
        {
            Vector3 origin = a.tr.position;
            ObjectReal best = null;
            float bestDist = float.MaxValue;
            foreach (Collider2D c in Physics2D.OverlapCircleAll(origin, reach * 0.64f))
            {
                ObjectReal or = c.GetComponent<ObjectReal>() ?? c.GetComponentInParent<ObjectReal>();
                if (or == null || !or.isObjectReal || or.destroyed) continue;
                if (string.IsNullOrEmpty(or.objectName)) continue;
                if (!gc.gameResources.objectPrefabDic.ContainsKey(or.objectName)) continue;
                float dist = Vector2.Distance(origin, or.tr.position);
                if (dist < bestDist) { bestDist = dist; best = or; }
            }
            return best;
        }
    }
}
