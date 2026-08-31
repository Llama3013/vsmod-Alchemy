using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace EffectLib
{
    /// <summary>Controls how <see cref="JsonEffectDefinition.Scan"/> reads effects off collectibles.</summary>
    public sealed class JsonEffectScanOptions
    {
        /// <summary>Collectible attribute holding the effect definition, e.g. <c>"effectinfo"</c>.</summary>
        public string AttributeKey { get; set; } = "effectinfo";

        /// <summary>Field inside that attribute holding the effect id.</summary>
        public string IdField { get; set; } = "effectId";

        /// <summary>Mod domain recorded with each effect, used for its lang keys and HUD icon.</summary>
        public string Domain { get; set; } = EffectRegistry.DefaultDomain;

        /// <summary>
        /// Ids already owned by code or config. A JSON definition is skipped for these, so a
        /// content pack cannot silently take over an effect whose values come from a config.
        /// </summary>
        public System.Func<string, bool> IsReserved { get; set; }

        /// <summary>
        /// Extra id validation. Return false to reject an id with a warning. Optional.
        /// </summary>
        public System.Func<string, bool> IsValidId { get; set; }

        /// <summary>Warning text shown when <see cref="IsValidId"/> rejects an id.</summary>
        public string InvalidIdMessage { get; set; } = "it is not a valid effect id";

        /// <summary>
        /// Called for each effect accepted, so the caller can read its own extra fields off
        /// the same definition.
        /// </summary>
        public System.Action<string, JsonObject> OnRegistered { get; set; }
    }

    /// <summary>
    /// Reads effects declared entirely in JSON, so a content-only mod can add effects without
    /// shipping code. The schema is the field set of <see cref="EffectContext"/>.
    /// </summary>
    public static class JsonEffectDefinition
    {
        /// <summary>
        /// Every field a definition may set. Most are read by <see cref="Apply"/> into an
        /// <see cref="EffectContext"/>; <c>channels</c> and <c>exclusivityGroup</c> are the two
        /// exceptions - they describe the *registration*, not one application, so
        /// <see cref="RegisterFrom"/> reads them directly into <see cref="EffectRegistry"/>
        /// instead.
        /// </summary>
        public static readonly string[] Fields =
        [
            "duration",
            "resetsEffects",
            "resetDomains",
            "resetEffectIds",
            "repeatSec",
            "stats",
            "health",
            "tickSec",
            "damageType",
            "glowStrength",
            "temporalStabilityGain",
            "retainedNutrition",
            "sizeChange",
            "sizeMinHeight",
            "sizeMaxHeight",
            "fallDamageReduction",
            "waterBreathe",
            "coldResist",
            "canClimbAnywhere",
            "canFly",
            "respawn",
            "reshape",
            "knockbackResistance",
            "noFallDamage",
            "disableClimbing",
            "climbTouchDistance",
            "weight",
            "noGravity",
            "channels",
            "exclusivityGroup",
        ];

        /// <summary>True when <paramref name="def"/> sets any effect value at all.</summary>
        public static bool HasEffectData(JsonObject def)
        {
            if (def == null)
                return false;

            foreach (string key in Fields)
            {
                if (def.KeyExists(key))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Fills <paramref name="ctx"/> from a JSON definition. Public so a mod can reuse the
        /// schema from its own attribute layout instead of going through <see cref="Scan"/>.
        /// </summary>
        public static void Apply(EffectContext ctx, JsonObject def)
        {
            if (ctx == null || def == null)
                return;

            ctx.ResetsEffects = def["resetsEffects"].AsBool();
            ctx.Duration = def["duration"].AsInt();
            ctx.RepeatSec = def["repeatSec"].AsFloat();

            string[] resetDomains = def["resetDomains"].AsArray<string>(null);
            if (resetDomains != null)
                ctx.ResetDomains.AddRange(resetDomains.Where(d => !string.IsNullOrWhiteSpace(d)));

            string[] resetEffectIds = def["resetEffectIds"].AsArray<string>(null);
            if (resetEffectIds != null)
                ctx.ResetEffectIds.AddRange(
                    resetEffectIds.Where(id => !string.IsNullOrWhiteSpace(id))
                );

            Dictionary<string, float> stats = def["stats"]
                .AsObject<Dictionary<string, float>>(null);
            if (stats != null)
            {
                foreach (KeyValuePair<string, float> stat in stats)
                    ctx.AddStat(stat.Key, stat.Value);
            }

            float health = def["health"].AsFloat();
            if (Math.Abs(health) > float.Epsilon)
            {
                ctx.SetHealth(health);
                ctx.TickSec = def["tickSec"].AsFloat();

                string damageType = def["damageType"].AsString();
                if (
                    !string.IsNullOrWhiteSpace(damageType)
                    && Enum.TryParse(damageType, true, out EnumDamageType parsed)
                )
                    ctx.DamageType = parsed;
            }

            ctx.GlowStrength = def["glowStrength"].AsInt();
            ctx.TemporalStabilityGain = def["temporalStabilityGain"].AsFloat();
            ctx.RetainedNutrition = def["retainedNutrition"].AsFloat();
            ctx.SizeChange = def["sizeChange"].AsFloat();
            ctx.SizeMinHeight = def["sizeMinHeight"].AsFloat();
            ctx.SizeMaxHeight = def["sizeMaxHeight"].AsFloat();
            ctx.FallDamageReduction = def["fallDamageReduction"].AsFloat();

            ctx.WaterBreathe = def["waterBreathe"].AsBool();
            ctx.ColdResist = def["coldResist"].AsBool();
            ctx.CanClimbAnywhere = def["canClimbAnywhere"].AsBool();
            ctx.CanFly = def["canFly"].AsBool();
            ctx.Respawn = def["respawn"].AsBool();
            ctx.Reshape = def["reshape"].AsBool();

            ctx.KnockbackResistance = def["knockbackResistance"].AsFloat();
            ctx.NoFallDamage = def["noFallDamage"].AsBool();
            ctx.DisableClimbing = def["disableClimbing"].AsBool();
            ctx.ClimbTouchDistance = def["climbTouchDistance"].AsFloat();
            ctx.Weight = def["weight"].AsFloat();
            ctx.NoGravity = def["noGravity"].AsBool();
        }

        /// <summary>
        /// Registers <paramref name="effectId"/> from a JSON definition in one call - the
        /// builder (<see cref="Apply"/>), plus the registration-level fields <c>channels</c> and
        /// <c>exclusivityGroup</c> that don't belong on <see cref="EffectContext"/>. Every
        /// self-registering behavior (<c>CollectibleBehaviorEffectItem</c> and its siblings) and
        /// <see cref="Scan"/> itself all funnel through this, so channels/groups are captured
        /// the same way regardless of which one found the definition.
        /// </summary>
        public static void RegisterFrom(
            string effectId,
            string domain,
            JsonObject def,
            AssetLocation iconSource = null
        )
        {
            JsonObject definition = def;
            string[] channels = def["channels"].AsArray<string>(null);
            string exclusivityGroup = def["exclusivityGroup"].AsString();

            EffectRegistry.Register(
                effectId,
                ctx => Apply(ctx, definition),
                domain,
                iconSource,
                channels,
                exclusivityGroup
            );
        }

        /// <summary>
        /// Scans every loaded collectible for <see cref="JsonEffectScanOptions.AttributeKey"/>
        /// and registers an effect for each distinct id found. Returns how many were registered.
        /// </summary>
        public static int Scan(ICoreAPI api, JsonEffectScanOptions options)
        {
            if (api?.World?.Collectibles == null || options == null)
                return 0;

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                JsonObject def = obj?.Attributes?[options.AttributeKey];
                if (def?.Exists != true)
                    continue;

                string effectId = def[options.IdField].AsString()?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(effectId))
                {
                    api.Logger.Warning(
                        "[EffectLib] {0} has a {1} block with no {2}, ignoring it.",
                        obj.Code,
                        options.AttributeKey,
                        options.IdField
                    );
                    continue;
                }

                if (options.IsReserved?.Invoke(effectId) == true)
                {
                    if (HasEffectData(def))
                        api.Logger.Warning(
                            "[EffectLib] {0} defines values for '{1}', which is driven by code or "
                                + "config. They are ignored - retune it there instead.",
                            obj.Code,
                            effectId
                        );
                    continue;
                }

                if (!seen.Add(effectId))
                    continue;

                if (options.IsValidId?.Invoke(effectId) == false)
                {
                    api.Logger.Warning(
                        "[EffectLib] {0} names the effect '{1}', but {2}. Ignoring it.",
                        obj.Code,
                        effectId,
                        options.InvalidIdMessage
                    );
                    seen.Remove(effectId);
                    continue;
                }

                WarnOnDamageTypeMismatch(api, obj, def);

                RegisterFrom(effectId, options.Domain, def, obj.Code);

                options.OnRegistered?.Invoke(effectId, def);
            }

            return seen.Count;
        }

        // A positive health with a damaging type heals nothing and a negative health with Heal
        // damages instead, both of which look like the mod is broken rather than the JSON.
        private static void WarnOnDamageTypeMismatch(
            ICoreAPI api,
            CollectibleObject obj,
            JsonObject def
        )
        {
            float health = def["health"].AsFloat();
            string damageType = def["damageType"].AsString();

            if (
                Math.Abs(health) <= float.Epsilon
                || string.IsNullOrWhiteSpace(damageType)
                || !Enum.TryParse(damageType, true, out EnumDamageType parsed)
            )
                return;

            bool healing = parsed == EnumDamageType.Heal;
            if (healing == health > 0)
                return;

            api.Logger.Warning(
                "[EffectLib] {0} declares health {1} with damageType '{2}', which will {3} instead of {4}. "
                    + "Use a positive health with 'Heal', or a negative health with a damaging type.",
                obj.Code,
                health,
                parsed,
                healing ? "heal" : "damage",
                healing ? "damage" : "heal"
            );
        }
    }
}
