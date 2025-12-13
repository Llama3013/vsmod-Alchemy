using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class TempEffect()
    {
        //Why didn't I make a contstructor?
        private EntityPlayer effectedEntity;

        private const string effectCode = "potionmod";
        private string effectId;
        private int tickCnt = 0;
        private PotionContext potionContext;

        /// <summary>
        /// This needs to be called to give the entity the new stats and to give setTempStats and resetTempStats the variables it needs.
        /// </summary>
        /// <param name="entity"> The entity that will have their stats changed </param>
        /// <param name="effectList"> A dictionary filled with the stat to be changed and the amount to add/remove </param>
        /// <param name="duration"> The amount of time in seconds that the stat will be changed for. </param>
        /// <param name="id"> The id for the RegisterCallback which is saved to WatchedAttributes </param>
        public void TempEntityStats(
            EntityPlayer entity,
            string id,
            PotionContext newPotionContext
        )
        {
            effectedEntity = entity;
            effectId = id;
            potionContext = newPotionContext;
            SetTempStats(potionContext.EffectList);
            RecieveDamage();
            long effectIdCallback = effectedEntity.World.RegisterCallback(Reset, potionContext.Duration * 1000);
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
            string id,
            PotionContext newPotionContext
        )
        {
            effectedEntity = entity;
            effectId = id;
            potionContext = newPotionContext;
            SetTempStats(potionContext.EffectList);
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
                    potionContext.Health = stat.Value;
                    RecieveDamage();
                }
                else
                {
                    effectedEntity.Stats.Set(stat.Key, effectCode, stat.Value, false);
                }
            }
        }

        public void OnEffectTick(float dt)
        {
            tickCnt++;
            if (potionContext.TickSec != 0)
            {
                if (tickCnt % potionContext.TickSec == 0)
                {
                    RecieveDamage();
                }
                if (tickCnt >= potionContext.Duration)
                {
                    long effectIdGametick = effectedEntity.WatchedAttributes.GetLong(effectId);
                    effectedEntity.World.UnregisterGameTickListener(effectIdGametick);
                    Reset(effectIdGametick);
                }
            }
        }

        private void RecieveDamage()
        {
            if (Math.Abs(potionContext.Health) > float.Epsilon)
            {
                float healeffectivnessArmour;
                if (potionContext.IgnoreArmour)
                {
                    ITreeAttribute statsAttr = effectedEntity.WatchedAttributes.GetTreeAttribute(
                        "stats"
                    );
                    ITreeAttribute healingAttr = statsAttr?.GetTreeAttribute("healingeffectivness");
                    if (statsAttr != null && healingAttr != null)
                    {
                        healeffectivnessArmour = healingAttr.GetFloat("wearablemod");
                    }
                    else
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
                if (Math.Abs(healeffectivnessArmour) > float.Epsilon)
                    effectedEntity.Stats.Set("healingeffectivness", "wearablemod", 0, false);
                effectedEntity.ReceiveDamage(
                    new DamageSource()
                    {
                        Source = EnumDamageSource.Internal,
                        Type = potionContext.Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                    },
                    Math.Abs(potionContext.Health)
                );
                if (Math.Abs(healeffectivnessArmour) > float.Epsilon)
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
            foreach (string statKey in potionContext.EffectList.Select(stat => stat.Key))
            {
                effectedEntity.Stats.Remove(statKey, effectCode);
                if (statKey == "maxhealthExtraPoints")
                    effectedEntity.GetBehavior<EntityBehaviorHealth>().MarkDirty();
            }
            if (effectId.Contains("tickpotionid"))
            {
                long effectIdGametick = effectedEntity.WatchedAttributes.GetLong(effectId);
                effectedEntity.World.UnregisterGameTickListener(effectIdGametick);
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