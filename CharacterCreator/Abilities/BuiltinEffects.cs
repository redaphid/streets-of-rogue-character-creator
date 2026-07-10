using UnityEngine;

namespace CharacterCreator
{
    // The stock effect kinds, one class each. New per-character powers go in that
    // character's folder (characters/<id>/ability.cs); these are the shared defaults.

    // Fires a bulletStatus projectile where the player aims; tagged so the big quest
    // can credit the kills it causes. This is also the fallback for an unknown kind.
    public class BoltEffect : IAbilityEffect
    {
        public string Kind => "bolt";
        public void Run(AbilityContext ctx)
        {
            Agent a = ctx.Agent; GameController gc = ctx.Gc; EffectDef fx = ctx.Fx;
            if (string.IsNullOrEmpty(fx.bullet)) return;
            if (!System.Enum.TryParse(fx.bullet, out bulletStatus type))
            {
                Plugin.Log.LogWarning("Unknown bullet type '" + fx.bullet + "' for '" + ctx.Def.name + "'.");
                return;
            }
            ctx.Say(fx.shout);
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
    }

    // Teleports the caster to a valid tile between near..far units away.
    public class BlinkEffect : IAbilityEffect
    {
        public string Kind => "blink";
        public void Run(AbilityContext ctx)
        {
            Agent a = ctx.Agent; GameController gc = ctx.Gc; EffectDef fx = ctx.Fx;
            float near = fx.near <= 0 ? 3f : fx.near;
            float far = fx.far <= near ? near + 5f : fx.far;
            Vector2 spot = gc.tileInfo.FindLocationNearLocation(
                a.tr.position, a, near, far,
                accountForObstacles: true, notInside: false,
                dontCareAboutDanger: true, teleporting: true, accountForWalls: false);
            if (spot != Vector2.zero)
            {
                ctx.Say(fx.shout);
                a.Teleport(spot, bringOthers: false, immediate: true);
            }
            else
            {
                a.Say("...the spell fizzles.");
                gc.audioHandler.Play(a, "CantDo");
            }
        }
    }

    // Gives the caster a status effect for `seconds`.
    public class BuffEffect : IAbilityEffect
    {
        public string Kind => "buff";
        public void Run(AbilityContext ctx)
        {
            EffectDef fx = ctx.Fx;
            if (string.IsNullOrEmpty(fx.status)) return;
            ctx.Say(fx.shout);
            ctx.Agent.statusEffects.AddStatusEffect(fx.status, Mathf.Max(1, Mathf.RoundToInt(fx.seconds)));
        }
    }

    // Heals the caster (healAmount 0 = full heal).
    public class HealEffect : IAbilityEffect
    {
        public string Kind => "heal";
        public void Run(AbilityContext ctx)
        {
            Agent a = ctx.Agent; EffectDef fx = ctx.Fx;
            ctx.Say(fx.shout);
            if (fx.healAmount <= 0) a.currentHealth = (int)a.healthMax;
            else a.currentHealth = (int)Mathf.Min(a.healthMax, a.currentHealth + fx.healAmount);
        }
    }

    // Gives the caster `count` of an item (empty item = a random weapon).
    public class SpawnEffect : IAbilityEffect
    {
        public string Kind => "spawn";
        public void Run(AbilityContext ctx)
        {
            Agent a = ctx.Agent; EffectDef fx = ctx.Fx;
            ctx.Say(fx.shout);
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
    }
}
