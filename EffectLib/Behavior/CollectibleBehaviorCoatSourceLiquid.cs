using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Liquid-container sibling of <see cref="CollectibleBehaviorCoatSource"/>: coats from
    /// whatever a <see cref="BlockLiquidContainerBase"/> currently holds, instead of a fixed
    /// attribute on the container itself.
    /// </summary>
    public class CollectibleBehaviorCoatSourceLiquid(CollectibleObject collObj)
        : CollectibleBehaviorCoatSource(collObj)
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

        protected override ItemStack GetSourceStack(ItemSlot slot) =>
            (collObj as BlockLiquidContainerBase)?.GetContent(slot.Itemstack);

        protected override bool TryResolveEffect(
            ItemStack sourceStack,
            out string effectId,
            out float potencyMul
        )
        {
            effectId = null;
            potencyMul = 1f;

            CollectibleObject content = sourceStack?.Collectible;
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

        protected override bool ConsumeSource(ItemSlot slot, EntityAgent byEntity)
        {
            if (collObj is not BlockLiquidContainerBase container)
                return false;

            EntityPlayer player = byEntity as EntityPlayer;
            int consumed = container.SplitStackAndPerformAction(
                player,
                slot,
                stack => container.TryTakeLiquid(stack, consumeLitres)?.StackSize ?? 0
            );
            slot.MarkDirty();
            player?.Player?.InventoryManager?.BroadcastHotbarSlot();
            return consumed > 0;
        }
    }
}
