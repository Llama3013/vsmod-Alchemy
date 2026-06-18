using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class BlockThrowablePotionFlask : BlockPotionFlask
    {
        private CollectibleBehaviorThrowable throwable;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            throwable = GetCollectibleBehavior<CollectibleBehaviorThrowable>(true);
        }

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling
        )
        {
            if (throwable == null)
                return;

            EnumHandHandling handHandling = EnumHandHandling.NotHandled;
            EnumHandling behaviorHandling = EnumHandling.PassThrough;
            throwable.OnHeldInteractStart(
                slot,
                byEntity,
                blockSel,
                entitySel,
                firstEvent,
                ref handHandling,
                ref behaviorHandling
            );
            if (behaviorHandling != EnumHandling.PassThrough)
                handling = handHandling;
        }

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            if (throwable == null)
                return false;

            EnumHandling behaviorHandling = EnumHandling.PassThrough;
            return throwable.OnHeldInteractStep(
                secondsUsed,
                slot,
                byEntity,
                blockSel,
                entitySel,
                ref behaviorHandling
            );
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            if (throwable == null)
                return;

            EnumHandling behaviorHandling = EnumHandling.PassThrough;
            throwable.OnHeldInteractStop(
                secondsUsed,
                slot,
                byEntity,
                blockSel,
                entitySel,
                ref behaviorHandling
            );
        }

        public override bool OnHeldInteractCancel(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            EnumItemUseCancelReason cancelReason
        )
        {
            if (throwable == null)
                return true;

            EnumHandling behaviorHandling = EnumHandling.PassThrough;
            return throwable.OnHeldInteractCancel(
                secondsUsed,
                slot,
                byEntity,
                blockSel,
                entitySel,
                cancelReason,
                ref behaviorHandling
            );
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity) { }

        public override void GetHeldItemInfo(
            ItemSlot inSlot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine(
                Lang.Get(
                    "alchemy:throwableflask-info",
                    AlchemyConfig.Loaded.ThrowableFlaskSplashRadius
                )
            );
        }
    }
}
