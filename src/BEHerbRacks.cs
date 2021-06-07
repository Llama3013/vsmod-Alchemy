using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;


namespace Alchemy
{
    public class BlockEntityHerbRacks : BlockEntityDisplay
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "herbrack";
        public override string AttributeTransformCode => "herbRackTransform";

        Block block;


        public BlockEntityHerbRacks()
        {
            inv = new InventoryGeneric(8, "herbrack-0", null, null);
            meshes = new MeshData[8];
        }

        public override void Initialize(ICoreAPI api)
        {
            block = api.World.BlockAccessor.GetBlock(Pos);
            base.Initialize(api);
        }

        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            if (transType == EnumTransitionType.Dry) return 0;
            if (Api == null) return 0;

            if (transType == EnumTransitionType.Cure) {
                return 5f;
            }
            else if (transType == EnumTransitionType.Perish || transType == EnumTransitionType.Ripen)
            {
                float perishRate = GetPerishRate();
                if (transType == EnumTransitionType.Ripen)
                {
                    return GameMath.Clamp(((1 - perishRate) - 0.5f) * 3, 0, 1);
                }

                return baseMul * perishRate;
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
                if (colObj.Attributes != null && colObj.Attributes["herbrackable"].AsBool(false) == true)
                {
                    AssetLocation sound = slot.Itemstack?.Block?.Sounds?.Place;

                    if (TryPut(slot, blockSel))
                    {
                        Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
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
                    Api.World.PlaySoundAt(sound != null ? sound : new AssetLocation("sounds/player/build"), byPlayer.Entity, byPlayer, true, 16);
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


            float ripenRate = GameMath.Clamp(((1 - GetPerishRate()) - 0.5f) * 3, 0, 1);
            if (ripenRate > 0)
            {
                sb.Append("Suitable spot for food ripening.");
            }


            sb.AppendLine();

            bool up = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

            for (int j = 7; j >= 0; j--)
            {

                if (inv[j].Empty) continue;

                ItemStack stack = inv[j].Itemstack;


                if (stack.Collectible.TransitionableProps != null && stack.Collectible.TransitionableProps.Length > 0)
                {
                    sb.Append(PerishableInfoCompact(Api, inv[j], ripenRate));
                }
                else
                {
                    sb.AppendLine(stack.GetName());
                }
            }
        }

        public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
        {
            if (contentSlot.Empty) return "";

            StringBuilder dsc = new StringBuilder();

            if (withStackName)
            {
                dsc.Append(contentSlot.Itemstack.GetName());
            }

            TransitionState[] transitionStates = contentSlot.Itemstack?.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

            bool nowSpoiling = false;

            if (transitionStates != null)
            {
                bool appendLine = false;
                for (int i = 0; i < transitionStates.Length; i++)
                {
                    TransitionState state = transitionStates[i];

                    TransitionableProperties prop = state.Props;
                    float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                    if (perishRate <= 0) continue;

                    float transitionLevel = state.TransitionLevel;
                    float freshHoursLeft = state.FreshHoursLeft / perishRate;

                    switch (prop.Type)
                    {
                        case EnumTransitionType.Perish:

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                nowSpoiling = true;
                                dsc.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;

                        case EnumTransitionType.Ripen:
                            if (nowSpoiling) break;

                            appendLine = true;

                            if (transitionLevel > 0)
                            {
                                dsc.Append(", " + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                            }
                            else
                            {
                                double hoursPerday = Api.World.Calendar.HoursPerDay;

                                if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                                }
                                else if (freshHoursLeft > hoursPerday)
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                                }
                                else
                                {
                                    dsc.Append(", " + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                                }
                            }
                            break;
                    }
                }


                if (appendLine) dsc.AppendLine();
            }

            return dsc.ToString();
        }

        protected override void translateMesh(MeshData mesh, int index)
        {
            float x;
            float z;
            float rotate;
            //Api.Logger.Debug("potion {0}", index);
            switch (index) {
                case 0:
                    x = 1 / 16f;
                    z = 8 / 16f;
                    rotate = 0f;
                    break;
                case 1:
                    x = 2.75f / 16f;
                    z = 3.25f / 16f;
                    rotate = 315f;
                    //rotate = -0.785398175f;
                    break;
                case 2:
                    x = 3 / 16f;
                    z = 13 / 16f;
                    rotate = 45f;
                    //rotate = 0.785398175f;
                    break;
                case 3:
                    x = 8 / 16f;
                    z = 1f / 16f;
                    rotate = 270f;
                    //rotate = 1.57079635f;
                    break;
                case 4:
                    x = 8 / 16f;
                    z = 15f / 16f;
                    rotate = 90f;
                    //rotate = -1.57079635f;
                    break;
                case 5:
                    x = 13.25f / 16f;
                    z = 3.25f / 16f;
                    rotate = 225f;
                    //rotate = -2.356194525f;
                    break;
                case 6:
                    x = 13 / 16f;
                    z = 13 / 16f;
                    rotate = 135f;
                    //rotate = 2.356194525f;
                    break;
                case 7:
                    x = 15f / 16f;
                    z = 8 / 16f;
                    rotate = 180f;
                    //rotate = 0f;
                    break;
                default:
                    x = 0f;
                    z = 0f;
                    rotate = 0f;
                    break;
            }

            mesh.Scale(new Vec3f(0.5f, 0f, 0.5f), 0.75f, 0.75f, 0.75f);
            mesh.Rotate(new Vec3f(0.5f, 0f, 0.5f), 0f, rotate * GameMath.DEG2RAD, 0f);
            mesh.Translate(x - 0.5f, -0.175f, z - 0.5f);
        }
    }
}
