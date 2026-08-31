using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Liquid-container sibling of <see cref="CollectibleBehaviorEffectItem"/>: reads and
    /// applies the effect declared on whatever a <see cref="BlockLiquidContainerBase"/>
    /// currently holds, instead of a fixed attribute on this item itself. Everything else -
    /// the hold flow, progress bar, tooltip and effect application - is inherited unchanged.
    /// Add <c>{ "name": "EffectLiquid" }</c> to a liquid container's <c>behaviors</c> and its
    /// content items carry the <c>effectinfo</c> attribute instead of the container.
    /// </summary>
    public class CollectibleBehaviorEffectLiquid(CollectibleObject collObj)
        : CollectibleBehaviorEffectItem(collObj)
    {
        protected float consumeLitres;
        protected float checkLitres;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            consumeLitres = properties["consumeLitres"].AsFloat(0.25f);
            checkLitres = properties["checkLitres"].AsFloat(0.25f);
        }

        // What this container holds varies per use, so there is nothing fixed to register once
        // at load - TryResolveEffect below resolves (and registers, the first time it sees each
        // content item) on demand instead.
        protected override void RegisterOwnEffect() { }

        protected override bool TryResolveEffect(
            ItemSlot slot,
            EntityAgent byEntity,
            out string effectId,
            out float potencyMul
        )
        {
            effectId = null;
            potencyMul = 1f;

            CollectibleObject content = (
                collObj as BlockLiquidContainerBase
            )?.GetContent(slot.Itemstack)?.Collectible;
            if (content == null)
                return false;

            JsonObject def = content.Attributes?[attributeKey];
            if (def?.Exists != true)
                return false;

            effectId = def[idField].AsString()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(effectId))
            {
                effectId = null;
                return false;
            }

            if (!EffectRegistry.IsRegistered(effectId))
                JsonEffectDefinition.RegisterFrom(effectId, content.Code.Domain, def, content.Code);

            return true;
        }

        protected override bool HasEnoughSource(ItemSlot slot) =>
            collObj is BlockLiquidContainerBase container
            && container.GetCurrentLitres(slot.Itemstack) >= checkLitres;

        protected override void OnConsumed(ItemSlot slot, EntityAgent byEntity)
        {
            if (collObj is not BlockLiquidContainerBase container)
                return;

            EntityPlayer player = byEntity as EntityPlayer;
            container.SplitStackAndPerformAction(
                player,
                slot,
                stack => container.TryTakeLiquid(stack, consumeLitres)?.StackSize ?? 0
            );
            slot.MarkDirty();
            player?.Player?.InventoryManager?.BroadcastHotbarSlot();
        }
    }
}
