using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    public class CollectibleBehaviorCoatSource(CollectibleObject collObj)
        : CollectibleBehavior(collObj)
    {
        protected string attributeKey;
        protected string idField;
        private float consumeTime;
        private string ownEffectId;

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

        protected virtual void RegisterOwnEffect()
        {
            JsonObject def = collObj.Attributes?[attributeKey];
            if (def?.Exists != true)
                return;

            ownEffectId = def[idField].AsString()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ownEffectId))
            {
                Api.Logger.Warning(
                    "[EffectLib] {0}'s '{1}' attribute has no '{2}' - give it one, e.g. "
                        + "\"{1}\": {{ \"{2}\": \"{3}:youreffectid\", ... }}. This item will not coat anything.",
                    collObj.Code,
                    attributeKey,
                    idField,
                    collObj.Code.Domain
                );
                ownEffectId = null;
                return;
            }

            JsonEffectDefinition.RegisterFrom(ownEffectId, collObj.Code.Domain, def, collObj.Code);
        }

        protected virtual float GetConsumeTime() => consumeTime;

        protected virtual ItemStack GetSourceStack(ItemSlot slot) => slot.Itemstack;

        protected virtual bool TryResolveEffect(
            ItemStack sourceStack,
            out string effectId,
            out float potencyMul
        )
        {
            effectId = ownEffectId;
            potencyMul = 1f;
            return effectId != null;
        }

        protected virtual bool HasEnoughSource(ItemSlot slot) => (slot.Itemstack?.StackSize ?? 0) > 0;

        protected virtual bool ConsumeSource(ItemSlot slot, EntityAgent byEntity)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
            return true;
        }

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
