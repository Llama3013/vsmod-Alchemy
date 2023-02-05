using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class TempEffect
    {
        EntityPlayer effectedEntity;

        Dictionary<string, float> effectedList;

        string effectCode;

        string effectId;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        /// <param name="code"> The identity of what is changing the stat. If "code" is present on same stat then the latest Set will override it. </param>
        /// <param name="duration"> The amount of time in seconds that the stat will be changed for. </param>
        /// <param name="id"> The id for the RegisterCallback which is saved to WatchedAttributes </param>
        public void tempEntityStats(
            EntityPlayer entity,
            Dictionary<string, float> effectList,
            string code,
            int duration,
            string id
        )
        {
            effectedEntity = entity;
            effectedList = effectList;
            effectCode = code;
            effectId = id;
            if (effectedList.Count >= 1)
            {
                setTempStats();
            }
            long effectIdCallback = effectedEntity.World.RegisterCallback(
                resetTempStats,
                duration * 1000
            );
            effectedEntity.WatchedAttributes.SetLong(effectId, effectIdCallback);
        }

        int effectDuration;

        int effectTickSec;

        float effectHealth = 0;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        /// <param name="code"> The identity of what is changing the stat. If "code" is present on same stat then the latest Set will override it. </param>
        /// <param name="duration"> The amount of time in seconds that the stat will be changed for. </param>
        /// <param name="id"> The id for the RegisterCallback which is saved to WatchedAttributes </param>
        public void tempTickEntityStats(
            EntityPlayer entity,
            Dictionary<string, float> effectList,
            string code,
            int duration,
            string id,
            int tickSec,
            float health
        )
        {
            effectedEntity = entity;
            effectedList = effectList;
            effectCode = code;
            effectId = id;
            effectDuration = duration;
            effectTickSec = tickSec;
            effectHealth = health;
            if (effectedList.Count >= 1)
            {
                setTempStats();
            }
            long effectIdGametick = entity.World.RegisterGameTickListener(onEffectTick, 1000);
            effectedEntity.WatchedAttributes.SetLong(effectId, effectIdGametick);
        }

        /// <summary>
        /// Iterates through the provided effect dictionary and sets every stat provided
        /// </summary>
        public void setTempStats()
        {
            if (effectedList.ContainsKey("maxhealthExtraPoints"))
            {
                effectedEntity.World.Api.Logger.Debug(
                    "blendedhealth {0}",
                    effectedEntity.Stats.GetBlended("maxhealthExtraPoints")
                );
                effectedEntity.World.Api.Logger.Debug(
                    "maxhealthExtraPoints {0}",
                    effectedList["maxhealthExtraPoints"]
                );
                effectedList["maxhealthExtraPoints"] =
                    (14f + effectedEntity.Stats.GetBlended("maxhealthExtraPoints"))
                    * effectedList["maxhealthExtraPoints"];
            }
            foreach (KeyValuePair<string, float> stat in effectedList)
            {
                effectedEntity.Stats.Set(stat.Key, effectCode, stat.Value, false);
            }
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
            reset();
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
                        effectedEntity.ReceiveDamage(
                            new DamageSource()
                            {
                                Source = EnumDamageSource.Internal,
                                Type =
                                    effectHealth > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                            },
                            Math.Abs(effectHealth)
                        );
                    }
                }
                if (tickCnt >= effectDuration)
                {
                    long effectIdGametick = effectedEntity.WatchedAttributes.GetLong(effectId);
                    effectedEntity.World.UnregisterGameTickListener(effectIdGametick);
                    reset();
                }
            }
        }

        public void reset()
        {
            foreach (KeyValuePair<string, float> stat in effectedList)
            {
                effectedEntity.Stats.Remove(stat.Key, effectCode);
            }
            if (effectedList.ContainsKey("maxhealthExtraPoints"))
            {
                EntityBehaviorHealth ebh = effectedEntity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
            }
            if (effectId.Contains("tickpotionid"))
            {
                long effectIdGametick = effectedEntity.WatchedAttributes.GetLong(effectId);
                effectedEntity.World.UnregisterGameTickListener(effectIdGametick);
                effectDuration = 0;
                effectHealth = 0;
                effectTickSec = 0;
            }
            effectedEntity.WatchedAttributes.RemoveAttribute(effectId);

            IServerPlayer player = (
                effectedEntity.World.PlayerByUid((effectedEntity as EntityPlayer).PlayerUID)
                as IServerPlayer
            );
            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "You feel the effects of the potion disapate",
                EnumChatType.Notification
            );
        }

        public void resetAllTempStats(EntityPlayer entity, string effectCode)
        {
            foreach (var stats in entity.Stats)
            {
                entity.Stats.Remove(stats.Key, effectCode);
            }
            EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
            ebh.MarkDirty();
        }

        public void resetAllAttrListeners(
            EntityPlayer entity,
            string callbackCode,
            string listenerCode
        )
        {
            foreach (var watch in entity.WatchedAttributes.Keys)
            {
                if (watch.Contains(callbackCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
                else if (watch.Contains(listenerCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
            }
        }

        public bool resetAllListeners(EntityPlayer entity, string callbackCode, string listenerCode)
        {
            bool effectReseted = false;
            foreach (var watch in entity.WatchedAttributes.Keys)
            {
                if (watch.Contains(callbackCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            effectReseted = true;
                            entity.World.UnregisterCallback(potionListenerId);
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
                else if (watch.Contains(listenerCode))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(watch);
                        if (potionListenerId != 0)
                        {
                            effectReseted = true;
                            entity.World.UnregisterGameTickListener(potionListenerId);
                            entity.WatchedAttributes.RemoveAttribute(watch);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
            }
            return effectReseted;
        }
    }
}
