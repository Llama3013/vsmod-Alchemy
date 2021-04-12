using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.API.Config;

namespace Alchemy
{
    public class PotionFixBehavior : EntityBehavior
    {
        private ModConfig config;

        public PotionFixBehavior(Entity entity, ModConfig config) : base(entity)
        {
            this.config = config;
        }

        public override string PropertyName()
        {
            return "PotionFixBehavior";
        }

        private IServerPlayer GetIServerPlayer()
        {
            return this.entity.World.PlayerByUid((this.entity as EntityPlayer).PlayerUID) as IServerPlayer;
        }

        /* This override is to add the behavior to the player of when they die they also reset all of their potion effects */
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            IServerPlayer player = GetIServerPlayer();
            /* Note that the default value of GetBlended is 1 and as a float. I have converted the listener potion ids value to a long so that it works with
            RegisterCallback, UnregisterCallback, RegisterGameTickListener and UnregisterGameTickListener. I think this could cause a problem
            if the listener id is too big of a number for float but so far the listeners have only gone to about a thousand with my testing.
            I will create a better system soon. */
            long archerPotionId = entity.WatchedAttributes.GetLong("archerpotionid");
            if (archerPotionId != 0)
            {
                entity.World.UnregisterCallback(archerPotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the archer potion dissipate.", EnumChatType.Notification);
            }
            long hungerEnhancePotionId = entity.WatchedAttributes.GetLong("hungeranhancepotionid");
            if (hungerEnhancePotionId != 0)
            {
                entity.World.UnregisterCallback(hungerEnhancePotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the hunger enhance potion dissipate.", EnumChatType.Notification);
            }
            long hungerSupressPotionId = entity.WatchedAttributes.GetLong("hungersupresspotionid");
            if (hungerSupressPotionId != 0)
            {
                entity.World.UnregisterCallback(hungerSupressPotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the hunger supress potion dissipate.", EnumChatType.Notification);
            }
            long meleePotionId = entity.WatchedAttributes.GetLong("meleepotionid");
            if (meleePotionId != 0)
            {
                entity.World.UnregisterCallback(meleePotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the melee potion dissipate.", EnumChatType.Notification);
            }
            long poisonPotionId = entity.WatchedAttributes.GetLong("poisonpotionid");
            if (poisonPotionId != 0)
            {
                entity.World.UnregisterGameTickListener(poisonPotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the poison potion dissipate.", EnumChatType.Notification);
            }
            long regenPotionId = entity.WatchedAttributes.GetLong("regenpotionid");
            if (regenPotionId != 0)
            {
                entity.World.UnregisterGameTickListener(regenPotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the regen potion dissipate.", EnumChatType.Notification);
            }
            long miningPotionId = entity.WatchedAttributes.GetLong("miningpotionid");
            if (miningPotionId != 0)
            {
                entity.World.UnregisterCallback(miningPotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the mining potion dissipate.", EnumChatType.Notification);
            }
            long speedPotionId = entity.WatchedAttributes.GetLong("speedpotionid");
            if (speedPotionId != 0)
            {
                entity.World.UnregisterCallback(speedPotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the speed potion dissipate.", EnumChatType.Notification);
            }
            long healingEffectPotionId = entity.WatchedAttributes.GetLong("healingeffectpotionid");
            if (healingEffectPotionId != 0)
            {
                entity.World.UnregisterCallback(healingEffectPotionId);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the speed potion dissipate.", EnumChatType.Notification);
            }

            entity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
            entity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
            entity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
            entity.Stats.Set("hungerrate", "potionmod", 0, false);
            entity.Stats.Set("meleeWeaponsDamage", "potionmod", 0, false);
            entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
            entity.Stats.Set("walkspeed", "potionmod", 0, false);
            entity.Stats.Set("healingeffectivness", "potionmod", 0, false);
            entity.WatchedAttributes.SetLong("healingeffectpotionid", 0);
            entity.WatchedAttributes.SetLong("regenpotionid", 0);
            entity.WatchedAttributes.SetLong("poisonpotionid", 0);
            entity.WatchedAttributes.SetLong("archerpotionid", 0);
            entity.WatchedAttributes.SetLong("hungerenhancepotionid", 0);
            entity.WatchedAttributes.SetLong("hungersupresspotionid", 0);
            entity.WatchedAttributes.SetLong("meleepotionid", 0);
            entity.WatchedAttributes.SetLong("accuracypotionid", 0);
            entity.WatchedAttributes.SetLong("miningpotionid", 0);
            entity.WatchedAttributes.SetLong("speedpotionid", 0);

            base.OnEntityDeath(damageSourceForDeath);
        }
    }
}