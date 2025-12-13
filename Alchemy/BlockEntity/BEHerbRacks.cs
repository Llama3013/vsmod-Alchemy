using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Alchemy.BlockEntity
{
    public class BlockEntityHerbRacks : BlockEntityDisplay
    {
        private readonly InventoryGeneric inv;
        private static readonly int slotCount = 8;

        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "herbrack";

        public override string AttributeTransformCode => "herbRackTransform";

        public BlockEntityHerbRacks()
        {
            inv = new InventoryDisplayed(this, 8, "herbrack-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inv.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;
        }

        private float Inventory_OnAcquireTransitionSpeed(
            EnumTransitionType transType,
            ItemStack stack,
            float baseMul
        )
        {
            if (transType == EnumTransitionType.Dry || transType == EnumTransitionType.Melt)
                return container.Room?.ExitCount == 0 ? 5f : 4f;
            if (Api == null)
                return 0;

            if (transType == EnumTransitionType.Cure)
            {
                return 2.5f;
            }
            if (transType == EnumTransitionType.Ripen)
            {
                float perishRate = container.GetPerishRate();
                return GameMath.Clamp((1 - perishRate - 0.5f) * 3, 0, 1);
            }

            return 1;
        }

        internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (slot.Empty)
            {
                if (TryTake(byPlayer, blockSel))
                {
                    return true;
                }
                return false;
            }
            else
            {
                CollectibleObject colObj = slot.Itemstack.Collectible;
                if (colObj.Attributes != null && colObj.Attributes["herbrackable"].AsBool(false))
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;

                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(
                            sound ?? new AssetLocation("sounds/player/build"),
                            byPlayer.Entity,
                            byPlayer,
                            true,
                            16
                        );
                        int index = blockSel.SelectionBoxIndex;
                        Api.World.Logger.Audit(
                            "{0} Put 1x{1} into HerbRack slotid {2} at {3}.",
                            byPlayer.PlayerName,
                            inv[index].Itemstack?.Collectible.Code,
                            index,
                            Pos
                        );
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;

            Api.Logger.Debug("potion {0}", blockSel.SelectionBoxIndex);
            if (inv[index].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inv[index]);

                if (moved > 0)
                {
                    updateMesh(index);
                    MarkDirty(true);
                }
                return moved > 0;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int index = blockSel.SelectionBoxIndex;
            if (!inv[index].Empty)
            {
                ItemStack stack = inv[index].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(
                        sound ?? new AssetLocation("sounds/player/build"),
                        byPlayer.Entity,
                        byPlayer,
                        true,
                        16
                    );
                    Api.World.Logger.Audit(
                        "{0} Took 1x{1} from HerbRack slotid {2} at {3}.",
                        byPlayer.PlayerName,
                        stack.Collectible.Code,
                        index,
                        Pos
                    );
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos);
                }
                (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(
                    EnumHandInteract.HeldItemInteract
                );
                updateMesh(index);
                MarkDirty(true);
                return true;
            }

            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            float ripenRate = GameMath.Clamp(((1 - container.GetPerishRate()) - 0.5f) * 3, 0, 1);

            dsc.AppendLine();

            for (int j = 7; j >= 0; j--)
            {
                if (inv[j].Empty)
                    continue;

                ItemStack stack = inv[j].Itemstack;

                if (
                    stack.Collectible.TransitionableProps != null
                    && stack.Collectible.TransitionableProps.Length > 0
                )
                {
                    dsc.Append(PerishableInfoCompact(Api, inv[j], ripenRate));
                }
                else
                {
                    dsc.AppendLine(stack.GetName());
                }
            }
        }

        public static string PerishableInfoCompact(
            ICoreAPI Api,
            ItemSlot contentSlot,
            float ripenRate,
            bool withStackName = true
        )
        {
            if (contentSlot.Empty)
                return "";
            string baseGamePerishInfo = BlockEntityShelf.PerishableInfoCompact(
                Api,
                contentSlot,
                ripenRate,
                withStackName
            );
            StringBuilder dsc = new();
            if (baseGamePerishInfo != "")
            {
                dsc.Append(baseGamePerishInfo);
            }
            TransitionState[] transitionStates =
                contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(
                    Api.World,
                    contentSlot
                );
            bool nowSpoiling = false;

            if (transitionStates != null)
            {
                foreach (TransitionState state in transitionStates)
                {
                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(
                        Api.World,
                        contentSlot,
                        prop.Type
                    );

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;
                    float transitionHoursLeft = state.TransitionHours - state.TransitionedHours;
                    double hoursPerday = Api.World.Calendar.HoursPerDay;

                    //Skip perish and ripen, already handled by base game
                    switch (prop.Type)
                    {
                        case EnumTransitionType.Dry:
                            if (nowSpoiling)
                                break;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(
                                    ", "
                                        + Lang.Get(
                                            "{1:0.#} days left to dry ({0}%)",
                                            (int)Math.Round(transitionLevel * 100),
                                            transitionHoursLeft / hoursPerday
                                        )
                                );
                            }
                            else
                            {
                                if (
                                    transitionHoursLeft / hoursPerday
                                    >= Api.World.Calendar.DaysPerYear
                                )
                                {
                                    dsc.Append(
                                        ", "
                                            + Lang.Get(
                                                "will dry in {0} years",
                                                Math.Round(
                                                    transitionHoursLeft
                                                        / hoursPerday
                                                        / Api.World.Calendar.DaysPerYear,
                                                    1
                                                )
                                            )
                                    );
                                }
                                else if (transitionHoursLeft > hoursPerday)
                                {
                                    dsc.Append(
                                        ", "
                                            + Lang.Get(
                                                "will dry in {0} days",
                                                Math.Round(transitionHoursLeft / hoursPerday, 1)
                                            )
                                    );
                                }
                                else
                                {
                                    dsc.Append(
                                        ", "
                                            + Lang.Get(
                                                "will dry in {0} hours",
                                                Math.Round(transitionHoursLeft, 1)
                                            )
                                    );
                                }
                            }
                            break;

                        case EnumTransitionType.Cure:
                            if (nowSpoiling)
                                break;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(
                                    ", "
                                        + Lang.Get(
                                            "{1:0.#} days left to cure ({0}%)",
                                            (int)Math.Round(transitionLevel * 100),
                                            transitionHoursLeft / hoursPerday / ripenRate
                                        )
                                );
                            }
                            else
                            {
                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(
                                        ", "
                                            + Lang.Get(
                                                "will cure in {0} years",
                                                Math.Round(
                                                    freshHoursLeft
                                                        / hoursPerday
                                                        / Api.World.Calendar.DaysPerYear,
                                                    1
                                                )
                                            )
                                    );
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(
                                        ", "
                                            + Lang.Get(
                                                "will cure in {0} days",
                                                Math.Round(freshHoursLeft / hoursPerday, 1)
                                            )
                                    );
                                }
                                else
                                {
                                    dsc.Append(
                                        ", "
                                            + Lang.Get(
                                                "will cure in {0} hours",
                                                Math.Round(freshHoursLeft, 1)
                                            )
                                    );
                                }
                            }
                            break;
                    }
                }
            }

            return dsc.ToString();
        }

        protected override float[][] genTransformationMatrices()
        {
            float[][] tfMatrices = new float[slotCount][];
            for (int index = 0; index < slotCount; index++)
            {
                float x;
                float y = -(3.10f / 16f);
                float z;
                float rotate;

                switch (index)
                {
                    case 0:
                        x = -(5f / 16f);
                        z = 2f / 16f;
                        rotate = 0f;

                        break;

                    case 1:
                        x = 3.75f / 16f;
                        z = 11.5f / 16f;
                        rotate = 135f;

                        break;

                    case 2:
                        x = -(5.4f / 16f);
                        z = 13f / 16f;
                        rotate = 45f;

                        break;

                    case 3:
                        x = 14f / 16f;
                        z = -5f / 16f;
                        rotate = 270f;

                        break;

                    case 4:
                        x = 2f / 16f;
                        z = 21f / 16f;
                        rotate = 90f;
                        break;

                    case 5:
                        x = 21.5f / 16f;
                        z = 3.25f / 16f;
                        rotate = 225f;

                        break;

                    case 6:
                        x = 13f / 16f;
                        z = 21.5f / 16f;
                        rotate = 135f;

                        break;

                    case 7:
                        x = 21f / 16f;
                        z = 14f / 16f;
                        rotate = 180f;

                        break;

                    default:
                        x = 0f;
                        z = 0f;
                        rotate = 0f;
                        break;
                }
                tfMatrices[index] = new Matrixf()
                    .Translate(x, y, z)
                    .Scale(0.75f, 0.75f, 0.75f)
                    .RotateYDeg(rotate)
                    .Values;
            }
            return tfMatrices;
        }
    }
}
