using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class BlockEntityHerbRacks : BlockEntityDisplay
    {
        InventoryGeneric inv;
        static int slotCount = 8;

        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "herbrack";

        public override string AttributeTransformCode => "herbRackTransform";

        Block block;

        public BlockEntityHerbRacks()
        {
            inv = new InventoryGeneric(8, "herbrack-0", null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            base.Initialize(api);
        }

        protected override float Inventory_OnAcquireTransitionSpeed(
            EnumTransitionType transType,
            ItemStack stack,
            float baseMul
        )
        {
            if (Api == null)
                return 1;

            if (transType == EnumTransitionType.Dry)
            {
                return 5f;
            }
            if (transType == EnumTransitionType.Cure)
            {
                return 2.5f;
            }
            if (transType == EnumTransitionType.Perish || transType == EnumTransitionType.Ripen)
            {
                float perishRate = GetPerishRate();
                if (transType == EnumTransitionType.Ripen)
                {
                    return GameMath.Clamp(((1 - perishRate) - 0.5f) * 3, 0, 1);
                }

                return baseMul * perishRate;
            }

            return base.Inventory_OnAcquireTransitionSpeed(transType, stack, baseMul);
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
                if (
                    colObj.Attributes != null
                    && colObj.Attributes["herbrackable"].AsBool(false) == true
                )
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;

                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(
                            sound != null ? sound : new AssetLocation("sounds/player/build"),
                            byPlayer.Entity,
                            byPlayer,
                            true,
                            16
                        );
                        updateMeshes();
                        return true;
                    }

                    return false;
                }
            }

            return false;
        }

        private bool TryPut(ItemSlot slot, BlockSelection blockSel)
        {
            int selectionBoxIndex = blockSel.SelectionBoxIndex;

            //Api.Logger.Debug("potion {0}", blockSel.SelectionBoxIndex);
            if (inv[selectionBoxIndex].Empty)
            {
                int moved = slot.TryPutInto(Api.World, inv[selectionBoxIndex]);
                updateMesh(selectionBoxIndex);
                MarkDirty(true);
                return moved > 0;
            }

            return false;
        }

        private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
        {
            int selectionBoxIndex = blockSel.SelectionBoxIndex;
            if (!inv[selectionBoxIndex].Empty)
            {
                ItemStack stack = inv[selectionBoxIndex].TakeOut(1);
                if (byPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    AssetLocation sound = stack.Block?.Sounds?.Place;
                    Api.World.PlaySoundAt(
                        sound != null ? sound : new AssetLocation("sounds/player/build"),
                        byPlayer.Entity,
                        byPlayer,
                        true,
                        16
                    );
                }

                if (stack.StackSize > 0)
                {
                    Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
                MarkDirty(true);
                updateMesh(selectionBoxIndex);
                return true;
            }

            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);

            float cureRate = GameMath.Clamp(((1 - GetPerishRate()) - 0.5f) * 3, 0, 1);

            sb.AppendLine();

            bool up =
                forPlayer.CurrentBlockSelection != null
                && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

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
                    sb.Append(PerishableInfoCompact(Api, inv[j], cureRate));
                }
                else
                {
                    sb.AppendLine(stack.GetName());
                }
            }
        }

        public static string PerishableInfoCompact(
            ICoreAPI Api,
            ItemSlot contentSlot,
            float cureRate,
            bool withStackName = true
        )
        {
            if (contentSlot.Empty)
                return "";

            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates =
                contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(
                    Api.World,
                    contentSlot
                );

            bool nowSpoiling = false;

            if (transitionStates != null)
            {
                bool appendLine = false;
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
                    float transitionHoursLeft =
                        (state.TransitionHours - state.TransitionedHours) / 3;
                    double hoursPerday = Api.World.Calendar.HoursPerDay;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:
                            appendLine = true;

                            if (transitionLevel > 0f)
                            {
                                nowSpoiling = true;
                                dsc.Append(
                                    ", "
                                        + Lang.Get(
                                            "{0}% spoiled",
                                            new object[] { (int)Math.Round(transitionLevel * 100f) }
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
                                                "fresh for {0} years",
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
                                                "fresh for {0} days",
                                                Math.Round(freshHoursLeft / hoursPerday, 1)
                                            )
                                    );
                                }
                                else
                                {
                                    dsc.Append(
                                        ", "
                                            + Lang.Get(
                                                "fresh for {0} hours",
                                                Math.Round(freshHoursLeft, 1)
                                            )
                                    );
                                }
                            }
                            break;
                        case EnumTransitionType.Dry:
                            if (nowSpoiling)
                                break;

                            appendLine = true;
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

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(
                                    ", "
                                        + Lang.Get(
                                            "{1:0.#} days left to cure ({0}%)",
                                            (int)Math.Round(transitionLevel * 100),
                                            transitionHoursLeft / hoursPerday / cureRate
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

                if (appendLine)
                    dsc.AppendLine();
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

                //Api.Logger.Debug("potion {0}", index);
                switch (index)
                {
                    case 0:
                        x = -(5f / 16f);
                        z = 2f / 16f;
                        rotate = 0f;

                        //rotate = 1.57079635f;
                        break;
                    case 1:
                        x = 3.75f / 16f;
                        z = 11.5f / 16f;
                        rotate = 135f;

                        //rotate = -2.356194525f;
                        break;
                    case 2:
                        x = -(5.4f / 16f);
                        z = 13f / 16f;
                        rotate = 45f;

                        //rotate = -0.785398175f;
                        break;
                    case 3:
                        x = 14f / 16f;
                        z = -5f / 16f;
                        rotate = 270f;

                        //rotate = 0f;
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

                        //rotate = 2.356194525f;
                        break;
                    case 6:
                        x = 13f / 16f;
                        z = 21.5f / 16f;
                        rotate = 135f;

                        //rotate = awAWW0.785398175f;
                        break;
                    case 7:
                        x = 21f / 16f;
                        z = 14f / 16f;   
                        rotate = 180f;

                        //rotate = -1.57079635f;
                        break;
                    default:
                        x = 0f;
                        z = 0f;
                        rotate = 0f;
                        break;
                }
                tfMatrices[index] = 
                    new Matrixf()
                    .Translate(x, y, z)
                    .Scale(0.75f, 0.75f, 0.75f)
                    .RotateYDeg(rotate)
                    .Values
                ;
            }
            return tfMatrices;
        }
    }
}
