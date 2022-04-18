using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Alchemy
{
    public class TempEffect
    {
        EntityPlayer effectedEntity;
        Dictionary<string, float> effectedList;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        public void tempEntityStats(EntityPlayer entity, Dictionary<string, float> effectList)
        {
            effectedEntity = entity;
            effectedList = effectList;
            setTempStats();
            long effectIdCallback = effectedEntity.World.RegisterCallback(resetTempStats, (int)effectedList["duration"] * 1000);
            effectedEntity.WatchedAttributes.SetLong("potionid", effectIdCallback);
        }

        int effectDuration;
        int effectTickSec;
        float effectHealth = 0;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        public void tempTickEntityStats(EntityPlayer entity, Dictionary<string, float> effectList, int tickSec, float health)
        {
            effectedEntity = entity;
            effectedList = effectList;
            effectDuration = (int)effectedList["duration"];
            effectTickSec = tickSec;
            effectHealth = health;
            setTempStats();
            long effectIdGametick = entity.World.RegisterGameTickListener(onEffectTick, 1000);
            effectedEntity.WatchedAttributes.SetLong("potionid", effectIdGametick);
        }

        /// <summary>
        /// Iterates through the provided effect dictionary and sets every stat provided
        /// </summary>
        public void setTempStats()
        {
            //This calculates a correct percentage of max health to increase
            if (effectedList.ContainsKey("maxhealthExtraPoints"))
            {
                effectedList["maxhealthExtraPoints"] = (14f + effectedEntity.Stats.GetBlended("maxhealthExtraPoints")) * effectedList["maxhealthExtraPoints"];
            }
            foreach (KeyValuePair<string, float> stat in effectedList)
            {
                switch (stat.Key)
                {
                    case "glow":
                        effectedEntity.WatchedAttributes.SetBool("glow", true);
                        break;
                    case "recall":
                        break;
                    case "duration":
                        break;
                    default:
                        effectedEntity.Stats.Set(stat.Key, "potionmod", stat.Value, false);
                        break;
                }
            }
            //This is required everytime max health is changes
            if (effectedList.ContainsKey("maxhealthExtraPoints"))
            {
                EntityBehaviorHealth ebh = effectedEntity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
            }
        }

        /// <summary>
        /// Iterates through the provided effect dictionary and resets every stat provided (only resets effects that has the same effectCode)
        /// </summary>
        public void resetTempStats(float dt)
        {
            reset(effectedEntity, true);
        }

        int tickCnt = 0;
        public void onEffectTick(float dt)
        {
            tickCnt++;
            if (effectTickSec != 0)
            {
                if (tickCnt % effectTickSec == 0)
                {
                    if (effectHealth != 0)
                    {
                        //api.Logger.Debug("Potion tickSec: {0}", attrClass.ticksec);
                        effectedEntity.ReceiveDamage(new DamageSource()
                        {
                            Source = EnumDamageSource.Internal,
                            Type = effectHealth > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                        }, Math.Abs(effectHealth));
                    }
                }
                if (tickCnt >= effectDuration)
                {
                    long effectIdGametick = effectedEntity.WatchedAttributes.GetLong("potionid");
                    effectedEntity.World.UnregisterGameTickListener(effectIdGametick);
                    reset(effectedEntity, true);
                }
            }
        }

        public void reset(EntityPlayer entity, bool message)
        {
            foreach (var stats in entity.Stats)
            {
                entity.Stats.Remove(stats.Key, "potionmod");
            }
            EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
            ebh.MarkDirty();
            if (entity.WatchedAttributes.HasAttribute("glow")) entity.WatchedAttributes.RemoveAttribute("glow");
            if (entity.WatchedAttributes.HasAttribute("potionid"))
            {
                long effectIdGametick = entity.WatchedAttributes.GetLong("potionid");
                entity.World.UnregisterGameTickListener(effectIdGametick);
                effectDuration = 0;
                effectHealth = 0;
                effectTickSec = 0;
                entity.WatchedAttributes.RemoveAttribute("potionid");
            }
            if (message)
            {
                IServerPlayer player = (entity.World.PlayerByUid((entity as EntityPlayer).PlayerUID) as IServerPlayer);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the potion disapate", EnumChatType.Notification);
            }
        }
    }
}