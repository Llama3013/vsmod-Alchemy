using HarmonyLib;
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
        //Why didn't I make a contstructor?
        private EntityPlayer effectedEntity;

        private Dictionary<string, float> effectedList;

        private const string effectCode = "potionmod";
        private string effectId;

        private int effectDuration,
            effectTickSec,
            tickCnt = 0;

        private float effectHealth = 0;
        private bool ignoreArmourFlag;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        /// <param name="duration"> The amount of time in seconds that the stat will be changed for. </param>
        /// <param name="id"> The id for the RegisterCallback which is saved to WatchedAttributes </param>
        public void TempEntityStats(
            EntityPlayer entity,
            Dictionary<string, float> effectList,
            int duration,
            string id,
            bool ignoreArmour
        )
        {
            effectedEntity = entity;
            effectedList = effectList;
            effectId = id;
            ignoreArmourFlag = ignoreArmour;
            SetTempStats(effectedList);
            RecieveDamage();
            long effectIdCallback = effectedEntity.World.RegisterCallback(Reset, duration * 1000);
            effectedEntity.WatchedAttributes.SetLong(effectId, effectIdCallback);
        }

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
            int duration,
            string id,
            int tickSec,
            float health,
            bool ignoreArmour
        )
        {
            effectedEntity = entity;
            effectedList = effectList;
            effectId = id;
            effectDuration = duration;
            effectTickSec = tickSec;
            effectHealth = health;
            ignoreArmourFlag = ignoreArmour;
            SetTempStats(effectedList);
            long effectIdGametick = entity.World.RegisterGameTickListener(OnEffectTick, 1000);
            effectedEntity.WatchedAttributes.SetLong(effectId, effectIdGametick);
        }

        /// <summary>
        /// Iterates through the provided effect dictionary and sets every stat provided
        /// </summary>
        public void SetTempStats(Dictionary<string, float> newEffectedList)
        {
            foreach (KeyValuePair<string, float> stat in newEffectedList)
            {
                if (stat.Key == "maxhealthExtraPoints")
                {
                    effectedEntity.Stats.Set(
                        stat.Key,
                        effectCode,
                        (14f + effectedEntity.Stats.GetBlended("maxhealthExtraPoints"))
                            * stat.Value,
                        false
                    );
                    EntityBehaviorHealth ebh = effectedEntity.GetBehavior<EntityBehaviorHealth>();
                    ebh.MarkDirty();
                }
                else if (stat.Key == "health")
                {
                    effectHealth = stat.Value;
                    RecieveDamage();
                } else 
                {
                    effectedEntity.Stats.Set(stat.Key, effectCode, stat.Value, false);
                }
            }
        }

        public void OnEffectTick(float dt)
        {
            tickCnt++;
            if (effectTickSec != 0)
            {
                if (tickCnt % effectTickSec == 0)
                {
                    RecieveDamage();
                }
                if (tickCnt >= effectDuration)
                {
                    long effectIdGametick = effectedEntity.WatchedAttributes.GetLong(effectId);
                    effectedEntity.World.UnregisterGameTickListener(effectIdGametick);
                    Reset(effectIdGametick);
                }
            }
        }

        private void RecieveDamage()
        {
            if (effectHealth != 0)
            {
                float healeffectivnessArmour;
                if (ignoreArmourFlag)
                {
                    try
                    {
                        healeffectivnessArmour = effectedEntity.WatchedAttributes
                            .GetTreeAttribute("stats")
                            .GetTreeAttribute("healingeffectivness")
                            .GetFloat("wearablemod");
                    }
                    catch (NullReferenceException)
                    {
                        effectedEntity.Api.Logger.Error(
                            "Couldn't gather armour heal effectivness for healing potion. Skipping wearable healeffectivness wipe..."
                        );
                        healeffectivnessArmour = 0;
                    }
                }
                else
                {
                    healeffectivnessArmour = 0;
                }
                if (healeffectivnessArmour != 0)
                    effectedEntity.Stats.Set(
                        "healingeffectivness",
                        "wearablemod",
                        0,
                        false
                    );
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
                if (healeffectivnessArmour != 0)
                    effectedEntity.Stats.Set(
                        "healingeffectivness",
                        "wearablemod",
                        healeffectivnessArmour,
                        false
                    );
            }

        }

        /// <summary>
        /// Iterates through the provided effect dictionary and resets every stat provided (only resets effects that has the same effectCode)
        /// </summary>
        public void Reset(float _dt)
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
            effectId = null;
            if (
                effectedEntity.World.PlayerByUid(effectedEntity.PlayerUID)
                is IServerPlayer serverPlayer
            )
            {
                serverPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get("alchemy:effect-lose"),
                    EnumChatType.Notification
                );
            }
            effectedEntity = null;
        }

        public static void ResetAllTempStats(EntityPlayer entity)
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
                            entity.World.UnregisterCallback(potionListenerId);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.Api.Logger.Error("Error on potion remove");
                        entity.Api.Logger.Error(watch);
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
                            entity.World.UnregisterGameTickListener(potionListenerId);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.Api.Logger.Error("Error on remove potion tick");
                        entity.Api.Logger.Error(watch);
                        entity.WatchedAttributes.RemoveAttribute(watch);
                    }
                }
            }
        }

        public static bool ResetAllListeners(
            EntityPlayer entity,
            string callbackCode,
            string listenerCode
        )
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