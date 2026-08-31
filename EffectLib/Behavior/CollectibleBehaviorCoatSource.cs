using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// An item that coats a weapon or arrow with an effect when held in the off hand while
    /// shift+right-clicking. Works with zero code for a JSON-only mod - add
    /// <c>{ "name": "CoatSource" }</c> and an <c>effectinfo</c> attribute to an item and it
    /// registers and reads its own effect the moment it loads, the same way
    /// <see cref="CollectibleBehaviorEffectItem"/> does. See
    /// <see cref="CollectibleBehaviorCoatSourceLiquid"/> for the liquid-container form.
    /// </summary>
    public class CollectibleBehaviorCoatSource(CollectibleObject collObj)
        : CollectibleBehavior(collObj)
    {
        // protected: CollectibleBehaviorCoatSourceLiquid resolves the same schema off a liquid
        // container's current content instead of this item's own attribute.
        protected string attributeKey;
        protected string idField;
        private float consumeTime;
        private string defaultEffectId;

        /// <summary>The API captured in <see cref="OnLoaded"/>, available to subclasses.</summary>
        protected ICoreAPI Api { get; private set; }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            attributeKey = properties["attributeKey"].AsString("effectinfo");
            idField = properties["idField"].AsString("effectId");
            consumeTime = properties["consumeTime"].AsFloat(1.5f);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Api = api;
            RegisterOwnEffect();
        }

        /// <summary>
        /// Reads this item's own <c>effectinfo</c> attribute and registers it, so a JSON-only
        /// coating source works with nothing else needed. Override as a no-op if effect ids are
        /// registered elsewhere instead.
        /// </summary>
        protected virtual void RegisterOwnEffect()
        {
            JsonObject def = collObj.Attributes?[attributeKey];
            if (def?.Exists != true)
                return; // a coat source without an effect of its own is not a misconfiguration

            defaultEffectId = def[idField].AsString()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(defaultEffectId))
            {
                defaultEffectId = null;
                return;
            }

            JsonEffectDefinition.RegisterFrom(defaultEffectId, collObj.Code.Domain, def, collObj.Code);
        }

        /// <summary>Seconds to hold shift+right-click before the coating is applied.</summary>
        protected virtual float GetConsumeTime() => consumeTime;

        /// <summary>The stack that actually carries the effect data - this item by default.</summary>
        protected virtual ItemStack GetSourceStack(ItemSlot slot) => slot.Itemstack;

        /// <summary>
        /// Resolves what to coat with and at what potency. Default is the id captured by
        /// <see cref="RegisterOwnEffect"/> at potency 1.
        /// </summary>
        protected virtual bool TryResolveEffect(
            ItemStack sourceStack,
            out string effectId,
            out float potencyMul
        )
        {
            effectId = defaultEffectId;
            potencyMul = 1f;
            return effectId != null;
        }

        /// <summary>Whether there is enough of the source left to use.</summary>
        protected virtual bool HasEnoughSource(ItemSlot slot) => (slot.Itemstack?.StackSize ?? 0) > 0;

        /// <summary>Consumes whatever backs the coating. Default takes one off the stack.</summary>
        protected virtual bool ConsumeSource(ItemSlot slot, EntityAgent byEntity)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
            return true;
        }

        /// <summary>
        /// Call every tick this item is idle in hand - see <c>Item.OnHeldIdle</c> /
        /// <c>Block.OnHeldIdle</c>. There is no held-interact equivalent for an off-hand item,
        /// so this is the only hook available for the coating gesture.
        /// </summary>
        public void CoatingIdle(ItemSlot slot, EntityAgent byEntity)
        {
            ItemStack sourceStack = GetSourceStack(slot);
            TryResolveEffect(sourceStack, out string effectId, out float potencyMul);
            string itemCode = CoatedEffects.DefaultItemCode(sourceStack?.Collectible);

            CoatingInteraction.HandleIdle(
                Api,
                slot,
                byEntity,
                effectId,
                potencyMul,
                itemCode,
                s => HasEnoughSource(s) && ConsumeSource(s, byEntity),
                GetConsumeTime()
            );
        }
    }
}
