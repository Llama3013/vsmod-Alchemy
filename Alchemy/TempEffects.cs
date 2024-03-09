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
        private EntityPlayer effectedEntity;

        private Dictionary<string, float> effectedList;

        private string effectCode;

        private string effectId;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        /// <param name="code"> The identity of what is changing the stat. If "code" is present on same stat then the latest Set will override it. </param>
        /// <param name="duration"> The amount of time in seconds that the stat will be changed for. </param>
        /// <param name="id"> The id for the RegisterCallback which is saved to WatchedAttributes </param>
        public void TempEntityStats(
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
                SetTempStats();
            }
            long effectIdCallback = effectedEntity.World.RegisterCallback(
                ResetTempStats,
                duration * 1000
            );
            effectedEntity.WatchedAttributes.SetLong(effectId, effectIdCallback);
        }

        private int effectDuration;

        private int effectTickSec;

        private float effectHealth = 0;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        /// <param name="code"> The identity of what is changing the stat. If "code" is present on same stat then the latest Set will override it. </param>
        /// <param name="duration"> The amount of time in seconds that the stat will be changed for. </param>
        /// <param name="id"> The id for the RegisterCallback which is saved to WatchedAttributes </param>
        public void TempTickEntityStats(
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
                SetTempStats();
            }
            long effectIdGametick = entity.World.RegisterGameTickListener(OnEffectTick, 1000);
            effectedEntity.WatchedAttributes.SetLong(effectId, effectIdGametick);
        }

        /// <summary>
        /// Iterates through the provided effect dictionary and sets every stat provided
        /// </summary>
        public void SetTempStats()
        {
            if (effectedList.TryGetValue("maxhealthExtraPoints", out float value))
            {
                effectedList["maxhealthExtraPoints"] =
                    (14f + effectedEntity.Stats.GetBlended("maxhealthExtraPoints"))
                    * value;
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
        public void ResetTempStats(float dt)
        {
            Reset();
        }

        private int tickCnt = 0;

        public void OnEffectTick(float dt)
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
                    Reset();
                }
            }
        }

        public void Reset()
        {
            foreach (KeyValuePair<string, float> stat in effectedList)
            {
                effectedEntity.Stats.Remove(stat.Key, effectCode);
                if (stat.Key == "maxhealthExtraPoints")
                    effectedEntity.GetBehavior<EntityBehaviorHealth>().MarkDirty();
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
                effectedEntity.World.PlayerByUid(effectedEntity.PlayerUID)
                as IServerPlayer
            );
            player.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                "You feel the effects of the potion disapate",
                EnumChatType.Notification
            );
        }

        public static void ResetAllTempStats(EntityPlayer entity, string effectCode)
        {
            foreach (KeyValuePair<string, EntityFloatStats> stats in entity.Stats)
            {
                entity.Stats.Remove(stats.Key, effectCode);
            }
            EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
            ebh.MarkDirty();
        }

        public static void ResetAllAttrListeners(
            EntityPlayer entity,
            string callbackCode,
            string listenerCode
        )
        {
            foreach (string watch in entity.WatchedAttributes.Keys)
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

        public static bool ResetAllListeners(EntityPlayer entity, string callbackCode, string listenerCode)
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