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
            long archerPotionId = ((long)player.Entity.Stats.GetBlended("archerpotionid"));
            if (archerPotionId != 1)
            {
                entity.World.UnregisterCallback(archerPotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the archer potion dissipate.", EnumChatType.Notification);
            }
            long hungerEnhancePotionId = ((long)player.Entity.Stats.GetBlended("hungerenhancepotionid"));
            if (hungerEnhancePotionId != 1)
            {
                entity.World.UnregisterCallback(hungerEnhancePotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the hunger enhance potion dissipate.", EnumChatType.Notification);
            }
            long hungerSupressPotionId = ((long)player.Entity.Stats.GetBlended("hungersupresspotionid"));
            if (hungerSupressPotionId != 1)
            {
                entity.World.UnregisterCallback(hungerSupressPotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the hunger supress potion dissipate.", EnumChatType.Notification);
            }
            long meleePotionId = ((long)player.Entity.Stats.GetBlended("meleepotionid"));
            if (meleePotionId != 1)
            {
                entity.World.UnregisterCallback(meleePotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the melee potion dissipate.", EnumChatType.Notification);
            }
            long poisonPotionId = ((long)player.Entity.Stats.GetBlended("poisonpotionid"));
            if (poisonPotionId != 1)
            {
                entity.World.UnregisterGameTickListener(poisonPotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the poison potion dissipate.", EnumChatType.Notification);
            }
            long regenPotionId = ((long)player.Entity.Stats.GetBlended("regenpotionid"));
            if (regenPotionId != 1)
            {
                entity.World.UnregisterGameTickListener(regenPotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the regen potion dissipate.", EnumChatType.Notification);
            }
            long miningPotionId = ((long)player.Entity.Stats.GetBlended("miningpotionid"));
            if (miningPotionId != 1)
            {
                entity.World.UnregisterCallback(miningPotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the mining potion dissipate.", EnumChatType.Notification);
            }
            long speedPotionId = ((long)player.Entity.Stats.GetBlended("speedpotionid"));
            if (speedPotionId != 1)
            {
                entity.World.UnregisterCallback(speedPotionId - 1);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the speed potion dissipate.", EnumChatType.Notification);
            }
            long healingEffectPotionId = ((long)player.Entity.Stats.GetBlended("healingeffectpotionid"));
            if (healingEffectPotionId != 1)
            {
                entity.World.UnregisterCallback(healingEffectPotionId - 1);
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
            entity.Stats.Set("healingeffectpotionid", "potionmod", 0, false);
            entity.Stats.Set("regenpotionid", "potionmod", 0, false);
            entity.Stats.Set("poisonpotionid", "potionmod", 0, false);
            entity.Stats.Set("archerpotionid", "potionmod", 0, false);
            entity.Stats.Set("hungerenhancepotionid", "potionmod", 0, false);
            entity.Stats.Set("hungersupresspotionid", "potionmod", 0, false);
            entity.Stats.Set("meleepotionid", "potionmod", 0, false);
            entity.Stats.Set("accuracypotionid", "potionmod", 0, false);
            entity.Stats.Set("miningpotionid", "potionmod", 0, false);
            entity.Stats.Set("speedpotionid", "potionmod", 0, false);

            base.OnEntityDeath(damageSourceForDeath);
        }
    }
}