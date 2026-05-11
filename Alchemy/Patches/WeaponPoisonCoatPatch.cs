using System;
using System.Collections.Generic;
using Alchemy.Behavior;
using Alchemy.ModConfig;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

// poison damage shouldn't work on mechanical entities
// Apply poison to weapons one charge at a time

// Config should set max amount of charges and effect multiplier for weapon coats
// Apply strength
namespace Alchemy.Patches
{
    [HarmonyPatch(typeof(CollectibleObject), "OnAttackingWith")]
    public static class WeaponCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(
            IWorldAccessor world,
            Entity byEntity,
            Entity attackedEntity,
            ItemSlot itemslot
        )
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return;
            if (world.Side != EnumAppSide.Server)
                return;
            if (itemslot?.Itemstack == null)
                return;
            if (attackedEntity == null || !attackedEntity.Alive)
                return;

            var attrs = itemslot.Itemstack.Attributes;
            string potionId = attrs.GetString("coatedPotionId");
            if (string.IsNullOrEmpty(potionId))
                return;

            int charges = attrs.GetInt("coatCharges");
            if (charges <= 0)
            {
                attrs.RemoveAttribute("coatedPotionId");
                attrs.RemoveAttribute("coatedDisplayName");
                attrs.RemoveAttribute("coatCharges");
                attrs.RemoveAttribute("coatMultiplier");
                itemslot.MarkDirty();
                return;
            }

            float multiplier = attrs.GetFloat(
                "coatMultiplier",
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier
            );
            WeaponCoatEffects.Apply(potionId, attackedEntity, multiplier);

            charges--;
            if (charges <= 0)
            {
                attrs.RemoveAttribute("coatedPotionId");
                attrs.RemoveAttribute("coatedDisplayName");
                attrs.RemoveAttribute("coatCharges");
                attrs.RemoveAttribute("coatMultiplier");
            }
            else
            {
                attrs.SetInt("coatCharges", charges);
            }
            itemslot.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(EntityProjectileBase), "ImpactOnEntity")]
    public static class ArrowCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityProjectileBase __instance, Entity target)
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return;
            if (__instance.World.Side != EnumAppSide.Server)
                return;

            ItemStack projectileStack = __instance.ProjectileStack;
            if (projectileStack == null)
                return;

            string potionId = projectileStack.Attributes.GetString("coatedPotionId");
            if (string.IsNullOrEmpty(potionId))
                return;

            float multiplier = projectileStack.Attributes.GetFloat(
                "coatMultiplier",
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier
            );
            projectileStack.Attributes.RemoveAttribute("coatedPotionId");
            projectileStack.Attributes.RemoveAttribute("coatedDisplayName");
            projectileStack.Attributes.RemoveAttribute("coatMultiplier");

            if (target != null && target.Alive)
                WeaponCoatEffects.Apply(potionId, target, multiplier);
        }
    }

    [HarmonyPatch(typeof(EntityProjectile), "OnCollided")]
    public static class ArrowTerrainCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityProjectile __instance)
        {
            if (__instance.World.Side != EnumAppSide.Server)
                return;
            __instance.ProjectileStack?.Attributes.RemoveAttribute("coatedPotionId");
            __instance.ProjectileStack?.Attributes.RemoveAttribute("coatedDisplayName");
            __instance.ProjectileStack?.Attributes.RemoveAttribute("coatMultiplier");
        }
    }

    internal static class WeaponCoatEffects
    {
        internal static void Apply(string potionId, Entity entity, float multiplier)
        {
            if (entity == null || !entity.Alive)
                return;

            if (entity is EntityPlayer playerEntity)
            {
                var behavior = playerEntity.GetBehavior<EntityBehaviorPotionEffect>();
                if (behavior?.Manager == null)
                    return;
                PotionContext ctx = PotionRegistry.BuildPotionDef(potionId, multiplier);
                if (ctx != null)
                    behavior.Manager.TryApplyPotion(potionId, ctx, potionId);
            }
            else if (entity is EntityAgent agent)
            {
                PotionContext ctx = PotionRegistry.BuildPotionDef(potionId, multiplier);
                if (ctx == null)
                    return;

                if (ctx.TickSec > 0 && Math.Abs(ctx.Health) > float.Epsilon)
                    ApplyTickEffect(agent, ctx);
                else if (ctx.Effects.Count > 0 && ctx.Duration > 0)
                    ApplyStatEffect(agent, ctx);
            }
        }

        private static void ApplyTickEffect(EntityAgent agent, PotionContext ctx)
        {
            if (Math.Abs(ctx.Health) > float.Epsilon)
            {
                if (agent.HasBehavior<EntityBehaviorPoisoned>())
                    agent
                        .GetBehavior<EntityBehaviorPoisoned>()
                        .Refresh(ctx.Health, ctx.TickSec, ctx.Duration, ctx.IgnoreArmour);
                else
                {
                    var b = new EntityBehaviorPoisoned(agent);
                    agent.AddBehavior(b);
                    b.Setup(ctx.Health, ctx.TickSec, ctx.Duration, ctx.IgnoreArmour);
                }
            }
        }

        private static void ApplyStatEffect(EntityAgent agent, PotionContext ctx)
        {
            const string subkey = "weaponcoat";
            foreach (var stat in ctx.Effects)
                agent.Stats.Set(stat.Key, subkey, stat.Value, false);

            long agentId = agent.EntityId;
            var effectKeys = new List<string>(ctx.Effects.Keys);
            agent.World.RegisterCallback(
                _ =>
                {
                    if (agent.World.GetEntityById(agentId) is not EntityAgent target)
                        return;
                    foreach (string key in effectKeys)
                        target.Stats.Remove(key, subkey);
                },
                ctx.Duration * 1000
            );
        }
    }
}
