using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

// poison damage shouldn't work on mechanical entities
// Apply poison to weapons one charge at a time

// Config should set max amount of charges and effect multiplier for weapon coats
// Apply strength
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
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

            ITreeAttribute attrs = itemslot.Itemstack.Attributes;
            string potionId = attrs.GetString("coatedPotionId");
            if (string.IsNullOrEmpty(potionId))
                return;

            int charges = attrs.GetInt("coatCharges");
            if (charges <= 0)
            {
                attrs.RemoveAttribute("coatedPotionId");
                attrs.RemoveAttribute("coatedItemCode");
                attrs.RemoveAttribute("coatCharges");
                attrs.RemoveAttribute("coatMultiplier");
                itemslot.MarkDirty();
                return;
            }

            if (!PotionConsumableLogic.IsCoatingAllowed(potionId))
                return;

            float multiplier = attrs.GetFloat(
                "coatMultiplier",
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier
            );
            string coatedItemCode = attrs.GetString("coatedItemCode");
            string displayName = WeaponCoatEffects.ResolveDisplayName(coatedItemCode, potionId);
            WeaponCoatEffects.Apply(potionId, attackedEntity, multiplier, displayName);

            charges--;
            if (charges <= 0)
            {
                attrs.RemoveAttribute("coatedPotionId");
                attrs.RemoveAttribute("coatedItemCode");
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

            if (!PotionConsumableLogic.IsCoatingAllowed(potionId))
                return;

            float multiplier = projectileStack.Attributes.GetFloat(
                "coatMultiplier",
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier
            );
            string coatedItemCode = projectileStack.Attributes.GetString("coatedItemCode");
            string displayName = WeaponCoatEffects.ResolveDisplayName(coatedItemCode, potionId);
            projectileStack.Attributes.RemoveAttribute("coatedPotionId");
            projectileStack.Attributes.RemoveAttribute("coatedItemCode");
            projectileStack.Attributes.RemoveAttribute("coatMultiplier");

            if (target != null && target.Alive)
                WeaponCoatEffects.Apply(potionId, target, multiplier, displayName);
        }
    }

    [HarmonyPatch(typeof(EntityProjectile), "OnCollided")]
    public static class ArrowTerrainCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityProjectile __instance)
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return;
            if (__instance.World.Side != EnumAppSide.Server)
                return;
            __instance.ProjectileStack?.Attributes.RemoveAttribute("coatedPotionId");
            __instance.ProjectileStack?.Attributes.RemoveAttribute("coatedItemCode");
            __instance.ProjectileStack?.Attributes.RemoveAttribute("coatMultiplier");
        }
    }

    [HarmonyPatch(typeof(BlockEntityBarrel), "FindMatchingRecipe", typeof(IPlayer))]
    public static class BarrelCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(BlockEntityBarrel __instance)
        {
            if (
                !AlchemyConfig.Loaded.AllowWeaponCoating || !AlchemyConfig.Loaded.AllowBarrelCoating
            )
                return;

            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server)
                return;

            try
            {
                InventoryBase inv = __instance.Inventory;
                if (inv == null || inv.Count < 2)
                    return;

                PotionConsumableLogic.TryCoatInBarrel(api, inv[0], inv[1]);
            }
            catch (Exception e)
            {
                api.Logger.Error(
                    "[Alchemy] Barrel coating failed, if the error occurs a lot consider turning off the AllowBarrelCoating config option: {0}",
                    e
                );
            }
        }
    }

    internal static class WeaponCoatEffects
    {
        internal static string ResolveDisplayName(string langKey, string fallback)
        {
            return string.IsNullOrEmpty(langKey) ? fallback : Lang.Get(langKey);
        }

        internal static void Apply(
            string potionId,
            Entity entity,
            float multiplier,
            string displayName
        )
        {
            if (entity == null || !entity.Alive)
                return;

            PotionConsumableLogic.ApplySideEffects(entity, potionId, multiplier);

            if (entity is EntityPlayer playerEntity)
            {
                EntityBehaviorEffects behavior =
                    playerEntity.GetBehavior<EntityBehaviorEffects>();
                if (behavior?.Manager == null)
                    return;
                EffectContext ctx = EffectRegistry.Build(potionId, multiplier);
                if (ctx == null)
                    return;

                if (!ctx.ResetsEffects)
                {
                    string exclusivityBlock = PotionConsumableLogic.CheckPotionExclusivity(
                        playerEntity,
                        potionId
                    );
                    if (exclusivityBlock != null)
                    {
                        (playerEntity.Player as IServerPlayer)?.SendMessage(
                            GlobalConstants.InfoLogChatGroup,
                            Lang.Get(exclusivityBlock),
                            EnumChatType.Notification
                        );
                        return;
                    }
                }

                if (behavior.Manager.TryApply(potionId, ctx, displayName))
                {
                    (playerEntity.Player as IServerPlayer)?.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:effect-gain", displayName),
                        EnumChatType.Notification
                    );
                }
            }
            else if (entity is EntityAgent agent)
            {
                EffectContext ctx = EffectRegistry.Build(potionId, multiplier);
                if (ctx == null)
                    return;

                if (ctx.TickSec > 0 && Math.Abs(ctx.Health) > float.Epsilon)
                    ApplyTickEffect(agent, ctx);
                else if (ctx.StatModifiers.Count > 0 && ctx.Duration > 0)
                    ApplyStatEffect(agent, ctx);
            }
        }

        private static void ApplyTickEffect(EntityAgent agent, EffectContext ctx)
        {
            if (Math.Abs(ctx.Health) > float.Epsilon)
            {
                if (agent.HasBehavior<EntityBehaviorEffectOverTime>())
                    agent
                        .GetBehavior<EntityBehaviorEffectOverTime>()
                        .Refresh(ctx.Health, ctx.TickSec, ctx.Duration);
                else
                {
                    EntityBehaviorEffectOverTime b = new(agent);
                    agent.AddBehavior(b);
                    b.Setup(ctx.Health, ctx.TickSec, ctx.Duration);
                }
            }
        }

        private static void ApplyStatEffect(EntityAgent agent, EffectContext ctx)
        {
            const string subkey = "weaponcoat";
            foreach (KeyValuePair<string, float> stat in ctx.StatModifiers)
                agent.Stats.Set(stat.Key, subkey, stat.Value, false);

            long agentId = agent.EntityId;
            List<string> effectKeys = [.. ctx.StatModifiers.Keys];
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
