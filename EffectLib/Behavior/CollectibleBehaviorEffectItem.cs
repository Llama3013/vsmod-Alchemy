using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// "Hold to consume, then apply an effect" item behavior. Works with zero code for a
    /// JSON-only mod - add <c>{ "name": "EffectItem" }</c> to a collectible's <c>behaviors</c>
    /// and an <c>effectinfo</c> attribute (same schema as <see cref="JsonEffectDefinition"/>)
    /// and it reads and registers its own effect the moment it loads.
    /// </summary>
    /// <remarks>
    /// A mod with its own delivery rules - strength tiers, liquid containers, exclusivity
    /// groups, config-driven consume time - subclasses this and overrides the extension points
    /// below rather than reimplementing the hold-interact flow, the same way a game-provided
    /// Vintage Story behavior is meant to be extended. See <c>Alchemy.PotionConsumableBehavior</c>
    /// for a full example.
    /// </remarks>
    public class CollectibleBehaviorEffectItem(CollectibleObject collObj)
        : CollectibleBehavior(collObj)
    {
        // protected: CollectibleBehaviorEffectLiquid resolves the same schema off a liquid
        // container's current content instead of this item's own attribute.
        protected string attributeKey;
        protected string idField;
        private bool consumeOnUse;
        private int durabilityCost;

        // protected: a subclass with its own delivery rules typically wants a different default
        // sound/animation than the generic one Initialize reads (e.g. "drink" instead of "eat").
        protected string animation;
        protected string sound;
        private float consumeTime;

        private string defaultEffectId;
        private IProgressBar progressBarRender;

        /// <summary>The API captured in <see cref="OnLoaded"/>, available to subclasses.</summary>
        protected ICoreAPI Api { get; private set; }

        // Guards against a held-interact-stop firing twice for the same use, which the engine
        // occasionally does. Static and shared across every item using this behavior (and its
        // subclasses) - keyed by entity, not item, which is what the dedupe actually needs.
        private static readonly HashSet<long> resolvedEntities = [];

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            attributeKey = properties["attributeKey"].AsString("effectinfo");
            idField = properties["idField"].AsString("effectId");
            consumeOnUse = properties["consumeOnUse"].AsBool(true);
            durabilityCost = properties["durabilityCost"].AsInt();
            animation = properties["animation"].AsString("eat");
            sound = properties["sound"].AsString("game:sounds/player/eat");
            consumeTime = properties["consumeTime"].AsFloat(1.6f);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Api = api;
            RegisterOwnEffect();
        }

        /// <summary>
        /// Reads this item's own <c>effectinfo</c> attribute and registers it under the item's
        /// own mod domain, so a JSON-only item works with nothing else needed - including
        /// surviving a server restart, since <see cref="EffectRegistry"/> is how a saved effect
        /// gets rebuilt. Override as a no-op if effect ids are registered elsewhere instead (as
        /// <c>Alchemy.PotionConsumableBehavior</c> does via its own code/JSON scan).
        /// </summary>
        protected virtual void RegisterOwnEffect()
        {
            JsonObject def = collObj.Attributes?[attributeKey];
            if (def?.Exists != true)
            {
                Api.Logger.Warning(
                    "[EffectLib] {0} has the EffectItem behavior but no '{1}' attribute, so it "
                        + "will do nothing.",
                    collObj.Code,
                    attributeKey
                );
                return;
            }

            defaultEffectId = def[idField].AsString()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(defaultEffectId))
            {
                Api.Logger.Warning(
                    "[EffectLib] {0}'s '{1}' has no {2}, so it will do nothing.",
                    collObj.Code,
                    attributeKey,
                    idField
                );
                defaultEffectId = null;
                return;
            }

            JsonEffectDefinition.RegisterFrom(defaultEffectId, collObj.Code.Domain, def, collObj.Code);
        }

        // ----- Extension points - override these, not the interact handlers below -----

        /// <summary>
        /// Whether this use should be intercepted at all. Default always intercepts; override
        /// to defer to the vanilla interaction in some cases (Alchemy does this for
        /// shift-clicking a liquid container, which means "fill from water", not "drink").
        /// </summary>
        protected virtual bool ShouldIntercept(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel
        ) => true;

        /// <summary>
        /// Resolves what to grant and at what potency for one use. Default is the id captured
        /// by <see cref="RegisterOwnEffect"/> at potency 1. Override to read a different source
        /// (a liquid container's content, a strength tier read off the item).
        /// <paramref name="byEntity"/> is null when resolving for a tooltip - the game does not
        /// give <see cref="GetHeldItemInfo"/> an entity - so do not dereference it unconditionally.
        /// </summary>
        protected virtual bool TryResolveEffect(
            ItemSlot slot,
            EntityAgent byEntity,
            out string effectId,
            out float potencyMul
        )
        {
            effectId = defaultEffectId;
            potencyMul = 1f;
            return effectId != null;
        }

        /// <summary>Seconds to hold before the effect applies; 0 applies instantly on click.</summary>
        protected virtual float GetConsumeTime(EntityAgent byEntity) => consumeTime;

        /// <summary>Whether there is enough of the source to use, e.g. stack size or litres.</summary>
        protected virtual bool HasEnoughSource(ItemSlot slot) => (slot.Itemstack?.StackSize ?? 0) > 0;

        /// <summary>
        /// A lang key naming why this use should be refused, or null to allow it. Checked after
        /// <see cref="EffectContext"/> is built, so overrides may inspect it (e.g. skip
        /// exclusivity checks when the effect purges first). Default only checks
        /// <see cref="HasEnoughSource"/>.
        /// </summary>
        protected virtual string GetBlockReason(
            ItemSlot slot,
            EntityAgent byEntity,
            string effectId,
            EffectContext ctx
        ) => HasEnoughSource(slot) ? null : "effectlib:not-enough-source";

        /// <summary>
        /// Makes the effect happen. Default applies it through the player's own
        /// <see cref="EffectManager"/>. Override to layer on extra steps first (a purge, a
        /// size-change gate, side effects) the way <c>TryProcessPotionEffects</c> does.
        /// </summary>
        protected virtual bool ApplyEffect(
            ItemSlot slot,
            EntityAgent byEntity,
            string effectId,
            EffectContext ctx
        )
        {
            if (byEntity is not EntityPlayer player)
                return false;

            EffectManager manager = EntityBehaviorEffects.ManagerFor(player);
            return manager != null
                && manager.TryApply(effectId, ctx, EffectLang.Name(effectId));
        }

        /// <summary>
        /// Consumes whatever backs the effect. Default spends <c>durabilityCost</c> when the item
        /// has durability, otherwise takes one off the stack - so a wand wears out instead of
        /// being eaten, with no code of its own.
        /// </summary>
        protected virtual void OnConsumed(ItemSlot slot, EntityAgent byEntity)
        {
            if (durabilityCost > 0 && collObj.GetMaxDurability(slot.Itemstack) > 1)
            {
                collObj.DamageItem(byEntity.World, byEntity, slot, durabilityCost);
                return;
            }

            if (!consumeOnUse)
                return;
            slot.TakeOut(1);
            slot.MarkDirty();
        }

        /// <summary>Extra tooltip lines beyond what <see cref="AppendDescription"/> writes.</summary>
        protected virtual void AppendExtraTooltip(
            StringBuilder dsc,
            string effectId,
            EffectContext ctx
        ) { }

        /// <summary>Plays a denial sound, and a chat message if <paramref name="reasonLangKey"/> is given.</summary>
        protected virtual void DenyUse(EntityAgent byEntity, string reasonLangKey)
        {
            byEntity.PlayEntitySound("smallhurt", (byEntity as EntityPlayer)?.Player);
            if (reasonLangKey != null && byEntity is EntityPlayer { Player: IServerPlayer serverPlayer })
                serverPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get(reasonLangKey),
                    EnumChatType.Notification
                );
        }

        // ----- Interact flow - shared, not meant to be overridden -----

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling,
            ref EnumHandling bhHandling
        )
        {
            if (!ShouldIntercept(slot, byEntity, blockSel))
                return;
            if (!TryResolveEffect(slot, byEntity, out _, out _))
                return;

            if (byEntity.World.Side == EnumAppSide.Server)
                resolvedEntities.Remove(byEntity.EntityId);

            float time = GetConsumeTime(byEntity);
            if (time <= 0f)
            {
                if (firstEvent && byEntity.World.Side == EnumAppSide.Server)
                    TryConsumeAndApply(slot, byEntity);

                handling = EnumHandHandling.PreventDefault;
                bhHandling = EnumHandling.PreventDefault;
                return;
            }

            byEntity.World.RegisterCallback(
                dt =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                        byEntity.PlayEntitySound(sound, (byEntity as EntityPlayer)?.Player);
                },
                200
            );
            byEntity.AnimManager?.StartAnimation(animation);

            if (Api?.Side == EnumAppSide.Client)
            {
                ModSystemProgressBar progressBarSystem =
                    Api.ModLoader.GetModSystem<ModSystemProgressBar>();
                progressBarSystem.RemoveProgressbar(progressBarRender);
                progressBarRender = progressBarSystem.AddProgressbar();
            }

            handling = EnumHandHandling.PreventDefault;
            bhHandling = EnumHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            ref EnumHandling handling
        )
        {
            if (!TryResolveEffect(slot, byEntity, out _, out _))
                return base.OnHeldInteractStep(
                    secondsUsed,
                    slot,
                    byEntity,
                    blockSel,
                    entitySel,
                    ref handling
                );

            handling = EnumHandling.PreventDefault;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
                pos.Y -= 0.4f;
                byEntity.World.SpawnCubeParticles(
                    pos,
                    slot.Itemstack,
                    0.3f,
                    4,
                    0.5f,
                    (byEntity as EntityPlayer)?.Player
                );
            }

            float currentConsumeTime = GetConsumeTime(byEntity);
            if (progressBarRender != null)
                progressBarRender.Progress =
                    currentConsumeTime > 0 ? secondsUsed / currentConsumeTime : 1f;

            return secondsUsed <= currentConsumeTime;
        }

        public override bool OnHeldInteractCancel(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            EnumItemUseCancelReason cancelReason,
            ref EnumHandling handling
        )
        {
            ClearProgressBar();
            return base.OnHeldInteractCancel(
                secondsUsed,
                slot,
                byEntity,
                blockSel,
                entitySel,
                cancelReason,
                ref handling
            );
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            ref EnumHandling handling
        )
        {
            ClearProgressBar();

            if (!TryResolveEffect(slot, byEntity, out _, out _))
                return;

            handling = EnumHandling.PreventDefault;

            float consumeTimeNow = GetConsumeTime(byEntity);
            if (consumeTimeNow <= 0f)
                return; // already applied instantly in OnHeldInteractStart

            if (byEntity.World.Side != EnumAppSide.Server)
                return;
            if (secondsUsed < consumeTimeNow - 0.05f)
                return;
            if (!resolvedEntities.Add(byEntity.EntityId))
                return;

            TryConsumeAndApply(slot, byEntity);
        }

        private void ClearProgressBar()
        {
            Api?.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(progressBarRender);
            progressBarRender = null;
        }

        private void TryConsumeAndApply(ItemSlot slot, EntityAgent byEntity)
        {
            if (!TryResolveEffect(slot, byEntity, out string effectId, out float potencyMul))
                return;

            EffectContext ctx = EffectRegistry.Build(effectId, potencyMul);
            if (ctx == null)
                return;

            string blockReason = GetBlockReason(slot, byEntity, effectId, ctx);
            if (blockReason != null)
            {
                DenyUse(byEntity, blockReason);
                return;
            }

            if (ApplyEffect(slot, byEntity, effectId, ctx))
                OnConsumed(slot, byEntity);
            else
                DenyUse(byEntity, null);
        }

        public override void GetHeldItemInfo(
            ItemSlot slot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            if (!TryResolveEffect(slot, null, out string effectId, out float potencyMul))
                return;

            EffectContext ctx = EffectRegistry.Build(effectId, potencyMul);
            if (ctx == null)
                return;

            AppendDescription(dsc, effectId, ctx);
            AppendExtraTooltip(dsc, effectId, ctx);
        }

        // A generic tooltip built entirely from EffectContext, using the same lang keys the
        // shared HUD falls back to (effectlib:<key>) so a JSON-only mod gets a readable
        // tooltip with zero lang keys of its own.
        private static void AppendDescription(StringBuilder dsc, string effectId, EffectContext ctx)
        {
            foreach (KeyValuePair<string, float> stat in ctx.StatModifiers)
            {
                string label = EffectLang.GetIfExists(effectId, stat.Key) ?? stat.Key;
                dsc.AppendLine($"{label}: {stat.Value * 100:+0.#;-0.#}%");
            }

            if (Math.Abs(ctx.Health) > float.Epsilon)
                dsc.AppendLine(Lang.Get("effectlib:health") + ": " + ctx.Health.ToString("+0.#;-0.#"));
            if (ctx.GlowStrength > 0)
                dsc.AppendLine(Lang.Get("effectlib:glow"));
            if (ctx.WaterBreathe)
                dsc.AppendLine(Lang.Get("effectlib:waterbreathe"));
            if (ctx.ColdResist)
                dsc.AppendLine(Lang.Get("effectlib:coldresist"));
            if (ctx.CanFly)
                dsc.AppendLine(Lang.Get("effectlib:flight"));
            if (ctx.NoGravity)
                dsc.AppendLine(Lang.Get("effectlib:nogravity"));
            if (ctx.CanClimbAnywhere)
                dsc.AppendLine(Lang.Get("effectlib:climb"));
            if (ctx.DisableClimbing)
                dsc.AppendLine(Lang.Get("effectlib:noclimb"));
            if (ctx.NoFallDamage)
                dsc.AppendLine(Lang.Get("effectlib:nofalldamage"));
            if (ctx.FallDamageReduction > 0)
                dsc.AppendLine(Lang.Get("effectlib:fall"));
            if (Math.Abs(ctx.KnockbackResistance) > float.Epsilon)
                dsc.AppendLine(Lang.Get("effectlib:knockbackresist"));
            if (Math.Abs(ctx.ClimbTouchDistance) > float.Epsilon)
                dsc.AppendLine(Lang.Get("effectlib:climbreach"));
            if (Math.Abs(ctx.Weight) > float.Epsilon)
                dsc.AppendLine(Lang.Get("effectlib:weight"));
            if (ctx.Respawn)
                dsc.AppendLine(Lang.Get("effectlib:respawn"));
            if (ctx.Reshape)
                dsc.AppendLine(Lang.Get("effectlib:reshape"));
            if (Math.Abs(ctx.RetainedNutrition) > float.Epsilon)
                dsc.AppendLine(Lang.Get("effectlib:nutrition"));
            if (Math.Abs(ctx.TemporalStabilityGain) > float.Epsilon)
                dsc.AppendLine(Lang.Get("effectlib:temporalstability"));
            if (ctx.SizeChange > 0)
                dsc.AppendLine(Lang.Get("effectlib:grow"));
            if (ctx.SizeChange < 0)
                dsc.AppendLine(Lang.Get("effectlib:shrink"));
            if (ctx.ResetsEffects)
                dsc.AppendLine(Lang.Get("effectlib:purge"));

            if (ctx.Duration > 0)
                dsc.AppendLine(Lang.Get("effectlib:duration", ctx.Duration));
        }
    }
}
