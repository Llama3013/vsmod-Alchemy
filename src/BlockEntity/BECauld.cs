using System;
using System.Collections.Generic;
using System.IO;
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
    public class BlockEntityCauld : BlockEntityLiquidContainer
    {
        public int capacityLitres { get; set; } = 50;

        public bool isFull = false;

        GuiDialogCauld invDialog;

        // Slot 0, 1, 2, 3, 4: Input/Item slot
        // Slot 5: Liquid output slot
        // Slot 6: Liquid input slot
        public override string InventoryClassName => "cauld";

        MeshData currentMesh;
        BlockCauld ownBlock;

        Dictionary<string, float> maxEssenceDic;

        public BlockEntityCauld()
        {
            inventory = new InventoryGeneric(7, null, null, (id, self) =>
            {
                if (id >= 0 && id < 5) return new ItemSlotWatertight(self);
                else if (id == 5) return new ItemSlotLiquidOutput(self, 50);
                else return new ItemSlotLiquidOnly(self, 50);
            });
            inventory.BaseWeight = 1;
            inventory.OnGetSuitability = (sourceSlot, targetSlot, isMerge) => (isMerge ? (inventory.BaseWeight + 3) : (inventory.BaseWeight + 1)) + (sourceSlot.Inventory is InventoryBasePlayer ? 6 : 0);

            inventory.SlotModified += Inventory_SlotModified;
        }


        protected override float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
        {
            return base.Inventory_OnAcquireTransitionSpeed(transType, stack, baseMul);
        }

        protected override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            if (atBlockFace == BlockFacing.UP) return inventory[0];
            return null;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            try
            {
                IAsset maxEssences = Api.Assets.TryGet("alchemy:config/essences.json");
                if (maxEssences != null)
                {
                    maxEssenceDic = maxEssences.ToObject<Dictionary<string, float>>();
                }
            }
            catch (Exception e)
            {
                Api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", e);
            }

            ownBlock = Block as BlockCauld;

            if (ownBlock?.Attributes?["capacityLitres"].Exists == true)
            {
                capacityLitres = ownBlock.Attributes["capacityLitres"].AsInt(50);
                (inventory[5] as ItemSlotLiquidOutput).capacityLitres = capacityLitres;
                (inventory[6] as ItemSlotLiquidOnly).CapacityLitres = capacityLitres;
            }

            if (api.Side == EnumAppSide.Client && currentMesh == null)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        private void Inventory_SlotModified(int slotId)
        {
            if (slotId == 0 || slotId == 1 || slotId == 2 || slotId == 3 || slotId == 4 || slotId == 5 || slotId == 6)
            {
                invDialog?.UpdateContents();
                if (Api?.Side == EnumAppSide.Client)
                {
                    currentMesh = GenMesh();
                }

                MarkDirty(true);
            }

        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            invDialog?.TryClose();
            invDialog = null;
        }


        public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data)
        {
            if (packetid <= 1000)
            {
                inventory.InvNetworkUtil.HandleClientPacket(fromPlayer, packetid, data);
            }

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                if (fromPlayer.InventoryManager != null)
                {
                    fromPlayer.InventoryManager.CloseInventory(Inventory);
                }
            }

            if (packetid == 1337)
            {
                MixCauld();
            }
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            base.OnReceivedServerPacket(packetid, data);

            if (packetid == (int)EnumBlockEntityPacketId.Close)
            {
                (Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(Inventory);
                invDialog?.TryClose();
                invDialog?.Dispose();
                invDialog = null;
            }
        }

        //Is there such thing as minimum litre amount? Can I dynamcally change colour and can I have a ghost item?
        public void MixCauld()
        {
            if (!inventory[6].Empty)
            {
                if (inventory[6].Itemstack.StackSize < 8) return;
                float strongEffect;
                if (inventory[6].Itemstack.MatchesSearchText(Api.World, "Potion tea")) strongEffect = 1;
                else if (inventory[6].Itemstack.MatchesSearchText(Api.World, "Aqua Vitae")) strongEffect = 2;
                else return;
                int canMix = 0;
                int isEmpty = 0;
                Dictionary<string, float> essencesDic = new Dictionary<string, float>();
                for (int i = 0; i < 5; i++)
                {
                    if (!inventory[i].Empty)
                    {
                        JsonObject essences = inventory[i].Itemstack.ItemAttributes?["potionessences"];
                        //Api.Logger.Debug("{0}, {1}", inventory[i].Itemstack.Item.Code.ToShortString(), inventory[i].Itemstack.ItemAttributes.ToString());
                        if (inventory[i].Itemstack.ItemAttributes["potionessences"].KeyExists(inventory[i].Itemstack.Collectible.Code.ToShortString()))
                        {
                            essences = inventory[i].Itemstack.ItemAttributes?["potionessences"][inventory[i].Itemstack.Collectible.Code.ToShortString()];
                        }
                        bool hasEss = false;
                        foreach (var essence in maxEssenceDic.Keys.ToList())
                        {
                            if (essences[essence].Exists)
                            {
                                if (!essencesDic.ContainsKey(essence)) essencesDic.Add(essence, 0);
                                essencesDic[essence] = (essencesDic[essence] + essences[essence].AsFloat() < maxEssenceDic[essence]) ? essencesDic[essence] += essences[essence].AsFloat() : maxEssenceDic[essence];
                                if (essencesDic[essence] == 0.0f) essencesDic.Remove(essence);
                                hasEss = true;
                            }
                        }
                        if (hasEss) canMix += 1;
                    }
                    else
                    {
                        isEmpty += 1;
                    }
                }
                JsonObject baseEssences = inventory[6].Itemstack.ItemAttributes?["potionessences"];

                foreach (var essence in maxEssenceDic.Keys.ToList())
                {
                    if (baseEssences[essence].Exists)
                    {
                        if (!essencesDic.ContainsKey(essence)) essencesDic.Add(essence, 0);
                        essencesDic[essence] = (essencesDic[essence] + baseEssences[essence].AsFloat() < maxEssenceDic[essence]) ? essencesDic[essence] += baseEssences[essence].AsFloat() : maxEssenceDic[essence];
                        if (essencesDic[essence] == 0.0f) essencesDic.Remove(essence);
                    }
                }
                canMix += isEmpty;
                if (canMix >= 5 && isEmpty < 5)
                {
                    if (!inventory[5].Empty)
                    {
                        ITreeAttribute outputEssences = Inventory[5].Itemstack.Attributes;
                        foreach (var essence in maxEssenceDic.Keys.ToList())
                        {
                            if (outputEssences.TryGetFloat("potion" + essence) != null)
                            {
                                if (!essencesDic.TryGetValue(essence, out float value))
                                {
                                    return;
                                }
                                else if (!(Math.Abs(essencesDic[essence] - outputEssences.GetFloat("potion" + essence)) < 1e-7))
                                {
                                    return;
                                }
                            }
                            else if (essencesDic.TryGetValue(essence, out float value))
                            {
                                return;
                            }
                        }
                    }

                    for (int i = 0; i <= 4; i++)
                    {
                        if (!inventory[i].Empty)
                        {
                            if (inventory[i].Itemstack.Collectible.IsLiquid())
                            {
                                var srcProps = BlockLiquidContainerBase.GetContainableProps(inventory[0].Itemstack);
                                int liquid = (int)(inventory[0].Itemstack.StackSize / srcProps?.ItemsPerLitre) * 25;
                                inventory[i].TakeOut(liquid);
                            }
                            else
                            {
                                inventory[i].TakeOut(1);
                            }
                        }
                    }
                    //investigate litres change in ItemSlotWaterTight.cs and ItemsPerLitre
                    inventory[6].TakeOut(8);
                    if (inventory[5].Empty)
                    {
                        inventory[5].Itemstack = new ItemStack(Api.World.GetItem(new AssetLocation("alchemy:potionportion")), 8);
                        foreach (var essence in maxEssenceDic.Keys.ToList())
                        {
                            float value;
                            if (essencesDic.TryGetValue(essence, out value))
                            {
                                inventory[5].Itemstack.Attributes.SetFloat("potion" + essence, (float)Math.Round(value * strongEffect, 2));
                            }
                        }
                    }
                    else
                    {
                        inventory[5].Itemstack.StackSize += 8;
                    }
                    if (Api?.Side == EnumAppSide.Server)
                    {
                        MarkDirty(true);
                        Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
                    }
                    invDialog?.UpdateContents();
                    if (Api?.Side == EnumAppSide.Client)
                    {
                        currentMesh = GenMesh();
                        MarkDirty(true);
                    }
                    return;
                }
            }
        }


        public void OnBlockInteract(IPlayer byPlayer)
        {

            if (Api.Side == EnumAppSide.Client)
            {
                if (invDialog == null)
                {
                    invDialog = new GuiDialogCauld("Cauld", Inventory, Pos, Api as ICoreClientAPI);
                    invDialog.OnClosed += () =>
                    {
                        invDialog = null;
                        (Api as ICoreClientAPI).Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumBlockEntityPacketId.Close, null);
                        byPlayer.InventoryManager.CloseInventory(inventory);
                    };
                }

                invDialog.TryOpen();

                (Api as ICoreClientAPI).Network.SendPacketClient(inventory.Open(byPlayer));
            }
            else
            {
                byPlayer.InventoryManager.OpenInventory(inventory);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
                invDialog?.UpdateContents();
            }
        }

        internal MeshData GenMesh()
        {
            if (ownBlock == null) return null;

            MeshData mesh = ownBlock.GenMesh(inventory[5].Itemstack, inventory[6].Itemstack, Pos);

            if (mesh != null)
            {
                if (mesh.CustomInts != null)
                {
                    for (int i = 0; i < mesh.CustomInts.Count; i++)
                    {
                        mesh.CustomInts.Values[i] |= 1 << 27; // Disable water wavy
                        mesh.CustomInts.Values[i] |= 1 << 26; // Enabled weak foam
                    }
                }
            }

            return mesh;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            invDialog?.Dispose();
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            mesher.AddMeshData(currentMesh);
            return true;
        }
    }
}
