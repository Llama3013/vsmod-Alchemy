using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class BlockCauldronFirepit
        : BlockCookingContainer,
            IIgnitable,
            ISmokeEmitter,
            IInFirepitRendererSupplier
    {
        public int Stage
        {
            get
            {
                return LastCodePart() switch
                {
                    "construct0" => 0,
                    "construct1" => 1,
                    "construct2" => 2,
                    "construct3" => 3,
                    "construct4" => 4,
                    _ => 5,
                };
            }
        }

        public string NextStageCodePart
        {
            get
            {
                return LastCodePart() switch
                {
                    "construct0" => "construct1",
                    "construct1" => "construct2",
                    "construct2" => "construct3",
                    "construct3" => "construct4",
                    "construct4" => "cold",
                    _ => "cold",
                };
            }
        }

        private static bool IsVanillaFirepit(BlockEntityFirepit firepit) =>
            firepit is not BlockEntityCauldronFirepit;

        IInFirepitRenderer IInFirepitRendererSupplier.GetRendererWhenInFirepit(
            ItemStack stack,
            BlockEntityFirepit firepit,
            bool isOutputStack
        ) =>
            capi == null
                ? null
                : new CauldronInFirepitRenderer(
                    capi,
                    stack,
                    firepit.Pos,
                    firepit,
                    drawCauldronMesh: firepit is not BlockEntityCauldronFirepit
                );

        EnumFirepitModel IInFirepitRendererSupplier.GetDesiredFirepitModel(
            ItemStack stack,
            BlockEntityFirepit firepit,
            bool isOutputStack
        )
        {
            if (IsVanillaFirepit(firepit) && !AlchemyConfig.Loaded.AllowCauldronInVanillaFirepit)
                return EnumFirepitModel.Normal;
            return EnumFirepitModel.Wide;
        }

        public bool IsExtinct;
        protected AdvancedParticleProperties[] ringParticles;
        protected Vec3f[] basePos;
        protected WorldInteraction[] interactions;
        ICoreClientAPI capi;

        private static readonly Cuboidf[] SideSelectionBoxes =
        [
            new(0.6f, 0.25f, 0.25f, 0.75f, 1.1f, 0.4f),
            new(0.25f, 0.25f, 0.25f, 0.4f, 1.1f, 0.4f),
            new(0.25f, 0.25f, 0.6f, 0.4f, 1.1f, 0.75f),
            new(0.6f, 0.25f, 0.6f, 0.75f, 1.1f, 0.75f),
        ];

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;
            capi?.Event.RegisterEventBusListener(OnGetTransform, 0.5, "ongettransform");

            IsExtinct = LastCodePart() != "lit";

            if (!IsExtinct && api.Side == EnumAppSide.Client)
            {
                ringParticles = new AdvancedParticleProperties[this.ParticleProperties.Length * 4];
                basePos = new Vec3f[ringParticles.Length];

                Cuboidf[] spawnBoxes =
                [
                    new Cuboidf(x1: 0.0f, y1: 0, z1: 0.35f, x2: 0.25f, y2: 0.09f, z2: 0.65f),
                    new Cuboidf(x1: 0.75f, y1: 0, z1: 0.35f, x2: 1.0f, y2: 0.09f, z2: 0.65f),
                    new Cuboidf(x1: 0.35f, y1: 0, z1: 0.0f, x2: 0.65f, y2: 0.09f, z2: 0.25f),
                    new Cuboidf(x1: 0.35f, y1: 0, z1: 0.75f, x2: 0.65f, y2: 0.09f, z2: 1.0f),
                ];

                for (int i = 0; i < ParticleProperties.Length; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        AdvancedParticleProperties props = ParticleProperties[i].Clone();

                        Cuboidf box = spawnBoxes[j];
                        basePos[i * 4 + j] = new Vec3f(0, 0, 0);

                        props.PosOffset[0].avg = box.MidX;
                        props.PosOffset[0].var = box.Width / 2;

                        props.PosOffset[1].avg = 0.0f;
                        props.PosOffset[1].var = 0.05f;

                        props.PosOffset[2].avg = box.MidZ;
                        props.PosOffset[2].var = box.Length / 2;

                        props.Quantity.avg /= 4f;
                        props.Quantity.var /= 4f;

                        ringParticles[i * 4 + j] = props;
                    }
                }
            }

            if (api.Side != EnumAppSide.Client)
                return;

            interactions = ObjectCacheUtil.GetOrCreate(
                api,
                "cauldronFirepitInteractions-" + Stage,
                () =>
                {
                    if (Stage == 0)
                    {
                        return new WorldInteraction[]
                        {
                            new()
                            {
                                ActionLangCode = "game:blockhelp-firepit-refuel",
                                MouseButton = EnumMouseButton.Right,
                                Itemstacks =
                                [
                                    new(api.World.GetItem(new AssetLocation("drygrass"))),
                                ],
                            },
                        };
                    }

                    if (Stage >= 1 && Stage <= 4)
                    {
                        return
                        [
                            new()
                            {
                                ActionLangCode = "game:blockhelp-firepit-refuel",
                                MouseButton = EnumMouseButton.Right,
                                Itemstacks =
                                [
                                    new(api.World.GetItem(new AssetLocation("firewood"))),
                                ],
                            },
                        ];
                    }

                    List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(
                        api,
                        true
                    );

                    List<ItemStack> spoonStacks = [];
                    foreach (Item item in api.World.Items)
                        if (item?.Code?.Path?.StartsWith("stirringspoon") == true)
                            spoonStacks.Add(new ItemStack(item));

                    List<ItemStack> liquidContainerStacks = [];

                    foreach (CollectibleObject obj in api.World.Collectibles)
                    {
                        if (obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                        {
                            List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                            if (stacks != null)
                                liquidContainerStacks.AddRange(stacks);
                        }
                    }

                    ItemStack[] lstacks = [.. liquidContainerStacks];

                    return
                    [
                        new()
                        {
                            ActionLangCode = "blockhelp-firepit-open",
                            MouseButton = EnumMouseButton.Right,
                        },
                        new()
                        {
                            ActionLangCode = "blockhelp-firepit-ignite",
                            MouseButton = EnumMouseButton.Right,
                            Itemstacks = [.. canIgniteStacks],
                            GetMatchingStacks = (wi, bs, es) =>
                            {
                                BlockEntityFirepit bef =
                                    api.World.BlockAccessor.GetBlockEntity(bs.Position)
                                    as BlockEntityFirepit;
                                if (bef?.fuelSlot != null && !bef.fuelSlot.Empty && !bef.IsBurning)
                                    return wi.Itemstacks;
                                return null;
                            },
                        },
                        new()
                        {
                            ActionLangCode = "blockhelp-firepit-refuel",
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = "shift",
                        },
                        new WorldInteraction()
                        {
                            ActionLangCode = "blockhelp-bucket-rightclick",
                            MouseButton = EnumMouseButton.Right,
                            Itemstacks = lstacks,
                            GetMatchingStacks = (wi, bs, ws) =>
                            {
                                return lstacks;
                            },
                        },
                        new()
                        {
                            ActionLangCode = "alchemy:blockhelp-cauldron-attachspoon",
                            MouseButton = EnumMouseButton.Right,
                            Itemstacks = [.. spoonStacks],
                        },
                        new()
                        {
                            ActionLangCode = "alchemy:blockhelp-cauldron-detachspoon",
                            MouseButton = EnumMouseButton.Right,
                        },
                    ];
                }
            );
        }

        private void OnGetTransform(string eventName, ref EnumHandling handling, IAttribute data)
        {
            TreeAttribute tree = data as TreeAttribute;
            if (tree.GetString("target") != "infirepitTransform")
                return;

            ItemSlot slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;

            InFirePitProps props = BlockEntityFirepit.GetRenderProps(slot.Itemstack);
            if (props?.Transform == null)
                return;

            handling = EnumHandling.PreventDefault;
            tree.SetBool("preventDefault", true);
            props.Transform.ToTreeAttribute(tree);
        }

        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (
                world.Rand.NextDouble() < 0.05
                && GetBlockEntity<BlockEntityFirepit>(pos)?.IsBurning == true
            )
            {
                entity.ReceiveDamage(
                    new DamageSource()
                    {
                        Source = EnumDamageSource.Block,
                        SourceBlock = this,
                        Type = EnumDamageType.Fire,
                        SourcePos = pos.ToVec3d(),
                    },
                    0.5f
                );
            }

            base.OnEntityInside(world, entity, pos);
        }

        EnumIgniteState IIgnitable.OnTryIgniteStack(
            EntityAgent byEntity,
            BlockPos pos,
            ItemSlot slot,
            float secondsIgniting
        )
        {
            BlockEntityCauldronFirepit bef =
                api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCauldronFirepit;
            if (bef.IsBurning)
                return secondsIgniting > 2 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
            return EnumIgniteState.NotIgnitable;
        }

        public EnumIgniteState OnTryIgniteBlock(
            EntityAgent byEntity,
            BlockPos pos,
            float secondsIgniting
        )
        {
            if (api.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityFirepit bef)
                return EnumIgniteState.NotIgnitable;
            return bef.GetIgnitableState(secondsIgniting);
        }

        public void OnTryIgniteBlockOver(
            EntityAgent byEntity,
            BlockPos pos,
            float secondsIgniting,
            ref EnumHandling handling
        )
        {
            if (
                api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityFirepit bef
                && !bef.canIgniteFuel
            )
            {
                bef.canIgniteFuel = true;
                bef.extinguishedTotalHours = api.World.Calendar.TotalHours;
            }

            handling = EnumHandling.PreventDefault;
        }

        public override bool ShouldReceiveClientParticleTicks(
            IWorldAccessor world,
            IPlayer player,
            BlockPos pos,
            out bool isWindAffected
        )
        {
            bool val = base.ShouldReceiveClientParticleTicks(world, player, pos, out _);
            isWindAffected = true;

            return val;
        }

        public override void OnAsyncClientParticleTick(
            IAsyncParticleManager manager,
            BlockPos pos,
            float windAffectednessAtPos,
            float secondsTicking
        )
        {
            if (IsExtinct)
            {
                base.OnAsyncClientParticleTick(manager, pos, windAffectednessAtPos, secondsTicking);
                return;
            }

            for (int i = 0; i < ringParticles.Length; i++)
            {
                AdvancedParticleProperties bps = ringParticles[i];
                bps.WindAffectednesAtPos = windAffectednessAtPos;
                bps.basePos.X = pos.X + basePos[i].X;
                bps.basePos.Y = pos.InternalY + basePos[i].Y;
                bps.basePos.Z = pos.Z + basePos[i].Z;

                manager.Spawn(bps);
            }

            return;
        }

        private int BaseSelectionBoxCount(IBlockAccessor blockAccessor, BlockPos pos) =>
            base.GetSelectionBoxes(blockAccessor, pos)?.Length ?? 1;

        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel
        )
        {
            if (
                blockSel != null
                && !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use)
            )
            {
                return false;
            }

            int stage = Stage;
            ItemStack stack = byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack;

            if (stage == 5)
            {
                if (
                    world.BlockAccessor.GetBlockEntity(blockSel.Position)
                    is not BlockEntityCauldronFirepit bef
                )
                    return base.OnBlockInteractStart(world, byPlayer, blockSel);

                if (
                    stack?.Block != null
                    && stack.Block.HasBehavior<BlockBehaviorCanIgnite>()
                    && bef.GetIgnitableState(0) == EnumIgniteState.Ignitable
                )
                {
                    return false;
                }

                ItemStack cauldronStack = bef.inputSlot?.Itemstack;
                bool hasStick = cauldronStack?.Attributes.HasAttribute("stirringSpoon") == true;

                int baseBoxCount = BaseSelectionBoxCount(world.BlockAccessor, blockSel.Position);

                if (hasStick && blockSel.SelectionBoxIndex == baseBoxCount)
                {
                    if (world.Side == EnumAppSide.Server)
                    {
                        ItemStack stickStack = (
                            cauldronStack.Attributes["stirringSpoon"] as ItemstackAttribute
                        )?.value?.Clone();
                        cauldronStack.Attributes.RemoveAttribute("stirringSpoon");
                        cauldronStack.Attributes.RemoveAttribute("stirringSpoonFacing");
                        bef.inputSlot.MarkDirty();
                        if (stickStack != null)
                        {
                            stickStack.ResolveBlockOrItem(world);
                            if (!byPlayer.InventoryManager.TryGiveItemstack(stickStack))
                                world.SpawnItemEntity(
                                    stickStack,
                                    blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
                                );
                        }
                    }
                    return true;
                }

                int sideIndex = blockSel.SelectionBoxIndex - baseBoxCount;
                if (!hasStick && sideIndex >= 0 && sideIndex <= 3)
                {
                    if (stack?.Item is ItemStirringSpoon && cauldronStack != null)
                    {
                        if (world.Side == EnumAppSide.Server)
                        {
                            ItemStack spoonStack = stack.Clone();
                            spoonStack.StackSize = 1;
                            cauldronStack.Attributes["stirringSpoon"] = new ItemstackAttribute(
                                spoonStack
                            );
                            cauldronStack.Attributes.SetInt("stirringSpoonFacing", sideIndex);
                            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                            bef.inputSlot.MarkDirty();
                        }
                    }
                    return true;
                }

                if (stack != null)
                {
                    bool activated = false;

                    CollectibleObject heldCollectible = stack.Collectible;

                    if (heldCollectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
                    {
                        EnumHandHandling liqHandling = EnumHandHandling.NotHandled;
                        heldCollectible.OnHeldInteractStart(
                            byPlayer.InventoryManager.ActiveHotbarSlot,
                            byPlayer.Entity,
                            blockSel,
                            null,
                            true,
                            ref liqHandling
                        );
                        if (
                            liqHandling == EnumHandHandling.PreventDefault
                            || liqHandling == EnumHandHandling.PreventDefaultAction
                        )
                            return true;
                    }

                    if (heldCollectible is ILiquidInterface)
                    {
                        if (
                            heldCollectible is ILiquidSource objLso
                            && objLso.AllowHeldLiquidTransfer
                        )
                        {
                            ItemStack workingStack;
                            if (stack.StackSize > 1)
                            {
                                workingStack = stack.Clone();
                                workingStack.StackSize = 1;
                            }
                            else
                            {
                                workingStack = stack;
                            }

                            ItemStack contentStack = objLso.GetContent(workingStack);
                            if (
                                contentStack != null
                                && contentStack.StackSize > 0
                                && bef.Inventory is InventorySmelting inv
                            )
                            {
                                if (world.Side == EnumAppSide.Server)
                                {
                                    ItemStack contentCopy = contentStack.Clone();
                                    DummySlot tmpSlot = new(contentCopy);
                                    int before = contentCopy.StackSize;
                                    foreach (ItemSlot cookSlot in inv.CookingSlots)
                                    {
                                        if (
                                            tmpSlot.Itemstack == null
                                            || tmpSlot.Itemstack.StackSize <= 0
                                        )
                                            break;
                                        if (cookSlot.Empty)
                                            continue;
                                        if (
                                            !cookSlot.Itemstack.Equals(
                                                world,
                                                tmpSlot.Itemstack,
                                                GlobalConstants.IgnoredStackAttributes
                                            )
                                        )
                                            continue;
                                        int space = cookSlot.MaxSlotStackSize - cookSlot.StackSize;
                                        if (space <= 0)
                                            continue;
                                        int toMove = Math.Min(space, tmpSlot.Itemstack.StackSize);
                                        cookSlot.Itemstack.StackSize += toMove;
                                        tmpSlot.Itemstack.StackSize -= toMove;
                                        if (tmpSlot.Itemstack.StackSize <= 0)
                                            tmpSlot.Itemstack = null;
                                        cookSlot.MarkDirty();
                                    }
                                    foreach (ItemSlot cookSlot in inv.CookingSlots)
                                    {
                                        if (
                                            tmpSlot.Itemstack == null
                                            || tmpSlot.Itemstack.StackSize <= 0
                                        )
                                            break;
                                        if (!cookSlot.Empty)
                                            continue;
                                        ItemStackMoveOperation op = new ItemStackMoveOperation(
                                            world,
                                            EnumMouseButton.Left,
                                            0,
                                            EnumMergePriority.AutoMerge,
                                            tmpSlot.Itemstack.StackSize
                                        );
                                        tmpSlot.TryPutInto(cookSlot, ref op);
                                    }
                                    int moved = before - (tmpSlot.Itemstack?.StackSize ?? 0);
                                    if (moved > 0)
                                    {
                                        objLso.TryTakeContent(workingStack, moved);
                                        if (stack.StackSize > 1)
                                        {
                                            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                                            if (
                                                !byPlayer.InventoryManager.TryGiveItemstack(
                                                    workingStack,
                                                    true
                                                )
                                            )
                                                world.SpawnItemEntity(
                                                    workingStack,
                                                    blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
                                                );
                                        }
                                        else
                                        {
                                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                                        }
                                        bef.MarkDirty(true);
                                        WaterTightContainableProps lprops =
                                            BlockLiquidContainerBase.GetContainableProps(
                                                contentStack
                                            );
                                        float litres = moved / (lprops?.ItemsPerLitre ?? 1f);
                                        world.PlaySoundAt(
                                            lprops?.FillSound
                                                ?? new AssetLocation(
                                                    "sounds/effect/water-fill.ogg"
                                                ),
                                            byPlayer.Entity,
                                            null,
                                            true,
                                            16,
                                            GameMath.Clamp(litres / 5f, 0.35f, 1f)
                                        );
                                    }
                                }
                                return true;
                            }
                        }

                        if (heldCollectible is ILiquidSink objLsi && objLsi.AllowHeldLiquidTransfer)
                        {
                            ItemStack outputStack = bef.outputSlot?.Itemstack;
                            if (outputStack != null && outputStack.StackSize > 0)
                            {
                                if (world.Side == EnumAppSide.Server)
                                {
                                    ItemStack workingStack;
                                    if (stack.StackSize > 1)
                                    {
                                        workingStack = stack.Clone();
                                        workingStack.StackSize = 1;
                                    }
                                    else
                                    {
                                        workingStack = stack;
                                    }

                                    int moved = objLsi.TryPutLiquid(
                                        workingStack,
                                        outputStack,
                                        objLsi.CapacityLitres
                                    );
                                    if (moved > 0)
                                    {
                                        bef.outputSlot.Itemstack.StackSize -= moved;
                                        if (bef.outputSlot.Itemstack.StackSize <= 0)
                                            bef.outputSlot.Itemstack = null;
                                        bef.outputSlot.MarkDirty();
                                        if (stack.StackSize > 1)
                                        {
                                            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                                            if (
                                                !byPlayer.InventoryManager.TryGiveItemstack(
                                                    workingStack,
                                                    true
                                                )
                                            )
                                                world.SpawnItemEntity(
                                                    workingStack,
                                                    blockSel.Position.ToVec3d().Add(0.5, 1.0, 0.5)
                                                );
                                        }
                                        else
                                        {
                                            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                                        }
                                        bef.MarkDirty(true);
                                        WaterTightContainableProps lprops =
                                            BlockLiquidContainerBase.GetContainableProps(
                                                outputStack
                                            );
                                        float litres = moved / (lprops?.ItemsPerLitre ?? 1f);
                                        world.PlaySoundAt(
                                            lprops?.PourSound
                                                ?? new AssetLocation(
                                                    "sounds/effect/water-pour.ogg"
                                                ),
                                            byPlayer.Entity,
                                            null,
                                            true,
                                            16,
                                            GameMath.Clamp(litres / 5f, 0.35f, 1f)
                                        );
                                    }
                                }
                                return true;
                            }

                            if (bef.Inventory is InventorySmelting inv)
                            {
                                for (int i = inv.CookingSlots.Length - 1; i >= 0; i--)
                                {
                                    ItemSlot cookSlot = inv.CookingSlots[i];
                                    if (cookSlot.Empty)
                                        continue;
                                    WaterTightContainableProps lProps =
                                        BlockLiquidContainerBase.GetContainableProps(
                                            cookSlot.Itemstack
                                        );
                                    if (lProps == null)
                                        continue;

                                    if (world.Side == EnumAppSide.Server)
                                    {
                                        ItemStack workingStack;
                                        if (stack.StackSize > 1)
                                        {
                                            workingStack = stack.Clone();
                                            workingStack.StackSize = 1;
                                        }
                                        else
                                        {
                                            workingStack = stack;
                                        }

                                        int moved = objLsi.TryPutLiquid(
                                            workingStack,
                                            cookSlot.Itemstack,
                                            objLsi.CapacityLitres
                                        );
                                        if (moved > 0)
                                        {
                                            cookSlot.Itemstack.StackSize -= moved;
                                            if (cookSlot.Itemstack.StackSize <= 0)
                                                cookSlot.Itemstack = null;
                                            cookSlot.MarkDirty();
                                            if (stack.StackSize > 1)
                                            {
                                                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(
                                                    1
                                                );
                                                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                                                if (
                                                    !byPlayer.InventoryManager.TryGiveItemstack(
                                                        workingStack,
                                                        true
                                                    )
                                                )
                                                    world.SpawnItemEntity(
                                                        workingStack,
                                                        blockSel
                                                            .Position.ToVec3d()
                                                            .Add(0.5, 1.0, 0.5)
                                                    );
                                            }
                                            else
                                            {
                                                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                                            }
                                            bef.MarkDirty(true);
                                            float litres = moved / lProps.ItemsPerLitre;
                                            world.PlaySoundAt(
                                                lProps.PourSound
                                                    ?? new AssetLocation(
                                                        "sounds/effect/water-pour.ogg"
                                                    ),
                                                byPlayer.Entity,
                                                null,
                                                true,
                                                16,
                                                GameMath.Clamp(litres / 5f, 0.35f, 1f)
                                            );
                                        }
                                    }
                                    return true;
                                }
                            }
                        }
                    }

                    if (byPlayer.Entity.Controls.ShiftKey)
                    {
                        CombustibleProperties combustibleProps =
                            stack.Collectible.GetCombustibleProperties(world, stack, null);
                        if (combustibleProps != null && combustibleProps.MeltingPoint > 0)
                        {
                            ItemStackMoveOperation op = new(
                                world,
                                EnumMouseButton.Left,
                                0,
                                EnumMergePriority.DirectMerge,
                                1
                            );
                            byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(
                                bef.inputSlot,
                                ref op
                            );
                            if (op.MovedQuantity > 0)
                                activated = true;
                        }

                        if (combustibleProps != null && combustibleProps.BurnTemperature > 0)
                        {
                            ItemStackMoveOperation op = new(
                                world,
                                EnumMouseButton.Left,
                                0,
                                EnumMergePriority.DirectMerge,
                                1
                            );
                            byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(
                                bef.fuelSlot,
                                ref op
                            );
                            if (op.MovedQuantity > 0)
                                activated = true;
                        }
                    }

                    if (
                        stack?.Collectible is BlockSmeltingContainer or BlockSmeltedContainer
                        && !activated
                    )
                    {
                        if (
                            byPlayer.InventoryManager.ActiveHotbarSlot.TryPutInto(
                                api.World,
                                bef.inputSlot,
                                1
                            ) > 0
                        )
                            activated = true;
                    }

                    if (activated)
                    {
                        (byPlayer as IClientPlayer)?.TriggerFpAnimation(
                            EnumHandInteract.HeldItemInteract
                        );

                        AssetLocation loc =
                            stack.ItemAttributes?["placeSound"].Exists == true
                                ? AssetLocation.Create(
                                    stack.ItemAttributes["placeSound"].AsString(),
                                    stack.Collectible.Code.Domain
                                )
                                : null;

                        if (loc != null)
                        {
                            api.World.PlaySoundAt(
                                loc.WithPathPrefixOnce("sounds/"),
                                blockSel.Position.X,
                                blockSel.Position.InternalY,
                                blockSel.Position.Z,
                                byPlayer,
                                0.88f + (float)api.World.Rand.NextDouble() * 0.24f,
                                16
                            );
                        }

                        return true;
                    }
                }

                return bef.OnPlayerRightClick(byPlayer, blockSel);
            }

            if (
                stack != null
                && TryConstruct(world, blockSel.Position, stack.Collectible, byPlayer)
            )
            {
                if (byPlayer != null && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                }
                return true;
            }

            return false;
        }

        public bool TryConstruct(
            IWorldAccessor world,
            BlockPos pos,
            CollectibleObject obj,
            IPlayer player
        )
        {
            int stage = Stage;

            bool isGrass = obj is ItemDryGrass;
            bool isFirewood = obj.Attributes?.IsTrue("firepitConstructable") == true;

            if (stage == 0 && !isGrass)
                return false;
            if (stage >= 1 && !isFirewood)
                return false;

            if (stage == 5)
                return false;

            Block block = world.GetBlock(CodeWithParts(NextStageCodePart));
            world.BlockAccessor.ExchangeBlock(block.BlockId, pos);
            world.BlockAccessor.MarkBlockDirty(pos);
            if (block.Sounds != null)
                world.PlaySoundAt(block.Sounds.Place, pos, -0.5, player);

            if (stage == 4)
            {
                BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
                if (be is BlockEntityCauldronFirepit firepit)
                {
                    firepit.Inventory[0].Itemstack = new ItemStack(obj, 4);
                    firepit.EnsureCauldronInSlot();
                }
            }

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }

        public static bool IsFirewoodPile(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityGroundStorage beg =
                world.BlockAccessor.GetBlockEntity<BlockEntityGroundStorage>(pos);
            return beg != null && beg.Inventory[0]?.Itemstack?.Collectible is ItemFirewood;
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(
            IWorldAccessor world,
            BlockSelection selection,
            IPlayer forPlayer
        )
        {
            int baseBoxCount =
                selection != null
                    ? BaseSelectionBoxCount(world.BlockAccessor, selection.Position)
                    : 1;
            bool onSideBox =
                selection != null
                && selection.SelectionBoxIndex >= baseBoxCount
                && selection.SelectionBoxIndex < baseBoxCount + 4;

            if (onSideBox)
            {
                BlockEntityFirepit bef =
                    world.BlockAccessor.GetBlockEntity(selection.Position) as BlockEntityFirepit;
                ItemStack cauldronStack = bef?.inputSlot?.Itemstack;
                bool hasSpoon = cauldronStack?.Attributes.HasAttribute("stirringSpoon") == true;
                string target = hasSpoon
                    ? "alchemy:blockhelp-cauldron-detachspoon"
                    : "alchemy:blockhelp-cauldron-attachspoon";
                foreach (WorldInteraction wi in interactions)
                    if (wi.ActionLangCode == target)
                        return [wi];
                return [];
            }

            List<WorldInteraction> result = [];
            foreach (WorldInteraction wi in interactions)
            {
                if (
                    wi.ActionLangCode == "alchemy:blockhelp-cauldron-attachspoon"
                    || wi.ActionLangCode == "alchemy:blockhelp-cauldron-detachspoon"
                )
                    continue;
                result.Add(wi);
            }

            return result
                .ToArray()
                .Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public override bool TryPlaceBlock(
            IWorldAccessor world,
            IPlayer byPlayer,
            ItemStack itemstack,
            BlockSelection blockSel,
            ref string failureCode
        )
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;
            DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            return true;
        }

        public override int GetRandomColor(
            ICoreClientAPI capi,
            BlockPos pos,
            BlockFacing facing,
            int rndIndex = -1
        )
        {
            if (Textures.TryGetValue("metal", out CompositeTexture tex) && tex?.Baked != null)
                return capi.BlockTextureAtlas.GetRandomColor(tex.Baked.TextureSubId, rndIndex);
            return 0;
        }

        public bool EmitsSmoke(BlockPos pos)
        {
            BlockEntityFirepit beFirepit =
                api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFirepit;
            return beFirepit?.IsBurning == true;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (Stage < 5)
                return base.GetSelectionBoxes(blockAccessor, pos);

            Cuboidf[] boxes = base.GetSelectionBoxes(blockAccessor, pos);
            BlockEntityFirepit beFirepit = blockAccessor.GetBlockEntity(pos) as BlockEntityFirepit;
            ItemStack cauldronStack = beFirepit?.inputSlot?.Itemstack;

            if (cauldronStack?.Attributes.HasAttribute("stirringSpoon") == true)
            {
                int facing = cauldronStack.Attributes.GetInt("stirringSpoonFacing", 0);
                Cuboidf[] extended = new Cuboidf[(boxes?.Length ?? 0) + 1];
                boxes?.CopyTo(extended, 0);
                extended[^1] = SideSelectionBoxes[GameMath.Clamp(facing, 0, 3)];
                return extended;
            }

            Cuboidf[] withSides = new Cuboidf[(boxes?.Length ?? 0) + 4];
            boxes?.CopyTo(withSides, 0);
            int offset = boxes?.Length ?? 0;
            for (int i = 0; i < 4; i++)
                withSides[offset + i] = SideSelectionBoxes[i];
            return withSides;
        }

        public override void DoSmelt(
            IWorldAccessor world,
            ISlotProvider cookingSlotsProvider,
            ItemSlot inputSlot,
            ItemSlot outputSlot
        )
        {
            if (
                cookingSlotsProvider is InventorySmelting smelting
                && world.BlockAccessor.GetBlockEntity(smelting.pos)
                    is not BlockEntityCauldronFirepit
            )
            {
                base.DoSmelt(world, cookingSlotsProvider, inputSlot, outputSlot);
                return;
            }

            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider);
            CookingRecipe recipe = GetMatchingCookingRecipe(
                world,
                stacks,
                out int quantityServings
            );

            if (recipe?.CooksInto == null)
                return;
            if (quantityServings < 1 || quantityServings > MaxServingSize)
                return;

            ItemStack cauldronStack = inputSlot.Itemstack;
            if (recipe.CooksInto.Code?.Domain == "alchemy")
            {
                bool hasSpoon = cauldronStack?.Attributes.HasAttribute("stirringSpoon") == true;
                if (!hasSpoon)
                    return;

                ItemStack spoonStack = (
                    cauldronStack.Attributes["stirringSpoon"] as ItemstackAttribute
                )?.value;
                bool isGlassSpoon =
                    spoonStack?.Collectible?.Code?.Path?.StartsWith("stirringspoon-glass") == true;
                if (!isGlassSpoon && recipe.CooksInto.Code.Path?.Contains("strong") == true)
                    return;
            }

            ItemStack outStack = recipe.CooksInto.ResolvedItemstack?.Clone();
            if (outStack == null)
                return;

            outStack.StackSize *= quantityServings;

            if (!outputSlot.Empty)
            {
                if (
                    !outputSlot.Itemstack.Equals(
                        world,
                        outStack,
                        GlobalConstants.IgnoredStackAttributes
                    )
                )
                    return;
                if (
                    outputSlot.Itemstack.StackSize + outStack.StackSize
                    > outputSlot.MaxSlotStackSize
                )
                    return;

                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                    cookingSlotsProvider.Slots[i].Itemstack = null;

                outputSlot.Itemstack.StackSize += outStack.StackSize;
            }
            else
            {
                for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
                    cookingSlotsProvider.Slots[i].Itemstack = null;

                outputSlot.Itemstack = outStack;
            }

            outputSlot.MarkDirty();
        }
    }
}
